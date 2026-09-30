using System;
using System.Reflection;
using HarmonyLib;
using Mirror;
using UnityEngine;

namespace SMTCustomerImprovements
{
    // Everything that goes between the host and the other players, plus the players' side of payment fraud:
    // the key that checks a payment and the flashing hint on fake bills and stolen cards.
    //
    // It all goes through the game's chat calls. The game's chat drops any message containing "</b>", so players
    // without the mod never see these lines, and a host without the mod passes them on unseen.
    // The host's announcements are plain chat lines that every player sees.
    static class Net
    {
        const string Sender = "Store security";
        const string HintPrefix = "<b>customer-improvements:hint:";
        const string CheckPrefix = "<b>customer-improvements:check:";
        const string SayPrefix = "<b>customer-improvements:say:";
        const string DayPrefix = "<b>customer-improvements:day:";
        const string ChildPrefix = "<b>customer-improvements:child:";
        const string PartyPrefix = "<b>customer-improvements:party:";
        const string HoboPrefix = "<b>customer-improvements:hobo:";
        const string StonerPrefix = "<b>customer-improvements:stoner:";
        const string CartPrefix = "<b>customer-improvements:cart:";
        // Marks a line to show above a customer's head as it is, instead of looking it up in the game's translations
        const string SayKey = "SMTCustomerImprovements.Say:";
        const string Suffix = "</b>";

        // How close a player has to be to a register to check its payment
        const float Reach = 4f;

        static readonly string[] PendingPaths =
            { PaymentHint.CashPath, PaymentHint.CardPath, "CashRegisterCanvas/Container", "CreditCardCanvas/Container" };

        static readonly Action<PlayerObjectController, string, string> ReceiveChatMsg =
            AccessTools.MethodDelegate<Action<PlayerObjectController, string, string>>(
                AccessTools.Method(typeof(PlayerObjectController), "RpcReceiveChatMsg"));

        static readonly Action<NPC_Info, string, string> ShowAboveHead =
            AccessTools.MethodDelegate<Action<NPC_Info, string, string>>(
                AccessTools.Method(typeof(NPC_Info), "UserCode_RPCNotificationAboveHead__String__String"));

        static readonly FieldInfo ChatInputField = AccessTools.Field(typeof(ChatController), "ChatInputField");
        static ChatController chat;

        static PlayerObjectController LocalPlayer =>
            LobbyController.Instance != null ? LobbyController.Instance.LocalplayerController : null;

        public static void Update()
        {
            if (!CustomerImprovementsPlugin.CheckPayment.Value.IsDown() || Typing()) return;
            var register = NearestPayingRegister();
            if (register == null) return;

            if (NetworkServer.active)
            {
                Fraud.Check(register);
                return;
            }
            var player = LocalPlayer;
            if (player != null) player.SendChatMsg(CheckPrefix + register.netId + Suffix);
        }

        // The register closest to the player where a customer is paying. Only the host keeps the registers in the
        // game's register folder; on the other players' side they sit elsewhere, so they're looked up by type.
        static Data_Container NearestPayingRegister()
        {
            var camera = Camera.main;
            if (camera == null) return null;

            Data_Container nearest = null;
            float best = Reach;
            foreach (var register in UnityEngine.Object.FindObjectsOfType<Data_Container>())
            {
                float distance = Vector3.Distance(camera.transform.position, register.transform.position);
                if (distance > best || !IsRegister(register) || !PaymentPending(register)) continue;
                nearest = register;
                best = distance;
            }
            return nearest;
        }

        // Registers are the containers with a spot where customers hand over their payment
        static bool IsRegister(Data_Container container) => container.transform.Find("Payments") != null;

        static bool PaymentPending(Data_Container register)
        {
            foreach (var path in PendingPaths)
            {
                var part = register.transform.Find(path);
                if (part != null && part.gameObject.activeSelf) return true;
            }
            return false;
        }

        static bool Typing()
        {
            try
            {
                if (chat == null) chat = UnityEngine.Object.FindObjectOfType<ChatController>();
                if (chat == null || ChatInputField == null) return false;
                // TMP_InputField.isFocused, read by reflection so the mod doesn't need TextMeshPro
                var field = ChatInputField.GetValue(chat);
                var focused = field?.GetType().GetProperty("isFocused")?.GetValue(field);
                return focused is bool b && b;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static void SendHint(Data_Container register, FraudKind kind) =>
            Send(HintPrefix + register.netId + ":" + (kind == FraudKind.Cash ? "cash" : "card") + Suffix);

        // Shows the line above the customer's head for every player with the mod
        public static void Say(NPC_Info customer, string line) =>
            Send(SayPrefix + customer.netId + ":" + line + Suffix);

        // Tells every player with the mod to show this customer as a child, at this size
        public static void SendChild(NPC_Info child, float size) =>
            Send(ChildPrefix + child.netId + ":" + size.ToString(System.Globalization.CultureInfo.InvariantCulture) + Suffix);

        // Tells every player with the mod to give this customer the party aura
        public static void SendParty(NPC_Info member) => Send(PartyPrefix + member.netId + Suffix);

        // Tells every player with the mod to give this customer the stink cloud
        public static void SendHobo(NPC_Info hobo) => Send(HoboPrefix + hobo.netId + Suffix);

        // Tells every player with the mod to give this customer the hazy glow
        public static void SendStoner(NPC_Info stoner) => Send(StonerPrefix + stoner.netId + Suffix);

        // Tells every player with the mod what is in customers' carts
        public static void SendCarts(string contents) => Send(CartPrefix + contents + Suffix);

        public static void SendDayLosses(string totals) => Send(DayPrefix + totals + Suffix);

        public static void Announce(string message)
        {
            if (CustomerImprovementsPlugin.AnnounceInChat.Value) Send(message);
        }

        static void Send(string message)
        {
            try
            {
                var player = LocalPlayer;
                if (player == null || !player.isServer) return;
                ReceiveChatMsg(player, Sender, message);
            }
            catch (Exception e)
            {
                CustomerImprovementsPlugin.Log.LogWarning($"Could not send a chat line: {e.Message}");
            }
        }

        static T FindSpawned<T>(uint netId) where T : Component
        {
            var spawned = NetworkServer.active ? NetworkServer.spawned : NetworkClient.spawned;
            if (!spawned.TryGetValue(netId, out var identity) || identity == null) return null;
            return identity.GetComponent<T>();
        }

        static Data_Container FindRegister(uint netId)
        {
            var register = FindSpawned<Data_Container>(netId);
            return register != null && IsRegister(register) ? register : null;
        }

        // Runs on the host when a player sends a chat line
        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_CmdSendMessage__String__NetworkConnectionToClient")]
        static class CheckPatch
        {
            static bool Prefix(string message)
            {
                if (message == null || !message.StartsWith(CheckPrefix)) return true;
                var id = message.Substring(CheckPrefix.Length).Replace(Suffix, "");
                if (uint.TryParse(id, out var netId))
                {
                    var register = FindRegister(netId);
                    if (register != null) Fraud.Check(register);
                }
                return false;
            }
        }

        // Runs on every player with the mod, the host included, when a chat line arrives
        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_RpcReceiveChatMsg__String__String")]
        static class HintPatch
        {
            static bool Prefix(string playerName, string message)
            {
                if (message == null) return true;
                if (message.StartsWith(CheckPrefix)) return false;
                if (playerName != Sender) return true;
                if (message.StartsWith(SayPrefix))
                {
                    ShowLine(message.Substring(SayPrefix.Length));
                    return false;
                }
                if (message.StartsWith(HoboPrefix))
                {
                    Hobos.Received(message.Substring(HoboPrefix.Length).Replace(Suffix, ""));
                    return false;
                }
                if (message.StartsWith(CartPrefix))
                {
                    Carts.Received(message.Substring(CartPrefix.Length).Replace(Suffix, ""));
                    return false;
                }
                if (message.StartsWith(StonerPrefix))
                {
                    Stoners.Received(message.Substring(StonerPrefix.Length).Replace(Suffix, ""));
                    return false;
                }
                if (message.StartsWith(PartyPrefix))
                {
                    Party.Received(message.Substring(PartyPrefix.Length).Replace(Suffix, ""));
                    return false;
                }
                if (message.StartsWith(ChildPrefix))
                {
                    Families.Received(message.Substring(ChildPrefix.Length).Replace(Suffix, ""));
                    return false;
                }
                if (message.StartsWith(DayPrefix))
                {
                    DayLosses.Received(message.Substring(DayPrefix.Length).Replace(Suffix, ""));
                    return false;
                }
                if (!message.StartsWith(HintPrefix)) return true;

                var parts = message.Substring(HintPrefix.Length).Replace(Suffix, "").Split(':');
                if (CustomerImprovementsPlugin.ShowHint.Value && parts.Length == 2 && uint.TryParse(parts[0], out var netId))
                {
                    var register = FindRegister(netId);
                    if (register != null) PaymentHint.Start(register, cash: parts[1] == "cash");
                }
                return false;
            }
        }

        static void ShowLine(string body)
        {
            if (body.EndsWith(Suffix)) body = body.Substring(0, body.Length - Suffix.Length);
            int colon = body.IndexOf(':');
            if (colon < 0 || !uint.TryParse(body.Substring(0, colon), out var netId)) return;
            var customer = FindSpawned<NPC_Info>(netId);
            if (customer == null) return;
            try
            {
                ShowAboveHead(customer, SayKey + body.Substring(colon + 1), "");
            }
            catch (Exception e)
            {
                CustomerImprovementsPlugin.Log.LogWarning($"Could not show what the customer said: {e.Message}");
            }
        }

        [HarmonyPatch(typeof(LocalizationManager), nameof(LocalizationManager.GetLocalizationString))]
        static class SayKeyPatch
        {
            static bool Prefix(string key, ref string __result)
            {
                if (key == null || !key.StartsWith(SayKey)) return true;
                __result = key.Substring(SayKey.Length);
                return false;
            }
        }

        // Every payment ends with the register being cleared, which is when the hint goes away
        [HarmonyPatch(typeof(Data_Container), "UserCode_RpcClearCheckoutData")]
        static class ClearPatch
        {
            static void Postfix(Data_Container __instance)
            {
                PaymentHint.Stop(__instance);
            }
        }
    }
}
