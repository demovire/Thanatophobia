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

        transport.AppId = string.IsNullOrEmpty(Settings.VoiceAppId.Value) ? Settings.AppId.Value : Settings.VoiceAppId.Value;
        transport.AppVersion = PhotonNetwork.GameVersion;
        transport.AuthValues = null; // no user id: the server hands each connection its own

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
            // Hidden, or voice rooms show up in the server browser and anyone can find them.
            __instance.OpJoinOrCreateRoom(new EnterRoomParams { RoomName = roomName, RoomOptions = new RoomOptions { IsVisible = false } });
        }
    }
}

internal static class VoiceRecorders {
    private static Recorder _active;
    private static float _nextFind;
    private static bool _inited;

    // Our own recorder. It lives on our avatar, which is replaced on every scene load, so look it up again when it dies.
    public static Recorder Active {
        get {
            if (_active == null && Time.unscaledTime >= _nextFind) {
                _nextFind = Time.unscaledTime + 1f;
                foreach (var rec in UnityEngine.Object.FindObjectsOfType<Recorder>())
                    if (IsOurs(rec)) { _active = rec; break; }
            }
            return _active;
        }
    }

    // Other players' avatars carry a Recorder too; starting those opens our mic once per avatar.
    private static bool IsOurs(Recorder rec) {
        var view = rec.GetComponentInParent<PhotonView>();
        return view == null || view.IsMine;
    }

    private static bool _wasOpen;

    // Open mic: the game's push-to-talk switches transmission off on key release, so hold it on and let
    // Photon's voice detection keep silence off the wire.
    public static void Update() {
        var rec = Active;
        if (rec == null) return;
        var open = Settings.OpenMic.Value;
        if (rec.VoiceDetection != open) rec.VoiceDetection = open;
        if (open != rec.TransmitEnabled && (open || _wasOpen)) rec.TransmitEnabled = open;
        _wasOpen = open;
    }

    // Start our recorders once we're in the voice room, reset on disconnect. Mirrors the old working mod.
    public static void OnVoiceState(PhotonVoiceNetwork pvn, ClientState toState) {
        if (toState == ClientState.Joined && !_inited) {
            foreach (var rec in UnityEngine.Object.FindObjectsOfType<Recorder>()) {
                if (!IsOurs(rec)) continue;
                rec.AutoStart = false;
                rec.Init(pvn);
                rec.StartRecording(); // capture runs; the game's push-to-talk gates transmission
                _active = rec;
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
            var pvn = PhotonVoiceNetwork.Instance;
            var rec = VoiceRecorders.Active ?? pvn.PrimaryRecorder;
            // Full mic state: which of these is wrong for a silent host tells us where the chain breaks.
            MelonLogger.Msg(rec == null
                ? $"[VoiceDiag] voice={pvn.ClientState} mic=none speakers={speakers.Count}"
                : $"[VoiceDiag] voice={pvn.ClientState} mic init={rec.IsInitialized} recording={rec.IsRecording} enabled={rec.isActiveAndEnabled} txEnabled={rec.TransmitEnabled} transmit={rec.IsCurrentlyTransmitting} restart={rec.RequiresRestart} viewId={rec.UserData?.ToString() ?? "null"} speakers={speakers.Count}");
        }

        foreach (var sp in speakers) {
            if (sp == null) continue;

            var src = sp.GetComponent<AudioSource>();
            if (src != null && src.outputAudioMixerGroup != null) src.outputAudioMixerGroup = null;

            if (diag)
                MelonLogger.Msg($"[VoiceDiag] {VoiceActivity.Name(sp)} playing={sp.IsPlaying} vol={(src != null ? src.volume : -1f):0.00} mute={(src != null && src.mute)} lag={sp.Lag}ms delay={sp.PlayDelayMs}ms idle={VoiceActivity.FrameAgeText(sp)}");
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
