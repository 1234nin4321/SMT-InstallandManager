using System;
using System.Collections.Generic;
using System.Linq;

namespace SMTRandomAnnouncements
{
    enum Effect
    {
        None,
        Megaphone,  // thin and crackly
        Muffled,    // talking from the back office
        Echo,       // big empty warehouse
        Rally       // loud, booming over a big crowd
    }

    // The game has one text-to-speech voice per language. Each voice here is the English one with a different
    // pitch and sound effect applied to the speakers while it plays.
    sealed class Voice
    {
        public readonly string Name;
        public readonly float Pitch;
        public readonly Effect Effect;

        Voice(string name, float pitch, Effect effect)
        {
            Name = name;
            Pitch = pitch;
            Effect = effect;
        }

        public static readonly Voice[] All =
        {
            new Voice("Announcer", 1f, Effect.None),
            new Voice("Store Manager", 0.86f, Effect.None),
            new Voice("Summer Intern", 1.15f, Effect.None),
            new Voice("Security", 0.8f, Effect.Megaphone),
            new Voice("Back Office", 0.95f, Effect.Muffled),
            new Voice("Warehouse", 0.92f, Effect.Echo),
            new Voice("Chipmunk", 1.45f, Effect.None),
            new Voice("Big Boss", 0.9f, Effect.Rally)
        };

        public static IEnumerable<string> Names => All.Select(v => v.Name);

        public static Voice Find(string name) =>
            All.FirstOrDefault(v => string.Equals(v.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));

        // The voices picked in the config, or the plain announcer if none of them are known
        public static List<Voice> Enabled()
        {
            var voices = RandomAnnouncementsPlugin.Voices.Value.Split(',')
                .Select(Find).Where(v => v != null).Distinct().ToList();
            if (voices.Count == 0) voices.Add(All[0]);
            return voices;
        }
    }
}
