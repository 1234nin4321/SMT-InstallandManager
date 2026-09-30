using UnityEngine;

namespace SMTUberEats
{
    // Banner at the top of the screen: "Store delivery arrives in 7 s", then what arrived
    static class DeliveryBanner
    {
        const float ArrivedShownFor = 4f;

        static ManagerBlackboard board;  // host: read the live box count from the order list
        static int fixedBoxes;            // other players: the count the host announced
        static float arrivesAt = -1f;
        static string arrivedText;
        static float arrivedUntil = -1f;
        static GUIStyle style;
        static int styleHeight;

        public static void StartCountdown(ManagerBlackboard orderBoard, float seconds)
        {
            board = orderBoard;
            Start(seconds);
        }

        public static void StartCountdown(float seconds, int boxes)
        {
            board = null;
            fixedBoxes = boxes;
            Start(seconds);
        }

        static void Start(float seconds)
        {
            arrivesAt = Time.time + seconds;
            arrivedUntil = -1f;
        }

        public static void ShowArrived(string summary)
        {
            arrivesAt = -1f;
            arrivedText = "Store delivery arrived: " + summary;
            arrivedUntil = Time.time + ArrivedShownFor;
        }

        // "9 in storage, 3 by the delivery point"
        public static string Summary(int stored, int stacked)
        {
            int total = stored + stacked;
            return stacked == 0 ? $"{Boxes(total)} put in storage"
                : stored == 0 ? $"{Boxes(total)} by the delivery point"
                : $"{stored} in storage, {stacked} by the delivery point";
        }

        public static void Draw()
        {
            string text;
            if (Time.time < arrivesAt)
            {
                int seconds = Mathf.CeilToInt(arrivesAt - Time.time);
                int boxes = board != null ? board.idsToSpawn.Count : fixedBoxes;
                text = $"Store delivery arrives in {seconds} s  ·  {Boxes(boxes)}";
            }
            else if (Time.time < arrivedUntil)
            {
                text = arrivedText;
            }
            else return;

            var labelStyle = Style();
            var content = new GUIContent(text);
            var size = labelStyle.CalcSize(content);
            var rect = new Rect((Screen.width - size.x) / 2f, Screen.height * 0.08f, size.x, size.y);
            GUI.Label(rect, content, labelStyle);
        }

        public static string Boxes(int n) => n == 1 ? "1 box" : $"{n} boxes";

        static GUIStyle Style()
        {
            // Rebuilt when the resolution changes so the banner keeps the same size on screen
            if (style != null && styleHeight == Screen.height) return style;
            styleHeight = Screen.height;
            if (style != null) Object.Destroy(style.normal.background);
            float scale = Mathf.Max(1f, Screen.height / 1080f);
            var background = new Texture2D(1, 1);
            background.SetPixel(0, 0, new Color(0.08f, 0.08f, 0.1f, 0.85f));
            background.Apply();
            int padX = Mathf.RoundToInt(22 * scale), padY = Mathf.RoundToInt(10 * scale);
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(24 * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                padding = new RectOffset(padX, padX, padY, padY),
                normal = { textColor = Color.white, background = background }
            };
            return style;
        }
    }
}
