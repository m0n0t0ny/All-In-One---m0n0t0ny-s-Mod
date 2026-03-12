using System;
using System.Reflection;
using Duckov.UI;
using Duckov.Utilities;
using Duckov.Weathers;
using ItemStatsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace m0n0t0nysMod
{
    enum DisplayMode { Combined, SingleOnly, StackOnly }

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        // ── PlayerPrefs keys ──────────────────────────────────────────────
        private const string PREF_ENABLED        = "DisplayItemValue_Enabled";
        private const string PREF_MODE           = "DisplayItemValue_Mode";
        private const string PREF_SLEEP_ENABLED  = "DisplayItemValue_SleepEnabled";
        private const string PREF_PRESET1H       = "DisplayItemValue_Preset1H";
        private const string PREF_PRESET1M       = "DisplayItemValue_Preset1M";
        private const string PREF_PRESET2H       = "DisplayItemValue_Preset2H";
        private const string PREF_PRESET2M       = "DisplayItemValue_Preset2M";
        private const KeyCode MENU_KEY            = KeyCode.F9;

        // ── Item value display ────────────────────────────────────────────
        private bool _showValue;
        private DisplayMode _mode;
        private TextMeshProUGUI? _valueText;

        // ── Settings panel UI refs ────────────────────────────────────────
        private GameObject? _settingsCanvas;
        private Image? _toggleBtnImage;
        private TextMeshProUGUI? _toggleBtnText;
        private TextMeshProUGUI? _modeBtnText;
        private Image? _sleepToggleImage;
        private TextMeshProUGUI? _sleepToggleText;

        // ── Sleep preset state ────────────────────────────────────────────
        private bool _sleepPresetsEnabled;
        private int  _preset1Hour, _preset1Min;
        private int  _preset2Hour, _preset2Min;
        private SleepView? _sleepViewInstance;
        private bool _sleepPresetsInjected;

        // Live label refs so settings picker updates the in-sleep-view button text
        private TextMeshProUGUI? _preset1BtnLabel;
        private TextMeshProUGUI? _preset2BtnLabel;

        TextMeshProUGUI ValueText
        {
            get
            {
                if (_valueText == null)
                    _valueText = Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
                return _valueText;
            }
        }

        void Awake()
        {
            _showValue           = PlayerPrefs.GetInt(PREF_ENABLED,      1)  == 1;
            _mode                = (DisplayMode)PlayerPrefs.GetInt(PREF_MODE, (int)DisplayMode.Combined);
            _sleepPresetsEnabled = PlayerPrefs.GetInt(PREF_SLEEP_ENABLED, 1)  == 1;
            _preset1Hour         = PlayerPrefs.GetInt(PREF_PRESET1H,  5);
            _preset1Min          = PlayerPrefs.GetInt(PREF_PRESET1M, 30);
            _preset2Hour         = PlayerPrefs.GetInt(PREF_PRESET2H, 21);
            _preset2Min          = PlayerPrefs.GetInt(PREF_PRESET2M, 30);
            Debug.Log($"[m0n0t0nysMod] Loaded. Press {MENU_KEY} to open settings.");
            BuildSettingsPanel();
        }

        void OnDestroy()
        {
            if (_valueText != null) Destroy(_valueText.gameObject);
            if (_settingsCanvas != null) Destroy(_settingsCanvas);
        }

        void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
        }

        void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
        }

        void Update()
        {
            if (Input.GetKeyDown(MENU_KEY))
                _settingsCanvas!.SetActive(!_settingsCanvas.activeSelf);

            if (_sleepPresetsEnabled)
                CheckSleepViewInjection();
        }

        // ── Sleep Presets injection ───────────────────────────────────────

        private static Slider? GetSlider(SleepView sv) =>
            typeof(SleepView)
                .GetField("slider", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.GetValue(sv) as Slider;

        private void CheckSleepViewInjection()
        {
            var sv = SleepView.Instance;
            if (sv == null) return;

            if (sv != _sleepViewInstance)
            {
                _sleepViewInstance    = sv;
                _sleepPresetsInjected = false;
                _preset1BtnLabel      = null;
                _preset2BtnLabel      = null;
            }

            if (!_sleepPresetsInjected && sv.gameObject.activeInHierarchy)
            {
                InjectSleepPresets(sv);
                _sleepPresetsInjected = true;
            }
        }

        private void InjectSleepPresets(SleepView sv)
        {
            var slider = GetSlider(sv);
            if (slider == null)
            {
                Debug.LogWarning("[m0n0t0nysMod] Could not find SleepView.slider via reflection.");
                return;
            }

            // Find the Sleep button by looking for one whose child TMP text contains "Sleep"
            Button? sleepBtn = null;
            foreach (var btn in sv.GetComponentsInChildren<Button>(true))
            {
                var tmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null && tmp.text.IndexOf("Sleep", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    sleepBtn = btn;
                    break;
                }
            }
            if (sleepBtn == null)
            {
                Debug.LogWarning("[m0n0t0nysMod] Could not find Sleep button in SleepView.");
                return;
            }

            var sleepRect       = sleepBtn.GetComponent<RectTransform>();
            var sleepImg        = sleepBtn.GetComponent<Image>();
            var origParent      = sleepRect.parent;
            var origAnchorMin   = sleepRect.anchorMin;
            var origAnchorMax   = sleepRect.anchorMax;
            var origPivot       = sleepRect.pivot;
            var origSizeDelta   = sleepRect.sizeDelta;
            var origAnchoredPos = sleepRect.anchoredPosition;
            int origSiblingIdx  = sleepBtn.transform.GetSiblingIndex();

            // Create wrapper that occupies exactly the same rect as the Sleep button did
            var wrapper = new GameObject("SleepPresetWrapper");
            wrapper.transform.SetParent(origParent, false);
            wrapper.transform.SetSiblingIndex(origSiblingIdx);
            var wrapperRect = wrapper.AddComponent<RectTransform>();
            wrapperRect.anchorMin        = origAnchorMin;
            wrapperRect.anchorMax        = origAnchorMax;
            wrapperRect.pivot            = origPivot;
            wrapperRect.sizeDelta        = origSizeDelta;
            wrapperRect.anchoredPosition = origAnchoredPos;

            var hLayout = wrapper.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing               = 6f;
            hLayout.childAlignment        = TextAnchor.MiddleCenter;
            hLayout.childForceExpandWidth  = false;
            hLayout.childForceExpandHeight = true;
            hLayout.padding               = new RectOffset(0, 0, 0, 0);

            // Reparent the real Sleep button into the wrapper (left slot)
            sleepBtn.transform.SetParent(wrapper.transform, false);
            var sleepLE = sleepBtn.gameObject.GetComponent<LayoutElement>();
            if (sleepLE == null) sleepLE = sleepBtn.gameObject.AddComponent<LayoutElement>();
            sleepLE.preferredWidth = origSizeDelta.x * 0.32f;
            sleepLE.flexibleWidth  = 0f;

            // Preset grid container — takes remaining width
            var presetGrid = new GameObject("PresetGrid");
            presetGrid.transform.SetParent(wrapper.transform, false);
            var gridLE = presetGrid.AddComponent<LayoutElement>();
            gridLE.flexibleWidth = 1f;
            var vLayout = presetGrid.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing               = 4f;
            vLayout.childAlignment        = TextAnchor.MiddleCenter;
            vLayout.childForceExpandWidth  = true;
            vLayout.childForceExpandHeight = true;

            // Row 1: preset1, preset2, Rain
            var row1 = MakePresetRow(presetGrid);
            _preset1BtnLabel = AddGridBtn(row1, sv, $"{_preset1Hour:D2}:{_preset1Min:D2}", () => MinutesUntilTime(_preset1Hour, _preset1Min), sleepImg);
            _preset2BtnLabel = AddGridBtn(row1, sv, $"{_preset2Hour:D2}:{_preset2Min:D2}", () => MinutesUntilTime(_preset2Hour, _preset2Min), sleepImg);
            AddGridBtn(row1, sv, "Rain",      () => MinutesUntilRain(),    sleepImg);

            // Row 2: Storm I, Storm II, Post-Storm
            var row2 = MakePresetRow(presetGrid);
            AddGridBtn(row2, sv, "Storm I",    () => MinutesUntilStorm(1),   sleepImg);
            AddGridBtn(row2, sv, "Storm II",   () => MinutesUntilStorm(2),   sleepImg);
            AddGridBtn(row2, sv, "Post-Storm", () => MinutesUntilStormEnd(), sleepImg);
        }

        private static GameObject MakePresetRow(GameObject parent)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(parent.transform, false);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing               = 4f;
            h.childAlignment        = TextAnchor.MiddleCenter;
            h.childForceExpandWidth  = true;
            h.childForceExpandHeight = true;
            return row;
        }

        private static TextMeshProUGUI AddGridBtn(GameObject row, SleepView sv, string label, Func<float?> getMinutes, Image? styleSource)
        {
            var go  = new GameObject($"P_{label}");
            go.transform.SetParent(row.transform, false);

            var img = go.AddComponent<Image>();
            if (styleSource?.sprite != null)
            {
                img.sprite = styleSource.sprite;
                img.type   = styleSource.type;
                img.color  = styleSource.color;
            }
            else
            {
                img.color = new Color(0.44f, 0.78f, 0.86f, 1f);
            }

            var btn  = go.AddComponent<Button>();
            var cols = btn.colors;
            cols.normalColor      = Color.white;
            cols.highlightedColor = new Color(0.85f, 0.95f, 1f, 1f);
            cols.pressedColor     = new Color(0.55f, 0.75f, 0.85f, 1f);
            btn.colors        = cols;
            btn.targetGraphic = img;

            var txtGo = new GameObject("T");
            txtGo.transform.SetParent(go.transform, false);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text             = label;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin      = 8f;
            tmp.fontSizeMax      = 20f;
            tmp.alignment        = TextAlignmentOptions.Center;
            tmp.enableWordWrapping = false;
            // Copy font, style and color from the Sleep button's own text
            var sleepTxt = styleSource?.GetComponentInChildren<TextMeshProUGUI>(true);
            if (sleepTxt != null)
            {
                tmp.font      = sleepTxt.font;
                tmp.fontStyle = sleepTxt.fontStyle;
                tmp.color     = sleepTxt.color;
            }
            else
            {
                tmp.color = Color.white;
            }
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero; tr.anchoredPosition = Vector2.zero;

            btn.onClick.AddListener(() =>
            {
                var s = GetSlider(sv);
                var m = getMinutes();
                if (s != null && m.HasValue && m.Value > 0)
                    s.value = Mathf.Clamp(m.Value, s.minValue, s.maxValue);
            });
            return tmp;
        }

        // ── Preset time calculations ──────────────────────────────────────

        private static float? MinutesUntilTime(int hour, int minute)
        {
            var now    = GameClock.TimeOfDay;
            var target = new TimeSpan(hour, minute, 0);
            var diff   = target - now;
            if (diff.TotalMinutes <= 0) diff += TimeSpan.FromHours(24);
            return (float)diff.TotalMinutes;
        }

        private static float? MinutesUntilRain()
        {
            if (WeatherManager.GetWeather() == Weather.Rainy) return null;
            var now = GameClock.Now;
            for (int i = 1; i <= 576; i++)
            {
                var future = now + TimeSpan.FromMinutes(i * 5);
                if (WeatherManager.GetWeather(future) == Weather.Rainy)
                    return i * 5f;
            }
            return null;
        }

        private static float? MinutesUntilStorm(int phase)
        {
            if (WeatherManager.Instance?.Storm == null) return null;
            var now = GameClock.Now;
            var eta = phase == 1
                ? WeatherManager.Instance.Storm.GetStormETA(now)
                : WeatherManager.Instance.Storm.GetStormIOverETA(now);
            if (eta.TotalMinutes > 0) return (float)eta.TotalMinutes;
            return null;
        }

        private static float? MinutesUntilStormEnd()
        {
            if (WeatherManager.Instance?.Storm == null) return null;
            var now = GameClock.Now;
            var eta = WeatherManager.Instance.Storm.GetStormIIOverETA(now);
            if (eta.TotalMinutes > 0) return (float)eta.TotalMinutes;
            return null;
        }

        // ── Settings Panel ────────────────────────────────────────────────

        private void BuildSettingsPanel()
        {
            _settingsCanvas = new GameObject("DisplayItemValue_Canvas");
            DontDestroyOnLoad(_settingsCanvas);
            var canvas = _settingsCanvas.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            _settingsCanvas.AddComponent<CanvasScaler>();
            _settingsCanvas.AddComponent<GraphicRaycaster>();

            var border = MakeImage(_settingsCanvas, "Border", new Color(0f, 0.78f, 0.52f, 0.22f));
            SetRect(border, 0, 0, 328, 386);
            var panel = MakeImage(_settingsCanvas, "Panel", new Color(0.067f, 0.071f, 0.082f, 0.97f));
            SetRect(panel, 0, 0, 320, 380);

            // Header
            var header = MakeImage(panel, "Header", new Color(0.10f, 0.11f, 0.14f, 1f));
            SetRect(header, 0, 168, 320, 44);
            var accent = MakeImage(panel, "Accent", new Color(0f, 0.78f, 0.52f, 1f));
            SetRect(accent, 0, 146, 320, 2);
            var title = MakeText(panel, "Title", "m0n0t0ny's mod", 15);
            title.GetComponent<TextMeshProUGUI>().color = Color.white;
            SetRect(title, -20, 168, 240, 28);
            var ver = MakeText(panel, "Ver", "v1.0", 9);
            ver.GetComponent<TextMeshProUGUI>().color = new Color(0f, 0.78f, 0.52f, 1f);
            SetRect(ver, 120, 168, 60, 28);

            // ── Item Value ────────────────────────────────────────────
            var toggleBtn = MakeImage(panel, "ToggleBtn", Color.gray);
            SetRect(toggleBtn, 0, 110, 284, 36);
            _toggleBtnImage = toggleBtn.GetComponent<Image>();
            var toggleBtnText = MakeText(toggleBtn, "Label", "", 13);
            StretchRect(toggleBtnText);
            _toggleBtnText = toggleBtnText.GetComponent<TextMeshProUGUI>();
            toggleBtn.AddComponent<Button>().onClick.AddListener(OnToggleClicked);
            RefreshToggleButton();

            MakeSep(panel, 76, "DISPLAY MODE");

            var modeBtn = MakeImage(panel, "ModeBtn", new Color(0.13f, 0.20f, 0.35f, 1f));
            SetRect(modeBtn, 0, 44, 284, 36);
            var modeBtnText = MakeText(modeBtn, "Label", "", 12);
            StretchRect(modeBtnText);
            _modeBtnText = modeBtnText.GetComponent<TextMeshProUGUI>();
            modeBtn.AddComponent<Button>().onClick.AddListener(OnModeCycleClicked);
            RefreshModeButton();

            // ── Sleep Presets ─────────────────────────────────────────
            MakeSep(panel, 12, "SLEEP PRESETS");

            var sleepToggle = MakeImage(panel, "SleepToggle", Color.gray);
            SetRect(sleepToggle, 0, -18, 284, 32);
            _sleepToggleImage = sleepToggle.GetComponent<Image>();
            var sleepToggleTxt = MakeText(sleepToggle, "Label", "", 12);
            StretchRect(sleepToggleTxt);
            _sleepToggleText = sleepToggleTxt.GetComponent<TextMeshProUGUI>();
            sleepToggle.AddComponent<Button>().onClick.AddListener(OnSleepToggleClicked);
            RefreshSleepToggle();

            // Preset 1
            MakeLineSep(panel, -46);
            var p1Label = MakeText(panel, "P1Label", "Preset 1  (time of day)", 8);
            p1Label.GetComponent<TextMeshProUGUI>().color = new Color(0.4f, 0.4f, 0.5f, 1f);
            SetRect(p1Label, 0, -53, 284, 14);

            var picker1 = new GameObject("Picker1");
            picker1.transform.SetParent(panel.transform, false);
            picker1.AddComponent<RectTransform>();
            SetRect(picker1, 0, -76, 284, 28);
            BuildTimePicker(picker1,
                () => _preset1Hour,
                v => { _preset1Hour = v; PlayerPrefs.SetInt(PREF_PRESET1H, v); PlayerPrefs.Save();
                       if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}"; },
                () => _preset1Min,
                v => { _preset1Min  = v; PlayerPrefs.SetInt(PREF_PRESET1M, v); PlayerPrefs.Save();
                       if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}"; });

            // Preset 2
            MakeLineSep(panel, -96);
            var p2Label = MakeText(panel, "P2Label", "Preset 2  (time of day)", 8);
            p2Label.GetComponent<TextMeshProUGUI>().color = new Color(0.4f, 0.4f, 0.5f, 1f);
            SetRect(p2Label, 0, -103, 284, 14);

            var picker2 = new GameObject("Picker2");
            picker2.transform.SetParent(panel.transform, false);
            picker2.AddComponent<RectTransform>();
            SetRect(picker2, 0, -126, 284, 28);
            BuildTimePicker(picker2,
                () => _preset2Hour,
                v => { _preset2Hour = v; PlayerPrefs.SetInt(PREF_PRESET2H, v); PlayerPrefs.Save();
                       if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}"; },
                () => _preset2Min,
                v => { _preset2Min  = v; PlayerPrefs.SetInt(PREF_PRESET2M, v); PlayerPrefs.Save();
                       if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}"; });

            // ── Close ────────────────────────────────────────────────
            MakeLineSep(panel, -148);
            var closeBtn = MakeImage(panel, "CloseBtn", new Color(0.48f, 0.09f, 0.09f, 1f));
            SetRect(closeBtn, 0, -168, 140, 28);
            var closeTxt = MakeText(closeBtn, "Label", $"Close  [{MENU_KEY}]", 11);
            StretchRect(closeTxt);
            closeBtn.AddComponent<Button>().onClick.AddListener(() => _settingsCanvas!.SetActive(false));
            var hint = MakeText(panel, "Hint", $"[{MENU_KEY}] to open / close", 9);
            hint.GetComponent<TextMeshProUGUI>().color = new Color(0.35f, 0.35f, 0.4f, 1f);
            SetRect(hint, 0, -182, 284, 14);

            _settingsCanvas.SetActive(false);
        }

        // Builds [ − ] [ HH ] [ + ] : [ − ] [ MM ] [ + ]
        private static void BuildTimePicker(
            GameObject parent,
            Func<int> getH, Action<int> setH,
            Func<int> getM, Action<int> setM)
        {
            var layout = parent.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment        = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth  = false;
            layout.childForceExpandHeight = true;
            layout.spacing = 3;

            var hMinus   = MakePickerBtn(parent, "−");
            var hDisplay = MakePickerDisplay(parent, $"{getH():D2}");
            var hPlus    = MakePickerBtn(parent, "+");
            var colon    = MakePickerColon(parent);
            var mMinus   = MakePickerBtn(parent, "−");
            var mDisplay = MakePickerDisplay(parent, $"{getM():D2}");
            var mPlus    = MakePickerBtn(parent, "+");

            var hTxt = hDisplay.GetComponentInChildren<TextMeshProUGUI>()!;
            var mTxt = mDisplay.GetComponentInChildren<TextMeshProUGUI>()!;

            hMinus.GetComponent<Button>().onClick.AddListener(() => { setH(((getH()-1)+24)%24); hTxt.text=$"{getH():D2}"; });
            hPlus.GetComponent<Button>().onClick.AddListener(()  => { setH((getH()+1)%24);      hTxt.text=$"{getH():D2}"; });
            mMinus.GetComponent<Button>().onClick.AddListener(() => { setM(((getM()-10)+60)%60); mTxt.text=$"{getM():D2}"; });
            mPlus.GetComponent<Button>().onClick.AddListener(()  => { setM((getM()+10)%60);     mTxt.text=$"{getM():D2}"; });
        }

        private static GameObject MakePickerBtn(GameObject parent, string label)
        {
            var go = new GameObject($"Btn{label}");
            go.transform.SetParent(parent.transform, false);
            var r = go.AddComponent<RectTransform>();
            r.sizeDelta = new Vector2(24, 28);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.18f, 0.28f, 0.45f, 1f);
            var btn = go.AddComponent<Button>();
            var c   = btn.colors;
            c.highlightedColor = new Color(0.26f, 0.42f, 0.65f, 1f);
            c.pressedColor     = new Color(0.10f, 0.16f, 0.28f, 1f);
            btn.colors = c;
            var t = new GameObject("T");
            t.transform.SetParent(go.transform, false);
            var tmp = t.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center;
            var tr = t.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero; tr.anchoredPosition = Vector2.zero;
            go.AddComponent<LayoutElement>().preferredWidth = 24;
            return go;
        }

        private static GameObject MakePickerDisplay(GameObject parent, string text)
        {
            var go = new GameObject("Disp");
            go.transform.SetParent(parent.transform, false);
            var r = go.AddComponent<RectTransform>();
            r.sizeDelta = new Vector2(36, 28);
            var img = go.AddComponent<Image>();
            img.color = new Color(0.05f, 0.05f, 0.08f, 1f);
            var t = new GameObject("T");
            t.transform.SetParent(go.transform, false);
            var tmp = t.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center;
            var tr = t.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero; tr.anchoredPosition = Vector2.zero;
            go.AddComponent<LayoutElement>().preferredWidth = 36;
            return go;
        }

        private static GameObject MakePickerColon(GameObject parent)
        {
            var go = new GameObject("Colon");
            go.transform.SetParent(parent.transform, false);
            var r = go.AddComponent<RectTransform>();
            r.sizeDelta = new Vector2(12, 28);
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = ":"; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.6f, 0.6f, 0.7f, 1f);
            go.AddComponent<LayoutElement>().preferredWidth = 12;
            return go;
        }

        private void MakeSep(GameObject parent, float y, string label)
        {
            MakeLineSep(parent, y + 8);
            var lbl = MakeText(parent, $"SepLbl_{label}", label, 8);
            lbl.GetComponent<TextMeshProUGUI>().color = new Color(0.4f, 0.4f, 0.5f, 1f);
            SetRect(lbl, 0, y, 284, 14);
        }

        private static void MakeLineSep(GameObject parent, float y)
        {
            var line = new GameObject("Line");
            line.transform.SetParent(parent.transform, false);
            line.AddComponent<RectTransform>();
            line.AddComponent<Image>().color = new Color(0.22f, 0.22f, 0.27f, 1f);
            SetRect(line, 0, y, 284, 1);
        }

        // ── Settings callbacks ────────────────────────────────────────────

        private void OnToggleClicked()
        {
            _showValue = !_showValue;
            PlayerPrefs.SetInt(PREF_ENABLED, _showValue ? 1 : 0);
            PlayerPrefs.Save();
            RefreshToggleButton();
        }

        private void OnModeCycleClicked()
        {
            _mode = _mode == DisplayMode.StackOnly ? DisplayMode.Combined : _mode + 1;
            PlayerPrefs.SetInt(PREF_MODE, (int)_mode);
            PlayerPrefs.Save();
            RefreshModeButton();
        }

        private void OnSleepToggleClicked()
        {
            _sleepPresetsEnabled = !_sleepPresetsEnabled;
            PlayerPrefs.SetInt(PREF_SLEEP_ENABLED, _sleepPresetsEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSleepToggle();
        }

        private void RefreshToggleButton()
        {
            _toggleBtnImage!.color = _showValue
                ? new Color(0.15f, 0.55f, 0.15f, 1f)
                : new Color(0.55f, 0.15f, 0.15f, 1f);
            _toggleBtnText!.text = _showValue ? "Item Value Display: ON" : "Item Value Display: OFF";
        }

        private void RefreshModeButton()
        {
            _modeBtnText!.text = _mode switch
            {
                DisplayMode.SingleOnly => "Single item value only",
                DisplayMode.StackOnly  => "Stack total value only",
                _                      => "Combined  (single / stack)",
            };
        }

        private void RefreshSleepToggle()
        {
            _sleepToggleImage!.color = _sleepPresetsEnabled
                ? new Color(0.15f, 0.55f, 0.15f, 1f)
                : new Color(0.55f, 0.15f, 0.15f, 1f);
            _sleepToggleText!.text = _sleepPresetsEnabled
                ? "Sleep Presets: ON"
                : "Sleep Presets: OFF";
        }

        // ── Item Hover UI ─────────────────────────────────────────────────

        private void OnSetupMeta(ItemHoveringUI ui, ItemMetaData data)
        {
            ValueText.gameObject.SetActive(false);
        }

        private void OnSetupItemHoveringUI(ItemHoveringUI uiInstance, Item item)
        {
            if (!_showValue || item == null)
            {
                ValueText.gameObject.SetActive(false);
                return;
            }

            int singleValue = (int)(item.Value / 2);
            int stackValue  = (int)(item.GetTotalRawValue() / 2);
            bool isStack    = item.StackCount > 1;

            ValueText.gameObject.SetActive(true);
            ValueText.transform.SetParent(uiInstance.LayoutParent);
            ValueText.transform.localScale = Vector3.one;
            ValueText.text = _mode switch
            {
                DisplayMode.SingleOnly => $"${singleValue}",
                DisplayMode.StackOnly  => $"${stackValue}",
                _                      => isStack ? $"${singleValue} / ${stackValue}" : $"${singleValue}",
            };
            ValueText.fontSize = 20f;
        }

        // ── UI Helpers ────────────────────────────────────────────────────

        private static GameObject MakeImage(GameObject parent, string name, Color color)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = color;
            return go;
        }

        private static GameObject MakeText(GameObject parent, string name, string text, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size; tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }

        private static void SetRect(GameObject go, float x, float y, float w, float h)
        {
            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta        = new Vector2(w, h);
        }

        private static void StretchRect(GameObject go)
        {
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero; r.anchoredPosition = Vector2.zero;
        }
    }
}
