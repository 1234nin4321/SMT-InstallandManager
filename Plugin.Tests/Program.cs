using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using SMTModBrowser.Core;

static class Program
{
    static int failures;

    static void Check(bool ok, string what)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
        if (!ok) failures++;
    }

    static int Main()
    {
        var http = new HttpClient();
        http.DefaultRequestHeaders.UserAgent.ParseAdd("SMTModBrowser-Tests/0.1");

        Console.WriteLine("Layout mapping");
        Check(ModLayout.MapEntry("BepInEx/plugins/BetterSMT.dll", "Seiko-BetterSMT") == "plugins/Seiko-BetterSMT/BetterSMT.dll", "BepInEx/plugins → plugins/Owner-Name");
        Check(ModLayout.MapEntry("plugins/Sub/a.dll", "O-N") == "plugins/O-N/Sub/a.dll", "plugins/ keeps subfolders");
        Check(ModLayout.MapEntry("Mod.dll", "O-N") == "plugins/O-N/Mod.dll", "root dll → plugins/Owner-Name");
        Check(ModLayout.MapEntry("manifest.json", "O-N") == "plugins/O-N/manifest.json", "manifest → plugins/Owner-Name");
        Check(ModLayout.MapEntry("BepInEx/config/x.cfg", "O-N") == "config/x.cfg", "config not namespaced");
        Check(ModLayout.MapEntry("patchers/p.dll", "O-N") == "patchers/O-N/p.dll", "patchers namespaced");
        Check(ModLayout.MapEntry("Assets/pic.png", "O-N") == "plugins/O-N/Assets/pic.png", "other folders → plugins/Owner-Name");
        try { ModLayout.MapEntry("../evil.dll", "O-N"); Check(false, "rejects .."); } catch (InvalidDataException) { Check(true, "rejects .."); }

        Console.WriteLine("Versions and dependency refs");
        Check(Thunderstore.CompareVersions("1.10.0", "1.9.9") > 0, "1.10.0 > 1.9.9");
        Check(Thunderstore.CompareVersions("5.4.2100", "5.4.2100") == 0, "equal");
        var dep = DependencyRef.Parse("M3lowdy32-Custom_Products-1.2.0");
        Check(dep.FullName == "M3lowdy32-Custom_Products" && dep.Version == "1.2.0", "parse dependency string");

        Console.WriteLine("Text helpers");
        var plain = TextUtil.MarkdownToPlain("# Title\n\n**Bold** [link](http://x) ![img](a.png)\n- item\n<b>tag</b>");
        Check(plain == "Title\n\nBold link \n\u2022 item\ntag", "markdown to plain text: " + plain.Replace("\n", "\\n"));
        Check(TextUtil.Count(34300) == "34.3k" && TextUtil.Count(999) == "999", "download counts");

        Console.WriteLine("Live package list");
        var json = http.GetStringAsync(Thunderstore.ListUrl).Result;
        var all = Thunderstore.ParseList(json);
        var browsable = all.Where(Thunderstore.IsBrowsable).ToList();
        Console.WriteLine($"  {all.Count} packages, {browsable.Count} browsable");
        Check(all.Count > 20, "parsed package list");
        Check(!browsable.Any(p => p.FullName == "BepInEx-BepInExPack"), "BepInExPack hidden");
        Check(browsable.All(p => !p.Deprecated), "deprecated hidden");
        var byName = all.ToDictionary(p => p.FullName, StringComparer.OrdinalIgnoreCase);

        var withDeps = browsable.First(p => p.Latest.Dependencies.Any(d => !Thunderstore.IsModLoader(DependencyRef.Parse(d).FullName) && byName.ContainsKey(DependencyRef.Parse(d).FullName)));
        var missing = new List<string>();
        var plan = ModInstaller.PlanInstall(withDeps, byName, new Dictionary<string, InstalledMod>(), missing);
        Console.WriteLine($"  plan for {withDeps.FullName}: {string.Join(", ", plan.Select(v => v.FullName))}");
        Check(plan.Count >= 2 && plan.Last().FullName.StartsWith(withDeps.FullName + "-"), "dependencies come before the mod");
        Check(plan.All(v => !Thunderstore.IsModLoader(DependencyRef.Parse(v.FullName).FullName)), "BepInExPack never planned");

        Console.WriteLine("Install into a temp BepInEx folder");
        var root = Path.Combine(Path.GetTempPath(), "smtmb-test-" + Guid.NewGuid().ToString("N"), "BepInEx");
        Directory.CreateDirectory(root);
        var installer = new ModInstaller(root);
        foreach (var v in plan)
        {
            var owner = DependencyRef.Parse(v.FullName).FullName;
            var zip = http.GetByteArrayAsync(v.DownloadUrl).Result;
            var result = installer.Install(owner, zip);
            Console.WriteLine($"  installed {v.FullName}: {result.Files} files, deferred={result.Deferred}");
            Check(!result.Deferred && File.Exists(Path.Combine(root, "plugins", owner, "manifest.json")), $"{owner} installed with manifest");
        }
        var installed = installer.ScanInstalled();
        Check(plan.All(v => installed.ContainsKey(DependencyRef.Parse(v.FullName).FullName)), "scan finds installed mods");
        Check(ModInstaller.PlanInstall(withDeps, byName, installed, new List<string>()).Count == 1, "installed dependencies are skipped");

        var better = byName["Seiko-BetterSMT"];
        installer.Install("Seiko-BetterSMT", http.GetByteArrayAsync(better.Latest.DownloadUrl).Result);
        Check(File.Exists(Path.Combine(root, "plugins", "Seiko-BetterSMT", "BetterSMT.dll")), "BetterSMT dll mapped out of BepInEx/plugins");

        Console.WriteLine("Locked files are staged for the next launch");
        var dll = Path.Combine(root, "plugins", "Seiko-BetterSMT", "BetterSMT.dll");
        File.WriteAllText(Path.Combine(root, "plugins", "Seiko-BetterSMT", "stale.txt"), "old version leftover");
        var zipBytes = http.GetByteArrayAsync(better.Latest.DownloadUrl).Result;
        using (File.Open(dll, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            var staged = installer.Install("Seiko-BetterSMT", zipBytes);
            Check(staged.Deferred, "update deferred while dll locked");
            Check(installer.ScanInstalled()["Seiko-BetterSMT"].PendingInstall, "scan reports pending install");
            Check(!installer.Uninstall("Seiko-BetterSMT"), "uninstall deferred while dll locked");
            Check(installer.ScanInstalled()["Seiko-BetterSMT"].PendingRemoval, "scan reports pending removal");
        }
        // Simulate the next launch: clear, copy, clear → removed
        PendingOps.Apply(root, m => Console.WriteLine("  preloader: " + m));
        Check(!Directory.Exists(Path.Combine(root, "plugins", "Seiko-BetterSMT")), "pending ops applied in order (ends removed)");
        Check(!Directory.Exists(PendingOps.StagingDir(root)), "staging cleaned up");

        using (File.Open(Path.Combine(root, "plugins", owner0(plan), "manifest.json"), FileMode.Open, FileAccess.Read, FileShare.None))
            installer.Install(owner0(plan), http.GetByteArrayAsync(plan[0].DownloadUrl).Result);
        File.WriteAllText(Path.Combine(root, "plugins", owner0(plan), "stale.txt"), "x");
        PendingOps.Apply(root, m => Console.WriteLine("  preloader: " + m));
        Check(File.Exists(Path.Combine(root, "plugins", owner0(plan), "manifest.json")) &&
              !File.Exists(Path.Combine(root, "plugins", owner0(plan), "stale.txt")), "staged update replaces old files");

        Check(installer.Uninstall(owner0(plan)) && !Directory.Exists(Path.Combine(root, "plugins", owner0(plan))), "uninstall removes folder");

        Console.WriteLine("Removing mods");
        var manifestDir = Path.Combine(root, "plugins", "Gone-OldMod");
        Directory.CreateDirectory(manifestDir);
        File.WriteAllText(Path.Combine(manifestDir, "manifest.json"), "{\"name\":\"OldMod\",\"version_number\":\"1.0.2\",\"description\":\"Taken down\"}");
        var gone = installer.ScanInstalled()["Gone-OldMod"];
        Check(gone.Version == "1.0.2" && gone.Description == "Taken down", "scan reads manifest description");
        var local = Thunderstore.FromInstalled(gone);
        Check(local.Local && local.Owner == "Gone" && local.Name == "OldMod" && local.Latest.Version == "1.0.2", "stand-in package for mods not on Thunderstore");
        Check(installer.Uninstall("Gone-OldMod") && !Directory.Exists(manifestDir), "mods not on Thunderstore can be removed");

        installer.Install("Seiko-BetterSMT", zipBytes);
        using (File.Open(dll, FileMode.Open, FileAccess.Read, FileShare.None))
            Check(installer.Install("Seiko-BetterSMT", zipBytes).Deferred, "update staged while dll locked");
        Check(!installer.Uninstall("Seiko-BetterSMT"), "removing a mod with a staged update reports restart needed");
        Check(!Directory.Exists(Path.Combine(root, "plugins", "Seiko-BetterSMT")), "unlocked folders deleted right away");
        Check(installer.ScanInstalled()["Seiko-BetterSMT"].PendingRemoval, "scan reports pending removal");
        PendingOps.Apply(root, m => Console.WriteLine("  preloader: " + m));
        Check(!Directory.Exists(Path.Combine(root, "plugins", "Seiko-BetterSMT")), "staged update doesn't bring a removed mod back");

        Directory.Delete(Path.GetDirectoryName(root), recursive: true);
        Console.WriteLine(failures == 0 ? "\nAll tests passed." : $"\n{failures} test(s) FAILED.");
        return failures == 0 ? 0 : 1;
    }

    static string owner0(List<PackageVersion> plan) => DependencyRef.Parse(plan[0].FullName).FullName;
}
