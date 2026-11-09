using System;
using HarmonyLib;
using Il2CppPhoton.Pun;
using Il2CppPhoton.Realtime;
using Il2CppPhoton.Voice;
using Il2CppPhoton.Voice.PUN;
using Il2CppPhoton.Voice.Unity;
using MelonLoader;
using UnityEngine;

namespace Thanatophobia;

[HarmonyPatch(typeof(PhotonVoiceNetwork), "Awake")]
internal static class VoiceAwakePatch {
    private static void Postfix(PhotonVoiceNetwork __instance) {
        __instance.Settings ??= new AppSettings();
        PhotonSettings.Apply(__instance.Settings);
    }
}

[HarmonyPatch(typeof(PhotonVoiceNetwork), "OnEnable")]
internal static class VoiceEnablePatch {
    private static void Prefix(PhotonVoiceNetwork __instance) {
        __instance.usePunAppSettings = true;
        __instance.UsePunAuthValues = true;
        __instance.WorkInOfflineMode = false;
    }
}

// The native connect path returns false on this build, so connect the transport directly.
[HarmonyPatch(typeof(VoiceConnection), nameof(VoiceConnection.ConnectUsingSettings))]
internal static class VoiceConnectPatch {
    // Unique per process: two clients from one install would otherwise share a voice identity and Photon won't route voice between them.
    private static readonly string VoiceUserId = Guid.NewGuid().ToString();

    private static bool Prefix(VoiceConnection __instance, AppSettings overwriteSettings, ref bool __result) {
        __result = false;
        __instance.Settings ??= new AppSettings();
        PhotonSettings.Apply(__instance.Settings);
        PhotonSettings.Apply(overwriteSettings);

        var transport = __instance.Client;
        if (transport == null) {
            MelonLogger.Error("[Voice] Cannot connect: transport is null.");
            return false;
        }

        transport.AppId = Settings.AppId.Value;
        transport.AppVersion = PhotonNetwork.GameVersion;
        transport.AuthValues = null;
        transport.UserId = VoiceUserId;

        try {
            transport.ConnectToNameServer();
            __result = transport.State != ClientState.PeerCreated;
        } catch (Exception ex) {
            MelonLogger.Error($"[Voice] Connect failed: {ex.Message}");
        }
        return false;
    }
}

// The voice transport never advances past the name server by itself, so drive it to the room.
[HarmonyPatch(typeof(LoadBalancingTransport), "onStateChangeVoiceClient")]
internal static class VoiceTransportPatch {
    private static void Postfix(LoadBalancingTransport __instance, ClientState fromState, ClientState state) {
        MelonLogger.Msg($"[Voice] {fromState} -> {state}");

        if (state == ClientState.ConnectedToNameServer) {
            __instance.ConnectToRegionMaster(Settings.Region.Value);
        } else if (state == ClientState.ConnectedToMasterServer) {
            var roomName = PhotonVoiceNetwork.GetVoiceRoomName();
            if (string.IsNullOrEmpty(roomName)) {
                MelonLogger.Warning("[Voice] Voice room name is empty, cannot join.");
                return;
            }
            __instance.OpJoinOrCreateRoom(new EnterRoomParams { RoomName = roomName, RoomOptions = new RoomOptions() });
        }
    }
}

internal static class VoiceRecorders {
    public static Recorder Active { get; private set; }
    private static bool _inited;

    // Start every recorder once we're in the voice room, reset on disconnect. Mirrors the old working mod.
    public static void OnVoiceState(PhotonVoiceNetwork pvn, ClientState toState) {
        if (toState == ClientState.Joined && !_inited) {
            foreach (var rec in UnityEngine.Object.FindObjectsOfType<Recorder>()) {
                rec.AutoStart = false;
                rec.Init(pvn);
                rec.StartRecording(); // capture runs; the game's push-to-talk gates transmission
                Active = rec;
            }
            _inited = true;
            MelonLogger.Msg("[Voice] recorders started");
        } else if (toState is ClientState.Disconnecting or ClientState.Disconnected) {
            _inited = false;
        }
    }
}

// The game routes each voice Speaker through a room SFX bus (e.g. FirstFloorSFX) that its audio-occlusion
// system mutes, so remote players are inaudible. Send the speaker straight to master instead.
internal static class VoiceAudio {
    private static float _nextDiag;

    public static void Update() {
        var speakers = PhotonVoiceNetwork.Instance?.linkedSpeakers;
        if (speakers == null) return;

        var diag = Settings.VoiceDiagnostics.Value && Time.unscaledTime >= _nextDiag;
        if (diag) {
            _nextDiag = Time.unscaledTime + 2f;
            var rec = VoiceRecorders.Active;
            MelonLogger.Msg($"[VoiceDiag] mic transmit={(rec != null && rec.IsCurrentlyTransmitting)} speakers={speakers.Count}");
        }

        foreach (var sp in speakers) {
            if (sp == null) continue;

            var src = sp.GetComponent<AudioSource>();
            if (src != null && src.outputAudioMixerGroup != null) src.outputAudioMixerGroup = null;

            if (diag)
                MelonLogger.Msg($"[VoiceDiag] {VoiceActivity.Name(sp)} playing={sp.IsPlaying} lag={sp.Lag}ms delay={sp.PlayDelayMs}ms idle={VoiceActivity.FrameAgeText(sp)}");
        }
    }
}

[HarmonyPatch(typeof(PhotonVoiceNetwork), nameof(PhotonVoiceNetwork.OnVoiceStateChanged))]
internal static class VoiceStatePatch {
    private static void Postfix(PhotonVoiceNetwork __instance, ClientState toState) =>
        VoiceRecorders.OnVoiceState(__instance, toState);
}

// Native OnEnable crashes this build; recorders are started from the voice-room join instead.
[HarmonyPatch(typeof(Recorder), "OnEnable")]
internal static class RecorderEnablePatch {
    private static bool Prefix() => false;
}

// Some builds ship no SpeakerPrefab, so remote voice has nothing to play through. Create one on demand.
[HarmonyPatch(typeof(VoiceConnection), "SimpleSpeakerFactory")]
internal static class SpeakerFactoryPatch {
    private static void Prefix(VoiceConnection __instance, int __0) {
        if (__instance.SpeakerPrefab == null) {
            var go = new GameObject("VoiceSpeaker_" + __0);
            go.AddComponent<Speaker>();
            __instance.SpeakerPrefab = go;
        }
    }
}
