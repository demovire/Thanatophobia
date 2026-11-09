using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2CppPhoton.Voice.Unity;
using MelonLoader;
using UnityEngine;

namespace Thanatophobia;

// Tracks when each remote Speaker last received a voice frame. Drives the HUD's "actively speaking" test and
// lets VoiceAudio spot a stalled stream that keeps replaying buffered audio (the voice loop-back bug).
internal static class VoiceActivity {
    private static readonly Dictionary<IntPtr, float> _lastFrame = new();

    public static void Mark(Speaker sp) => _lastFrame[sp.Pointer] = Time.unscaledTime;

    public static float FrameAge(Speaker sp) =>
        _lastFrame.TryGetValue(sp.Pointer, out var t) ? Time.unscaledTime - t : float.MaxValue;

    public static string FrameAgeText(Speaker sp) {
        var age = FrameAge(sp);
        return age > 1e6f ? "never" : $"{age:0.00}s";
    }

    public static void Forget(Speaker sp) => _lastFrame.Remove(sp.Pointer);

    public static string Name(Speaker sp) {
        var nick = sp.Actor?.NickName;
        if (!string.IsNullOrEmpty(nick)) return nick;
        var link = sp.remoteVoiceLink;
        return link != null ? $"player {link.PlayerId}" : "?";
    }
}

[HarmonyPatch(typeof(Speaker), nameof(Speaker.OnAudioFrame))]
internal static class SpeakerFramePatch {
    private static void Postfix(Speaker __instance) {
        if (Settings.VoiceDiagnostics.Value && VoiceActivity.FrameAge(__instance) > 0.5f)
            MelonLogger.Msg($"[VoiceDiag] {VoiceActivity.Name(__instance)} stream active");
        VoiceActivity.Mark(__instance);
    }
}

[HarmonyPatch(typeof(Speaker), nameof(Speaker.OnRemoteVoiceInfo))]
internal static class SpeakerInfoPatch {
    private static void Postfix(Speaker __instance) {
        if (Settings.VoiceDiagnostics.Value)
            MelonLogger.Msg($"[VoiceDiag] {VoiceActivity.Name(__instance)} stream linked, playDelay={__instance.PlayDelayMs}ms");
    }
}

[HarmonyPatch(typeof(Speaker), nameof(Speaker.OnRemoteVoiceRemove))]
internal static class SpeakerRemovePatch {
    private static void Postfix(Speaker __instance) {
        if (Settings.VoiceDiagnostics.Value)
            MelonLogger.Msg($"[VoiceDiag] {VoiceActivity.Name(__instance)} stream removed");
        VoiceActivity.Forget(__instance);
    }
}
