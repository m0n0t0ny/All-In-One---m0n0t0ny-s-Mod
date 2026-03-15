using System;
using System.Collections.Generic;
using System.Reflection;
using Duckov.UI;
using Duckov.Utilities;
using Duckov.Weathers;
using ItemStatsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace m0n0t0nysMod
{
    enum DisplayMode { Combined, SingleOnly, StackOnly }
    enum TransferModifier { Shift, Alt }

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        // ── PlayerPrefs keys ──────────────────────────────────────────────
        private const string PREF_ENABLED        = "DisplayItemValue_Enabled";
        private const string PREF_MODE           = "DisplayItemValue_Mode";
        private const string PREF_SLEEP_ENABLED  = "DisplayItemValue_SleepEnabled";
        private const string PREF_PRESET1H       = "DisplayItemValue_Preset1H";
        private const string PREF_PRESET1M       = "DisplayItemValue_Preset1M";
        private const string PREF_PRESET2H       = "DisplayItemValue_Preset2H";
        private const string PREF_PRESET2M        = "DisplayItemValue_Preset2M";
        private const string PREF_ENEMY_NAMES       = "DisplayItemValue_EnemyNames";
        private const string PREF_TRANSFER_ENABLED  = "DisplayItemValue_TransferEnabled";
        private const string PREF_TRANSFER_MOD      = "DisplayItemValue_TransferMod";
        private const string PREF_AC_WASD            = "DisplayItemValue_ACWasd";
        private const string PREF_AC_SHIFT           = "DisplayItemValue_ACShift";
        private const string PREF_AC_SPACE           = "DisplayItemValue_ACSpace";
        private const string PREF_AC_DAMAGE          = "DisplayItemValue_ACDamage";
        private const KeyCode MENU_KEY               = KeyCode.F9;

        // ── Item value display ────────────────────────────────────────────
        private bool _showValue;
        private DisplayMode _mode;
        private TextMeshProUGUI? _valueText;
        private Item? _lastHoveredItem;

        // ── Shift/Alt-click transfer ──────────────────────────────────────
        private bool _transferEnabled;
        private TransferModifier _transferModifier;
        private Item? _transferCachedItem; // snapshotted at end of each frame via LateUpdate
        private Image? _transferToggleImage;
        private TextMeshProUGUI? _transferToggleText;
        private Image[]? _transferModBtnImages;
        private TextMeshProUGUI[]? _transferModBtnLabels;
        private GameObject? _shiftConflictWarning;
        private static readonly FieldInfo? _lootCharInvField =
            typeof(LootView).GetField("characterInventoryDisplay", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? _lootTargetInvField =
            typeof(LootView).GetField("lootTargetInventoryDisplay", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo? _invDisplayTargetProp =
            typeof(InventoryDisplay).GetProperty("Target", BindingFlags.Public | BindingFlags.Instance);

        // ── Auto-close container ──────────────────────────────────────────
        private bool _autoCloseOnWASD;
        private bool _autoCloseOnShift;
        private bool _autoCloseOnSpace;
        private bool _autoCloseOnDamage;
        private Image[]? _autoCloseBtnImages;
        private TextMeshProUGUI[]? _autoCloseBtnTexts;
        private Component? _playerHealthComp;
        private PropertyInfo? _playerHealthValueProp;
        private float _playerHealthPrev = float.MaxValue;
        private float _damageInitTimer;

        // ── Enemy name display ────────────────────────────────────────────
        private bool _showEnemyNames;
        private float _nameUpdateTimer;
        private static readonly FieldInfo? _hbNameTextField =
            typeof(HealthBar).GetField("nameText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? _characterPresetField =
            typeof(CharacterMainControl).GetField("characterPreset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo? _displayNameProp =
            typeof(CharacterMainControl)
                .GetField("characterPreset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);

        // ── Settings panel UI refs ────────────────────────────────────────
        private GameObject? _settingsCanvas;
        private Image? _toggleBtnImage;
        private TextMeshProUGUI? _toggleBtnText;
        private Image[]? _modeBtnImages;
        private TextMeshProUGUI[]? _modeBtnLabels;
        private Image? _enemyNamesToggleImage;
        private TextMeshProUGUI? _enemyNamesToggleText;
        private Image? _sleepToggleImage;
        private TextMeshProUGUI? _sleepToggleText;

        // ── Sleep preset state ────────────────────────────────────────────
        private bool _sleepPresetsEnabled;
        private int  _preset1Hour, _preset1Min;
        private int  _preset2Hour, _preset2Min;
        private SleepView? _sleepViewInstance;
        private bool _sleepPresetsInjected;
        private TextMeshProUGUI? _preset1BtnLabel;
        private TextMeshProUGUI? _preset2BtnLabel;

        // ── Factory Recorder badge ────────────────────────────────────────
        private const string PREF_RECORDER_BADGE = "DisplayItemValue_RecorderBadge";
        private bool _showRecorderBadge;
        private Image? _recorderToggleImage;
        private TextMeshProUGUI? _recorderToggleText;
        // CraftingManager reflection
        private static PropertyInfo? _cmInstanceProp;
        private static MethodInfo?   _cmIsUnlockedMethod;
        // Item.ItemPreset and preset.ID reflection
        private static PropertyInfo? _itemPresetProp;
        private static PropertyInfo? _presetIdProp;
        // Slot badge overlay tracking
        private static Type?       _slotCompType;
        private static MemberInfo? _slotItemMember; // PropertyInfo or FieldInfo → Item
        private static readonly Dictionary<Type, MemberInfo?> _typeItemMemberCache = new Dictionary<Type, MemberInfo?>();
        private float _badgeScanTimer;
        private readonly Dictionary<int, GameObject> _slotBadges = new Dictionary<int, GameObject>();

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
            _showValue           = PlayerPrefs.GetInt(PREF_ENABLED,      1) == 1;
            _mode                = (DisplayMode)PlayerPrefs.GetInt(PREF_MODE, (int)DisplayMode.Combined);
            _showEnemyNames      = PlayerPrefs.GetInt(PREF_ENEMY_NAMES,      1) == 1;
            _transferEnabled     = PlayerPrefs.GetInt(PREF_TRANSFER_ENABLED, 1) == 1;
            _transferModifier    = (TransferModifier)PlayerPrefs.GetInt(PREF_TRANSFER_MOD, (int)TransferModifier.Shift);
            _autoCloseOnWASD     = PlayerPrefs.GetInt(PREF_AC_WASD,   0) == 1;
            _autoCloseOnShift    = PlayerPrefs.GetInt(PREF_AC_SHIFT,  0) == 1;
            _autoCloseOnSpace    = PlayerPrefs.GetInt(PREF_AC_SPACE,  0) == 1;
            _autoCloseOnDamage   = PlayerPrefs.GetInt(PREF_AC_DAMAGE, 0) == 1;
            _sleepPresetsEnabled = PlayerPrefs.GetInt(PREF_SLEEP_ENABLED, 1) == 1;
            _preset1Hour         = PlayerPrefs.GetInt(PREF_PRESET1H,  5);
            _preset1Min          = PlayerPrefs.GetInt(PREF_PRESET1M, 30);
            _preset2Hour         = PlayerPrefs.GetInt(PREF_PRESET2H, 21);
            _preset2Min          = PlayerPrefs.GetInt(PREF_PRESET2M, 30);
            _showRecorderBadge   = PlayerPrefs.GetInt(PREF_RECORDER_BADGE, 1) == 1;
            Debug.Log($"[m0n0t0nysMod] Loaded. Press {MENU_KEY} to open settings.");
            CacheRecorderReflection();
BuildSettingsPanel();
        }

        void OnDestroy()
        {
            if (_valueText != null) Destroy(_valueText.gameObject);
            if (_settingsCanvas != null) Destroy(_settingsCanvas);
            foreach (var kvp in _slotBadges)
                if (kvp.Value != null) Destroy(kvp.Value);
            _slotBadges.Clear();
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

            if (_transferEnabled && Input.GetMouseButtonDown(0))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool alt   = Input.GetKey(KeyCode.LeftAlt)   || Input.GetKey(KeyCode.RightAlt);
                bool mod   = _transferModifier == TransferModifier.Shift ? shift : alt;
if (mod) TryShiftClickTransfer();
            }

            if (_showEnemyNames)
            {
                _nameUpdateTimer -= Time.deltaTime;
                if (_nameUpdateTimer <= 0f)
                {
                    _nameUpdateTimer = 0.15f;
                    UpdateEnemyNameBars();
                }
            }

            if (_autoCloseOnWASD || _autoCloseOnShift || _autoCloseOnSpace || _autoCloseOnDamage)
                CheckAutoCloseContainer();

            if (_showRecorderBadge)
            {
                _badgeScanTimer -= Time.deltaTime;
                if (_badgeScanTimer <= 0f)
                {
                    _badgeScanTimer = 0.5f;
                    ScanAndBadgeSlots();
                }
            }
        }

        void LateUpdate()
        {
            // Snapshot the hovered item at the END of every frame so that
            // TryShiftClickTransfer() in the NEXT frame's Update() sees a
            // stable value even if EventSystem clears the hover mid-frame.
            _transferCachedItem = _lastHoveredItem;
        }

        // ── Enemy Name Bars ───────────────────────────────────────────────

        private void UpdateEnemyNameBars()
        {
            foreach (var hb in FindObjectsOfType<HealthBar>())
            {
                if (hb == null || !hb.gameObject.activeInHierarchy) continue;

                var health = hb.target;
                if (health == null) continue;

                var character = health.TryGetCharacter();
                if (character == null || character.IsMainCharacter) continue;

                var preset = _characterPresetField?.GetValue(character);
                if (preset == null) continue;

                var displayName = _displayNameProp?.GetValue(preset) as string;
                if (string.IsNullOrEmpty(displayName)) continue;

                var nameTMP = _hbNameTextField?.GetValue(hb) as TextMeshProUGUI;
                if (nameTMP == null) continue;

                nameTMP.text = displayName;
                nameTMP.gameObject.SetActive(true);
            }
        }

        // ── Shift-click transfer ──────────────────────────────────────────

        private void TryShiftClickTransfer()
        {
            var lv = LootView.Instance ?? FindObjectOfType<LootView>();
            bool lvActive = lv != null && lv.gameObject.activeInHierarchy;
            var item = _transferCachedItem ?? _lastHoveredItem;
if (!lvActive || item == null) return;

            var charInvDisplay = _lootCharInvField?.GetValue(lv)  as InventoryDisplay;
            var lootInvDisplay = _lootTargetInvField?.GetValue(lv) as InventoryDisplay;
            var charInv = _invDisplayTargetProp?.GetValue(charInvDisplay) as Inventory;
            var lootInv = _invDisplayTargetProp?.GetValue(lootInvDisplay) as Inventory;

            if (lootInv != null && lootInv.Content.Contains(item) && charInv != null)
            {
                lootInv.RemoveItem(item);
                if (!charInv.AddItem(item))
                    lootInv.AddItem(item);
            }
            else if (charInv != null && charInv.Content.Contains(item) && lootInv != null)
            {
                charInv.RemoveItem(item);
                if (!lootInv.AddItem(item))
                    charInv.AddItem(item);
            }
        }

        // ── Auto-close container ──────────────────────────────────────────

        private void CheckAutoCloseContainer()
        {
            var lv = LootView.Instance ?? FindObjectOfType<LootView>();
            if (lv == null || !lv.gameObject.activeInHierarchy) return;

            if (_autoCloseOnWASD &&
                (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.A) ||
                 Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.D)))
            { lv.Close(); return; }

            if (_autoCloseOnShift &&
                (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift)))
            { lv.Close(); return; }

            if (_autoCloseOnSpace && Input.GetKeyDown(KeyCode.Space))
            { lv.Close(); return; }

            if (_autoCloseOnDamage)
            {
                if (_playerHealthComp == null)
                {
                    _damageInitTimer -= Time.deltaTime;
                    if (_damageInitTimer <= 0f) { _damageInitTimer = 3f; TryInitPlayerHealth(); }
                }
                else if (_playerHealthValueProp != null)
                {
                    float cur = (float)(_playerHealthValueProp.GetValue(_playerHealthComp) ?? (object)float.MaxValue);
                    if (cur < _playerHealthPrev) { _playerHealthPrev = cur; lv.Close(); return; }
                    _playerHealthPrev = cur;
                }
            }
        }

        private void TryInitPlayerHealth()
        {
            foreach (var ch in FindObjectsOfType<CharacterMainControl>())
            {
                if (!ch.IsMainCharacter) continue;
                foreach (var comp in ch.GetComponents<Component>())
                {
                    var t = comp.GetType();
                    var prop = t.GetProperty("Current", BindingFlags.Public | BindingFlags.Instance);
                    if (prop?.PropertyType == typeof(float))
                    {
                        _playerHealthComp     = comp;
                        _playerHealthValueProp = prop;
                        _playerHealthPrev      = (float)prop.GetValue(comp)!;
                        return;
                    }
                }
            }
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

            sleepBtn.transform.SetParent(wrapper.transform, false);
            var sleepLE = sleepBtn.gameObject.GetComponent<LayoutElement>();
            if (sleepLE == null) sleepLE = sleepBtn.gameObject.AddComponent<LayoutElement>();
            sleepLE.preferredWidth = origSizeDelta.x * 0.32f;
            sleepLE.flexibleWidth  = 0f;

            var presetGrid = new GameObject("PresetGrid");
            presetGrid.transform.SetParent(wrapper.transform, false);
            var gridLE = presetGrid.AddComponent<LayoutElement>();
            gridLE.flexibleWidth = 1f;
            var vLayout = presetGrid.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing               = 4f;
            vLayout.childAlignment        = TextAnchor.MiddleCenter;
            vLayout.childForceExpandWidth  = true;
            vLayout.childForceExpandHeight = true;

            var row1 = MakePresetRow(presetGrid);
            _preset1BtnLabel = AddGridBtn(row1, sv, $"{_preset1Hour:D2}:{_preset1Min:D2}", () => MinutesUntilTime(_preset1Hour, _preset1Min), sleepImg);
            _preset2BtnLabel = AddGridBtn(row1, sv, $"{_preset2Hour:D2}:{_preset2Min:D2}", () => MinutesUntilTime(_preset2Hour, _preset2Min), sleepImg);
            AddGridBtn(row1, sv, "Rain",      () => MinutesUntilRain(),    sleepImg);

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
                {
                    if (m.Value > s.maxValue) s.maxValue = m.Value;
                    s.value = m.Value;
                }
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

        // ── Factory Recorder reflection ───────────────────────────────────

        private static void CacheRecorderReflection()
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type[]? types = null;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.Name != "CraftingManager") continue;
                    _cmInstanceProp     = t.GetProperty("Instance",          BindingFlags.Public | BindingFlags.Static);
                    _cmIsUnlockedMethod = t.GetMethod("IsFormulaUnlocked",   BindingFlags.Public | BindingFlags.Instance);
                    Debug.Log($"[m0n0t0nysMod] CraftingManager cached: instance={_cmInstanceProp != null}, unlock={_cmIsUnlockedMethod != null}");
                    break;
                }
            }
            // Cache Item → preset → ID reflection
            var allItemProps = typeof(Item).GetProperties(BindingFlags.Public | BindingFlags.Instance);
            foreach (var p in allItemProps)
            {
                if (!p.Name.Contains("Preset")) continue;
                var idProp = p.PropertyType.GetProperty("ID", BindingFlags.Public | BindingFlags.Instance);
                if (idProp != null && idProp.PropertyType == typeof(string))
                {
                    _itemPresetProp = p;
                    _presetIdProp   = idProp;
                    Debug.Log($"[m0n0t0nysMod] Item preset cached: {p.Name}.{idProp.Name}");
                    break;
                }
            }
        }

        private static object? GetCM() => _cmInstanceProp?.GetValue(null);

        private static string? GetItemPresetId(Item item)
        {
            if (_itemPresetProp == null) return null;
            var preset = _itemPresetProp.GetValue(item);
            if (preset == null) return null;
            return _presetIdProp?.GetValue(preset) as string;
        }

        private static bool IsRecipeRecorded(Item item)
        {
            var id = GetItemPresetId(item);
            if (id == null) return false;
            var cm = GetCM();
            if (cm == null || _cmIsUnlockedMethod == null) return false;
            return (bool)(_cmIsUnlockedMethod.Invoke(cm, new object[] { id }) ?? false);
        }

        // ── Slot badge scanning ───────────────────────────────────────────

        // Called from OnSetupItemHoveringUI — finds the slot by matching the exact Item instance
        private static void TryCacheSlotTypeFromHover(Item item)
        {
            var es = EventSystem.current;
            if (es == null) return;

            var pointerData = new PointerEventData(es) { position = Input.mousePosition };
            var results = new List<RaycastResult>();
            es.RaycastAll(pointerData, results);

            foreach (var result in results)
            {
                if (result.gameObject == null) continue;
                var t = result.gameObject.transform;
                while (t != null)
                {
                    foreach (var mb in t.GetComponents<MonoBehaviour>())
                    {
                        if (mb == null) continue;
                        var compType = mb.GetType();

                        foreach (var prop in compType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (prop.PropertyType != typeof(Item)) continue;
                            try
                            {
                                if (prop.GetValue(mb) == (object)item)
                                {
                                    _slotCompType   = compType;
                                    _slotItemMember = prop;
                                    Debug.Log($"[m0n0t0nysMod] Slot found via hover: {compType.FullName}, prop: {prop.Name}");
                                    return;
                                }
                            }
                            catch { }
                        }

                        foreach (var field in compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (field.FieldType != typeof(Item)) continue;
                            try
                            {
                                if (field.GetValue(mb) == (object)item)
                                {
                                    _slotCompType   = compType;
                                    _slotItemMember = field;
                                    Debug.Log($"[m0n0t0nysMod] Slot found via hover: {compType.FullName}, field: {field.Name}");
                                    return;
                                }
                            }
                            catch { }
                        }
                    }
                    t = t.parent;
                }
            }
        }

        private void ScanAndBadgeSlots()
        {
            if (_slotCompType != null)
            {
                // Fast path: scan only instances of the known slot type
                var slots = FindObjectsOfType(_slotCompType);
                var seen  = new HashSet<int>();

                foreach (var obj in slots)
                {
                    var mb = obj as MonoBehaviour;
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                    int id = mb.GetInstanceID();
                    seen.Add(id);

                    Item? item = null;
                    if (_slotItemMember is PropertyInfo pi)
                        item = pi.GetValue(mb) as Item;
                    else if (_slotItemMember is FieldInfo fi)
                        item = fi.GetValue(mb) as Item;

                    bool showBadge = item != null && IsRecipeRecorded(item);

                    if (!_slotBadges.TryGetValue(id, out var badge))
                    {
                        if (!showBadge) continue;
                        badge = CreateSlotBadge(mb);
                        _slotBadges[id] = badge;
                    }
                    if (badge != null) badge.SetActive(showBadge);
                }

                var toRemove = new List<int>();
                foreach (var kvp in _slotBadges)
                {
                    if (!seen.Contains(kvp.Key))
                    {
                        if (kvp.Value != null) Destroy(kvp.Value);
                        toRemove.Add(kvp.Key);
                    }
                }
                foreach (var k in toRemove) _slotBadges.Remove(k);
            }
            else
            {
                // Slow path: scan ALL UI MonoBehaviours to find recorded recipe items
                // Runs until _slotCompType is discovered (then fast path takes over)
                BroadScanForRecordedItems();
            }
        }

        private void BroadScanForRecordedItems()
        {
            foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (mb.GetComponent<RectTransform>() == null) continue;
                if (mb.GetType() == GetType()) continue; // skip self

                var compType = mb.GetType();
                int id = mb.GetInstanceID();

                // Resolve cached Item member for this component type (reflection only once per type)
                if (!_typeItemMemberCache.TryGetValue(compType, out var member))
                {
                    member = null;
                    foreach (var prop in compType.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                    {
                        if (prop.PropertyType != typeof(Item)) continue;
                        member = prop;
                        break;
                    }
                    if (member == null)
                    {
                        foreach (var field in compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (field.FieldType != typeof(Item)) continue;
                            member = field;
                            break;
                        }
                    }
                    _typeItemMemberCache[compType] = member; // null → this type has no Item member
                }

                if (member == null) continue;

                Item? item = null;
                try
                {
                    if (member is PropertyInfo pi2) item = pi2.GetValue(mb) as Item;
                    else if (member is FieldInfo fi2) item = fi2.GetValue(mb) as Item;
                }
                catch { continue; }

                bool showBadge = item != null && IsRecipeRecorded(item);

                if (!_slotBadges.TryGetValue(id, out var badge))
                {
                    if (!showBadge) continue;
                    badge = CreateSlotBadge(mb);
                    _slotBadges[id] = badge;
                    // Cache slot type so next tick uses the fast path
                    if (_slotCompType == null)
                    {
                        _slotCompType   = compType;
                        _slotItemMember = member;
                        Debug.Log($"[m0n0t0nysMod] Slot type cached via broad scan: {compType.Name}.{member.Name}");
                    }
                }
                else
                {
                    badge?.SetActive(showBadge);
                }
            }

            // Cleanup badges whose slot objects are gone or no longer active
            var stale = new List<int>();
            foreach (var kvp in _slotBadges)
            {
                if (kvp.Value == null || kvp.Value.transform.parent == null ||
                    !kvp.Value.transform.parent.gameObject.activeInHierarchy)
                {
                    if (kvp.Value != null) Destroy(kvp.Value);
                    stale.Add(kvp.Key);
                }
            }
            foreach (var k in stale) _slotBadges.Remove(k);
        }

        private static GameObject CreateSlotBadge(MonoBehaviour slot)
        {
            var badge = new GameObject("RecorderBadge");
            badge.transform.SetParent(slot.transform, false);
            var rt = badge.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 0f);
            rt.anchorMax        = new Vector2(1f, 0f);
            rt.pivot            = new Vector2(1f, 0f);
            rt.anchoredPosition = new Vector2(-2f, 2f);
            rt.sizeDelta        = new Vector2(14f, 14f);

            var circleImg = badge.AddComponent<Image>();
            circleImg.color = new Color(0.13f, 0.65f, 0.28f, 1f);

            var txtGo = new GameObject("Check");
            txtGo.transform.SetParent(badge.transform, false);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = "✓";
            tmp.fontSize  = 10f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero; tr.anchoredPosition = Vector2.zero;

            return badge;
        }

        // ── Settings Panel ────────────────────────────────────────────────

        private void BuildSettingsPanel()
        {
            _settingsCanvas = new GameObject("m0n0t0nysMod_Canvas");
            DontDestroyOnLoad(_settingsCanvas);
            var canvas = _settingsCanvas.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            _settingsCanvas.AddComponent<CanvasScaler>();
            _settingsCanvas.AddComponent<GraphicRaycaster>();

            // Panel — auto-sizes vertically, fixed 360px wide, centered
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_settingsCanvas.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(360f, 0f);
            panel.AddComponent<Image>().color = new Color(0.063f, 0.065f, 0.080f, 0.97f);
            var panelVLG = panel.AddComponent<VerticalLayoutGroup>();
            panelVLG.childAlignment       = TextAnchor.UpperCenter;
            panelVLG.childForceExpandWidth  = true;
            panelVLG.childForceExpandHeight = false;
            panelVLG.spacing = 0f;
            panelVLG.padding = new RectOffset(0, 0, 0, 0);
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Header ────────────────────────────────────────────────────
            var header = LChild(panel, "Header", 50f);
            header.GetComponent<Image>().color = new Color(0.10f, 0.11f, 0.145f, 1f);
            var hHLG = header.AddComponent<HorizontalLayoutGroup>();
            hHLG.padding              = new RectOffset(16, 16, 0, 0);
            hHLG.childAlignment       = TextAnchor.MiddleLeft;
            hHLG.childForceExpandHeight = true;
            hHLG.childForceExpandWidth  = false;
            hHLG.spacing = 0f;
            var titleGo = LText(header, "Title", "m0n0t0ny's mod", 17f, flexW: 1f);
            var titleTMP = titleGo.GetComponent<TextMeshProUGUI>();
            titleTMP.color = Color.white;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Left;
            var verGo = LText(header, "Ver", "v1.7", 10f, prefW: 44f);
            verGo.GetComponent<TextMeshProUGUI>().color = new Color(0f, 0.78f, 0.52f, 1f);
            verGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

            // ── Teal accent line ──────────────────────────────────────────
            var accent = LChild(panel, "Accent", 2f);
            accent.GetComponent<Image>().color = new Color(0f, 0.78f, 0.52f, 1f);

            LGap(panel, 10f);

            // ── Section 1: Item Value ─────────────────────────────────────
            var s1 = LSectionCard(panel);

            LLabel(s1, "ITEM VALUE", 8f, new Color(0f, 0.78f, 0.52f, 1f));
            LDivider(s1);

            var (tRow, tImg, tTMP) = LToggleRow(s1, "Show sell value on hover");
            _toggleBtnImage = tImg;
            _toggleBtnText  = tTMP;
            tRow.GetComponentInChildren<Button>().onClick.AddListener(OnToggleClicked);
            RefreshToggleButton();

            LLabel(s1, "Display Mode", 10f, new Color(0.48f, 0.48f, 0.58f, 1f));

            var modeRow = LChild(s1, "ModeRow", 34f);
            modeRow.GetComponent<Image>().color = Color.clear;
            var mHLG = modeRow.AddComponent<HorizontalLayoutGroup>();
            mHLG.spacing               = 5f;
            mHLG.childForceExpandWidth  = true;
            mHLG.childForceExpandHeight = true;

            var (btnS, imgS, lblS) = LModeBtn(modeRow, "Single");
            var (btnC, imgC, lblC) = LModeBtn(modeRow, "Combined");
            var (btnT, imgT, lblT) = LModeBtn(modeRow, "Stack");
            _modeBtnImages = new[] { imgS, imgC, imgT };
            _modeBtnLabels = new[] { lblS, lblC, lblT };
            btnS.onClick.AddListener(() => SetMode(DisplayMode.SingleOnly));
            btnC.onClick.AddListener(() => SetMode(DisplayMode.Combined));
            btnT.onClick.AddListener(() => SetMode(DisplayMode.StackOnly));
            RefreshModeButtons();

            LGap(panel, 8f);

            // ── Section 2: Enemy Names ────────────────────────────────────
            var s2 = LSectionCard(panel);

            LLabel(s2, "ENEMIES", 8f, new Color(0f, 0.78f, 0.52f, 1f));
            LDivider(s2);

            var (enRow, enImg, enTMP) = LToggleRow(s2, "Show enemy names above health bar");
            _enemyNamesToggleImage = enImg;
            _enemyNamesToggleText  = enTMP;
            enRow.GetComponentInChildren<Button>().onClick.AddListener(OnEnemyNamesToggleClicked);
            RefreshEnemyNamesToggle();

            LGap(panel, 8f);

            // ── Section 3: Item Transfer ──────────────────────────────────
            var s3t = LSectionCard(panel);

            LLabel(s3t, "ITEM TRANSFER", 8f, new Color(0f, 0.78f, 0.52f, 1f));
            LDivider(s3t);

            var (trRow, trImg, trTMP) = LToggleRow(s3t, "Modifier + click to transfer items");
            _transferToggleImage = trImg;
            _transferToggleText  = trTMP;
            trRow.GetComponentInChildren<Button>().onClick.AddListener(OnTransferToggleClicked);
            RefreshTransferToggle();

            LLabel(s3t, "Modifier key", 10f, new Color(0.48f, 0.48f, 0.58f, 1f));

            var modRow = LChild(s3t, "ModRow", 34f);
            modRow.GetComponent<Image>().color = Color.clear;
            var modHLG = modRow.AddComponent<HorizontalLayoutGroup>();
            modHLG.spacing               = 5f;
            modHLG.childForceExpandWidth  = true;
            modHLG.childForceExpandHeight = true;

            var (btnSh, imgSh, lblSh) = LModeBtn(modRow, "Shift");
            var (btnAl, imgAl, lblAl) = LModeBtn(modRow, "Alt");
            _transferModBtnImages = new[] { imgSh, imgAl };
            _transferModBtnLabels = new[] { lblSh, lblAl };
            btnSh.onClick.AddListener(() => SetTransferModifier(TransferModifier.Shift));
            btnAl.onClick.AddListener(() => SetTransferModifier(TransferModifier.Alt));
            RefreshTransferModifierButtons();

            var warnGo = new GameObject("ShiftConflictWarn");
            warnGo.transform.SetParent(s3t.transform, false);
            warnGo.AddComponent<RectTransform>();
            var warnTMP = warnGo.AddComponent<TextMeshProUGUI>();
            warnTMP.text = "Shift is also set to close containers.\nSwitch transfer to Alt to avoid conflicts.";
            warnTMP.fontSize = 10f;
            warnTMP.color = new Color(1f, 0.75f, 0.2f, 1f);
            warnTMP.alignment = TextAlignmentOptions.Left;
            warnGo.AddComponent<LayoutElement>().preferredHeight = 28f;
            _shiftConflictWarning = warnGo;
            RefreshShiftConflict();

            LGap(panel, 8f);

            // ── Section 4: Auto-Close ─────────────────────────────────────
            var s4ac = LSectionCard(panel);

            LLabel(s4ac, "AUTO-CLOSE CONTAINER", 8f, new Color(0f, 0.78f, 0.52f, 1f));
            LDivider(s4ac);

            var acLabels = new[] { "Close on movement (W/A/S/D)", "Close on Shift", "Close on Space", "Close on damage" };
            _autoCloseBtnImages = new Image[4];
            _autoCloseBtnTexts  = new TextMeshProUGUI[4];
            for (int i = 0; i < 4; i++)
            {
                var idx = i;
                var (acRow, acImg, acTMP) = LToggleRow(s4ac, acLabels[i]);
                _autoCloseBtnImages[i] = acImg;
                _autoCloseBtnTexts[i]  = acTMP;
                acRow.GetComponentInChildren<Button>().onClick.AddListener(() => OnAutoCloseToggleClicked(idx));
            }
            RefreshAutoCloseToggles();

            LGap(panel, 8f);

            // ── Section 5: Factory Recorder ───────────────────────────────
            var s5fr = LSectionCard(panel);

            LLabel(s5fr, "FACTORY RECORDER", 8f, new Color(0f, 0.78f, 0.52f, 1f));
            LDivider(s5fr);

            var (frRow, frImg, frTMP) = LToggleRow(s5fr, "Show badge on recorded recipes & keys");
            _recorderToggleImage = frImg;
            _recorderToggleText  = frTMP;
            frRow.GetComponentInChildren<Button>().onClick.AddListener(OnRecorderBadgeToggleClicked);
            RefreshRecorderBadgeToggle();

            LGap(panel, 8f);

            // ── Section 6: Sleep Presets ──────────────────────────────────
            var s4 = LSectionCard(panel);

            LLabel(s4, "SLEEP PRESETS", 8f, new Color(0f, 0.78f, 0.52f, 1f));
            LDivider(s4);

            var (stRow, stImg, stTMP) = LToggleRow(s4, "Wake-up preset buttons");
            _sleepToggleImage = stImg;
            _sleepToggleText  = stTMP;
            stRow.GetComponentInChildren<Button>().onClick.AddListener(OnSleepToggleClicked);
            RefreshSleepToggle();

            LPickerRow(s4, "Preset 1",
                () => _preset1Hour,
                v => { _preset1Hour = v; PlayerPrefs.SetInt(PREF_PRESET1H, v); PlayerPrefs.Save();
                       if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}"; },
                () => _preset1Min,
                v => { _preset1Min  = v; PlayerPrefs.SetInt(PREF_PRESET1M, v); PlayerPrefs.Save();
                       if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}"; });

            LPickerRow(s4, "Preset 2",
                () => _preset2Hour,
                v => { _preset2Hour = v; PlayerPrefs.SetInt(PREF_PRESET2H, v); PlayerPrefs.Save();
                       if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}"; },
                () => _preset2Min,
                v => { _preset2Min  = v; PlayerPrefs.SetInt(PREF_PRESET2M, v); PlayerPrefs.Save();
                       if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}"; });

            LGap(panel, 10f);

            // ── Bottom bar ────────────────────────────────────────────────
            var bottom = LChild(panel, "Bottom", 46f);
            bottom.GetComponent<Image>().color = new Color(0.075f, 0.078f, 0.095f, 1f);
            var bHLG = bottom.AddComponent<HorizontalLayoutGroup>();
            bHLG.padding              = new RectOffset(14, 14, 8, 8);
            bHLG.spacing              = 10f;
            bHLG.childForceExpandHeight = true;
            bHLG.childForceExpandWidth  = false;

            var closeGo = new GameObject("CloseBtn");
            closeGo.transform.SetParent(bottom.transform, false);
            closeGo.AddComponent<RectTransform>();
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.color = new Color(0.46f, 0.08f, 0.08f, 1f);
            closeGo.AddComponent<LayoutElement>().preferredWidth = 110f;
            closeGo.AddComponent<Button>().onClick.AddListener(() => _settingsCanvas!.SetActive(false));
            var cTxtGo = new GameObject("T");
            cTxtGo.transform.SetParent(closeGo.transform, false);
            var cTMP = cTxtGo.AddComponent<TextMeshProUGUI>();
            cTMP.text = $"Close  [{MENU_KEY}]"; cTMP.fontSize = 12f;
            cTMP.alignment = TextAlignmentOptions.Center; cTMP.color = Color.white;
            var cTr = cTxtGo.GetComponent<RectTransform>();
            cTr.anchorMin = Vector2.zero; cTr.anchorMax = Vector2.one;
            cTr.sizeDelta = Vector2.zero; cTr.anchoredPosition = Vector2.zero;

            var hintGo = LText(bottom, "Hint", $"[{MENU_KEY}]  open / close", 9f, flexW: 1f);
            var hintTMP = hintGo.GetComponent<TextMeshProUGUI>();
            hintTMP.color = new Color(0.28f, 0.28f, 0.35f, 1f);
            hintTMP.alignment = TextAlignmentOptions.Right;

            _settingsCanvas.SetActive(false);
        }

        // ── Layout helpers ────────────────────────────────────────────────

        // Fixed-height child with Image (clear by default)
        private static GameObject LChild(GameObject parent, string name, float h)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = Color.clear;
            go.AddComponent<LayoutElement>().preferredHeight = h;
            return go;
        }

        // Invisible gap
        private static void LGap(GameObject parent, float h)
        {
            var go = new GameObject("Gap");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<LayoutElement>().preferredHeight = h;
        }

        // Section card — dark bg, auto-height VLG
        private static GameObject LSectionCard(GameObject parent)
        {
            var go = new GameObject("Section");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = new Color(0.090f, 0.095f, 0.118f, 1f);
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.padding             = new RectOffset(14, 14, 10, 12);
            vlg.spacing             = 7f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }

        // Text GO inside a layout group (optional preferred/flexible widths)
        private static GameObject LText(GameObject parent, string name, string text, float size,
            float prefW = -1f, float flexW = -1f)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size;
            if (prefW >= 0 || flexW >= 0)
            {
                var le = go.AddComponent<LayoutElement>();
                if (prefW >= 0) le.preferredWidth = prefW;
                if (flexW >= 0) le.flexibleWidth  = flexW;
            }
            return go;
        }

        // Small section label
        private static void LLabel(GameObject parent, string text, float size, Color color)
        {
            var go = new GameObject("Label");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = size;
            tmp.color = color; tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Left;
            go.AddComponent<LayoutElement>().preferredHeight = 14f;
        }

        // 1px divider line
        private static void LDivider(GameObject parent)
        {
            var go = new GameObject("Divider");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = new Color(0.20f, 0.22f, 0.28f, 1f);
            go.AddComponent<LayoutElement>().preferredHeight = 1f;
        }

        // Row: [label ──────────────────────] [ ON ]
        private static (GameObject row, Image pillImg, TextMeshProUGUI pillTMP)
            LToggleRow(GameObject parent, string labelText)
        {
            var row = new GameObject("ToggleRow");
            row.transform.SetParent(parent.transform, false);
            row.AddComponent<RectTransform>();
            row.AddComponent<Image>().color = Color.clear;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment       = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = false;
            hlg.spacing = 10f;
            row.AddComponent<LayoutElement>().preferredHeight = 34f;

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(row.transform, false);
            lbl.AddComponent<RectTransform>();
            var lblTMP = lbl.AddComponent<TextMeshProUGUI>();
            lblTMP.text = labelText; lblTMP.fontSize = 12f;
            lblTMP.alignment = TextAlignmentOptions.Left;
            lblTMP.color = new Color(0.82f, 0.82f, 0.88f, 1f);
            lbl.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var pill = new GameObject("Pill");
            pill.transform.SetParent(row.transform, false);
            pill.AddComponent<RectTransform>();
            var pillImg = pill.AddComponent<Image>();
            var pillBtn = pill.AddComponent<Button>();
            pillBtn.targetGraphic = pillImg;
            pill.AddComponent<LayoutElement>().preferredWidth = 54f;

            var pt = new GameObject("T");
            pt.transform.SetParent(pill.transform, false);
            var ptTMP = pt.AddComponent<TextMeshProUGUI>();
            ptTMP.fontSize = 12f; ptTMP.fontStyle = FontStyles.Bold;
            ptTMP.alignment = TextAlignmentOptions.Center;
            var ptr = pt.GetComponent<RectTransform>();
            ptr.anchorMin = Vector2.zero; ptr.anchorMax = Vector2.one;
            ptr.sizeDelta = Vector2.zero; ptr.anchoredPosition = Vector2.zero;

            return (row, pillImg, ptTMP);
        }

        // One of the 3 mode buttons
        private static (Button btn, Image img, TextMeshProUGUI lbl)
            LModeBtn(GameObject parent, string label)
        {
            var go = new GameObject($"Mode_{label}");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var img = go.AddComponent<Image>();
            var btn = go.AddComponent<Button>();
            btn.targetGraphic = img;

            var t = new GameObject("T");
            t.transform.SetParent(go.transform, false);
            var tmp = t.AddComponent<TextMeshProUGUI>();
            tmp.text = label; tmp.fontSize = 11f; tmp.alignment = TextAlignmentOptions.Center;
            var tr = t.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero; tr.anchoredPosition = Vector2.zero;

            return (btn, img, tmp);
        }

        // Row: [Preset N]  [−][HH][+] : [−][MM][+]
        private static void LPickerRow(GameObject parent, string label,
            Func<int> getH, Action<int> setH, Func<int> getM, Action<int> setM)
        {
            var row = new GameObject($"PickerRow_{label}");
            row.transform.SetParent(parent.transform, false);
            row.AddComponent<RectTransform>();
            row.AddComponent<Image>().color = Color.clear;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment       = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth  = false;
            hlg.spacing = 10f;
            row.AddComponent<LayoutElement>().preferredHeight = 32f;

            var lbl = new GameObject("Label");
            lbl.transform.SetParent(row.transform, false);
            lbl.AddComponent<RectTransform>();
            var lblTMP = lbl.AddComponent<TextMeshProUGUI>();
            lblTMP.text = label; lblTMP.fontSize = 11f;
            lblTMP.alignment = TextAlignmentOptions.Left;
            lblTMP.color = new Color(0.52f, 0.52f, 0.62f, 1f);
            lbl.AddComponent<LayoutElement>().preferredWidth = 58f;

            var picker = new GameObject("Picker");
            picker.transform.SetParent(row.transform, false);
            picker.AddComponent<RectTransform>();
            picker.AddComponent<LayoutElement>().flexibleWidth = 1f;
            BuildTimePicker(picker, getH, setH, getM, setM);
        }

        // ── Settings callbacks ────────────────────────────────────────────

        private void OnToggleClicked()
        {
            _showValue = !_showValue;
            PlayerPrefs.SetInt(PREF_ENABLED, _showValue ? 1 : 0);
            PlayerPrefs.Save();
            RefreshToggleButton();
        }

        private void SetMode(DisplayMode mode)
        {
            _mode = mode;
            PlayerPrefs.SetInt(PREF_MODE, (int)_mode);
            PlayerPrefs.Save();
            RefreshModeButtons();
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
                ? new Color(0.10f, 0.48f, 0.10f, 1f)
                : new Color(0.48f, 0.10f, 0.10f, 1f);
            _toggleBtnText!.text  = _showValue ? "ON" : "OFF";
            _toggleBtnText!.color = Color.white;
        }

        private void RefreshModeButtons()
        {
            var modes = new[] { DisplayMode.SingleOnly, DisplayMode.Combined, DisplayMode.StackOnly };
            for (int i = 0; i < 3; i++)
            {
                bool active = modes[i] == _mode;
                _modeBtnImages![i].color = active
                    ? new Color(0.04f, 0.42f, 0.28f, 1f)
                    : new Color(0.11f, 0.115f, 0.15f, 1f);
                _modeBtnLabels![i].color = active
                    ? Color.white
                    : new Color(0.40f, 0.40f, 0.50f, 1f);
                _modeBtnLabels![i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private void RefreshSleepToggle()
        {
            _sleepToggleImage!.color = _sleepPresetsEnabled
                ? new Color(0.10f, 0.48f, 0.10f, 1f)
                : new Color(0.48f, 0.10f, 0.10f, 1f);
            _sleepToggleText!.text  = _sleepPresetsEnabled ? "ON" : "OFF";
            _sleepToggleText!.color = Color.white;
        }

        private void OnEnemyNamesToggleClicked()
        {
            _showEnemyNames = !_showEnemyNames;
            PlayerPrefs.SetInt(PREF_ENEMY_NAMES, _showEnemyNames ? 1 : 0);
            PlayerPrefs.Save();
            RefreshEnemyNamesToggle();
        }

        private void RefreshEnemyNamesToggle()
        {
            _enemyNamesToggleImage!.color = _showEnemyNames
                ? new Color(0.10f, 0.48f, 0.10f, 1f)
                : new Color(0.48f, 0.10f, 0.10f, 1f);
            _enemyNamesToggleText!.text  = _showEnemyNames ? "ON" : "OFF";
            _enemyNamesToggleText!.color = Color.white;
        }

        private void OnTransferToggleClicked()
        {
            _transferEnabled = !_transferEnabled;
            PlayerPrefs.SetInt(PREF_TRANSFER_ENABLED, _transferEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshTransferToggle();
            RefreshShiftConflict();
        }

        private void RefreshTransferToggle()
        {
            _transferToggleImage!.color = _transferEnabled
                ? new Color(0.10f, 0.48f, 0.10f, 1f)
                : new Color(0.48f, 0.10f, 0.10f, 1f);
            _transferToggleText!.text  = _transferEnabled ? "ON" : "OFF";
            _transferToggleText!.color = Color.white;
        }

        private void SetTransferModifier(TransferModifier mod)
        {
            _transferModifier = mod;
            PlayerPrefs.SetInt(PREF_TRANSFER_MOD, (int)mod);
            PlayerPrefs.Save();
            RefreshTransferModifierButtons();
        }

        private void RefreshTransferModifierButtons()
        {
            var mods = new[] { TransferModifier.Shift, TransferModifier.Alt };
            for (int i = 0; i < 2; i++)
            {
                bool active = mods[i] == _transferModifier;
                _transferModBtnImages![i].color = active
                    ? new Color(0.04f, 0.42f, 0.28f, 1f)
                    : new Color(0.11f, 0.115f, 0.15f, 1f);
                _transferModBtnLabels![i].color = active ? Color.white : new Color(0.40f, 0.40f, 0.50f, 1f);
                _transferModBtnLabels![i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
            RefreshShiftConflict();
        }

        private void RefreshShiftConflict()
        {
            bool conflict = _transferEnabled
                         && _transferModifier == TransferModifier.Shift
                         && _autoCloseOnShift;
            if (_shiftConflictWarning != null)
                _shiftConflictWarning.SetActive(conflict);
        }

        private void OnAutoCloseToggleClicked(int index)
        {
            switch (index)
            {
                case 0: _autoCloseOnWASD   = !_autoCloseOnWASD;   PlayerPrefs.SetInt(PREF_AC_WASD,   _autoCloseOnWASD   ? 1 : 0); break;
                case 1: _autoCloseOnShift  = !_autoCloseOnShift;  PlayerPrefs.SetInt(PREF_AC_SHIFT,  _autoCloseOnShift  ? 1 : 0); break;
                case 2: _autoCloseOnSpace  = !_autoCloseOnSpace;  PlayerPrefs.SetInt(PREF_AC_SPACE,  _autoCloseOnSpace  ? 1 : 0); break;
                case 3: _autoCloseOnDamage = !_autoCloseOnDamage; PlayerPrefs.SetInt(PREF_AC_DAMAGE, _autoCloseOnDamage ? 1 : 0); break;
            }
            PlayerPrefs.Save();
            RefreshAutoCloseToggles();
            RefreshShiftConflict();
        }

        private void RefreshAutoCloseToggles()
        {
            var states = new[] { _autoCloseOnWASD, _autoCloseOnShift, _autoCloseOnSpace, _autoCloseOnDamage };
            for (int i = 0; i < 4; i++)
            {
                _autoCloseBtnImages![i].color = states[i]
                    ? new Color(0.10f, 0.48f, 0.10f, 1f)
                    : new Color(0.48f, 0.10f, 0.10f, 1f);
                _autoCloseBtnTexts![i].text  = states[i] ? "ON" : "OFF";
                _autoCloseBtnTexts![i].color = Color.white;
            }
        }

        private void OnRecorderBadgeToggleClicked()
        {
            _showRecorderBadge = !_showRecorderBadge;
            PlayerPrefs.SetInt(PREF_RECORDER_BADGE, _showRecorderBadge ? 1 : 0);
            PlayerPrefs.Save();
            RefreshRecorderBadgeToggle();
            if (!_showRecorderBadge)
            {
                foreach (var kvp in _slotBadges)
                    if (kvp.Value != null) kvp.Value.SetActive(false);
            }
        }

        private void RefreshRecorderBadgeToggle()
        {
            _recorderToggleImage!.color = _showRecorderBadge
                ? new Color(0.10f, 0.48f, 0.10f, 1f)
                : new Color(0.48f, 0.10f, 0.10f, 1f);
            _recorderToggleText!.text  = _showRecorderBadge ? "ON" : "OFF";
            _recorderToggleText!.color = Color.white;
        }

        // ── Item Hover UI ─────────────────────────────────────────────────

        private void OnSetupMeta(ItemHoveringUI ui, ItemMetaData data)
        {
            _lastHoveredItem = null;
            ValueText.gameObject.SetActive(false);
        }

        private void OnSetupItemHoveringUI(ItemHoveringUI uiInstance, Item item)
        {
            _lastHoveredItem = item;

            // Use hover event to discover slot component type (most reliable method)
            if (_showRecorderBadge && _slotCompType == null && item != null)
                TryCacheSlotTypeFromHover(item);

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

        // ── Time Picker ───────────────────────────────────────────────────

        private static void BuildTimePicker(
            GameObject parent,
            Func<int> getH, Action<int> setH,
            Func<int> getM, Action<int> setM)
        {
            var layout = parent.AddComponent<HorizontalLayoutGroup>();
            layout.childAlignment       = TextAnchor.MiddleCenter;
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
            img.color = new Color(0.16f, 0.22f, 0.38f, 1f);
            var btn = go.AddComponent<Button>();
            var c   = btn.colors;
            c.highlightedColor = new Color(0.24f, 0.36f, 0.58f, 1f);
            c.pressedColor     = new Color(0.08f, 0.12f, 0.22f, 1f);
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
            go.AddComponent<Image>().color = new Color(0.06f, 0.065f, 0.085f, 1f);
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
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = ":"; tmp.fontSize = 14; tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = new Color(0.5f, 0.5f, 0.6f, 1f);
            go.AddComponent<LayoutElement>().preferredWidth = 12;
            return go;
        }
    }
}
