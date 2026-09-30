using System;
using System.Text.RegularExpressions;
using HarmonyLib;

namespace SMTUberEats
{
    // Tells the other players about deliveries through the game's own chat, which every client receives
    // whether or not it has the mod. Clients with the mod turn those lines into the banner instead.
    static class DeliveryChat
    {
        const string Sender = "Store delivery";
        static readonly Regex CountdownLine = new Regex(@"^Arriving in (\d+) seconds? \((\d+) box(?:es)?\)$");
        const string ArrivedPrefix = "Arrived: ";

        static readonly Action<PlayerObjectController, string, string> ReceiveChatMsg =
            AccessTools.MethodDelegate<Action<PlayerObjectController, string, string>>(
                AccessTools.Method(typeof(PlayerObjectController), "RpcReceiveChatMsg"));

        public static void AnnounceCountdown(int seconds, int boxes) =>
            Send($"Arriving in {seconds} {(seconds == 1 ? "second" : "seconds")} ({DeliveryBanner.Boxes(boxes)})");

        public static void AnnounceArrival(string summary) => Send(ArrivedPrefix + summary);

        static void Send(string message)
        {
            if (!UberEatsPlugin.AnnounceInChat.Value) return;
            try
            {
                var player = LobbyController.Instance != null ? LobbyController.Instance.LocalplayerController : null;
                if (player == null || !player.isServer) return;
                ReceiveChatMsg(player, Sender, message);
            }
            catch (Exception e)
            {
                UberEatsPlugin.Log.LogWarning($"Could not post the delivery in chat: {e.Message}");
            }
        }

        // Runs on every client with the mod, the host included, when a chat line arrives
        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_RpcReceiveChatMsg__String__String")]
        static class ReceivePatch
        {
            static bool Prefix(PlayerObjectController __instance, string playerName, string message)
            {
                if (playerName != Sender) return true;
                // The host already shows its own banner
                if (__instance.isServer) return false;

                var countdown = CountdownLine.Match(message);
                if (countdown.Success)
                    DeliveryBanner.StartCountdown(int.Parse(countdown.Groups[1].Value), int.Parse(countdown.Groups[2].Value));
                else if (message.StartsWith(ArrivedPrefix))
                    DeliveryBanner.ShowArrived(message.Substring(ArrivedPrefix.Length));
                else
                    return true;  // not one of ours after all
                return false;
            }
        }
    }
}
