using Duckov.UI;
using Duckov.Utilities;
using ItemStatsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DisplayItemValue
{
    enum DisplayMode { Combined, SingleOnly, StackOnly }

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        private const string PREF_ENABLED = "DisplayItemValue_Enabled";
        private const string PREF_MODE    = "DisplayItemValue_Mode";
        private const KeyCode MENU_KEY    = KeyCode.F9;

        private bool _showValue;
        private DisplayMode _mode;
        private TextMeshProUGUI? _valueText;
        private GameObject? _settingsCanvas;
        private Image? _toggleBtnImage;
        private TextMeshProUGUI? _toggleBtnText;
        private TextMeshProUGUI? _modeBtnText;

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
            _showValue = PlayerPrefs.GetInt(PREF_ENABLED, 1) == 1;
            _mode      = (DisplayMode)PlayerPrefs.GetInt(PREF_MODE, (int)DisplayMode.Combined);
            Debug.Log($"[DisplayItemValue] Loaded. Press {MENU_KEY} to open settings.");
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
        }

        // ── Settings Panel ────────────────────────────────────────────────

        private void BuildSettingsPanel()
        {
            // Canvas
            _settingsCanvas = new GameObject("DisplayItemValue_Canvas");
            DontDestroyOnLoad(_settingsCanvas);
            var canvas = _settingsCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            _settingsCanvas.AddComponent<CanvasScaler>();
            _settingsCanvas.AddComponent<GraphicRaycaster>();

            // Outer glow border
            var border = MakeImage(_settingsCanvas, "Border", new Color(0f, 0.78f, 0.52f, 0.22f));
            SetRect(border, 0, 0, 328, 248);

            // Panel background
            var panel = MakeImage(_settingsCanvas, "Panel", new Color(0.067f, 0.071f, 0.082f, 0.97f));
            SetRect(panel, 0, 0, 320, 240);

            // Header background
            var header = MakeImage(panel, "Header", new Color(0.10f, 0.11f, 0.14f, 1f));
            SetRect(header, 0, 98, 320, 44);

            // Header accent line (bottom of header)
            var accent = MakeImage(panel, "Accent", new Color(0f, 0.78f, 0.52f, 1f));
            SetRect(accent, 0, 76, 320, 2);

            // Title
            var title = MakeText(panel, "Title", "Display Item Value", 15);
            title.GetComponent<TextMeshProUGUI>().color = Color.white;
            SetRect(title, -20, 98, 240, 28);

            // Version tag
            var ver = MakeText(panel, "Ver", "v1.0", 9);
            ver.GetComponent<TextMeshProUGUI>().color = new Color(0f, 0.78f, 0.52f, 1f);
            SetRect(ver, 120, 98, 60, 28);

            // Toggle button
            var toggleBtn = MakeImage(panel, "ToggleBtn", Color.gray);
            SetRect(toggleBtn, 0, 42, 284, 38);
            _toggleBtnImage = toggleBtn.GetComponent<Image>();
            var toggleBtnText = MakeText(toggleBtn, "Label", "", 13);
            StretchRect(toggleBtnText);
            _toggleBtnText = toggleBtnText.GetComponent<TextMeshProUGUI>();
            toggleBtn.AddComponent<Button>().onClick.AddListener(OnToggleClicked);
            RefreshToggleButton();

            // Separator with label
            var sepLine = MakeImage(panel, "SepLine", new Color(0.22f, 0.22f, 0.27f, 1f));
            SetRect(sepLine, 0, 12, 284, 1);
            var sepLabel = MakeText(panel, "SepLabel", "DISPLAY MODE", 8);
            sepLabel.GetComponent<TextMeshProUGUI>().color = new Color(0.4f, 0.4f, 0.5f, 1f);
            SetRect(sepLabel, 0, 5, 284, 14);

            // Mode cycle button
            var modeBtn = MakeImage(panel, "ModeBtn", new Color(0.13f, 0.20f, 0.35f, 1f));
            SetRect(modeBtn, 0, -28, 284, 38);
            var modeBtnText = MakeText(modeBtn, "Label", "", 12);
            StretchRect(modeBtnText);
            _modeBtnText = modeBtnText.GetComponent<TextMeshProUGUI>();
            modeBtn.AddComponent<Button>().onClick.AddListener(OnModeCycleClicked);
            RefreshModeButton();

            // Bottom separator
            var sepLine2 = MakeImage(panel, "SepLine2", new Color(0.22f, 0.22f, 0.27f, 1f));
            SetRect(sepLine2, 0, -58, 284, 1);

            // Close button
            var closeBtn = MakeImage(panel, "CloseBtn", new Color(0.48f, 0.09f, 0.09f, 1f));
            SetRect(closeBtn, 0, -84, 140, 30);
            var closeBtnText = MakeText(closeBtn, "Label", $"Close  [{MENU_KEY}]", 11);
            StretchRect(closeBtnText);
            closeBtn.AddComponent<Button>().onClick.AddListener(() => _settingsCanvas!.SetActive(false));

            // Hint
            var hint = MakeText(panel, "Hint", $"[{MENU_KEY}] to open / close", 9);
            hint.GetComponent<TextMeshProUGUI>().color = new Color(0.35f, 0.35f, 0.4f, 1f);
            SetRect(hint, 0, -108, 284, 14);

            _settingsCanvas.SetActive(false);
        }

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
            var img = go.AddComponent<Image>();
            img.color = color;
            return go;
        }

        private static GameObject MakeText(GameObject parent, string name, string text, float size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = size;
            tmp.alignment = TextAlignmentOptions.Center;
            return go;
        }

        private static void SetRect(GameObject go, float x, float y, float w, float h)
        {
            var r = go.GetComponent<RectTransform>();
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(w, h);
        }

        private static void StretchRect(GameObject go)
        {
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = Vector2.zero;
            r.anchorMax = Vector2.one;
            r.sizeDelta = Vector2.zero;
            r.anchoredPosition = Vector2.zero;
        }
    }
}
