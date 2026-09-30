using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace SMTRandomAnnouncements
{
    [BepInPlugin(Guid, Name, Version)]
    public class RandomAnnouncementsPlugin : BaseUnityPlugin
    {
        public const string Guid = "smt.installandmanager.randomannouncements";
        public const string Name = "SMT Random Announcements";
        public const string Version = "0.1.2";

        internal static RandomAnnouncementsPlugin Instance;
        internal static ConfigEntry<int> PerDay;
        internal static ConfigEntry<string> Voices;
        internal static ConfigEntry<bool> AnnounceInChat;
        internal static ConfigEntry<bool> ShowInChat;
        internal static ConfigEntry<KeyboardShortcut> AnnounceNow;
        internal static BepInEx.Logging.ManualLogSource Log;

        void Awake()
        {
            Instance = this;
            Log = Logger;
            PerDay = Config.Bind("General", "Announcements per day", 8,
                new ConfigDescription("How many random announcements play each in-game day, at random times between " +
                    "opening and closing. A day in the store lasts about 15 real minutes.",
                    new AcceptableValueRange<int>(1, 30)));
            PerDay.SettingChanged += (_, __) => Announcer.Reschedule();
            Voices = Config.Bind("General", "Voices", string.Join(", ", Voice.Names),
                "Comma-separated voices to pick from at random. Available: " + string.Join(", ", Voice.Names) + ".");
            AnnounceInChat = Config.Bind("General", "Announce in chat", true,
                "The host also posts each announcement in the game chat. Players who have this mod use it to hear the " +
                "same voice; players without it just see the chat line.");
            ShowInChat = Config.Bind("General", "Show in chat", true,
                "Show the host's announcements as chat lines. They play over the speakers either way.");
            AnnounceNow = Config.Bind("General", "Announce now", new KeyboardShortcut(KeyCode.F9),
                "Host only: play a random announcement straight away.");

            Announcements.Load(Paths.ConfigPath);
            new Harmony(Guid).PatchAll(typeof(RandomAnnouncementsPlugin).Assembly);
            Logger.LogInfo($"{Name} {Version} loaded.");
        }

        void Update() => Announcer.Update();
    }
}
