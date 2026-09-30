using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SMTRandomAnnouncements
{
    // Plays announcements through the game's own announcement desk, so every player hears them on the placed
    // speakers whether or not they have the mod. The voice goes along as a chat line, which players with the mod
    // pick up to apply the same voice.
    static class Loudspeaker
    {
        const string Sender = "Loudspeaker";
        static readonly Regex ChatLine = new Regex(@"^\[([^\]]+)\] (.+)$");
        const float PendingFor = 10f;

        static readonly AccessTools.FieldRef<AnnouncementsDesk, bool> IsPlayingField =
            AccessTools.FieldRefAccess<AnnouncementsDesk, bool>("announcementIsPlaying");
        static readonly Action<AnnouncementsDesk, int, string> RpcPlayAnnouncement =
            AccessTools.MethodDelegate<Action<AnnouncementsDesk, int, string>>(
                AccessTools.Method(typeof(AnnouncementsDesk), "RpcPlayAnnouncement"));
        static readonly Action<PlayerObjectController, string, string> RpcReceiveChatMsg =
            AccessTools.MethodDelegate<Action<PlayerObjectController, string, string>>(
                AccessTools.Method(typeof(PlayerObjectController), "RpcReceiveChatMsg"));

        static AnnouncementsDesk desk;
        static Voice pendingVoice;
        static string pendingText;
        static float pendingAt;

        // Only there once the player has placed the announcement desk
        public static AnnouncementsDesk Desk
        {
            get
            {
                if (desk == null) desk = Object.FindObjectOfType<AnnouncementsDesk>();
                return desk;
            }
        }

        public static bool IsPlaying(AnnouncementsDesk d) => d != null && IsPlayingField(d);

        public static List<GameObject> Speakers =>
            GameData.Instance != null ? GameData.Instance.GetComponent<NetworkSpawner>().speakersList : new List<GameObject>();

        // Host only
        public static void Play(AnnouncementsDesk d, Voice voice, string text)
        {
            if (!(RandomAnnouncementsPlugin.AnnounceInChat.Value && SendChat($"[{voice.Name}] {text}")))
                SetPending(voice, text);  // at least the host hears the voice
            RpcPlayAnnouncement(d, EnglishVoice(d), text);
        }

        static void SetPending(Voice voice, string text)
        {
            pendingVoice = voice;
            pendingText = text;
            pendingAt = Time.time;
        }

        static Voice TakePending(string text)
        {
            var voice = pendingVoice;
            bool matches = voice != null && pendingText == text && Time.time - pendingAt < PendingFor;
            pendingVoice = null;
            return matches ? voice : null;
        }

        // The desk's languages each come with their own voice; ours are all variations of the English one
        static int EnglishVoice(AnnouncementsDesk d)
        {
            for (int i = 0; i < d.languageNames.Length && i < d.voicesNames.Length; i++)
                if (d.languageNames[i] != null && d.languageNames[i].IndexOf("english", StringComparison.OrdinalIgnoreCase) >= 0)
                    return i;
            for (int i = 0; i < d.voicesNames.Length; i++)
                if (d.voicesNames[i] != null && d.voicesNames[i].StartsWith("en", StringComparison.OrdinalIgnoreCase))
                    return i;
            return 0;
        }

        static bool SendChat(string message)
        {
            try
            {
                var player = LobbyController.Instance != null ? LobbyController.Instance.LocalplayerController : null;
                if (player == null || !player.isServer) return false;
                RpcReceiveChatMsg(player, Sender, message);
                return true;
            }
            catch (Exception e)
            {
                RandomAnnouncementsPlugin.Log.LogWarning($"Could not post the announcement in chat: {e.Message}");
                return false;
            }
        }

        // Runs on every client with the mod, the host included. The chat line always arrives just before the
        // announcement itself, as both come from the host in that order.
        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_RpcReceiveChatMsg__String__String")]
        static class ReceiveChatPatch
        {
            static bool Prefix(string playerName, string message)
            {
                if (playerName != Sender) return true;
                var line = ChatLine.Match(message);
                if (!line.Success) return true;  // not one of ours after all

                // A voice this version doesn't know (the host has a newer mod) plays as the plain announcer
                SetPending(Voice.Find(line.Groups[1].Value) ?? Voice.All[0], line.Groups[2].Value);
                return RandomAnnouncementsPlugin.ShowInChat.Value;
            }
        }

        [HarmonyPatch(typeof(AnnouncementsDesk), "UserCode_RpcPlayAnnouncement__Int32__String")]
        static class PlayAnnouncementPatch
        {
            static void Prefix(AnnouncementsDesk __instance, out bool __state) => __state = IsPlaying(__instance);

            static void Postfix(AnnouncementsDesk __instance, string announcementString, bool __state)
            {
                var voice = TakePending(announcementString);
                // Skipped by the game: already playing one, no speakers, or a public game
                if (voice == null || __state || !IsPlaying(__instance)) return;

                RandomAnnouncementsPlugin.Instance.StartCoroutine(SpeakerEffects.Apply(__instance, voice));
            }
        }
    }
}
