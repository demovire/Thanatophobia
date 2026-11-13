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
    // Frames arrive on Photon's decode thread, so lock the map and use a clock that is safe off the main thread.
    private static readonly Dictionary<IntPtr, long> _lastFrame = new();

    public static void Mark(Speaker sp) {
        lock (_lastFrame) _lastFrame[sp.Pointer] = Environment.TickCount64;
    }

    public static float FrameAge(Speaker sp) {
        lock (_lastFrame)
            return _lastFrame.TryGetValue(sp.Pointer, out var t) ? (Environment.TickCount64 - t) / 1000f : float.MaxValue;
    }

    public static string FrameAgeText(Speaker sp) {
        var age = FrameAge(sp);
        return age > 1e6f ? "never" : $"{age:0.00}s";
    }

    public static void Forget(Speaker sp) {
        lock (_lastFrame) _lastFrame.Remove(sp.Pointer);
    }

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
