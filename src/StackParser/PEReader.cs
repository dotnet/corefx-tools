using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System.Diagnostics;

namespace StackParser
{
    /// <summary>
    /// An interface for reading bytes from a module image
    /// </summary>
    public interface IModuleReader
    {
        int ReadAtOffset(byte[] fileBytes, int offset);
        PEStreamFormat Format { get; }
        int Size { get; }
    }
    /// <summary>
    /// Describes the layout of PE information within a stream
    /// </summary>
    public enum PEStreamFormat
    {
        // The format of a PE file as it exists on disk, as speced by CLI.
        // In this format RVAs must be mapped to stream contents using section header mapping information.
        DiskFormat,
        // The canonical format of PE data as it is layed out in memory by the
        // OS loader. In this format RVAs can be used as direct offsets into the stream.
        MemoryLayoutFormat
    }
    public class StreamModuleReader : IModuleReader
    {
        Stream m_peStream;

        public StreamModuleReader(Stream peStream)
        {
            m_peStream = peStream;
        }

        public int ReadAtOffset(byte[] fileBytes, int offset)
        {
            m_peStream.Seek(offset, SeekOrigin.Begin);
            int bytesReadTotal = 0;
            int bytesRead = 0;
            do
            {
                bytesRead = m_peStream.Read(fileBytes, bytesReadTotal, fileBytes.Length - bytesReadTotal);
                bytesReadTotal += bytesRead;
            } while (bytesReadTotal != fileBytes.Length && bytesRead != 0);
            return bytesReadTotal;
        }

        public PEStreamFormat Format { get { return PEStreamFormat.DiskFormat; } }

        public int Size { get { return (int)m_peStream.Length; } }
    }

    public enum GetRuntimeFunctionEntryResult
    {
        Found,
        DoesNotExist,
        MissingMemory
    }

    public class RuntimeFunctionEntry : IComparable<RuntimeFunctionEntry>
    {
        /// <summary>
        /// The RVA where code for this function starts
        /// </summary>
        public int BeginAddress;
        /// <summary>
        /// The RVA where code for this function ends
        /// </summary>
        public int EndAddress;
        public int UnwindData;

        public virtual int Size { get { throw new NotImplementedException(); } }

        public virtual int CompareTo(RuntimeFunctionEntry other)
        {
            return BeginAddress.CompareTo(other.BeginAddress);
        }

        public virtual void WriteTo(BinaryWriter writer)
        {
            throw new NotImplementedException();
        }

        public static int GetSize(ImageFileMachine arch)
        {
            if (arch == ImageFileMachine.ARM)
            {
                return ARMImageRuntimeFunctionEntry.GetSize();
            }
            else
            {
                return AMD64ImageRuntimeFunctionEntry.GetSize();
            }
        }

        public static RuntimeFunctionEntry ReadFrom(BinaryReader r, ImageFileMachine arch)
        {
            if (arch == ImageFileMachine.ARM)
            {
                return ARMImageRuntimeFunctionEntry.ReadFrom(r);
            }
            else
            {
                return AMD64ImageRuntimeFunctionEntry.ReadFrom(r);
            }
        }
    }

    public class ARMImageRuntimeFunctionEntry : RuntimeFunctionEntry
    {
        public static ARMImageRuntimeFunctionEntry ReadFrom(BinaryReader r)
        {
            ARMImageRuntimeFunctionEntry entry = new ARMImageRuntimeFunctionEntry();
            entry.BeginAddress = r.ReadInt32();
            entry.UnwindData = r.ReadInt32();
            return entry;
        }
        public static int GetSize() { return 8; }

        public override int Size { get { return 8; } }
        public override void WriteTo(BinaryWriter writer)
        {
            writer.Write(BeginAddress);
            writer.Write(UnwindData);
        }
    }

    public class AMD64ImageRuntimeFunctionEntry : RuntimeFunctionEntry
    {
        public static AMD64ImageRuntimeFunctionEntry ReadFrom(BinaryReader r)
        {
            AMD64ImageRuntimeFunctionEntry entry = new AMD64ImageRuntimeFunctionEntry();
            entry.BeginAddress = r.ReadInt32();
            entry.EndAddress = r.ReadInt32();
            entry.UnwindData = r.ReadInt32();
            return entry;
        }
        public static int GetSize() { return 12; }

        public override int Size { get { return 12; } }
        public override void WriteTo(BinaryWriter writer)
        {
            writer.Write(BeginAddress);
            writer.Write(EndAddress);
            writer.Write(UnwindData);
        }
    }

    public class PEReaderException : Exception
    {
        public PEReaderException(string message) : base(message) { }
    }


    /// <summary>
    /// A very basic PE reader that can extract a few useful pieces of information
    /// </summary>
    public class PEReader
    {


        // PE file
        IModuleReader m_peReader;

        // cached information from the PE file
        int peHeaderOffset = 0;
        int optionalHeaderDirectoryEntriesOffset = 0;

        // PE file contents starting from the PEHeaderOffset
        // 4 byte signature
        COFFFileHeader? coffHeader;
        // optional header standard fields
        // optional header additional fields
        OptionalHeaderDirectoryEntries? optionalHeaderDirectoryEntries;
        SectionHeader[] sectionHeaders;
        COR20Header? cor20header;
        CodeViewDebugData codeViewDebugData;
        RuntimeFunctionEntry[] m_pData;

        public PEReader(IModuleReader peReader)
        {
            m_peReader = peReader;
        }

        public PEReader(Stream peFileStream)
        {
            m_peReader = new StreamModuleReader(peFileStream);
        }

        public int TimeStamp
        {
            get
            {
                return ReadDwordAtFileOffset(PEHeaderOffset + 8);
            }
        }

        public int SizeOfImage
        {
            get
            {
                return ReadDwordAtFileOffset(PEHeaderOffset + 80);
            }
        }

        int PEHeaderOffset
        {
            get
            {
                if (peHeaderOffset == 0)
                {
                    peHeaderOffset = ReadDwordAtFileOffset(0x3c);
                }
                return peHeaderOffset;
            }
        }

        int OptionalHeaderDirectoryEntriesOffset
        {
            get
            {
                if (optionalHeaderDirectoryEntriesOffset == 0)
                {
                    int peOptionalHeaderOffset = PEHeaderOffset + 4 + PEFileConstants.SizeofCOFFFileHeader;
                    PEMagic magic = (PEMagic) ReadWordAtFileOffset(peOptionalHeaderOffset);
                    if (magic == PEMagic.PEMagic32)
                    {
                        optionalHeaderDirectoryEntriesOffset = peOptionalHeaderOffset + PEFileConstants.SizeofOptionalHeaderStandardFields32 +
                            PEFileConstants.SizeofOptionalHeaderNTAdditionalFields32;
                    }
                    else
                    {
                        optionalHeaderDirectoryEntriesOffset = peOptionalHeaderOffset + PEFileConstants.SizeofOptionalHeaderStandardFields64 +
                            PEFileConstants.SizeofOptionalHeaderNTAdditionalFields64;
                    }
                }
                return optionalHeaderDirectoryEntriesOffset;
            }
        }

        int SectionHeadersOffset
        {
            get
            {
                return OptionalHeaderDirectoryEntriesOffset + PEFileConstants.SizeofOptionalHeaderDirectoriesEntries;
            }
        }

        public COFFFileHeader COFFFileHeader
        {
            get
            {
                if (!coffHeader.HasValue)
                {
                    coffHeader = ReadCOFFFileHeader();
                }
                return coffHeader.Value;
            }
        }

        public OptionalHeaderDirectoryEntries OptionalHeaderDirectoryEntries
        {
            get
            {
                if (!optionalHeaderDirectoryEntries.HasValue)
                {
                    optionalHeaderDirectoryEntries = ReadOptionalHeaderDirectoryEntries();
                }
                return optionalHeaderDirectoryEntries.Value;
            }
        }

        public SectionHeader[] SectionHeaders
        {
            get
            {
                if (sectionHeaders == null)
                {
                    sectionHeaders = ReadSectionHeaders();
                }
                return sectionHeaders;
            }
        }

        public COR20Header Cor20Header
        {
            get
            {
                if (cor20header == null)
                {
                    cor20header = ReadCOR20Header();
                }
                return cor20header.Value;
            }
        }

        COFFFileHeader ReadCOFFFileHeader()
        {
            byte[] bytes = new byte[PEFileConstants.SizeofCOFFFileHeader];
            ReadBytesAtFileOffset(bytes, PEHeaderOffset + 4);
            BinaryReader reader = new BinaryReader(new MemoryStream(bytes));

            COFFFileHeader header = new COFFFileHeader();
            header.Machine = reader.ReadUInt16();
            header.NumberOfSections = reader.ReadInt16();
            header.TimeDateStamp = reader.ReadInt32();
            header.PointerToSymbolTable = reader.ReadInt32();
            header.NumberOfSymbols = reader.ReadInt32();
            header.SizeOfOptionalHeader = reader.ReadInt16();
            header.Characteristics = reader.ReadUInt16();
            return header;
        }

        OptionalHeaderStandardFields ReadOptionalHeaderStandardFields32()
        {
            byte[] bytes = new byte[PEFileConstants.SizeofOptionalHeaderStandardFields32];
            ReadBytesAtFileOffset(bytes, PEHeaderOffset + 4 + PEFileConstants.SizeofCOFFFileHeader);
            BinaryReader reader = new BinaryReader(new MemoryStream(bytes));

            OptionalHeaderStandardFields header = new OptionalHeaderStandardFields();
            header.PEMagic = (PEMagic)reader.ReadUInt16();
            header.MajorLinkerVersion = reader.ReadByte();
            header.MinorLinkerVersion = reader.ReadByte();
            header.SizeOfCode = reader.ReadInt32();
            header.SizeOfInitializedData = reader.ReadInt32();
            header.SizeOfUninitializedData = reader.ReadInt32();
            header.RVAOfEntryPoint = reader.ReadInt32();
            header.BaseOfCode = reader.ReadInt32();
            header.BaseOfData = reader.ReadInt32();
            return header;
        }

        OptionalHeaderStandardFields ReadOptionalHeaderStandardFields64()
        {
            byte[] bytes = new byte[PEFileConstants.SizeofOptionalHeaderStandardFields64];
            ReadBytesAtFileOffset(bytes, PEHeaderOffset + 4 + PEFileConstants.SizeofCOFFFileHeader);
            BinaryReader reader = new BinaryReader(new MemoryStream(bytes));

            OptionalHeaderStandardFields header = new OptionalHeaderStandardFields();
            header.PEMagic = (PEMagic)reader.ReadUInt16();
            header.MajorLinkerVersion = reader.ReadByte();
            header.MinorLinkerVersion = reader.ReadByte();
            header.SizeOfCode = reader.ReadInt32();
            header.SizeOfInitializedData = reader.ReadInt32();
            header.SizeOfUninitializedData = reader.ReadInt32();
            header.RVAOfEntryPoint = reader.ReadInt32();
            header.BaseOfCode = reader.ReadInt32();
            return header;
        }

        OptionalHeaderDirectoryEntries ReadOptionalHeaderDirectoryEntries()
        {
            byte[] bytes = new byte[PEFileConstants.SizeofOptionalHeaderDirectoriesEntries];
            ReadBytesAtFileOffset(bytes, OptionalHeaderDirectoryEntriesOffset);
            BinaryReader reader = new BinaryReader(new MemoryStream(bytes));

            OptionalHeaderDirectoryEntries header = new OptionalHeaderDirectoryEntries();
            header.ExportTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ExportTableDirectory.Size = reader.ReadUInt32();
            header.ImportTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ImportTableDirectory.Size = reader.ReadUInt32();
            header.ResourceTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ResourceTableDirectory.Size = reader.ReadUInt32();
            header.ExceptionTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ExceptionTableDirectory.Size = reader.ReadUInt32();
            header.CertificateTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.CertificateTableDirectory.Size = reader.ReadUInt32();
            header.BaseRelocationTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.BaseRelocationTableDirectory.Size = reader.ReadUInt32();
            header.DebugTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.DebugTableDirectory.Size = reader.ReadUInt32();
            header.CopyrightTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.CopyrightTableDirectory.Size = reader.ReadUInt32();
            header.GlobalPointerTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.GlobalPointerTableDirectory.Size = reader.ReadUInt32();
            header.ThreadLocalStorageTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ThreadLocalStorageTableDirectory.Size = reader.ReadUInt32();
            header.LoadConfigTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.LoadConfigTableDirectory.Size = reader.ReadUInt32();
            header.BoundImportTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.BoundImportTableDirectory.Size = reader.ReadUInt32();
            header.ImportAddressTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ImportAddressTableDirectory.Size = reader.ReadUInt32();
            header.DelayImportTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.DelayImportTableDirectory.Size = reader.ReadUInt32();
            header.COR20HeaderTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.COR20HeaderTableDirectory.Size = reader.ReadUInt32();
            header.ReservedDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ReservedDirectory.Size = reader.ReadUInt32();
            return header;
        }

        List<DebugDirectory> ReadDebugDirectories()
        {
            // there might not be a debug directory, in which case return an empty list
            if (OptionalHeaderDirectoryEntries.DebugTableDirectory.RelativeVirtualAddress == 0)
                return new List<DebugDirectory>();

            BinaryReader reader;
            if (!TryDirectoryToReader(OptionalHeaderDirectoryEntries.DebugTableDirectory, out reader))
            {
                throw new PEReaderException("Unable to load DebugDirectory from PE");
            }
            int countDirectories = (int) (OptionalHeaderDirectoryEntries.DebugTableDirectory.Size / PEFileConstants.SizeofDebugDirectory);
            List<DebugDirectory> debugDirectories = new List<DebugDirectory>();
            for (int i = 0; i < countDirectories; i++)
            {
                DebugDirectory debugDir = new DebugDirectory();
                debugDir.Characteristics = reader.ReadUInt32();
                debugDir.TimeDateStamp = reader.ReadUInt32();
                debugDir.MajorVersion = reader.ReadUInt16();
                debugDir.MinorVersion = reader.ReadUInt16();
                debugDir.Type = (ImageDebugType)reader.ReadUInt32();
                debugDir.SizeOfData = reader.ReadUInt32();
                debugDir.AddressOfRawData = reader.ReadUInt32();
                debugDir.PointerToRawData = reader.ReadUInt32();
                debugDirectories.Add(debugDir);
            }

            return debugDirectories;
        }

        CodeViewDebugData ReadCodeViewDebugData(DebugDirectory debugDir)
        {
            Debug.Assert(debugDir.Type == ImageDebugType.CodeView);
            //Console.WriteLine("ReadCodeViewDebugData: Debug directory with AddressOfRawData 0x" + debugDir.AddressOfRawData.ToString("x"));

            // sanity check read size
            if (debugDir.SizeOfData > 1000)
            {
                Console.WriteLine("ReadCodeViewDebugData: SizeOfData > 1000 - assuming bad data");
                return null;
            }

            BinaryReader reader;
            if (!TryReadAtRVA((int)debugDir.AddressOfRawData, (int)debugDir.SizeOfData, out reader))
            {
                Console.WriteLine("ReadCodeViewDebugData: Unable to read rva=0x" + debugDir.AddressOfRawData.ToString("x") + " size=" +debugDir.SizeOfData);
                return null;
            }
            int signature = reader.ReadInt32();
            if (signature != PEFileConstants.CodeViewSignature)
            {
                Console.WriteLine("ReadCodeViewDebugData: Invalid codeview signature Expected: " + PEFileConstants.CodeViewSignature + " Actual: " + signature);
                return null;
            }
            CodeViewDebugData codeView = new CodeViewDebugData();
            codeView.Signature = new Guid(reader.ReadBytes(16));
            codeView.Age = (int)reader.ReadUInt32();
            codeView.PdbPath = reader.ReadNullTerminatedString(Encoding.UTF8);
            //Console.WriteLine("ReadCodeViewDebugData: PdbPath=" + codeView.PdbPath + " Age=" + codeView.Age + " Signature="+codeView.Signature);
            return codeView;
        }

        public CodeViewDebugData CodeViewDebugData
        {
            get
            {
                if (codeViewDebugData == null)
                {
                    List<DebugDirectory> debugDirs = ReadDebugDirectories();
                    // Console.WriteLine("CodeViewDebugData: Found " + debugDirs.Count + " debug dirs");
                    foreach (DebugDirectory dir in debugDirs)
                    {
                        if (dir.Type == ImageDebugType.CodeView)
                        {
                            // Console.WriteLine("CodeViewDebugData: Debug directory for 0x" + dir.AddressOfRawData.ToString("x") + " is code view");
                            codeViewDebugData = ReadCodeViewDebugData(dir);
                        }
                        else
                        {
                            // Console.WriteLine("CodeViewDebugData: Debug directory for 0x" + dir.AddressOfRawData.ToString("x") + " is not code view");
                        }
                    }
                }
                return codeViewDebugData;
            }
        }        

        RuntimeFunctionEntry[] PData
        {
            get
            {
                if (m_pData == null)
                {
                    int rfeSize = RuntimeFunctionEntry.GetSize((ImageFileMachine)this.COFFFileHeader.Machine);
                    int countEntries = (int)OptionalHeaderDirectoryEntries.ExceptionTableDirectory.Size / rfeSize;
                    List<RuntimeFunctionEntry> entries = new List<RuntimeFunctionEntry>();
                    int rva = OptionalHeaderDirectoryEntries.ExceptionTableDirectory.RelativeVirtualAddress;
                    for (int i = 0; i < countEntries; i++, rva += rfeSize)
                    {
                        // in to read from dumps which may have gaps in the collected memory we read
                        // each RuntimeFunctionEntry individually. Even if some are missing we will
                        // get the rest
                        BinaryReader pdataReader;
                        if (TryReadAtRVA(rva, rfeSize, out pdataReader))
                        {
                            entries.Add(RuntimeFunctionEntry.ReadFrom(pdataReader, (ImageFileMachine)this.COFFFileHeader.Machine));
                        }
                    }
                    m_pData = entries.ToArray();
                }
                return m_pData;
            }
        }

        public GetRuntimeFunctionEntryResult GetRuntimeFunctionEntry(int functionRVA, out RuntimeFunctionEntry entry)
        {
            RuntimeFunctionEntry[] entries = PData;
            entry = null;
            if (entries == null)
                return GetRuntimeFunctionEntryResult.MissingMemory;

            RuntimeFunctionEntry targetEntry = new RuntimeFunctionEntry();
            targetEntry.BeginAddress = functionRVA;
            int index = Array.BinarySearch(entries, targetEntry);

            if (index >= 0)  // found an entry whose start address matches exactly
                entry = entries[index];
            else if (~index > 0)
            {
                // ~index is the index of the first entry larger than targetEntry
                // ~index could be >= entries.Length, in which case there was no entry larger
                // than targetEntry
                int possibleEnclosingIndex = Math.Min(~index, entries.Length) - 1;
                if (entries[possibleEnclosingIndex].BeginAddress <= functionRVA)
                    entry = entries[possibleEnclosingIndex];
            }

            if (entry == null)
                return GetRuntimeFunctionEntryResult.DoesNotExist;

            if ((ImageFileMachine)this.COFFFileHeader.Machine == ImageFileMachine.AMD64 &&
                (entry.UnwindData & 0x1) == 0x1) // chained data
            {
                int parentEntryRva = entry.UnwindData & ~0x1;
                int basePDataRva = OptionalHeaderDirectoryEntries.ExceptionTableDirectory.RelativeVirtualAddress;
                int parentIndex = (parentEntryRva - basePDataRva) /
                    RuntimeFunctionEntry.GetSize((ImageFileMachine)this.COFFFileHeader.Machine);

                if (parentIndex < 0 || parentIndex >= entries.Length)
                    return GetRuntimeFunctionEntryResult.DoesNotExist; // bad pdata?

                entry = entries[parentIndex];
            }
            return GetRuntimeFunctionEntryResult.Found;
        }

        COR20Header ReadCOR20Header()
        {
            BinaryReader reader;
            if(!TryDirectoryToReader(OptionalHeaderDirectoryEntries.COR20HeaderTableDirectory, out reader))
            {
                throw new PEReaderException("Unable to load Cor20HeaderTableDirectory from PE");
            }

            COR20Header header = new COR20Header();
            header.CountBytes = reader.ReadInt32();
            header.MajorRuntimeVersion = reader.ReadUInt16();
            header.MinorRuntimeVersion = reader.ReadUInt16();
            header.MetaDataDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.MetaDataDirectory.Size = reader.ReadUInt32();
            header.COR20Flags = (COR20Flags)reader.ReadUInt32();
            header.EntryPointTokenOrRVA = reader.ReadUInt32();
            header.ResourcesDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ResourcesDirectory.Size = reader.ReadUInt32();
            header.StrongNameSignatureDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.StrongNameSignatureDirectory.Size = reader.ReadUInt32();
            header.CodeManagerTableDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.CodeManagerTableDirectory.Size = reader.ReadUInt32();
            header.VtableFixupsDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ExportAddressTableJumpsDirectory.Size = reader.ReadUInt32();
            header.ExportAddressTableJumpsDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ExportAddressTableJumpsDirectory.Size = reader.ReadUInt32();
            header.ManagedNativeHeaderDirectory.RelativeVirtualAddress = reader.ReadInt32();
            header.ManagedNativeHeaderDirectory.Size = reader.ReadUInt32();
            return header;
        }

        public PEExport[] ReadExports()
        {
            BinaryReader reader;
            if (!TryDirectoryToReader(OptionalHeaderDirectoryEntries.ExportTableDirectory, out reader))
            {
                throw new PEReaderException("Unable to load ExportDirectory from PE");
            }
            ExportDirectory ed = new ExportDirectory();
            ed.ExportFlags = reader.ReadUInt32();
            ed.DateTimeStamp = reader.ReadUInt32();
            ed.MajorVersion = reader.ReadUInt16();
            ed.MinorVersion = reader.ReadUInt16();
            ed.NameRva = reader.ReadUInt32();
            ed.OrdinalBase = reader.ReadUInt32();
            ed.AddressTablesEntries = reader.ReadUInt32();
            ed.NumberNamePointers = reader.ReadUInt32();
            ed.ExportAddressTableRva = reader.ReadUInt32();
            ed.NamePointerRva = reader.ReadUInt32();
            ed.OrdinalTableRva = reader.ReadUInt32();

            // read the export name pointer table
            reader = null;
            if (!TryReadAtRVA((int)ed.NamePointerRva, (int)ed.NumberNamePointers * 4, out reader))
            {
                throw new PEReaderException("Unable to read Export NamePointer table from PE");
            }
            int[] nameRvas = new int[ed.NumberNamePointers];
            for (int i = 0; i < ed.NumberNamePointers; i++)
            {
                nameRvas[i] = reader.ReadInt32();
            }

            // read the exports name table
            PEExport[] exports = new PEExport[ed.NumberNamePointers];
            for (int i = 0; i < ed.NumberNamePointers; i++)
            {
                // making some simplifying assumptions that all names are < 100 chars and
                // that a 100 char read won't go off then end of a mapped section. I'm not
                // sure if either is really true.
                reader = null;
                if (!TryReadAtRVA(nameRvas[i], 100, out reader))
                {
                    throw new PEReaderException("Unable to read Export name from PE");
                }
                exports[i].Name = reader.ReadNullTerminatedString(Encoding.ASCII);
            }

            // read the export ordinal table
            reader = null;
            if (!TryReadAtRVA((int)ed.OrdinalTableRva, (int)ed.NumberNamePointers * 2, out reader))
            {
                throw new PEReaderException("Unable to read Export OridnalTable from PE");
            }
            for (int i = 0; i < ed.NumberNamePointers; i++)
            {
                //This doesn't match up with what the PE spec describes, but it does seem to produce
                //the right data empirically
                //The spec claims that value encoded in the PE is the ordinal, and that you need to
                //subtract the ordinal base in order to get an index into the address table
                //In practice it seems that the value encoded in the PE is the index into the address
                //table and you need to add ordinal base in order to get the ordinal
                exports[i].Ordinal = (int)(reader.ReadUInt16() + ed.OrdinalBase);
            }

            // read the export address table
            reader = null;
            if (!TryReadAtRVA((int)ed.ExportAddressTableRva, (int)ed.AddressTablesEntries * 4, out reader))
            {
                throw new PEReaderException("Unable to read Export OridnalTable from PE");
            }
            int[] exportAddresses = new int[ed.AddressTablesEntries];
            for (int i = 0; i < ed.AddressTablesEntries; i++)
            {
                exportAddresses[i] = reader.ReadInt32();
            }

            //populate export addresses
            for (int i = 0; i < exports.Length; i++)
            {
                int ord = (int)(exports[i].Ordinal - ed.OrdinalBase);
                if (ord >= 0 && ord < exportAddresses.Length)
                    exports[i].Rva = exportAddresses[ord];
            }
            return exports;
        }

        SectionHeader[] ReadSectionHeaders()
        {
            int numberOfSections = COFFFileHeader.NumberOfSections;
            byte[] bytes = new byte[PEFileConstants.SizeofSectionHeader * numberOfSections];
            ReadBytesAtFileOffset(bytes, SectionHeadersOffset);
            BinaryReader reader = new BinaryReader(new MemoryStream(bytes));

            SectionHeader[] sectionHeaderArray = new SectionHeader[numberOfSections];
            for (int i = 0; i < numberOfSections; ++i)
            {
                sectionHeaderArray[i].Name = Encoding.UTF8.GetString(reader.ReadBytes(PEFileConstants.SizeofSectionName));
                sectionHeaderArray[i].VirtualSize = reader.ReadInt32();
                sectionHeaderArray[i].VirtualAddress = reader.ReadInt32();
                sectionHeaderArray[i].SizeOfRawData = reader.ReadInt32();
                sectionHeaderArray[i].OffsetToRawData = reader.ReadInt32();
                sectionHeaderArray[i].RVAToRelocations = reader.ReadInt32();
                sectionHeaderArray[i].PointerToLineNumbers = reader.ReadInt32();
                sectionHeaderArray[i].NumberOfRelocations = reader.ReadUInt16();
                sectionHeaderArray[i].NumberOfLineNumbers = reader.ReadUInt16();
                sectionHeaderArray[i].SectionCharacteristics = reader.ReadUInt32();
            }
            return sectionHeaderArray;
        }

        bool TryDirectoryToReader(DirectoryEntry directory, out BinaryReader reader)
        {
            return TryReadAtRVA(directory.RelativeVirtualAddress, (int)directory.Size, out reader);
        }

        public bool TryReadAtRVA(int rva, int size, out byte[] data)
        {
            data = null;
            // disk format requires RVA->file offset mapping
            if (m_peReader.Format == PEStreamFormat.DiskFormat)
            {
                foreach (SectionHeader sectionHeaderIter in SectionHeaders)
                {
                    if (sectionHeaderIter.VirtualAddress <= rva && rva < sectionHeaderIter.VirtualAddress + sectionHeaderIter.VirtualSize)
                    {
                        int relativeOffset = rva - sectionHeaderIter.VirtualAddress;
                        data = new byte[size];
                        return TryReadBytesAtFileOffset(data, sectionHeaderIter.OffsetToRawData + relativeOffset);
                    }
                }

                // although its not part of the section table, the OS does map the initial file headers into the address space and it is
                // reasonable to refer to them with RVAs. I'm not certain exactly what the OS maps in, but lets assume it includes everything
                // up through the section tables at least.
                int endSectionTablesOffset = SectionHeadersOffset + PEFileConstants.SizeofSectionHeader * COFFFileHeader.NumberOfSections;
                if (rva + size <= endSectionTablesOffset)
                {
                    data = new byte[size];
                    return TryReadBytesAtFileOffset(data, rva);
                }

                return false;
            }
            // in memory format can use RVA offsets directly
            else
            {
                data = new byte[size];
                return TryReadBytesAtFileOffset(data, rva);
            }
        }

        public bool TryReadAtRVA(int rva, int size, out BinaryReader reader)
        {
            byte[] data;
            if (TryReadAtRVA(rva, size, out data))
            {
                reader = new BinaryReader(new MemoryStream(data));
                return true;
            }
            else
            {
                reader = null;
                return false;
            }
        }

        public bool IsManagedBinary
        {
            get
            {
                DirectoryEntry de = OptionalHeaderDirectoryEntries.COR20HeaderTableDirectory;
                return de.RelativeVirtualAddress != 0;
            }
        }        

        int ReadDwordAtFileOffset(int fileOffset)
        {
            byte[] dword = new byte[4];
            ReadBytesAtFileOffset(dword, fileOffset);
            return BitConverter.ToInt32(dword, 0);
        }

        ushort ReadWordAtFileOffset(int fileOffset)
        {
            byte[] word = new byte[2];
            ReadBytesAtFileOffset(word, fileOffset);
            return BitConverter.ToUInt16(word, 0);
        }

        bool TryReadBytesAtFileOffset(byte[] bytes, int fileOffset)
        {
            return (bytes.Length == m_peReader.ReadAtOffset(bytes, fileOffset));
        }

        void ReadBytesAtFileOffset(byte[] bytes, int fileOffset)
        {
            if (bytes.Length != m_peReader.ReadAtOffset(bytes, fileOffset))
            {
                throw new PEReaderException("Unable to read at 0x" + fileOffset.ToString("x") + ", 0x" + bytes.Length.ToString("x") + " bytes");
            }
        }
    }
    internal static class BinaryReaderExtensions
    {
        // BinaryReader reads length prefixed strings but not null-terminated ones
        public static string ReadNullTerminatedString(this BinaryReader reader, Encoding encoding)
        {
            // make sure we are scanning something where a single 0 byte means end of string
            // although UTF8 does have code points that encode with more than one byte, byte 0 is never used except
            // to encode code point 0. 
            if (encoding != Encoding.UTF8 && encoding != Encoding.ASCII)
                throw new NotSupportedException("PEReader.ReadNullTerminatedString: Only UTF8 or ascii are supported for now");
            List<byte> stringBytes = new List<byte>();
            byte nextByte = reader.ReadByte();
            while (nextByte != 0)
            {
                stringBytes.Add(nextByte);
                nextByte = reader.ReadByte();
            }

            return encoding.GetString(stringBytes.ToArray(), 0, stringBytes.Count);
        }
    }
    internal static class PEFileConstants
    {
        internal const ushort DosSignature = 0x5A4D;     // MZ
        internal const int PESignatureOffsetLocation = 0x3C;
        internal const uint PESignature = 0x00004550;    // PE00
        internal const int BasicPEHeaderSize = PEFileConstants.PESignatureOffsetLocation;
        internal const int SizeofCOFFFileHeader = 20;
        internal const int SizeofOptionalHeaderStandardFields32 = 28;
        internal const int SizeofOptionalHeaderStandardFields64 = 24;
        internal const int SizeofOptionalHeaderNTAdditionalFields32 = 68;
        internal const int SizeofOptionalHeaderNTAdditionalFields64 = 88;
        internal const int NumberofOptionalHeaderDirectoryEntries = 16;
        internal const int SizeofOptionalHeaderDirectoriesEntries = 128;
        internal const int SizeofSectionHeader = 40;
        internal const int SizeofSectionName = 8;
        internal const int SizeofResourceDirectory = 16;
        internal const int SizeofResourceDirectoryEntry = 8;
        internal const int SizeofDebugDirectory = 28;
        internal const int CodeViewSignature = 0x53445352; // "RSDS" in little endian format
    }

    public struct COFFFileHeader
    {
        public ushort Machine;
        public short NumberOfSections;
        public int TimeDateStamp;
        public int PointerToSymbolTable;
        public int NumberOfSymbols;
        public short SizeOfOptionalHeader;
        public ushort Characteristics;
    }

    public struct OptionalHeaderStandardFields
    {
        public PEMagic PEMagic;
        public byte MajorLinkerVersion;
        public byte MinorLinkerVersion;
        public int SizeOfCode;
        public int SizeOfInitializedData;
        public int SizeOfUninitializedData;
        public int RVAOfEntryPoint;
        public int BaseOfCode;
        public int BaseOfData;
    }

    public struct DirectoryEntry
    {
        public int RelativeVirtualAddress;
        public uint Size;
    }

    public struct OptionalHeaderDirectoryEntries
    {
        public DirectoryEntry ExportTableDirectory;
        public DirectoryEntry ImportTableDirectory;
        public DirectoryEntry ResourceTableDirectory;
        public DirectoryEntry ExceptionTableDirectory;
        public DirectoryEntry CertificateTableDirectory;
        public DirectoryEntry BaseRelocationTableDirectory;
        public DirectoryEntry DebugTableDirectory;
        public DirectoryEntry CopyrightTableDirectory;
        public DirectoryEntry GlobalPointerTableDirectory;
        public DirectoryEntry ThreadLocalStorageTableDirectory;
        public DirectoryEntry LoadConfigTableDirectory;
        public DirectoryEntry BoundImportTableDirectory;
        public DirectoryEntry ImportAddressTableDirectory;
        public DirectoryEntry DelayImportTableDirectory;
        public DirectoryEntry COR20HeaderTableDirectory;
        public DirectoryEntry ReservedDirectory;
    }

    public struct SectionHeader
    {
        public string Name;
        public int VirtualSize;
        public int VirtualAddress;
        public int SizeOfRawData;
        public int OffsetToRawData;
        public int RVAToRelocations;
        public int PointerToLineNumbers;
        public ushort NumberOfRelocations;
        public ushort NumberOfLineNumbers;
        public uint SectionCharacteristics;
    }

    internal enum ImageDebugType : uint
    {
        Unknown = 0,
        Coff = 1,
        CodeView = 2,
        Fpo = 3,
        Misc = 4,
        Exception = 5,
        Fixup = 6,
        Borland = 9
    }

    internal struct DebugDirectory
    {
        public uint Characteristics;
        public uint TimeDateStamp;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public ImageDebugType Type;
        public uint SizeOfData;
        public uint AddressOfRawData;
        public uint PointerToRawData;
    }

    public class CodeViewDebugData
    {
        public Guid Signature;
        public int Age;
        public string PdbPath;
    }

    public enum COR20Flags : uint
    {
        ILOnly = 0x00000001,
        Bit32Required = 0x00000002,
        ILLibrary = 0x00000004,
        StrongNameSigned = 0x00000008,
        NativeEntryPoint = 0x00000010,
        TrackDebugData = 0x00010000,
    }

    public enum PEMagic : ushort
    {
        PEMagic32 = 0x010B,
        PEMagic64 = 0x020B,
    }



    public struct COR20Header
    {
        public int CountBytes;
        public ushort MajorRuntimeVersion;
        public ushort MinorRuntimeVersion;
        public DirectoryEntry MetaDataDirectory;
        public COR20Flags COR20Flags;
        public uint EntryPointTokenOrRVA;
        public DirectoryEntry ResourcesDirectory;
        public DirectoryEntry StrongNameSignatureDirectory;
        public DirectoryEntry CodeManagerTableDirectory;
        public DirectoryEntry VtableFixupsDirectory;
        public DirectoryEntry ExportAddressTableJumpsDirectory;
        public DirectoryEntry ManagedNativeHeaderDirectory;
    }

    public class ImageImportDescriptor
    {
        public const uint Size = 4 * 5;
        public uint Characteristics { get; private set; }
        public uint OriginalFirstThunk { get; private set; }
        public uint TimeDateStamp { get; private set; }
        public uint ForwarderChain { get; private set; }
        public uint Name { get; private set; }
        public uint FirstThunk { get; private set; }
        public ImageImportDescriptor(BinaryReader br)
        {
            this.Characteristics = br.ReadUInt32();
            if (this.Characteristics != 0)
            {
                this.OriginalFirstThunk = this.Characteristics;
                this.TimeDateStamp = br.ReadUInt32();
                this.ForwarderChain = br.ReadUInt32();
                this.Name = br.ReadUInt32();
                this.FirstThunk = br.ReadUInt32();
            }
            else
            {
                this.OriginalFirstThunk = 0;
                this.TimeDateStamp = 0;
                this.ForwarderChain = 0;
                this.Name = 0;
                this.FirstThunk = 0;
            }
        }
    }

    struct ExportDirectory
    {
        public uint ExportFlags;
        public uint DateTimeStamp;
        public ushort MajorVersion;
        public ushort MinorVersion;
        public uint NameRva;
        public uint OrdinalBase;
        public uint AddressTablesEntries;
        public uint NumberNamePointers;
        public uint ExportAddressTableRva;
        public uint NamePointerRva;
        public uint OrdinalTableRva;
    }

    public struct PEExport
    {
        public string Name;
        public int Ordinal;
        public int Rva;
    }

    [Flags]
    public enum ImageFileMachine : int
    {
        X86 = 0x014c,
        AMD64 = 0x8664,
        IA64 = 0x0200,
        ARM = 0x01c4, // IMAGE_FILE_MACHINE_ARMNT
    }

    public enum Platform
    {
        None = 0,
        X86 = 1,
        AMD64 = 2,
        IA64 = 3,
        ARM = 4,
    }

    /// <summary>
    /// Describes the ProcessorArchitecture in a SYSTEM_INFO field.
    /// This can also be reported by a dump file.
    /// </summary>
    public enum ProcessorArchitecture : ushort
    {
        PROCESSOR_ARCHITECTURE_INTEL = 0,
        PROCESSOR_ARCHITECTURE_MIPS = 1,
        PROCESSOR_ARCHITECTURE_ALPHA = 2,
        PROCESSOR_ARCHITECTURE_PPC = 3,
        PROCESSOR_ARCHITECTURE_SHX = 4,
        PROCESSOR_ARCHITECTURE_ARM = 5,
        PROCESSOR_ARCHITECTURE_IA64 = 6,
        PROCESSOR_ARCHITECTURE_ALPHA64 = 7,
        PROCESSOR_ARCHITECTURE_MSIL = 8,
        PROCESSOR_ARCHITECTURE_AMD64 = 9,
        PROCESSOR_ARCHITECTURE_IA32_ON_WIN64 = 10,
    }


}
