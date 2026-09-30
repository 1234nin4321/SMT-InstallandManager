using System.Collections.Generic;
using System.Linq;
using Mirror;
using UnityEngine;

namespace SMTRandomAnnouncements
{
    // Host only: plays random announcements at random times of the in-game day while the store is open
    static class Announcer
    {
        const float CheckEvery = 1f;
        const float FirstAt = 8.25f;   // 08:15, just after opening
        const float LastAt = 22.25f;   // 22:15, before the store closes at 22:30
        const int MaxLength = 300;     // the game's own limit for announcements typed at the desk

        static readonly List<float> schedule = new List<float>();  // today's times still to come, earliest first
        static bool scheduled;
        static float nextCheck;
        static Voice lastVoice;
        static string lastProblem;

        // A new number of announcements per day applies straight away to the rest of the day
        public static void Reschedule() => scheduled = false;

        public static void Update()
        {
            if (!NetworkServer.active)
            {
                scheduled = false;
                return;
            }

            if (RandomAnnouncementsPlugin.AnnounceNow.Value.IsDown()) TryAnnounce();

            if (Time.time < nextCheck) return;
            nextCheck = Time.time + CheckEvery;

            var game = GameData.Instance;
            if (game == null || !game.isSupermarketOpen)
            {
                scheduled = false;
                return;
            }
            if (!scheduled)
            {
                Schedule(game.timeOfDay);
                scheduled = true;
            }

            // When it can't play yet (another announcement is on), it tries again on the next check. Times that
            // went by meanwhile are dropped so they don't all play back to back.
            if (schedule.Count > 0 && game.timeOfDay >= schedule[0] && TryAnnounce())
                schedule.RemoveAll(t => t <= game.timeOfDay);
        }

        // Splits the rest of the opening hours into equal slots and picks a random time in each, so the
        // announcements come at random but don't bunch up
        static void Schedule(float now)
        {
            schedule.Clear();
            float from = Mathf.Max(now, FirstAt);
            int count = Mathf.RoundToInt(RandomAnnouncementsPlugin.PerDay.Value * (LastAt - from) / (LastAt - FirstAt));
            if (count <= 0) return;

            float slot = (LastAt - from) / count;
            for (int i = 0; i < count; i++)
                schedule.Add(from + slot * (i + Random.Range(0.15f, 0.85f)));
            RandomAnnouncementsPlugin.Log.LogInfo("Announcements today at " + string.Join(", ", schedule.Select(Clock)));
        }

        static string Clock(float time)
        {
            int minutes = Mathf.FloorToInt(time * 60f);
            return $"{minutes / 60:00}:{minutes % 60:00}";
        }

        static bool TryAnnounce()
        {
            var desk = Loudspeaker.Desk;
            if (desk == null) return Skip("Place an announcement desk in the store to hear random announcements.");
            if (Loudspeaker.Speakers.Count == 0) return Skip("Place at least one speaker in the store to hear random announcements.");
            if (desk.restrictPlayingInPublicGames) return Skip("The game doesn't play announcements in public games.");
            if (Loudspeaker.IsPlaying(desk)) return false;  // another announcement is on, try again shortly
            lastProblem = null;

            var voice = PickVoice();
            string text = Announcements.Pick(voice);
            if (text.Length > MaxLength) text = text.Substring(0, MaxLength);
            Loudspeaker.Play(desk, voice, text);
            RandomAnnouncementsPlugin.Log.LogInfo($"{voice.Name}: {text}");
            return true;
        }

        // Logs why nothing played, once until the reason changes
        static bool Skip(string problem)
        {
            if (problem != lastProblem) RandomAnnouncementsPlugin.Log.LogInfo(problem);
            lastProblem = problem;
            return false;
        }

        // Random, but not the same voice twice in a row when there's a choice
        static Voice PickVoice()
        {
            List<Voice> voices = Voice.Enabled();
            if (voices.Count > 1) voices.Remove(lastVoice);
            lastVoice = voices[Random.Range(0, voices.Count)];
            return lastVoice;
        }
    }
}
