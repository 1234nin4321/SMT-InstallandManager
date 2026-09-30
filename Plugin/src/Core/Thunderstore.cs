using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTModBrowser.Core
{
    public class PackageVersion
    {
        public string FullName;      // Owner-Name-1.2.3
        public string Version;
        public string Description;
        public string IconUrl;
        public string DownloadUrl;
        public string WebsiteUrl;
        public List<string> Dependencies;
        public long Downloads;
        public long FileSize;
        public DateTime Created;
    }

    public class Package
    {
        public string Owner;
        public string Name;
        public string FullName;      // Owner-Name
        public string PackageUrl;
        public DateTime Created;
        public DateTime Updated;
        public long Rating;
        public bool Deprecated;
        public List<string> Categories;
        public List<PackageVersion> Versions;   // newest first
        public long TotalDownloads;
        public bool Local;   // built from an installed manifest because it isn't in the Thunderstore list

        public PackageVersion Latest => Versions[0];
        public string DisplayName => Name.Replace('_', ' ');
    }

    /// <summary>A "Owner-Name-1.2.3" dependency string.</summary>
    public struct DependencyRef
    {
        public string FullName;   // Owner-Name
        public string Version;

        public static DependencyRef Parse(string text)
        {
            // Thunderstore owners and names can't contain '-', so the last '-' starts the version
            var cut = text.LastIndexOf('-');
            return cut < 0
                ? new DependencyRef { FullName = text, Version = "0.0.0" }
                : new DependencyRef { FullName = text.Substring(0, cut), Version = text.Substring(cut + 1) };
        }
    }

    public static class Thunderstore
    {
        public const string Community = "supermarket-together";
        public static string ListUrl => $"https://thunderstore.io/c/{Community}/api/v1/package/";

        public static string ReadmeUrl(Package p) =>
            $"https://thunderstore.io/api/experimental/package/{p.Owner}/{p.Name}/{p.Latest.Version}/readme/";

        /// <summary>BepInEx itself is installed by SMTInstaller, so packs are never installed as mods.</summary>
        public static bool IsModLoader(string fullName) =>
            fullName.EndsWith("-BepInExPack", StringComparison.OrdinalIgnoreCase);

        // Mod managers listed in the community; they aren't mods
        static readonly HashSet<string> Hidden = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ebkr-r2modman", "Kesomannen-GaleModManager",
        };

        public static List<Package> ParseList(string json)
        {
            var result = new List<Package>();
            foreach (Dictionary<string, object> p in (List<object>)Json.Parse(json))
            {
                var versions = p.List("versions").Cast<Dictionary<string, object>>().Select(v => new PackageVersion
                {
                    FullName = v.Str("full_name"),
                    Version = v.Str("version_number"),
                    Description = v.Str("description") ?? "",
                    IconUrl = v.Str("icon"),
                    DownloadUrl = v.Str("download_url"),
                    WebsiteUrl = v.Str("website_url"),
                    Dependencies = v.List("dependencies").Cast<string>().ToList(),
                    Downloads = v.Long("downloads"),
                    FileSize = v.Long("file_size"),
                    Created = v.Date("date_created"),
                }).ToList();
                if (versions.Count == 0) continue;

                var package = new Package
                {
                    Owner = p.Str("owner"),
                    Name = p.Str("name"),
                    FullName = p.Str("full_name"),
                    PackageUrl = p.Str("package_url"),
                    Created = p.Date("date_created"),
                    Updated = p.Date("date_updated"),
                    Rating = p.Long("rating_score"),
                    Deprecated = p.Bool("is_deprecated"),
                    Categories = p.List("categories").Cast<string>().ToList(),
                    Versions = versions,
                    TotalDownloads = versions.Sum(v => v.Downloads),
                };
                if (p.Bool("has_nsfw_content")) continue;
                result.Add(package);
            }
            return result;
        }

        /// <summary>A stand-in for an installed mod that the Thunderstore list doesn't have (removed, or the list is offline).</summary>
        public static Package FromInstalled(InstalledMod mod)
        {
            var dash = mod.FullName.IndexOf('-');
            var owner = dash > 0 ? mod.FullName.Substring(0, dash) : "";
            var name = dash > 0 ? mod.FullName.Substring(dash + 1) : mod.FullName;
            return new Package
            {
                Owner = owner,
                Name = name,
                FullName = mod.FullName,
                Categories = new List<string>(),
                Versions = new List<PackageVersion>
                {
                    new PackageVersion
                    {
                        FullName = $"{mod.FullName}-{mod.Version}",
                        Version = mod.Version,
                        Description = mod.Description ?? "",
                        Dependencies = new List<string>(),
                    },
                },
                Local = true,
            };
        }

        /// <summary>Packages worth showing in the browser.</summary>
        public static bool IsBrowsable(Package p) => !p.Deprecated && !IsModLoader(p.FullName) && !Hidden.Contains(p.FullName);

        /// <summary>Compares "1.2.3"-style versions numerically.</summary>
        public static int CompareVersions(string a, string b)
        {
            var pa = (a ?? "0").Split('.');
            var pb = (b ?? "0").Split('.');
            for (var i = 0; i < Math.Max(pa.Length, pb.Length); i++)
            {
                int.TryParse(i < pa.Length ? pa[i] : "0", out var x);
                int.TryParse(i < pb.Length ? pb[i] : "0", out var y);
                if (x != y) return x.CompareTo(y);
            }
            return 0;
        }
    }
}
