using MelonLoader;

namespace Thanatophobia;

internal static class Settings {
    public static MelonPreferences_Entry<string> AppId { get; private set; }
    public static MelonPreferences_Entry<string> VoiceAppId { get; private set; }
    public static MelonPreferences_Entry<string> Region { get; private set; }
    public static MelonPreferences_Entry<int> SendRate { get; private set; }
    public static MelonPreferences_Entry<int> SerializationRate { get; private set; }
    public static MelonPreferences_Entry<byte> MaxPlayers { get; private set; }
    public static MelonPreferences_Entry<float> TalkingListRange { get; private set; }
    public static MelonPreferences_Entry<float> MicGain { get; private set; }
    public static MelonPreferences_Entry<string> Nickname { get; private set; }
    public static MelonPreferences_Entry<bool> VoiceDiagnostics { get; private set; }

    public static void Load() {
        var category = MelonPreferences.CreateCategory("Thanatophobia");
        AppId = category.CreateEntry("AppId", "", "Photon App ID (required; paste yours via the main menu button). Everyone you play with must use the same one.");
        VoiceAppId = category.CreateEntry("VoiceAppId", "", "Photon Voice App ID (optional; blank reuses the App ID above)");
        Region = category.CreateEntry("ServersRegion", "au", "Photon Cloud region code");
        SendRate = category.CreateEntry("SendRate", 20, "Photon send rate (ticks/sec)");
        SerializationRate = category.CreateEntry("SerializationRate", 20, "Photon serialization rate (ticks/sec)");
        MaxPlayers = category.CreateEntry("MaxPlayersUncap", byte.MaxValue, "Room max player cap after join");
        TalkingListRange = category.CreateEntry("TalkingListRange", 10f, "Metres within which a talking player is listed on screen (radio users always are)");
        MicGain = category.CreateEntry("HudMicGain", 6f, "Voice HUD bar sensitivity (higher = bars swing more for the same mic volume)");
        Nickname = category.CreateEntry("Nickname", "", "Your in-game name (blank = use your Steam name)");
        VoiceDiagnostics = category.CreateEntry("VoiceDiagnostics", true, "Log voice stream diagnostics to the console (set false once voice is stable)");
    }
}
