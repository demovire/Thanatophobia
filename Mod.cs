using Il2CppPhoton.Pun;
using MelonLoader;

[assembly: MelonInfo(typeof(Thanatophobia.Mod), "Thanatophobia", "1.0.0", "demovire")]
[assembly: MelonGame("Kinetic Games", "Phasmophobia")]

namespace Thanatophobia;

// MelonLoader applies [HarmonyPatch] classes itself; calling PatchAll() here would double them.
public class Mod : MelonMod {
    public override void OnInitializeMelon() {
        AntiTamper.Apply(); // before the first scene can run a check
        Settings.Load();
        PhotonNetwork.SendRate = System.Math.Max(1, Settings.SendRate.Value); // 0 divides by zero inside Photon
        PhotonNetwork.SerializationRate = System.Math.Max(1, Settings.SerializationRate.Value);
        LoggerInstance.Msg("Thanatophobia active.");
        if (string.IsNullOrWhiteSpace(Settings.AppId.Value))
            LoggerInstance.Warning("No Photon App ID set. Multiplayer is disabled until you paste one via the main-menu button or UserData\\MelonPreferences.cfg.");
    }

    public override void OnLateInitializeMelon() {
        TalkingIndicator.Create();
    }

    public override void OnUpdate() {
        // Reassert each frame so our name wins over whatever Steam name the game sets.
        var nickname = Settings.Nickname.Value;
        if (!string.IsNullOrWhiteSpace(nickname) && PhotonNetwork.NickName != nickname)
            PhotonNetwork.NickName = nickname;

        TalkingIndicator.Update();
        InventoryHotkeys.Update();
        Lobby.Update();
        VoiceRecorders.Update();
        VoiceAudio.Update();
        SettingsMenu.Update();
        PhotonReconnect.Update();
    }
}
