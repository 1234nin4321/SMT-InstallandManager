using System.Collections.Generic;
using UnityEngine;

namespace SMTModBrowser
{
    /// <summary>IMGUI skin matching the SMT Installer's dark theme.</summary>
    static class Styles
    {
        public static readonly Color Background = Hex("#14161B");
        public static readonly Color Surface = Hex("#1C1F26");
        public static readonly Color SurfaceHover = Hex("#252932");
        public static readonly Color SurfacePressed = Hex("#2C313B");
        public static readonly Color Border = Hex("#2A2E37");
        public static readonly Color Text = Hex("#ECEFF4");
        public static readonly Color Muted = Hex("#8B93A1");
        public static readonly Color Disabled = Hex("#555C68");
        public static readonly Color Accent = Hex("#2ECC71");
        public static readonly Color AccentHover = Hex("#48D884");
        public static readonly Color AccentPressed = Hex("#27AE60");
        public static readonly Color OnAccent = Hex("#0B1F14");
        public static readonly Color Info = Hex("#5DADE2");
        public static readonly Color Warning = Hex("#F5B041");
        public static readonly Color Error = Hex("#EF5350");

        public static GUISkin Skin;
        public static GUIStyle Window, Panel, Title, Subtitle, Heading, Body, Small, SmallMuted, OneLine,
            Row, RowSelected, RowName, Button, PrimaryButton, DangerButton, DangerButtonArmed, DangerSmall, DangerSmallArmed, Segment, SegmentOn, Search, Placeholder, CloseButton;
        public static Texture2D Dim, IconPlaceholder, ProgressTrack, ProgressFill;

        static readonly Dictionary<Color, GUIStyle> pills = new Dictionary<Color, GUIStyle>();
        static readonly List<Texture2D> textures = new List<Texture2D>();

        /// <summary>Builds the styles on first use, and again if Unity unloaded the textures.</summary>
        public static void Ensure()
        {
            if (Skin != null && Window.normal.background != null) return;
            foreach (var t in textures) if (t != null) Object.Destroy(t);
            textures.Clear();
            pills.Clear();

            Skin = Object.Instantiate(GUI.skin);
            Skin.hideFlags = HideFlags.HideAndDontSave;
            Skin.settings.cursorColor = Text;
            Skin.settings.selectionColor = new Color(Accent.r, Accent.g, Accent.b, 0.35f);
            Skin.scrollView = new GUIStyle();
            Skin.verticalScrollbar = new GUIStyle { normal = { background = Rounded(Mix(Color.white, Surface, 0.03f), Color.clear, 4) },
                border = new RectOffset(5, 5, 5, 5), fixedWidth = 8, margin = new RectOffset(6, 0, 0, 0) };
            Skin.verticalScrollbarThumb = new GUIStyle { normal = { background = Rounded(Border, Color.clear, 4) },
                hover = { background = Rounded(Muted, Color.clear, 4) }, border = new RectOffset(5, 5, 5, 5), fixedWidth = 8 };
            Skin.verticalScrollbarUpButton = new GUIStyle();
            Skin.verticalScrollbarDownButton = new GUIStyle();
            Skin.horizontalScrollbar = new GUIStyle { fixedHeight = 0 };
            Skin.horizontalScrollbarThumb = new GUIStyle { fixedHeight = 0 };

            Dim = Solid(new Color(0, 0, 0, 0.6f));
            IconPlaceholder = Solid(SurfaceHover);
            ProgressTrack = Rounded(Mix(Color.white, Surface, 0.06f), Color.clear, 3);
            ProgressFill = Rounded(Info, Color.clear, 3);

            Window = Box(Background, Border, 14, new RectOffset(0, 0, 0, 0));
            Panel = Box(Surface, Border, 10, new RectOffset(18, 18, 16, 16));

            Title = Label(20, Text, FontStyle.Bold);
            Subtitle = Label(12, Muted);
            Heading = Label(17, Text, FontStyle.Bold);
            Heading.wordWrap = true;
            Body = Label(13, Text);
            Body.wordWrap = true;
            Small = Label(11, Text);
            SmallMuted = Label(11, Muted);
            SmallMuted.wordWrap = true;
            OneLine = Label(11, Muted);
            OneLine.clipping = TextClipping.Clip;
            RowName = Label(14, Text, FontStyle.Bold);
            RowName.clipping = TextClipping.Clip;

            Row = Interactive(Surface, SurfaceHover, SurfacePressed, Border, 10, Text);
            RowSelected = Interactive(Mix(Accent, Surface, 0.12f), Mix(Accent, Surface, 0.16f), Mix(Accent, Surface, 0.2f), Mix(Accent, Surface, 0.55f), 10, Text);

            Button = Interactive(Mix(Color.white, Surface, 0.05f), SurfaceHover, SurfacePressed, Border, 8, Text);
            Button.fontSize = 13;
            Button.fontStyle = FontStyle.Bold;
            Button.padding = new RectOffset(16, 16, 9, 9);
            Button.alignment = TextAnchor.MiddleCenter;

            PrimaryButton = Interactive(Accent, AccentHover, AccentPressed, Color.clear, 8, OnAccent);
            PrimaryButton.fontSize = 13;
            PrimaryButton.fontStyle = FontStyle.Bold;
            PrimaryButton.padding = new RectOffset(20, 20, 9, 9);
            PrimaryButton.alignment = TextAnchor.MiddleCenter;

            DangerButton = new GUIStyle(Button);
            DangerButton.normal.textColor = DangerButton.hover.textColor = DangerButton.active.textColor = Error;

            // Filled red while waiting for the confirming second click
            DangerButtonArmed = Interactive(Error, Mix(Color.white, Error, 0.15f), Mix(Color.black, Error, 0.15f), Color.clear, 8, Color.white);
            DangerButtonArmed.fontSize = Button.fontSize;
            DangerButtonArmed.fontStyle = FontStyle.Bold;
            DangerButtonArmed.padding = Button.padding;
            DangerButtonArmed.alignment = TextAnchor.MiddleCenter;

            DangerSmall = new GUIStyle(DangerButton) { fontSize = 11, padding = new RectOffset(8, 8, 4, 4) };
            DangerSmallArmed = new GUIStyle(DangerButtonArmed) { fontSize = 11, padding = new RectOffset(8, 8, 4, 4) };

            CloseButton = Interactive(Background, SurfaceHover, SurfacePressed, Color.clear, 8, Muted);
            CloseButton.hover.textColor = Text;
            CloseButton.fontSize = 18;
            CloseButton.alignment = TextAnchor.MiddleCenter;
            CloseButton.fixedWidth = CloseButton.fixedHeight = 34;

            Segment = Interactive(Surface, SurfaceHover, SurfacePressed, Border, 8, Muted);
            Segment.hover.textColor = Text;
            Segment.fontSize = 12;
            Segment.padding = new RectOffset(12, 12, 8, 8);
            Segment.alignment = TextAnchor.MiddleCenter;
            Segment.margin = new RectOffset(0, 6, 0, 0);
            SegmentOn = Interactive(Mix(Accent, Surface, 0.18f), Mix(Accent, Surface, 0.24f), Mix(Accent, Surface, 0.3f), Mix(Accent, Surface, 0.5f), 8, Accent);
            SegmentOn.fontSize = 12;
            SegmentOn.fontStyle = FontStyle.Bold;
            SegmentOn.padding = Segment.padding;
            SegmentOn.alignment = TextAnchor.MiddleCenter;
            SegmentOn.margin = Segment.margin;

            Search = Interactive(Surface, Surface, Surface, Border, 8, Text);
            Search.focused.background = Rounded(Surface, Mix(Accent, Surface, 0.6f), 8);
            Search.focused.textColor = Text;
            Search.fontSize = 13;
            Search.padding = new RectOffset(12, 12, 9, 9);
            Search.clipping = TextClipping.Clip;
            Placeholder = Label(13, Disabled);
            Placeholder.padding = Search.padding;
        }

        /// <summary>A small rounded tag in the given color.</summary>
        public static GUIStyle Pill(Color color)
        {
            if (pills.TryGetValue(color, out var style)) return style;
            style = new GUIStyle
            {
                normal = { background = Rounded(Mix(color, Surface, 0.18f), Color.clear, 9), textColor = color },
                border = new RectOffset(9, 9, 9, 9),
                padding = new RectOffset(9, 9, 3, 4),
                fontSize = 11,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            pills[color] = style;
            return style;
        }

        static GUIStyle Label(int size, Color color, FontStyle fontStyle = FontStyle.Normal) => new GUIStyle
        {
            fontSize = size,
            fontStyle = fontStyle,
            normal = { textColor = color },
            richText = false,
            padding = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 2, 2),
        };

        static GUIStyle Box(Color fill, Color border, int radius, RectOffset padding) => new GUIStyle
        {
            normal = { background = Rounded(fill, border, radius) },
            border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
            padding = padding,
        };

        static GUIStyle Interactive(Color normal, Color hover, Color active, Color border, int radius, Color text) => new GUIStyle
        {
            normal = { background = Rounded(normal, border, radius), textColor = text },
            hover = { background = Rounded(hover, border, radius), textColor = text },
            active = { background = Rounded(active, border, radius), textColor = text },
            onNormal = { background = Rounded(normal, border, radius), textColor = text },
            border = new RectOffset(radius + 1, radius + 1, radius + 1, radius + 1),
            margin = new RectOffset(0, 0, 0, 0),
        };

        static Texture2D Solid(Color color)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, color);
            t.Apply();
            textures.Add(t);
            return t;
        }

        /// <summary>A 9-sliceable rounded rectangle with anti-aliased edges and an optional 1px border.</summary>
        static Texture2D Rounded(Color fill, Color border, int radius)
        {
            var size = radius * 2 + 2;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
            var pixels = new Color[size * size];
            var half = size / 2f;
            for (var y = 0; y < size; y++)
            {
                for (var x = 0; x < size; x++)
                {
                    // Signed distance to the rounded edge: negative inside
                    var qx = Mathf.Abs(x + 0.5f - half) - (half - radius);
                    var qy = Mathf.Abs(y + 0.5f - half) - (half - radius);
                    var distance = new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - radius;

                    var color = fill;
                    if (border.a > 0) color = Color.Lerp(fill, border, Mathf.Clamp01(distance + 1.5f));
                    color.a *= Mathf.Clamp01(0.5f - distance);
                    pixels[y * size + x] = color;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            textures.Add(tex);
            return tex;
        }

        public static Color Mix(Color top, Color bottom, float amount) => Color.Lerp(bottom, top, amount);

        static Color Hex(string hex) => ColorUtility.TryParseHtmlString(hex, out var c) ? c : Color.magenta;
    }
}
