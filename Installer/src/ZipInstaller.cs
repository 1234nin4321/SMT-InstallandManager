using System;
using System.IO;
using System.IO.Compression;

namespace SMTInstaller
{
    static class ZipInstaller
    {
        /// <summary>Extracts a zip into a folder, overwriting existing files.</summary>
        public static int ExtractOver(string zipPath, string destination)
        {
            var root = Path.GetFullPath(destination).TrimEnd('\\') + "\\";
            var count = 0;

            using (var zip = ZipFile.OpenRead(zipPath))
            {
                foreach (var entry in zip.Entries)
                {
                    var target = Path.GetFullPath(Path.Combine(root, entry.FullName));
                    // Refuse entries that would escape the game folder (e.g. "..\..\file")
                    if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException($"Unsafe path in zip: {entry.FullName}");

                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    entry.ExtractToFile(target, overwrite: true);
                    count++;
                }
            }
            return count;
        }
    }
}
