using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SMTModBrowser.Core
{
    public class InstalledMod
    {
        public string FullName;    // Owner-Name
        public string Version;
        public string Description;   // from manifest.json, for mods that aren't on Thunderstore
        public bool PendingInstall;   // staged, applied on next launch
        public bool PendingRemoval;   // removed on next launch
    }

    public class InstallResult
    {
        public int Files;
        public bool Deferred;   // true when the change applies after a restart
    }

    /// <summary>Installs, updates and removes Thunderstore packages in a BepInEx folder.</summary>
    public class ModInstaller
    {
        readonly string root;

        public ModInstaller(string bepinexRoot)
        {
            root = bepinexRoot;
        }

        /// <summary>Mods installed as plugins/Owner-Name/manifest.json, including pending changes.</summary>
        public Dictionary<string, InstalledMod> ScanInstalled()
        {
            var result = new Dictionary<string, InstalledMod>(StringComparer.OrdinalIgnoreCase);
            var plugins = Path.Combine(root, "plugins");
            if (Directory.Exists(plugins))
            {
                foreach (var dir in Directory.GetDirectories(plugins))
                {
                    var name = Path.GetFileName(dir);
                    var manifest = ReadManifest(Path.Combine(dir, "manifest.json"));
                    var version = manifest?.Str("version_number");
                    if (name.Contains("-") && version != null)
                        result[name] = new InstalledMod { FullName = name, Version = version, Description = manifest.Str("description") };
                }
            }

            // Replay pending operations so the UI shows the state after the next restart
            foreach (var op in PendingOps.Read(root))
            {
                if (op.Key == "clear")
                {
                    if (result.TryGetValue(op.Value, out var mod)) mod.PendingRemoval = true;
                    else result[op.Value] = new InstalledMod { FullName = op.Value, PendingRemoval = true };
                }
                else if (op.Key == "copy")
                {
                    var staged = Path.Combine(PendingOps.StagingDir(root), op.Value, "plugins", op.Value, "manifest.json");
                    var manifest = ReadManifest(staged);
                    result[op.Value] = new InstalledMod
                    {
                        FullName = op.Value, Version = manifest?.Str("version_number"), Description = manifest?.Str("description"), PendingInstall = true,
                    };
                }
            }
            return result;
        }

        /// <summary>
        /// The package versions to install for <paramref name="target"/>, dependencies first.
        /// Dependencies already installed at the required version or newer are skipped;
        /// ones not on Thunderstore are added to <paramref name="missing"/>.
        /// </summary>
        public static List<PackageVersion> PlanInstall(Package target, IDictionary<string, Package> packages,
            IDictionary<string, InstalledMod> installed, List<string> missing)
        {
            var plan = new List<PackageVersion>();
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void Visit(Package package)
            {
                if (!visited.Add(package.FullName)) return;
                foreach (var dependency in package.Latest.Dependencies.Select(DependencyRef.Parse))
                {
                    if (Thunderstore.IsModLoader(dependency.FullName)) continue;
                    if (!packages.TryGetValue(dependency.FullName, out var depPackage))
                    {
                        missing.Add(dependency.FullName);
                        continue;
                    }
                    if (installed.TryGetValue(dependency.FullName, out var have) && !have.PendingRemoval &&
                        Thunderstore.CompareVersions(have.Version, dependency.Version) >= 0)
                        continue;
                    Visit(depPackage);
                }
                plan.Add(package.Latest);
            }

            Visit(target);
            return plan;
        }

        /// <summary>
        /// Installs a package zip, replacing any previous version. If the old version's files are
        /// locked by the running game, the install is staged and applied on the next launch.
        /// </summary>
        public InstallResult Install(string ownerName, byte[] zip)
        {
            var files = ZipReader.Read(zip)
                .Select(e => new { Path = ModLayout.MapEntry(e.Path, ownerName), e.Data })
                .Where(f => f.Path != null)
                .ToList();

            if (CanModify(ownerName))
            {
                DeleteOwnedFolders(ownerName);
                foreach (var f in files) WriteFile(root, f.Path, f.Data);
                return new InstallResult { Files = files.Count };
            }

            // Stage for the preloader patcher to apply on the next launch
            var staging = Path.Combine(PendingOps.StagingDir(root), ownerName);
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            foreach (var f in files) WriteFile(staging, f.Path, f.Data);
            PendingOps.Add(root, "clear", ownerName);
            PendingOps.Add(root, "copy", ownerName);
            return new InstallResult { Files = files.Count, Deferred = true };
        }

        /// <summary>Removes a mod. Returns false if it's in use and will be removed on the next launch.</summary>
        public bool Uninstall(string ownerName)
        {
            // A staged update would bring the mod back on the next launch, so cancel it with a later clear
            var staged = PendingOps.Read(root).Any(op => op.Key == "copy" && op.Value.Equals(ownerName, StringComparison.OrdinalIgnoreCase));
            if (CanModify(ownerName))
            {
                DeleteOwnedFolders(ownerName);
                if (!staged) return true;
            }
            PendingOps.Add(root, "clear", ownerName);
            return false;
        }

        /// <summary>True if none of the mod's files are locked (e.g. a DLL loaded by the game).</summary>
        bool CanModify(string ownerName)
        {
            foreach (var dir in ModLayout.OwnedFolders(root, ownerName).Where(Directory.Exists))
            {
                foreach (var file in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        using (File.Open(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) { }
                    }
                    catch (IOException) { return false; }
                    catch (UnauthorizedAccessException) { return false; }
                }
            }
            return true;
        }

        void DeleteOwnedFolders(string ownerName)
        {
            foreach (var dir in ModLayout.OwnedFolders(root, ownerName).Where(Directory.Exists))
                Directory.Delete(dir, recursive: true);
        }

        static void WriteFile(string baseDir, string relativePath, byte[] data)
        {
            var target = ModLayout.ToFullPath(baseDir, relativePath);
            // Keep the user's settings when reinstalling or updating
            if (ModLayout.IsUserConfig(relativePath) && File.Exists(target)) return;
            Directory.CreateDirectory(Path.GetDirectoryName(target));
            File.WriteAllBytes(target, data);
        }

        static Dictionary<string, object> ReadManifest(string path)
        {
            if (!File.Exists(path)) return null;
            try
            {
                return Json.Parse(File.ReadAllText(path)) as Dictionary<string, object>;
            }
            catch (FormatException)
            {
                return null;
            }
        }
    }
}
