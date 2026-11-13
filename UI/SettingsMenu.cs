using System;
using System.Collections.Generic;
using MelonLoader;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace Thanatophobia;

// A self-contained screen-space settings overlay (own canvas + raycaster), so it never fights the
// whiteboard menu's layout or z-order. Text boxes are real InputFields you can type or paste into.
internal static class SettingsMenu {
    private static readonly string[] Regions = {
        "asia", "au", "cae", "eu", "hk", "in", "jp", "za", "sa", "kr", "tr", "uae", "us", "ussc", "usw",
    };

    private static readonly Color PanelBg = new(0.07f, 0.07f, 0.085f, 0.98f);
    private static readonly Color FieldBg = new(0.14f, 0.14f, 0.17f, 1f);
    private static readonly Color ButtonBg = new(0.2f, 0.21f, 0.26f, 1f);
    private static readonly Color Bone = new(0.86f, 0.83f, 0.77f);
    private static readonly Color Dim = new(0.6f, 0.58f, 0.54f);

    private static GameObject _root;
    private static CanvasGroup _group;
    private static bool _open;
    private static Text _regionLabel;
    private static Text _voiceLabel;
    private static Font _font;
    private static string _appIdAtOpen;
    private static string _regionAtOpen;

    public static void Toggle() {
        if (_root == null) Build();
        _open = !_open;
        if (_open) {
            _appIdAtOpen = Settings.AppId.Value;
            _regionAtOpen = Settings.Region.Value;
        } else {
            MelonPreferences.Save();
            if (Settings.AppId.Value != _appIdAtOpen || Settings.Region.Value != _regionAtOpen)
                PhotonReconnect.Apply(); // apply App ID / region live instead of needing a restart
        }
        _group.alpha = _open ? 1f : 0f;
        _group.blocksRaycasts = _open;
        _group.interactable = _open;
        ApplyCursor();
    }

    // The menu's free-look re-grabs the cursor every frame, so hold it free while the overlay is open.
    public static void Update() {
        if (_open) ApplyCursor();
    }

    private static void ApplyCursor() {
        if (!_open) return;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private static void Build() {
        _font = FindFont();

        _root = new GameObject("ThanatophobiaSettings");
        Object.DontDestroyOnLoad(_root);
        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000;
        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        _root.AddComponent<GraphicRaycaster>();
        _group = _root.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = false;
        _group.interactable = false;

        var dim = Stretch("Dim", _root.transform, Vector2.zero, Vector2.zero);
        AddImage(dim, new Color(0f, 0f, 0f, 0.75f), 0);

        var panel = Centered("Panel", _root.transform, new Vector2(760, 560), Vector2.zero);
        AddImage(panel, PanelBg, 18);

        AddText(Centered("Title", panel, new Vector2(700, 50), new Vector2(0, 230)), "THANATOPHOBIA", 34, TextAnchor.MiddleCenter, Bone);

        AddLabel(panel, "PHOTON APP ID", new Vector2(0, 150));
        AddInput(panel, new Vector2(0, 110), Settings.AppId.Value, "Paste your Photon App ID", v => Settings.AppId.Value = v.Trim());

        AddLabel(panel, "NICKNAME", new Vector2(0, 30));
        AddInput(panel, new Vector2(0, -10), Settings.Nickname.Value, "Blank = your Steam name", v => Settings.Nickname.Value = v.Trim());

        AddLabel(panel, "REGION / VOICE", new Vector2(0, -90));
        _regionLabel = AddButton(panel, new Vector2(-170, -130), new Vector2(320, 56), RegionLabel(), NextRegion);
        _voiceLabel = AddButton(panel, new Vector2(170, -130), new Vector2(320, 56), VoiceLabel(), ToggleOpenMic);

        AddButton(panel, new Vector2(0, -210), new Vector2(280, 60), "SAVE & CLOSE", Toggle);
        AddText(Centered("Note", panel, new Vector2(700, 24), new Vector2(0, -256)), "Changes apply on close (App ID / region reconnect briefly).", 15, TextAnchor.MiddleCenter, Dim);
    }

    private static string VoiceLabel() => Settings.OpenMic.Value ? "VOICE: OPEN MIC" : "VOICE: PUSH TO TALK";

    private static void ToggleOpenMic() {
        Settings.OpenMic.Value = !Settings.OpenMic.Value;
        _voiceLabel.text = VoiceLabel();
    }

    private static string RegionLabel() => $"REGION: {Settings.Region.Value.ToUpperInvariant()}";

    private static void NextRegion() {
        var next = (Array.IndexOf(Regions, Settings.Region.Value) + 1) % Regions.Length;
        Settings.Region.Value = Regions[next];
        _regionLabel.text = RegionLabel();
    }

    private static void AddLabel(Transform parent, string label, Vector2 pos) =>
        AddText(Centered("Label", parent, new Vector2(660, 26), pos), label, 18, TextAnchor.LowerLeft, Dim);

    private static void AddInput(Transform parent, Vector2 pos, string value, string placeholder, Action<string> onChange) {
        var rect = Centered("Input", parent, new Vector2(660, 56), pos);
        AddImage(rect, FieldBg, 8);

        var input = rect.gameObject.AddComponent<InputField>();
        var text = AddText(Child(rect, "Text", Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-16, -6)), "", 22, TextAnchor.MiddleLeft, new Color(0.93f, 0.93f, 0.93f));
        var ph = AddText(Child(rect, "Placeholder", Vector2.zero, Vector2.one, new Vector2(16, 6), new Vector2(-16, -6)), placeholder, 22, TextAnchor.MiddleLeft, new Color(0.5f, 0.5f, 0.5f));

        input.textComponent = text;
        input.placeholder = ph;
        input.text = value ?? "";
        input.onValueChanged.AddListener((UnityAction<string>)onChange);
    }

    private static Text AddButton(Transform parent, Vector2 pos, Vector2 size, string label, Action onClick) {
        var rect = Centered("Button", parent, size, pos);
        AddImage(rect, ButtonBg, 8);
        var button = rect.gameObject.AddComponent<Button>();
        var text = AddText(Child(rect, "Text", Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero), label, 20, TextAnchor.MiddleCenter, Bone);
        text.resizeTextForBestFit = true;
        text.resizeTextMinSize = 8;
        text.resizeTextMaxSize = 20;
        button.onClick.AddListener((UnityAction)onClick);
        return text;
    }

    private static RectTransform Centered(string name, Transform parent, Vector2 size, Vector2 pos) {
        var rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        return rect;
    }

    private static RectTransform Stretch(string name, Transform parent, Vector2 offsetMin, Vector2 offsetMax) =>
        Child(parent, name, Vector2.zero, Vector2.one, offsetMin, offsetMax);

    private static RectTransform Child(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
        var rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    private static Text AddText(RectTransform rect, string value, int fontSize, TextAnchor alignment, Color color) {
        var text = rect.gameObject.AddComponent<Text>();
        text.font = _font;
        text.supportRichText = false;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.text = value;
        return text;
    }

    private static Material _uiMaterial;
    private static readonly Dictionary<int, Sprite> _sprites = new();

    private static void AddImage(RectTransform rect, Color color, int radius) {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.material = UiMaterial();
        if (radius <= 0) return;
        image.sprite = RoundedSprite(radius);
        image.type = Image.Type.Sliced;
    }

    private static Material UiMaterial() {
        if (_uiMaterial != null) return _uiMaterial;
        var shader = Shader.Find("UI/Default");
        if (shader != null) _uiMaterial = new Material(shader);
        else
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
                if (img.material != null && img.material.shader != null) { _uiMaterial = img.material; break; }
        return _uiMaterial;
    }

    private static Sprite RoundedSprite(int radius) {
        if (_sprites.TryGetValue(radius, out var cached)) return cached;
        var size = radius * 2 + 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        var half = size / 2f;
        var inner = half - radius;
        for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++) {
                var qx = Mathf.Abs(x + 0.5f - half) - inner;
                var qy = Mathf.Abs(y + 0.5f - half) - inner;
                var d = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f) - radius;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - d)));
            }
        tex.Apply();
        var border = new Vector4(radius, radius, radius, radius);
        var sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0u, SpriteMeshType.FullRect, border);
        _sprites[radius] = sprite;
        return sprite;
    }

    private static Font FindFont() {
        foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
            if (text.font != null) return text.font;
        return Font.GetDefault();
    }
}
