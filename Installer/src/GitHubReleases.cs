using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SMTInstaller
{
    [DataContract]
    class Release
    {
        [DataMember(Name = "tag_name")] public string TagName { get; set; }
        [DataMember(Name = "html_url")] public string PageUrl { get; set; }
        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        [DataMember(Name = "published_at")] public string PublishedAt { get; set; }   // ISO 8601, sorts as text
        [DataMember(Name = "assets")] public Asset[] Assets { get; set; }
    }

    [DataContract]
    class Asset
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "browser_download_url")] public string DownloadUrl { get; set; }
        [DataMember(Name = "digest")] public string Digest { get; set; }   // "sha256:<hex>", missing on old uploads
    }

    /// <summary>One entry of optional-mods.json in this repo.</summary>
    [DataContract]
    class CatalogEntry
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "description")] public string Description { get; set; }
        [DataMember(Name = "repo")] public string Repo { get; set; }         // defaults to this repo
        [DataMember(Name = "asset")] public string Asset { get; set; }       // regex for the release zip
        [DataMember(Name = "file")] public string File { get; set; }         // DLL whose version shows what's installed
        [DataMember(Name = "selected")] public bool Selected { get; set; }   // ticked by default
    }

    [DataContract]
    class Catalog
    {
        [DataMember(Name = "mods")] public CatalogEntry[] Mods { get; set; }
    }

    /// <summary>A zip to fetch from a repo's GitHub releases and extract into the game folder.</summary>
    class ModPackage
    {
        public string DisplayName;
        public string Description;
        public string Glyph;
        public string Repo;          // "owner/name"
        public Regex AssetPattern;   // picks the right zip out of the release assets
        public bool Optional;        // the user chooses whether to install it
        public bool Selected;        // optional mods only: ticked for install

        // Relative to the game folder. The version file's version is the installed version; the mod
        // only counts as installed if the required files exist too.
        public string VersionFile;
        public string[] RequiredFiles = new string[0];

        public string Version;       // filled in by Resolve, for display
        public Version LatestVersion;
        public Asset Asset;
        public Version InstalledVersion;   // filled in from the game folder; null if not installed

        /// <summary>Missing, or older than the latest release. Unknown versions count as up to date.</summary>
        public bool NeedsInstall => InstalledVersion == null || LatestVersion != null && InstalledVersion < LatestVersion;
    }

    static class GitHubReleases
    {
        static readonly HttpClient Http = CreateClient();

        static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            // GitHub rejects API requests without a User-Agent
            client.DefaultRequestHeaders.UserAgent.ParseAdd("SMTInstaller/1.0");
            return client;
        }

        const string CatalogFile = "optional-mods.json";
        static readonly Regex VersionInName = new Regex(@"\d+(?:\.\d+){1,3}");
        static readonly ConcurrentDictionary<string, Task<Release[]>> releaseLists = new ConcurrentDictionary<string, Task<Release[]>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Finds the newest stable download matching the mod's pattern across the repo's recent releases.
        /// Not just the latest release, since a release may ship only some of the repo's files.
        /// </summary>
        public static async Task Resolve(ModPackage mod)
        {
            var releases = await List(mod.Repo);
            var best = releases
                .Where(r => !r.Prerelease && r.Assets != null)
                .SelectMany(r => r.Assets.Where(a => mod.AssetPattern.IsMatch(a.Name)).Select(a => new { Release = r, Asset = a, Version = VersionOf(a.Name) }))
                .OrderByDescending(x => x.Version)
                .ThenByDescending(x => x.Release.PublishedAt, StringComparer.Ordinal)
                .FirstOrDefault();
            if (best == null)
                throw new Exception($"No download matching {mod.AssetPattern} in the releases of {mod.Repo}.");

            mod.Version = best.Version != null ? $"v{best.Version}" : best.Release.TagName;
            mod.LatestVersion = InstallCheck.Normalize(best.Version);
            mod.Asset = best.Asset;
        }

        /// <summary>"5.4.23.5" out of "BepInEx_win_x64_5.4.23.5.zip", or null.</summary>
        public static Version VersionOf(string assetName)
        {
            var match = VersionInName.Match(assetName);
            return match.Success && Version.TryParse(match.Value, out var version) ? version : null;
        }

        /// <summary>The most recent releases of a repo, in no particular order. Fetched once per run.</summary>
        public static Task<Release[]> List(string repo) =>
            releaseLists.GetOrAdd(repo, r => GetJson<Release[]>($"https://api.github.com/repos/{r}/releases?per_page=30", r));

        /// <summary>The optional mods listed in optional-mods.json on the repo's main branch; empty if there is none.</summary>
        public static async Task<CatalogEntry[]> OptionalMods(string repo)
        {
            // raw.githubusercontent.com doesn't count against the API rate limit
            using (var response = await Http.GetAsync($"https://raw.githubusercontent.com/{repo}/main/{CatalogFile}"))
            {
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return new CatalogEntry[0];
                var body = await response.Content.ReadAsStringAsync();
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"GitHub returned {(int)response.StatusCode} for {CatalogFile}: {body}");
                using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(body)))
                    return ((Catalog)new DataContractJsonSerializer(typeof(Catalog)).ReadObject(stream)).Mods ?? new CatalogEntry[0];
            }
        }

        static async Task<T> GetJson<T>(string url, string repo)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (var response = await Http.SendAsync(request))
                {
                    var body = await response.Content.ReadAsStringAsync();
                    if (!response.IsSuccessStatusCode)
                        throw new Exception($"GitHub returned {(int)response.StatusCode} for {repo}: {body}");

                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(body)))
                        return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
                }
            }
        }

        public static async Task Download(string url, string destination, IProgress<double> progress = null)
        {
            using (var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();
                var total = response.Content.Headers.ContentLength ?? 0;

                using (var input = await response.Content.ReadAsStreamAsync())
                using (var file = File.Create(destination))
                {
                    var buffer = new byte[81920];
                    long received = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await file.WriteAsync(buffer, 0, read);
                        received += read;
                        if (total > 0) progress?.Report((double)received / total);
                    }
                }
            }
        }
    }
}
