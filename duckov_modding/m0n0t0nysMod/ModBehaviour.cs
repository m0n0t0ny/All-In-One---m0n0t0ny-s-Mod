using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Duckov.UI;
using Duckov.Utilities;
using Duckov.Weathers;
using EPOOutline;
using ItemStatsSystem;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace AllInOneMod_m0n0t0ny
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
        private const string PREF_PRESET2M       = "DisplayItemValue_Preset2M";
        private const string PREF_PRESET3H       = "DisplayItemValue_Preset3H";
        private const string PREF_PRESET3M       = "DisplayItemValue_Preset3M";
        private const string PREF_PRESET4H       = "DisplayItemValue_Preset4H";
        private const string PREF_PRESET4M       = "DisplayItemValue_Preset4M";
        private const string PREF_ENEMY_NAMES       = "DisplayItemValue_EnemyNames";
        private const string PREF_TRANSFER_ENABLED  = "DisplayItemValue_TransferEnabled";
        private const string PREF_TRANSFER_MOD      = "DisplayItemValue_TransferMod";
        private const string PREF_AC_WASD            = "DisplayItemValue_ACWasd";
        private const string PREF_AC_SHIFT           = "DisplayItemValue_ACShift";
        private const string PREF_AC_SPACE           = "DisplayItemValue_ACSpace";
        private const string PREF_AC_DAMAGE          = "DisplayItemValue_ACDamage";
        private const string PREF_FPS_COUNTER        = "DisplayItemValue_FpsCounter";
        private const string PREF_SKIP_MELEE         = "DisplayItemValue_SkipMelee";
        private const KeyCode MENU_KEY               = KeyCode.F9;
        private const string MC_MOD_NAME             = "All In One - m0n0t0ny's Mod";

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
        private RectTransform? _transferToggleThumb;
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
        private RectTransform[]? _autoCloseBtnThumbs;
        private Component? _playerHealthComp;
        private PropertyInfo? _playerHealthValueProp;
        private float _playerHealthPrev = float.MaxValue;
        private float _damageInitTimer;

        // ── LootView cache (shared by AutoUnload + AutoClose + Transfer) ──
        private LootView? _cachedLootView;
        private float     _lootViewCacheTimer; // counts down; refresh when <= 0
        // Refreshed ONCE per frame in Update() — not per-caller.

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
        private Canvas?     _settingsCanvasComp;
        private Image? _toggleBtnImage;
        private RectTransform? _toggleBtnThumb;
        private Image[]? _modeBtnImages;
        private TextMeshProUGUI[]? _modeBtnLabels;
        private Image? _enemyNamesToggleImage;
        private RectTransform? _enemyNamesToggleThumb;
        private Image? _sleepToggleImage;
        private RectTransform? _sleepToggleThumb;

        // ── Sleep preset state ────────────────────────────────────────────
        private bool _sleepPresetsEnabled;
        private int  _preset1Hour, _preset1Min;
        private int  _preset2Hour, _preset2Min;
        private int  _preset3Hour, _preset3Min;
        private int  _preset4Hour, _preset4Min;
        private SleepView? _sleepViewInstance;
        private bool _sleepPresetsInjected;
        private TextMeshProUGUI? _preset1BtnLabel;
        private TextMeshProUGUI? _preset2BtnLabel;
        private TextMeshProUGUI? _preset3BtnLabel;
        private TextMeshProUGUI? _preset4BtnLabel;

        // ── FPS counter ───────────────────────────────────────────────────
        private bool _showFps;
        private GameObject? _fpsCanvas;
        private TextMeshProUGUI? _fpsTMP;
        private float _fpsDeltaAccum;
        private int _fpsFrameCount;
        private float _fpsValue;
        private Image? _fpsToggleImage;
        private RectTransform? _fpsToggleThumb;

        // ── Skip melee on scroll ──────────────────────────────────────────
        private bool _skipMeleeOnScroll;
        private bool _scrollDetectedThisFrame;
        private int  _lastScrollDir;
        private Image? _skipMeleeToggleImage;
        private RectTransform? _skipMeleeToggleThumb;
        private CharacterMainControl? _playerCtrl;

        // ── Auto-unload enemy weapons on loot-open ────────────────────────
        private const string PREF_AUTO_UNLOAD = "DisplayItemValue_AutoUnload";
        private bool _autoUnloadEnabled;
        private Image? _autoUnloadToggleImage;
        private RectTransform? _autoUnloadToggleThumb;
        private int _lastAutoUnloadInvId;
        private static PropertyInfo? _itemPlugsProp;
        private static FieldInfo?    _itemPlugsField;
        private static bool          _itemPlugsSearched;
        private static PropertyInfo? _stackCountProp;
        private static bool          _stackCountSearched;

        // ── ModConfig integration (optional) ─────────────────────────────
        private static Type?    _mcAPI;
        private bool            _mcChecked;
        private Action<string>? _mcDelegate;

        // ── Factory Recorder badge ────────────────────────────────────────
        private const string PREF_RECORDER_BADGE = "DisplayItemValue_RecorderBadge";
        private bool _showRecorderBadge;
        private Image? _recorderToggleImage;
        private RectTransform? _recorderToggleThumb;
        // ItemUtilities.IsRegistered(Item) → bool  (static helper used by the game)
        private static MethodInfo? _isRegisteredMethod;
        // Slot badge overlay tracking
        private static Type?       _slotCompType;
        private static MemberInfo? _slotItemMember; // PropertyInfo or FieldInfo → Item
        private static readonly Dictionary<Type, MemberInfo?> _typeItemMemberCache = new Dictionary<Type, MemberInfo?>();
        private float _badgeScanTimer;
        private readonly Dictionary<int, GameObject> _slotBadges = new Dictionary<int, GameObject>();

        // ── Lootbox Highlight ─────────────────────────────────────────────
        private const string PREF_LOOTBOX_HL            = "DisplayItemValue_LootboxHL";
        private const string PREF_LOOTBOX_HL_UNSEARCHED = "DisplayItemValue_LootboxHLUnsearched";
        private bool _lootboxHLEnabled;
        private bool _lootboxHLOnlyUnsearched;
        private Image? _lootboxHLToggleImage;
        private RectTransform? _lootboxHLToggleThumb;
        private Image? _lootboxHLUnsearchedToggleImage;
        private RectTransform? _lootboxHLUnsearchedToggleThumb;
        // key = GameObject instanceID
        private readonly Dictionary<int, Outlinable> _lootboxOutlines = new Dictionary<int, Outlinable>();
        private float _lootboxScanTimer;
        private float _lootboxUpdateTimer;
        private static Type?      _lbType;              // InteractableLootbox
        private static Type?      _imType;              // InteractMarker
        private static FieldInfo? _imMarkedAsUsed;      // InteractMarker.markedAsUsed
        private static FieldInfo? _invInspectedField;   // Inventory.hasBeenInspectedInLootBox
        private static bool       _lbCached;

        // ── Kill Feed ─────────────────────────────────────────────────────
        private const string PREF_KILL_FEED = "DisplayItemValue_KillFeed";
        private bool _killFeedEnabled;
        private Image? _killFeedToggleImage;
        private RectTransform? _killFeedToggleThumb;
        private GameObject? _killFeedCanvas;
        private GameObject? _killFeedContainer;
        private readonly List<KfEntry> _kfEntries = new List<KfEntry>();
        private const int   KF_MAX     = 5;
        private const float KF_DISPLAY = 5f;
        private const float KF_FADE    = 0.5f;

        private sealed class KfEntry
        {
            public readonly GameObject  Go;
            public readonly CanvasGroup Group;
            public float Timer;
            public KfEntry(GameObject go, CanvasGroup group, float timer)
            { Go = go; Group = group; Timer = timer; }
        }

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
            _preset3Hour         = PlayerPrefs.GetInt(PREF_PRESET3H,  8);
            _preset3Min          = PlayerPrefs.GetInt(PREF_PRESET3M,  0);
            _preset4Hour         = PlayerPrefs.GetInt(PREF_PRESET4H, 12);
            _preset4Min          = PlayerPrefs.GetInt(PREF_PRESET4M,  0);
            _showRecorderBadge   = PlayerPrefs.GetInt(PREF_RECORDER_BADGE, 1) == 1;
            _showFps             = PlayerPrefs.GetInt(PREF_FPS_COUNTER,    0) == 1;
            _skipMeleeOnScroll   = PlayerPrefs.GetInt(PREF_SKIP_MELEE,     1) == 1;
            _autoUnloadEnabled       = PlayerPrefs.GetInt(PREF_AUTO_UNLOAD,         1) == 1;
            _lootboxHLEnabled        = PlayerPrefs.GetInt(PREF_LOOTBOX_HL,          1) == 1;
            _lootboxHLOnlyUnsearched = PlayerPrefs.GetInt(PREF_LOOTBOX_HL_UNSEARCHED, 0) == 1;
            _killFeedEnabled         = PlayerPrefs.GetInt(PREF_KILL_FEED,             1) == 1;
            CacheRecorderReflection();
            EnsureLootboxTypes();
            BuildSettingsPanel();
            TryInitModConfig();
        }

        private CursorLockMode _prevLockMode;
        private bool           _prevCursorVisible;
        private bool           _menuOpen;
        private static readonly Type? _inputControlType =
            AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Array.Empty<Type>(); } })
                .FirstOrDefault(t => t.Name == "CharacterInputControl");
        private readonly List<Behaviour> _disabledInputControls = new List<Behaviour>();

        private void SetMenuVisible(bool open)
        {
            _menuOpen = open;
            _settingsCanvas!.SetActive(open);
            if (_settingsCanvasComp != null) _settingsCanvasComp.enabled = open;

            if (open)
            {
                _prevLockMode      = Cursor.lockState;
                _prevCursorVisible = Cursor.visible;
                Cursor.lockState   = CursorLockMode.None;
                Cursor.visible     = true;

                _disabledInputControls.Clear();
                if (_inputControlType != null)
                {
                    foreach (UnityEngine.Object obj in FindObjectsOfType(_inputControlType))
                    {
                        if (obj is Behaviour b && b.enabled)
                        {
                            b.enabled = false;
                            _disabledInputControls.Add(b);
                        }
                    }
                }
            }
            else
            {
                Cursor.lockState = _prevLockMode;
                Cursor.visible   = _prevCursorVisible;

                foreach (var b in _disabledInputControls)
                    if (b != null) b.enabled = true;
                _disabledInputControls.Clear();
            }
        }

        void OnDestroy()
        {
            if (_mcDelegate != null && _mcAPI != null)
            {
                try
                {
                    var rm = _mcAPI.GetMethod("SafeRemoveOnOptionsChangedDelegate",
                        BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(Action<string>) }, null);
                    rm?.Invoke(null, new object[] { _mcDelegate });
                }
                catch { }
                _mcDelegate = null;
            }
            if (_valueText != null) Destroy(_valueText.gameObject);
            if (_settingsCanvas != null) Destroy(_settingsCanvas);
            if (_fpsCanvas != null) Destroy(_fpsCanvas);
            if (_killFeedCanvas != null) Destroy(_killFeedCanvas);
            ClearLootboxOutlines();
            foreach (var kvp in _slotBadges)
                if (kvp.Value != null) Destroy(kvp.Value);
            _slotBadges.Clear();
            ClearKillFeedSubscriptions();
        }

        private void EnsureFpsCanvas()
        {
            if (_fpsCanvas != null) return;

            _fpsCanvas = new GameObject("FpsCounter");
            DontDestroyOnLoad(_fpsCanvas);
            var canvas = _fpsCanvas.AddComponent<Canvas>();
            canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _fpsCanvas.AddComponent<CanvasScaler>();
            _fpsCanvas.AddComponent<GraphicRaycaster>();

            var go = new GameObject("FpsText");
            go.transform.SetParent(_fpsCanvas.transform, false);
            _fpsTMP = go.AddComponent<TextMeshProUGUI>();
            _fpsTMP.fontSize  = 14f;
            _fpsTMP.color     = Color.white;
            _fpsTMP.fontStyle = FontStyles.Bold;
            _fpsTMP.alignment = TextAlignmentOptions.TopRight;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -10f);
            rt.sizeDelta        = new Vector2(100f, 30f);

            // Shadow for readability
            var shadow = go.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
            SceneManager.sceneLoaded   += OnSceneLoaded;
            Health.OnDead              += OnKillFeedDeadDirect;
        }

        void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
            SceneManager.sceneLoaded   -= OnSceneLoaded;
            Health.OnDead              -= OnKillFeedDeadDirect;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearLootboxOutlines();
            ClearKillFeedSubscriptions();
        }

        void Update()
        {
            if (!_mcChecked) TryInitModConfig();

            // If the game externally hides our canvas (SetActive or Canvas.enabled), restore it
            if (_menuOpen && _settingsCanvas != null)
            {
                if (!_settingsCanvas.activeSelf) _settingsCanvas.SetActive(true);
                if (_settingsCanvasComp != null && !_settingsCanvasComp.enabled) _settingsCanvasComp.enabled = true;
            }

            if (_settingsCanvas != null && _settingsCanvas.activeSelf)
            {
                if (Cursor.lockState != CursorLockMode.None) Cursor.lockState = CursorLockMode.None;
                if (!Cursor.visible) Cursor.visible = true;
            }

            _scrollDetectedThisFrame = false;
            if (_skipMeleeOnScroll)
            {
                float scroll = Input.GetAxisRaw("Mouse ScrollWheel");
                if (scroll != 0f)
                {
                    _scrollDetectedThisFrame = true;
                    _lastScrollDir = scroll > 0f ? 1 : -1;
                }
            }

            if (Input.GetKeyDown(MENU_KEY))
                SetMenuVisible(!_menuOpen);

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
                    _nameUpdateTimer = 0.5f;
                    UpdateEnemyNameBars();
                }
            }

            // Resolve active LootView ONCE per frame — shared by AutoClose + AutoUnload.
            // FindObjectOfType is throttled to max once every 0.2s via _lootViewCacheTimer.
            LootView? activeLootView = null;
            bool needsLootView = (_autoCloseOnWASD || _autoCloseOnShift || _autoCloseOnSpace || _autoCloseOnDamage) || _autoUnloadEnabled;
            if (needsLootView)
            {
                _lootViewCacheTimer -= Time.deltaTime;
                if (_lootViewCacheTimer <= 0f)
                {
                    _lootViewCacheTimer = 0.2f;
                    _cachedLootView = LootView.Instance ?? FindObjectOfType<LootView>();
                }
                activeLootView = (_cachedLootView != null && _cachedLootView.gameObject.activeInHierarchy)
                    ? _cachedLootView : null;
            }

            if (_autoCloseOnWASD || _autoCloseOnShift || _autoCloseOnSpace || _autoCloseOnDamage)
                CheckAutoCloseContainer(activeLootView);

            if (_showRecorderBadge)
            {
                _badgeScanTimer -= Time.deltaTime;
                if (_badgeScanTimer <= 0f)
                {
                    _badgeScanTimer = 1.0f;
                    ScanAndBadgeSlots();
                }
            }

            if (_autoUnloadEnabled)
                TryAutoUnloadLoot(activeLootView);

            if (_lootboxHLEnabled && LevelManager.Instance != null && LevelManager.Instance.IsRaidMap)
                UpdateLootboxHighlight();

            if (_killFeedEnabled)
                UpdateKillFeedEntries();

            if (_showFps)
            {
                _fpsDeltaAccum += Time.unscaledDeltaTime;
                _fpsFrameCount++;
                if (_fpsDeltaAccum >= 0.5f)
                {
                    _fpsValue      = _fpsFrameCount / _fpsDeltaAccum;
                    _fpsDeltaAccum = 0f;
                    _fpsFrameCount = 0;
                    EnsureFpsCanvas();
                    _fpsTMP!.text = $"{Mathf.RoundToInt(_fpsValue)} FPS";
                }
            }
        }

        void LateUpdate()
        {
            // Snapshot the hovered item at the END of every frame so that
            // TryShiftClickTransfer() in the NEXT frame's Update() sees a
            // stable value even if EventSystem clears the hover mid-frame.
            _transferCachedItem = _lastHoveredItem;

            // Skip melee during scroll — the game has already switched weapons by LateUpdate,
            // so if we ended up on melee we call SwitchWeapon once more to skip past it.
            if (_skipMeleeOnScroll && _scrollDetectedThisFrame)
            {
                var ctrl = _playerCtrl;
                if (ctrl == null)
                {
                    foreach (var ch in FindObjectsOfType<CharacterMainControl>())
                    { if (ch.IsMainCharacter) { _playerCtrl = ch; ctrl = ch; break; } }
                }
                if (ctrl != null && ctrl.GetGun() == null && ctrl.GetMeleeWeapon() != null)
                    ctrl.SwitchWeapon(_lastScrollDir);
            }
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

        // ── Auto-unload enemy weapons ─────────────────────────────────────
        // Triggered when the player opens loot on an enemy — zero polling overhead.
        // Scans items in the loot inventory and detaches any plugged sub-items
        // (ammo, magazines) directly into that same inventory.

        private void TryAutoUnloadLoot(LootView? lv)
        {
            if (lv == null) return;

            var lootInvDisplay = _lootTargetInvField?.GetValue(lv) as InventoryDisplay;
            var lootInv = _invDisplayTargetProp?.GetValue(lootInvDisplay) as Inventory;
            if (lootInv == null) return;

            int invId = lootInv.GetInstanceID();
            if (invId == _lastAutoUnloadInvId) return; // Already processed this loot session
            _lastAutoUnloadInvId = invId;

            var items = lootInv.Content?.ToList();
            if (items == null) return;

            foreach (var item in items)
            {
                if (item == null) continue;
                var plugs = GetItemPlugs(item);
                if (plugs == null || plugs.Count == 0) continue;
                foreach (var plug in plugs)
                {
                    if (plug == null) continue;
                    try
                    {
                        plug.Detach();
                        if (!TryMergeStack(plug, lootInv))
                            lootInv.AddItem(plug);
                    }
                    catch { }
                }
            }
        }

        // Merges plug into an existing same-type stack in the inventory.
        // Returns true if merged (plug is consumed); false if AddItem should be called instead.
        private static bool TryMergeStack(Item plug, Inventory inv)
        {
            if (!_stackCountSearched)
            {
                _stackCountSearched = true;
                var p = typeof(Item).GetProperty("StackCount", BindingFlags.Public | BindingFlags.Instance);
                if (p != null && p.CanWrite) _stackCountProp = p;
            }
            if (_stackCountProp == null) return false;

            int plugCount;
            try { plugCount = (int)(_stackCountProp.GetValue(plug) ?? 0); }
            catch { return false; }
            if (plugCount <= 0) return false;

            var target = inv.Content?.FirstOrDefault(i => i != null && i != plug && i.GetType() == plug.GetType());
            if (target == null) return false;

            try
            {
                int existingCount = (int)(_stackCountProp.GetValue(target) ?? 0);
                _stackCountProp.SetValue(target, existingCount + plugCount);
                return true;
            }
            catch { return false; }
        }

        // Discovers which field/property on Item holds plugged sub-items (ammo, mods).
        // Scans by common name first, then by type. Result is cached after first call.
        private static List<Item> GetItemPlugs(Item item)
        {
            if (!_itemPlugsSearched)
            {
                _itemPlugsSearched = true;
                var type = typeof(Item);
                var names = new[] {
                    "plugs","Plugs","_plugs",
                    "parts","Parts","_parts",
                    "mods","Mods","_mods",
                    "modules","Modules","_modules",
                    "subItems","SubItems","_subItems",
                    "children","Children","_children",
                    "attachments","Attachments","_attachments",
                    "slots","Slots","_slots",
                    "items","Items","_items"
                };
                foreach (var name in names)
                {
                    var f = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (f != null && IsItemCollectionType(f.FieldType)) { _itemPlugsField = f; break; }
                    var p = type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (p != null && IsItemCollectionType(p.PropertyType)) { _itemPlugsProp = p; break; }
                }
                // Fallback: scan all fields and properties by type
                if (_itemPlugsField == null && _itemPlugsProp == null)
                {
                    foreach (var f in type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        if (IsItemCollectionType(f.FieldType)) { _itemPlugsField = f; break; }
                    if (_itemPlugsField == null)
                        foreach (var p in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                            if (IsItemCollectionType(p.PropertyType)) { _itemPlugsProp = p; break; }
                }
            }

            var results = new List<Item>();
            object? raw = null;
            try
            {
                if (_itemPlugsField != null) raw = _itemPlugsField.GetValue(item);
                else if (_itemPlugsProp != null) raw = _itemPlugsProp.GetValue(item);
            }
            catch { return results; }

            if (raw == null) return results;
            if (raw is IEnumerable<Item> typed)
                results.AddRange(typed.Where(i => i != null));
            else if (raw is System.Collections.IEnumerable untyped)
                foreach (var obj in untyped)
                    if (obj is Item i) results.Add(i);
            return results;
        }

        private static bool IsItemCollectionType(Type t)
        {
            if (t == typeof(string)) return false;
            if (typeof(IEnumerable<Item>).IsAssignableFrom(t)) return true;
            if (t.IsArray && t.GetElementType() == typeof(Item)) return true;
            if (t.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(t))
            {
                var args = t.GetGenericArguments();
                return args.Length == 1 && args[0] == typeof(Item);
            }
            return false;
        }

        // ── Lootbox Highlight ─────────────────────────────────────────────

        private static void EnsureLootboxTypes()
        {
            if (_lbCached) return;
            _lbCached = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    foreach (var t in asm.GetTypes())
                    {
                        if (_lbType == null && t.Name == "InteractableLootbox" && typeof(Component).IsAssignableFrom(t))
                            _lbType = t;
                        if (_imType == null && t.Name == "InteractMarker" && typeof(Component).IsAssignableFrom(t))
                        {
                            _imType = t;
                            _imMarkedAsUsed = t.GetField("markedAsUsed",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        if (_invInspectedField == null && t.Name == "Inventory")
                        {
                            _invInspectedField = t.GetField("hasBeenInspectedInLootBox",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        }
                        if (_lbType != null && _imType != null && _invInspectedField != null) return;
                    }
                }
                catch { }
            }
        }

        private void UpdateLootboxHighlight()
        {
            _lootboxScanTimer -= Time.deltaTime;
            if (_lootboxScanTimer <= 0f)
            {
                _lootboxScanTimer = 5f;
                ScanLootboxes();
            }

            _lootboxUpdateTimer -= Time.deltaTime;
            if (_lootboxUpdateTimer <= 0f)
            {
                _lootboxUpdateTimer = 0.15f;
                RefreshLootboxOutlines();
            }
        }

        private void ScanLootboxes()
        {
            EnsureLootboxTypes();
            if (_lbType == null) return;

            foreach (UnityEngine.Object lb in FindObjectsOfType(_lbType))
            {
                var go = (lb as Component)?.gameObject;
                if (go == null) continue;
                int id = go.GetInstanceID();
                if (_lootboxOutlines.ContainsKey(id)) continue;

                var ol = go.GetComponent<Outlinable>() ?? go.AddComponent<Outlinable>();
                ol.AddAllChildRenderersToRenderingList();
                try { ol.OutlineParameters.Color = new Color(1f, 0.75f, 0f, 1f); } catch { }
                ol.enabled = true;
                _lootboxOutlines[id] = ol;
            }
        }

        private void RefreshLootboxOutlines()
        {
            var toRemove = new List<int>();
            foreach (var kvp in _lootboxOutlines)
            {
                var ol = kvp.Value;
                if (ol == null) { toRemove.Add(kvp.Key); continue; }
                var go = ol.gameObject;
                if (go == null) { toRemove.Add(kvp.Key); continue; }

                bool show = go.activeInHierarchy;
                if (show && _lootboxHLOnlyUnsearched)
                {
                    bool searched = false;
                    // 1. InteractMarker.markedAsUsed (may be on a child GO)
                    if (_imType != null && _imMarkedAsUsed != null)
                    {
                        var marker = go.GetComponentInChildren(_imType);
                        if (marker != null)
                            try { searched = (bool)(_imMarkedAsUsed.GetValue(marker) ?? false); } catch { }
                    }
                    // 2. Fallback: Inventory.hasBeenInspectedInLootBox
                    if (!searched && _invInspectedField != null)
                    {
                        var inv = go.GetComponentInChildren<Inventory>();
                        if (inv != null)
                            try { searched = (bool)(_invInspectedField.GetValue(inv) ?? false); } catch { }
                    }
                    show = !searched;
                }
                ol.enabled = show;
            }
            foreach (var k in toRemove) _lootboxOutlines.Remove(k);
        }

        private void ClearLootboxOutlines()
        {
            foreach (var ol in _lootboxOutlines.Values)
            {
                if (ol == null) continue;
                ol.enabled = false;
                try { Destroy(ol); } catch { }
            }
            _lootboxOutlines.Clear();
            _lootboxScanTimer  = 0f;
            _lootboxUpdateTimer = 0f;
        }

        // ── Shift-click transfer ──────────────────────────────────────────

        private void TryShiftClickTransfer()
        {
            var lv = (_cachedLootView != null && _cachedLootView.gameObject.activeInHierarchy) ? _cachedLootView : null;
            bool lvActive = lv != null;
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

        private void CheckAutoCloseContainer(LootView? lv)
        {
            if (lv == null) return;

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
                _preset3BtnLabel      = null;
                _preset4BtnLabel      = null;
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
                return;
            }

            var sleepRect  = sleepBtn.GetComponent<RectTransform>();
            var origParent = sleepRect.parent;
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
            _preset1BtnLabel = AddGridBtn(row1, sv, $"{_preset1Hour:D2}:{_preset1Min:D2}", () => MinutesUntilTime(_preset1Hour, _preset1Min), sleepBtn);
            _preset2BtnLabel = AddGridBtn(row1, sv, $"{_preset2Hour:D2}:{_preset2Min:D2}", () => MinutesUntilTime(_preset2Hour, _preset2Min), sleepBtn);
            _preset3BtnLabel = AddGridBtn(row1, sv, $"{_preset3Hour:D2}:{_preset3Min:D2}", () => MinutesUntilTime(_preset3Hour, _preset3Min), sleepBtn);
            _preset4BtnLabel = AddGridBtn(row1, sv, $"{_preset4Hour:D2}:{_preset4Min:D2}", () => MinutesUntilTime(_preset4Hour, _preset4Min), sleepBtn);

            var row2 = MakePresetRow(presetGrid);
            AddGridBtn(row2, sv, "Rain",       () => MinutesUntilRain(),     sleepBtn);
            AddGridBtn(row2, sv, "Storm I",    () => MinutesUntilStorm(1),   sleepBtn);
            AddGridBtn(row2, sv, "Storm II",   () => MinutesUntilStorm(2),   sleepBtn);
            AddGridBtn(row2, sv, "Post-Storm", () => MinutesUntilStormEnd(), sleepBtn);
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

        private static TextMeshProUGUI AddGridBtn(GameObject row, SleepView sv, string label, Func<float?> getMinutes, Button? template)
        {
            // The Sleep button uses ProceduralImage (game custom component) for its background,
            // not a standard Unity Image, so we must Instantiate to preserve that visual style.
            // The moon icon is identified by its sprite name ("bedtime") and hidden.
            GameObject go;
            Button btn;
            TextMeshProUGUI? tmp = null;

            if (template != null)
            {
                go = UnityEngine.Object.Instantiate(template.gameObject);
                go.name = $"P_{label}";
                go.transform.SetParent(row.transform, false);

                btn = go.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();

                // Hide moon icon (identified by its "bedtime" sprite name)
                foreach (var img in go.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite != null && img.sprite.name.IndexOf("bedtime", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        img.gameObject.SetActive(false);
                        break;
                    }
                }

                // Set label; disable TextLocalizor so it doesn't override our text
                tmp = go.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmp != null)
                {
                    var loc = tmp.GetComponent<MonoBehaviour>(); // TextLocalizor if present
                    // Disable any MonoBehaviour on the TMP GO whose name contains "Localiz"
                    foreach (var mb in tmp.gameObject.GetComponents<MonoBehaviour>())
                        if (mb != null && mb.GetType().Name.IndexOf("Localiz", System.StringComparison.OrdinalIgnoreCase) >= 0)
                            mb.enabled = false;
                    tmp.text               = label;
                    tmp.enableWordWrapping = false;
                    tmp.enableAutoSizing   = true;
                    tmp.fontSizeMin        = 6f;
                    tmp.fontSizeMax        = 20f;
                }
            }
            else
            {
                go = new GameObject($"P_{label}");
                go.transform.SetParent(row.transform, false);
                var img2 = go.AddComponent<Image>();
                img2.sprite = GetOrCreateRoundedRectSprite();
                img2.type   = Image.Type.Sliced;
                img2.color  = Color.white;
                btn = go.AddComponent<Button>();
                btn.targetGraphic = img2;
                var txtGo2 = new GameObject("T");
                txtGo2.transform.SetParent(go.transform, false);
                tmp = txtGo2.AddComponent<TextMeshProUGUI>();
                var tr2 = txtGo2.GetComponent<RectTransform>();
                tr2.anchorMin = Vector2.zero; tr2.anchorMax = Vector2.one;
                tr2.sizeDelta = Vector2.zero; tr2.anchoredPosition = Vector2.zero;
                tmp.text = label; tmp.color = Color.white;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.enableWordWrapping = false;
                tmp.enableAutoSizing = true;
                tmp.fontSizeMin = 6f; tmp.fontSizeMax = 20f;
            }

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
            return tmp ?? go.GetComponentInChildren<TextMeshProUGUI>(true)!;
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
                if (_isRegisteredMethod != null) break;
                var fn = asm.FullName ?? "";
                if (fn.StartsWith("UnityEngine") || fn.StartsWith("System") ||
                    fn.StartsWith("Mono")        || fn.StartsWith("mscorlib") ||
                    fn.StartsWith("TMPro")       || fn.StartsWith("Unity.")) continue;
                Type[]? types = null;
                try { types = asm.GetTypes(); } catch { continue; }
                foreach (var t in types)
                {
                    if (t == null || t.Name != "ItemUtilities") continue;
                    _isRegisteredMethod = t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                        .FirstOrDefault(m => m.Name == "IsRegistered" &&
                                             m.ReturnType == typeof(bool) &&
                                             m.GetParameters().Length == 1 &&
                                             m.GetParameters()[0].ParameterType.Name == "Item");
                    if (_isRegisteredMethod != null) break;
                }
            }
        }

        private static bool IsRecipeRecorded(Item item)
        {
            if (_isRegisteredMethod == null) return false;
            try { return (bool)(_isRegisteredMethod.Invoke(null, new object[] { item }) ?? false); }
            catch { return false; }
        }

        // ── Slot badge scanning ───────────────────────────────────────────

        // Returns the first property or field of type Item on compType, using a per-type cache.
        private static MemberInfo? FindItemMember(Type compType)
        {
            if (_typeItemMemberCache.TryGetValue(compType, out var cached))
                return cached;

            MemberInfo? member = null;
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
            return member;
        }

        // Reads the Item value from a cached property or field member.
        private static Item? ReadItemFromMember(MemberInfo member, object obj)
        {
            if (member is PropertyInfo pi) return pi.GetValue(obj) as Item;
            if (member is FieldInfo   fi) return fi.GetValue(obj) as Item;
            return null;
        }

        // Called from OnSetupItemHoveringUI — finds the slot by matching the exact Item instance.
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
                            try { if (prop.GetValue(mb) == (object)item) { _slotCompType = compType; _slotItemMember = prop; return; } } catch { }
                        }
                        foreach (var field in compType.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                        {
                            if (field.FieldType != typeof(Item)) continue;
                            try { if (field.GetValue(mb) == (object)item) { _slotCompType = compType; _slotItemMember = field; return; } } catch { }
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

                    var item      = ReadItemFromMember(_slotItemMember!, mb);
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
                // Slow path: scan ALL UI MonoBehaviours to find recorded recipe items.
                // Runs until _slotCompType is discovered, then fast path takes over.
                BroadScanForRecordedItems();
            }
        }

        private void BroadScanForRecordedItems()
        {
            foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (!(mb.transform is RectTransform)) continue;
                if (mb.GetType() == GetType()) continue; // skip self

                var compType = mb.GetType();
                int id       = mb.GetInstanceID();

                if (mb is ItemHoveringUI) continue; // skip tooltip UI
                // Skip HUD/action-bar/button components — not inventory slots
                var tn = compType.Name;
                if (tn.Contains("HUD")    || tn.Contains("Status") || tn.Contains("Stamina") ||
                    tn.Contains("Health") || tn.Contains("Energy")  || tn.Contains("Equip")  ||
                    tn.Contains("Button") || tn.Contains("Weapon")  || tn.Contains("Action")) continue;

                var member = FindItemMember(compType);
                if (member == null) continue;

                Item? item;
                try   { item = ReadItemFromMember(member, mb); }
                catch { continue; }

                if (item == null) continue; // skip empty slots

                // Discover slot type from any slot holding any item (not just registered ones)
                if (_slotCompType == null)
                {
                    _slotCompType   = compType;
                    _slotItemMember = member;
                }

                bool showBadge = IsRecipeRecorded(item);

                if (!_slotBadges.TryGetValue(id, out var badge))
                {
                    if (!showBadge) continue;
                    badge = CreateSlotBadge(mb);
                    _slotBadges[id] = badge;
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
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-5f, -5f);
            rt.sizeDelta        = new Vector2(28f, 28f);

            var circleImg = badge.AddComponent<Image>();
            circleImg.color  = new Color(0.13f, 0.65f, 0.28f, 1f);
            circleImg.sprite = GetOrCreateCircleSprite();
            circleImg.type   = Image.Type.Simple;

            var txtGo = new GameObject("Check");
            txtGo.transform.SetParent(badge.transform, false);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text      = "✓";
            tmp.fontSize  = 18f;
            tmp.color     = Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontStyle = FontStyles.Bold;
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.sizeDelta = Vector2.zero; tr.anchoredPosition = Vector2.zero;

            return badge;
        }

        private static Sprite? _circleSprite;
        private static Sprite GetOrCreateCircleSprite()
        {
            if (_circleSprite != null) return _circleSprite;
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            float r = size / 2f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - r + 0.5f, dy = y - r + 0.5f;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
                }
            tex.Apply();
            _circleSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
            return _circleSprite;
        }

        // Rounded rect sprite for cards — 9-sliced so it stretches cleanly
        private static Sprite? _roundedRectSprite;
        private static Sprite GetOrCreateRoundedRectSprite()
        {
            if (_roundedRectSprite != null) return _roundedRectSprite;
            const int size = 256;
            const float r = 56f; // same proportion as original 14/64
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, size - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, size - r);
                    float dx = (x + 0.5f) - cx, dy = (y + 0.5f) - cy;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
                }
            tex.Apply();
            _roundedRectSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                400f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _roundedRectSprite;
        }

        // Pill sprite for toggle track — 192×96 with r=48 (= h/2), 9-sliced
        // Borders 48+48=96 == target height 96 → zero vertical distortion
        private static Sprite? _pillSprite;
        private static Sprite GetOrCreatePillSprite()
        {
            if (_pillSprite != null) return _pillSprite;
            const int w = 192, h = 96;
            const float r = 48f; // = h/2 → pure pill
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++)
                {
                    float cx = Mathf.Clamp(x + 0.5f, r, w - r);
                    float cy = Mathf.Clamp(y + 0.5f, r, h - r);
                    float dx = (x + 0.5f) - cx, dy = (y + 0.5f) - cy;
                    tex.SetPixel(x, y, dx * dx + dy * dy <= r * r ? Color.white : Color.clear);
                }
            tex.Apply();
            _pillSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                400f, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
            return _pillSprite;
        }

        // ── Settings Panel ────────────────────────────────────────────────

        private void BuildSettingsPanel()
        {
            _settingsCanvas = new GameObject("AllInOneMod_m0n0t0ny_Canvas");
            DontDestroyOnLoad(_settingsCanvas);
            var canvas = _settingsCanvas.AddComponent<Canvas>();
            _settingsCanvasComp = canvas;
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            _settingsCanvas.AddComponent<CanvasScaler>();
            _settingsCanvas.AddComponent<GraphicRaycaster>();

            // Panel — rounded outer container, auto-sizes vertically, 800px wide, centered
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_settingsCanvas.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(800f, 0f);
            var panelImg = panel.AddComponent<Image>();
            panelImg.sprite = GetOrCreateRoundedRectSprite();
            panelImg.type   = Image.Type.Sliced;
            panelImg.color  = new Color(0.047f, 0.047f, 0.055f, 0.98f);
            var panelMask = panel.AddComponent<Mask>();
            panelMask.showMaskGraphic = true;
            var panelVLG = panel.AddComponent<VerticalLayoutGroup>();
            panelVLG.childAlignment       = TextAnchor.UpperCenter;
            panelVLG.childForceExpandWidth  = true;
            panelVLG.childForceExpandHeight = false;
            panelVLG.spacing = 0f;
            panelVLG.padding = new RectOffset(0, 0, 0, 0);
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Header ────────────────────────────────────────────────────
            var header = LChild(panel, "Header", 54f);
            header.GetComponent<Image>().color = new Color(0.060f, 0.060f, 0.072f, 1f);
            var hHLG = header.AddComponent<HorizontalLayoutGroup>();
            hHLG.padding              = new RectOffset(16, 16, 0, 0);
            hHLG.childAlignment       = TextAnchor.MiddleLeft;
            hHLG.childForceExpandHeight = true;
            hHLG.childForceExpandWidth  = false;
            hHLG.spacing = 0f;
            var titleGo = LText(header, "Title", "All In One - m0n0t0ny's Mod", 15f, flexW: 1f);
            var titleTMP = titleGo.GetComponent<TextMeshProUGUI>();
            titleTMP.color = Color.white;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Left;
            var verGo = LText(header, "Ver", "v2.3", 10f, prefW: 44f);
            verGo.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.75f, 0f, 1f);
            verGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

            // Gold accent line
            var accent = LChild(panel, "Accent", 2f);
            accent.GetComponent<Image>().color = new Color(1f, 0.75f, 0f, 1f);

            // ── Cards box — equal 16px padding on all four sides ─────────
            var cardsBox = new GameObject("CardsBox");
            cardsBox.transform.SetParent(panel.transform, false);
            cardsBox.AddComponent<RectTransform>();
            cardsBox.AddComponent<Image>().color = Color.clear;
            var cbVLG = cardsBox.AddComponent<VerticalLayoutGroup>();
            cbVLG.padding              = new RectOffset(16, 16, 16, 16);
            cbVLG.spacing              = 0f;
            cbVLG.childForceExpandWidth  = true;
            cbVLG.childForceExpandHeight = false;
            cardsBox.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Three-column content area ─────────────────────────────────
            var content = new GameObject("Content");
            content.transform.SetParent(cardsBox.transform, false);
            content.AddComponent<RectTransform>();
            content.AddComponent<Image>().color = Color.clear;
            var cHLG = content.AddComponent<HorizontalLayoutGroup>();
            cHLG.padding              = new RectOffset(0, 0, 0, 0);
            cHLG.spacing              = 12f;
            cHLG.childAlignment       = TextAnchor.UpperLeft;
            cHLG.childForceExpandWidth  = true;
            cHLG.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var col1 = LColumn(content, "Col1");
            var col2 = LColumn(content, "Col2");
            var col3 = LColumn(content, "Col3");

            // ── COL 1: Item Value ─────────────────────────────────────────
            var c1v = LCard(col1, "Item Value");

            var (tRow, tImg, tThumb) = LToggleRow(c1v, "Show sell value on hover",
                "Visible always, not just in shops");
            _toggleBtnImage = tImg;
            _toggleBtnThumb = tThumb;
            tRow.GetComponentInChildren<Button>().onClick.AddListener(OnToggleClicked);
            RefreshToggleButton();

            LSubLabel(c1v, "Display mode");

            var modeRow = LChild(c1v, "ModeRow", 30f);
            modeRow.GetComponent<Image>().color = Color.clear;
            var mHLG = modeRow.AddComponent<HorizontalLayoutGroup>();
            mHLG.spacing               = 6f;
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

            // ── COL 1: Enemies ────────────────────────────────────────────
            var c1e = LCard(col1, "Enemies");

            var (enRow, enImg, enThumb) = LToggleRow(c1e, "Show enemy names",
                "Displayed above their health bar");
            _enemyNamesToggleImage = enImg;
            _enemyNamesToggleThumb = enThumb;
            enRow.GetComponentInChildren<Button>().onClick.AddListener(OnEnemyNamesToggleClicked);
            RefreshEnemyNamesToggle();

            var (auRow, auImg, auThumb) = LToggleRow(c1e, "Auto-unload gun on kill",
                "Moves ammo to enemy stash when you kill them");
            _autoUnloadToggleImage = auImg;
            _autoUnloadToggleThumb = auThumb;
            auRow.GetComponentInChildren<Button>().onClick.AddListener(OnAutoUnloadToggleClicked);
            RefreshAutoUnloadToggle();

            var (kfRow, kfImg, kfThumb) = LToggleRow(c1e, "Kill feed",
                "Shows kills in the top-right corner during raids");
            _killFeedToggleImage = kfImg;
            _killFeedToggleThumb = kfThumb;
            kfRow.GetComponentInChildren<Button>().onClick.AddListener(OnKillFeedToggleClicked);
            RefreshKillFeedToggle();

            // ── COL 1: Item Transfer ──────────────────────────────────────
            var c1t = LCard(col1, "Item Transfer");

            var (trRow, trImg, trThumb) = LToggleRow(c1t, "Modifier + click to transfer",
                "Moves items between container and backpack");
            _transferToggleImage = trImg;
            _transferToggleThumb = trThumb;
            trRow.GetComponentInChildren<Button>().onClick.AddListener(OnTransferToggleClicked);
            RefreshTransferToggle();

            LSubLabel(c1t, "Modifier key");

            var modRow = LChild(c1t, "ModRow", 30f);
            modRow.GetComponent<Image>().color = Color.clear;
            var modHLG = modRow.AddComponent<HorizontalLayoutGroup>();
            modHLG.spacing               = 6f;
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
            warnGo.transform.SetParent(c1t.transform, false);
            warnGo.AddComponent<RectTransform>();
            var warnTMP = warnGo.AddComponent<TextMeshProUGUI>();
            warnTMP.text = "⚠ Shift is also set to close containers.\nSwitch transfer to Alt to avoid conflicts.";
            warnTMP.fontSize = 9.5f;
            warnTMP.color = new Color(1f, 0.75f, 0.2f, 1f);
            warnTMP.alignment = TextAlignmentOptions.Left;
            warnGo.AddComponent<LayoutElement>().preferredHeight = 32f;
            _shiftConflictWarning = warnGo;
            RefreshShiftConflict();

            // ── COL 2: Auto-Close Container ───────────────────────────────
            var c2ac = LCard(col2, "Auto-Close Container");

            var acLabels = new (string name, string desc)[]
            {
                ("Close on movement", "W / A / S / D keys"),
                ("Close on Shift",    "When pressing Shift"),
                ("Close on Space",    "When pressing Space"),
                ("Close on damage",   "When taking a hit"),
            };
            _autoCloseBtnImages = new Image[4];
            _autoCloseBtnThumbs = new RectTransform[4];
            for (int i = 0; i < 4; i++)
            {
                var idx = i;
                var (acRow, acImg, acThumb) = LToggleRow(c2ac, acLabels[i].name, acLabels[i].desc);
                _autoCloseBtnImages[i] = acImg;
                _autoCloseBtnThumbs[i] = acThumb;
                acRow.GetComponentInChildren<Button>().onClick.AddListener(() => OnAutoCloseToggleClicked(idx));
            }
            RefreshAutoCloseToggles();

            // ── COL 2: Weapons ────────────────────────────────────────────
            var c2w = LCard(col2, "Weapons");

            var (smRow, smImg, smThumb) = LToggleRow(c2w, "Skip melee on scroll",
                "Scroll wheel skips the melee slot");
            _skipMeleeToggleImage = smImg;
            _skipMeleeToggleThumb = smThumb;
            smRow.GetComponentInChildren<Button>().onClick.AddListener(OnSkipMeleeToggleClicked);
            RefreshSkipMeleeToggle();

            // ── COL 2: Lootbox Highlight ──────────────────────────────────
            var c2lb = LCard(col2, "Lootbox Highlight");

            var (lbRow, lbImg, lbThumb) = LToggleRow(c2lb, "Highlight loot containers",
                "Gold outline on loot boxes in the world");
            _lootboxHLToggleImage = lbImg;
            _lootboxHLToggleThumb = lbThumb;
            lbRow.GetComponentInChildren<Button>().onClick.AddListener(OnLootboxHLToggleClicked);
            RefreshLootboxHLToggle();

            var (lbuRow, lbuImg, lbuThumb) = LToggleRow(c2lb, "Only unsearched",
                "Hides outline on already-opened containers");
            _lootboxHLUnsearchedToggleImage = lbuImg;
            _lootboxHLUnsearchedToggleThumb = lbuThumb;
            lbuRow.GetComponentInChildren<Button>().onClick.AddListener(OnLootboxHLUnsearchedToggleClicked);
            RefreshLootboxHLUnsearchedToggle();

            // ── COL 3: Recorded Items ─────────────────────────────────────
            var c3fr = LCard(col3, "Recorded Items");

            var (frRow, frImg, frThumb) = LToggleRow(c3fr, "Show badge on recorded items",
                "Green ✓ on blueprints and master keys");
            _recorderToggleImage = frImg;
            _recorderToggleThumb = frThumb;
            frRow.GetComponentInChildren<Button>().onClick.AddListener(OnRecorderBadgeToggleClicked);
            RefreshRecorderBadgeToggle();

            // ── COL 3: FPS Counter ────────────────────────────────────────
            var c3fps = LCard(col3, "FPS Counter");

            var (fpsRow, fpsImg, fpsThumb) = LToggleRow(c3fps, "Show FPS counter",
                "Displayed in the top-right corner");
            _fpsToggleImage = fpsImg;
            _fpsToggleThumb = fpsThumb;
            fpsRow.GetComponentInChildren<Button>().onClick.AddListener(OnFpsToggleClicked);
            RefreshFpsToggle();

            // ── COL 3: Sleep Presets ──────────────────────────────────────
            var c3sp = LCard(col3, "Sleep Presets");

            var (stRow, stImg, stThumb) = LToggleRow(c3sp, "Wake-up preset buttons",
                "Adds preset buttons to the sleep screen");
            _sleepToggleImage = stImg;
            _sleepToggleThumb = stThumb;
            stRow.GetComponentInChildren<Button>().onClick.AddListener(OnSleepToggleClicked);
            RefreshSleepToggle();

            LPickerRow(c3sp, "Preset 1",
                () => _preset1Hour,
                v => { _preset1Hour = v; PlayerPrefs.SetInt(PREF_PRESET1H, v); PlayerPrefs.Save();
                       if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}"; },
                () => _preset1Min,
                v => { _preset1Min  = v; PlayerPrefs.SetInt(PREF_PRESET1M, v); PlayerPrefs.Save();
                       if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}"; });

            LPickerRow(c3sp, "Preset 2",
                () => _preset2Hour,
                v => { _preset2Hour = v; PlayerPrefs.SetInt(PREF_PRESET2H, v); PlayerPrefs.Save();
                       if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}"; },
                () => _preset2Min,
                v => { _preset2Min  = v; PlayerPrefs.SetInt(PREF_PRESET2M, v); PlayerPrefs.Save();
                       if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}"; });

            LPickerRow(c3sp, "Preset 3",
                () => _preset3Hour,
                v => { _preset3Hour = v; PlayerPrefs.SetInt(PREF_PRESET3H, v); PlayerPrefs.Save();
                       if (_preset3BtnLabel != null) _preset3BtnLabel.text = $"{_preset3Hour:D2}:{_preset3Min:D2}"; },
                () => _preset3Min,
                v => { _preset3Min  = v; PlayerPrefs.SetInt(PREF_PRESET3M, v); PlayerPrefs.Save();
                       if (_preset3BtnLabel != null) _preset3BtnLabel.text = $"{_preset3Hour:D2}:{_preset3Min:D2}"; });

            LPickerRow(c3sp, "Preset 4",
                () => _preset4Hour,
                v => { _preset4Hour = v; PlayerPrefs.SetInt(PREF_PRESET4H, v); PlayerPrefs.Save();
                       if (_preset4BtnLabel != null) _preset4BtnLabel.text = $"{_preset4Hour:D2}:{_preset4Min:D2}"; },
                () => _preset4Min,
                v => { _preset4Min  = v; PlayerPrefs.SetInt(PREF_PRESET4M, v); PlayerPrefs.Save();
                       if (_preset4BtnLabel != null) _preset4BtnLabel.text = $"{_preset4Hour:D2}:{_preset4Min:D2}"; });

            // ── Bottom bar ────────────────────────────────────────────────
            var bottom = LChild(panel, "Bottom", 52f);
            bottom.GetComponent<Image>().color = new Color(0.040f, 0.040f, 0.050f, 1f);
            var bHLG = bottom.AddComponent<HorizontalLayoutGroup>();
            bHLG.padding              = new RectOffset(16, 16, 10, 10);
            bHLG.spacing              = 10f;
            bHLG.childAlignment       = TextAnchor.MiddleLeft;
            bHLG.childForceExpandHeight = false;
            bHLG.childForceExpandWidth  = false;

            var closeGo = new GameObject("CloseBtn");
            closeGo.transform.SetParent(bottom.transform, false);
            closeGo.AddComponent<RectTransform>();
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.sprite = GetOrCreateRoundedRectSprite();
            closeImg.type   = Image.Type.Sliced;
            closeImg.color  = new Color(0.55f, 0.10f, 0.10f, 1f);
            var closeLe = closeGo.AddComponent<LayoutElement>();
            closeLe.preferredWidth  = 110f;
            closeLe.preferredHeight = 32f;
            closeGo.AddComponent<Button>().onClick.AddListener(() => SetMenuVisible(false));
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

        // Column in the three-column content area (VLG, flex width, no ContentSizeFitter — parent HLG handles sizing)
        private static GameObject LColumn(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = Color.clear;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment      = TextAnchor.UpperLeft;
            vlg.padding             = new RectOffset(0, 0, 0, 0);
            vlg.spacing             = 10f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.minWidth       = 0f;
            le.preferredWidth = 0f;
            le.flexibleWidth  = 1f;
            return go;
        }

        // Rounded card — one per category, black semi-transparent bg
        private static GameObject LCard(GameObject parent, string categoryTitle)
        {
            var card = new GameObject($"Card_{categoryTitle}");
            card.transform.SetParent(parent.transform, false);
            card.AddComponent<RectTransform>();
            var img = card.AddComponent<Image>();
            img.sprite = GetOrCreateRoundedRectSprite();
            img.type   = Image.Type.Sliced;
            img.color  = new Color(0f, 0f, 0f, 0.72f);
            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding             = new RectOffset(10, 10, 10, 12);
            vlg.spacing             = 6f;
            vlg.childForceExpandWidth  = true;
            vlg.childForceExpandHeight = false;

            // Category label inside card
            var lbl = new GameObject("CardTitle");
            lbl.transform.SetParent(card.transform, false);
            lbl.AddComponent<RectTransform>();
            var tmp = lbl.AddComponent<TextMeshProUGUI>();
            tmp.text = categoryTitle.ToUpper(); tmp.fontSize = 8f;
            tmp.color = new Color(0.42f, 0.42f, 0.52f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Left;
            tmp.characterSpacing = 1.2f;
            lbl.AddComponent<LayoutElement>().preferredHeight = 11f;

            LGap(card, 2f);
            return card;
        }

        // Small sub-label (e.g. "Display mode", "Modifier key")
        private static void LSubLabel(GameObject parent, string text)
        {
            var go = new GameObject("SubLabel");
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = text; tmp.fontSize = 9.5f;
            tmp.color = new Color(0.38f, 0.38f, 0.48f, 1f);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Left;
            go.AddComponent<LayoutElement>().preferredHeight = 14f;
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

        // Row: [VLG: Name / Description] [iOS switch]
        private static (GameObject row, Image trackImg, RectTransform thumbRT)
            LToggleRow(GameObject parent, string labelText, string description = "")
        {
            var row = new GameObject("ToggleRow");
            row.transform.SetParent(parent.transform, false);
            row.AddComponent<RectTransform>();
            row.AddComponent<Image>().color = Color.clear;
            var hlg = row.AddComponent<HorizontalLayoutGroup>();
            hlg.childAlignment        = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth  = false;
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(0, 0, 5, 5);
            row.AddComponent<LayoutElement>().preferredHeight = string.IsNullOrEmpty(description) ? 36f : 50f;

            // Text group: name + optional description
            var textGrp = new GameObject("TextGroup");
            textGrp.transform.SetParent(row.transform, false);
            textGrp.AddComponent<RectTransform>();
            var tgVLG = textGrp.AddComponent<VerticalLayoutGroup>();
            tgVLG.childAlignment       = TextAnchor.UpperLeft;
            tgVLG.childForceExpandWidth  = true;
            tgVLG.childForceExpandHeight = false;
            tgVLG.spacing = 2f;
            textGrp.AddComponent<LayoutElement>().flexibleWidth = 1f;

            var nameGo = new GameObject("Name");
            nameGo.transform.SetParent(textGrp.transform, false);
            nameGo.AddComponent<RectTransform>();
            var nameTMP = nameGo.AddComponent<TextMeshProUGUI>();
            nameTMP.text = labelText; nameTMP.fontSize = 12f;
            nameTMP.fontStyle = FontStyles.Bold;
            nameTMP.color = new Color(0.90f, 0.90f, 0.95f, 1f);
            nameTMP.alignment = TextAlignmentOptions.Left;
            nameGo.AddComponent<LayoutElement>().preferredHeight = 16f;

            if (!string.IsNullOrEmpty(description))
            {
                var descGo = new GameObject("Desc");
                descGo.transform.SetParent(textGrp.transform, false);
                descGo.AddComponent<RectTransform>();
                var descTMP = descGo.AddComponent<TextMeshProUGUI>();
                descTMP.text = description; descTMP.fontSize = 9.5f;
                descTMP.color = new Color(0.38f, 0.38f, 0.48f, 1f);
                descTMP.alignment = TextAlignmentOptions.Left;
                descGo.AddComponent<LayoutElement>().preferredHeight = 14f;
            }

            // iOS-style toggle track (pill)
            var track = new GameObject("Track");
            track.transform.SetParent(row.transform, false);
            var trackRT = track.AddComponent<RectTransform>();
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite = GetOrCreatePillSprite();
            trackImg.type   = Image.Type.Sliced;
            var trackBtn = track.AddComponent<Button>();
            trackBtn.targetGraphic = trackImg;
            var trackLE = track.AddComponent<LayoutElement>();
            trackLE.preferredWidth  = 44f;
            trackLE.preferredHeight = 24f;

            // White circle thumb
            var thumb = new GameObject("Thumb");
            thumb.transform.SetParent(track.transform, false);
            var thumbRT = thumb.AddComponent<RectTransform>();
            thumbRT.anchorMin = thumbRT.anchorMax = thumbRT.pivot = new Vector2(0.5f, 0.5f);
            thumbRT.sizeDelta = new Vector2(18f, 18f);
            var thumbImg = thumb.AddComponent<Image>();
            thumbImg.sprite = GetOrCreateCircleSprite();
            thumbImg.color  = Color.white;

            return (row, trackImg, thumbRT);
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

        private static void RefreshIOSToggle(Image track, RectTransform thumb, bool on)
        {
            track.color = on
                ? new Color(1f, 0.75f, 0f, 1f)
                : new Color(0.16f, 0.16f, 0.22f, 1f);
            thumb.anchoredPosition = new Vector2(on ? 11f : -11f, 0f);
        }

        private void RefreshToggleButton()
        {
            RefreshIOSToggle(_toggleBtnImage!, _toggleBtnThumb!, _showValue);
        }

        private void RefreshModeButtons()
        {
            var modes = new[] { DisplayMode.SingleOnly, DisplayMode.Combined, DisplayMode.StackOnly };
            for (int i = 0; i < 3; i++)
            {
                bool active = modes[i] == _mode;
                _modeBtnImages![i].color = active
                    ? new Color(0.38f, 0.26f, 0f, 1f)
                    : new Color(0.11f, 0.115f, 0.15f, 1f);
                _modeBtnLabels![i].color = active
                    ? Color.white
                    : new Color(0.40f, 0.40f, 0.50f, 1f);
                _modeBtnLabels![i].fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
            }
        }

        private void RefreshSleepToggle()
        {
            RefreshIOSToggle(_sleepToggleImage!, _sleepToggleThumb!, _sleepPresetsEnabled);
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
            RefreshIOSToggle(_enemyNamesToggleImage!, _enemyNamesToggleThumb!, _showEnemyNames);
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
            RefreshIOSToggle(_transferToggleImage!, _transferToggleThumb!, _transferEnabled);
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
                    ? new Color(0.38f, 0.26f, 0f, 1f)
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
                RefreshIOSToggle(_autoCloseBtnImages![i], _autoCloseBtnThumbs![i], states[i]);
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
            RefreshIOSToggle(_recorderToggleImage!, _recorderToggleThumb!, _showRecorderBadge);
        }

        private void OnFpsToggleClicked()
        {
            _showFps = !_showFps;
            PlayerPrefs.SetInt(PREF_FPS_COUNTER, _showFps ? 1 : 0);
            PlayerPrefs.Save();
            RefreshFpsToggle();
            if (!_showFps && _fpsCanvas != null)
                _fpsCanvas.SetActive(false);
            else if (_showFps)
            {
                EnsureFpsCanvas();
                _fpsCanvas!.SetActive(true);
            }
        }

        private void RefreshFpsToggle()
        {
            RefreshIOSToggle(_fpsToggleImage!, _fpsToggleThumb!, _showFps);
        }

        private void OnSkipMeleeToggleClicked()
        {
            _skipMeleeOnScroll = !_skipMeleeOnScroll;
            PlayerPrefs.SetInt(PREF_SKIP_MELEE, _skipMeleeOnScroll ? 1 : 0);
            PlayerPrefs.Save();
            RefreshSkipMeleeToggle();
        }

        private void RefreshSkipMeleeToggle()
        {
            RefreshIOSToggle(_skipMeleeToggleImage!, _skipMeleeToggleThumb!, _skipMeleeOnScroll);
        }

        private void OnAutoUnloadToggleClicked()
        {
            _autoUnloadEnabled = !_autoUnloadEnabled;
            PlayerPrefs.SetInt(PREF_AUTO_UNLOAD, _autoUnloadEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshAutoUnloadToggle();
        }

        private void RefreshAutoUnloadToggle()
        {
            RefreshIOSToggle(_autoUnloadToggleImage!, _autoUnloadToggleThumb!, _autoUnloadEnabled);
        }

        private void OnLootboxHLToggleClicked()
        {
            _lootboxHLEnabled = !_lootboxHLEnabled;
            PlayerPrefs.SetInt(PREF_LOOTBOX_HL, _lootboxHLEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshLootboxHLToggle();
            if (!_lootboxHLEnabled) ClearLootboxOutlines();
        }

        private void RefreshLootboxHLToggle()
        {
            RefreshIOSToggle(_lootboxHLToggleImage!, _lootboxHLToggleThumb!, _lootboxHLEnabled);
        }

        private void OnLootboxHLUnsearchedToggleClicked()
        {
            _lootboxHLOnlyUnsearched = !_lootboxHLOnlyUnsearched;
            PlayerPrefs.SetInt(PREF_LOOTBOX_HL_UNSEARCHED, _lootboxHLOnlyUnsearched ? 1 : 0);
            PlayerPrefs.Save();
            RefreshLootboxHLUnsearchedToggle();
        }

        private void RefreshLootboxHLUnsearchedToggle()
        {
            RefreshIOSToggle(_lootboxHLUnsearchedToggleImage!, _lootboxHLUnsearchedToggleThumb!, _lootboxHLOnlyUnsearched);
        }

        // ── ModConfig integration ─────────────────────────────────────────

        private bool _mcScanDone; // true after the one-time assembly scan

        private void TryInitModConfig()
        {
            if (_mcChecked) return;

            // Step 1: scan assemblies for ModConfigAPI — runs exactly ONCE.
            // Previously, returning early when ModConfig was absent left _mcChecked = false,
            // causing the expensive GetTypes() loop to run every single frame.
            if (_mcAPI == null && !_mcScanDone)
            {
                _mcScanDone = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (_mcAPI != null) break;
                    Type[]? types = null;
                    try { types = asm.GetTypes(); } catch { continue; }
                    foreach (var t in types)
                    {
                        if (t?.Name == "ModConfigAPI") { _mcAPI = t; break; }
                    }
                }
            }

            if (_mcAPI == null) { _mcChecked = true; return; } // not installed — stop forever

            // Step 2: call Initialize() — returns false if ModConfig's ModBehaviour isn't running yet.
            // Caller retries each frame via Update() until this returns true.
            bool ready = false;
            try
            {
                var initMethod = _mcAPI.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                ready = initMethod != null && (bool)(initMethod.Invoke(null, null) ?? false);
            }
            catch { }
            if (!ready) return;

            // Booleans
            MCAddBool(PREF_ENABLED,          "Show sell value on hover",       _showValue);
            MCAddBool(PREF_ENEMY_NAMES,      "Show enemy names",               _showEnemyNames);
            MCAddBool(PREF_TRANSFER_ENABLED, "Item transfer enabled",          _transferEnabled);
            MCAddBool(PREF_AC_WASD,          "Auto-close on movement (WASD)",  _autoCloseOnWASD);
            MCAddBool(PREF_AC_SHIFT,         "Auto-close on Shift",            _autoCloseOnShift);
            MCAddBool(PREF_AC_SPACE,         "Auto-close on Space",            _autoCloseOnSpace);
            MCAddBool(PREF_AC_DAMAGE,        "Auto-close on damage",           _autoCloseOnDamage);
            MCAddBool(PREF_SLEEP_ENABLED,    "Sleep preset buttons",           _sleepPresetsEnabled);
            MCAddBool(PREF_RECORDER_BADGE,   "Recorded items badge",           _showRecorderBadge);
            MCAddBool(PREF_FPS_COUNTER,      "FPS counter",                    _showFps);
            MCAddBool(PREF_AUTO_UNLOAD,            "Auto-unload gun on kill",            _autoUnloadEnabled);
            MCAddBool(PREF_LOOTBOX_HL,             "Lootbox highlight",                  _lootboxHLEnabled);
            MCAddBool(PREF_LOOTBOX_HL_UNSEARCHED,  "Lootbox highlight: only unsearched", _lootboxHLOnlyUnsearched);
            MCAddBool(PREF_KILL_FEED,              "Kill feed",                          _killFeedEnabled);
            // Dropdowns
            var modeOpts = new SortedDictionary<string, object>
            {
                { "Combined",    (int)DisplayMode.Combined    },
                { "Single only", (int)DisplayMode.SingleOnly  },
                { "Stack only",  (int)DisplayMode.StackOnly   },
            };
            MCAddDropdown(PREF_MODE, "Sell value display mode", modeOpts, typeof(int), (int)_mode);

            var modOpts = new SortedDictionary<string, object>
            {
                { "Shift", (int)TransferModifier.Shift },
                { "Alt",   (int)TransferModifier.Alt   },
            };
            MCAddDropdown(PREF_TRANSFER_MOD, "Transfer modifier key", modOpts, typeof(int), (int)_transferModifier);

            // Sliders
            MCAddSlider(PREF_PRESET1H, "Preset 1 - hour",    typeof(int), _preset1Hour, new Vector2(0, 23));
            MCAddSlider(PREF_PRESET1M, "Preset 1 - minutes", typeof(int), _preset1Min,  new Vector2(0, 50));
            MCAddSlider(PREF_PRESET2H, "Preset 2 - hour",    typeof(int), _preset2Hour, new Vector2(0, 23));
            MCAddSlider(PREF_PRESET2M, "Preset 2 - minutes", typeof(int), _preset2Min,  new Vector2(0, 50));
            MCAddSlider(PREF_PRESET3H, "Preset 3 - hour",    typeof(int), _preset3Hour, new Vector2(0, 23));
            MCAddSlider(PREF_PRESET3M, "Preset 3 - minutes", typeof(int), _preset3Min,  new Vector2(0, 50));
            MCAddSlider(PREF_PRESET4H, "Preset 4 - hour",    typeof(int), _preset4Hour, new Vector2(0, 23));
            MCAddSlider(PREF_PRESET4M, "Preset 4 - minutes", typeof(int), _preset4Min,  new Vector2(0, 50));

            // Change delegate
            try
            {
                _mcDelegate = OnModConfigChanged;
                _mcAPI.GetMethod("SafeAddOnOptionsChangedDelegate",
                        BindingFlags.Public | BindingFlags.Static, null,
                        new[] { typeof(Action<string>) }, null)
                    ?.Invoke(null, new object[] { _mcDelegate });
            }
            catch { }

            _mcChecked = true; // Registration complete — stop retrying
        }

        private void OnModConfigChanged(string key)
        {
            if (_mcAPI == null) return;

            if      (key == PREF_ENABLED)
            { _showValue = MCLoadBool(key, _showValue); PlayerPrefs.SetInt(key, _showValue ? 1 : 0); RefreshToggleButton(); }
            else if (key == PREF_MODE)
            { _mode = (DisplayMode)MCLoadInt(key, (int)_mode); PlayerPrefs.SetInt(key, (int)_mode); RefreshModeButtons(); }
            else if (key == PREF_ENEMY_NAMES)
            { _showEnemyNames = MCLoadBool(key, _showEnemyNames); PlayerPrefs.SetInt(key, _showEnemyNames ? 1 : 0); RefreshEnemyNamesToggle(); }
            else if (key == PREF_TRANSFER_ENABLED)
            { _transferEnabled = MCLoadBool(key, _transferEnabled); PlayerPrefs.SetInt(key, _transferEnabled ? 1 : 0); RefreshTransferToggle(); RefreshShiftConflict(); }
            else if (key == PREF_TRANSFER_MOD)
            { _transferModifier = (TransferModifier)MCLoadInt(key, (int)_transferModifier); PlayerPrefs.SetInt(key, (int)_transferModifier); RefreshTransferModifierButtons(); }
            else if (key == PREF_AC_WASD)
            { _autoCloseOnWASD  = MCLoadBool(key, _autoCloseOnWASD);  PlayerPrefs.SetInt(key, _autoCloseOnWASD  ? 1 : 0); RefreshAutoCloseToggles(); RefreshShiftConflict(); }
            else if (key == PREF_AC_SHIFT)
            { _autoCloseOnShift = MCLoadBool(key, _autoCloseOnShift); PlayerPrefs.SetInt(key, _autoCloseOnShift ? 1 : 0); RefreshAutoCloseToggles(); RefreshShiftConflict(); }
            else if (key == PREF_AC_SPACE)
            { _autoCloseOnSpace = MCLoadBool(key, _autoCloseOnSpace); PlayerPrefs.SetInt(key, _autoCloseOnSpace ? 1 : 0); RefreshAutoCloseToggles(); }
            else if (key == PREF_AC_DAMAGE)
            { _autoCloseOnDamage = MCLoadBool(key, _autoCloseOnDamage); PlayerPrefs.SetInt(key, _autoCloseOnDamage ? 1 : 0); RefreshAutoCloseToggles(); }
            else if (key == PREF_SLEEP_ENABLED)
            { _sleepPresetsEnabled = MCLoadBool(key, _sleepPresetsEnabled); PlayerPrefs.SetInt(key, _sleepPresetsEnabled ? 1 : 0); RefreshSleepToggle(); }
            else if (key == PREF_RECORDER_BADGE)
            {
                _showRecorderBadge = MCLoadBool(key, _showRecorderBadge);
                PlayerPrefs.SetInt(key, _showRecorderBadge ? 1 : 0);
                RefreshRecorderBadgeToggle();
                if (!_showRecorderBadge) foreach (var kvp in _slotBadges) if (kvp.Value != null) kvp.Value.SetActive(false);
            }
            else if (key == PREF_FPS_COUNTER)
            {
                _showFps = MCLoadBool(key, _showFps);
                PlayerPrefs.SetInt(key, _showFps ? 1 : 0);
                RefreshFpsToggle();
                if (!_showFps && _fpsCanvas != null) _fpsCanvas.SetActive(false);
                else if (_showFps) { EnsureFpsCanvas(); _fpsCanvas!.SetActive(true); }
            }
            else if (key == PREF_AUTO_UNLOAD)
            { _autoUnloadEnabled = MCLoadBool(key, _autoUnloadEnabled); PlayerPrefs.SetInt(key, _autoUnloadEnabled ? 1 : 0); RefreshAutoUnloadToggle(); }
            else if (key == PREF_LOOTBOX_HL)
            { _lootboxHLEnabled = MCLoadBool(key, _lootboxHLEnabled); PlayerPrefs.SetInt(key, _lootboxHLEnabled ? 1 : 0); RefreshLootboxHLToggle(); if (!_lootboxHLEnabled) ClearLootboxOutlines(); }
            else if (key == PREF_LOOTBOX_HL_UNSEARCHED)
            { _lootboxHLOnlyUnsearched = MCLoadBool(key, _lootboxHLOnlyUnsearched); PlayerPrefs.SetInt(key, _lootboxHLOnlyUnsearched ? 1 : 0); RefreshLootboxHLUnsearchedToggle(); }
            else if (key == PREF_KILL_FEED)
            { _killFeedEnabled = MCLoadBool(key, _killFeedEnabled); PlayerPrefs.SetInt(key, _killFeedEnabled ? 1 : 0); RefreshKillFeedToggle(); }
            else if (key == PREF_PRESET1H || key == PREF_PRESET1M)
            {
                _preset1Hour = MCLoadInt(PREF_PRESET1H, _preset1Hour); _preset1Min = MCLoadInt(PREF_PRESET1M, _preset1Min);
                PlayerPrefs.SetInt(PREF_PRESET1H, _preset1Hour); PlayerPrefs.SetInt(PREF_PRESET1M, _preset1Min);
                if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}";
            }
            else if (key == PREF_PRESET2H || key == PREF_PRESET2M)
            {
                _preset2Hour = MCLoadInt(PREF_PRESET2H, _preset2Hour); _preset2Min = MCLoadInt(PREF_PRESET2M, _preset2Min);
                PlayerPrefs.SetInt(PREF_PRESET2H, _preset2Hour); PlayerPrefs.SetInt(PREF_PRESET2M, _preset2Min);
                if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}";
            }
            else if (key == PREF_PRESET3H || key == PREF_PRESET3M)
            {
                _preset3Hour = MCLoadInt(PREF_PRESET3H, _preset3Hour); _preset3Min = MCLoadInt(PREF_PRESET3M, _preset3Min);
                PlayerPrefs.SetInt(PREF_PRESET3H, _preset3Hour); PlayerPrefs.SetInt(PREF_PRESET3M, _preset3Min);
                if (_preset3BtnLabel != null) _preset3BtnLabel.text = $"{_preset3Hour:D2}:{_preset3Min:D2}";
            }
            else if (key == PREF_PRESET4H || key == PREF_PRESET4M)
            {
                _preset4Hour = MCLoadInt(PREF_PRESET4H, _preset4Hour); _preset4Min = MCLoadInt(PREF_PRESET4M, _preset4Min);
                PlayerPrefs.SetInt(PREF_PRESET4H, _preset4Hour); PlayerPrefs.SetInt(PREF_PRESET4M, _preset4Min);
                if (_preset4BtnLabel != null) _preset4BtnLabel.text = $"{_preset4Hour:D2}:{_preset4Min:D2}";
            }
            PlayerPrefs.Save();
        }

        private void MCAddBool(string key, string desc, bool def)
        {
            try { _mcAPI!.GetMethod("SafeAddBoolDropdownList", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { MC_MOD_NAME, key, desc, def }); }
            catch { }
        }

        private void MCAddDropdown(string key, string desc, SortedDictionary<string, object> options, Type valueType, object def)
        {
            try { _mcAPI!.GetMethod("SafeAddDropdownList", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { MC_MOD_NAME, key, desc, options, valueType, def }); }
            catch { }
        }

        private void MCAddSlider(string key, string desc, Type valueType, object def, Vector2 range)
        {
            try { _mcAPI!.GetMethod("SafeAddInputWithSlider", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { MC_MOD_NAME, key, desc, valueType, def, (Vector2?)range }); }
            catch { }
        }

        private bool MCLoadBool(string key, bool def)
        {
            try
            {
                var result = _mcAPI!.GetMethod("SafeLoad", BindingFlags.Public | BindingFlags.Static)
                    ?.MakeGenericMethod(typeof(bool))
                    .Invoke(null, new object[] { MC_MOD_NAME, key, def });
                return result is bool b ? b : def;
            }
            catch { return def; }
        }

        private int MCLoadInt(string key, int def)
        {
            try
            {
                var result = _mcAPI!.GetMethod("SafeLoad", BindingFlags.Public | BindingFlags.Static)
                    ?.MakeGenericMethod(typeof(int))
                    .Invoke(null, new object[] { MC_MOD_NAME, key, def });
                return result is int i ? i : def;
            }
            catch { return def; }
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
            if (_showRecorderBadge && item != null && _slotCompType == null)
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
            img.color = new Color(0.38f, 0.26f, 0f, 1f);
            var btn = go.AddComponent<Button>();
            var c   = btn.colors;
            c.highlightedColor = new Color(0.55f, 0.38f, 0f, 1f);
            c.pressedColor     = new Color(0.22f, 0.15f, 0f, 1f);
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

        // ── Kill Feed ─────────────────────────────────────────────────────

        // Subscribed to the static Health.OnDead event (same approach as the KillFeed mod).
        // Fires for every death in the game — no scanning, no polling needed.
        private void OnKillFeedDeadDirect(Health health, DamageInfo dmgInfo)
        {
            if (!_killFeedEnabled) return;
            var victim = dmgInfo.toDamageReceiver?.health?.TryGetCharacter();
            if (victim == null) return;
            var killer = dmgInfo.fromCharacter;

            string victimName = victim.characterPreset != null
                ? victim.characterPreset.DisplayName : "?";
            string killerName = "";
            if (killer != null && killer != victim)
                killerName = killer.IsMainCharacter
                    ? "You"
                    : (killer.characterPreset != null ? killer.characterPreset.DisplayName : "");

            bool headshot = dmgInfo.crit > 0;
            string prefix = headshot ? "<color=#FF6060>[HS]</color> " : "";
            string entry  = killerName.Length > 0
                ? $"{prefix}{killerName} → {victimName}"
                : $"{prefix}→ {victimName}";
            AddKillFeedEntry(entry);
        }

        // ── Shared UI helpers ─────────────────────────────────────────────

        private string? GetCharacterDisplayName(CharacterMainControl ctrl)
        {
            if (ctrl == null) return null;
            var preset = _characterPresetField?.GetValue(ctrl);
            if (preset == null) return null;
            return _displayNameProp?.GetValue(preset) as string;
        }

        private void EnsureKillFeedCanvas()
        {
            if (_killFeedCanvas != null) return;

            _killFeedCanvas = new GameObject("KillFeed");
            DontDestroyOnLoad(_killFeedCanvas);
            var canvas = _killFeedCanvas.AddComponent<Canvas>();
            canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            _killFeedCanvas.AddComponent<CanvasScaler>();
            _killFeedCanvas.AddComponent<GraphicRaycaster>();

            _killFeedContainer = new GameObject("Container");
            _killFeedContainer.transform.SetParent(_killFeedCanvas.transform, false);
            var rt = _killFeedContainer.AddComponent<RectTransform>();
            rt.anchorMin        = new Vector2(1f, 1f);
            rt.anchorMax        = new Vector2(1f, 1f);
            rt.pivot            = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-10f, -42f); // below FPS counter
            rt.sizeDelta        = new Vector2(0f, 0f);
            var vlg = _killFeedContainer.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment         = TextAnchor.UpperRight;
            vlg.childForceExpandWidth  = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 4f;
            var csf = _killFeedContainer.AddComponent<ContentSizeFitter>();
            csf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;
            csf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        private void AddKillFeedEntry(string text)
        {
            EnsureKillFeedCanvas();

            while (_kfEntries.Count >= KF_MAX)
            {
                var oldest = _kfEntries[_kfEntries.Count - 1];
                if (oldest.Go != null) Destroy(oldest.Go);
                _kfEntries.RemoveAt(_kfEntries.Count - 1);
            }

            var go = new GameObject("KFEntry");
            go.transform.SetParent(_killFeedContainer!.transform, false);
            go.transform.SetAsFirstSibling();

            var img = go.AddComponent<Image>();
            img.color  = new Color(0f, 0f, 0f, 0.55f);
            img.sprite = GetOrCreateRoundedRectSprite();
            img.type   = Image.Type.Sliced;

            // HorizontalLayoutGroup + ContentSizeFitter shrink-wraps the pill to text width
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding               = new RectOffset(8, 8, 4, 4);
            hlg.childAlignment        = TextAnchor.MiddleCenter;
            hlg.childControlWidth     = true;
            hlg.childControlHeight    = true;
            hlg.childForceExpandWidth  = false;
            hlg.childForceExpandHeight = false;
            var entryCsf = go.AddComponent<ContentSizeFitter>();
            entryCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            entryCsf.verticalFit   = ContentSizeFitter.FitMode.PreferredSize;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var txtGo = new GameObject("T");
            txtGo.transform.SetParent(go.transform, false);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            var templateTmp = GameplayDataSettings.UIStyle.TemplateTextUGUI;
            if (templateTmp != null) tmp.font = templateTmp.font;
            tmp.text               = text;
            tmp.fontSize           = 12f;
            tmp.color              = Color.white;
            tmp.alignment          = TextAlignmentOptions.MidlineRight;
            tmp.enableWordWrapping = false;
            tmp.overflowMode       = TextOverflowModes.Overflow;
            tmp.richText           = true;
            var shadow = txtGo.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor    = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);

            _kfEntries.Insert(0, new KfEntry(go, cg, KF_DISPLAY + KF_FADE));
        }

        private void UpdateKillFeedEntries()
        {
            for (int i = _kfEntries.Count - 1; i >= 0; i--)
            {
                var e = _kfEntries[i];
                if (e.Go == null) { _kfEntries.RemoveAt(i); continue; }
                e.Timer -= Time.deltaTime;

                float alpha;
                if      (e.Timer > KF_DISPLAY) alpha = (KF_DISPLAY + KF_FADE - e.Timer) / KF_FADE;
                else if (e.Timer > KF_FADE)    alpha = 1f;
                else if (e.Timer > 0f)         alpha = e.Timer / KF_FADE;
                else { Destroy(e.Go); _kfEntries.RemoveAt(i); continue; }

                e.Group.alpha = alpha;
            }
        }

        private void ClearKillFeedSubscriptions()
        {
            foreach (var e in _kfEntries)
                if (e.Go != null) Destroy(e.Go);
            _kfEntries.Clear();
        }

        private void OnKillFeedToggleClicked()
        {
            _killFeedEnabled = !_killFeedEnabled;
            PlayerPrefs.SetInt(PREF_KILL_FEED, _killFeedEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshKillFeedToggle();
        }

        private void RefreshKillFeedToggle()
        {
            RefreshIOSToggle(_killFeedToggleImage!, _killFeedToggleThumb!, _killFeedEnabled);
        }
    }
}
