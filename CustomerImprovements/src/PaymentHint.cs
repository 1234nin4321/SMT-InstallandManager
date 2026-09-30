using System.Collections.Generic;
using UnityEngine;

namespace SMTCustomerImprovements
{
    // Fake bills and stolen cards flash, pulsing between two colours, until the payment is over: purple and yellow
    // for fake cash, red and blue for a stolen card. Runs on every player with the mod, the host included.
    static class PaymentHint
    {
        class Hint
        {
            public Data_Container register;
            public Renderer[] renderers;
            public CanvasRenderer[] ui;
            public Color from, to;
            public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        }

        public const string CashPath = "Payments/Payment_Money";
        public const string CardPath = "Payments/Payment_Card";

        static readonly Color CashFrom = new Color(0.65f, 0.25f, 1f);
        static readonly Color CashTo = new Color(1f, 0.9f, 0.1f);
        static readonly Color CardFrom = new Color(1f, 0.15f, 0.15f);
        static readonly Color CardTo = new Color(0.2f, 0.45f, 1f);

        // Colour swings per second
        const float PulsesPerSecond = 1.5f;

        static readonly List<Hint> hints = new List<Hint>();

        public static void Start(Data_Container register, bool cash)
        {
            var part = register.transform.Find(cash ? CashPath : CardPath);
            if (part == null) return;
            Stop(register);
            hints.Add(new Hint
            {
                register = register,
                renderers = part.GetComponentsInChildren<Renderer>(true),
                ui = part.GetComponentsInChildren<CanvasRenderer>(true),
                from = cash ? CashFrom : CardFrom,
                to = cash ? CashTo : CardTo,
            });
        }

        public static void Stop(Data_Container register)
        {
            for (int i = hints.Count - 1; i >= 0; i--)
            {
                if (hints[i].register != register) continue;
                Paint(hints[i], null);
                hints.RemoveAt(i);
            }
        }

        public static void Update()
        {
            if (hints.Count == 0) return;
            // Lingers on each colour a little before swinging to the other, so it reads as flashing
            float wave = (Mathf.Sin(Time.time * PulsesPerSecond * 2f * Mathf.PI) + 1f) / 2f;
            float t = Mathf.SmoothStep(0f, 1f, wave);
            for (int i = hints.Count - 1; i >= 0; i--)
            {
                var hint = hints[i];
                if (hint.register == null)
                {
                    hints.RemoveAt(i);
                    continue;
                }
                Paint(hint, Color.Lerp(hint.from, hint.to, t));
            }
        }

        static void Paint(Hint hint, Color? color)
        {
            if (color.HasValue)
            {
                hint.block.SetColor("_Color", color.Value);
                hint.block.SetColor("_BaseColor", color.Value);
            }
            foreach (var renderer in hint.renderers)
                if (renderer != null) renderer.SetPropertyBlock(color.HasValue ? hint.block : null);
            foreach (var ui in hint.ui)
                if (ui != null) ui.SetColor(color ?? Color.white);
        }
    }
}
