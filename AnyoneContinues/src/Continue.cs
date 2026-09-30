using System;
using HarmonyLib;
using UnityEngine;

namespace SMTAnyoneContinues
{
    // At the end of the day the game waits on the host alone: GameData only listens for a key press on the server,
    // and only the host sees the "press any key" prompt. The host tells the other players when the prompt is up,
    // and a key press from any of them is passed back to the host, which starts the next day as if it pressed one.
    //
    // Both directions go through the game's chat calls. The game's chat drops any message containing "</b>",
    // so players without the mod never see these lines, and a host without the mod passes them on unseen.
    static class Continue
    {
        const string Sender = "Anyone Continues";
        const string ShowMessage = "<b>prompt:show</b>";
        const string HideMessage = "<b>prompt:hide</b>";
        const string PressMessage = "<b>anyone-continues:press</b>";

        // GameData resets its key press 0.25 s after showing the prompt; a press before that would be lost
        const float Settle = 0.3f;
        const float ResendAfter = 1f;

        static readonly AccessTools.FieldRef<GameData, bool> KeyPress = AccessTools.FieldRefAccess<GameData, bool>("keyPress");
        static readonly Action<PlayerObjectController, string, string> ReceiveChatMsg =
            AccessTools.MethodDelegate<Action<PlayerObjectController, string, string>>(
                AccessTools.Method(typeof(PlayerObjectController), "RpcReceiveChatMsg"));

        // Host: whether the prompt is up, and since when
        static bool hostPrompt;
        static float hostPromptSince;

        // Other players: whether the host said the prompt is up
        static bool clientPrompt;
        static float clientPromptSince;
        static float lastPressSent;

        static PlayerObjectController LocalPlayer =>
            LobbyController.Instance != null ? LobbyController.Instance.LocalplayerController : null;

        public static void Update()
        {
            var game = GameData.Instance;
            if (game == null || game.pressAnyKeyOBJ == null) return;

            if (game.isServer) WatchHostPrompt(game);
            else if (clientPrompt) WatchClientKeys();
        }

        static void WatchHostPrompt(GameData game)
        {
            bool shown = game.pressAnyKeyOBJ.activeSelf;
            if (shown == hostPrompt) return;
            hostPrompt = shown;
            hostPromptSince = Time.unscaledTime;
            Broadcast(shown ? ShowMessage : HideMessage);
        }

        static void WatchClientKeys()
        {
            if (!Input.anyKeyDown) return;
            if (Time.unscaledTime - clientPromptSince < Settle) return;
            if (Time.unscaledTime - lastPressSent < ResendAfter) return;
            var player = LocalPlayer;
            if (player == null) return;
            lastPressSent = Time.unscaledTime;
            player.SendChatMsg(PressMessage);
        }

        static void Broadcast(string message)
        {
            try
            {
                var player = LocalPlayer;
                if (player == null) return;
                ReceiveChatMsg(player, Sender, message);
            }
            catch (Exception e)
            {
                AnyoneContinuesPlugin.Log.LogWarning($"Could not tell the other players about the prompt: {e.Message}");
            }
        }

        static void SetClientPrompt(bool shown)
        {
            clientPrompt = shown;
            clientPromptSince = Time.unscaledTime;
            lastPressSent = float.MinValue;
            var game = GameData.Instance;
            if (game != null && game.pressAnyKeyOBJ != null) game.pressAnyKeyOBJ.SetActive(shown);
        }

        // Runs on the host when a player sends a chat line
        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_CmdSendMessage__String__NetworkConnectionToClient")]
        static class PressPatch
        {
            static bool Prefix(string message)
            {
                if (message != PressMessage) return true;
                var game = GameData.Instance;
                if (game != null && hostPrompt && Time.unscaledTime - hostPromptSince >= Settle)
                    KeyPress(game) = true;
                return false;
            }
        }

        // Runs on every player with the mod, the host included, when a chat line arrives
        [HarmonyPatch(typeof(PlayerObjectController), "UserCode_RpcReceiveChatMsg__String__String")]
        static class PromptPatch
        {
            static bool Prefix(PlayerObjectController __instance, string playerName, string message)
            {
                if (message == PressMessage) return false;
                if (playerName != Sender) return true;
                if (__instance.isServer) return false;   // the host shows its own prompt
                if (message == ShowMessage) SetClientPrompt(true);
                else if (message == HideMessage) SetClientPrompt(false);
                else return true;
                return false;
            }
        }

        // In case the hide message got lost, the new day always takes the prompt away
        [HarmonyPatch(typeof(GameData), "UserCode_RpcStartDay")]
        static class StartDayPatch
        {
            static void Postfix(GameData __instance)
            {
                if (!__instance.isServer && clientPrompt) SetClientPrompt(false);
            }
        }
    }
}
