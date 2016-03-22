using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace StackParser
{
    /// <summary>
    /// An extension that adds symbol server support to MDbg. Loading this extension will
    /// cause all paths in the symbol search path to get interpretted as symbol stores
    /// in addition to flat directories. Also paths that start with srv* will now be interpretted
    /// 
    /// </summary>
#if NotNow
    [MDbgExtensionEntryPointClass]
    public abstract class SrcSrvExtension : CommandBase
    {
        public static void LoadExtension()
        {
            m_symbolServerSearchTechnique = new SymbolServerSearchTechnique();
            Debugger.FileLocator.SearchTechniques.Add(m_symbolServerSearchTechnique);
            WriteOutput("SymbolSrv loaded");
        }

        public static void UnloadExtension()
        {
            Debugger.FileLocator.SearchTechniques.Remove(m_symbolServerSearchTechnique);
            WriteOutput("SymbolSrv unloaded");
        }

        private static SymbolServerSearchTechnique m_symbolServerSearchTechnique;
    }

    public class SymbolServerSearchTechnique : ISearchTechnique
    {
        public void Search(string searchPathEntry, FileSearchInfo searchInfo, FileSearchResult result)
        {
            if (searchPathEntry == FileLocator.FirstSearchEntry || searchPathEntry == FileLocator.LastSearchEntry)
                return;
            if (searchInfo.SearchKind == FileSearchInfo.FileSearchKind.Source)
                return;

            // don't search for ngen images on the symbol server, we won't find them
            if (searchInfo.Path.EndsWith(".ni.dll", StringComparison.OrdinalIgnoreCase))
                return;

            string fileName = Path.GetFileName(searchInfo.Path);
            string indexString = ComputeIndexString(searchInfo);

            SymStore store;
            if(searchPathEntry.StartsWith("srv*"))
                store = SymStore.FromPath(searchPathEntry);
            else
                store = SymStore.FromPath(@"srv**" + searchPathEntry);
            string filePath;
            if (store.TryGetFile(fileName, indexString, out filePath))
                result.CreateEntry(filePath);
        }


        private static string ComputeIndexString(FileSearchInfo searchInfo)
        {
            string indexString = null;
            if (searchInfo is SymbolFileSearchInfo)
            {
                SymbolFileSearchInfo symSearchInfo = (searchInfo as SymbolFileSearchInfo);
                indexString = symSearchInfo.Signature.ToString().Replace("-", "").ToUpper() +
                    symSearchInfo.Age.ToString();
            }
            else if (searchInfo is BinaryFileSearchInfo)
            {
                BinaryFileSearchInfo binarySearchInfo = (searchInfo as BinaryFileSearchInfo);
                indexString = binarySearchInfo.Timestamp.ToString("x").Replace("-", "").ToUpper() +
                    binarySearchInfo.Size.ToString("x");
            }
            return indexString;
        }
    }
#endif
    public class SymStoreResult
    {
        public SymStoreResult()
        {
            MessageLog = new List<string>();
        }
        public string CachedPath { get; set; }
        public string OriginalPath { get; set; }
        public List<string> MessageLog { get; set; }
    }

    public abstract class SymStore
    {
        public static SymStore FromPath(string path)
        {
            List<string> stores = new List<string>();
            if (path != null)
                stores.AddRange(path.Split('*'));

            // if there is a leading srv*, remove it
            // even without the srv* we assume everything is a symstore
            if (stores.Count > 0 && stores[0] == "srv")
                stores.RemoveAt(0);

            return FromPathHelper(stores, stores.Count - 1, null);
        }

        private static SymStore FromPathHelper(List<string> stores, int currentIndex, SymStore backingStore)
        {
            if (currentIndex == -1)
                return backingStore;
            string currentStorePath = RedirectStorePath(stores[currentIndex]);
            if (currentStorePath.StartsWith("http://") || currentStorePath.StartsWith("https://"))
                return FromPathHelper(stores, currentIndex - 1, new HttpSymStore(currentStorePath, backingStore));
            else
                return FromPathHelper(stores, currentIndex - 1, new UNCSymStore(currentStorePath, backingStore));
        }

        private static string RedirectStorePath(string storePath)
        {
            if (storePath == "") // default downstream store
            {
                return Path.Combine(Environment.CurrentDirectory, "sym");
            }
            else
            {
                try
                {
                    if (!storePath.StartsWith("http:") && !storePath.StartsWith("https:"))
                    {
                        string pingMePath = Path.Combine(storePath, "pingme.txt");
                        if (File.Exists(pingMePath))
                        {
                            string pingMeContents = File.ReadAllText(pingMePath);
                            if (pingMeContents.StartsWith("STORE:"))
                                return pingMeContents.Substring(6).TrimEnd('\n', '\r');
                        }
                    }
                }
                catch (IOException)
                {
                    return storePath;
                }
            }

            return storePath;
        }

        protected SymStore(string path, SymStore backingStore)
        {
            if (!path.EndsWith("/"))
                path += "/";
            StoreRootAddress = new Uri(path);
            BackingStore = backingStore;
        }

        public bool TryGetFile(string fileName, string indexString, SymStoreResult result)
        {
            Stream unused;
            bool ret = TryGetFile(fileName, indexString, result, out unused);
            if (unused != null)
                unused.Dispose();
            return ret;
        }

        public bool TryGetFile(string fileName, string indexString, SymStoreResult result, out Stream fileData)
        {
            // try and get the file from this store
            if (TryGetFileLocal(fileName, indexString, result, out fileData))
            {
                return true;
            }

            // if there is backing store try getting it from there and cache it in this store
            if (BackingStore != null)
            {
                Stream backingFileData;
                if (BackingStore.TryGetFile(fileName, indexString, result, out backingFileData))
                {
                    WriteFile(fileName, indexString, backingFileData);
                    if (backingFileData != null)
                        backingFileData.Dispose();
                    if (TryGetFileLocal(fileName, indexString, result, out fileData))
                        return true;
                }
            }

            // if nothing has worked yet, fail
            return false;
        }

        private bool TryGetFileLocal(string fileName, string indexString, SymStoreResult result, out Stream fileData)
        {
            fileData = null;
            string errorMessage = null;
            Uri request = new Uri(StoreRootAddress, fileName + "/" + indexString + "/" + fileName);

            //We could do better and actually calculate the backing path
            if (result.OriginalPath == null)
                result.OriginalPath = request.ToString();

            if (TryGetDataForUri(request, out fileData, out errorMessage))
            {
                result.CachedPath = request.AbsolutePath;
                result.MessageLog.Add("Retrieved " + request);
                return true;
            }
            else
            {
                result.MessageLog.Add(request.ToString() + " [" + errorMessage + "]");
            }

            string fileNameUnderBar = fileName.Substring(0, fileName.Length - 1) + "_";
            Uri requestUnderBar = new Uri(StoreRootAddress, fileName + "/" + indexString + "/" + fileNameUnderBar);
            Stream compressedFileData = null;
            if (TryGetDataForUri(requestUnderBar, out compressedFileData, out errorMessage))
            {
                fileData = CabUnpacker.Unpack(compressedFileData);
                result.CachedPath = requestUnderBar.AbsolutePath;
                result.MessageLog.Add("Retrieved " + requestUnderBar);
                return true;
            }
            else
            {
                result.MessageLog.Add(requestUnderBar.ToString() + " [" + errorMessage + "]");
            }

            Uri requestPtr = new Uri(StoreRootAddress, fileName + "/" + indexString + "/file.ptr");
            Stream ptrFileData;
            PtrFile ptr;
            if (!TryGetDataForUri(requestPtr, out ptrFileData, out errorMessage))
            {
                result.MessageLog.Add(requestPtr.ToString() + " [" + errorMessage + "]");
                return false;
            }
            else if (!PtrFile.TryParse(new StreamReader(ptrFileData).ReadToEnd(), out ptr))
            {
                result.MessageLog.Add(requestPtr.ToString() + " [Unable to parse]");
                return false;
            }
            else
            {
                result.MessageLog.Add(requestPtr.ToString() + " [Redirecting search]");
                result.MessageLog.Add("    Path: " + ptr.Path);
                if (!string.IsNullOrWhiteSpace(ptr.Message))
                {
                    result.MessageLog.Add("    Msg: " + ptr.Message);
                }
            }
            try
            {
                fileData = File.OpenRead(ptr.Path);
                result.CachedPath = ptr.Path;
            }
            catch (IOException e)
            {
                result.MessageLog.Add(ptr.Path + " [" + e.ToString() + "]");
                return false;
            }

            return true;
        }

        protected string WriteFile(string fileName, string indexString, Stream fileData)
        {
            Uri request = new Uri(Path.Combine(StoreRootAddress.AbsolutePath, fileName + "\\" + indexString + "\\" + fileName));
            WriteFile(request, fileData);
            return request.AbsolutePath;
        }

        protected abstract bool TryGetDataForUri(Uri file, out Stream fileData, out string errorMessage);
        protected abstract void WriteFile(Uri file, Stream fileData);

        public SymStore BackingStore { get; private set; }
        public Uri StoreRootAddress { get; private set; }

        protected class PtrFile
        {
            public static bool TryParse(string fileContents, out PtrFile file)
            {
                file = new PtrFile();
                if (fileContents.StartsWith("MSG: "))
                {
                    file.Message = fileContents.Substring(5);
                }
                else if (fileContents.StartsWith("PATH:"))
                {
                    file.Path = fileContents.Substring(5);
                }
                else
                {
                    file = null;
                    return false;
                }
                return true;
            }

            public string Message { get; private set; }
            public string Path { get; private set; }
        }
    }

    internal class HttpSymStore : SymStore
    {
        internal HttpSymStore(string path, SymStore backingStore) : base(path, backingStore) { }

        protected override bool TryGetDataForUri(Uri file, out Stream fileData, out string errorMessage)
        {
            fileData = null;
            errorMessage = null;
            HttpWebRequest wr = (HttpWebRequest)HttpWebRequest.Create(file);
            wr.UserAgent = "Microsoft-Symbol-Server/6.3.9600.17095";
            try
            {
                IAsyncResult result = wr.BeginGetResponse(null, null);
                if (!result.AsyncWaitHandle.WaitOne(30000))
                {
                    wr.Abort();
                    errorMessage = "Timed out waiting for http response";
                }
                HttpWebResponse response = (HttpWebResponse)wr.EndGetResponse(result);
                HttpStatusCode statusCode = response.StatusCode;
                // for missing files the symbol server still returns code OK, but the content is an html error message
                if (statusCode != HttpStatusCode.OK)
                {
                    errorMessage = "Http request answered with status code " + statusCode;
                }
                else if (response.ContentType.Equals("text/html") && response.ResponseUri != null)
                {
                    errorMessage = "Http request redirected to " + response.ResponseUri;
                }
                else
                {
                    fileData = response.GetResponseStream();
                    return true;
                }
            }
            catch (WebException e)
            {
                errorMessage = e.Message;
            }

            return false;
        }

        protected override void WriteFile(Uri file, Stream fileData)
        {
            throw new NotSupportedException();
        }
    }

    internal class UNCSymStore : SymStore
    {
        internal UNCSymStore(string storeRootAddress, SymStore backingStore) : base(storeRootAddress, backingStore) { }

        protected override bool TryGetDataForUri(Uri file, out Stream fileData, out string errorMessage)
        {
            fileData = null;
            errorMessage = null;
            try
            {
                if (File.Exists(file.AbsolutePath))
                {
                    fileData = File.OpenRead(file.AbsolutePath);
                    return true;
                }
                else
                {
                    errorMessage = "File not found at " + file.AbsolutePath;
                }
            }
            catch (IOException e)
            {
                errorMessage = e.ToString();
            }
            return false;
        }

        protected override void WriteFile(Uri file, Stream fileData)
        {
            // copy the stream in 64KB chunks if we can
            byte[] block = new byte[1024 * 64];
            string dir = Path.GetDirectoryName(file.AbsolutePath);
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            using (FileStream outFile = File.Open(file.AbsolutePath, FileMode.Create, FileAccess.Write))
            {
                int bytesRead = 0;
                do
                {
                    bytesRead = fileData.Read(block, 0, block.Length);
                    outFile.Write(block, 0, bytesRead);
                } while (bytesRead != 0);
            }
        }
    }
}
