using UnityEngine;

namespace SMTCustomerImprovements
{
    // "Customers: 12" at the right side of the screen, a quarter of the way down. Every player counts on their own:
    // the game keeps every customer under the same parent object on the host and on the other players' side.
    // Children who came with a parent aren't customers, so they aren't counted.
    static class CustomerCounter
    {
        static GUIStyle style;
        static int styleHeight;

        public static void Draw()
        {
            if (!CustomerImprovementsPlugin.ShowCustomerCount.Value) return;
            var manager = NPC_Manager.Instance;
            if (manager == null || manager.customersnpcParentOBJ == null) return;

            var labelStyle = Style();
            var content = new GUIContent($"Customers: {manager.customersnpcParentOBJ.transform.childCount}");
            var size = labelStyle.CalcSize(content);
            float margin = 16f * Mathf.Max(1f, Screen.height / 1080f);
            var rect = new Rect(Screen.width - size.x - margin, Screen.height * 0.25f, size.x, size.y);
            GUI.Label(rect, content, labelStyle);
        }

        static GUIStyle Style()
        {
            // Rebuilt when the resolution changes so the counter keeps the same size on screen
            if (style != null && styleHeight == Screen.height) return style;
            styleHeight = Screen.height;
            if (style != null) Object.Destroy(style.normal.background);
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.1f, 0.85f));
            background.Apply();
            int padX = Mathf.RoundToInt(16 * scale), padY = Mathf.RoundToInt(8 * scale);
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(22 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(padX, padX, padY, padY),
                normal = { textColor = Color.white, background = background }
            };
            return style;
        }
    }
}
