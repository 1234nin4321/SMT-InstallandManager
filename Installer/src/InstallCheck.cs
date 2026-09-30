using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace SMTInstaller
{
    /// <summary>Works out which version of a mod is already in the game folder.</summary>
    static class InstallCheck
    {
        /// <summary>
        /// The version of <paramref name="mod"/>'s main DLL in the game folder, or null if the mod isn't
        /// fully installed (the DLL or one of its required files is missing) or can't be checked.
        /// Works however it was installed: by this installer, r2modman or by hand.
        /// </summary>
        public static Version InstalledVersion(ModPackage mod, string gamePath)
        {
            if (mod.VersionFile == null) return null;
            var main = FullPath(gamePath, mod.VersionFile);
            if (!File.Exists(main) || mod.RequiredFiles.Any(f => !File.Exists(FullPath(gamePath, f)))) return null;

            // The numeric parts, not the FileVersion string, which may carry suffixes
            var info = FileVersionInfo.GetVersionInfo(main);
            return new Version(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart, info.FilePrivatePart);
        }

        /// <summary>Fills missing parts with 0, so 19.0 equals 19.0.0.0 when compared.</summary>
        public static Version Normalize(Version v) =>
            v == null ? null : new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));

        /// <summary>"5.4.23.5", "19.0.0", "0.2.0".</summary>
        public static string Format(Version v) => v.Revision > 0 ? v.ToString() : v.ToString(3);

        static string FullPath(string gamePath, string relative) =>
            Path.Combine(gamePath, relative.Replace('/', Path.DirectorySeparatorChar));
    }
}
