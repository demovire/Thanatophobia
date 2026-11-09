using System;
using HarmonyLib;
using Il2Cpp;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Thanatophobia;

// One menu button (a clone of the game's own, placed beside Quit) opens the Thanatophobia settings overlay.
[HarmonyPatch(typeof(MainManager), "Awake")]
internal static class ServerButtonsPatch {
    private static void Postfix(MainManager __instance) {
        var quit = __instance.transform.Find("QuitButton");
        if (quit == null) return;

        var rect = Object.Instantiate(quit.gameObject, quit.parent).GetComponent<RectTransform>();
        rect.anchoredPosition = new Vector2(340f, -250f); // beside Quit, in the clear area
        rect.sizeDelta = new Vector2(300f, 90f);

        var text = rect.GetComponentInChildren<Text>();
        var loc = text.GetComponent<TextLocaliserUI>();
        if (loc != null) Object.DestroyImmediate(loc);
        text.resizeTextForBestFit = true; // "THANATOPHOBIA" is longer than the game's button labels
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = text.fontSize;
        text.text = "THANATOPHOBIA";

        var button = rect.GetComponent<Button>();
        button.onClick = new Button.ButtonClickedEvent(); // the copied event would still quit the game
        button.onClick.AddListener((UnityAction)new Action(SettingsMenu.Toggle));
    }
}
