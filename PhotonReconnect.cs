using HarmonyLib;
using Il2Cpp;
using Il2CppPhoton.Pun;
using Il2CppPhoton.Realtime;
using MelonLoader;

namespace Thanatophobia;

// Apply App ID / region changes live by cycling the Photon connection. The PhotonServerSettings getter
// already feeds the new values, so a reconnect picks them up; we only suppress the game's error screen
// during our own intentional disconnect, then reconnect once it has fully dropped.
internal static class PhotonReconnect {
    public static bool Suppressing { get; private set; }
    private static bool _queued;

    public static void Apply() {
        if (!PhotonNetwork.IsConnected) { PhotonNetwork.ConnectUsingSettings(); return; }
        Suppressing = true;
        PhotonNetwork.Disconnect();
    }

    public static void Handled() {
        Suppressing = false;
        _queued = true; // reconnect from Update, out of the callback and once the drop completes
    }

    public static void Update() {
        if (!_queued || PhotonNetwork.NetworkClientState != ClientState.Disconnected) return;
        _queued = false;
        PhotonNetwork.ConnectUsingSettings();
        MelonLogger.Msg("[Thanatophobia] Photon reconnected with new settings.");
    }
}

[HarmonyPatch(typeof(MainManager), "OnDisconnected")]
internal static class ReconnectSuppressPatch {
    private static bool Prefix() {
        if (!PhotonReconnect.Suppressing) return true; // a real disconnect: let the game show its error
        PhotonReconnect.Handled();
        return false; // skip the error screen for our intentional reconnect
    }
}
