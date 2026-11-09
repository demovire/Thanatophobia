using System.Collections.Generic;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using Il2CppPhoton.Pun;
using Il2CppPhoton.Voice.PUN;
using Il2CppPhoton.Voice.Unity;
using UnityEngine;
using UnityEngine.UI;

namespace Thanatophobia;

internal static class TalkingIndicator {
    private const float NamesRefreshInterval = 0.1f;
    private const float FadeSpeed = 9f;
    private const float HiddenAlpha = 0.005f;
    private const float MinBarScale = 0.14f;
    private const float BarWidth = 5f;
    private const float BarGap = 6f;
    private const float BarHeight = 26f;
    private const int MaxNameLength = 32;

    private static readonly float[] BarSpeeds = { 3.8f, 5.2f, 4.5f, 6.0f, 4.1f };
    private static readonly float[] BarShape = { 0.55f, 0.8f, 1f, 0.8f, 0.55f };
    private static readonly Vector2 MiddleLeft = new(0f, 0.5f);

    private static readonly Color PanelBg = new(0.035f, 0.035f, 0.045f, 0.72f);
    private static readonly Color Accent = new(0.80f, 0.78f, 0.73f); // muted bone, matches the game's hand-drawn UI
    private static readonly Color NameColor = new(0.88f, 0.86f, 0.81f);

    private static CanvasGroup _panel;
    private static RectTransform[] _bars;
    private static Image _dot;
    private static float _alpha;
    private static float _level;
    private static Text _names;
    private static CanvasGroup _namesPanel;
    private static RectTransform _namesRect;
    private static float _nextNamesRefresh;
    private static readonly List<Text> _texts = new();
    private static bool _fontReady;

    public static void Create() {
        var root = new GameObject("TalkingIndicator");
        Object.DontDestroyOnLoad(root);

        var canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = short.MaxValue;

        var scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        var bottomCenter = new Vector2(0.5f, 0.11f);
        var panel = CreateRect("Panel", root.transform, bottomCenter, new Vector2(0.5f, 0.5f), new Vector2(236, 46), Vector2.zero);
        AddImage(panel, PanelBg, 23);
        AddOutline(panel);
        _panel = panel.gameObject.AddComponent<CanvasGroup>();
        _panel.alpha = 0f;
        _panel.blocksRaycasts = false;

        var dot = CreateRect("LiveDot", panel, MiddleLeft, MiddleLeft, new Vector2(11, 11), new Vector2(18, 0));
        _dot = AddCircle(dot, Accent);

        var barContainer = CreateRect("Bars", panel, MiddleLeft, MiddleLeft, new Vector2(60, BarHeight), new Vector2(44, 0));
        _bars = new RectTransform[BarSpeeds.Length];
        for (var i = 0; i < _bars.Length; i++) {
            var position = new Vector2(i * (BarWidth + BarGap), 0);
            _bars[i] = CreateRect($"Bar{i}", barContainer, MiddleLeft, new Vector2(0.5f, 0.5f), new Vector2(BarWidth, BarHeight), position);
            AddImage(_bars[i], Accent, 2);
        }

        CreateLabel(panel);

        var topRight = Vector2.one;
        var listRect = CreateRect("TalkingList", root.transform, topRight, topRight, new Vector2(320, 0), new Vector2(-24, -24));
        _namesRect = listRect;
        AddImage(listRect, PanelBg, 14);
        AddOutline(listRect);
        _namesPanel = listRect.gameObject.AddComponent<CanvasGroup>();
        _namesPanel.alpha = 0f;
        _namesPanel.blocksRaycasts = false;

        var header = Child(listRect, "Header", new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -34f), new Vector2(-16f, -10f));
        AddText(header, 14, TextAnchor.UpperRight, Accent).text = "NEARBY VOICES";

        var rule = Child(listRect, "Rule", new Vector2(0f, 1f), Vector2.one, new Vector2(16f, -40f), new Vector2(-16f, -38f));
        AddImage(rule, new Color(Accent.r, Accent.g, Accent.b, 0.35f), 1);

        var textRect = Child(listRect, "Names", Vector2.zero, Vector2.one, new Vector2(16f, 12f), new Vector2(-16f, -46f));
        _names = AddText(textRect, 18, TextAnchor.UpperRight, NameColor);
    }

    private static RectTransform Child(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax) {
        var rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
        return rect;
    }

    public static void Update() {
        if (_panel == null) return;

        if (Time.unscaledTime >= _nextNamesRefresh) {
            _nextNamesRefresh = Time.unscaledTime + NamesRefreshInterval;
            RefreshNames();
        }

        // Our init-time font may be a stripped one; adopt the game's own font once a scene with UI is loaded.
        if (!_fontReady) {
            var font = FindGameFont();
            if (font != null) {
                foreach (var text in _texts) text.font = font;
                _fontReady = true;
            }
        }

        var recorder = VoiceRecorders.Active;
        var talking = recorder != null && recorder.IsCurrentlyTransmitting;

        _alpha = Mathf.Lerp(_alpha, talking ? 1f : 0f, Time.deltaTime * FadeSpeed);
        _panel.alpha = _alpha < HiddenAlpha ? 0f : _alpha;
        if (_panel.alpha == 0f) return;

        var pulse = 0.45f + 0.35f * Mathf.Sin(Time.time * 3f);
        _dot.color = new Color(Accent.r, Accent.g, Accent.b, talking ? pulse : 0.25f);

        // Drive the bars from the real mic level (peak amplitude, 0..1).
        var raw = MicLevel(recorder);
        _level = Mathf.Lerp(_level, raw, Time.deltaTime * (raw > _level ? 24f : 9f)); // fast attack, slow release

        for (var i = 0; i < _bars.Length; i++) {
            var wiggle = 1f + 0.18f * _level * Mathf.Sin(i * 0.9f + Time.time * BarSpeeds[i]);
            var scale = Mathf.Clamp(MinBarScale + (1f - MinBarScale) * _level * BarShape[i] * wiggle, MinBarScale, 1f);
            _bars[i].sizeDelta = new Vector2(BarWidth, BarHeight * scale);
        }
    }

    private static float MicLevel(Recorder recorder) {
        var meter = recorder?.LevelMeter;
        return meter == null ? 0f : Mathf.Clamp01(meter.CurrentPeakAmp * Settings.MicGain.Value);
    }

    private static void RefreshNames() {
        var talkers = new List<string>(AudibleTalkers());
        _names.text = string.Join("\n", talkers);

        var show = talkers.Count > 0;
        _namesPanel.alpha = show ? 1f : 0f;
        if (show)
            _namesRect.sizeDelta = new Vector2(_namesRect.sizeDelta.x, _names.preferredHeight + 62f); // header + rule + padding
    }

    private static IEnumerable<string> AudibleTalkers() {
        var voice = PhotonVoiceNetwork.Instance;
        var listener = Object.FindObjectOfType<AudioListener>();
        var room = PhotonNetwork.CurrentRoom;
        if (voice == null || listener == null || room == null || voice.linkedSpeakers == null) yield break;

        foreach (var speaker in voice.linkedSpeakers) {
            if (speaker == null || !speaker.IsLinked || !CanHear(speaker, listener) || !IsSpeaking(speaker)) continue;
            var player = room.GetPlayer(speaker.remoteVoiceLink.PlayerId);
            if (player != null) yield return Clamp(player.NickName);
        }
    }

    private const float SpeakingLevel = 0.008f;
    private static readonly Il2CppStructArray<float> _outBuf = new(256);

    // True only while the speaker is actually outputting sound now (like a Discord overlay), not merely linked.
    private static bool IsSpeaking(Speaker speaker) {
        if (!speaker.IsPlaying) return false;
        var src = speaker.GetComponent<AudioSource>();
        if (src == null) return false;
        src.GetOutputData(_outBuf, 0);
        var sum = 0f;
        for (var i = 0; i < _outBuf.Length; i++) sum += _outBuf[i] * _outBuf[i];
        return Mathf.Sqrt(sum / _outBuf.Length) > SpeakingLevel;
    }

    // Names come from other players, so cap the length and never let them render as rich text (see AddText).
    private static string Clamp(string name) =>
        string.IsNullOrEmpty(name) || name.Length <= MaxNameLength ? name : name.Substring(0, MaxNameLength);

    private static bool CanHear(Speaker speaker, AudioListener listener) {
        var source = speaker.GetComponent<AudioSource>();
        if (source == null || !source.isActiveAndEnabled || source.mute || source.volume <= 0f) return false;

        // The game makes voice non-positional while someone is on the radio.
        if (source.spatialBlend <= 0f) return true;
        return Vector3.Distance(speaker.transform.position, listener.transform.position) <= Settings.TalkingListRange.Value;
    }

    private static RectTransform CreateRect(string name, Transform parent, Vector2 anchor, Vector2 pivot, Vector2 size, Vector2 position) {
        var rect = new GameObject(name).AddComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = anchor;
        rect.pivot = pivot;
        rect.sizeDelta = size;
        rect.anchoredPosition = position;
        return rect;
    }

    private static Material _uiMaterial;
    private static readonly Dictionary<int, Sprite> _sprites = new();

    // Runtime-created uGUI graphics get no material in IL2CPP and render invisibly; assign the UI shader.
    private static Material UiMaterial() {
        if (_uiMaterial != null) return _uiMaterial;
        var shader = Shader.Find("UI/Default");
        if (shader != null) _uiMaterial = new Material(shader);
        else
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
                if (img.material != null && img.material.shader != null) { _uiMaterial = img.material; break; }
        return _uiMaterial;
    }

    // Procedural rounded-rect sprite, 9-sliced so corners stay crisp at any size.
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

    private static void AddImage(RectTransform rect, Color color, int radius) {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.material = UiMaterial();
        image.sprite = RoundedSprite(radius);
        image.type = Image.Type.Sliced;
    }

    private static Image AddCircle(RectTransform rect, Color color) {
        var image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        image.material = UiMaterial();
        image.sprite = RoundedSprite((int)(rect.sizeDelta.x / 2f) - 1);
        return image;
    }

    private static void AddOutline(RectTransform rect) {
        var outline = rect.gameObject.AddComponent<Outline>();
        outline.effectColor = new Color(0f, 0f, 0f, 0.5f); // soft dark rim for depth, no glow
        outline.effectDistance = new Vector2(1f, -1f);
    }

    private static void CreateLabel(RectTransform panel) {
        var rect = new GameObject("Label").AddComponent<RectTransform>();
        rect.SetParent(panel, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(104, 4);
        rect.offsetMax = new Vector2(-16, -4);

        AddText(rect, 16, TextAnchor.MiddleLeft, Accent).text = "SPEAKING";
    }

    private static Text AddText(RectTransform rect, int fontSize, TextAnchor alignment, Color color) {
        var text = rect.gameObject.AddComponent<Text>();
        text.font = FindFont();
        text.supportRichText = false;
        text.fontSize = fontSize;
        text.fontStyle = FontStyle.Bold;
        text.alignment = alignment;
        text.color = color;
        text.raycastTarget = false;
        rect.gameObject.AddComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.6f); // readable over bright scenes
        _texts.Add(text);
        return text;
    }

    private static Font FindFont() {
        var fonts = Resources.FindObjectsOfTypeAll<Font>();
        return fonts.Length > 0 ? fonts[0] : Font.GetDefault();
    }

    // A font the game actually renders with (ours aren't added to the scene until later).
    private static Font FindGameFont() {
        foreach (var text in Resources.FindObjectsOfTypeAll<Text>())
            if (text.font != null && !_texts.Contains(text)) return text.font;
        return null;
    }
}
