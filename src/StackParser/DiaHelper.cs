using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices.CustomMarshalers;

namespace StackParser
{
    class DiaHelper
    {
        static Dictionary<IDiaSession, IDiaDataSource> sessionMap = new Dictionary<IDiaSession, IDiaDataSource>();
        public static IDiaSession LoadPDB(string pdbFileName)
        {
            string msg;
            IDiaDataSource source;
            string curDir = System.IO.Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
            string msdDIADllPath = System.IO.Path.Combine(curDir, "msdia140.dll");
            if (!MSDebugInterfaceAccessFactory.TryCreate<IDiaDataSource>(msdDIADllPath, out source, out msg, new Guid("e6756135-1e65-4d17-8576-610761398c3c")))
            {
                throw new InvalidOperationException("Could not load dia from path " + msdDIADllPath);
            }

            IDiaSession session;
            source.loadDataFromPdb(pdbFileName);
            source.openSession(out session);
            sessionMap.Add(session, source);
            return session;
        }

        public static void ReleaseSession(IDiaSession session)
        {
            IDiaDataSource source;
            if (sessionMap.TryGetValue(session, out source))
            {
                sessionMap.Remove(session);
                Marshal.FinalReleaseComObject(session);
                Marshal.FinalReleaseComObject(source);
            }
        }

        public static string GetMethodName(IDiaSession pdbSession, uint rva)
        {
            IDiaSymbol funcSym;
            pdbSession.findSymbolByRVA(rva, SymTagEnum.SymTagFunction, out funcSym);
            if (funcSym == null)
                pdbSession.findSymbolByRVA(rva, SymTagEnum.SymTagPublicSymbol, out funcSym);

            if (funcSym == null)
                return null;

            StringBuilder methodNameStr = new StringBuilder();

            // Append the method-name
            methodNameStr.Append(funcSym.GetUndecoratedNameEx(UndName.DebuggerInternal));

            // Append the offset of current instruction inside method
            methodNameStr.Append("+0x");
            methodNameStr.Append(Convert.ToString(rva - funcSym.GetRelativeVirtualAddress(), 16));

            // Append the source line
            IDiaEnumLineNumbers lines = null;
            IDiaSourceFile sourceFile = null;
            pdbSession.findLinesByRVA(rva, 1, out lines);
            int i;
            if (lines != null && lines.count == 1)
            {
                IDiaLineNumber line = lines.Item(0);
                if (line != null)
                {
                    sourceFile = line.SourceFile;
                    methodNameStr.Append(" (");
                    methodNameStr.Append(sourceFile.FileName);
                    methodNameStr.Append(", line ");
                    methodNameStr.Append(line.LineNumber);
                    methodNameStr.Append(")");

                    i = Marshal.ReleaseComObject(line);
                    if (i != 0)
                        Console.WriteLine("RefCount(funcSym) == {0}", i);
                }
            }
            i = Marshal.ReleaseComObject(funcSym);
            if (i != 0)
                Console.WriteLine("RefCount(funcSym) == {0}", i);
            if (lines != null)
            {              
                if (sourceFile != null)
                {
                    i = Marshal.ReleaseComObject(sourceFile);
                    if (i != 0)
                        Console.WriteLine("RefCount(sourceFile) == {0}", i);

                }
                // line?
                i = Marshal.ReleaseComObject(lines);
                if (i != 0)
                    Console.WriteLine("RefCount(lines) == {0}", i);
            }

            //return funcSym.GetName();
            return methodNameStr.ToString();
        }
    }

    public static class MSDebugInterfaceAccessFactory
    {
        /// <summary>
        /// Creates a stackwalker out of msdia specified by MSDiaDllPath.
        /// </summary>
        /// <param name="source"></param>
        /// <param name="errorMessage"></param>
        /// <param name="specifiedGuid">This will be used to create the stackwalker in DllGetClassObject</param>
        /// <param anem="T"</param> The DIA type parameter. Currently the interfaces supported are IDiaDataSource, IDiaStackWalker
        /// <returns>True if sucessful, false if not</returns>
        public static bool TryCreate<T>(string msdDIADllPath, out T source, out string errorMessage, Guid diaSourceClsid)
            where T : class
        {
            if (!string.IsNullOrWhiteSpace(msdDIADllPath) && !System.IO.File.Exists(msdDIADllPath))
            {
                source = default(T);
                errorMessage = "Path to msdia not found - " + msdDIADllPath;
                return false;
            }
            return ComDllObjectFactory.LoadAndCreate<T>(msdDIADllPath, out source, out errorMessage, diaSourceClsid);
        }
    }

    public static class ComDllObjectFactory
    {

        /// <summary>
        /// The signature of the DllGetClassObject, COM entriy point in DLLs, that allows to create objects from that DLL
        /// </summary>
        ///
        [UnmanagedFunctionPointer(System.Runtime.InteropServices.CallingConvention.StdCall)]
        public delegate int DllGetClassObjectFunction([In] ref Guid clsid, [In] ref Guid riid, [Out, MarshalAs(UnmanagedType.Interface)] out object iFace);

        [ComImport]
        [Guid("00000001-0000-0000-C000-000000000046")]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        private interface IClassFactory
        {
            [return: MarshalAs(UnmanagedType.Interface)]
            object CreateInstance(
                [MarshalAs(UnmanagedType.IUnknown)] object punkOuter,
                [In, MarshalAs(UnmanagedType.LPStruct)] Guid iid);

            void LockServer(bool fLock);
        }

        [DllImportAttribute("kernel32.dll")]
        public static extern IntPtr LoadLibraryEx(String fileName, int hFile, uint dwFlags);
        public static IntPtr LoadLibrary(string fileName)
        {
            return LoadLibraryEx(fileName, 0, 0);
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Ansi, ExactSpelling = true)]
        public static extern IntPtr GetProcAddress(
            IntPtr hModule,
            string procName);

        /// <summary>
        /// Creates an object from a given COM dll by a class id and a given interface
        /// </summary>
        public static bool LoadAndCreate<T>(string dllPath, out T source, out string errorMessage, Guid classId)
            where T : class
        {
            if (string.IsNullOrWhiteSpace(dllPath) && !System.IO.File.Exists(dllPath))
            {
                source = null;
                errorMessage = "Path to a dll not found - " + dllPath;
                return false;
            }

            IntPtr dllHandle = LoadLibrary(dllPath);
            if (dllHandle == IntPtr.Zero)
            {
                source = null;
                errorMessage = "Could not load library " + dllPath;
                return false;
            }

            try
            {
                IntPtr fp = GetProcAddress(dllHandle, "DllGetClassObject");
                DllGetClassObjectFunction getClassObj = (DllGetClassObjectFunction)Marshal.GetDelegateForFunctionPointer(fp, typeof(DllGetClassObjectFunction));
                object c;
                Guid diaSourceClassGuid = classId;

                Guid classFactoryGuid = typeof(IClassFactory).GUID;
                int hr = getClassObj(ref diaSourceClassGuid, ref classFactoryGuid, out c);

                if (hr != 0)
                {
                    throw new COMException("Could not get object for CLSID " + classId, hr);
                }

                IClassFactory factory = c as IClassFactory;
                T diaSource = (T)factory.CreateInstance(null, typeof(T).GUID);
                // the cast to T increased the reference count
                //int cnt = Marshal.ReleaseComObject(diaSource);
                //if (cnt != 1)
                //    Console.WriteLine("diaSource.RefCount not 1, but {0}", cnt);
                source = diaSource;
                errorMessage = "";
                return true;
            }
            catch (Exception e)
            {
                errorMessage = string.Format("Could not get {0} from the library {1}. Details: {2}", typeof(T).Name, dllPath, e);
                source = null;
                return false;
            }
        }
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("79F1BB5F-B66E-48E5-B6A9-1545C323CA3D")]
    public interface IDiaDataSource
    {
        //[DispId(1)]
        //void loadDataFromPdb([In, MarshalAs(UnmanagedType.LPWStr)] string pdbPath);

        //[DispId(5)]
        //void openSession([MarshalAs(UnmanagedType.Interface)] out IDiaSession ppSession);

        string lastError { [return: MarshalAs(UnmanagedType.BStr)] get; }
        void loadDataFromPdb([In, MarshalAs(UnmanagedType.LPWStr)] string pdbPath);
        void loadAndValidateDataFromPdb([In, MarshalAs(UnmanagedType.LPWStr)] string pdbPath, [In] ref Guid pcsig70, [In] uint sig, [In] uint age);
        void loadDataForExe([In, MarshalAs(UnmanagedType.LPWStr)] string executable, [In, MarshalAs(UnmanagedType.LPWStr)] string searchPath, [In, MarshalAs(UnmanagedType.IUnknown)] object pCallback);
        void loadDataFromIStream([In, MarshalAs(UnmanagedType.Interface)] System.Runtime.InteropServices.ComTypes.IStream pIStream);
        void openSession([MarshalAs(UnmanagedType.Interface)] out IDiaSession ppSession);
        void loadDataFromCodeViewInfo([In, MarshalAs(UnmanagedType.LPWStr)] string executable, [In, MarshalAs(UnmanagedType.LPWStr)] string searchPath, [In] uint cbCvInfo,
                [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 2)] byte[] pbCvInfo,
                [In, MarshalAs(UnmanagedType.IUnknown)] object pCallback);
        void loadDataFromMiscInfo([In, MarshalAs(UnmanagedType.LPWStr)] string executable, [In, MarshalAs(UnmanagedType.LPWStr)] string searchPath,
                [In] uint timeStampExe, [In] uint timeStampDbg, [In] uint sizeOfExe, [In] uint cbMiscInfo, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 5)] byte[] pbMiscInfo,
                  [In, MarshalAs(UnmanagedType.IUnknown)] object pCallback);
    }



    [ComImport, Guid("2F609EE1-D1C8-4E24-8288-3326BADCD211"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDiaSession
    {
        ulong loadAddress { get; [param: In]  set; }
        IDiaSymbol globalScope { [return: MarshalAs(UnmanagedType.Interface)]  get; }
        void getEnumTables();

        void getSymbolsByAddr();

        void findChildren();

        void findChildrenEx();

        void findChildrenExByAddr();

        void findChildrenExByVA();

        void findChildrenExByRVA();

        void findSymbolByAddr();

        void findSymbolByRVA([In] uint rva, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol);

        void findSymbolByVA([In] long va, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol);

        void findSymbolByToken([In] uint token, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol);

        void symsAreEquiv([In, MarshalAs(UnmanagedType.Interface)] IDiaSymbol symbolA, [In, MarshalAs(UnmanagedType.Interface)] IDiaSymbol symbolB);

        IDiaSymbol symbolById([In] uint id);

        void findSymbolByRVAEx([In] uint rva, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol, out int displacement);

        void findSymbolByVAEx([In] long va, [In] SymTagEnum symTag, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol ppSymbol, out int displacement);

        void findFile();

        void findFileById([In] uint uniqueId, [MarshalAs(UnmanagedType.Interface)] out IDiaSourceFile ppResult);

        void findLines([In, MarshalAs(UnmanagedType.Interface)] IDiaSymbol compiland, [In, MarshalAs(UnmanagedType.Interface)] IDiaSourceFile file, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findLinesByAddr([In] uint seg, [In] uint offset, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);

        void findLinesByRVA([In] uint rva, [In] uint length, [MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppResult);
    }

    [Flags]
    public enum UndName : uint
    {
        DebuggerInternal = 0x80000000   // only used by debuggers when dealing with symbols from ProjectN PDB
    }

    public enum SymTagEnum
    {
        SymTagNull,
        SymTagExe,
        SymTagCompiland,
        SymTagCompilandDetails,
        SymTagCompilandEnv,
        SymTagFunction,
        SymTagBlock,
        SymTagData,
        SymTagAnnotation,
        SymTagLabel,
        SymTagPublicSymbol,
        SymTagUDT,
        SymTagEnum,
        SymTagFunctionType,
        SymTagPointerType,
        SymTagArrayType,
        SymTagBaseType,
        SymTagTypedef,
        SymTagBaseClass,
        SymTagFriend,
        SymTagFunctionArgType,
        SymTagFuncDebugStart,
        SymTagFuncDebugEnd,
        SymTagUsingNamespace,
        SymTagVTableShape,
        SymTagVTable,
        SymTagCustom,
        SymTagThunk,
        SymTagCustomType,
        SymTagManagedType,
        SymTagDimension,
        SymTagCallSite,
        SymTagInlineSite,
        SymTagBaseInterface,
        SymTagVectorType,
        SymTagMatrixType,
        SymTagHLSLType,
        SymTagMax
    }

    [ComImport, Guid("CB787B2F-BD6C-4635-BA52-933126BD2DCD"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDiaSymbol
    {
        [DispId(0)]
        uint GetSymIndexId();

        [DispId(1)]
        SymTagEnum GetSymTag();

        [DispId(2)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetName();

        [DispId(3)]
        IDiaSymbol GetLexicalParent();

        [DispId(4)]
        IDiaSymbol GetClassParent();

        [DispId(5)]
        IDiaSymbol GetType();

        [DispId(6)]
        int GetDataKind();

        [DispId(7)]
        int GetLocationType();

        [DispId(8)]
        uint GetAddressSection();

        [DispId(9)]
        uint GetAddressOffset();

        [DispId(10)]
        uint GetRelativeVirtualAddress();

        [DispId(11)]
        long GetVirtualAddress();

        [DispId(12)]
        int GetRegisterId();

        [DispId(13)]
        int GetOffset();

        [DispId(14)]
        ulong GetLength();

        [DispId(15)]
        [PreserveSig]
        int GetSlot(out int slotIndex);

        [DispId(16)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetVolatileType();

        [DispId(17)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetConstType();

        [DispId(18)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetUnalignedType();

        [DispId(19)]
        uint GetAccess();

        [DispId(20)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetLibraryName();

        [DispId(21)]
        uint GetPlatform();

        [DispId(22)]
        uint GetLanguage();

        [DispId(23)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetEditAndContinueEnabled();

        [DispId(24)]
        uint GetFrontEndMajor();

        [DispId(25)]
        uint GetFrontEndMinor();

        [DispId(26)]
        uint GetFrontEndBuild();

        [DispId(27)]
        uint GetBackEndMajor();

        [DispId(28)]
        uint GetBackEndMinor();

        [DispId(29)]
        uint GetBackEndBuild();

        [DispId(30)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetSourceFileName();

        [DispId(31)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetUnused();

        [DispId(32)]
        uint GetThunkOrdinal();

        [DispId(33)]
        int GetThisAdjust();

        [DispId(34)]
        uint GetVirtualBaseOffset();

        [DispId(35)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetVirtual();

        [DispId(36)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetIntro();

        [DispId(37)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetPure();

        [DispId(38)]
        uint GetCallingConvention();

        [DispId(39)]
        [return: MarshalAs(UnmanagedType.Struct)]
        object GetValue();

        [DispId(40)]
        int GetBaseType();

        [DispId(41)]
        uint GetToken();

        [DispId(42)]
        uint GetTimeStamp();

        [DispId(43)]
        Guid GetGuid();

        [DispId(44)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetSymbolsFileName();

        [DispId(46)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetReference();

        [DispId(47)]
        int GetCount();

        [DispId(49)]
        int GetBitPosition();

        [DispId(50)]
        IDiaSymbol GetArrayIndexType();

        [DispId(51)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetPacked();

        [DispId(52)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetConstructor();

        [DispId(53)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetOverloadedOperator();

        [DispId(54)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetNested();

        [DispId(55)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetHasNestedTypes();

        [DispId(56)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetHasAssignmentOperator();

        [DispId(57)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetHasCastOperator();

        [DispId(58)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetScoped();

        [DispId(59)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetVirtualBaseClass();

        [DispId(60)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetIndirectVirtualBaseClass();

        [DispId(61)]
        int GetVirtualBasePointerOffset();

        [DispId(62)]
        IDiaSymbol GetVirtualTableShape();

        [DispId(64)]
        uint GetLexicalParentId();

        [DispId(65)]
        uint GetClassParentId();

        [DispId(66)]
        uint GetTypeId();

        [DispId(67)]
        uint GetArrayIndexTypeId();

        [DispId(68)]
        uint GetVirtualTableShapeId();

        [DispId(69)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetCode();

        [DispId(70)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetFunction();

        [DispId(71)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetManaged();

        [DispId(72)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetMsil();

        [DispId(73)]
        uint GetVirtualBaseDispIndex();

        [DispId(74)]
        [return: MarshalAs(UnmanagedType.BStr)]
        string GetUndecoratedName();

        [DispId(75)]
        uint GetAge();

        [DispId(76)]
        uint GetSignature();

        [DispId(77)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetCompilerGenerated();

        [DispId(78)]
        [return: MarshalAs(UnmanagedType.Bool)]
        bool GetAddressTaken();

        [DispId(79)]
        uint GetRank();

        [DispId(80)]
        IDiaSymbol GetLowerBound();

        [DispId(81)]
        IDiaSymbol GetUpperBound();

        [DispId(82)]
        uint GetLowerBoundId();

        [DispId(83)]
        uint GetUpperBoundId();

        [PreserveSig]
        int GetDataBytes(int cbData, out int pcbData, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] byte[] data);

        void FindChildren(SymTagEnum symTag, [In, MarshalAs(UnmanagedType.BStr)] string name, uint compareFlags);

        void FindChildrenEx(SymTagEnum symTag, [In, MarshalAs(UnmanagedType.BStr)] string name, uint compareFlags);

        void FindChildrenExByAddr(SymTagEnum symTag, [In, MarshalAs(UnmanagedType.BStr)] string name, uint compareFlags, uint isect, uint offset);

        void FindChildrenExByVA(SymTagEnum symTag, [In, MarshalAs(UnmanagedType.BStr)] string name, uint compareFlags, ulong va);

        void FindChildrenExByRVA(SymTagEnum symTag, [In, MarshalAs(UnmanagedType.BStr)] string name, uint compareFlags, uint rva);

        [DispId(84)]
        uint GetTargetSection();

        [DispId(85)]
        uint GetTargetOffset();

        [DispId(86)]
        uint GetTargetRelativeVirtualAddress();

        [DispId(87)]
        ulong GetTargetVirtualAddress();

        [DispId(88)]
        uint GetMachineType();

        [DispId(89)]
        uint GetOemId();

        [DispId(90)]
        uint GetOemSymbolId();

        void GetTypes(int cTypes, out int pcTypes, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] IDiaSymbol[] pTypes);

        void GetTypeIds(int cTypeIds, out int pcTypeIds, [Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] int[] pdwTypeIds);

        [DispId(91)]
        IDiaSymbol GetObjectPointerType();

        [DispId(92)]
        int GetUdtKind();

        [return: MarshalAs(UnmanagedType.BStr)]
        string GetUndecoratedNameEx(UndName undecorateOptions);
    }

    [ComImport, Guid("FE30E878-54AC-44F1-81BA-39DE940F6052"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown), DefaultMember("Item")]
    public interface IDiaEnumLineNumbers
    {
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "", MarshalTypeRef = typeof(EnumeratorToEnumVariantMarshaler), MarshalCookie = "")]
        IEnumerator GetEnumerator();
        int count { get; }
        [return: MarshalAs(UnmanagedType.Interface)]
        IDiaLineNumber Item([In] int index);
        void Next([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaLineNumber rgelt, out uint pceltFetched);
        void Skip([In] uint celt);
        void Reset();
        void Clone([MarshalAs(UnmanagedType.Interface)] out IDiaEnumLineNumbers ppenum);
    }

    [ComImport, Guid("B388EB14-BE4D-421D-A8A1-6CF7AB057086"), InterfaceType((short)1)]
    public interface IDiaLineNumber
    {
        IDiaSymbol Compiland { get; }
        IDiaSourceFile SourceFile { get; }
        int LineNumber { get; }
        int LineNumberEnd { get; }
        int ColumnNumber { get; }
        int ColumnNumberEnd { get; }
        int AddressSection { get; }
        int AddressOffset { get; }
        int RelativeVirtualAddress { get; }
        long VirtualAddress { get; }
        int Length { get; }
        int SourceFileId { get; }
        int Statement { get; }
        int CompilandId { get; }
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("A2EF5353-F5A8-4EB3-90D2-CB526ACB3CDD")]
    public interface IDiaSourceFile
    {
        int UniqueId { get; }
        string FileName { [return: MarshalAs(UnmanagedType.BStr)] get; }
        int ChecksumType { get; }
        IDiaEnumSymbols Compilands { get; }
        void GetChecksum(int cbData, out int pcbData, [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 0)] byte[] pbData);
    }

    [ComImport, InterfaceType(ComInterfaceType.InterfaceIsIUnknown), Guid("CAB72C48-443B-48F5-9B0B-42F0820AB29A"), DefaultMember("Item")]
    public interface IDiaEnumSymbols
    {
        [return: MarshalAs(UnmanagedType.CustomMarshaler, MarshalType = "", MarshalTypeRef = typeof(EnumeratorToEnumVariantMarshaler), MarshalCookie = "")]
        IEnumerator GetEnumerator();
        int count { get; }
        [return: MarshalAs(UnmanagedType.Interface)]
        IDiaSymbol Item([In] int index);
        void Next([In] uint celt, [MarshalAs(UnmanagedType.Interface)] out IDiaSymbol rgelt, out uint pceltFetched);
        void Skip([In] uint celt);
        void Reset();
        void Clone([MarshalAs(UnmanagedType.Interface)] out IDiaEnumSymbols ppenum);
    }
}
