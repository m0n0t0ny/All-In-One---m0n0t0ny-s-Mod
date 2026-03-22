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
using Duckov.Options.UI;

namespace AllInOneMod_m0n0t0ny
{
    enum DisplayMode { Combined, SingleOnly, StackOnly }
    enum TransferModifier { Shift, Alt }

    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        // ── PlayerPrefs keys ──────────────────────────────────────────────
        private const string PREF_ENABLED = "DisplayItemValue_Enabled";
        private const string PREF_MODE = "DisplayItemValue_Mode";
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
        private const string PREF_AC_WASD = "DisplayItemValue_ACWasd";
        private const string PREF_AC_SHIFT = "DisplayItemValue_ACShift";
        private const string PREF_AC_SPACE = "DisplayItemValue_ACSpace";
        private const string PREF_AC_DAMAGE = "DisplayItemValue_ACDamage";
        private const string PREF_FPS_COUNTER = "DisplayItemValue_FpsCounter";
        private const string PREF_SKIP_MELEE = "DisplayItemValue_SkipMelee";

        // ── Item value display ────────────────────────────────────────────
        private bool _showValue;
        private DisplayMode _mode;
        private TextMeshProUGUI? _valueText;
        private Item? _lastHoveredItem;

        // ── Shift/Alt-click transfer ──────────────────────────────────────
        private bool _transferEnabled;
        private TransferModifier _transferModifier;
        private Item? _transferCachedItem; // snapshotted at end of each frame via LateUpdate
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

        // ── Native settings tab injection ─────────────────────────────────
        private static FieldInfo? _optTabButtonsField;
        private static FieldInfo? _optTabContentField;
        private static MethodInfo? _optSetupMethod;
        private static bool _optReflectionSearched;
        private bool _ddolTabInited;
        private bool _mmTabInited;
        private float _tabInjectTimer;
        private GameObject? _ddolTabContent;
        private GameObject? _mmTabContent;
        private SystemLanguage _tabBuiltLang;
        private float _langCheckTimer;

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

        // ── Skip melee on scroll ──────────────────────────────────────────
        private bool _skipMeleeOnScroll;
        private bool _scrollDetectedThisFrame;
        private int _lastScrollDir;
        private CharacterMainControl? _playerCtrl;

        // ── Auto-unload enemy weapons on loot-open ────────────────────────
        private const string PREF_AUTO_UNLOAD = "DisplayItemValue_AutoUnload";
        private bool _autoUnloadEnabled;
        private int _lastAutoUnloadInvId;
        private static PropertyInfo? _itemPlugsProp;
        private static FieldInfo? _itemPlugsField;
        private static bool _itemPlugsSearched;
        private static PropertyInfo? _stackCountProp;
        private static PropertyInfo? _maxStackCountProp;
        private static PropertyInfo? _typeIDProp;
        private static bool _stackCountSearched;


        // ── Factory Recorder badge ────────────────────────────────────────
        private const string PREF_RECORDER_BADGE = "DisplayItemValue_RecorderBadge";
        private bool _showRecorderBadge;
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
        private static readonly FieldInfo? _qvActiveEntriesField =
            typeof(QuestView).GetField("activeEntries", BindingFlags.NonPublic | BindingFlags.Instance);

        // ── Kill Feed ─────────────────────────────────────────────────────
        private const string PREF_KILL_FEED = "DisplayItemValue_KillFeed";
        private const string PREF_HIDE_CTRL = "DisplayItemValue_HideCtrlHint";
        private const string PREF_CAMERA_VIEW = "DisplayItemValue_CameraView";
        private const string PREF_HIDE_HUD_ADS = "DisplayItemValue_HideHudOnAds";
        private const string PREF_HIDE_AMMO_ADS = "DisplayItemValue_HideAmmoOnAds";
        private bool _killFeedEnabled;
        private bool _hideCtrlHint;
        private bool _hideHudOnAds;
        private bool _hideAmmoOnAds;
        private bool _adsHiding;
        private readonly List<CanvasGroup> _adsHideGroups = new List<CanvasGroup>();
        private readonly List<(CanvasGroup cg, float alpha, bool rays)> _adsSnapshot = new List<(CanvasGroup, float, bool)>();
        private bool _cameraViewPersist;
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
            _showEnemyNames = PlayerPrefs.GetInt(PREF_ENEMY_NAMES, 1) == 1;
            _transferEnabled = PlayerPrefs.GetInt(PREF_TRANSFER_ENABLED, 1) == 1;
            _transferModifier = (TransferModifier)PlayerPrefs.GetInt(PREF_TRANSFER_MOD, (int)TransferModifier.Shift);
            _autoCloseOnWASD = PlayerPrefs.GetInt(PREF_AC_WASD, 0) == 1;
            _autoCloseOnShift = PlayerPrefs.GetInt(PREF_AC_SHIFT, 0) == 1;
            _autoCloseOnSpace = PlayerPrefs.GetInt(PREF_AC_SPACE, 0) == 1;
            _autoCloseOnDamage = PlayerPrefs.GetInt(PREF_AC_DAMAGE, 0) == 1;
            _sleepPresetsEnabled = PlayerPrefs.GetInt(PREF_SLEEP_ENABLED, 1) == 1;
            _preset1Hour = PlayerPrefs.GetInt(PREF_PRESET1H, 5);
            _preset1Min = PlayerPrefs.GetInt(PREF_PRESET1M, 45);
            _preset2Hour = PlayerPrefs.GetInt(PREF_PRESET2H, 11);
            _preset2Min = PlayerPrefs.GetInt(PREF_PRESET2M, 45);
            _preset3Hour = PlayerPrefs.GetInt(PREF_PRESET3H, 17);
            _preset3Min = PlayerPrefs.GetInt(PREF_PRESET3M, 45);
            _preset4Hour = PlayerPrefs.GetInt(PREF_PRESET4H, 23);
            _preset4Min = PlayerPrefs.GetInt(PREF_PRESET4M, 45);
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
        }

        void OnDestroy()
        {
            if (_valueText != null) Destroy(_valueText.gameObject);
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
            _simpleIndicatorsFound = false;
            _simpleIndicatorSearchTimer = 0f;
            _adsHiding = false;
            _viewRestorePending = true;
            _mmTabInited = false;
            _mmTabContent = null;
            _playerCtrl = null;
        }

        private Transform? _simpleIndicators;
        private bool _simpleIndicatorsFound;
        private float _simpleIndicatorSearchTimer;
        void Update()
        {
            // Only poll for tab injection when needed and in the right context.
            // - DDOL tab: inject once; flag is never reset on scene load (panel persists).
            // - MM tab: only attempt when not in a raid (LevelManager is null in menus).
            if (!_ddolTabInited || (!_mmTabInited && LevelManager.Instance == null))
            {
                _tabInjectTimer -= Time.unscaledDeltaTime;
                if (_tabInjectTimer <= 0f)
                {
                    _tabInjectTimer = 0.5f;
                    TryInjectSettingsTab();
                }
            }
            // Language change detection: rebuild settings tab content when the game language changes.
            if (_ddolTabContent != null)
            {
                _langCheckTimer -= Time.unscaledDeltaTime;
                if (_langCheckTimer <= 0f)
                {
                    _langCheckTimer = 0.5f;
                    var currentLang = GetGameLanguage();
                    if (currentLang != _tabBuiltLang)
                    {
                        _tabBuiltLang = currentLang;
                        RefreshTabContent(_ddolTabContent);
                        if (_mmTabContent != null) RefreshTabContent(_mmTabContent);
                    }
                }
            }
            if (!_simpleIndicatorsFound && LevelManager.Instance != null)
            {
                // Throttle to avoid calling FindObjectsOfType<Canvas> every frame
                _simpleIndicatorSearchTimer -= Time.deltaTime;
                if (_simpleIndicatorSearchTimer > 0f) goto _skipHudSearch;
                _simpleIndicatorSearchTimer = 0.5f;
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
                            n.IndexOf("Reticle", StringComparison.OrdinalIgnoreCase) >= 0)
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
                    _simpleIndicatorsFound = true;
                    break;
                }
            }
        _skipHudSearch:;
            // Keep enforcing: if hide is on and the game re-enabled it, hide it again
            if (_hideCtrlHint && _simpleIndicators != null && _simpleIndicators.gameObject.activeSelf)
                _simpleIndicators.gameObject.SetActive(false);

            // Hide HUD on ADS (right mouse button) - CanvasGroup.alpha avoids SetActive spikes
            if (_hideHudOnAds && _simpleIndicatorsFound && _adsHideGroups.Count > 0)
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
        // Returns true if plug is fully merged (caller must NOT call AddItem).
        // Returns false if plug could not be merged or has leftover - caller should call AddItem.
        private static bool TryMergeStack(Item plug, Inventory inv)
        {
            if (!_stackCountSearched)
            {
                _stackCountSearched = true;
                var t = typeof(Item);
                var sc = t.GetProperty("StackCount", BindingFlags.Public | BindingFlags.Instance);
                if (sc != null && sc.CanWrite) _stackCountProp = sc;
                _maxStackCountProp = t.GetProperty("MaxStackCount", BindingFlags.Public | BindingFlags.Instance);
                _typeIDProp = t.GetProperty("TypeID", BindingFlags.Public | BindingFlags.Instance);
            }
            if (_stackCountProp == null || _maxStackCountProp == null) return false;

            // Only stackable items (MaxStackCount > 1) should ever merge.
            int maxStack;
            try { maxStack = (int)(_maxStackCountProp.GetValue(plug) ?? 1); }
            catch { return false; }
            if (maxStack <= 1) return false;

            int plugCount;
            try { plugCount = (int)(_stackCountProp.GetValue(plug) ?? 0); }
            catch { return false; }
            if (plugCount <= 0) return false;

            // Find a slot of the same type with room, using TypeID when available.
            object? plugID = null;
            try { plugID = _typeIDProp?.GetValue(plug); } catch { }

            Item? target = null;
            if (inv.Content != null)
            {
                foreach (var candidate in inv.Content)
                {
                    if (candidate == null || candidate == plug) continue;
                    bool sameType = plugID != null
                        ? plugID.Equals(_typeIDProp!.GetValue(candidate))
                        : candidate.GetType() == plug.GetType();
                    if (!sameType) continue;
                    int cMax, cCount;
                    try { cMax = (int)(_maxStackCountProp.GetValue(candidate) ?? 1); } catch { continue; }
                    try { cCount = (int)(_stackCountProp.GetValue(candidate) ?? 0); } catch { continue; }
                    if (cCount < cMax) { target = candidate; break; }
                }
            }
            if (target == null) return false;

            try
            {
                int existingCount = (int)(_stackCountProp.GetValue(target) ?? 0);
                int targetMax = (int)(_maxStackCountProp.GetValue(target) ?? maxStack);
                int canFit = targetMax - existingCount;
                int moved = Math.Min(plugCount, canFit);
                _stackCountProp.SetValue(target, existingCount + moved);
                if (moved >= plugCount) return true;          // fully merged
                _stackCountProp.SetValue(plug, plugCount - moved); // update remainder
                return false;                                  // partial - caller does AddItem
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
                if (!TryMergeStack(item, charInv) && !charInv.AddItem(item))
                    lootInv.AddItem(item);
            }
            else if (charInv != null && charInv.Content.Contains(item) && lootInv != null)
            {
                charInv.RemoveItem(item);
                if (!TryMergeStack(item, lootInv) && !lootInv.AddItem(item))
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

        // ── Localization ──────────────────────────────────────────────────

        private static PropertyInfo? _locLangProp;
        private static bool _locInit;

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
            // Section headers
            ["Looting"] = new[] { "Pillage", "Plündern", "拾取", "拾取", "ルーティング", "루팅", "Saque", "Мародёрство", "Saqueo" },
            ["Combat"] = new[] { "Combat", "Kampf", "战斗", "戰鬥", "戦闘", "전투", "Combate", "Бой", "Combate" },
            ["Survival"] = new[] { "Survie", "Überleben", "生存", "生存", "サバイバル", "생존", "Sobrevivência", "Выживание", "Supervivencia" },
            ["HUD"] = new[] { "HUD", "HUD", "界面", "介面", "HUD", "HUD", "HUD", "Интерфейс", "HUD" },
            ["Quests"] = new[] { "Quêtes", "Aufgaben", "任务", "任務", "クエスト", "퀘스트", "Missões", "Задания", "Misiones" },
            // Dropdown values
            ["Off"] = new[] { "Off", "Aus", "关闭", "關閉", "オフ", "끔", "Desligado", "Выкл.", "Apagado" },
            ["On"] = new[] { "On", "An", "开启", "開啟", "オン", "켬", "Ligado", "Вкл.", "Activado" },
            ["Combined"] = new[] { "Combiné", "Kombiniert", "组合", "組合", "複合", "복합", "Combinado", "Комбинированный", "Combinado" },
            ["Single"] = new[] { "Unité", "Einzeln", "单个", "單個", "単体", "단일", "Unitário", "Единица", "Unidad" },
            ["Stack"] = new[] { "Pile", "Stapel", "堆叠", "堆疊", "スタック", "스택", "Pilha", "Стопка", "Pila" },
            ["All"] = new[] { "Tous", "Alle", "全部", "全部", "全て", "전체", "Todos", "Все", "Todos" },
            ["Only unsearched"] = new[] { "Seulement non fouillés", "Nur Ungesucht", "仅未搜寻", "僅未搜尋", "未探索のみ", "미수색만", "Apenas não vasculhados", "Только необысканные", "Solo sin registrar" },
            ["Hide all"] = new[] { "Tout masquer", "Alles ausblenden", "隐藏全部", "隱藏全部", "全て非表示", "모두 숨기기", "Ocultar tudo", "Скрыть всё", "Ocultar todo" },
            ["Show Only Ammo"] = new[] { "Afficher seulement munitions", "Nur Munition anzeigen", "仅显示弹药", "僅顯示彈藥", "弾薬のみ表示", "탄약만 표시", "Mostrar só munição", "Только патроны", "Solo munición" },
            // Feature labels
            ["Show item value on hover"] = new[] { "Afficher la valeur de l'objet", "Artikelwert beim Hover", "悬停显示物品价值", "懸停顯示物品價值", "ホバー時に価値を表示", "호버 시 아이템 가치 표시", "Mostrar valor do item", "Стоимость при наведении", "Mostrar valor al pasar" },
            ["Quick item transfer"] = new[] { "Transfert rapide", "Schnelltransfer", "快速转移物品", "快速轉移物品", "クイックアイテム移動", "빠른 아이템 이동", "Transferir item rapidamente", "Быстрый перенос", "Transferencia rápida" },
            ["Lootbox highlight"] = new[] { "Surligner les caisses", "Loot-Kisten hervorheben", "高亮战利品箱", "高亮戰利品箱", "ルートボックス強調", "루트박스 강조", "Destacar caixas de loot", "Подсветка контейнеров", "Resaltar cajas de botín" },
            ["Badge on recorded keys and Blueprints"] = new[] { "Badge sur clés/plans enregistrés", "Badge erfasste Schlüssel/Pläne", "已记录钥匙和蓝图徽章", "已記錄鑰匙和藍圖徽章", "記録済みキー/設計図バッジ", "기록된 열쇠/청사진 뱃지", "Badge em chaves/plantas registradas", "Значок записанных ключей/схем", "Insignia en llaves/planos registrados" },
            ["Show enemy name"] = new[] { "Afficher le nom ennemi", "Feindnamen anzeigen", "显示敌人名称", "顯示敵人名稱", "敵の名前を表示", "적 이름 표시", "Mostrar nome inimigo", "Показывать имя врага", "Mostrar nombre enemigo" },
            ["Auto-unload gun on kill"] = new[] { "Décharger arme à la mort", "Waffe bei Tod entladen", "击杀时自动卸弹", "擊殺時自動卸彈", "キル時に自動アンロード", "처치 시 총알 제거", "Descarregar arma ao matar", "Авто-разрядка при убийстве", "Descargar arma al matar" },
            ["Kill feed"] = new[] { "Fil de mort", "Kill-Feed", "击杀信息流", "擊殺信息流", "キルフィード", "킬 피드", "Feed de abates", "Лента убийств", "Feed de bajas" },
            ["Skip melee on scroll"] = new[] { "Ignorer mêlée au défilement", "Nahkampf beim Scrollen skippen", "滚轮跳过近战", "滾輪跳過近戰", "スクロールで近接スキップ", "스크롤 시 근접 건너뜀", "Pular melee no scroll", "Пропуск ближнего боя", "Saltar melee al girar" },
            ["Close on movement"] = new[] { "Fermer au mouvement", "Bei Bewegung schließen", "移动时关闭", "移動時關閉", "移動で閉じる", "이동 시 닫기", "Fechar ao mover", "Закрыть при движении", "Cerrar al moverse" },
            ["Close on Shift"] = new[] { "Fermer avec Shift", "Mit Shift schließen", "按Shift时关闭", "按Shift時關閉", "Shiftで閉じる", "Shift로 닫기", "Fechar com Shift", "Закрыть при Shift", "Cerrar con Shift" },
            ["Close on Space"] = new[] { "Fermer avec Espace", "Mit Leertaste schließen", "按空格时关闭", "按空格時關閉", "スペースで閉じる", "스페이스로 닫기", "Fechar com Espaço", "Закрыть при Пробеле", "Cerrar con Espacio" },
            ["Close on damage"] = new[] { "Fermer en cas de dégâts", "Bei Schaden schließen", "受伤时关闭", "受傷時關閉", "ダメージで閉じる", "피격 시 닫기", "Fechar ao tomar dano", "Закрыть при уроне", "Cerrar al recibir daño" },
            ["Wake-up presets"] = new[] { "Réveils préréglés", "Aufwach-Voreinstellungen", "起床预设", "起床預設", "起床プリセット", "기상 프리셋", "Presets de acordar", "Пресеты пробуждения", "Preajustes de despertar" },
            ["Preset 1"] = new[] { "Préréglage 1", "Voreinstellung 1", "预设 1", "預設 1", "プリセット 1", "프리셋 1", "Predefinição 1", "Пресет 1", "Preajuste 1" },
            ["Preset 2"] = new[] { "Préréglage 2", "Voreinstellung 2", "预设 2", "預設 2", "プリセット 2", "프리셋 2", "Predefinição 2", "Пресет 2", "Preajuste 2" },
            ["Preset 3"] = new[] { "Préréglage 3", "Voreinstellung 3", "预设 3", "預設 3", "プリセット 3", "프리셋 3", "Predefinição 3", "Пресет 3", "Preajuste 3" },
            ["Preset 4"] = new[] { "Préréglage 4", "Voreinstellung 4", "预设 4", "預設 4", "プリセット 4", "프리셋 4", "Predefinição 4", "Пресет 4", "Preajuste 4" },
            ["FPS counter"] = new[] { "Compteur FPS", "FPS-Anzeige", "帧率计数器", "幀率計數器", "FPSカウンター", "FPS 카운터", "Contador FPS", "Счётчик FPS", "Contador FPS" },
            ["Hide controls hint"] = new[] { "Masquer l'aide de contrôle", "Steuerungshinweis ausblenden", "隐藏操作提示", "隱藏操作提示", "操作ヒントを非表示", "조작 힌트 숨기기", "Ocultar dica de controles", "Скрыть подсказку управления", "Ocultar ayuda de controles" },
            ["Hide HUD on ADS"] = new[] { "Masquer HUD en visée", "HUD beim Zielen ausblenden", "瞄准时隐藏HUD", "瞄準時隱藏HUD", "ADS中HUDを非表示", "ADS 시 HUD 숨기기", "Ocultar HUD ao mirar", "Скрывать HUD при прицеливании", "Ocultar HUD al apuntar" },
            ["Remember camera view"] = new[] { "Mémoriser la vue caméra", "Kameraansicht merken", "记住相机视角", "記住相機視角", "カメラビューを記憶", "카메라 뷰 기억", "Lembrar visão de câmera", "Запомнить вид камеры", "Recordar vista de cámara" },
            ["Quest favorites (N key)"] = new[] { "Favoris (touche N)", "Favoriten (Taste N)", "任务收藏 (N键)", "任務收藏 (N鍵)", "お気に入り (Nキー)", "즐겨찾기 (N키)", "Favoritos (tecla N)", "Избранные (клавиша N)", "Favoritos (tecla N)" },
        };

        private static string L(string key)
        {
            var lang = GetGameLanguage();
            int idx = Array.IndexOf(_langOrder, lang);
            if (idx >= 0 && _t.TryGetValue(key, out var arr) && idx < arr.Length)
                return arr[idx];
            return key;
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

        // ── Hide Controls Hint ────────────────────────────────────────────

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

        // ── Native Settings Tab Injection ─────────────────────────────────

        private static bool EnsureOptReflection(OptionsPanel panel)
        {
            if (_optReflectionSearched) return _optTabButtonsField != null;
            _optReflectionSearched = true;
            _optTabButtonsField = typeof(OptionsPanel).GetField("tabButtons",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _optTabContentField = typeof(OptionsPanel_TabButton).GetField("tab",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            _optSetupMethod = typeof(OptionsPanel).GetMethod("Setup",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            return _optTabButtonsField != null;
        }

        private static void LogHierarchy(Transform t, int depth)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < depth; i++) sb.Append("  ");
            sb.Append($"[{t.gameObject.name}] ");
            foreach (var comp in t.GetComponents<Component>())
                if (comp != null) sb.Append($"<{comp.GetType().Name}> ");
            Debug.Log(sb.ToString());
            for (int i = 0; i < t.childCount; i++)
                LogHierarchy(t.GetChild(i), depth + 1);
        }

        private bool TryInjectTab(string sceneName, out GameObject? tabContent)
        {
            tabContent = null;
            var panels = FindObjectsOfType<OptionsPanel>(true);
            if (panels == null || panels.Length == 0) return false;

            OptionsPanel? target = null;
            foreach (var p in panels)
            {
                if (p == null) continue;
                if (p.gameObject.scene.name == sceneName) { target = p; break; }
            }
            if (target == null) return false;

            if (!EnsureOptReflection(target)) return false;

            var tabButtons = _optTabButtonsField!.GetValue(target) as System.Collections.IList;
            if (tabButtons == null) return false;

            // Check if we already injected into this panel instance
            foreach (var tb in tabButtons)
            {
                var tbComp = tb as OptionsPanel_TabButton;
                if (tbComp != null && tbComp.name == "m0n0t0nyTab") return true;
            }

            // Find a non-selected tab button to clone
            var selection = target.GetSelection();
            OptionsPanel_TabButton? srcBtn = null;
            foreach (var tb in tabButtons)
            {
                var tbComp = tb as OptionsPanel_TabButton;
                if (tbComp != null && tbComp != selection) { srcBtn = tbComp; break; }
            }
            if (srcBtn == null)
            {
                foreach (var tb in tabButtons)
                {
                    var tbComp = tb as OptionsPanel_TabButton;
                    if (tbComp != null) { srcBtn = tbComp; break; }
                }
            }
            if (srcBtn == null) return false;

            // Clone the tab button
            var newBtnGo = UnityEngine.Object.Instantiate(srcBtn.gameObject, srcBtn.transform.parent);
            var newBtn = newBtnGo.GetComponent<OptionsPanel_TabButton>();
            if (newBtn == null) { UnityEngine.Object.Destroy(newBtnGo); return false; }
            newBtnGo.name = "m0n0t0nyTab";

            // Clone the tab content
            var srcContent = _optTabContentField!.GetValue(srcBtn) as GameObject;
            if (srcContent == null) { UnityEngine.Object.Destroy(newBtnGo); return false; }
            var newContent = UnityEngine.Object.Instantiate(srcContent, srcContent.transform.parent);
            newContent.name = "m0n0t0nyContent";

            // Destroy all children immediately (Destroy is deferred and would interfere with PopulateTabContent)
            for (int i = newContent.transform.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(newContent.transform.GetChild(i).gameObject);

            // Remove any layout components inherited from the clone
            var existingVLG = newContent.GetComponent<VerticalLayoutGroup>();
            if (existingVLG != null) UnityEngine.Object.DestroyImmediate(existingVLG);
            var existingHLG = newContent.GetComponent<HorizontalLayoutGroup>();
            if (existingHLG != null) UnityEngine.Object.DestroyImmediate(existingHLG);
            var existingCSF = newContent.GetComponent<ContentSizeFitter>();
            if (existingCSF != null) UnityEngine.Object.DestroyImmediate(existingCSF);

            // No background on the content root - sections have their own native backgrounds
            var existingBg = newContent.GetComponent<Image>();
            if (existingBg != null) DestroyImmediate(existingBg);

            // Link new content to new button
            _optTabContentField?.SetValue(newBtn, newContent);

            // Set tab button name
            var tabNameTmp = newBtn.GetComponentInChildren<TextMeshProUGUI>(true);
            if (tabNameTmp != null)
            {
                var loc = newBtn.GetComponentsInChildren<MonoBehaviour>(true)
                    .FirstOrDefault(c => c.GetType().Name == "TextLocalizor");
                if (loc != null) UnityEngine.Object.Destroy(loc);
                tabNameTmp.text = "ALL IN ONE";
            }

            // Add to panel's tab list
            tabButtons!.Add(newBtn);

            // Refresh panel
            _optSetupMethod?.Invoke(target, null);

            tabContent = newContent;
            return true;
        }

        private void PopulateTabContent(GameObject content)
        {
            // Outer VLG: stacks sections vertically with spacing between them
            var vlg = content.AddComponent<VerticalLayoutGroup>();
            vlg.childAlignment = TextAnchor.UpperLeft;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.padding = new RectOffset(8, 8, 8, 8);
            vlg.spacing = 8f;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var area = content.transform;

            // Clone dropdown template from any native panel
            OptionsUIEntry_Dropdown? dropdownTemplate = null;
            foreach (var panel in FindObjectsOfType<OptionsPanel>(true))
            {
                dropdownTemplate = panel.GetComponentInChildren<OptionsUIEntry_Dropdown>(true);
                if (dropdownTemplate != null) break;
            }

            // Find the native category wrapper from the KeyBindings tab:
            // UI_InputBinding > Layout > first child that has VerticalLayoutGroup (the [Move] group)
            GameObject? wrapperTemplate = null;
            foreach (var panel in FindObjectsOfType<OptionsPanel>(true))
            {
                var tabBtns = _optTabButtonsField?.GetValue(panel) as System.Collections.IList;
                if (tabBtns == null) continue;
                foreach (var tb in tabBtns)
                {
                    var tbComp = tb as OptionsPanel_TabButton;
                    if (tbComp == null) continue;
                    var tabGo = _optTabContentField?.GetValue(tbComp) as GameObject;
                    if (tabGo == null || tabGo.name != "UI_InputBinding") continue;
                    var layout = tabGo.transform.Find("Layout");
                    if (layout == null) continue;
                    for (int i = 0; i < layout.childCount; i++)
                    {
                        var child = layout.GetChild(i);
                        if (child.GetComponent<VerticalLayoutGroup>() != null)
                        { wrapperTemplate = child.gameObject; break; }
                    }
                    if (wrapperTemplate != null) break;
                }
                if (wrapperTemplate != null) break;
            }

            var dropdownLabelField = typeof(OptionsUIEntry_Dropdown).GetField("label", BindingFlags.NonPublic | BindingFlags.Instance);
            var dropdownDropdownField = typeof(OptionsUIEntry_Dropdown).GetField("dropdown", BindingFlags.NonPublic | BindingFlags.Instance);

            void AddToggle(string labelText, bool value, Action<bool> onChange) =>
                AddCycle(labelText, new[] { L("Off"), L("On") }, value ? 1 : 0, idx => onChange(idx > 0));

            void AddCycle(string labelText, string[] options, int currentIdx, Action<int> onChange)
            {
                if (dropdownTemplate == null) return;
                var clone = Instantiate(dropdownTemplate.gameObject, area);
                var comp = clone.GetComponent<OptionsUIEntry_Dropdown>();
                var lbl = dropdownLabelField?.GetValue(comp) as TextMeshProUGUI;
                var dropdown = dropdownDropdownField?.GetValue(comp) as TMP_Dropdown;
                if (lbl != null) lbl.text = labelText;
                if (dropdown != null)
                {
                    dropdown.ClearOptions();
                    dropdown.AddOptions(new List<string>(options));
                    dropdown.SetValueWithoutNotify(currentIdx);
                    dropdown.onValueChanged.RemoveAllListeners();
                    dropdown.onValueChanged.AddListener(idx => onChange(idx));
                }
                if (comp != null) DestroyImmediate(comp);
                // Remove any extra direct children (e.g. globe icon siblings)
                for (int i = clone.transform.childCount - 1; i >= 0; i--)
                {
                    var child = clone.transform.GetChild(i).gameObject;
                    if (child != lbl?.gameObject && child != dropdown?.gameObject)
                        DestroyImmediate(child);
                }
                // Remove globe icon from inside the dropdown GO ([Image] child alongside [Label]/[Arrow]/[Template])
                if (dropdown != null)
                {
                    for (int i = dropdown.transform.childCount - 1; i >= 0; i--)
                    {
                        var child = dropdown.transform.GetChild(i).gameObject;
                        if (child.name != "Label" && child.name != "Arrow" && child.name != "Template"
                            && child.name != "[Label]" && child.name != "[Arrow]" && child.name != "[Template]")
                            DestroyImmediate(child);
                    }
                }
            }

            // Clone the native wrapper, strip children, add a label, redirect area for rows
            void AddSection(string labelText, Action fill)
            {
                var savedArea = area;
                GameObject wrapper;
                if (wrapperTemplate != null)
                {
                    wrapper = Instantiate(wrapperTemplate, savedArea);
                    wrapper.name = "Section_" + labelText;
                    for (int i = wrapper.transform.childCount - 1; i >= 0; i--)
                        DestroyImmediate(wrapper.transform.GetChild(i).gameObject);
                    // Ensure rows expand to fill the wrapper width
                    var wVlg = wrapper.GetComponent<VerticalLayoutGroup>();
                    if (wVlg != null) wVlg.childForceExpandWidth = true;
                }
                else
                {
                    // Fallback if keybindings tab not found: plain dark container
                    wrapper = new GameObject("Section_" + labelText);
                    wrapper.transform.SetParent(savedArea, false);
                    wrapper.AddComponent<LayoutElement>(); // lets outer VLG size it
                    var wVlg = wrapper.AddComponent<VerticalLayoutGroup>();
                    wVlg.childForceExpandWidth = true;
                    wVlg.childForceExpandHeight = false;
                    wrapper.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }

                // Section label (same style as native [Label_Move])
                var labelGo = new GameObject("SectionLabel");
                labelGo.transform.SetParent(wrapper.transform, false);
                var le = labelGo.AddComponent<LayoutElement>();
                le.preferredHeight = 30f;
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.text = labelText.ToUpper();
                tmp.fontSize = 12f;
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.MidlineLeft;
                var rt = labelGo.GetComponent<RectTransform>();
                rt.offsetMin = new Vector2(12f, 0f);

                area = wrapper.transform;
                fill();
                area = savedArea;
            }

            // ── Looting ───────────────────────────────────────────────────────
            AddSection(L("Looting"), () =>
            {
                // idx: 0=Combined, 1=Single, 2=Stack, 3=Off
                AddCycle(L("Show item value on hover"), new[] { L("Combined"), L("Single"), L("Stack"), L("Off") },
                    !_showValue ? 3 : (int)_mode,
                    idx =>
                    {
                        if (idx == 3) { _showValue = false; PlayerPrefs.SetInt(PREF_ENABLED, 0); }
                        else { _showValue = true; _mode = (DisplayMode)idx; PlayerPrefs.SetInt(PREF_ENABLED, 1); PlayerPrefs.SetInt(PREF_MODE, idx); }
                        PlayerPrefs.Save();
                    });
                // idx: 0=Alt+Click, 1=Shift+Click, 2=Off
                AddCycle(L("Quick item transfer"), new[] { "Alt + Click", "Shift + Click", L("Off") },
                    !_transferEnabled ? 2 : (_transferModifier == TransferModifier.Alt ? 0 : 1),
                    idx =>
                    {
                        if (idx == 2) { _transferEnabled = false; PlayerPrefs.SetInt(PREF_TRANSFER_ENABLED, 0); }
                        else { _transferEnabled = true; _transferModifier = idx == 0 ? TransferModifier.Alt : TransferModifier.Shift; PlayerPrefs.SetInt(PREF_TRANSFER_ENABLED, 1); PlayerPrefs.SetInt(PREF_TRANSFER_MOD, (int)_transferModifier); }
                        PlayerPrefs.Save();
                    });
                // idx: 0=All, 1=Only unsearched, 2=Off
                AddCycle(L("Highlight loot container"), new[] { L("All"), L("Only unsearched"), L("Off") },
                    !_lootboxHLEnabled ? 2 : (_lootboxHLOnlyUnsearched ? 1 : 0),
                    idx =>
                    {
                        if (idx == 2) { _lootboxHLEnabled = false; PlayerPrefs.SetInt(PREF_LOOTBOX_HL, 0); }
                        else { _lootboxHLEnabled = true; _lootboxHLOnlyUnsearched = idx == 1; PlayerPrefs.SetInt(PREF_LOOTBOX_HL, 1); PlayerPrefs.SetInt(PREF_LOOTBOX_HL_UNSEARCHED, idx == 1 ? 1 : 0); }
                        PlayerPrefs.Save();
                    });
                AddToggle(L("Badge on recorded Keys and Blueprints"), _showRecorderBadge, v => { _showRecorderBadge = v; PlayerPrefs.SetInt(PREF_RECORDER_BADGE, v ? 1 : 0); PlayerPrefs.Save(); });
            });

            // ── Combat ────────────────────────────────────────────────────────
            AddSection(L("Combat"), () =>
            {
                AddToggle(L("Show enemy name"), _showEnemyNames, v => { _showEnemyNames = v; PlayerPrefs.SetInt(PREF_ENEMY_NAMES, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Auto-unload enemy gun on kill"), _autoUnloadEnabled, v => { _autoUnloadEnabled = v; PlayerPrefs.SetInt(PREF_AUTO_UNLOAD, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Kill feed"), _killFeedEnabled, v => { _killFeedEnabled = v; PlayerPrefs.SetInt(PREF_KILL_FEED, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Skip melee on scroll"), _skipMeleeOnScroll, v => { _skipMeleeOnScroll = v; PlayerPrefs.SetInt(PREF_SKIP_MELEE, v ? 1 : 0); PlayerPrefs.Save(); });
            });

            // ── Survival ──────────────────────────────────────────────────────
            AddSection(L("Survival"), () =>
            {
                AddToggle(L("Close on movement"), _autoCloseOnWASD, v => { _autoCloseOnWASD = v; PlayerPrefs.SetInt(PREF_AC_WASD, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Close on Shift"), _autoCloseOnShift, v => { _autoCloseOnShift = v; PlayerPrefs.SetInt(PREF_AC_SHIFT, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Close on Space"), _autoCloseOnSpace, v => { _autoCloseOnSpace = v; PlayerPrefs.SetInt(PREF_AC_SPACE, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Close on damage"), _autoCloseOnDamage, v => { _autoCloseOnDamage = v; PlayerPrefs.SetInt(PREF_AC_DAMAGE, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Wake-up preset buttons"), _sleepPresetsEnabled, v => { _sleepPresetsEnabled = v; PlayerPrefs.SetInt(PREF_SLEEP_ENABLED, v ? 1 : 0); PlayerPrefs.Save(); });
                var timeSlots = Enumerable.Range(0, 96).Select(i => $"{i / 4:D2}:{(i % 4) * 15:D2}").ToArray();
                AddCycle(L("Preset 1"), timeSlots, _preset1Hour * 4 + _preset1Min / 15, idx =>
                {
                    _preset1Hour = idx / 4; _preset1Min = (idx % 4) * 15;
                    PlayerPrefs.SetInt(PREF_PRESET1H, _preset1Hour); PlayerPrefs.SetInt(PREF_PRESET1M, _preset1Min); PlayerPrefs.Save();
                    if (_preset1BtnLabel != null) _preset1BtnLabel.text = $"{_preset1Hour:D2}:{_preset1Min:D2}";
                });
                AddCycle(L("Preset 2"), timeSlots, _preset2Hour * 4 + _preset2Min / 15, idx =>
                {
                    _preset2Hour = idx / 4; _preset2Min = (idx % 4) * 15;
                    PlayerPrefs.SetInt(PREF_PRESET2H, _preset2Hour); PlayerPrefs.SetInt(PREF_PRESET2M, _preset2Min); PlayerPrefs.Save();
                    if (_preset2BtnLabel != null) _preset2BtnLabel.text = $"{_preset2Hour:D2}:{_preset2Min:D2}";
                });
                AddCycle(L("Preset 3"), timeSlots, _preset3Hour * 4 + _preset3Min / 15, idx =>
                {
                    _preset3Hour = idx / 4; _preset3Min = (idx % 4) * 15;
                    PlayerPrefs.SetInt(PREF_PRESET3H, _preset3Hour); PlayerPrefs.SetInt(PREF_PRESET3M, _preset3Min); PlayerPrefs.Save();
                    if (_preset3BtnLabel != null) _preset3BtnLabel.text = $"{_preset3Hour:D2}:{_preset3Min:D2}";
                });
                AddCycle(L("Preset 4"), timeSlots, _preset4Hour * 4 + _preset4Min / 15, idx =>
                {
                    _preset4Hour = idx / 4; _preset4Min = (idx % 4) * 15;
                    PlayerPrefs.SetInt(PREF_PRESET4H, _preset4Hour); PlayerPrefs.SetInt(PREF_PRESET4M, _preset4Min); PlayerPrefs.Save();
                    if (_preset4BtnLabel != null) _preset4BtnLabel.text = $"{_preset4Hour:D2}:{_preset4Min:D2}";
                });
            });

            // ── HUD ───────────────────────────────────────────────────────────
            AddSection(L("HUD"), () =>
            {
                AddToggle(L("Show FPS counter"), _showFps, v =>
                {
                    _showFps = v; PlayerPrefs.SetInt(PREF_FPS_COUNTER, v ? 1 : 0); PlayerPrefs.Save();
                    if (_fpsCanvas != null) _fpsCanvas.SetActive(v);
                });
                AddToggle(L("Hide controls hint"), _hideCtrlHint, v =>
                {
                    _hideCtrlHint = v; PlayerPrefs.SetInt(PREF_HIDE_CTRL, v ? 1 : 0); PlayerPrefs.Save();
                    ApplyCtrlHintSetting();
                });
                // idx: 0=Hide all, 1=Show only ammo, 2=Off
                AddCycle(L("Hide HUD on ADS"), new[] { L("Hide all"), L("Show Only Ammo"), L("Off") },
                    !_hideHudOnAds ? 2 : (_hideAmmoOnAds ? 0 : 1),
                    idx =>
                    {
                        if (idx == 2) { _hideHudOnAds = false; _hideAmmoOnAds = false; PlayerPrefs.SetInt(PREF_HIDE_HUD_ADS, 0); PlayerPrefs.SetInt(PREF_HIDE_AMMO_ADS, 0); }
                        else { _hideHudOnAds = true; _hideAmmoOnAds = idx == 0; PlayerPrefs.SetInt(PREF_HIDE_HUD_ADS, 1); PlayerPrefs.SetInt(PREF_HIDE_AMMO_ADS, idx == 0 ? 1 : 0); }
                        PlayerPrefs.Save();
                    });
                AddToggle(L("Remember camera view"), _cameraViewPersist, v =>
                {
                    _cameraViewPersist = v; PlayerPrefs.SetInt(PREF_CAMERA_VIEW, v ? 1 : 0);
                    if (!v) PlayerPrefs.DeleteKey("CameraViewSavedTopDown");
                    PlayerPrefs.Save();
                });
            });

            // ── Quests ────────────────────────────────────────────────────────
            AddSection(L("Quests"), () =>
            {
                AddToggle(L("Quest favorites (N key)"), _questFavEnabled, v => { _questFavEnabled = v; PlayerPrefs.SetInt(PREF_QUEST_FAV, v ? 1 : 0); PlayerPrefs.Save(); });
            });
        }

        private void TryInjectSettingsTab()
        {
            if (!_ddolTabInited)
            {
                // TryInjectTab returns true both on fresh injection (ddolContent != null)
                // and when the tab already exists (ddolContent == null). Either way, mark done.
                if (TryInjectTab("DontDestroyOnLoad", out var ddolContent))
                {
                    if (ddolContent != null)
                    {
                        PopulateTabContent(ddolContent);
                        _ddolTabContent = ddolContent;
                        _tabBuiltLang = GetGameLanguage();
                    }
                    _ddolTabInited = true;
                }
            }
            // MainMenu panel only exists outside raids; LevelManager is null in menu scenes.
            if (!_mmTabInited && LevelManager.Instance == null)
            {
                if (TryInjectTab("MainMenu", out var mmContent))
                {
                    if (mmContent != null)
                    {
                        PopulateTabContent(mmContent);
                        _mmTabContent = mmContent;
                        _tabBuiltLang = GetGameLanguage();
                    }
                    _mmTabInited = true;
                }
            }
        }

        private void RefreshTabContent(GameObject content)
        {
            for (int i = content.transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(content.transform.GetChild(i).gameObject);
            var vlg = content.GetComponent<VerticalLayoutGroup>();
            if (vlg != null) DestroyImmediate(vlg);
            var csf = content.GetComponent<ContentSizeFitter>();
            if (csf != null) DestroyImmediate(csf);
            PopulateTabContent(content);
        }
    }
}
