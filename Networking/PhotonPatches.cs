using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2CppPhoton.Pun;
using Il2CppPhoton.Realtime;
using Il2CppPhoton.Voice;
using UnityEngine;

namespace Thanatophobia;

internal static class PhotonSettings {
    public static void Apply(AppSettings settings) {
        if (settings == null) return;
        settings.AppIdRealtime = Settings.AppId.Value;
        settings.AppIdVoice = string.IsNullOrEmpty(Settings.VoiceAppId.Value) ? Settings.AppId.Value : Settings.VoiceAppId.Value;
        settings.AuthMode = AuthModeOption.AuthOnceWss;
        settings.Server = "";
        settings.FixedRegion = Settings.Region.Value;
        settings.UseNameServer = true;
    }
}

[HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.PhotonServerSettings), MethodType.Getter)]
internal static class ServerSettingsPatch {
    private static void Postfix(ServerSettings __result) => PhotonSettings.Apply(__result?.AppSettings);
}

[HarmonyPatch]
internal static class ClearAuthPatch {
    private static IEnumerable<MethodBase> TargetMethods() {
        yield return AccessTools.Method(typeof(LoadBalancingClient), nameof(LoadBalancingClient.ConnectUsingSettings));
        yield return AccessTools.Method(typeof(LoadBalancingClient), nameof(LoadBalancingClient.ConnectToRegionMaster));
        yield return AccessTools.Method(typeof(LoadBalancingClient), nameof(LoadBalancingClient.ConnectToNameServer));
    }

    private static void Prefix(LoadBalancingClient __instance) => __instance.AuthValues = null;
}

[HarmonyPatch(typeof(PhotonNetwork), nameof(PhotonNetwork.NickName), MethodType.Getter)]
internal static class NickNamePatch {
    private static void Postfix(ref string __result) {
        if (string.IsNullOrEmpty(__result)) __result = "Player";
    }
}
