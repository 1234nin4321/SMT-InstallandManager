using System;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SMTCustomerImprovements
{
    // Fake cash and stolen cards that nobody caught are paid like any other purchase. At the end of the day the fake
    // bills turn up in the till and the card company takes its money back: the host takes the day's total off the
    // store's funds, and every player with the mod sees it as its own line in the end-of-day summary.
    static class DayLosses
    {
        // Host: what got through today
        static float cashLost, cardLost;
        static int cashCount, cardCount;

        // Every player: what the host said was lost, for the summary about to be shown
        static float[] pending;

        const string RobbedField = "moneyLostBecauseRobbingTMP";
        const string BalanceField = "dayBalanceTMP";
        static GameObject line;

        public static void Add(FraudKind kind, float amount)
        {
            if (amount <= 0f) return;
            if (kind == FraudKind.Cash) { cashLost += amount; cashCount++; }
            else { cardLost += amount; cardCount++; }
        }

        static void Reset()
        {
            cashLost = cardLost = 0f;
            cashCount = cardCount = 0;
        }

        static float Round(float amount) => Mathf.Round(amount * 100f) / 100f;

        // For the chat lines between players
        static string Money(float amount) => Round(amount).ToString(CultureInfo.InvariantCulture);

        static void Settle(GameData game)
        {
            float total = Round(cashLost + cardLost);
            if (total > 0f)
                game.NetworkgameFunds = Round(Mathf.Clamp(game.gameFunds - total, 0f, 2.14E+09f));

            Net.SendDayLosses(string.Join(":", Money(cashLost), cashCount, Money(cardLost), cardCount));

            if (cashCount > 0)
                Net.Announce($"Counting the till: {cashCount} fake {(cashCount == 1 ? "bill" : "bills")} slipped through today. ${Money(cashLost)} lost.");
            if (cardCount > 0)
                Net.Announce($"The card company took back ${Money(cardLost)} for {cardCount} stolen {(cardCount == 1 ? "card" : "cards")}.");
            Reset();
        }

        // Every player: the host's totals for today, sent just before the summary
        public static void Received(string body)
        {
            var parts = body.Split(':');
            if (parts.Length != 4) return;
            var values = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out values[i])) return;
            pending = values;
        }

        static Component SummaryText(string field) =>
            AccessTools.Field(typeof(GameCanvas), field)?.GetValue(GameCanvas.Instance) as Component;

        // TextMeshPro's text property, set by reflection so the mod doesn't need TextMeshPro
        static void SetText(Component text, string value) =>
            text.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance)?.SetValue(text, value);

        // A copy of the "money lost because of robbing" line, placed right under it
        static Component Line()
        {
            var robbed = SummaryText(RobbedField);
            if (line != null) return line.GetComponent(robbed.GetType());

            var source = (RectTransform)robbed.transform;
            var parent = source.parent;
            line = UnityEngine.Object.Instantiate(robbed.gameObject, parent);
            line.name = "FakePaymentsLost";
            line.transform.SetSiblingIndex(source.GetSiblingIndex() + 1);

            // In a layout group the new line finds its own place; otherwise make room for it below the robbing line
            bool laidOut = parent.GetComponent("VerticalLayoutGroup") != null || parent.GetComponent("GridLayoutGroup") != null;
            if (!laidOut)
            {
                float step = RowHeight(source);
                foreach (Transform sibling in parent)
                {
                    if (sibling == line.transform || sibling == source || !(sibling is RectTransform rect)) continue;
                    if (rect.anchoredPosition.y < source.anchoredPosition.y - 0.5f)
                        rect.anchoredPosition -= new Vector2(0f, step);
                }
                ((RectTransform)line.transform).anchoredPosition = source.anchoredPosition - new Vector2(0f, step);
            }
            return line.GetComponent(robbed.GetType());
        }

        // The distance between the robbing line and the closest line above or below it
        static float RowHeight(RectTransform source)
        {
            float best = float.MaxValue;
            foreach (Transform sibling in source.parent)
            {
                if (sibling == source || !(sibling is RectTransform rect)) continue;
                float gap = Mathf.Abs(rect.anchoredPosition.y - source.anchoredPosition.y);
                if (gap > 1f && gap < best) best = gap;
            }
            return best < float.MaxValue ? best : source.rect.height;
        }

        // Runs on the host when the day is ended, just before everyone is sent the summary
        [HarmonyPatch(typeof(GameData), "UserCode_CmdEndDayFromButton")]
        static class EndDayPatch
        {
            static void Prefix(GameData __instance)
            {
                if (__instance.timeOfDay > 22f && !__instance.isSupermarketOpen) Settle(__instance);
            }
        }

        // A new game starts with a clean slate
        [HarmonyPatch(typeof(NPC_Manager), "Awake")]
        static class NewGamePatch
        {
            static void Postfix() => Reset();
        }

        // Runs on every player when the end-of-day summary is filled in
        [HarmonyPatch(typeof(GameCanvas), nameof(GameCanvas.TriggerEndDayStats))]
        static class SummaryPatch
        {
            static void Postfix(float dBenefits, float mLostBecauseRobbing, float lCost, float rCost, float emploCost,
                float mSpentOnProducts, float oCosts)
            {
                var values = pending;
                pending = null;
                if (values == null) return;
                try
                {
                    float lost = values[0] + values[2];
                    SetText(Line(), $"Fake cash & stolen cards ({values[1] + values[3]}): -${Round(lost)}");
                    if (lost <= 0f) return;

                    // The game's own balance, less what was lost
                    float balance = Round(dBenefits) - Round(mLostBecauseRobbing) - Round(lCost) - Round(rCost) - Round(emploCost)
                        - Round(mSpentOnProducts) - Mathf.Abs(Round(oCosts) + Round(mSpentOnProducts)) - lost;
                    balance = Round(balance);
                    SetText(SummaryText(BalanceField), (balance >= 0f ? "+$" : "-$") + Mathf.Abs(balance));
                }
                catch (Exception e)
                {
                    CustomerImprovementsPlugin.Log.LogWarning($"Could not add the fake payments to the summary: {e.Message}");
                }
            }
        }
    }
}
