using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SMTRandomAnnouncements
{
    // Gives the speakers a voice's pitch and sound while the speech plays, leaving the bells before and after alone
    static class SpeakerEffects
    {
        // Only filters added here are switched on and off, never ones the game put on a speaker
        static readonly HashSet<Behaviour> ours = new HashSet<Behaviour>();

        public static IEnumerator Apply(AnnouncementsDesk desk, Voice voice)
        {
            var originalPitch = new Dictionary<AudioSource, float>();
            while (Loudspeaker.IsPlaying(desk))
            {
                foreach (var speaker in Loudspeaker.Speakers)
                {
                    var source = speaker != null ? speaker.GetComponent<AudioSource>() : null;
                    if (source == null) continue;
                    if (!originalPitch.ContainsKey(source)) originalPitch[source] = source.pitch;

                    bool speech = source.clip != null && Array.IndexOf(desk.announcementBellAudios, source.clip) < 0;
                    source.pitch = speech ? originalPitch[source] * voice.Pitch : originalPitch[source];
                    SetEffect(source, speech ? voice.Effect : Effect.None);
                }
                yield return null;
            }

            foreach (var entry in originalPitch)
            {
                if (entry.Key == null) continue;
                entry.Key.pitch = entry.Value;
                SetEffect(entry.Key, Effect.None);
            }
        }

        static void SetEffect(AudioSource source, Effect effect)
        {
            Toggle<AudioHighPassFilter>(source, effect == Effect.Megaphone, f => f.cutoffFrequency = 700f);
            Toggle<AudioDistortionFilter>(source, effect == Effect.Megaphone || effect == Effect.Rally,
                f => f.distortionLevel = effect == Effect.Rally ? 0.2f : 0.55f);
            Toggle<AudioLowPassFilter>(source, effect == Effect.Megaphone || effect == Effect.Muffled,
                f => f.cutoffFrequency = effect == Effect.Muffled ? 1200f : 3200f);
            Toggle<AudioEchoFilter>(source, effect == Effect.Echo || effect == Effect.Rally, f =>
            {
                bool rally = effect == Effect.Rally;
                f.delay = rally ? 180f : 110f;
                f.decayRatio = rally ? 0.3f : 0.4f;
                f.wetMix = rally ? 0.4f : 0.55f;
                f.dryMix = 1f;
            });
        }

        static void Toggle<T>(AudioSource source, bool on, Action<T> setUp) where T : Behaviour
        {
            var filter = source.GetComponent<T>();
            if (filter == null)
            {
                if (!on) return;
                filter = source.gameObject.AddComponent<T>();
                ours.Add(filter);
            }
            else if (!ours.Contains(filter)) return;

            if (on) setUp(filter);
            filter.enabled = on;
        }
    }
}
