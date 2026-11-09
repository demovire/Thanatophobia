using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using Il2CppPhoton.Pun;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;
using PhotonPlayer = Il2CppPhoton.Realtime.Player;

namespace Thanatophobia;

// shortcut: extra sliders are PC only and reset on restart; add the VR menu and saving if needed.
[HarmonyPatch(typeof(PauseMenuController), "Start")]
internal static class ExtraVolumeSlidersPatch {
    private const int NativeSliders = 3;
    private const float RowHeight = 100f;
    private static readonly Vector3 ListTop = new(0f, 100f, 0f);

    // Keyed by actor number: nicknames can be null or shared between players.
    public static readonly Dictionary<int, float> Volumes = new();

    private static void Postfix(PauseMenuController __instance) {
        var others = PhotonNetwork.PlayerListOthers;
        if (others.Length <= NativeSliders) return;

        var template = __instance.player4Object.transform;
        var list = new GameObject("PlayerVolumes").AddComponent<RectTransform>();
        list.gameObject.layer = template.gameObject.layer;
        list.SetParent(template.parent, false);
        list.sizeDelta = Vector2.zero;

        __instance.player2Object.transform.SetParent(list, false);
        __instance.player3Object.transform.SetParent(list, false);
        template.SetParent(list, false);

        for (var i = NativeSliders; i < others.Length; i++) CreateRow(template, list, others[i], i);

        var scale = (float)NativeSliders / others.Length;
        list.localScale = Vector3.one * scale;
        list.localPosition = ListTop * (1f - scale);
    }

    private static void CreateRow(Transform template, Transform list, PhotonPlayer player, int index) {
        var row = Object.Instantiate(template.gameObject, list).transform;
        row.localPosition = template.localPosition + Vector3.down * (RowHeight * (index - (NativeSliders - 1)));
        row.gameObject.SetActive(true);

        // The name comes from another player, so block rich-text tags and cap the length.
        var displayName = player.NickName ?? "";
        if (displayName.Length > 32) displayName = displayName.Substring(0, 32);
        var nameText = row.Find("Text").GetComponent<Text>();
        nameText.supportRichText = false;
        nameText.text = displayName;

        var valueText = row.Find("SliderValue").GetComponent<Text>();
        var slider = row.Find("Slider").GetComponent<Slider>();
        var actor = player.ActorNumber;
        var volume = Volumes.TryGetValue(actor, out var saved) ? saved : 1f;

        // The copied event would still drive the template's player.
        slider.onValueChanged = new Slider.SliderEvent();
        slider.value = volume;
        valueText.text = Percent(volume);
        slider.onValueChanged.AddListener((UnityAction<float>)new Action<float>(value => {
            Volumes[actor] = value;
            valueText.text = Percent(value);
            Apply(actor, value);
        }));
    }

    private static void Apply(int actor, float value) {
        foreach (var voice in Object.FindObjectsOfType<VoiceVolume>()) {
            if (voice.view != null && voice.view.Owner != null && voice.view.Owner.ActorNumber == actor)
                voice.ApplyVoiceVolume(value);
        }
    }

    private static string Percent(float value) => $"{value * 100f:0}%";
}

[HarmonyPatch]
internal static class ExtraVolumeLookupPatch {
    private static IEnumerable<MethodBase> TargetMethods() {
        yield return AccessTools.Method(typeof(PauseMenuController), nameof(PauseMenuController.GetPlayerVolume));
        yield return AccessTools.Method(typeof(VoiceVolume), nameof(VoiceVolume.GetPlayerVolume));
    }

    private static void Postfix(PhotonPlayer __0, ref float __result) {
        if (__0 != null && ExtraVolumeSlidersPatch.Volumes.TryGetValue(__0.ActorNumber, out var volume)) __result = volume;
    }
}
