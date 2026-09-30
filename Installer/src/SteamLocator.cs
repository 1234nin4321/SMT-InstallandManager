using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace SMTInstaller
{
    static class SteamLocator
    {
        /// <summary>Returns the game's folder in any Steam library, or null if it isn't found.</summary>
        public static string FindGame(string folderName)
        {
            var steam = FindSteam();
            if (steam == null) return null;

            return GetLibraries(steam)
                .Select(lib => Path.Combine(lib, "steamapps", "common", folderName))
                .FirstOrDefault(Directory.Exists);
        }

        static string FindSteam()
        {
            var candidates = new[]
            {
                Registry.GetValue(@"HKEY_CURRENT_USER\Software\Valve\Steam", "SteamPath", null),
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath", null),
                Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam", "InstallPath", null),
            };
            return candidates
                .OfType<string>()
                .Select(p => p.Replace('/', '\\'))
                .FirstOrDefault(Directory.Exists);
        }

        static IEnumerable<string> GetLibraries(string steam)
        {
            var libraries = new List<string> { steam };
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            if (File.Exists(vdf))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                    libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
            }
            return libraries.Distinct(System.StringComparer.OrdinalIgnoreCase);
        }
    }
}
