using Microsoft.Deployment.Compression;
using Microsoft.Deployment.Compression.Cab;
using System;
using System.Diagnostics;
using System.IO;

namespace StackParser
{
    public static class CabUnpacker
    {
        public static Stream Unpack(Stream compressedStream)
        {
            CabEngine cabEngine = new CabEngine();
            InMemoryUnpackContext unpackContext = new InMemoryUnpackContext(compressedStream);
            cabEngine.Unpack(unpackContext, suffix => true);
            Stream s = unpackContext.UncompressedStream;
            s.Seek(0, SeekOrigin.Begin);
            return s;
        }

        class InMemoryUnpackContext : IUnpackStreamContext
        {
            public InMemoryUnpackContext(Stream compressedStream)
            {
                // The stream isn't always seekable and we need to be able to seek to decompress the
                // file. We solve this by copying all the data into a memory buffer that is seekable.
                // This copy is unfortunate for large files and could be improved if performance
                // analysis indicated a problem.
                if (!compressedStream.CanSeek)
                {
                    _compressedStream = new MemoryStream();
                    compressedStream.CopyTo(_compressedStream);
                }
                else
                {
                    _compressedStream = compressedStream;
                }
            }
            Stream _compressedStream;
            MemoryStream _uncompressedStream = new MemoryStream();

            public void CloseArchiveReadStream(int archiveNumber, string archiveName, Stream stream)
            {
                Debug.Assert(stream == _compressedStream);
            }

            public void CloseFileWriteStream(string path, Stream stream, FileAttributes attributes, DateTime lastWriteTime)
            {
                Debug.Assert(stream == _uncompressedStream);
            }

            public Stream OpenArchiveReadStream(int archiveNumber, string archiveName, CompressionEngine compressionEngine)
            {
                _compressedStream.Seek(0, SeekOrigin.Begin);
                return _compressedStream;
            }

            public Stream OpenFileWriteStream(string path, long fileSize, DateTime lastWriteTime)
            {
                return _uncompressedStream;
            }

            public MemoryStream UncompressedStream { get { return _uncompressedStream; } }
        }
    }
}
