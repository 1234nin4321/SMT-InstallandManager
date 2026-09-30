using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SMTModBrowser.Core
{
    /// <summary>
    /// File changes that couldn't happen while the game was running, because the mod's DLLs
    /// were loaded and locked. The preloader patcher applies them on the next launch,
    /// before any plugin is loaded.
    ///
    /// pending.txt holds one operation per line:
    ///   clear&lt;TAB&gt;Owner-Name   delete the mod's folders
    ///   copy&lt;TAB&gt;Owner-Name    copy staging/Owner-Name into the BepInEx folder
    /// </summary>
    public static class PendingOps
    {
        public static string DataDir(string bepinexRoot) => Path.Combine(bepinexRoot, "SMTModBrowser");
        public static string StagingDir(string bepinexRoot) => Path.Combine(DataDir(bepinexRoot), "staging");
        static string PendingFile(string bepinexRoot) => Path.Combine(DataDir(bepinexRoot), "pending.txt");

        public static void Add(string bepinexRoot, string op, string ownerName)
        {
            Directory.CreateDirectory(DataDir(bepinexRoot));
            File.AppendAllText(PendingFile(bepinexRoot), $"{op}\t{ownerName}\n");
        }

        public static List<KeyValuePair<string, string>> Read(string bepinexRoot)
        {
            var file = PendingFile(bepinexRoot);
            if (!File.Exists(file)) return new List<KeyValuePair<string, string>>();
            return File.ReadAllLines(file)
                .Select(l => l.Split('\t'))
                .Where(p => p.Length == 2)
                .Select(p => new KeyValuePair<string, string>(p[0], p[1]))
                .ToList();
        }

        /// <summary>Runs all pending operations. Call only when no mod DLLs are loaded.</summary>
        public static void Apply(string bepinexRoot, Action<string> log)
        {
            var ops = Read(bepinexRoot);
            if (ops.Count == 0) return;

            foreach (var op in ops)
            {
                try
                {
                    if (op.Key == "clear")
                    {
                        foreach (var dir in ModLayout.OwnedFolders(bepinexRoot, op.Value).Where(Directory.Exists))
                            Directory.Delete(dir, recursive: true);
                        log($"Removed {op.Value}");
                    }
                    else if (op.Key == "copy")
                    {
                        var source = Path.Combine(StagingDir(bepinexRoot), op.Value);
                        if (!Directory.Exists(source)) continue;
                        foreach (var file in Directory.GetFiles(source, "*", SearchOption.AllDirectories))
                        {
                            var relative = file.Substring(source.Length + 1).Replace(Path.DirectorySeparatorChar, '/');
                            var target = ModLayout.ToFullPath(bepinexRoot, relative);
                            if (ModLayout.IsUserConfig(relative) && File.Exists(target)) continue;
                            Directory.CreateDirectory(Path.GetDirectoryName(target));
                            File.Copy(file, target, overwrite: true);
                        }
                        log($"Installed {op.Value}");
                    }
                }
                catch (Exception ex)
                {
                    log($"Failed to {op.Key} {op.Value}: {ex.Message}");
                }
            }

            File.Delete(PendingFile(bepinexRoot));
            if (Directory.Exists(StagingDir(bepinexRoot)))
                Directory.Delete(StagingDir(bepinexRoot), recursive: true);
        }
    }
}
