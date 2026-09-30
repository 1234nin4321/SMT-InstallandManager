using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace SMTModBrowser.Core
{
    /// <summary>
    /// Reads zip files using only DeflateStream. Unity games don't reliably ship
    /// System.IO.Compression.dll (ZipArchive), but DeflateStream is always in System.dll.
    /// </summary>
    public static class ZipReader
    {
        public class Entry
        {
            public string Path;   // forward slashes
            public byte[] Data;
        }

        public static List<Entry> Read(byte[] zip)
        {
            var eocd = FindEndOfCentralDirectory(zip);
            var count = U16(zip, eocd + 10);
            var p = (int)U32(zip, eocd + 16);
            var entries = new List<Entry>(count);

            for (var n = 0; n < count; n++)
            {
                if (U32(zip, p) != 0x02014b50) throw new InvalidDataException("Corrupt zip central directory");
                var method = U16(zip, p + 10);
                var compressedSize = (int)U32(zip, p + 20);
                var size = (int)U32(zip, p + 24);
                var nameLength = U16(zip, p + 28);
                var extraLength = U16(zip, p + 30);
                var commentLength = U16(zip, p + 32);
                var localOffset = (int)U32(zip, p + 42);
                var name = Encoding.UTF8.GetString(zip, p + 46, nameLength).Replace('\\', '/');
                p += 46 + nameLength + extraLength + commentLength;

                if (name.EndsWith("/")) continue;   // directory entry

                if (U32(zip, localOffset) != 0x04034b50) throw new InvalidDataException($"Corrupt zip entry: {name}");
                var dataStart = localOffset + 30 + U16(zip, localOffset + 26) + U16(zip, localOffset + 28);

                byte[] data;
                if (method == 0)
                {
                    data = new byte[size];
                    Buffer.BlockCopy(zip, dataStart, data, 0, size);
                }
                else if (method == 8)
                {
                    data = new byte[size];
                    using (var input = new MemoryStream(zip, dataStart, compressedSize))
                    using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
                    {
                        var read = 0;
                        while (read < size)
                        {
                            var n2 = deflate.Read(data, read, size - read);
                            if (n2 == 0) throw new InvalidDataException($"Truncated zip entry: {name}");
                            read += n2;
                        }
                    }
                }
                else throw new NotSupportedException($"Unsupported zip compression method {method} for {name}");

                entries.Add(new Entry { Path = name, Data = data });
            }
            return entries;
        }

        static int FindEndOfCentralDirectory(byte[] zip)
        {
            // The EOCD record is at the end, followed by an optional comment of up to 64 KB
            for (var i = zip.Length - 22; i >= Math.Max(0, zip.Length - 22 - 0xFFFF); i--)
                if (U32(zip, i) == 0x06054b50) return i;
            throw new InvalidDataException("Not a zip file");
        }

        static int U16(byte[] b, int i) => b[i] | b[i + 1] << 8;
        static uint U32(byte[] b, int i) => (uint)(b[i] | b[i + 1] << 8 | b[i + 2] << 16 | b[i + 3] << 24);
    }
}
