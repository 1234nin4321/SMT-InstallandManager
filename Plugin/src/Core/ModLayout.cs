using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SMTModBrowser.Core
{
    /// <summary>
    /// Where a Thunderstore package's files go inside the BepInEx folder. Follows the same
    /// rules as r2modman, so mods installed either way end up in the same place.
    /// </summary>
    public static class ModLayout
    {
        /// <summary>BepInEx folders that get a per-mod "Owner-Name" subfolder.</summary>
        public static readonly string[] NamespacedFolders = { "plugins", "patchers", "core", "monomod" };

        /// <summary>
        /// Maps a zip entry to a path relative to the BepInEx folder:
        /// plugins/x.dll → plugins/Owner-Name/x.dll, config/x.cfg → config/x.cfg,
        /// anything else (root dlls, manifest.json, icon.png) → plugins/Owner-Name/...
        /// A leading "BepInEx/" is stripped first.
        /// </summary>
        public static string MapEntry(string entryPath, string ownerName)
        {
            var parts = entryPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count == 0) return null;
            if (parts.Any(p => p == ".." || p == ".")) throw new InvalidDataException($"Unsafe path in mod zip: {entryPath}");

            if (parts.Count > 1 && parts[0].Equals("BepInEx", StringComparison.OrdinalIgnoreCase))
                parts.RemoveAt(0);

            if (parts.Count > 1)
            {
                var top = parts[0].ToLowerInvariant();
                var rest = string.Join("/", parts.Skip(1));
                if (NamespacedFolders.Contains(top))
                    return $"{top}/{ownerName}/{rest}";
                if (top == "config")
                    return $"config/{rest}";
            }
            return $"plugins/{ownerName}/{string.Join("/", parts)}";
        }

        /// <summary>True for files the user may have edited, which are never overwritten.</summary>
        public static bool IsUserConfig(string relativePath) =>
            relativePath.StartsWith("config/", StringComparison.OrdinalIgnoreCase);

        /// <summary>The folders a mod owns (existing or not), as absolute paths.</summary>
        public static IEnumerable<string> OwnedFolders(string bepinexRoot, string ownerName) =>
            NamespacedFolders.Select(f => Path.Combine(bepinexRoot, f, ownerName));

        public static string ToFullPath(string bepinexRoot, string relativePath) =>
            Path.Combine(bepinexRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
