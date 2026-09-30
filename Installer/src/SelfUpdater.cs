using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace SMTInstaller
{
    class InstallerUpdate
    {
        public Version Version;
        public Asset Asset;
        public string PageUrl;
    }

    /// <summary>Replaces the running exe with a newer SMTInstaller_vX.Y.Z.exe from this repo's releases.</summary>
    static class SelfUpdater
    {
        public const string Repo = "1234nin4321/SMT-InstallandManager";

        // The installer shares releases with the plugin, so its version comes from the asset name, not the tag
        static readonly Regex AssetPattern = new Regex(@"^SMTInstaller_v(\d+\.\d+\.\d+)\.exe$", RegexOptions.IgnoreCase);

        public static Version CurrentVersion
        {
            get
            {
                var v = Assembly.GetExecutingAssembly().GetName().Version;
                return new Version(v.Major, v.Minor, v.Build);
            }
        }

        const string CleanupArg = "--cleanup";

        static string ExePath => Application.ExecutablePath;

        /// <summary>The newest published installer if it's newer than this one, otherwise null.</summary>
        public static async Task<InstallerUpdate> FindUpdate()
        {
            var releases = await GitHubReleases.List(Repo);
            var newest = releases
                .Where(r => !r.Prerelease && r.Assets != null)
                .SelectMany(r => r.Assets.Select(a => new { Release = r, Asset = a, Match = AssetPattern.Match(a.Name) }))
                .Where(x => x.Match.Success)
                .Select(x => new InstallerUpdate { Version = Version.Parse(x.Match.Groups[1].Value), Asset = x.Asset, PageUrl = x.Release.PageUrl })
                .OrderByDescending(u => u.Version)
                .FirstOrDefault();
            return newest != null && newest.Version > CurrentVersion ? newest : null;
        }

        /// <summary>
        /// Downloads the update next to the running exe and moves the running exe out of the way.
        /// Returns the new exe's path; pass it to Restart, which has the new exe delete the old one.
        /// </summary>
        public static async Task<string> Apply(InstallerUpdate update, IProgress<double> progress)
        {
            // Keep the file name in step with the version, unless the user renamed the exe
            var target = AssetPattern.IsMatch(Path.GetFileName(ExePath))
                ? Path.Combine(Path.GetDirectoryName(ExePath), update.Asset.Name)
                : ExePath;
            var newPath = target + ".new";
            var oldPath = ExePath + ".old";
            try
            {
                await GitHubReleases.Download(update.Asset.DownloadUrl, newPath, progress);
                Verify(newPath, update.Asset.Digest);

                // Windows won't overwrite or delete a running exe, but it will rename one
                if (File.Exists(oldPath)) File.Delete(oldPath);
                File.Move(ExePath, oldPath);
                try
                {
                    // e.g. the same version downloaded by hand earlier
                    if (File.Exists(target)) File.Delete(target);
                    File.Move(newPath, target);
                }
                catch
                {
                    File.Move(oldPath, ExePath);
                    throw;
                }
                return target;
            }
            finally
            {
                try { File.Delete(newPath); } catch { /* best effort */ }
            }
        }

        static void Verify(string path, string digest)
        {
            if (digest == null || !digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase)) return;

            string actual;
            using (var sha = SHA256.Create())
            using (var file = File.OpenRead(path))
                actual = string.Concat(sha.ComputeHash(file).Select(b => b.ToString("x2")));
            if (!string.Equals(actual, digest.Substring("sha256:".Length), StringComparison.OrdinalIgnoreCase))
                throw new Exception("The downloaded update is corrupted (checksum mismatch).");
        }

        /// <summary>Starts the updated exe and tells it to delete this one once this process has exited.</summary>
        public static void Restart(string newExe) => Process.Start(newExe, $"{CleanupArg} \"{ExePath}.old\"");

        /// <summary>Deletes the exe left behind by an update, retrying while the old process exits.</summary>
        public static void CleanupOldVersion(string[] args)
        {
            // Older versions restart without the argument, leaving "<this exe>.old"
            var leftovers = new List<string> { ExePath + ".old" };
            var i = Array.IndexOf(args, CleanupArg);
            if (i >= 0 && i + 1 < args.Length && IsLeftoverExe(args[i + 1])) leftovers.Add(args[i + 1]);
            leftovers = leftovers.Distinct(StringComparer.OrdinalIgnoreCase).Where(File.Exists).ToList();
            if (leftovers.Count == 0) return;

            Task.Run(() =>
            {
                foreach (var path in leftovers)
                {
                    for (var attempt = 0; attempt < 60 && File.Exists(path); attempt++)
                    {
                        try { File.Delete(path); }
                        catch (IOException) { Thread.Sleep(500); }
                        catch (UnauthorizedAccessException) { Thread.Sleep(500); }
                    }
                }
            });
        }

        /// <summary>Only a renamed exe in this exe's own folder may be deleted from the command line.</summary>
        static bool IsLeftoverExe(string path)
        {
            try
            {
                var full = Path.GetFullPath(path);
                return full.EndsWith(".exe.old", StringComparison.OrdinalIgnoreCase) &&
                       string.Equals(Path.GetDirectoryName(full), Path.GetDirectoryName(ExePath), StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is NotSupportedException || ex is PathTooLongException)
            {
                return false;
            }
        }
    }
}
