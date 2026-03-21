using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Duckov.Quests;
using Duckov.Quests.UI;
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
        private const string PREF_ENABLED = "DisplayItemValue_Enabled";
        private const string PREF_MODE = "DisplayItemValue_Mode";
        private const string PREF_SELL_COMBO = "DisplayItemValue_SellCombo";
        private const string PREF_SLEEP_ENABLED = "DisplayItemValue_SleepEnabled";
        private const string PREF_PRESET1H = "DisplayItemValue_Preset1H";
        private const string PREF_PRESET1M = "DisplayItemValue_Preset1M";
        private const string PREF_PRESET2H = "DisplayItemValue_Preset2H";
        private const string PREF_PRESET2M = "DisplayItemValue_Preset2M";
        private const string PREF_PRESET3H = "DisplayItemValue_Preset3H";
        private const string PREF_PRESET3M = "DisplayItemValue_Preset3M";
        private const string PREF_PRESET4H = "DisplayItemValue_Preset4H";
        private const string PREF_PRESET4M = "DisplayItemValue_Preset4M";
        private const string PREF_ENEMY_NAMES = "DisplayItemValue_EnemyNames";
        private const string PREF_TRANSFER_ENABLED = "DisplayItemValue_TransferEnabled";
        private const string PREF_TRANSFER_MOD = "DisplayItemValue_TransferMod";
        private const string PREF_TRANSFER_COMBO = "DisplayItemValue_TransferCombo";
        private const string PREF_AC_WASD = "DisplayItemValue_ACWasd";
        private const string PREF_AC_SHIFT = "DisplayItemValue_ACShift";
        private const string PREF_AC_SPACE = "DisplayItemValue_ACSpace";
        private const string PREF_AC_DAMAGE = "DisplayItemValue_ACDamage";
        private const string PREF_FPS_COUNTER = "DisplayItemValue_FpsCounter";
        private const string PREF_SKIP_MELEE = "DisplayItemValue_SkipMelee";
        private const KeyCode MENU_KEY = KeyCode.F9;
        private const string MC_MOD_NAME = "All In One - m0n0t0ny's Mod";

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
        private float _lootViewCacheTimer; // counts down; refresh when <= 0
        // Refreshed ONCE per frame in Update() - not per-caller.

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
        private Canvas? _settingsCanvasComp;
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
        private int _preset1Hour, _preset1Min;
        private int _preset2Hour, _preset2Min;
        private int _preset3Hour, _preset3Min;
        private int _preset4Hour, _preset4Min;
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
        private int _lastScrollDir;
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
        private static FieldInfo? _itemPlugsField;
        private static bool _itemPlugsSearched;
        private static PropertyInfo? _stackCountProp;
        private static bool _stackCountSearched;

        // ── ModConfig integration (optional) ─────────────────────────────
        private static Type? _mcAPI;
        private bool _mcChecked;
        private bool _mcDelegateRegistered;
        private bool _mcSavedRegistered;
        private Action<string>? _mcDelegate;
        private int _mcPollFrame;

        // ── Factory Recorder badge ────────────────────────────────────────
        private const string PREF_RECORDER_BADGE = "DisplayItemValue_RecorderBadge";
        private bool _showRecorderBadge;
        private Image? _recorderToggleImage;
        private RectTransform? _recorderToggleThumb;
        // ItemUtilities.IsRegistered(Item) → bool  (static helper used by the game)
        private static MethodInfo? _isRegisteredMethod;
        // Slot badge overlay tracking
        private static Type? _slotCompType;
        private static MemberInfo? _slotItemMember; // PropertyInfo or FieldInfo → Item
        private static readonly Dictionary<Type, MemberInfo?> _typeItemMemberCache = new Dictionary<Type, MemberInfo?>();
        private float _badgeScanTimer;
        private readonly Dictionary<int, GameObject> _slotBadges = new Dictionary<int, GameObject>();

        // ── Lootbox Highlight ─────────────────────────────────────────────
        private const string PREF_LOOTBOX_HL = "DisplayItemValue_LootboxHL";
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
        private static Type? _lbType;              // InteractableLootbox
        private static Type? _imType;              // InteractMarker
        private static FieldInfo? _imMarkedAsUsed;      // InteractMarker.markedAsUsed
        private static FieldInfo? _invInspectedField;   // Inventory.hasBeenInspectedInLootBox
        private static bool _lbCached;

        // ── Quest Favorites ───────────────────────────────────────────────
        private const string PREF_QUEST_FAV = "DisplayItemValue_QuestFav";
        private const string PREF_QUEST_FAV_IDS = "DisplayItemValue_QuestFavIds";
        private bool _questFavEnabled;
        private readonly HashSet<int> _favoriteQuestIds = new HashSet<int>();
        private float _questFavReorderTimer;
        private Image? _questFavToggleImage;
        private RectTransform? _questFavToggleThumb;
        private static readonly FieldInfo? _qvActiveEntriesField =
            typeof(QuestView).GetField("activeEntries", BindingFlags.NonPublic | BindingFlags.Instance);

        // ── Kill Feed ─────────────────────────────────────────────────────
        private const string PREF_KILL_FEED = "DisplayItemValue_KillFeed";
        private const string PREF_HIDE_CTRL = "DisplayItemValue_HideCtrlHint";
        private const string PREF_CAMERA_VIEW = "DisplayItemValue_CameraView";
        private const string PREF_HIDE_HUD_ADS = "DisplayItemValue_HideHudOnAds";
        private const string PREF_HIDE_AMMO_ADS = "DisplayItemValue_HideAmmoOnAds";
        private bool _killFeedEnabled;
        private Image? _killFeedToggleImage;
        private RectTransform? _killFeedToggleThumb;
        private bool _hideCtrlHint;
        private Image? _hideCtrlToggleImage;
        private RectTransform? _hideCtrlToggleThumb;
        private bool _hideHudOnAds;
        private bool _hideAmmoOnAds;
        private bool _adsHiding;
        private readonly List<CanvasGroup> _adsHideGroups = new List<CanvasGroup>();
        private readonly List<(CanvasGroup cg, float alpha, bool rays)> _adsSnapshot = new List<(CanvasGroup, float, bool)>();
        private Transform? _aimMarkerTransform;
        private Image? _hideHudAdsToggleImage;
        private RectTransform? _hideHudAdsToggleThumb;
        private Image? _hideAmmoAdsToggleImage;
        private RectTransform? _hideAmmoAdsToggleThumb;
        private bool _cameraViewPersist;
        private Image? _cameraViewToggleImage;
        private RectTransform? _cameraViewToggleThumb;
        private bool _savedTopDown;
        private bool _viewRestorePending;
        private static readonly FieldInfo? _topDownViewField =
            typeof(CameraArm).GetField("topDownView", BindingFlags.NonPublic | BindingFlags.Static);
        private static bool CameraArmGetTopDown() =>
            _topDownViewField != null && (bool)(_topDownViewField.GetValue(null) ?? false);
        private GameObject? _killFeedCanvas;
        private GameObject? _killFeedContainer;
        private readonly List<KfEntry> _kfEntries = new List<KfEntry>();
        private const int KF_MAX = 5;
        private const float KF_DISPLAY = 5f;
        private const float KF_FADE = 0.5f;

        private sealed class KfEntry
        {
            public readonly GameObject Go;
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
            _showValue = PlayerPrefs.GetInt(PREF_ENABLED, 1) == 1;
            _mode = (DisplayMode)PlayerPrefs.GetInt(PREF_MODE, (int)DisplayMode.Combined);
            if (!PlayerPrefs.HasKey(PREF_SELL_COMBO)) SaveSellComboPrefs();
            _showEnemyNames = PlayerPrefs.GetInt(PREF_ENEMY_NAMES, 1) == 1;
            _transferEnabled = PlayerPrefs.GetInt(PREF_TRANSFER_ENABLED, 1) == 1;
            _transferModifier = (TransferModifier)PlayerPrefs.GetInt(PREF_TRANSFER_MOD, (int)TransferModifier.Shift);
            _autoCloseOnWASD = PlayerPrefs.GetInt(PREF_AC_WASD, 0) == 1;
            _autoCloseOnShift = PlayerPrefs.GetInt(PREF_AC_SHIFT, 0) == 1;
            _autoCloseOnSpace = PlayerPrefs.GetInt(PREF_AC_SPACE, 0) == 1;
            _autoCloseOnDamage = PlayerPrefs.GetInt(PREF_AC_DAMAGE, 0) == 1;
            _sleepPresetsEnabled = PlayerPrefs.GetInt(PREF_SLEEP_ENABLED, 1) == 1;
            _preset1Hour = PlayerPrefs.GetInt(PREF_PRESET1H, 5);
            _preset1Min = PlayerPrefs.GetInt(PREF_PRESET1M, 30);
            _preset2Hour = PlayerPrefs.GetInt(PREF_PRESET2H, 21);
            _preset2Min = PlayerPrefs.GetInt(PREF_PRESET2M, 30);
            _preset3Hour = PlayerPrefs.GetInt(PREF_PRESET3H, 8);
            _preset3Min = PlayerPrefs.GetInt(PREF_PRESET3M, 0);
            _preset4Hour = PlayerPrefs.GetInt(PREF_PRESET4H, 12);
            _preset4Min = PlayerPrefs.GetInt(PREF_PRESET4M, 0);
            _showRecorderBadge = PlayerPrefs.GetInt(PREF_RECORDER_BADGE, 1) == 1;
            _showFps = PlayerPrefs.GetInt(PREF_FPS_COUNTER, 0) == 1;
            _skipMeleeOnScroll = PlayerPrefs.GetInt(PREF_SKIP_MELEE, 1) == 1;
            _autoUnloadEnabled = PlayerPrefs.GetInt(PREF_AUTO_UNLOAD, 1) == 1;
            _lootboxHLEnabled = PlayerPrefs.GetInt(PREF_LOOTBOX_HL, 1) == 1;
            _lootboxHLOnlyUnsearched = PlayerPrefs.GetInt(PREF_LOOTBOX_HL_UNSEARCHED, 0) == 1;
            _killFeedEnabled = PlayerPrefs.GetInt(PREF_KILL_FEED, 1) == 1;
            _hideCtrlHint = PlayerPrefs.GetInt(PREF_HIDE_CTRL, 1) == 1;
            _hideHudOnAds = PlayerPrefs.GetInt(PREF_HIDE_HUD_ADS, 0) == 1;
            _hideAmmoOnAds = PlayerPrefs.GetInt(PREF_HIDE_AMMO_ADS, 1) == 1;
            _cameraViewPersist = PlayerPrefs.GetInt(PREF_CAMERA_VIEW, 1) == 1;
            _savedTopDown = PlayerPrefs.GetInt("CameraViewSavedTopDown", 0) == 1;
            _questFavEnabled = PlayerPrefs.GetInt(PREF_QUEST_FAV, 1) == 1;
            foreach (var s in PlayerPrefs.GetString(PREF_QUEST_FAV_IDS, "").Split(','))
                if (int.TryParse(s.Trim(), out int qid) && qid != 0) _favoriteQuestIds.Add(qid);
            CacheRecorderReflection();
            EnsureLootboxTypes();
            BuildSettingsPanel();
            TryInitModConfig();
        }

        private CursorLockMode _prevLockMode;
        private bool _prevCursorVisible;
        private bool _menuOpen;
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
                _prevLockMode = Cursor.lockState;
                _prevCursorVisible = Cursor.visible;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

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
                Cursor.visible = _prevCursorVisible;

                foreach (var b in _disabledInputControls)
                    if (b != null) b.enabled = true;
                _disabledInputControls.Clear();

                // Sync F9 changes to ModConfig on panel close
                SyncAllToModConfig();
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
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _fpsCanvas.AddComponent<CanvasScaler>();
            _fpsCanvas.AddComponent<GraphicRaycaster>();

            var go = new GameObject("FpsText");
            go.transform.SetParent(_fpsCanvas.transform, false);
            _fpsTMP = go.AddComponent<TextMeshProUGUI>();
            _fpsTMP.fontSize = 14f;
            _fpsTMP.color = Color.white;
            _fpsTMP.fontStyle = FontStyles.Bold;
            _fpsTMP.alignment = TextAlignmentOptions.TopRight;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-42f, -10f);
            rt.sizeDelta = new Vector2(100f, 30f);

            // Shadow for readability
            var shadow = go.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
            shadow.effectDistance = new Vector2(1f, -1f);
        }

        void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Health.OnDead += OnKillFeedDeadDirect;
        }

        void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Health.OnDead -= OnKillFeedDeadDirect;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ClearLootboxOutlines();
            ClearKillFeedSubscriptions();
            _simpleIndicators = null;
            _adsHideGroups.Clear();
            _aimMarkerTransform = null;
            _simpleIndicatorsFound = false;
            _adsHiding = false;
            _viewRestorePending = true;
        }

        private Transform? _simpleIndicators;
        private bool _simpleIndicatorsFound;
        void Update()
        {
            if (!_mcChecked) TryInitModConfig();
            if (_mcChecked && _mcAPI != null && !_menuOpen)
            {
                if (++_mcPollFrame >= 60) { _mcPollFrame = 0; OnModConfigSaved(); }
            }
            var curLang = GetGameLanguage();
            if (_lastLang != SystemLanguage.Unknown && curLang != _lastLang)
                RebuildSettingsPanel();
            if (!_simpleIndicatorsFound && LevelManager.Instance != null)
            {
                foreach (var c in UnityEngine.Object.FindObjectsOfType<Canvas>())
                {
                    if (c.gameObject.name != "HUDCanvas") continue;
                    var si = c.transform.Find("SimpleIndicators");
                    if (si == null) continue;
                    _simpleIndicators = si;
                    // Find aim-related top-level children to KEEP visible
                    var keepTopLevel = new System.Collections.Generic.HashSet<Transform>();
                    foreach (var t in c.GetComponentsInChildren<Transform>(true))
                    {
                        if (t == c.transform) continue;
                        var n = t.name;
                        if (n.IndexOf("CrossHair", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("AimMarker", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            n.IndexOf("Reticle",   StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var cur = t;
                            while (cur.parent != null && cur.parent != c.transform)
                                cur = cur.parent;
                            if (cur.parent == c.transform) keepTopLevel.Add(cur);
                        }
                    }
                    // Also keep health bars visible during ADS
                    for (int i = 0; i < c.transform.childCount; i++)
                    {
                        var child = c.transform.GetChild(i);
                        if (child.name.IndexOf("Health", StringComparison.OrdinalIgnoreCase) >= 0)
                            keepTopLevel.Add(child);
                    }
                    // Build a CanvasGroup per child-to-hide (avoids SetActive spikes)
                    _adsHideGroups.Clear();
                    for (int i = 0; i < c.transform.childCount; i++)
                    {
                        var child = c.transform.GetChild(i);
                        if (keepTopLevel.Contains(child)) continue;
                        var cg = child.gameObject.GetComponent<CanvasGroup>()
                              ?? child.gameObject.AddComponent<CanvasGroup>();
                        _adsHideGroups.Add(cg);
                    }
                    // Store AimMarker reference for optional ammo hiding during ADS
                    _aimMarkerTransform = c.transform.Find("AimMarker");
                    _simpleIndicatorsFound = true;
                    break;
                }
            }
            // Keep enforcing: if hide is on and the game re-enabled it, hide it again
            if (_hideCtrlHint && _simpleIndicators != null && _simpleIndicators.gameObject.activeSelf)
                _simpleIndicators.gameObject.SetActive(false);

            // Hide HUD on ADS (right mouse button) - CanvasGroup.alpha avoids SetActive spikes
            if (_hideHudOnAds && _simpleIndicatorsFound && _adsHideGroups.Count > 0 && !_menuOpen)
            {
                bool ads = Input.GetMouseButton(1);
                if (ads && !_adsHiding)
                {
                    _adsHiding = true;
                    _adsSnapshot.Clear();
                    foreach (var cg in _adsHideGroups)
                    {
                        // If "Hide ammo on ADS" is OFF, keep bullet/weapon HUD visible
                        if (!_hideAmmoOnAds && cg.gameObject != null &&
                            cg.gameObject.name.IndexOf("Bullet", StringComparison.OrdinalIgnoreCase) >= 0)
                            continue;
                        _adsSnapshot.Add((cg, cg.alpha, cg.blocksRaycasts));
                        cg.alpha = 0f;
                        cg.blocksRaycasts = false;
                    }
                }
                // re-enforce handled in LateUpdate
                else if (!ads && _adsHiding)
                {
                    _adsHiding = false;
                    foreach (var s in _adsSnapshot) { if (s.cg == null) continue; s.cg.alpha = s.alpha; s.cg.blocksRaycasts = s.rays; }
                    _adsSnapshot.Clear();
                }
            }
            else if (_adsHiding)
            {
                _adsHiding = false;
                foreach (var s in _adsSnapshot) { if (s.cg == null) continue; s.cg.alpha = s.alpha; s.cg.blocksRaycasts = s.rays; }
                _adsSnapshot.Clear();
            }

            // Camera view: restore saved preference on scene load, then track changes
            if (_cameraViewPersist && LevelManager.Instance != null)
            {
                bool curTopDown = CameraArmGetTopDown();
                if (_viewRestorePending)
                {
                    _viewRestorePending = false;
                    if (curTopDown != _savedTopDown)
                        CameraArm.ToggleView();
                }
                else if (curTopDown != _savedTopDown)
                {
                    _savedTopDown = curTopDown;
                    PlayerPrefs.SetInt("CameraViewSavedTopDown", _savedTopDown ? 1 : 0);
                    PlayerPrefs.Save();
                }
            }

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

            if (_questFavEnabled && Input.GetKeyDown(KeyCode.N))
                TryToggleQuestFavorite();

            if (_questFavEnabled)
            {
                _questFavReorderTimer -= Time.unscaledDeltaTime;
                if (_questFavReorderTimer <= 0f)
                {
                    _questFavReorderTimer = 0.15f;
                    TryReorderQuestView();
                }
            }

            if (_sleepPresetsEnabled)
                CheckSleepViewInjection();

            if (_transferEnabled && Input.GetMouseButtonDown(0))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                bool mod = _transferModifier == TransferModifier.Shift ? shift : alt;
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

            // Resolve active LootView ONCE per frame - shared by AutoClose + AutoUnload.
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
                    _fpsValue = _fpsFrameCount / _fpsDeltaAccum;
                    _fpsDeltaAccum = 0f;
                    _fpsFrameCount = 0;
                    EnsureFpsCanvas();
                    _fpsTMP!.text = $"{Mathf.RoundToInt(_fpsValue)} FPS";
                }
            }
        }

        void LateUpdate()
        {
            // Re-enforce ADS hide after all game Update() calls have run
            if (_adsHiding)
                foreach (var s in _adsSnapshot) { if (s.cg == null) continue; s.cg.alpha = 0f; s.cg.blocksRaycasts = false; }

            // Snapshot the hovered item at the END of every frame so that
            // TryShiftClickTransfer() in the NEXT frame's Update() sees a
            // stable value even if EventSystem clears the hover mid-frame.
            _transferCachedItem = _lastHoveredItem;

            // Skip melee during scroll - the game has already switched weapons by LateUpdate,
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
        // Triggered when the player opens loot on an enemy - zero polling overhead.
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
            _lootboxScanTimer = 0f;
            _lootboxUpdateTimer = 0f;
        }

        // ── Shift-click transfer ──────────────────────────────────────────

        private void TryShiftClickTransfer()
        {
            var lv = (_cachedLootView != null && _cachedLootView.gameObject.activeInHierarchy) ? _cachedLootView : null;
            bool lvActive = lv != null;
            var item = _transferCachedItem ?? _lastHoveredItem;
            if (!lvActive || item == null) return;

            var charInvDisplay = _lootCharInvField?.GetValue(lv) as InventoryDisplay;
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
            if (LevelManager.Instance == null || !LevelManager.Instance.IsRaidMap) return;

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
                        _playerHealthComp = comp;
                        _playerHealthValueProp = prop;
                        _playerHealthPrev = (float)prop.GetValue(comp)!;
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
                _sleepViewInstance = sv;
                _sleepPresetsInjected = false;
                _preset1BtnLabel = null;
                _preset2BtnLabel = null;
                _preset3BtnLabel = null;
                _preset4BtnLabel = null;
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

            var sleepRect = sleepBtn.GetComponent<RectTransform>();
            var origParent = sleepRect.parent;
            var origAnchorMin = sleepRect.anchorMin;
            var origAnchorMax = sleepRect.anchorMax;
            var origPivot = sleepRect.pivot;
            var origSizeDelta = sleepRect.sizeDelta;
            var origAnchoredPos = sleepRect.anchoredPosition;
            int origSiblingIdx = sleepBtn.transform.GetSiblingIndex();

            var wrapper = new GameObject("SleepPresetWrapper");
            wrapper.transform.SetParent(origParent, false);
            wrapper.transform.SetSiblingIndex(origSiblingIdx);
            var wrapperRect = wrapper.AddComponent<RectTransform>();
            wrapperRect.anchorMin = origAnchorMin;
            wrapperRect.anchorMax = origAnchorMax;
            wrapperRect.pivot = origPivot;
            wrapperRect.sizeDelta = origSizeDelta;
            wrapperRect.anchoredPosition = origAnchoredPos;

            var hLayout = wrapper.AddComponent<HorizontalLayoutGroup>();
            hLayout.spacing = 6f;
            hLayout.childAlignment = TextAnchor.MiddleCenter;
            hLayout.childForceExpandWidth = false;
            hLayout.childForceExpandHeight = true;
            hLayout.padding = new RectOffset(0, 0, 0, 0);

            sleepBtn.transform.SetParent(wrapper.transform, false);
            var sleepLE = sleepBtn.gameObject.GetComponent<LayoutElement>();
            if (sleepLE == null) sleepLE = sleepBtn.gameObject.AddComponent<LayoutElement>();
            sleepLE.preferredWidth = origSizeDelta.x * 0.32f;
            sleepLE.flexibleWidth = 0f;

            var presetGrid = new GameObject("PresetGrid");
            presetGrid.transform.SetParent(wrapper.transform, false);
            var gridLE = presetGrid.AddComponent<LayoutElement>();
            gridLE.flexibleWidth = 1f;
            var vLayout = presetGrid.AddComponent<VerticalLayoutGroup>();
            vLayout.spacing = 4f;
            vLayout.childAlignment = TextAnchor.MiddleCenter;
            vLayout.childForceExpandWidth = true;
            vLayout.childForceExpandHeight = true;

            var row1 = MakePresetRow(presetGrid);
            _preset1BtnLabel = AddGridBtn(row1, sv, $"{_preset1Hour:D2}:{_preset1Min:D2}", () => MinutesUntilTime(_preset1Hour, _preset1Min), sleepBtn);
            _preset2BtnLabel = AddGridBtn(row1, sv, $"{_preset2Hour:D2}:{_preset2Min:D2}", () => MinutesUntilTime(_preset2Hour, _preset2Min), sleepBtn);
            _preset3BtnLabel = AddGridBtn(row1, sv, $"{_preset3Hour:D2}:{_preset3Min:D2}", () => MinutesUntilTime(_preset3Hour, _preset3Min), sleepBtn);
            _preset4BtnLabel = AddGridBtn(row1, sv, $"{_preset4Hour:D2}:{_preset4Min:D2}", () => MinutesUntilTime(_preset4Hour, _preset4Min), sleepBtn);

            var row2 = MakePresetRow(presetGrid);
            AddGridBtn(row2, sv, "Rain", () => MinutesUntilRain(), sleepBtn);
            AddGridBtn(row2, sv, "Storm I", () => MinutesUntilStorm(1), sleepBtn);
            AddGridBtn(row2, sv, "Storm II", () => MinutesUntilStorm(2), sleepBtn);
            AddGridBtn(row2, sv, "Storm End", () => MinutesUntilStormEnd(), sleepBtn);
        }

        private static GameObject MakePresetRow(GameObject parent)
        {
            var row = new GameObject("Row");
            row.transform.SetParent(parent.transform, false);
            var h = row.AddComponent<HorizontalLayoutGroup>();
            h.spacing = 4f;
            h.childAlignment = TextAnchor.MiddleCenter;
            h.childForceExpandWidth = true;
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
                    tmp.text = label;
                    tmp.enableWordWrapping = false;
                    tmp.enableAutoSizing = true;
                    tmp.fontSizeMin = 6f;
                    tmp.fontSizeMax = 20f;
                }
            }
            else
            {
                go = new GameObject($"P_{label}");
                go.transform.SetParent(row.transform, false);
                var img2 = go.AddComponent<Image>();
                img2.sprite = GetOrCreateRoundedRectSprite();
                img2.type = Image.Type.Sliced;
                img2.color = Color.white;
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
            var now = GameClock.TimeOfDay;
            var target = new TimeSpan(hour, minute, 0);
            var diff = target - now;
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
                    fn.StartsWith("Mono") || fn.StartsWith("mscorlib") ||
                    fn.StartsWith("TMPro") || fn.StartsWith("Unity.")) continue;
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
            if (member is FieldInfo fi) return fi.GetValue(obj) as Item;
            return null;
        }

        // Called from OnSetupItemHoveringUI - finds the slot by matching the exact Item instance.
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
                var seen = new HashSet<int>();

                foreach (var obj in slots)
                {
                    var mb = obj as MonoBehaviour;
                    if (mb == null || !mb.gameObject.activeInHierarchy) continue;

                    int id = mb.GetInstanceID();
                    seen.Add(id);

                    var item = ReadItemFromMember(_slotItemMember!, mb);
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
                int id = mb.GetInstanceID();

                if (mb is ItemHoveringUI) continue; // skip tooltip UI
                // Skip HUD/action-bar/button components - not inventory slots
                var tn = compType.Name;
                if (tn.Contains("HUD") || tn.Contains("Status") || tn.Contains("Stamina") ||
                    tn.Contains("Health") || tn.Contains("Energy") || tn.Contains("Equip") ||
                    tn.Contains("Button") || tn.Contains("Weapon") || tn.Contains("Action")) continue;

                var member = FindItemMember(compType);
                if (member == null) continue;

                Item? item;
                try { item = ReadItemFromMember(member, mb); }
                catch { continue; }

                if (item == null) continue; // skip empty slots

                // Discover slot type from any slot holding any item (not just registered ones)
                if (_slotCompType == null)
                {
                    _slotCompType = compType;
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
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-5f, -5f);
            rt.sizeDelta = new Vector2(28f, 28f);

            var circleImg = badge.AddComponent<Image>();
            circleImg.color = new Color(0.13f, 0.65f, 0.28f, 1f);
            circleImg.sprite = GetOrCreateCircleSprite();
            circleImg.type = Image.Type.Simple;

            var txtGo = new GameObject("Check");
            txtGo.transform.SetParent(badge.transform, false);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            tmp.text = "✓";
            tmp.fontSize = 18f;
            tmp.color = Color.white;
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

        // Rounded rect sprite for cards - 9-sliced so it stretches cleanly
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

        // Pill sprite for toggle track - 192×96 with r=48 (= h/2), 9-sliced
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

        // ── Localization ──────────────────────────────────────────────────

        private static PropertyInfo? _locLangProp;
        private static bool _locInit;
        private SystemLanguage _lastLang = SystemLanguage.Unknown;

        private static SystemLanguage GetGameLanguage()
        {
            if (!_locInit)
            {
                _locInit = true;
                try
                {
                    var t = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                        .FirstOrDefault(t => t.FullName == "SodaCraft.Localizations.LocalizationManager");
                    _locLangProp = t?.GetProperty("CurrentLanguage", BindingFlags.Public | BindingFlags.Static);
                }
                catch { }
            }
            try { if (_locLangProp != null) return (SystemLanguage)_locLangProp.GetValue(null)!; }
            catch { }
            return SystemLanguage.English;
        }

        // Translation table: key = English text, value = [fr, de, zh-CN, zh-TW, ja, ko, pt, ru, es]
        private static readonly SystemLanguage[] _langOrder =
        {
            SystemLanguage.French, SystemLanguage.German,
            SystemLanguage.ChineseSimplified, SystemLanguage.ChineseTraditional,
            SystemLanguage.Japanese, SystemLanguage.Korean,
            SystemLanguage.Portuguese, SystemLanguage.Russian, SystemLanguage.Spanish
        };

        private static readonly Dictionary<string, string[]> _t = new Dictionary<string, string[]>
        {
            // Card titles
            ["Item Value"] = new[] { "Valeur d'objet", "Gegenstandswert", "物品价值", "物品價值", "アイテム価格", "아이템 가치", "Valor do Item", "Стоимость предмета", "Valor del objeto" },
            ["Enemies"] = new[] { "Ennemis", "Feinde", "敌人", "敵人", "敵", "적", "Inimigos", "Враги", "Enemigos" },
            ["Item Transfer"] = new[] { "Transfert d'objets", "Gegenstandstransfer", "物品转移", "物品轉移", "アイテム移動", "아이템 이동", "Transferência de Item", "Перенос предметов", "Transferencia de objetos" },
            ["Auto-Close Container"] = new[] { "Fermeture auto conteneur", "Container autom. schließen", "自动关闭容器", "自動關閉容器", "コンテナ自動閉鎖", "컨테이너 자동 닫기", "Fechar Contêiner Auto", "Авто-закрытие контейнера", "Cerrar contenedor auto" },
            ["Weapons"] = new[] { "Armes", "Waffen", "武器", "武器", "武器", "무기", "Armas", "Оружие", "Armas" },
            ["Lootbox Highlight"] = new[] { "Surlignage des conteneurs", "Container-Hervorhebung", "战利品箱高亮", "戰利品箱高亮", "ルートボックス強調", "루트박스 강조", "Destaque de Caixas", "Подсветка контейнеров", "Resaltar cajas de botín" },
            ["Quests"] = new[] { "Quêtes", "Aufgaben", "任务", "任務", "クエスト", "퀘스트", "Missões", "Задания", "Misiones" },
            ["Recorded Items"] = new[] { "Objets enregistrés", "Erfasste Gegenstände", "已记录物品", "已記錄物品", "記録済みアイテム", "기록된 아이템", "Itens Registrados", "Записанные предметы", "Objetos registrados" },
            ["FPS Counter"] = new[] { "Compteur FPS", "FPS-Anzeige", "帧率显示", "幀率顯示", "FPS表示", "FPS 카운터", "Contador FPS", "Счётчик FPS", "Contador FPS" },
            ["Sleep Presets"] = new[] { "Préréglages de sommeil", "Schlaf-Voreinstellungen", "睡眠预设", "睡眠預設", "睡眠プリセット", "수면 프리셋", "Predefinições de Sono", "Пресеты сна", "Preajustes de sueño" },
            // Toggle labels
            ["Show sell value on hover"] = new[] { "Afficher la valeur de vente", "Verkaufswert anzeigen", "悬停显示售价", "懸停顯示售價", "売値をホバー表示", "호버 시 판매가 표시", "Mostrar valor de venda", "Показывать цену продажи", "Mostrar valor de venta" },
            ["Show enemy names"] = new[] { "Afficher les noms ennemis", "Feindnamen anzeigen", "显示敌人名称", "顯示敵人名稱", "敵の名前を表示", "적 이름 표시", "Mostrar nomes inimigos", "Показывать имена врагов", "Mostrar nombres enemigos" },
            ["Auto-unload gun on kill"] = new[] { "Décharger arme à la mort", "Waffe bei Tod entladen", "击杀时自动卸弹", "擊殺時自動卸彈", "キル時に自動アンロード", "처치 시 총알 제거", "Descarregar arma ao matar", "Авто-разрядка при убийстве", "Descargar arma al matar" },
            ["Kill feed"] = new[] { "Fil de mort", "Kill-Feed", "击杀信息流", "擊殺信息流", "キルフィード", "킬 피드", "Feed de abates", "Лента убийств", "Feed de bajas" },
            ["Modifier + click to transfer"] = new[] { "Modificateur + clic", "Modifiziertaste + Klick", "修饰键+点击转移", "修飾鍵+點擊轉移", "修飾キー+クリック移動", "수정키+클릭으로 이동", "Modificador + clique", "Модификатор + клик", "Modificador + clic" },
            ["Close on movement"] = new[] { "Fermer au mouvement", "Bei Bewegung schließen", "移动时关闭", "移動時關閉", "移動で閉じる", "이동 시 닫기", "Fechar ao mover", "Закрыть при движении", "Cerrar al moverse" },
            ["Close on Shift"] = new[] { "Fermer avec Shift", "Mit Shift schließen", "按Shift时关闭", "按Shift時關閉", "Shiftで閉じる", "Shift로 닫기", "Fechar com Shift", "Закрыть при Shift", "Cerrar con Shift" },
            ["Close on Space"] = new[] { "Fermer avec Espace", "Mit Leertaste schließen", "按空格时关闭", "按空格時關閉", "スペースで閉じる", "스페이스로 닫기", "Fechar com Espaço", "Закрыть при Пробеле", "Cerrar con Espacio" },
            ["Close on damage"] = new[] { "Fermer en cas de dégâts", "Bei Schaden schließen", "受伤时关闭", "受傷時關閉", "ダメージで閉じる", "피격 시 닫기", "Fechar ao tomar dano", "Закрыть при уроне", "Cerrar al recibir daño" },
            ["Skip melee on scroll"] = new[] { "Ignorer mêlée au défilement", "Nahkampf beim Scrollen skippen", "滚轮跳过近战", "滾輪跳過近戰", "スクロールで近接スキップ", "스크롤 시 근접 건너뜀", "Pular melee no scroll", "Пропуск ближнего боя", "Saltar melee al girar" },
            ["Highlight loot containers"] = new[] { "Surligner les conteneurs", "Loot-Container hervorheben", "高亮战利品容器", "高亮戰利品容器", "ルートコンテナを強調", "루트 컨테이너 강조", "Destacar contêineres", "Подсветка контейнеров", "Resaltar contenedores" },
            ["Only unsearched"] = new[] { "Seulement non fouillés", "Nur Ungesucht", "仅未搜寻", "僅未搜尋", "未探索のみ", "미수색만", "Apenas não vasculhados", "Только необысканные", "Solo sin registrar" },
            ["Quest favorites (N key)"] = new[] { "Favoris (touche N)", "Favoriten (Taste N)", "收藏任务 (N键)", "收藏任務 (N鍵)", "お気に入り (Nキー)", "즐겨찾기 (N키)", "Favoritos (tecla N)", "Избранные (клавиша N)", "Favoritos (tecla N)" },
            ["Show badge on recorded items"] = new[] { "Badge sur objets enregistrés", "Badge auf erfassten Gegenst.", "显示已记录徽章", "顯示已記錄徽章", "記録済みバッジ表示", "기록된 아이템 뱃지", "Badge em itens registrados", "Значок на записанных", "Insignia en registrados" },
            ["Show FPS counter"] = new[] { "Afficher compteur FPS", "FPS-Anzeige einblenden", "显示帧率计数器", "顯示幀率計數器", "FPSカウンターを表示", "FPS 카운터 표시", "Mostrar contador FPS", "Показывать счётчик FPS", "Mostrar contador FPS" },
            ["Hide controls hint"] = new[] { "Masquer l'aide de contrôle", "Steuerungshinweis ausblenden", "隐藏操作提示", "隱藏操作提示", "操作ヒントを非表示", "조작 힌트 숨기기", "Ocultar dica de controles", "Скрыть подсказку управления", "Ocultar ayuda de controles" },
            ["Remember camera view"] = new[] { "Mémoriser la vue caméra", "Kameraansicht merken", "记住相机视角", "記住相機視角", "カメラビューを記憶", "카메라 뷰 기억", "Lembrar visão de câmera", "Запомнить вид камеры", "Recordar vista de cámara" },
            ["Hide HUD on ADS"] = new[] { "Masquer HUD en visée", "HUD beim Zielen ausblenden", "瞄准时隐藏HUD", "瞄準時隱藏HUD", "ADS中HUDを非表示", "ADS 시 HUD 숨기기", "Ocultar HUD ao mirar", "Скрывать HUD при прицеливании", "Ocultar HUD al apuntar" },
            ["Hides the entire HUD while holding right-click (OFF by default)"] = new[] { "Cache tout le HUD lors du clic droit", "Blendet HUD bei Rechtsklick aus", "右键时隐藏全部HUD", "右鍵時隱藏全部HUD", "右クリック中HUD全体を非表示", "우클릭 중 전체 HUD 숨김", "Oculta HUD ao segurar clique direito", "Скрывает весь HUD при ПКМ", "Oculta HUD al mantener clic derecho" },
            ["Hide ammo on ADS"] = new[] { "Masquer munitions en visée", "Munition beim Zielen ausblenden", "瞄准时隐藏弹药", "瞄準時隱藏彈藥", "ADS中弾薬を非表示", "ADS 시 탄약 숨기기", "Ocultar munição ao mirar", "Скрывать боеприпасы при прицеливании", "Ocultar munición al apuntar" },
            ["Also hides bullet type and ammo count during ADS"] = new[] { "Cache aussi le type et la quantité de munitions", "Versteckt auch Munitionstyp und -anzahl beim Zielen", "同时隐藏弹药类型和数量", "同時隱藏彈藥類型和數量", "弾薬の種類と数もADS中に非表示", "ADS 중 탄약 종류 및 수량도 숨김", "Também oculta tipo e quantidade de munição", "Также скрывает тип и количество боеприпасов", "También oculta tipo y cantidad de munición" },
            ["Wake-up preset buttons"] = new[] { "Boutons de réveil", "Aufwach-Schnelltasten", "醒来预设按钮", "醒來預設按鈕", "起床プリセット", "기상 프리셋 버튼", "Botões de acordar", "Кнопки пробуждения", "Botones de despertar" },
            // Sub-labels
            ["Display mode"] = new[] { "Mode d'affichage", "Anzeigemodus", "显示模式", "顯示模式", "表示モード", "표시 모드", "Modo de exibição", "Режим отображения", "Modo de visualización" },
            ["Modifier key"] = new[] { "Touche modificatrice", "Modifiziertaste", "修饰键", "修飾鍵", "修飾キー", "수정 키", "Tecla modificadora", "Клавиша-модификатор", "Tecla modificadora" },
            // Preset labels
            ["Preset 1"] = new[] { "Préréglage 1", "Voreinstellung 1", "预设 1", "預設 1", "プリセット 1", "프리셋 1", "Predefinição 1", "Пресет 1", "Preajuste 1" },
            ["Preset 2"] = new[] { "Préréglage 2", "Voreinstellung 2", "预设 2", "預設 2", "プリセット 2", "프리셋 2", "Predefinição 2", "Пресет 2", "Preajuste 2" },
            ["Preset 3"] = new[] { "Préréglage 3", "Voreinstellung 3", "预设 3", "預設 3", "プリセット 3", "프리셋 3", "Predefinição 3", "Пресет 3", "Preajuste 3" },
            ["Preset 4"] = new[] { "Préréglage 4", "Voreinstellung 4", "预设 4", "預設 4", "プリセット 4", "프리셋 4", "Predefinição 4", "Пресет 4", "Preajuste 4" },
            // Close button
            ["Close"] = new[] { "Fermer", "Schließen", "关闭", "關閉", "閉じる", "닫기", "Fechar", "Закрыть", "Cerrar" },
            ["open / close"] = new[] { "ouvrir / fermer", "öffnen / schließen", "打开 / 关闭", "打開 / 關閉", "開く / 閉じる", "열기 / 닫기", "abrir / fechar", "открыть / закрыть", "abrir / cerrar" },
            // Descriptions
            ["Shows sell price at any time"] = new[] { "Affiche le prix de vente", "Zeigt Verkaufspreis jederzeit", "随时显示售价", "隨時顯示售價", "いつでも売値を表示", "언제든 판매가 표시", "Mostra preço de venda", "Показывает цену продажи", "Muestra precio de venta" },
            ["Displayed above their health bar"] = new[] { "Au-dessus de la barre de vie", "Über der Lebensanzeige", "显示在血条上方", "顯示在血條上方", "ヘルスバーの上に表示", "체력바 위에 표시", "Exibido acima da barra de vida", "Отображается над полоской здоровья", "Sobre la barra de vida" },
            ["Moves ammo to enemy stash when you kill them"] = new[] { "Munitions dans le loot ennemi", "Munition bei Tod in Loot", "击杀时弹药移至战利品", "擊殺時彈藥移至戰利品", "キル時に弾薬をスタッシュへ", "처치 시 탄약을 전리품으로", "Move munição para o loot", "Перемещает патроны в лут", "Mueve munición al botín" },
            ["Shows kills in the top-right corner during raids"] = new[] { "Affiche les kills pendant raids", "Zeigt Kills während Raids", "突袭中显示击杀", "突襲中顯示擊殺", "レイド中にキルを表示", "레이드 중 킬 표시", "Mostra abates durante raids", "Показывает убийства в рейде", "Muestra bajas durante raids" },
            ["Moves items between container and backpack"] = new[] { "Entre conteneur et sac à dos", "Zwischen Container und Rucksack", "物品在容器和背包间移动", "物品在容器和背包間移動", "コンテナとバッグ間でアイテム移動", "컨테이너와 백팩 간 이동", "Move itens entre contêiner e mochila", "Между контейнером и рюкзаком", "Mueve entre contenedor y mochila" },
            ["W / A / S / D keys"] = new[] { "Touches W / A / S / D", "W / A / S / D Tasten", "W/A/S/D键", "W/A/S/D鍵", "W/A/S/Dキー", "W/A/S/D 키", "Teclas W/A/S/D", "Клавиши W/A/S/D", "Teclas W/A/S/D" },
            ["When pressing Shift"] = new[] { "En appuyant sur Shift", "Beim Drücken von Shift", "按下Shift时", "按下Shift時", "Shiftを押したとき", "Shift 누를 때", "Ao pressionar Shift", "При нажатии Shift", "Al presionar Shift" },
            ["When pressing Space"] = new[] { "En appuyant sur Espace", "Beim Drücken der Leertaste", "按下空格时", "按下空格時", "スペースを押したとき", "스페이스 누를 때", "Ao pressionar Espaço", "При нажатии Пробела", "Al presionar Espacio" },
            ["When taking a hit"] = new[] { "En prenant un coup", "Beim Treffer", "受到攻击时", "受到攻擊時", "ヒットを受けたとき", "피격 시", "Ao receber dano", "При получении урона", "Al recibir un golpe" },
            ["Scroll wheel skips the melee slot"] = new[] { "La molette ignore la mêlée", "Mausrad überspringt Nahkampf", "滚轮跳过近战槽", "滾輪跳過近戰槽", "スクロールで近接スロットスキップ", "스크롤로 근접 슬롯 건너뜀", "Roda pula o slot de melee", "Прокрутка пропускает ближний бой", "Rueda omite el cuerpo a cuerpo" },
            ["Gold outline on loot boxes in the world"] = new[] { "Contour doré sur les caisses", "Goldener Umriss auf Loot-Kisten", "战利品箱金色边框", "戰利品箱金色邊框", "ルートボックスに金縁", "루트 박스에 금테", "Contorno dourado nas caixas", "Золотой контур на контейнерах", "Contorno dorado en cajas" },
            ["Hides outline on already-opened containers"] = new[] { "Cache contour des déjà ouverts", "Versteckt Umriss geöffneter Behälter", "隐藏已打开容器的边框", "隱藏已打開容器的邊框", "開けたコンテナの縁を隠す", "열린 컨테이너 외곽선 숨김", "Oculta contorno de já abertos", "Скрывает контур открытых контейнеров", "Oculta contorno de abiertos" },
            ["Press N on a selected quest to pin it to the top of the list"] = new[] { "N pour épingler la quête", "N drücken zum Anheften", "按N固定任务至顶部", "按N固定任務至頂部", "NでクエストをTOPに固定", "퀘스트 선택 후 N로 상단 고정", "Pressione N para fixar missão", "Нажмите N чтобы закрепить задание", "Presiona N para fijar misión" },
            ["Green ✓ on blueprints and master keys"] = new[] { "✓ vert sur blueprints et clés", "Grünes ✓ auf Blueprints und Schlüsseln", "蓝图和主钥匙上绿色✓", "藍圖和主鑰匙上綠色✓", "設計図とマスターキーに緑✓", "설계도와 마스터키에 녹색✓", "✓ verde em blueprints e chaves", "Зелёный ✓ на чертежах и ключах", "✓ verde en blueprints y llaves" },
            ["Displayed in the top-right corner"] = new[] { "Affiché en haut à droite", "Oben rechts angezeigt", "显示在右上角", "顯示在右上角", "右上に表示", "우측 상단에 표시", "Exibido no canto superior direito", "Отображается в правом верхнем углу", "Mostrado en esquina superior derecha" },
            ["Hides the 'Controls [O]' button in the HUD"] = new[] { "Cache le bouton 'Contrôles [O]'", "Versteckt 'Steuerung [O]'-Taste", "隐藏'操作[O]'按钮", "隱藏'操作[O]'按鈕", "'操作[O]'ボタンを非表示", "'조작[O]' 버튼 숨기기", "Oculta botão 'Controles [O]'", "Скрывает кнопку 'Управление [O]'", "Oculta botón 'Controles [O]'" },
            ["Restores top-down or default view between sessions"] = new[] { "Restaure la vue entre sessions", "Stellt Ansicht zwischen Sessions her", "会话间恢复相机视角", "會話間恢復相機視角", "セッション間でビューを復元", "세션 간 뷰 복원", "Restaura visão entre sessões", "Восстанавливает вид между сессиями", "Restaura vista entre sesiones" },
            ["Adds preset buttons to the sleep screen"] = new[] { "Ajoute des boutons de réveil", "Fügt Schlaf-Schnelltasten hinzu", "在睡眠界面添加预设按钮", "在睡眠介面添加預設按鈕", "睡眠画面にプリセットボタン追加", "수면 화면에 프리셋 버튼 추가", "Adiciona botões na tela de dormir", "Добавляет кнопки на экран сна", "Añade botones en pantalla de sueño" },
            // ModConfig labels (re-registered on language change)
            ["Sleep preset buttons"] = new[] { "Boutons de préréglage sommeil", "Schlaf-Voreinstellungs-Tasten", "睡眠预设按钮", "睡眠預設按鈕", "睡眠プリセットボタン", "수면 프리셋 버튼", "Botões de preset de sono", "Кнопки предустановки сна", "Botones de preajuste de sueño" },
            ["Lootbox highlight"] = new[] { "Surbrillance des caisses", "Container-Hervorhebung", "战利品箱高亮", "戰利品箱高亮", "ルートボックス強調", "루트박스 강조", "Destaque de caixas", "Подсветка контейнеров", "Resaltar cajas de botín" },
            ["Lootbox highlight: only unsearched"] = new[] { "Surbrillance: non fouillés seul.", "Hervorhebung: nur Ungesucht", "高亮：仅未搜寻", "高亮：僅未搜尋", "強調：未探索のみ", "강조: 미수색만", "Destaque: apenas não vasc.", "Подсветка: только необысканные", "Resaltar: solo sin registrar" },
            ["FPS counter"] = new[] { "Compteur FPS", "FPS-Anzeige", "帧率计数器", "幀率計數器", "FPSカウンター", "FPS 카운터", "Contador FPS", "Счётчик FPS", "Contador FPS" },
            ["Recorded items badge"] = new[] { "Badge objets enregistrés", "Badge erfasste Gegenstände", "已记录物品徽章", "已記錄物品徽章", "記録済みバッジ", "기록된 아이템 뱃지", "Badge de itens registrados", "Значок записанных предметов", "Insignia de objetos registrados" },
            ["Auto-close on movement (WASD)"] = new[] { "Fermeture auto au mouvement", "Auto-Schließen bei Bewegung", "移动时自动关闭 (WASD)", "移動時自動關閉 (WASD)", "移動で自動閉鎖 (WASD)", "이동 시 자동 닫기 (WASD)", "Fechar auto ao mover (WASD)", "Авто-закрытие при движении (WASD)", "Cerrar auto al mover (WASD)" },
            ["Auto-close on Shift"] = new[] { "Fermeture auto sur Shift", "Auto-Schließen bei Shift", "按Shift时自动关闭", "按Shift時自動關閉", "Shiftで自動閉鎖", "Shift로 자동 닫기", "Fechar auto com Shift", "Авто-закрытие при Shift", "Cerrar auto con Shift" },
            ["Auto-close on Space"] = new[] { "Fermeture auto sur Espace", "Auto-Schließen bei Leertaste", "按空格时自动关闭", "按空格時自動關閉", "スペースで自動閉鎖", "스페이스로 자동 닫기", "Fechar auto com Espaço", "Авто-закрытие при Пробеле", "Cerrar auto con Espacio" },
            ["Auto-close on damage"] = new[] { "Fermeture auto aux dégâts", "Auto-Schließen bei Schaden", "受伤时自动关闭", "受傷時自動關閉", "ダメージで自動閉鎖", "피격 시 자동 닫기", "Fechar auto ao tomar dano", "Авто-закрытие при уроне", "Cerrar auto al recibir daño" },
            ["Sell value display mode"] = new[] { "Mode d'affichage de la valeur", "Anzeigemodus für Verkaufswert", "售价显示模式", "售價顯示模式", "売値表示モード", "판매가 표시 모드", "Modo de exibição de valor", "Режим отображения цены", "Modo de visualización de valor" },
            ["Sell value on hover"] = new[] { "Valeur de vente au survol", "Verkaufswert beim Hover", "悬停售价", "懸停售價", "ホバー時の売値", "호버 시 판매가", "Valor de venda ao passar", "Цена продажи при наведении", "Valor de venta al pasar" },
            ["Combined"] = new[] { "Combiné", "Kombiniert", "组合", "組合", "複合", "복합", "Combinado", "Комбинированный", "Combinado" },
            ["Single only"] = new[] { "Unité seulement", "Nur einzeln", "仅单个", "僅單個", "単体のみ", "단일만", "Somente unitário", "Только единица", "Solo unitario" },
            ["Stack only"] = new[] { "Pile seulement", "Nur Stapel", "仅堆叠", "僅堆疊", "スタックのみ", "스택만", "Somente pilha", "Только стопка", "Solo pila" },
            ["Item transfer"] = new[] { "Transfert d'objets", "Gegenstandstransfer", "物品转移", "物品轉移", "アイテム移動", "아이템 이동", "Transferência de item", "Перенос предметов", "Transferencia de objetos" },
            ["Disabled"] = new[] { "Désactivé", "Deaktiviert", "禁用", "禁用", "無効", "비활성화", "Desativado", "Отключено", "Desactivado" },
            ["Enabled"] = new[] { "Activé", "Aktiviert", "启用", "啟用", "有効", "활성화", "Ativado", "Включено", "Activado" },
            ["Shift + Left Click"] = new[] { "Shift+Clic gauche", "Shift+Linksklick", "Shift+左键单击", "Shift+左鍵單擊", "Shift+左クリック", "Shift+좌클릭", "Shift+Clique esquerdo", "Shift+Левый клик", "Shift+Clic izquierdo" },
            ["Alt + Left Click"] = new[] { "Alt+Clic gauche", "Alt+Linksklick", "Alt+左键单击", "Alt+左鍵單擊", "Alt+左クリック", "Alt+좌클릭", "Alt+Clique esquerdo", "Alt+Левый клик", "Alt+Clic izquierdo" },
            ["Preset 1 - hour"] = new[] { "Prérégl. 1 - heure", "Voreinst. 1 - Stunde", "预设 1 - 小时", "預設 1 - 小時", "プリセット 1 - 時間", "프리셋 1 - 시간", "Predefinição 1 - hora", "Пресет 1 - час", "Preajuste 1 - hora" },
            ["Preset 1 - min"] = new[] { "Prérégl. 1 - min", "Voreinst. 1 - Min.", "预设 1 - 分钟", "預設 1 - 分鐘", "プリセット 1 - 分", "프리셋 1 - 분", "Predefinição 1 - min", "Пресет 1 - мин", "Preajuste 1 - min" },
            ["Preset 2 - hour"] = new[] { "Prérégl. 2 - heure", "Voreinst. 2 - Stunde", "预设 2 - 小时", "預設 2 - 小時", "プリセット 2 - 時間", "프리셋 2 - 시간", "Predefinição 2 - hora", "Пресет 2 - час", "Preajuste 2 - hora" },
            ["Preset 2 - min"] = new[] { "Prérégl. 2 - min", "Voreinst. 2 - Min.", "预设 2 - 分钟", "預設 2 - 分鐘", "プリセット 2 - 分", "프리셋 2 - 분", "Predefinição 2 - min", "Пресет 2 - мин", "Preajuste 2 - min" },
            ["Preset 3 - hour"] = new[] { "Prérégl. 3 - heure", "Voreinst. 3 - Stunde", "预设 3 - 小时", "預設 3 - 小時", "プリセット 3 - 時間", "프리셋 3 - 시간", "Predefinição 3 - hora", "Пресет 3 - час", "Preajuste 3 - hora" },
            ["Preset 3 - min"] = new[] { "Prérégl. 3 - min", "Voreinst. 3 - Min.", "预设 3 - 分钟", "預設 3 - 分鐘", "プリセット 3 - 分", "프리셋 3 - 분", "Predefinição 3 - min", "Пресет 3 - мин", "Preajuste 3 - min" },
            ["Preset 4 - hour"] = new[] { "Prérégl. 4 - heure", "Voreinst. 4 - Stunde", "预设 4 - 小时", "預設 4 - 小時", "プリセット 4 - 時間", "프리셋 4 - 시간", "Predefinição 4 - hora", "Пресет 4 - час", "Preajuste 4 - hora" },
            ["Preset 4 - min"] = new[] { "Prérégl. 4 - min", "Voreinst. 4 - Min.", "预设 4 - 分钟", "預設 4 - 分鐘", "プリセット 4 - 分", "프리셋 4 - 분", "Predefinição 4 - min", "Пресет 4 - мин", "Preajuste 4 - min" },
        };

        private static string L(string key)
        {
            var lang = GetGameLanguage();
            int idx = Array.IndexOf(_langOrder, lang);
            if (idx >= 0 && _t.TryGetValue(key, out var arr) && idx < arr.Length)
                return arr[idx];
            return key;
        }

        private void RebuildSettingsPanel()
        {
            bool wasOpen = _menuOpen;
            if (_settingsCanvas != null) { UnityEngine.Object.Destroy(_settingsCanvas); _settingsCanvas = null; _settingsCanvasComp = null; }
            BuildSettingsPanel();
            if (wasOpen) SetMenuVisible(true);
            // Re-register ModConfig labels in the new language (delegate registration is skipped via _mcDelegateRegistered)
            if (_mcAPI != null) _mcChecked = false;
        }

        // ── Settings Panel ────────────────────────────────────────────────

        private void BuildSettingsPanel()
        {
            _lastLang = GetGameLanguage();
            _settingsCanvas = new GameObject("AllInOneMod_m0n0t0ny_Canvas");
            DontDestroyOnLoad(_settingsCanvas);
            var canvas = _settingsCanvas.AddComponent<Canvas>();
            _settingsCanvasComp = canvas;
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            _settingsCanvas.AddComponent<CanvasScaler>();
            _settingsCanvas.AddComponent<GraphicRaycaster>();

            // Panel - rounded outer container, auto-sizes vertically, 800px wide, centered
            var panel = new GameObject("Panel");
            panel.transform.SetParent(_settingsCanvas.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(800f, 0f);
            var panelImg = panel.AddComponent<Image>();
            panelImg.sprite = GetOrCreateRoundedRectSprite();
            panelImg.type = Image.Type.Sliced;
            panelImg.color = new Color(0.047f, 0.047f, 0.055f, 0.98f);
            var panelMask = panel.AddComponent<Mask>();
            panelMask.showMaskGraphic = true;
            var panelVLG = panel.AddComponent<VerticalLayoutGroup>();
            panelVLG.childAlignment = TextAnchor.UpperCenter;
            panelVLG.childForceExpandWidth = true;
            panelVLG.childForceExpandHeight = false;
            panelVLG.spacing = 0f;
            panelVLG.padding = new RectOffset(0, 0, 0, 0);
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Header ────────────────────────────────────────────────────
            var header = LChild(panel, "Header", 54f);
            header.GetComponent<Image>().color = new Color(0.060f, 0.060f, 0.072f, 1f);
            var hHLG = header.AddComponent<HorizontalLayoutGroup>();
            hHLG.padding = new RectOffset(16, 16, 0, 0);
            hHLG.childAlignment = TextAnchor.MiddleLeft;
            hHLG.childForceExpandHeight = true;
            hHLG.childForceExpandWidth = false;
            hHLG.spacing = 0f;
            var titleGo = LText(header, "Title", "All In One - m0n0t0ny's Mod", 15f, flexW: 1f);
            var titleTMP = titleGo.GetComponent<TextMeshProUGUI>();
            titleTMP.color = Color.white;
            titleTMP.fontStyle = FontStyles.Bold;
            titleTMP.alignment = TextAlignmentOptions.Left;
            var verGo = LText(header, "Ver", "v2.9", 10f, prefW: 44f);
            verGo.GetComponent<TextMeshProUGUI>().color = new Color(1f, 0.75f, 0f, 1f);
            verGo.GetComponent<TextMeshProUGUI>().alignment = TextAlignmentOptions.Right;

            // Gold accent line
            var accent = LChild(panel, "Accent", 2f);
            accent.GetComponent<Image>().color = new Color(1f, 0.75f, 0f, 1f);

            // ── Cards box - equal 16px padding on all four sides ─────────
            var cardsBox = new GameObject("CardsBox");
            cardsBox.transform.SetParent(panel.transform, false);
            cardsBox.AddComponent<RectTransform>();
            cardsBox.AddComponent<Image>().color = Color.clear;
            var cbVLG = cardsBox.AddComponent<VerticalLayoutGroup>();
            cbVLG.padding = new RectOffset(16, 16, 16, 16);
            cbVLG.spacing = 0f;
            cbVLG.childForceExpandWidth = true;
            cbVLG.childForceExpandHeight = false;
            cardsBox.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // ── Three-column content area ─────────────────────────────────
            var content = new GameObject("Content");
            content.transform.SetParent(cardsBox.transform, false);
            content.AddComponent<RectTransform>();
            content.AddComponent<Image>().color = Color.clear;
            var cHLG = content.AddComponent<HorizontalLayoutGroup>();
            cHLG.padding = new RectOffset(0, 0, 0, 0);
            cHLG.spacing = 12f;
            cHLG.childAlignment = TextAnchor.UpperLeft;
            cHLG.childForceExpandWidth = true;
            cHLG.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var col1 = LColumn(content, "Col1");
            var col2 = LColumn(content, "Col2");
            var col3 = LColumn(content, "Col3");

            // ── COL 1: Item Value ─────────────────────────────────────────
            var c1v = LCard(col1, L("Item Value"));

            var (tRow, tImg, tThumb) = LToggleRow(c1v, L("Show sell value on hover"),
                L("Shows sell price at any time"));
            _toggleBtnImage = tImg;
            _toggleBtnThumb = tThumb;
            tRow.GetComponentInChildren<Button>().onClick.AddListener(OnToggleClicked);
            RefreshToggleButton();

            LSubLabel(c1v, L("Display mode"));

            var modeRow = LChild(c1v, "ModeRow", 30f);
            modeRow.GetComponent<Image>().color = Color.clear;
            var mHLG = modeRow.AddComponent<HorizontalLayoutGroup>();
            mHLG.spacing = 6f;
            mHLG.childForceExpandWidth = true;
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
            var c1e = LCard(col1, L("Enemies"));

            var (enRow, enImg, enThumb) = LToggleRow(c1e, L("Show enemy names"),
                L("Displayed above their health bar"));
            _enemyNamesToggleImage = enImg;
            _enemyNamesToggleThumb = enThumb;
            enRow.GetComponentInChildren<Button>().onClick.AddListener(OnEnemyNamesToggleClicked);
            RefreshEnemyNamesToggle();

            var (auRow, auImg, auThumb) = LToggleRow(c1e, L("Auto-unload gun on kill"),
                L("Moves ammo to enemy stash when you kill them"));
            _autoUnloadToggleImage = auImg;
            _autoUnloadToggleThumb = auThumb;
            auRow.GetComponentInChildren<Button>().onClick.AddListener(OnAutoUnloadToggleClicked);
            RefreshAutoUnloadToggle();

            var (kfRow, kfImg, kfThumb) = LToggleRow(c1e, L("Kill feed"),
                L("Shows kills in the top-right corner during raids"));
            _killFeedToggleImage = kfImg;
            _killFeedToggleThumb = kfThumb;
            kfRow.GetComponentInChildren<Button>().onClick.AddListener(OnKillFeedToggleClicked);
            RefreshKillFeedToggle();

            // ── COL 1: Item Transfer ──────────────────────────────────────
            var c1t = LCard(col1, L("Item Transfer"));

            var (trRow, trImg, trThumb) = LToggleRow(c1t, L("Modifier + click to transfer"),
                L("Moves items between container and backpack"));
            _transferToggleImage = trImg;
            _transferToggleThumb = trThumb;
            trRow.GetComponentInChildren<Button>().onClick.AddListener(OnTransferToggleClicked);
            RefreshTransferToggle();

            LSubLabel(c1t, L("Modifier key"));

            var modRow = LChild(c1t, "ModRow", 30f);
            modRow.GetComponent<Image>().color = Color.clear;
            var modHLG = modRow.AddComponent<HorizontalLayoutGroup>();
            modHLG.spacing = 6f;
            modHLG.childForceExpandWidth = true;
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
            var c2ac = LCard(col2, L("Auto-Close Container"));

            var acLabels = new (string name, string desc)[]
            {
                (L("Close on movement"), L("W / A / S / D keys")),
                (L("Close on Shift"),    L("When pressing Shift")),
                (L("Close on Space"),    L("When pressing Space")),
                (L("Close on damage"),   L("When taking a hit")),
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
            var c2w = LCard(col2, L("Weapons"));

            var (smRow, smImg, smThumb) = LToggleRow(c2w, L("Skip melee on scroll"),
                L("Scroll wheel skips the melee slot"));
            _skipMeleeToggleImage = smImg;
            _skipMeleeToggleThumb = smThumb;
            smRow.GetComponentInChildren<Button>().onClick.AddListener(OnSkipMeleeToggleClicked);
            RefreshSkipMeleeToggle();

            // ── COL 2: Lootbox Highlight ──────────────────────────────────
            var c2lb = LCard(col2, L("Lootbox Highlight"));

            var (lbRow, lbImg, lbThumb) = LToggleRow(c2lb, L("Highlight loot containers"),
                L("Gold outline on loot boxes in the world"));
            _lootboxHLToggleImage = lbImg;
            _lootboxHLToggleThumb = lbThumb;
            lbRow.GetComponentInChildren<Button>().onClick.AddListener(OnLootboxHLToggleClicked);
            RefreshLootboxHLToggle();

            var (lbuRow, lbuImg, lbuThumb) = LToggleRow(c2lb, L("Only unsearched"),
                L("Hides outline on already-opened containers"));
            _lootboxHLUnsearchedToggleImage = lbuImg;
            _lootboxHLUnsearchedToggleThumb = lbuThumb;
            lbuRow.GetComponentInChildren<Button>().onClick.AddListener(OnLootboxHLUnsearchedToggleClicked);
            RefreshLootboxHLUnsearchedToggle();

            // ── COL 2: Quest Favorites ────────────────────────────────────
            var c2qf = LCard(col2, L("Quests"));

            var (qfRow, qfImg, qfThumb) = LToggleRow(c2qf, L("Quest favorites (N key)"),
                L("Press N on a selected quest to pin it to the top of the list"));
            _questFavToggleImage = qfImg;
            _questFavToggleThumb = qfThumb;
            qfRow.GetComponentInChildren<Button>().onClick.AddListener(OnQuestFavToggleClicked);
            RefreshQuestFavToggle();

            // ── COL 3: Recorded Items ─────────────────────────────────────
            var c3fr = LCard(col3, L("Recorded Items"));

            var (frRow, frImg, frThumb) = LToggleRow(c3fr, L("Show badge on recorded items"),
                L("Green ✓ on blueprints and master keys"));
            _recorderToggleImage = frImg;
            _recorderToggleThumb = frThumb;
            frRow.GetComponentInChildren<Button>().onClick.AddListener(OnRecorderBadgeToggleClicked);
            RefreshRecorderBadgeToggle();

            // ── COL 3: FPS Counter ────────────────────────────────────────
            var c3fps = LCard(col3, L("FPS Counter"));

            var (fpsRow, fpsImg, fpsThumb) = LToggleRow(c3fps, L("Show FPS counter"),
                L("Displayed in the top-right corner"));
            _fpsToggleImage = fpsImg;
            _fpsToggleThumb = fpsThumb;
            fpsRow.GetComponentInChildren<Button>().onClick.AddListener(OnFpsToggleClicked);
            RefreshFpsToggle();

            var (hcRow, hcImg, hcThumb) = LToggleRow(c3fps, L("Hide controls hint"),
                L("Hides the 'Controls [O]' button in the HUD"));
            _hideCtrlToggleImage = hcImg;
            _hideCtrlToggleThumb = hcThumb;
            hcRow.GetComponentInChildren<Button>().onClick.AddListener(OnHideCtrlToggleClicked);
            RefreshHideCtrlToggle();

            var (cvRow, cvImg, cvThumb) = LToggleRow(c3fps, L("Remember camera view"),
                L("Restores top-down or default view between sessions"));
            _cameraViewToggleImage = cvImg;
            _cameraViewToggleThumb = cvThumb;
            cvRow.GetComponentInChildren<Button>().onClick.AddListener(OnCameraViewToggleClicked);
            RefreshCameraViewToggle();

            var (adsRow, adsImg, adsThumb) = LToggleRow(c3fps, L("Hide HUD on ADS"),
                L("Hides the entire HUD while holding right-click (OFF by default)"));
            _hideHudAdsToggleImage = adsImg;
            _hideHudAdsToggleThumb = adsThumb;
            adsRow.GetComponentInChildren<Button>().onClick.AddListener(OnHideHudAdsToggleClicked);
            RefreshHideHudAdsToggle();

            var (ammoAdsRow, ammoAdsImg, ammoAdsThumb) = LToggleRow(c3fps, L("Hide ammo on ADS"),
                L("Also hides bullet type and ammo count during ADS"));
            _hideAmmoAdsToggleImage = ammoAdsImg;
            _hideAmmoAdsToggleThumb = ammoAdsThumb;
            ammoAdsRow.GetComponentInChildren<Button>().onClick.AddListener(OnHideAmmoAdsToggleClicked);
            RefreshHideAmmoAdsToggle();

            // ── COL 3: Sleep Presets ──────────────────────────────────────
            var c3sp = LCard(col3, L("Sleep Presets"));

            var (stRow, stImg, stThumb) = LToggleRow(c3sp, L("Wake-up preset buttons"),
                L("Adds preset buttons to the sleep screen"));
            _sleepToggleImage = stImg;
            _sleepToggleThumb = stThumb;
            stRow.GetComponentInChildren<Button>().onClick.AddListener(OnSleepToggleClicked);
            RefreshSleepToggle();

            LPickerRow(c3sp, L("Preset 1"),
                () => _preset1Hour,
                v =>
                {
                    _preset1Hour = v; PlayerPrefs.SetInt(PREF_PRESET1H, v); PlayerPrefs.Save();
                    if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}";
                },
                () => _preset1Min,
                v =>
                {
                    _preset1Min = v; PlayerPrefs.SetInt(PREF_PRESET1M, v); PlayerPrefs.Save();
                    if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}";
                });

            LPickerRow(c3sp, L("Preset 2"),
                () => _preset2Hour,
                v =>
                {
                    _preset2Hour = v; PlayerPrefs.SetInt(PREF_PRESET2H, v); PlayerPrefs.Save();
                    if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}";
                },
                () => _preset2Min,
                v =>
                {
                    _preset2Min = v; PlayerPrefs.SetInt(PREF_PRESET2M, v); PlayerPrefs.Save();
                    if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}";
                });

            LPickerRow(c3sp, L("Preset 3"),
                () => _preset3Hour,
                v =>
                {
                    _preset3Hour = v; PlayerPrefs.SetInt(PREF_PRESET3H, v); PlayerPrefs.Save();
                    if (_preset3BtnLabel != null) _preset3BtnLabel.text = $"{_preset3Hour:D2}:{_preset3Min:D2}";
                },
                () => _preset3Min,
                v =>
                {
                    _preset3Min = v; PlayerPrefs.SetInt(PREF_PRESET3M, v); PlayerPrefs.Save();
                    if (_preset3BtnLabel != null) _preset3BtnLabel.text = $"{_preset3Hour:D2}:{_preset3Min:D2}";
                });

            LPickerRow(c3sp, L("Preset 4"),
                () => _preset4Hour,
                v =>
                {
                    _preset4Hour = v; PlayerPrefs.SetInt(PREF_PRESET4H, v); PlayerPrefs.Save();
                    if (_preset4BtnLabel != null) _preset4BtnLabel.text = $"{_preset4Hour:D2}:{_preset4Min:D2}";
                },
                () => _preset4Min,
                v =>
                {
                    _preset4Min = v; PlayerPrefs.SetInt(PREF_PRESET4M, v); PlayerPrefs.Save();
                    if (_preset4BtnLabel != null) _preset4BtnLabel.text = $"{_preset4Hour:D2}:{_preset4Min:D2}";
                });

            // ── Bottom bar ────────────────────────────────────────────────
            var bottom = LChild(panel, "Bottom", 52f);
            bottom.GetComponent<Image>().color = new Color(0.040f, 0.040f, 0.050f, 1f);
            var bHLG = bottom.AddComponent<HorizontalLayoutGroup>();
            bHLG.padding = new RectOffset(16, 16, 10, 10);
            bHLG.spacing = 10f;
            bHLG.childAlignment = TextAnchor.MiddleLeft;
            bHLG.childForceExpandHeight = false;
            bHLG.childForceExpandWidth = false;

            var closeGo = new GameObject("CloseBtn");
            closeGo.transform.SetParent(bottom.transform, false);
            closeGo.AddComponent<RectTransform>();
            var closeImg = closeGo.AddComponent<Image>();
            closeImg.sprite = GetOrCreateRoundedRectSprite();
            closeImg.type = Image.Type.Sliced;
            closeImg.color = new Color(0.55f, 0.10f, 0.10f, 1f);
            var closeLe = closeGo.AddComponent<LayoutElement>();
            closeLe.preferredWidth = 110f;
            closeLe.preferredHeight = 32f;
            closeGo.AddComponent<Button>().onClick.AddListener(() => SetMenuVisible(false));
            var cTxtGo = new GameObject("T");
            cTxtGo.transform.SetParent(closeGo.transform, false);
            var cTMP = cTxtGo.AddComponent<TextMeshProUGUI>();
            cTMP.text = $"{L("Close")}  [{MENU_KEY}]"; cTMP.fontSize = 12f;
            cTMP.alignment = TextAlignmentOptions.Center; cTMP.color = Color.white;
            var cTr = cTxtGo.GetComponent<RectTransform>();
            cTr.anchorMin = Vector2.zero; cTr.anchorMax = Vector2.one;
            cTr.sizeDelta = Vector2.zero; cTr.anchoredPosition = Vector2.zero;

            var hintGo = LText(bottom, "Hint", $"[{MENU_KEY}]  {L("open / close")}", 9f, flexW: 1f);
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

        // Column in the three-column content area (VLG, flex width, no ContentSizeFitter - parent HLG handles sizing)
        private static GameObject LColumn(GameObject parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            go.AddComponent<RectTransform>();
            go.AddComponent<Image>().color = Color.clear;
            var vlg = go.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.padding = new RectOffset(0, 0, 0, 0);
            vlg.spacing = 10f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var le = go.AddComponent<LayoutElement>();
            le.minWidth = 0f;
            le.preferredWidth = 0f;
            le.flexibleWidth = 1f;
            return go;
        }

        // Rounded card - one per category, black semi-transparent bg
        private static GameObject LCard(GameObject parent, string categoryTitle)
        {
            var card = new GameObject($"Card_{categoryTitle}");
            card.transform.SetParent(parent.transform, false);
            card.AddComponent<RectTransform>();
            var img = card.AddComponent<Image>();
            img.sprite = GetOrCreateRoundedRectSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(0f, 0f, 0f, 0.72f);
            var vlg = card.AddComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 12);
            vlg.spacing = 6f;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            var csf = card.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

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
                if (flexW >= 0) le.flexibleWidth = flexW;
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
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = false;
            hlg.childForceExpandWidth = false;
            hlg.spacing = 12f;
            hlg.padding = new RectOffset(0, 0, 5, 5);
            var rowCSF = row.AddComponent<ContentSizeFitter>();
            rowCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Text group: name + optional description
            var textGrp = new GameObject("TextGroup");
            textGrp.transform.SetParent(row.transform, false);
            textGrp.AddComponent<RectTransform>();
            var tgVLG = textGrp.AddComponent<VerticalLayoutGroup>();
            tgVLG.childAlignment = TextAnchor.UpperLeft;
            tgVLG.childForceExpandWidth = true;
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
            nameTMP.enableWordWrapping = true;
            nameTMP.overflowMode = TextOverflowModes.Overflow;
            nameGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            if (!string.IsNullOrEmpty(description))
            {
                var descGo = new GameObject("Desc");
                descGo.transform.SetParent(textGrp.transform, false);
                descGo.AddComponent<RectTransform>();
                var descTMP = descGo.AddComponent<TextMeshProUGUI>();
                descTMP.text = description; descTMP.fontSize = 9.5f;
                descTMP.color = new Color(0.38f, 0.38f, 0.48f, 1f);
                descTMP.alignment = TextAlignmentOptions.Left;
                descTMP.enableWordWrapping = true;
                descTMP.overflowMode = TextOverflowModes.Overflow;
                descGo.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }

            // iOS-style toggle track (pill)
            var track = new GameObject("Track");
            track.transform.SetParent(row.transform, false);
            var trackRT = track.AddComponent<RectTransform>();
            var trackImg = track.AddComponent<Image>();
            trackImg.sprite = GetOrCreatePillSprite();
            trackImg.type = Image.Type.Sliced;
            var trackBtn = track.AddComponent<Button>();
            trackBtn.targetGraphic = trackImg;
            var trackLE = track.AddComponent<LayoutElement>();
            trackLE.minWidth = 44f;
            trackLE.preferredWidth = 44f;
            trackLE.minHeight = 24f;
            trackLE.preferredHeight = 24f;
            trackLE.flexibleWidth = 0f;

            // White circle thumb
            var thumb = new GameObject("Thumb");
            thumb.transform.SetParent(track.transform, false);
            var thumbRT = thumb.AddComponent<RectTransform>();
            thumbRT.anchorMin = thumbRT.anchorMax = thumbRT.pivot = new Vector2(0.5f, 0.5f);
            thumbRT.sizeDelta = new Vector2(18f, 18f);
            var thumbImg = thumb.AddComponent<Image>();
            thumbImg.sprite = GetOrCreateCircleSprite();
            thumbImg.color = Color.white;

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
            hlg.childAlignment = TextAnchor.MiddleLeft;
            hlg.childForceExpandHeight = true;
            hlg.childForceExpandWidth = false;
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

        private void SaveSellComboPrefs()
        {
            int v = !_showValue ? 0 : _mode switch
            {
                DisplayMode.SingleOnly => 1,
                DisplayMode.StackOnly => 2,
                _ => 3,
            };
            PlayerPrefs.SetInt(PREF_SELL_COMBO, v);
        }

        private void OnToggleClicked()
        {
            _showValue = !_showValue;
            PlayerPrefs.SetInt(PREF_ENABLED, _showValue ? 1 : 0);
            SaveSellComboPrefs();
            PlayerPrefs.Save();
            RefreshToggleButton();
        }

        private void SetMode(DisplayMode mode)
        {
            _mode = mode;
            PlayerPrefs.SetInt(PREF_MODE, (int)_mode);
            SaveSellComboPrefs();
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
                case 0: _autoCloseOnWASD = !_autoCloseOnWASD; PlayerPrefs.SetInt(PREF_AC_WASD, _autoCloseOnWASD ? 1 : 0); break;
                case 1: _autoCloseOnShift = !_autoCloseOnShift; PlayerPrefs.SetInt(PREF_AC_SHIFT, _autoCloseOnShift ? 1 : 0); break;
                case 2: _autoCloseOnSpace = !_autoCloseOnSpace; PlayerPrefs.SetInt(PREF_AC_SPACE, _autoCloseOnSpace ? 1 : 0); break;
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

            // Step 1: scan assemblies for ModConfigAPI - runs exactly ONCE.
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

            if (_mcAPI == null) { _mcChecked = true; return; } // not installed - stop forever

            // Step 2: call Initialize() - returns false if ModConfig's ModBehaviour isn't running yet.
            // Caller retries each frame via Update() until this returns true.
            bool ready = false;
            try
            {
                var initMethod = _mcAPI.GetMethod("Initialize", BindingFlags.Public | BindingFlags.Static);
                ready = initMethod != null && (bool)(initMethod.Invoke(null, null) ?? false);
            }
            catch { }
            if (!ready) return;

            // Registered in reverse display order: ModConfig shows entries bottom-up,
            // so the last registered setting appears at the top of the list.

            // Sliders (sleep presets) - displayed last, registered first
            MCAddSlider(PREF_PRESET4M, L("Preset 4 - min"), typeof(int), _preset4Min, new Vector2(0, 50));
            MCAddSlider(PREF_PRESET4H, L("Preset 4 - hour"), typeof(int), _preset4Hour, new Vector2(0, 23));
            MCAddSlider(PREF_PRESET3M, L("Preset 3 - min"), typeof(int), _preset3Min, new Vector2(0, 50));
            MCAddSlider(PREF_PRESET3H, L("Preset 3 - hour"), typeof(int), _preset3Hour, new Vector2(0, 23));
            MCAddSlider(PREF_PRESET2M, L("Preset 2 - min"), typeof(int), _preset2Min, new Vector2(0, 50));
            MCAddSlider(PREF_PRESET2H, L("Preset 2 - hour"), typeof(int), _preset2Hour, new Vector2(0, 23));
            MCAddSlider(PREF_PRESET1M, L("Preset 1 - min"), typeof(int), _preset1Min, new Vector2(0, 50));
            MCAddSlider(PREF_PRESET1H, L("Preset 1 - hour"), typeof(int), _preset1Hour, new Vector2(0, 23));

            MCAddBool(PREF_SLEEP_ENABLED, L("Wake-up preset buttons"), _sleepPresetsEnabled);
            MCAddBool(PREF_CAMERA_VIEW, L("Remember camera view"), _cameraViewPersist);
            MCAddBool(PREF_HIDE_CTRL, L("Hide controls hint"), _hideCtrlHint);
            MCAddBool(PREF_HIDE_HUD_ADS, L("Hide HUD on ADS"), _hideHudOnAds);
            MCAddBool(PREF_HIDE_AMMO_ADS, L("Hide ammo on ADS"), _hideAmmoOnAds);
            MCAddBool(PREF_QUEST_FAV, L("Quest favorites (N key)"), _questFavEnabled);
            MCAddBool(PREF_KILL_FEED, L("Kill feed"), _killFeedEnabled);
            MCAddBool(PREF_LOOTBOX_HL_UNSEARCHED, L("Only unsearched"), _lootboxHLOnlyUnsearched);
            MCAddBool(PREF_LOOTBOX_HL, L("Highlight loot containers"), _lootboxHLEnabled);
            MCAddBool(PREF_SKIP_MELEE, L("Skip melee on scroll"), _skipMeleeOnScroll);
            MCAddBool(PREF_AUTO_UNLOAD, L("Auto-unload gun on kill"), _autoUnloadEnabled);
            MCAddBool(PREF_FPS_COUNTER, L("Show FPS counter"), _showFps);
            MCAddBool(PREF_RECORDER_BADGE, L("Show badge on recorded items"), _showRecorderBadge);
            MCAddBool(PREF_AC_DAMAGE, L("Close on damage"), _autoCloseOnDamage);
            MCAddBool(PREF_AC_SPACE, L("Close on Space"), _autoCloseOnSpace);
            MCAddBool(PREF_AC_SHIFT, L("Close on Shift"), _autoCloseOnShift);
            MCAddBool(PREF_AC_WASD, L("Close on movement"), _autoCloseOnWASD);

            // Unified item transfer dropdown: Disabled / Shift + Left Click / Alt + Left Click
            var transferComboOpts = new SortedDictionary<string, object>
            {
                { L("Disabled"),         0 },
                { L("Shift + Left Click"), 1 },
                { L("Alt + Left Click"),   2 },
            };
            int transferComboDefault = !_transferEnabled ? 0 : (_transferModifier == TransferModifier.Shift ? 1 : 2);
            MCAddDropdown(PREF_TRANSFER_COMBO, L("Modifier + click to transfer"), transferComboOpts, typeof(int), transferComboDefault);

            MCAddBool(PREF_ENEMY_NAMES, L("Show enemy names"), _showEnemyNames);

            // Last registered = first displayed
            int sellComboDefault = !_showValue ? 0 : _mode switch
            {
                DisplayMode.SingleOnly => 1,
                DisplayMode.StackOnly => 2,
                _ => 3,
            };
            var sellComboOpts = new SortedDictionary<string, object>
            {
                { L("Disabled"),     0 },
                { L("Single only"),  1 },
                { L("Stack only"),   2 },
                { L("Combined"),     3 },
            };
            MCAddDropdown(PREF_SELL_COMBO, L("Show sell value on hover"), sellComboOpts, typeof(int), sellComboDefault);

            // Change delegate - register only once; on language re-registration this is skipped
            if (!_mcDelegateRegistered)
            {
                try
                {
                    _mcDelegate = OnModConfigChanged;
                    _mcAPI.GetMethod("SafeAddOnOptionsChangedDelegate",
                            BindingFlags.Public | BindingFlags.Static, null,
                            new[] { typeof(Action<string>) }, null)
                        ?.Invoke(null, new object[] { _mcDelegate });
                    _mcDelegateRegistered = true;
                }
                catch { }
            }

            // OnConfigSaved fires AFTER ES3 is written - more reliable than per-change delegate
            if (!_mcSavedRegistered)
            {
                try
                {
                    _mcAPI.GetMethod("add_OnConfigSaved",
                            BindingFlags.Public | BindingFlags.Static, null,
                            new[] { typeof(Action) }, null)
                        ?.Invoke(null, new object[] { (Action)OnModConfigSaved });
                    _mcSavedRegistered = true;
                }
                catch { }
            }

            _mcChecked = true; // Registration complete - stop retrying until next language change
        }

        private void OnModConfigChanged(string key)
        {
            if (_mcAPI == null) return;
            ApplyModConfigValue(key);
        }

        private void ApplyModConfigValue(string key)
        {
            if (_mcAPI == null) return;

            if (key == PREF_SELL_COMBO)
            {
                int sellComboDefault = !_showValue ? 0 : _mode switch
                {
                    DisplayMode.SingleOnly => 1,
                    DisplayMode.StackOnly => 2,
                    _ => 3,
                };
                int v = MCLoadInt(key, sellComboDefault);
                _showValue = v != 0;
                if (v == 1) _mode = DisplayMode.SingleOnly;
                else if (v == 2) _mode = DisplayMode.StackOnly;
                else if (v == 3) _mode = DisplayMode.Combined;
                PlayerPrefs.SetInt(PREF_ENABLED, _showValue ? 1 : 0);
                PlayerPrefs.SetInt(PREF_MODE, (int)_mode);
                SaveSellComboPrefs();
                RefreshToggleButton(); RefreshModeButtons();
            }
            else if (key == PREF_ENEMY_NAMES)
            { _showEnemyNames = MCLoadBool(key, _showEnemyNames); PlayerPrefs.SetInt(key, _showEnemyNames ? 1 : 0); RefreshEnemyNamesToggle(); }
            else if (key == PREF_TRANSFER_COMBO)
            {
                int v = MCLoadInt(key, !_transferEnabled ? 0 : (_transferModifier == TransferModifier.Shift ? 1 : 2));
                _transferEnabled = v != 0;
                if (v == 1) _transferModifier = TransferModifier.Shift;
                else if (v == 2) _transferModifier = TransferModifier.Alt;
                PlayerPrefs.SetInt(PREF_TRANSFER_ENABLED, _transferEnabled ? 1 : 0);
                PlayerPrefs.SetInt(PREF_TRANSFER_MOD, (int)_transferModifier);
                RefreshTransferToggle();
                RefreshTransferModifierButtons();
                RefreshShiftConflict();
            }
            else if (key == PREF_AC_WASD)
            { _autoCloseOnWASD = MCLoadBool(key, _autoCloseOnWASD); PlayerPrefs.SetInt(key, _autoCloseOnWASD ? 1 : 0); RefreshAutoCloseToggles(); RefreshShiftConflict(); }
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
            else if (key == PREF_SKIP_MELEE)
            { _skipMeleeOnScroll = MCLoadBool(key, _skipMeleeOnScroll); PlayerPrefs.SetInt(key, _skipMeleeOnScroll ? 1 : 0); RefreshSkipMeleeToggle(); }
            else if (key == PREF_AUTO_UNLOAD)
            { _autoUnloadEnabled = MCLoadBool(key, _autoUnloadEnabled); PlayerPrefs.SetInt(key, _autoUnloadEnabled ? 1 : 0); RefreshAutoUnloadToggle(); }
            else if (key == PREF_LOOTBOX_HL)
            { _lootboxHLEnabled = MCLoadBool(key, _lootboxHLEnabled); PlayerPrefs.SetInt(key, _lootboxHLEnabled ? 1 : 0); RefreshLootboxHLToggle(); if (!_lootboxHLEnabled) ClearLootboxOutlines(); }
            else if (key == PREF_LOOTBOX_HL_UNSEARCHED)
            { _lootboxHLOnlyUnsearched = MCLoadBool(key, _lootboxHLOnlyUnsearched); PlayerPrefs.SetInt(key, _lootboxHLOnlyUnsearched ? 1 : 0); RefreshLootboxHLUnsearchedToggle(); }
            else if (key == PREF_KILL_FEED)
            { _killFeedEnabled = MCLoadBool(key, _killFeedEnabled); PlayerPrefs.SetInt(key, _killFeedEnabled ? 1 : 0); RefreshKillFeedToggle(); }
            else if (key == PREF_QUEST_FAV)
            { _questFavEnabled = MCLoadBool(key, _questFavEnabled); PlayerPrefs.SetInt(key, _questFavEnabled ? 1 : 0); RefreshQuestFavToggle(); }
            else if (key == PREF_HIDE_CTRL)
            { _hideCtrlHint = MCLoadBool(key, _hideCtrlHint); PlayerPrefs.SetInt(key, _hideCtrlHint ? 1 : 0); RefreshHideCtrlToggle(); ApplyCtrlHintSetting(); }
            else if (key == PREF_CAMERA_VIEW)
            { _cameraViewPersist = MCLoadBool(key, _cameraViewPersist); PlayerPrefs.SetInt(key, _cameraViewPersist ? 1 : 0); RefreshCameraViewToggle(); }
            else if (key == PREF_HIDE_HUD_ADS)
            { _hideHudOnAds = MCLoadBool(key, _hideHudOnAds); PlayerPrefs.SetInt(key, _hideHudOnAds ? 1 : 0); RefreshHideHudAdsToggle(); }
            else if (key == PREF_HIDE_AMMO_ADS)
            { _hideAmmoOnAds = MCLoadBool(key, _hideAmmoOnAds); PlayerPrefs.SetInt(key, _hideAmmoOnAds ? 1 : 0); RefreshHideAmmoAdsToggle(); }
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
            var opts = new SortedDictionary<string, object>
            {
                { L("Disabled"), 0 },
                { L("Enabled"),  1 },
            };
            MCAddDropdown(key, desc, opts, typeof(int), def ? 1 : 0);
        }

        private void MCAddDropdown(string key, string desc, SortedDictionary<string, object> options, Type valueType, object def)
        {
            try
            {
                _mcAPI!.GetMethod("SafeAddDropdownList", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { MC_MOD_NAME, key, desc, options, valueType, def });
            }
            catch { }
        }

        private void MCAddSlider(string key, string desc, Type valueType, object def, Vector2 range)
        {
            try
            {
                _mcAPI!.GetMethod("SafeAddInputWithSlider", BindingFlags.Public | BindingFlags.Static)
                    ?.Invoke(null, new object[] { MC_MOD_NAME, key, desc, valueType, def, (Vector2?)range });
            }
            catch { }
        }

        private bool MCLoadBool(string key, bool def)
        {
            return MCLoadInt(key, def ? 1 : 0) != 0;
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

        private void MCSetInt(string key, int value)
        {
            if (_mcAPI == null) return;
            try
            {
                _mcAPI.GetMethod("SafeSave", BindingFlags.Public | BindingFlags.Static)
                    ?.MakeGenericMethod(typeof(int))
                    .Invoke(null, new object[] { MC_MOD_NAME, key, value });
            }
            catch { }
        }

        private void SyncAllToModConfig()
        {
            if (_mcAPI == null) return;
            int sellV = !_showValue ? 0 : _mode switch { DisplayMode.SingleOnly => 1, DisplayMode.StackOnly => 2, _ => 3 };
            MCSetInt(PREF_SELL_COMBO, sellV);
            MCSetInt(PREF_ENEMY_NAMES, _showEnemyNames ? 1 : 0);
            int transferV = !_transferEnabled ? 0 : (_transferModifier == TransferModifier.Shift ? 1 : 2);
            MCSetInt(PREF_TRANSFER_COMBO, transferV);
            MCSetInt(PREF_AC_WASD, _autoCloseOnWASD ? 1 : 0);
            MCSetInt(PREF_AC_SHIFT, _autoCloseOnShift ? 1 : 0);
            MCSetInt(PREF_AC_SPACE, _autoCloseOnSpace ? 1 : 0);
            MCSetInt(PREF_AC_DAMAGE, _autoCloseOnDamage ? 1 : 0);
            MCSetInt(PREF_SKIP_MELEE, _skipMeleeOnScroll ? 1 : 0);
            MCSetInt(PREF_AUTO_UNLOAD, _autoUnloadEnabled ? 1 : 0);
            MCSetInt(PREF_LOOTBOX_HL, _lootboxHLEnabled ? 1 : 0);
            MCSetInt(PREF_LOOTBOX_HL_UNSEARCHED, _lootboxHLOnlyUnsearched ? 1 : 0);
            MCSetInt(PREF_KILL_FEED, _killFeedEnabled ? 1 : 0);
            MCSetInt(PREF_QUEST_FAV, _questFavEnabled ? 1 : 0);
            MCSetInt(PREF_HIDE_CTRL, _hideCtrlHint ? 1 : 0);
            MCSetInt(PREF_CAMERA_VIEW, _cameraViewPersist ? 1 : 0);
            MCSetInt(PREF_HIDE_HUD_ADS, _hideHudOnAds ? 1 : 0);
            MCSetInt(PREF_HIDE_AMMO_ADS, _hideAmmoOnAds ? 1 : 0);
            MCSetInt(PREF_SLEEP_ENABLED, _sleepPresetsEnabled ? 1 : 0);
            MCSetInt(PREF_RECORDER_BADGE, _showRecorderBadge ? 1 : 0);
            MCSetInt(PREF_FPS_COUNTER, _showFps ? 1 : 0);
            MCSetInt(PREF_PRESET1H, _preset1Hour); MCSetInt(PREF_PRESET1M, _preset1Min);
            MCSetInt(PREF_PRESET2H, _preset2Hour); MCSetInt(PREF_PRESET2M, _preset2Min);
            MCSetInt(PREF_PRESET3H, _preset3Hour); MCSetInt(PREF_PRESET3M, _preset3Min);
            MCSetInt(PREF_PRESET4H, _preset4Hour); MCSetInt(PREF_PRESET4M, _preset4Min);
        }

        private void OnModConfigSaved()
        {
            // add_OnConfigSaved fires AFTER ES3 is written - call ApplyModConfigValue directly (no frame delay)
            ApplyModConfigValue(PREF_SELL_COMBO);
            ApplyModConfigValue(PREF_ENEMY_NAMES);
            ApplyModConfigValue(PREF_TRANSFER_COMBO);
            ApplyModConfigValue(PREF_AC_WASD);
            ApplyModConfigValue(PREF_AC_SHIFT);
            ApplyModConfigValue(PREF_AC_SPACE);
            ApplyModConfigValue(PREF_AC_DAMAGE);
            ApplyModConfigValue(PREF_SKIP_MELEE);
            ApplyModConfigValue(PREF_AUTO_UNLOAD);
            ApplyModConfigValue(PREF_LOOTBOX_HL);
            ApplyModConfigValue(PREF_LOOTBOX_HL_UNSEARCHED);
            ApplyModConfigValue(PREF_KILL_FEED);
            ApplyModConfigValue(PREF_QUEST_FAV);
            ApplyModConfigValue(PREF_HIDE_CTRL);
            ApplyModConfigValue(PREF_CAMERA_VIEW);
            ApplyModConfigValue(PREF_HIDE_HUD_ADS);
            ApplyModConfigValue(PREF_HIDE_AMMO_ADS);
            ApplyModConfigValue(PREF_SLEEP_ENABLED);
            ApplyModConfigValue(PREF_RECORDER_BADGE);
            ApplyModConfigValue(PREF_FPS_COUNTER);
            ApplyModConfigValue(PREF_PRESET1H);
            ApplyModConfigValue(PREF_PRESET1M);
            ApplyModConfigValue(PREF_PRESET2H);
            ApplyModConfigValue(PREF_PRESET2M);
            ApplyModConfigValue(PREF_PRESET3H);
            ApplyModConfigValue(PREF_PRESET3M);
            ApplyModConfigValue(PREF_PRESET4H);
            ApplyModConfigValue(PREF_PRESET4M);
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
            int stackValue = (int)(item.GetTotalRawValue() / 2);
            bool isStack = item.StackCount > 1;

            ValueText.gameObject.SetActive(true);
            ValueText.transform.SetParent(uiInstance.LayoutParent);
            ValueText.transform.localScale = Vector3.one;
            ValueText.text = _mode switch
            {
                DisplayMode.SingleOnly => $"${singleValue}",
                DisplayMode.StackOnly => $"${stackValue}",
                _ => isStack ? $"${singleValue} / ${stackValue}" : $"${singleValue}",
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
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.spacing = 3;

            var hMinus = MakePickerBtn(parent, "−");
            var hDisplay = MakePickerDisplay(parent, $"{getH():D2}");
            var hPlus = MakePickerBtn(parent, "+");
            var colon = MakePickerColon(parent);
            var mMinus = MakePickerBtn(parent, "−");
            var mDisplay = MakePickerDisplay(parent, $"{getM():D2}");
            var mPlus = MakePickerBtn(parent, "+");

            var hTxt = hDisplay.GetComponentInChildren<TextMeshProUGUI>()!;
            var mTxt = mDisplay.GetComponentInChildren<TextMeshProUGUI>()!;

            hMinus.GetComponent<Button>().onClick.AddListener(() => { setH(((getH() - 1) + 24) % 24); hTxt.text = $"{getH():D2}"; });
            hPlus.GetComponent<Button>().onClick.AddListener(() => { setH((getH() + 1) % 24); hTxt.text = $"{getH():D2}"; });
            mMinus.GetComponent<Button>().onClick.AddListener(() => { setM(((getM() - 10) + 60) % 60); mTxt.text = $"{getM():D2}"; });
            mPlus.GetComponent<Button>().onClick.AddListener(() => { setM((getM() + 10) % 60); mTxt.text = $"{getM():D2}"; });
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
            var c = btn.colors;
            c.highlightedColor = new Color(0.55f, 0.38f, 0f, 1f);
            c.pressedColor = new Color(0.22f, 0.15f, 0f, 1f);
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
        // Fires for every death in the game - no scanning, no polling needed.
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
            string entry = killerName.Length > 0
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
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90;
            _killFeedCanvas.AddComponent<CanvasScaler>();
            _killFeedCanvas.AddComponent<GraphicRaycaster>();

            _killFeedContainer = new GameObject("Container");
            _killFeedContainer.transform.SetParent(_killFeedCanvas.transform, false);
            var rt = _killFeedContainer.AddComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-32f, _hideCtrlHint ? -76f : -152f);
            rt.sizeDelta = new Vector2(0f, 0f);
            var vlg = _killFeedContainer.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperRight;
            vlg.childForceExpandWidth = false;
            vlg.childForceExpandHeight = false;
            vlg.spacing = 4f;
            var csf = _killFeedContainer.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
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
            img.color = new Color(0f, 0f, 0f, 0.55f);
            img.sprite = GetOrCreateRoundedRectSprite();
            img.type = Image.Type.Sliced;

            // HorizontalLayoutGroup + ContentSizeFitter shrink-wraps the pill to text width
            var hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.padding = new RectOffset(8, 8, 4, 4);
            hlg.childAlignment = TextAnchor.MiddleCenter;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;
            var entryCsf = go.AddComponent<ContentSizeFitter>();
            entryCsf.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            entryCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            var txtGo = new GameObject("T");
            txtGo.transform.SetParent(go.transform, false);
            var tmp = txtGo.AddComponent<TextMeshProUGUI>();
            var templateTmp = GameplayDataSettings.UIStyle.TemplateTextUGUI;
            if (templateTmp != null) tmp.font = templateTmp.font;
            tmp.text = text;
            tmp.fontSize = 12f;
            tmp.color = Color.white;
            tmp.alignment = TextAlignmentOptions.MidlineRight;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Overflow;
            tmp.richText = true;
            var shadow = txtGo.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
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
                if (e.Timer > KF_DISPLAY) alpha = (KF_DISPLAY + KF_FADE - e.Timer) / KF_FADE;
                else if (e.Timer > KF_FADE) alpha = 1f;
                else if (e.Timer > 0f) alpha = e.Timer / KF_FADE;
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

        // ── Hide Controls Hint ────────────────────────────────────────────

        private void OnHideCtrlToggleClicked()
        {
            _hideCtrlHint = !_hideCtrlHint;
            PlayerPrefs.SetInt(PREF_HIDE_CTRL, _hideCtrlHint ? 1 : 0);
            PlayerPrefs.Save();
            RefreshHideCtrlToggle();
            ApplyCtrlHintSetting();
        }

        private void RefreshHideCtrlToggle()
        {
            if (_hideCtrlToggleImage != null)
                RefreshIOSToggle(_hideCtrlToggleImage, _hideCtrlToggleThumb!, _hideCtrlHint);
        }

        private void ApplyCtrlHintSetting()
        {
            if (_simpleIndicators != null)
                _simpleIndicators.gameObject.SetActive(!_hideCtrlHint);
            if (_killFeedContainer != null)
            {
                var rt = _killFeedContainer.GetComponent<RectTransform>();
                if (rt != null)
                    rt.anchoredPosition = new Vector2(-32f, _hideCtrlHint ? -76f : -152f);
            }
        }

        // ── Camera View Persistence ───────────────────────────────────────

        private void OnCameraViewToggleClicked()
        {
            _cameraViewPersist = !_cameraViewPersist;
            PlayerPrefs.SetInt(PREF_CAMERA_VIEW, _cameraViewPersist ? 1 : 0);
            PlayerPrefs.Save();
            RefreshCameraViewToggle();
        }

        private void RefreshCameraViewToggle()
        {
            if (_cameraViewToggleImage != null)
                RefreshIOSToggle(_cameraViewToggleImage, _cameraViewToggleThumb!, _cameraViewPersist);
        }

        // ── Hide HUD on ADS ───────────────────────────────────────────────

        private void OnHideHudAdsToggleClicked()
        {
            _hideHudOnAds = !_hideHudOnAds;
            PlayerPrefs.SetInt(PREF_HIDE_HUD_ADS, _hideHudOnAds ? 1 : 0);
            PlayerPrefs.Save();
            RefreshHideHudAdsToggle();
        }

        private void RefreshHideHudAdsToggle()
        {
            if (_hideHudAdsToggleImage != null)
                RefreshIOSToggle(_hideHudAdsToggleImage, _hideHudAdsToggleThumb!, _hideHudOnAds);
        }

        private void OnHideAmmoAdsToggleClicked()
        {
            _hideAmmoOnAds = !_hideAmmoOnAds;
            PlayerPrefs.SetInt(PREF_HIDE_AMMO_ADS, _hideAmmoOnAds ? 1 : 0);
            PlayerPrefs.Save();
            RefreshHideAmmoAdsToggle();
        }

        private void RefreshHideAmmoAdsToggle()
        {
            if (_hideAmmoAdsToggleImage != null)
                RefreshIOSToggle(_hideAmmoAdsToggleImage, _hideAmmoAdsToggleThumb!, _hideAmmoOnAds);
        }

        // ── Quest Favorites ───────────────────────────────────────────────

        private void TryToggleQuestFavorite()
        {
            var view = QuestView.Instance;
            if (view == null) return;
            var quest = view.SelectedQuest;
            if (quest == null) return;
            int id = quest.ID;
            if (_favoriteQuestIds.Contains(id))
                _favoriteQuestIds.Remove(id);
            else
                _favoriteQuestIds.Add(id);
            PlayerPrefs.SetString(PREF_QUEST_FAV_IDS, string.Join(",", _favoriteQuestIds));
            PlayerPrefs.Save();
            _questFavReorderTimer = 0f; // trigger immediate reorder next Update tick
        }

        private void TryReorderQuestView()
        {
            var view = QuestView.Instance;
            if (view == null || !view.gameObject.activeInHierarchy) return;
            var entries = _qvActiveEntriesField?.GetValue(view) as List<QuestEntry>;
            if (entries == null || entries.Count == 0) return;
            int favIdx = 0;
            foreach (var entry in entries)
            {
                if (entry?.Target == null) continue;
                bool isFav = _favoriteQuestIds.Contains(entry.Target.ID);
                // Manage ★ overlay
                var starTr = entry.transform.Find("FavStar");
                if (isFav && starTr == null)
                {
                    var starGo = new GameObject("FavStar");
                    starGo.transform.SetParent(entry.transform, false);
                    var starTmp = starGo.AddComponent<TextMeshProUGUI>();
                    starTmp.text = "♥";
                    var rt = starGo.GetComponent<RectTransform>();
                    rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.anchoredPosition = new Vector2(35f, 0f);
                    rt.sizeDelta = new Vector2(24f, 24f);
                    starTmp.fontSize = 20f;
                    starTmp.alignment = TextAlignmentOptions.Center;
                    starTmp.color = new Color(0.973f, 0.333f, 0.400f); // #f85566
                }
                else if (!isFav && starTr != null)
                {
                    UnityEngine.Object.Destroy(starTr.gameObject);
                }
                // Pin favorites to top
                if (isFav) entry.transform.SetSiblingIndex(favIdx++);
            }
        }

        private void OnQuestFavToggleClicked()
        {
            _questFavEnabled = !_questFavEnabled;
            PlayerPrefs.SetInt(PREF_QUEST_FAV, _questFavEnabled ? 1 : 0);
            PlayerPrefs.Save();
            RefreshQuestFavToggle();
            if (!_questFavEnabled)
            {
                // Remove all star overlays
                var view = QuestView.Instance;
                if (view != null)
                {
                    var entries = _qvActiveEntriesField?.GetValue(view) as List<QuestEntry>;
                    if (entries != null)
                        foreach (var entry in entries)
                        {
                            var starTr = entry?.transform.Find("FavStar");
                            if (starTr != null) UnityEngine.Object.Destroy(starTr.gameObject);
                        }
                }
            }
        }

        private void RefreshQuestFavToggle()
        {
            RefreshIOSToggle(_questFavToggleImage!, _questFavToggleThumb!, _questFavEnabled);
        }
    }
}
