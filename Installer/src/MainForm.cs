using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using SMTInstaller.UI;
using static SMTInstaller.UI.Theme;

namespace SMTInstaller
{
    class MainForm : Form
    {
        const string GameFolderName = "Supermarket Together";

        /// <summary>Always installed; Install stays disabled until all of them are found.</summary>
        static readonly ModPackage[] RequiredMods =
        {
            new ModPackage
            {
                DisplayName = "BepInEx",
                Description = "Mod loader that lets plugins run inside the game",
                Glyph = GlyphCode,
                Repo = "BepInEx/BepInEx",
                AssetPattern = new Regex(@"^BepInEx_win_x64_.*\.zip$", RegexOptions.IgnoreCase),
                VersionFile = "BepInEx/core/BepInEx.dll",
                RequiredFiles = new[] { "BepInEx/core/BepInEx.Preloader.dll", "winhttp.dll", "doorstop_config.ini" },
            },
            new ModPackage
            {
                DisplayName = "Configuration Manager",
                Description = "In-game settings menu for your mods — press F1",
                Glyph = GlyphSettings,
                Repo = "BepInEx/BepInEx.ConfigurationManager",
                AssetPattern = new Regex(@"^BepInEx\.ConfigurationManager_BepInEx5_.*\.zip$", RegexOptions.IgnoreCase),
                VersionFile = "BepInEx/plugins/ConfigurationManager/ConfigurationManager.dll",
            },
            new ModPackage
            {
                DisplayName = "Mod Browser",
                Description = "Browse and install Thunderstore mods in game \u2014 press F6",
                Glyph = GlyphShop,
                Repo = SelfUpdater.Repo,
                AssetPattern = new Regex(@"^SMTModBrowser_.*\.zip$", RegexOptions.IgnoreCase),
                VersionFile = "BepInEx/plugins/SMTModBrowser/SMTModBrowser.dll",
                RequiredFiles = new[] { "BepInEx/patchers/SMTModBrowser/SMTModBrowser.Preloader.dll" },
            },
        };

        readonly HeaderPanel header = new HeaderPanel();
        readonly FolderCard folderCard = new FolderCard();
        // Required mods first, then the optional ones from optional-mods.json once it has loaded
        readonly List<(ModPackage Mod, ModCard Card)> mods;
        // Tab 0 holds the game folder and the required mods, tab 1 the optional mods
        readonly TabStrip tabs = new TabStrip();
        readonly Panel requiredPage = new Panel { BackColor = Background };
        readonly Panel optionalPage = new Panel { BackColor = Background, Visible = false };
        readonly Label optionalHint = new Label { Text = "Loading optional mods…", AutoEllipsis = true, Font = Small, ForeColor = TextMuted };
        readonly FlatButton installButton = new FlatButton { Text = "Install", Glyph = GlyphDownload, Kind = ButtonKind.Primary, Enabled = false };
        readonly FlatButton detailsButton = new FlatButton { Text = "Show details", Glyph = GlyphChevronDown, Kind = ButtonKind.Ghost };
        readonly Label statusLabel = new Label { AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft, Font = Small, ForeColor = TextMuted };
        readonly RoundedPanel logPanel = new RoundedPanel { Visible = false };
        readonly TextBox logBox = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            ScrollBars = ScrollBars.Vertical,
            Dock = DockStyle.Fill,
            BackColor = Surface,
            ForeColor = TextMuted,
            Font = Mono,
        };

        string gamePath;
        bool versionsResolved, busy;
        int pagesTop, requiredModsTop, collapsedHeight, expandedHeight;

        public MainForm()
        {
            SuspendLayout();
            Text = "Supermarket Together Mod Installer";
            BackColor = Background;
            ForeColor = Theme.Text;
            Font = Body;
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            AutoScaleMode = AutoScaleMode.None;   // sizes are scaled by Theme.S instead
            DoubleBuffered = true;
            Icon = AppIcon(32) ?? Icon.ExtractAssociatedIcon(Application.ExecutablePath);

            header.Title = "Supermarket Together";
            header.Subtitle = "Mod Installer";
            header.Badge = $"v{SelfUpdater.CurrentVersion}";

            mods = RequiredMods.Select(m => (m, new ModCard { Title = m.DisplayName, Description = m.Description, Glyph = m.Glyph })).ToList();
            logPanel.Padding = new Padding(S(14), S(10), S(6), S(10));
            logPanel.Controls.Add(logBox);

            BuildLayout();
            ResumeLayout();

            folderCard.BrowseClicked += (s, e) => Browse();
            installButton.Click += async (s, e) => await Install();
            detailsButton.Click += (s, e) => ToggleDetails();
            tabs.SelectedIndexChanged += (s, e) => ShowTab(tabs.SelectedIndex);
            header.AboutClicked += (s, e) => ShowAbout();
            KeyPreview = true;
            KeyDown += (s, e) => { if (e.KeyCode == Keys.F1) { e.Handled = true; ShowAbout(); } };
            logBox.HandleCreated += (s, e) => SetWindowTheme(logBox.Handle, "DarkMode_Explorer", null);
            Shown += async (s, e) => await Initialize();
        }

        void BuildLayout()
        {
            var width = S(720);
            var pad = S(28);
            var inner = width - pad * 2;

            header.SetBounds(0, 0, width, S(116));
            Controls.Add(header);

            tabs.AddTab("Game & Required Mods");
            tabs.AddTab("Optional Mods");
            tabs.SetBounds(pad, header.Bottom, inner, tabs.Height);
            Controls.Add(tabs);
            pagesTop = tabs.Bottom + S(20);

            // Positions inside the pages are relative to the page, which sits at x = pad
            AddSection(requiredPage, "GAME FOLDER", 0);
            folderCard.SetBounds(0, S(24), inner, folderCard.Height);
            requiredPage.Controls.Add(folderCard);
            AddSection(requiredPage, "REQUIRED MODS", folderCard.Bottom + S(22));
            requiredModsTop = folderCard.Bottom + S(22) + S(24);

            optionalHint.SetBounds(S(2), 0, inner - S(2), S(20));
            optionalPage.Controls.Add(optionalHint);

            Controls.AddRange(new Control[] { requiredPage, optionalPage, detailsButton, statusLabel, installButton, logPanel });
            ClientSize = new Size(width, 0);
            LayoutMods();
            AcceptButton = installButton;
        }

        /// <summary>Places the mod cards on their tabs and everything below them; runs again when optional mods are added.</summary>
        void LayoutMods()
        {
            var width = ClientSize.Width;
            var pad = S(28);
            var inner = width - pad * 2;

            int PlaceCards(Panel page, bool optional, int top)
            {
                var bottom = top;
                foreach (var (mod, card) in mods.Where(m => m.Mod.Optional == optional))
                {
                    card.SetBounds(0, bottom, inner, card.Height);
                    if (card.Parent == null) page.Controls.Add(card);
                    bottom = card.Bottom + S(10);
                }
                return bottom;
            }

            // Both tabs get the taller one's height, so switching tabs doesn't resize the window
            var pageHeight = Math.Max(PlaceCards(requiredPage, false, requiredModsTop), PlaceCards(optionalPage, true, optionalHint.Bottom + S(14)));
            requiredPage.SetBounds(pad, pagesTop, inner, pageHeight);
            optionalPage.SetBounds(pad, pagesTop, inner, pageHeight);
            var y = pagesTop + pageHeight + S(10);

            var footerHeight = S(44);
            detailsButton.SetBounds(pad - S(10), y + (footerHeight - S(36)) / 2, S(140), S(36));
            installButton.SetBounds(width - pad - S(172), y, S(172), footerHeight);
            statusLabel.SetBounds(detailsButton.Right + S(8), y, installButton.Left - detailsButton.Right - S(24), footerHeight);
            y += footerHeight;

            collapsedHeight = y + pad;
            logPanel.SetBounds(pad, y + S(18), inner, S(170));
            expandedHeight = logPanel.Bottom + pad;
            ClientSize = new Size(width, logPanel.Visible ? expandedHeight : collapsedHeight);
        }

        static void AddSection(Control parent, string title, int y) =>
            parent.Controls.Add(new Label { Text = title, Font = Section, ForeColor = TextMuted, AutoSize = true, Location = new Point(S(2), y) });

        void ShowTab(int index)
        {
            requiredPage.Visible = index == 0;
            optionalPage.Visible = index == 1;
        }

        // Ctrl+Tab / Ctrl+Shift+Tab switch tabs; with two tabs both directions just flip
        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == (Keys.Control | Keys.Tab) || keyData == (Keys.Control | Keys.Shift | Keys.Tab))
            {
                tabs.SelectedIndex = (tabs.SelectedIndex + 1) % tabs.Count;
                return true;
            }
            return base.ProcessCmdKey(ref msg, keyData);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            DarkTitleBar(Handle);
        }

        async Task Initialize()
        {
            if (await UpdateSelf()) return;

            SetStatus("Looking for Supermarket Together…");
            var found = await Task.Run(() => SteamLocator.FindGame(GameFolderName));
            if (found != null)
            {
                SetGamePath(found, FolderStatus.Found);
                Log($"Found game: {found}");
            }
            else
            {
                folderCard.Status = FolderStatus.NotFound;
                Log("Could not find Supermarket Together in your Steam libraries.");
            }

            SetStatus("Checking GitHub for the latest versions…");
            Log("Checking GitHub for the latest versions...");
            var optionalLoaded = LoadOptionalMods();
            var results = await Task.WhenAll(mods.ToList().Select(m => ResolveMod(m.Mod, m.Card)));
            versionsResolved = results.All(ok => ok);
            await optionalLoaded;

            RefreshInstalled();
            ShowReadyStatus();
        }

        /// <summary>Checks which mods are already in the game folder and updates the cards and the Install button.</summary>
        void RefreshInstalled()
        {
            foreach (var (mod, card) in mods)
            {
                var wasInstalled = mod.InstalledVersion != null;
                mod.InstalledVersion = gamePath == null ? null : InstallCheck.InstalledVersion(mod, gamePath);
                if (mod.Asset == null) continue;   // not found on GitHub, keep showing that
                // Optional mods without a version file can't be checked; trust a successful install
                if (mod.VersionFile == null && card.State == ModState.Installed) continue;

                card.Progress = 0;
                if (mod.InstalledVersion == null)
                {
                    card.State = ModState.Ready;
                    card.Detail = null;
                }
                else if (mod.NeedsInstall)
                {
                    card.State = ModState.Outdated;
                    card.Detail = $"v{InstallCheck.Format(mod.InstalledVersion)} installed";
                }
                else
                {
                    card.State = ModState.Installed;
                    card.Detail = "up to date";
                }
                // Tick optional mods that are already installed, so updates for them are picked up
                if (mod.Optional && mod.InstalledVersion != null && !wasInstalled) card.Checked = true;
            }
            UpdateInstallButton();
        }

        /// <summary>Mods to install: required or ticked, and missing or out of date.</summary>
        List<(ModPackage Mod, ModCard Card)> Pending() =>
            mods.Where(m => m.Mod.Asset != null && (!m.Mod.Optional || m.Mod.Selected) && m.Mod.NeedsInstall).ToList();

        void ShowReadyStatus()
        {
            var pending = Pending();
            if (!versionsResolved)
                SetStatus("Couldn't reach GitHub. Check your connection and restart.", Theme.Error);
            else if (gamePath == null)
                SetStatus("Choose the game folder to continue.", Warning);
            else if (pending.Count == 0)
                SetStatus("Everything is installed and up to date.", Success);
            else if (pending.All(m => m.Mod.InstalledVersion != null))
                SetStatus($"{pending.Count} update{(pending.Count == 1 ? "" : "s")} available. Close the game, then click Update.", Warning);
            else
                SetStatus("Ready. Make sure the game is closed, then click Install.");
        }

        /// <summary>Offers to install a newer installer. Returns true if it did and this instance is closing.</summary>
        async Task<bool> UpdateSelf()
        {
            SetStatus("Checking for installer updates…");
            InstallerUpdate update;
            try
            {
                update = await SelfUpdater.FindUpdate();
            }
            catch (Exception ex)
            {
                Log($"Couldn't check for installer updates: {ex.Message}");
                return false;
            }
            if (update == null) return false;

            Log($"Installer v{update.Version} is available (you have v{SelfUpdater.CurrentVersion}).");
            var answer = MessageBox.Show(this,
                $"A new version of the installer is available.\n\nYou have v{SelfUpdater.CurrentVersion}, the latest is v{update.Version}.\n\nUpdate now?",
                Text, MessageBoxButtons.YesNo, MessageBoxIcon.Information);
            if (answer != DialogResult.Yes)
            {
                Log("Update skipped.");
                return false;
            }

            SetBusy(true);
            try
            {
                SetStatus($"Downloading installer v{update.Version}…", Info);
                Log($"Downloading {update.Asset.Name}...");
                var newExe = await SelfUpdater.Apply(update, new Progress<double>(p => SetStatus($"Downloading installer v{update.Version}… {p:P0}", Info)));
                Log("Update installed, restarting.");
                SelfUpdater.Restart(newExe);
                Close();
                return true;
            }
            catch (Exception ex)
            {
                SetBusy(false);
                Log($"ERROR (update): {ex.Message}");
                var open = MessageBox.Show(this,
                    $"The update couldn't be installed:\n{ex.Message}\n\nOpen the download page to get it manually?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
                if (open == DialogResult.Yes && update.PageUrl != null)
                    Process.Start(update.PageUrl);
                return false;
            }
        }

        /// <summary>Adds a card for each mod in optional-mods.json and looks up its download.</summary>
        async Task LoadOptionalMods()
        {
            CatalogEntry[] catalog;
            try
            {
                catalog = await GitHubReleases.OptionalMods(SelfUpdater.Repo);
            }
            catch (Exception ex)
            {
                Log($"Couldn't load the list of optional mods: {ex.Message}");
                optionalHint.Text = "Couldn't load the list of optional mods. Check your connection and restart.";
                return;
            }

            var added = new List<(ModPackage Mod, ModCard Card)>();
            foreach (var entry in catalog)
            {
                Regex pattern;
                try
                {
                    pattern = new Regex(entry.Asset, RegexOptions.IgnoreCase);
                }
                catch (ArgumentException)
                {
                    Log($"Skipping optional mod \"{entry.Name}\": invalid asset pattern.");
                    continue;
                }
                if (string.IsNullOrWhiteSpace(entry.Name)) continue;

                var mod = new ModPackage
                {
                    DisplayName = entry.Name,
                    Description = entry.Description ?? "",
                    Glyph = GlyphPackage,
                    Repo = string.IsNullOrWhiteSpace(entry.Repo) ? SelfUpdater.Repo : entry.Repo,
                    AssetPattern = pattern,
                    Optional = true,
                    Selected = entry.Selected,
                    VersionFile = string.IsNullOrWhiteSpace(entry.File) ? null : entry.File,
                };
                // Locked until its download is found
                var card = new ModCard { Title = mod.DisplayName, Description = mod.Description, Glyph = mod.Glyph, Optional = true, Checked = mod.Selected, Locked = true };
                card.CheckedChanged += (s, e) =>
                {
                    mod.Selected = card.Checked;
                    if (busy) return;
                    UpdateInstallButton();
                    ShowReadyStatus();
                };
                added.Add((mod, card));
            }
            if (added.Count == 0)
            {
                optionalHint.Text = "No optional mods are available right now.";
                return;
            }

            optionalHint.Text = "Tick the mods you want. They're installed together with the required mods.";
            mods.AddRange(added);
            SuspendLayout();
            LayoutMods();
            ResumeLayout();

            await Task.WhenAll(added.Select(async m =>
            {
                if (await ResolveMod(m.Mod, m.Card)) m.Card.Locked = busy;
                else m.Card.Checked = false;
            }));
        }

        async Task<bool> ResolveMod(ModPackage mod, ModCard card)
        {
            try
            {
                await GitHubReleases.Resolve(mod);
                card.Version = mod.Version;
                card.State = ModState.Ready;
                Log($"  {mod.DisplayName} {mod.Version}  ({mod.Asset.Name})");
                return true;
            }
            catch (Exception ex)
            {
                card.State = ModState.Failed;
                card.Detail = mod.Optional ? "not available yet" : "couldn't reach GitHub";
                Log($"ERROR ({mod.DisplayName}): {ex.Message}");
                return false;
            }
        }

        void Browse()
        {
            using (var dialog = new FolderBrowserDialog { Description = "Select the Supermarket Together folder" })
            {
                if (gamePath != null) dialog.SelectedPath = gamePath;
                if (dialog.ShowDialog(this) != DialogResult.OK) return;

                SetGamePath(dialog.SelectedPath, LooksLikeUnityGame(dialog.SelectedPath) ? FolderStatus.Selected : FolderStatus.Suspicious);
                Log($"Selected folder: {gamePath}");
                foreach (var (mod, card) in mods.Where(m => m.Mod.VersionFile == null && m.Card.State == ModState.Installed))
                    card.State = ModState.Ready;   // installed into the previous folder
                RefreshInstalled();
                ShowReadyStatus();
            }
        }

        void SetGamePath(string path, FolderStatus status)
        {
            gamePath = path;
            folderCard.FolderPath = path;
            folderCard.Status = status;
            UpdateInstallButton();
        }

        // Unity games have a "<Game>_Data" folder next to the exe
        static bool LooksLikeUnityGame(string path) => Directory.EnumerateDirectories(path, "*_Data").Any();

        async Task Install()
        {
            if (busy || gamePath == null) return;

            if (!Directory.Exists(gamePath))
            {
                ShowMessage("That folder doesn't exist anymore.", MessageBoxIcon.Warning);
                return;
            }
            if (!LooksLikeUnityGame(gamePath) &&
                MessageBox.Show(this, "This doesn't look like a Unity game folder (no *_Data folder).\n\nInstall here anyway?",
                    Text, MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            if (Process.GetProcessesByName(GameFolderName).Length > 0)
            {
                ShowMessage("Supermarket Together is running. Close the game and try again.", MessageBoxIcon.Warning);
                return;
            }

            // Only what's missing or out of date; if everything is current, the button reinstalls it all
            var toInstall = Pending();
            if (toInstall.Count == 0)
                toInstall = mods.Where(m => m.Mod.Asset != null && (!m.Mod.Optional || m.Mod.Selected)).ToList();

            SetBusy(true);
            foreach (var (mod, card) in toInstall)
            {
                card.State = ModState.Ready;
                card.Detail = null;
                card.Progress = 0;
            }

            var tempDir = Path.Combine(Path.GetTempPath(), "SMTInstaller-" + Guid.NewGuid());
            Directory.CreateDirectory(tempDir);
            ModCard current = null;
            try
            {
                foreach (var (mod, modCard) in toInstall)
                {
                    current = modCard;
                    tabs.SelectedIndex = mod.Optional ? 1 : 0;   // keep the card being installed in view

                    SetStatus($"Downloading {mod.DisplayName} {mod.Version}…", Info);
                    Log($"Downloading {mod.Asset.Name}...");
                    current.State = ModState.Downloading;
                    var zip = Path.Combine(tempDir, mod.Asset.Name);
                    var card = current;
                    await GitHubReleases.Download(mod.Asset.DownloadUrl, zip, new Progress<double>(p => card.Progress = p));

                    SetStatus($"Installing {mod.DisplayName}…", Info);
                    current.State = ModState.Installing;
                    var files = await Task.Run(() => ZipInstaller.ExtractOver(zip, gamePath));
                    current.State = ModState.Installed;
                    current.Detail = $"{files} files";
                    Log($"Installed {mod.DisplayName} {mod.Version} ({files} files).");
                }

                RefreshInstalled();
                SetStatus("All done! In game, press F6 to browse mods and F1 for mod settings.", Success);
                Log("Done. Start the game once so BepInEx can create its config files.");
            }
            catch (Exception ex)
            {
                if (current != null) current.State = ModState.Failed;
                SetStatus("Installation failed. See details below.", Theme.Error);
                Log($"ERROR: {ex.Message}");
                if (!logPanel.Visible) ToggleDetails();
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* temp cleanup is best effort */ }
                SetBusy(false);
            }
        }

        void ShowAbout()
        {
            using (var about = new AboutForm()) about.ShowDialog(this);
        }

        void SetBusy(bool value)
        {
            busy = value;
            folderCard.BrowseEnabled = !value;
            foreach (var (mod, card) in mods.Where(m => m.Mod.Optional && m.Mod.Asset != null))
                card.Locked = value;
            UpdateInstallButton();
        }

        void UpdateInstallButton()
        {
            var selected = mods.Count(m => m.Mod.Optional && m.Mod.Selected);
            tabs.SetBadge(1, selected > 0 ? $"{selected} selected" : null);
            installButton.Enabled = !busy && versionsResolved && gamePath != null;
            if (busy)
            {
                installButton.Text = "Installing…";
                return;
            }

            var pending = Pending();
            if (gamePath == null || pending.Any(m => m.Mod.InstalledVersion == null))
                SetInstallButton("Install", GlyphDownload, ButtonKind.Primary);
            else if (pending.Count > 0)
                SetInstallButton("Update", GlyphSync, ButtonKind.Primary);
            else
                SetInstallButton("Reinstall", GlyphRefresh, ButtonKind.Secondary);
        }

        void SetInstallButton(string text, string glyph, ButtonKind kind)
        {
            installButton.Text = text;
            installButton.Glyph = glyph;
            installButton.Kind = kind;
        }

        void ToggleDetails()
        {
            var show = !logPanel.Visible;
            logPanel.Visible = show;
            detailsButton.Text = show ? "Hide details" : "Show details";
            detailsButton.Glyph = show ? GlyphChevronUp : GlyphChevronDown;
            ClientSize = new Size(ClientSize.Width, show ? expandedHeight : collapsedHeight);
        }

        void SetStatus(string text, Color? color = null)
        {
            statusLabel.Text = text;
            statusLabel.ForeColor = color ?? TextMuted;
        }

        void ShowMessage(string text, MessageBoxIcon icon) => MessageBox.Show(this, text, Text, MessageBoxButtons.OK, icon);

        void Log(string line) => logBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {line}{Environment.NewLine}");

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string appName, string idList);
    }
}
