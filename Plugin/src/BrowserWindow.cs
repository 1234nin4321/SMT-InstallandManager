using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using SMTModBrowser.Core;
using UnityEngine;

namespace SMTModBrowser
{
    /// <summary>The in-game mod browser, drawn with IMGUI.</summary>
    class BrowserWindow
    {
        enum SortMode { Popular, TopRated, Updated, Newest }
        enum LoadState { Idle, Loading, Loaded, Failed }

        static readonly string[] SortLabels = { "Popular", "Top rated", "Updated", "Newest" };
        const int MaxIconRequests = 4;

        readonly ModBrowserPlugin plugin;
        readonly ManualLogSource log;
        readonly ModInstaller installer;

        List<Package> packages = new List<Package>();
        Dictionary<string, Package> byName = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
        List<Package> view = new List<Package>();
        Dictionary<string, InstalledMod> installed = new Dictionary<string, InstalledMod>();
        readonly Dictionary<string, Package> localPackages = new Dictionary<string, Package>(StringComparer.OrdinalIgnoreCase);
        readonly HashSet<string> changedThisSession = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        LoadState state = LoadState.Idle;
        string loadError;
        DateTime loadedAt;

        string search = "";
        SortMode sort = SortMode.Popular;
        bool installedOnly;
        bool viewDirty = true;

        Package selected;
        readonly Dictionary<string, string> readmes = new Dictionary<string, string>();
        Vector2 listScroll, detailScroll;

        readonly Dictionary<string, Texture2D> icons = new Dictionary<string, Texture2D>();
        readonly HashSet<string> iconsRequested = new HashSet<string>();
        readonly Queue<string> iconQueue = new Queue<string>();
        int iconsInFlight;

        string busyMod, busyText;
        float busyProgress;
        string notice;
        Color noticeColor;

        // Remove needs a second click within a few seconds
        string confirmRemove;
        float confirmUntil;

        // IMGUI runs several passes per frame; state changes from clicks wait for the next layout pass
        readonly Queue<Action> deferred = new Queue<Action>();

        public BrowserWindow(ModBrowserPlugin plugin, ManualLogSource log, string bepinexRoot)
        {
            this.plugin = plugin;
            this.log = log;
            installer = new ModInstaller(bepinexRoot);
        }

        public void OnOpened()
        {
            installed = installer.ScanInstalled();
            viewDirty = true;
            if (state == LoadState.Idle || state == LoadState.Failed ||
                state == LoadState.Loaded && DateTime.UtcNow - loadedAt > TimeSpan.FromMinutes(10))
                Refresh();
        }

        void Refresh()
        {
            state = LoadState.Loading;
            plugin.StartCoroutine(Web.GetText(Thunderstore.ListUrl, json =>
            {
                try
                {
                    packages = Thunderstore.ParseList(json);
                    byName = packages.ToDictionary(p => p.FullName, StringComparer.OrdinalIgnoreCase);
                    if (selected != null && byName.TryGetValue(selected.FullName, out var fresh)) selected = fresh;
                    state = LoadState.Loaded;
                    loadedAt = DateTime.UtcNow;
                    viewDirty = true;
                }
                catch (Exception ex)
                {
                    Fail($"Couldn't read the mod list: {ex.Message}");
                }
            }, error => Fail($"Couldn't reach Thunderstore: {error}")));

            void Fail(string message)
            {
                state = LoadState.Failed;
                loadError = message;
                log.LogWarning(message);
            }
        }

        void RebuildView()
        {
            var terms = search.Trim().ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            // Installed mods are always listed so they can be removed, even if deprecated or gone from Thunderstore
            var local = installed.Values
                .Where(m => !byName.ContainsKey(m.FullName) && !Thunderstore.IsModLoader(m.FullName))
                .Select(LocalPackage);
            var query = packages
                .Where(p => Thunderstore.IsBrowsable(p) || installed.ContainsKey(p.FullName) && !Thunderstore.IsModLoader(p.FullName))
                .Concat(local)
                .Where(p => !installedOnly || installed.ContainsKey(p.FullName))
                .Where(p => terms.All(t =>
                    p.DisplayName.ToLowerInvariant().Contains(t) ||
                    p.Owner.ToLowerInvariant().Contains(t) ||
                    p.Latest.Description.ToLowerInvariant().Contains(t)));

            switch (sort)
            {
                case SortMode.TopRated: query = query.OrderByDescending(p => p.Rating); break;
                case SortMode.Updated: query = query.OrderByDescending(p => p.Updated); break;
                case SortMode.Newest: query = query.OrderByDescending(p => p.Created); break;
                default: query = query.OrderByDescending(p => p.TotalDownloads); break;
            }
            view = query.ToList();
            viewDirty = false;
        }

        Package LocalPackage(InstalledMod m)
        {
            if (!localPackages.TryGetValue(m.FullName, out var p) || p.Latest.Version != m.Version)
                localPackages[m.FullName] = p = Thunderstore.FromInstalled(m);
            return p;
        }

        // ---------------------------------------------------------------- drawing

        public void Draw()
        {
            if (Event.current.type == EventType.Layout)
            {
                while (deferred.Count > 0) deferred.Dequeue()();
                if (viewDirty) RebuildView();
            }
            if (Event.current.type == EventType.Repaint) PumpIcons();

            Styles.Ensure();
            var previousSkin = GUI.skin;
            var previousMatrix = GUI.matrix;
            GUI.skin = Styles.Skin;

            var scale = plugin.UiScale.Value > 0 ? plugin.UiScale.Value : Mathf.Clamp(Screen.height / 1080f, 0.8f, 2.5f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1));
            var screenW = Screen.width / scale;
            var screenH = Screen.height / scale;

            GUI.DrawTexture(new Rect(0, 0, screenW, screenH), Styles.Dim);
            var width = Mathf.Min(1180, screenW - 60);
            var height = Mathf.Min(760, screenH - 60);
            var rect = new Rect((screenW - width) / 2, (screenH - height) / 2, width, height);
            GUI.Box(rect, GUIContent.none, Styles.Window);

            GUILayout.BeginArea(new Rect(rect.x + 24, rect.y + 18, rect.width - 48, rect.height - 36));
            DrawHeader();
            GUILayout.Space(14);
            DrawToolbar();
            GUILayout.Space(14);
            GUILayout.BeginHorizontal(GUILayout.ExpandHeight(true));
            GUILayout.BeginVertical(GUILayout.Width((rect.width - 48) * 0.52f));
            DrawList();
            GUILayout.EndVertical();
            GUILayout.Space(16);
            GUILayout.BeginVertical(Styles.Panel, GUILayout.ExpandHeight(true));
            DrawDetails();
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();
            GUILayout.Space(12);
            DrawFooter();
            GUILayout.EndArea();

            GUI.matrix = previousMatrix;
            GUI.skin = previousSkin;
        }

        void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.BeginVertical();
            GUILayout.Label("Mod Browser", Styles.Title);
            GUILayout.Label($"Supermarket Together mods from Thunderstore  ·  press {plugin.ToggleKey.Value} to close", Styles.Subtitle);
            GUILayout.EndVertical();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("×", Styles.CloseButton)) deferred.Enqueue(() => plugin.SetOpen(false));
            GUILayout.EndHorizontal();
        }

        void DrawToolbar()
        {
            GUILayout.BeginHorizontal();

            GUI.SetNextControlName("smtmb-search");
            var newSearch = GUILayout.TextField(search, 80, Styles.Search, GUILayout.Width(300), GUILayout.Height(36));
            if (newSearch != search) { search = newSearch; viewDirty = true; }
            if (search.Length == 0 && GUI.GetNameOfFocusedControl() != "smtmb-search")
                GUI.Label(GUILayoutUtility.GetLastRect(), "Search mods…", Styles.Placeholder);

            GUILayout.Space(16);
            for (var i = 0; i < SortLabels.Length; i++)
            {
                var mode = (SortMode)i;
                if (GUILayout.Button(SortLabels[i], sort == mode ? Styles.SegmentOn : Styles.Segment, GUILayout.Height(36)))
                    deferred.Enqueue(() => { sort = mode; viewDirty = true; listScroll = Vector2.zero; });
            }

            GUILayout.FlexibleSpace();
            var installedCount = installed.Values.Count(m => !m.PendingRemoval);
            if (GUILayout.Button($"Installed ({installedCount})", installedOnly ? Styles.SegmentOn : Styles.Segment, GUILayout.Height(36)))
                deferred.Enqueue(() => { installedOnly = !installedOnly; viewDirty = true; listScroll = Vector2.zero; });

            GUI.enabled = state != LoadState.Loading;
            if (GUILayout.Button("Refresh", Styles.Segment, GUILayout.Height(36)))
                deferred.Enqueue(() => { installed = installer.ScanInstalled(); Refresh(); });
            GUI.enabled = true;

            GUILayout.EndHorizontal();
        }

        void DrawList()
        {
            if (view.Count == 0)
            {
                if (state == LoadState.Loading && packages.Count == 0) CenteredMessage("Loading mods from Thunderstore…", Styles.Muted);
                else if (state == LoadState.Failed && packages.Count == 0) CenteredMessage(loadError, Styles.Error);
                else CenteredMessage(installedOnly ? "No installed mods match." : "No mods match your search.", Styles.Muted);
                return;
            }

            listScroll = GUILayout.BeginScrollView(listScroll, false, true);
            foreach (var package in view)
            {
                var r = GUILayoutUtility.GetRect(10, 78, GUILayout.ExpandWidth(true));
                DrawRow(package, r);
                GUILayout.Space(6);
            }
            GUILayout.EndScrollView();
        }

        void DrawRow(Package p, Rect r)
        {
            var canRemove = CanRemove(p);
            var removeRect = new Rect(r.xMax - 94, r.yMax - 34, 80, 24);

            // The row button would otherwise swallow clicks meant for the Remove button drawn on top of it
            var e = Event.current;
            var rowRect = canRemove && e.isMouse && removeRect.Contains(e.mousePosition) ? new Rect(r.x, r.y, 0, 0) : r;
            if (GUI.Button(rowRect, GUIContent.none, selected == p ? Styles.RowSelected : Styles.Row))
                deferred.Enqueue(() => Select(p));

            GUI.DrawTexture(new Rect(r.x + 13, r.y + 13, 52, 52), Icon(p.Latest.IconUrl), ScaleMode.ScaleToFit);

            var x = r.x + 80;
            var status = Status(p);
            var pillWidth = 0f;
            if (status.Text != null)
            {
                var pill = Styles.Pill(status.Color);
                var size = pill.CalcSize(new GUIContent(status.Text));
                pillWidth = size.x + 12;
                GUI.Label(new Rect(r.xMax - size.x - 14, r.y + 13, size.x, size.y), status.Text, pill);
            }
            var textRight = canRemove ? removeRect.x - 10 : r.xMax - 14;

            GUI.Label(new Rect(x, r.y + 12, r.xMax - x - pillWidth - 14, 20), p.DisplayName, Styles.RowName);
            GUI.Label(new Rect(x, r.y + 33, textRight - x, 16), p.Local
                ? $"by {p.Owner}   ·   v{p.Latest.Version}   ·   not on Thunderstore"
                : $"by {p.Owner}   ·   {TextUtil.Count(p.TotalDownloads)} downloads   ·   {p.Rating} likes", Styles.OneLine);
            GUI.Label(new Rect(x, r.y + 51, textRight - x, 16), p.Latest.Description, Styles.OneLine);

            if (canRemove)
            {
                var confirming = IsConfirmingRemove(p);
                GUI.enabled = busyMod == null;
                if (GUI.Button(removeRect, confirming ? "Confirm?" : "Remove", confirming ? Styles.DangerSmallArmed : Styles.DangerSmall))
                    deferred.Enqueue(() => RemoveClicked(p, selectFirst: true));
                GUI.enabled = true;
            }
        }

        void DrawDetails()
        {
            if (selected == null)
            {
                CenteredMessage(state == LoadState.Loaded ? "Select a mod to see its details." : "", Styles.Muted);
                return;
            }
            var p = selected;
            var have = installed.TryGetValue(p.FullName, out var m) && !m.PendingRemoval ? m : null;

            detailScroll = GUILayout.BeginScrollView(detailScroll, false, false);

            GUILayout.BeginHorizontal();
            var iconRect = GUILayoutUtility.GetRect(88, 88, GUILayout.Width(88), GUILayout.Height(88));
            GUI.DrawTexture(iconRect, Icon(p.Latest.IconUrl), ScaleMode.ScaleToFit);
            GUILayout.Space(16);
            GUILayout.BeginVertical();
            GUILayout.Label(p.DisplayName, Styles.Heading);
            GUILayout.Label($"by {p.Owner}", Styles.SmallMuted);
            GUILayout.Space(6);
            if (p.Local)
            {
                GUILayout.Label($"v{p.Latest.Version}   ·   installed", Styles.SmallMuted);
                GUILayout.Label("Not in the Thunderstore list, so it can only be removed.", new GUIStyle(Styles.SmallMuted) { normal = { textColor = Styles.Warning } });
            }
            else
            {
                GUILayout.Label($"v{p.Latest.Version}   ·   updated {TextUtil.Ago(p.Updated)}   ·   {TextUtil.Size(p.Latest.FileSize)}", Styles.SmallMuted);
                GUILayout.Label($"{TextUtil.Count(p.TotalDownloads)} downloads   ·   {p.Rating} likes", Styles.SmallMuted);
                if (p.Deprecated)
                    GUILayout.Label("Deprecated by its author.", new GUIStyle(Styles.SmallMuted) { normal = { textColor = Styles.Warning } });
            }
            GUILayout.EndVertical();
            GUILayout.EndHorizontal();

            GUILayout.Space(16);
            DrawActions(p, have);

            if (notice != null && busyMod == null)
            {
                GUILayout.Space(10);
                GUILayout.Label(notice, new GUIStyle(Styles.SmallMuted) { normal = { textColor = noticeColor } });
            }

            GUILayout.Space(16);
            GUILayout.Label(p.Latest.Description, Styles.Body);

            var dependencies = p.Latest.Dependencies.Select(DependencyRef.Parse).Where(d => !Thunderstore.IsModLoader(d.FullName)).ToList();
            if (dependencies.Count > 0)
            {
                GUILayout.Space(16);
                GUILayout.Label("REQUIRES", Styles.SmallMuted);
                foreach (var d in dependencies)
                {
                    var name = byName.TryGetValue(d.FullName, out var dp) ? dp.DisplayName : d.FullName;
                    var isInstalled = installed.TryGetValue(d.FullName, out var dm) && !dm.PendingRemoval;
                    GUILayout.Label($"• {name} {d.Version}{(isInstalled ? "   (installed)" : "   (will be installed)")}", Styles.Small);
                }
            }

            GUILayout.Space(16);
            GUILayout.Label("ABOUT", Styles.SmallMuted);
            GUILayout.Space(4);
            GUILayout.Label(readmes.TryGetValue(p.FullName, out var readme) ? readme : "Loading…", Styles.Body);

            GUILayout.EndScrollView();
        }

        void DrawActions(Package p, InstalledMod have)
        {
            if (busyMod == p.FullName)
            {
                GUILayout.Label(busyText, Styles.Small);
                GUILayout.Space(6);
                var bar = GUILayoutUtility.GetRect(10, 6, GUILayout.ExpandWidth(true));
                GUI.DrawTexture(bar, Styles.ProgressTrack);
                GUI.DrawTexture(new Rect(bar.x, bar.y, Mathf.Max(6, bar.width * busyProgress), bar.height), Styles.ProgressFill);
                return;
            }

            GUILayout.BeginHorizontal();
            GUI.enabled = busyMod == null;

            if (!p.Local)
            {
                string action;
                if (have == null) action = "Install";
                else if (Thunderstore.CompareVersions(have.Version, p.Latest.Version) < 0) action = $"Update to v{p.Latest.Version}";
                else action = "Reinstall";
                if (GUILayout.Button(action, have == null || action.StartsWith("Update") ? Styles.PrimaryButton : Styles.Button))
                    deferred.Enqueue(() => plugin.StartCoroutine(InstallRoutine(p)));
                GUILayout.Space(8);
            }

            if (have != null)
            {
                var confirming = IsConfirmingRemove(p);
                if (GUILayout.Button(confirming ? "Click again to remove" : "Remove", confirming ? Styles.DangerButtonArmed : Styles.DangerButton))
                    deferred.Enqueue(() => RemoveClicked(p, selectFirst: false));
            }

            GUI.enabled = true;
            GUILayout.FlexibleSpace();
            if (p.PackageUrl != null && GUILayout.Button("View on Thunderstore", Styles.Button))
                Application.OpenURL(p.PackageUrl);
            GUILayout.EndHorizontal();
        }

        void DrawFooter()
        {
            GUILayout.BeginHorizontal(GUILayout.Height(28));
            string text;
            var color = Styles.Muted;
            switch (state)
            {
                case LoadState.Loading: text = "Refreshing mod list…"; break;
                case LoadState.Failed: text = loadError; color = Styles.Error; break;
                case LoadState.Loaded: text = $"{view.Count} mods  ·  mod list from thunderstore.io, updated {TextUtil.Ago(loadedAt)}"; break;
                default: text = ""; break;
            }
            GUILayout.Label(text, new GUIStyle(Styles.SmallMuted) { normal = { textColor = color }, alignment = TextAnchor.MiddleLeft, wordWrap = false }, GUILayout.Height(28));
            GUILayout.FlexibleSpace();

            if (changedThisSession.Count > 0)
            {
                var n = changedThisSession.Count;
                GUILayout.Label($"Restart the game to apply {n} change{(n == 1 ? "" : "s")}", Styles.Pill(Styles.Warning), GUILayout.Height(26));
            }
            GUILayout.EndHorizontal();
        }

        void CenteredMessage(string text, Color color)
        {
            GUILayout.FlexibleSpace();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(text, new GUIStyle(Styles.Body) { normal = { textColor = color }, alignment = TextAnchor.MiddleCenter });
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();
            GUILayout.FlexibleSpace();
        }

        struct StatusInfo
        {
            public string Text;
            public Color Color;
        }

        StatusInfo Status(Package p)
        {
            if (busyMod == p.FullName) return new StatusInfo { Text = "Installing…", Color = Styles.Info };
            if (!installed.TryGetValue(p.FullName, out var m)) return default;
            if (m.PendingRemoval) return new StatusInfo { Text = "Removed on restart", Color = Styles.Warning };
            if (m.PendingInstall || changedThisSession.Contains(p.FullName)) return new StatusInfo { Text = "Restart to load", Color = Styles.Info };
            if (Thunderstore.CompareVersions(m.Version, p.Latest.Version) < 0) return new StatusInfo { Text = "Update", Color = Styles.Warning };
            return new StatusInfo { Text = "Installed", Color = Styles.Accent };
        }

        // ---------------------------------------------------------------- actions

        void Select(Package p)
        {
            selected = p;
            detailScroll = Vector2.zero;
            notice = null;
            if (confirmRemove != p.FullName) confirmRemove = null;
            if (p.Local) readmes[p.FullName] = string.IsNullOrWhiteSpace(p.Latest.Description) ? "No description available." : p.Latest.Description;
            if (readmes.ContainsKey(p.FullName)) return;

            readmes[p.FullName] = "Loading…";
            plugin.StartCoroutine(Web.GetText(Thunderstore.ReadmeUrl(p), json =>
            {
                try
                {
                    var markdown = (Json.Parse(json) as Dictionary<string, object>)?.Str("markdown");
                    readmes[p.FullName] = string.IsNullOrWhiteSpace(markdown) ? "No description provided." : TextUtil.MarkdownToPlain(markdown);
                }
                catch (FormatException)
                {
                    readmes[p.FullName] = "Couldn't read this mod's description.";
                }
            }, error => readmes.Remove(p.FullName)));
        }

        IEnumerator InstallRoutine(Package target)
        {
            busyMod = target.FullName;
            busyProgress = 0;
            notice = null;

            var missing = new List<string>();
            var plan = ModInstaller.PlanInstall(target, byName, installed, missing);
            var deferredInstall = false;
            string error = null;

            for (var i = 0; i < plan.Count && error == null; i++)
            {
                var version = plan[i];
                var owner = DependencyRef.Parse(version.FullName).FullName;
                var name = byName.TryGetValue(owner, out var pkg) ? pkg.DisplayName : owner;
                busyText = plan.Count > 1 ? $"Downloading {name} ({i + 1} of {plan.Count})…" : $"Downloading {name}…";

                byte[] data = null;
                var step = i;
                yield return Web.GetBytes(version.DownloadUrl,
                    progress => busyProgress = (step + progress) / plan.Count,
                    bytes => data = bytes,
                    e => error = $"Download of {name} failed: {e}");
                if (error != null) break;

                busyText = $"Installing {name}…";
                try
                {
                    var result = installer.Install(owner, data);
                    deferredInstall |= result.Deferred;
                    changedThisSession.Add(owner);
                    log.LogInfo($"Installed {version.FullName} ({result.Files} files{(result.Deferred ? ", applies on restart" : "")})");
                }
                catch (Exception ex)
                {
                    error = $"Couldn't install {name}: {ex.Message}";
                }
                yield return null;
            }

            installed = installer.ScanInstalled();
            viewDirty = true;
            busyMod = null;

            if (error != null)
            {
                log.LogError(error);
                SetNotice(error, Styles.Error);
            }
            else if (missing.Count > 0)
                SetNotice($"Installed, but these requirements aren't on Thunderstore: {string.Join(", ", missing)}", Styles.Warning);
            else
                SetNotice(deferredInstall
                    ? "Downloaded. The update is applied when you restart the game."
                    : "Installed! Restart the game to load it.", Styles.Accent);
        }

        bool CanRemove(Package p) => installed.TryGetValue(p.FullName, out var m) && !m.PendingRemoval && busyMod != p.FullName;

        bool IsConfirmingRemove(Package p) => confirmRemove == p.FullName && Time.realtimeSinceStartup < confirmUntil;

        void RemoveClicked(Package p, bool selectFirst)
        {
            if (!IsConfirmingRemove(p))
            {
                confirmRemove = p.FullName;
                confirmUntil = Time.realtimeSinceStartup + 4;
                return;
            }
            confirmRemove = null;
            if (selectFirst && selected != p) Select(p);   // so the result notice is visible
            Uninstall(p);
        }

        void Uninstall(Package p)
        {
            try
            {
                var removedNow = installer.Uninstall(p.FullName);
                changedThisSession.Add(p.FullName);
                SetNotice(removedNow ? "Removed. Restart the game to finish unloading it." : "It'll be removed when you restart the game.", Styles.Accent);
                log.LogInfo($"Uninstalled {p.FullName}{(removedNow ? "" : " (on restart)")}");
            }
            catch (Exception ex)
            {
                SetNotice($"Couldn't remove: {ex.Message}", Styles.Error);
                log.LogError(ex);
            }
            installed = installer.ScanInstalled();
            viewDirty = true;
        }

        void SetNotice(string text, Color color)
        {
            notice = text;
            noticeColor = color;
        }

        // ---------------------------------------------------------------- icons

        Texture Icon(string url)
        {
            if (string.IsNullOrEmpty(url)) return Styles.IconPlaceholder;
            if (icons.TryGetValue(url, out var texture)) return texture;
            if (iconsRequested.Add(url)) iconQueue.Enqueue(url);
            return Styles.IconPlaceholder;
        }

        void PumpIcons()
        {
            while (iconsInFlight < MaxIconRequests && iconQueue.Count > 0)
            {
                var url = iconQueue.Dequeue();
                iconsInFlight++;
                plugin.StartCoroutine(LoadIcon(url));
            }
        }

        IEnumerator LoadIcon(string url)
        {
            yield return Web.GetTexture(url, texture => icons[url] = texture);
            iconsInFlight--;
        }
    }
}
