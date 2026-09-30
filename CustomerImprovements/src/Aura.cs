using System.Collections.Generic;
using HarmonyLib;
using HighlightPlus;
using Mirror;
using UnityEngine;

namespace SMTCustomerImprovements
{
    enum AuraStyle { Party, Stink, Haze }

    // A glow around a customer's whole body, drawn with the highlight effect the game uses elsewhere.
    // Party-goers cycle through the colours; hobos pulse a murky green and brown; stoners drift in a hazy green.
    // Runs on every player with the mod: the host names the customer, and each player adds the glow once the
    // customer's model is there.
    static class Aura
    {
        class Glow
        {
            public HighlightEffect fx;
            public AuraStyle style;
            public float offset;
        }

        const float UpdateEvery = 0.1f;

        static readonly Color StinkFrom = new Color(0.45f, 0.55f, 0.1f);
        static readonly Color StinkTo = new Color(0.4f, 0.25f, 0.08f);
        static readonly Color HazeFrom = new Color(0.3f, 0.8f, 0.3f);
        static readonly Color HazeTo = new Color(0.65f, 0.75f, 0.65f);

        static readonly List<(uint netId, AuraStyle style, float until)> pending = new List<(uint, AuraStyle, float)>();
        static readonly List<Glow> glows = new List<Glow>();
        static float nextUpdate;

        static readonly AccessTools.FieldRef<NPC_Info, GameObject> CharacterOBJ =
            AccessTools.FieldRefAccess<NPC_Info, GameObject>("characterOBJ");

        public static void Request(uint netId, AuraStyle style) => pending.Add((netId, style, Time.time + 10f));

        public static void Update()
        {
            AddPending();
            if (glows.Count == 0 || Time.time < nextUpdate) return;
            nextUpdate = Time.time + UpdateEvery;
            for (int i = glows.Count - 1; i >= 0; i--)
            {
                var glow = glows[i];
                if (glow.fx == null)
                {
                    glows.RemoveAt(i);
                    continue;
                }
                glow.fx.glowHQColor = Colour(glow);
                glow.fx.UpdateMaterialProperties();
            }
        }

        static Color Colour(Glow glow)
        {
            if (glow.style == AuraStyle.Party)
                return Color.HSVToRGB(Mathf.Repeat(glow.offset + Time.time * 0.3f, 1f), 0.85f, 1f);
            if (glow.style == AuraStyle.Haze)
                return Color.Lerp(HazeFrom, HazeTo, (Mathf.Sin((glow.offset + Time.time) * 0.5f) + 1f) / 2f);
            float wave = (Mathf.Sin((glow.offset + Time.time) * 1.2f) + 1f) / 2f;
            return Color.Lerp(StinkFrom, StinkTo, wave);
        }

        // The model is made when the customer shows up on this player's side, which can be a moment after the message
        static void AddPending()
        {
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                var (netId, style, until) = pending[i];
                if (NetworkClient.spawned.TryGetValue(netId, out var identity) && identity != null)
                {
                    var npc = identity.GetComponent<NPC_Info>();
                    var model = npc != null ? CharacterOBJ(npc) : null;
                    if (model != null)
                    {
                        Add(model, style);
                        pending.RemoveAt(i);
                        continue;
                    }
                }
                if (Time.time > until) pending.RemoveAt(i);
            }
        }

        static void Add(GameObject model, AuraStyle style)
        {
            var fx = model.GetComponent<HighlightEffect>();
            if (fx == null) fx = model.AddComponent<HighlightEffect>();
            var glow = new Glow { fx = fx, style = style, offset = Random.value * 10f };
            fx.effectGroup = TargetOptions.Children;
            fx.outline = 0f;
            fx.glow = style == AuraStyle.Party ? 2f : 1.2f;
            fx.glowWidth = style == AuraStyle.Party ? 1.5f : 1f;
            fx.glowQuality = HighlightPlus.QualityLevel.Highest;
            fx.glowHQColor = Colour(glow);
            fx.Refresh();
            fx.highlighted = true;
            glows.Add(glow);
        }
    }
}
