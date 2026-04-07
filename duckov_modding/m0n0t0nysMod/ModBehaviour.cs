using System;
using System.Collections.Generic;
using System.IO;
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
using Duckov.MiniMaps;
using Duckov.MiniMaps.UI;
using Duckov.Scenes;
using Duckov.BlackMarkets;
using Duckov.BlackMarkets.UI;

namespace AllInOneMod_m0n0t0ny
{
    enum DisplayMode { Combined, SingleOnly, StackOnly }
    enum TransferModifier { Shift, Alt }
    enum ItemValueLevel { White, Green, Blue, Purple, Orange, LightRed, Red }


    public class ModBehaviour : Duckov.Modding.ModBehaviour
    {
        // ── PlayerPrefs keys ──────────────────────────────────────────────
        private const string MOD_VERSION = "(～￣▽￣)～ v3.2 ～(￣▽￣～)";
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
        private const string PREF_PROFILER = "DisplayItemValue_Profiler";
        private const string PREF_TRACE_LOG = "DisplayItemValue_TraceLog";
        private const string PREF_SKIP_MELEE = "DisplayItemValue_SkipMelee";

        // ── Item value display ────────────────────────────────────────────
        private bool _showValue;
        private DisplayMode _mode;
        private TextMeshProUGUI? _valueText;
        private Item? _lastHoveredItem;



        // ── Inventory count on hover ──────────────────────────────────────
        private const string PREF_INV_COUNT = "DisplayItemValue_InvCount";
        private bool _invCountEnabled;
        private TextMeshProUGUI? _invCountText;
        private readonly List<(int typeID, int stackCount)> _cachedStorageItems = new List<(int, int)>();
        private static Type? _petProxyType;
        private static PropertyInfo? _petProxyInvProp;
        private static bool _petProxySearched;
        private UnityEngine.Object[]? _cachedPetProxies;

        // ── Inventory count on hover ──────────────────────────────────────
        // GetItemCount only works outside raids (stash not loaded in raid scene).
        // We enumerate PlayerStorage.Inventory on hideout load and cache counts.
        private readonly Dictionary<int, int> _stashCountCache = new Dictionary<int, int>();
        private float _stashCacheRebuildTimer = -1f;
        private static Type? _psType;
        private static PropertyInfo? _psInvProp;
        private static bool _psReflectionSearched;

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
        private float _nameUpdateTimer = 0.25f; // staggered from slot scan
        private HealthBar[]? _cachedHealthBars;
        private Health[]? _cachedAllHealth;
        private readonly HashSet<int> _forcedBarIds = new HashSet<int>();
        private float _hbCacheTimer;
        private static readonly FieldInfo? _hbNameTextField =
            typeof(HealthBar).GetField("nameText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? _characterPresetField =
            typeof(CharacterMainControl).GetField("characterPreset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly PropertyInfo? _displayNameProp =
            typeof(CharacterMainControl)
                .GetField("characterPreset", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                ?.FieldType.GetProperty("DisplayName", BindingFlags.Public | BindingFlags.Instance);
        // ── Show hidden health bars ───────────────────────────────────────
        private static readonly PropertyInfo? _healthShowBarProp =
            typeof(Health).GetProperty("showHealthBar", BindingFlags.Public | BindingFlags.Instance);
        private bool _showHiddenBars;
        private float _hiddenBarScanTimer = -15f; // 15s delay before first scan

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

        // ── Performance profiler ──────────────────────────────────────────
        private bool _profilerEnabled;
        private const int PROF_WINDOW = 120;
        private const float PROF_SPIKE_MS = 1f;   // threshold for spike count
        private int _profFrame;
        private int _profWindowNum;
        private static readonly long[] _profAccum = new long[23];
        private static readonly long[] _profMin = new long[23];
        private static readonly long[] _profMax = new long[23];
        private static readonly int[] _profSpikes = new int[23];
        private static readonly float[] _profAvgMs = new float[23];
        private static readonly float[] _profMaxMs = new float[23];
        private static readonly System.Diagnostics.Stopwatch _profSw = new System.Diagnostics.Stopwatch();
        private static readonly float _profFreqMs = System.Diagnostics.Stopwatch.Frequency / 1000f;
        private GameObject? _profCanvas;
        private TextMeshProUGUI? _profTMP;
        private static readonly string[] _profNames = {
            "Tab inject", "Lang check", "HUD canvas", "Ctrl hint", "Hide HUD ADS",
            "Camera view", "Skip melee", "Quest fav", "Sleep preset", "Item transfer",
            "Enemy names", "Auto-close", "Slot scan", "Auto-unload", "Lootbox HL", "Kill feed",
            "Boss markers", "Hidden bars", "Sort button", "BM price", "Stash cache",
            "Trace flush", "FPS counter"
        };
        private const int PI_TAB = 0, PI_LANG = 1, PI_HUD = 2, PI_CTRL = 3, PI_ADS = 4, PI_CAM = 5,
            PI_SCROLL = 6, PI_QUEST = 7, PI_SLEEP = 8, PI_TRANSFER = 9, PI_ENAMES = 10,
            PI_AUTOCLOSE = 11, PI_SLOTS = 12, PI_AUTOUNLOAD = 13, PI_LOOTBOX = 14, PI_KILLFEED = 15,
            PI_BOSS = 16, PI_HIDDEN_BARS = 17, PI_SORT = 18, PI_BM = 19, PI_STASH_CACHE = 20,
            PI_TRACE = 21, PI_FPS = 22;

        // ── Event profiler (per-invocation, not per-frame) ────────────────
        private const int EVT_HOVER = 0, EVT_KILL = 1, EVT_LOOT = 2;
        private static readonly string[] _evtNames = { "Hover", "Kill event", "Loot open" };
        private static readonly long[] _evtAccum = new long[3];
        private static readonly long[] _evtMax = new long[3];
        private static readonly int[] _evtCalls = new int[3];
        private static readonly float[] _evtAvgMs = new float[3];
        private static readonly float[] _evtMaxMs = new float[3];
        private static readonly System.Diagnostics.Stopwatch _evtSw = new System.Diagnostics.Stopwatch();

        // ── Per-call trace log (Ctrl+Shift+T) ────────────────────────────
        private bool _traceEnabled;
        private float _traceGraceTimer; // suppress trace during scene load + grace period
        private const float TRACE_GRACE = 5f; // seconds to skip after scene load
        private readonly System.Text.StringBuilder _traceBuf = new System.Text.StringBuilder();

        // ── FPS counter ───────────────────────────────────────────────────
        private bool _showFps;
        private GameObject? _fpsCanvas;
        private TextMeshProUGUI? _fpsTMP;
        private float _fpsDeltaAccum;
        private int _fpsFrameCount;
        private float _fpsValue;

        // ── Skip melee on scroll ──────────���───────────────────────────────
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

        // ── Backpack value sort ───────────────────────────────────────���
        private const string PREF_VALUE_SORT = "DisplayItemValue_ValueSort";
        private const string PREF_VALUE_SORT_MODE = "DisplayItemValue_ValueSortMode";
        private bool _valueSortEnabled;
        private int _valueSortMode; // 0=Native, 1=Value, 2=StackValue, 3=ValuePerKg
        private static readonly FieldInfo? _invDisplaySortBtnField =
            typeof(InventoryDisplay).GetField("sortButton", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly MethodInfo? _invDisplaySortNativeMethod =
            typeof(InventoryDisplay).GetMethod("OnSortButtonClicked", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance);
        private readonly HashSet<int> _hookedSortButtons = new HashSet<int>();
        private readonly Dictionary<int, Button> _sortOrigBtns = new Dictionary<int, Button>();
        private readonly Dictionary<int, Button[]> _sortCustomButtons = new Dictionary<int, Button[]>();
        private readonly Dictionary<int, TextMeshProUGUI> _sortStoreAllTmps = new Dictionary<int, TextMeshProUGUI>();
        private float _sortBtnScanTimer;
        private GameObject? _sortToastCanvas;
        private TextMeshProUGUI? _sortToastTMP;
        private float _sortToastTimer;
        private const float SORT_TOAST_DISPLAY = 2f;
        private const float SORT_TOAST_FADE = 0.4f;
        private GameObject? _sortTooltipCanvas;
        private GameObject? _sortTooltipGo;
        private TextMeshProUGUI? _sortTooltipTmp;
        private RectTransform? _sortTooltipRT;

        // ── Factory Recorder badge ────────────────────────────────────────
        private const string PREF_RECORDER_BADGE = "DisplayItemValue_RecorderBadge";
        private bool _showRecorderBadge;
        // Slot badge overlay tracking
        private static Type? _slotCompType;
        private static MemberInfo? _slotItemMember; // PropertyInfo or FieldInfo → Item
        private static readonly Dictionary<Type, MemberInfo?> _typeItemMemberCache = new Dictionary<Type, MemberInfo?>();
        private float _badgeScanTimer;
        private float _slotCacheTimer;
        private float _slotActivityTimer; // counts down after last slot activity; keep fast-scan rate while > 0
        private object[]? _cachedSlots;
        private int _scanCursor; // incremental scan cursor - position within _cachedSlots for budget processing

        private bool _inventoryOpen; // true while any inventory/vendor panel is open (event-driven)
        private int _lastSlotScanCount; // tracks slot count growth for fast-rescan logic
        private readonly Dictionary<int, GameObject> _slotBadges = new Dictionary<int, GameObject>();
        private readonly Dictionary<int, (Graphic g, Color orig)> _slotLabelGraphics = new Dictionary<int, (Graphic, Color)>();
        private readonly Dictionary<int, bool> _isHudSlotCache = new Dictionary<int, bool>();
        private readonly HashSet<int> _scanSeenIds = new HashSet<int>();
        private readonly Dictionary<int, ItemValueLevel> _rarityLevelCache = new Dictionary<int, ItemValueLevel>();
        private const string PREF_REMOVE_NAME_BG = "DisplayItemValue_RemoveNameBg";
        private bool _removeNameBg;
        // key=slot id, value=(Graphic ncG, bool origEnabled, TMP, origFontStyle, origTmpAlign, HLG, origHlgAlign, LE, origFlexW)
        private readonly Dictionary<int, (Graphic? g, bool origEnabled, TextMeshProUGUI? tmp, FontStyles origFontStyle, TextAlignmentOptions origTmpAlign, HorizontalLayoutGroup? hlg, TextAnchor origHlgAlign, LayoutElement? le, float origFlexW)> _nameStyleCache
            = new Dictionary<int, (Graphic?, bool, TextMeshProUGUI?, FontStyles, TextAlignmentOptions, HorizontalLayoutGroup?, TextAnchor, LayoutElement?, float)>();
        private static bool _trueShadowSearched;
        private static Type? _trueShadowType;
        private static PropertyInfo? _tsPropColor;

        // ── Lootbox Highlight ─────────────────────────────────────────────
        // 0=Off, 1=Normal (gold), 2=By item rarity
        private const string PREF_LOOTBOX_HL = "DisplayItemValue_LootboxHL";
        private int _lootboxHLMode;
        private sealed class LbEntry
        {
            public Outlinable Ol = null!;
            public Component? Marker;       // InteractMarker (cached once)
            public Inventory? Inv;          // Inventory for inspected-flag + rarity (cached once)
            public Color CachedColor = new Color(1f, 0.75f, 0f, 1f); // last computed rarity color
            public int LastItemCount = -1;  // detect inventory changes to avoid recomputing color
        }
        // key = GameObject instanceID
        private readonly Dictionary<int, LbEntry> _lootboxOutlines = new Dictionary<int, LbEntry>();
        private float _lootboxScanTimer;
        private float _lootboxUpdateTimer;
        private static Type? _lbType;              // InteractableLootbox
        private static Type? _imType;              // InteractMarker
        private static FieldInfo? _imMarkedAsUsed;      // InteractMarker.markedAsUsed
        private static FieldInfo? _invInspectedField;   // Inventory.hasBeenInspectedInLootBox
        private static FieldInfo? _lbInvRefField;       // InteractableLootbox.inventoryReference
        private static bool _lbCached;

        // ── Quest Favorites ───────────────────────────────────────────────
        private const string PREF_QUEST_FAV = "DisplayItemValue_QuestFav";
        private const string PREF_QUEST_FAV_IDS = "DisplayItemValue_QuestFavIds";
        private bool _questFavEnabled;
        private readonly HashSet<int> _favoriteQuestIds = new HashSet<int>();
        private float _questFavReorderTimer;
        private static readonly FieldInfo? _qvActiveEntriesField =
            typeof(QuestView).GetField("activeEntries", BindingFlags.NonPublic | BindingFlags.Instance);

        // ── Item Rarity Display & Search Sounds ───────────────────────────
        private const string PREF_RARITY_DISPLAY = "DisplayItemValue_RarityDisplay";
        private const string PREF_RARITY_SOUND = "DisplayItemValue_RaritySound";
        private bool _rarityDisplayEnabled;
        private bool _raritySoundEnabled;
        private readonly Dictionary<Item, (Graphic g, Color orig)> _inspectingGraphics = new Dictionary<Item, (Graphic, Color)>();
        private static System.Collections.IDictionary? S_DynEntryMap;
        private static Color S_CWhite, S_CGreen, S_CBlue, S_CPurple, S_COrange, S_CLightRed, S_CRed;
        private static readonly int[] ForceWhiteTypeIDs = { 308, 309, 368, 394, 890 };

        // ── Black Market base price ───────────────────────────────────────
        private const string PREF_BM_BASE_PRICE = "DisplayItemValue_BMBasePrice";
        private bool _bmBasePriceEnabled;
        private float _bmScanTimer;
        private const string BM_ANNOT_MARKER = "\u2060"; // word joiner - invisible, used as annotation sentinel
        private static readonly FieldInfo? _supplyEntryTitleField =
            typeof(SupplyPanel_Entry).GetField("titleDisplay", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? _demandEntryTitleField =
            typeof(DemandPanel_Entry).GetField("titleDisplay", BindingFlags.NonPublic | BindingFlags.Instance);
        private static readonly FieldInfo? _dseFactorField =
            typeof(BlackMarket.DemandSupplyEntry).GetField("priceFactor", BindingFlags.NonPublic | BindingFlags.Instance);
        private SupplyPanel_Entry[]? _cachedBMSupplyEntries;
        private DemandPanel_Entry[]? _cachedBMDemandEntries;

        // ── Boss map markers ──────────────────────────────────────────────
        private const string PREF_BOSS_MARKERS = "DisplayItemValue_BossMarkers";
        private const string PREF_SHOW_HIDDEN_BARS = "DisplayItemValue_ShowHiddenBars";
        private bool _bossMarkersEnabled;
        private bool _mapOpen;
        private float _bossScanTimer;
        private CharacterSpawnerRoot[]? _cachedBossSpawners;
        private static FieldInfo? _charIconTypeField;
        private static bool _charIconTypeSearched;
        private sealed class BossEntry
        {
            public CharacterMainControl Char = null!;
            public GameObject Go = null!;
            public SimplePointOfInterest Poi = null!;
            public bool Alive = true;
            public string Name = "";
        }
        private readonly Dictionary<CharacterMainControl, BossEntry> _bossEntries
            = new Dictionary<CharacterMainControl, BossEntry>();
        private GameObject? _bossListCanvas;
        private TextMeshProUGUI? _bossListTMP;
        private float _bossListRefreshTimer;

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
        private int _cameraViewMode; // 0=Off, 1=Default, 2=Top-down
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

        TextMeshProUGUI InvCountText
        {
            get
            {
                if (_invCountText == null)
                    _invCountText = Instantiate(GameplayDataSettings.UIStyle.TemplateTextUGUI);
                return _invCountText;
            }
        }

        void Awake()
        {
            _traceBuf.Clear();
            _showValue = PlayerPrefs.GetInt(PREF_ENABLED, 1) == 1;
            _mode = (DisplayMode)PlayerPrefs.GetInt(PREF_MODE, (int)DisplayMode.Combined);
            _showEnemyNames = PlayerPrefs.GetInt(PREF_ENEMY_NAMES, 0) == 1;
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
            _traceEnabled = PlayerPrefs.GetInt(PREF_TRACE_LOG, 0) == 1;
            _profilerEnabled = PlayerPrefs.GetInt(PREF_PROFILER, 0) == 1;
            if (_traceEnabled)
                try { System.IO.File.WriteAllText(@"C:\Users\antob\AppData\LocalLow\TeamSoda\Duckov\mod_trace.txt", $"[{System.DateTime.Now:HH:mm:ss}] Trace started - format: F<frame> U/E <feature> <ms>\n"); } catch { }
            if (_profilerEnabled) EnsureProfilerCanvas();
            _skipMeleeOnScroll = PlayerPrefs.GetInt(PREF_SKIP_MELEE, 1) == 1;
            _autoUnloadEnabled = PlayerPrefs.GetInt(PREF_AUTO_UNLOAD, 1) == 1;
            _lootboxHLMode = PlayerPrefs.GetInt(PREF_LOOTBOX_HL, 0);
            _killFeedEnabled = PlayerPrefs.GetInt(PREF_KILL_FEED, 1) == 1;
            _hideCtrlHint = PlayerPrefs.GetInt(PREF_HIDE_CTRL, 1) == 1;
            _hideHudOnAds = PlayerPrefs.GetInt(PREF_HIDE_HUD_ADS, 0) == 1;
            _hideAmmoOnAds = PlayerPrefs.GetInt(PREF_HIDE_AMMO_ADS, 1) == 1;
            _cameraViewMode = PlayerPrefs.GetInt(PREF_CAMERA_VIEW, 0);
            _rarityDisplayEnabled = PlayerPrefs.GetInt(PREF_RARITY_DISPLAY, 0) == 1;
            _raritySoundEnabled = PlayerPrefs.GetInt(PREF_RARITY_SOUND, 1) == 1;
            _removeNameBg = PlayerPrefs.GetInt(PREF_REMOVE_NAME_BG, 1) == 1;
            _questFavEnabled = PlayerPrefs.GetInt(PREF_QUEST_FAV, 1) == 1;
            _bossMarkersEnabled = PlayerPrefs.GetInt(PREF_BOSS_MARKERS, 0) == 1;
            _showHiddenBars = PlayerPrefs.GetInt(PREF_SHOW_HIDDEN_BARS, 0) == 1;
            _invCountEnabled = PlayerPrefs.GetInt(PREF_INV_COUNT, 1) == 1;
            _bmBasePriceEnabled = PlayerPrefs.GetInt(PREF_BM_BASE_PRICE, 1) == 1;
            _valueSortEnabled = PlayerPrefs.GetInt(PREF_VALUE_SORT, 1) == 1;
            _valueSortMode = PlayerPrefs.GetInt(PREF_VALUE_SORT_MODE, 0);
            foreach (var s in PlayerPrefs.GetString(PREF_QUEST_FAV_IDS, "").Split(','))
                if (int.TryParse(s.Trim(), out int qid) && qid != 0) _favoriteQuestIds.Add(qid);
            EnsureLootboxTypes();
            // Pre-discover PetProxy type so the first hover doesn't pay the assembly scan cost
            if (!_petProxySearched)
            {
                _petProxySearched = true;
                _petProxyType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.Name == "PetProxy");
                _petProxyInvProp = _petProxyType?.GetProperty("Inventory", BindingFlags.Public | BindingFlags.Instance);
            }
        }

        void OnDestroy()
        {
            if (_valueText != null) Destroy(_valueText.gameObject);
            if (_invCountText != null) Destroy(_invCountText.gameObject);
            if (_fpsCanvas != null) Destroy(_fpsCanvas);
            if (_killFeedCanvas != null) Destroy(_killFeedCanvas);
            if (_bossListCanvas != null) Destroy(_bossListCanvas);
            if (_sortToastCanvas != null) Destroy(_sortToastCanvas);
            if (_sortTooltipCanvas != null) Destroy(_sortTooltipCanvas);
            ClearBossMarkers();
            ClearLootboxOutlines();
            foreach (var kvp in _slotBadges)
                if (kvp.Value != null) Destroy(kvp.Value);
            _slotBadges.Clear();
            foreach (var kvp in _slotLabelGraphics)
                if (kvp.Value.g != null) kvp.Value.g.color = kvp.Value.orig;
            _slotLabelGraphics.Clear();
            _nameStyleCache.Clear();
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

        private void EnsureProfilerCanvas()
        {
            if (_profCanvas != null) return;
            _profCanvas = new GameObject("ModProfiler");
            DontDestroyOnLoad(_profCanvas);
            var canvas = _profCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32767;
            _profCanvas.AddComponent<CanvasScaler>();
            var textGo = new GameObject("ProfText");
            textGo.transform.SetParent(_profCanvas.transform, false);
            _profTMP = textGo.AddComponent<TextMeshProUGUI>();
            _profTMP.fontSize = 11f;
            _profTMP.color = Color.white;
            _profTMP.alignment = TextAlignmentOptions.TopLeft;
            var rt = textGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.zero;
            rt.pivot = Vector2.zero;
            rt.anchoredPosition = new Vector2(10f, 10f);
            rt.sizeDelta = new Vector2(220f, 320f);
            var shadow = textGo.AddComponent<UnityEngine.UI.Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);
            _profTMP.text = "<color=white>Profiler: collecting...</color>";
        }

        private void ProfMark(int idx)
        {
            long elapsed = _profSw.ElapsedTicks;
            _profAccum[idx] += elapsed;
            if (elapsed < _profMin[idx]) _profMin[idx] = elapsed;
            if (elapsed > _profMax[idx]) _profMax[idx] = elapsed;
            if (elapsed / _profFreqMs >= PROF_SPIKE_MS) _profSpikes[idx]++;
            if (_traceEnabled && _traceGraceTimer <= 0f)
                _traceBuf.Append('F').Append(Time.frameCount).Append(" U ").Append(_profNames[idx]).Append(' ').Append((elapsed / _profFreqMs).ToString("F4")).Append('\n');
            _profSw.Restart();
        }

        private void ProfMarkEvt(int idx)
        {
            long elapsed = _evtSw.ElapsedTicks;
            _evtAccum[idx] += elapsed;
            if (elapsed > _evtMax[idx]) _evtMax[idx] = elapsed;
            _evtCalls[idx]++;
            if (_traceEnabled && _traceGraceTimer <= 0f)
                _traceBuf.Append('F').Append(Time.frameCount).Append(" E ").Append(_evtNames[idx]).Append(' ').Append((elapsed / _profFreqMs).ToString("F4")).Append('\n');
        }


        private static void LogError(string feature, Exception ex)
        {
            try
            {
                var msg = $"[{System.DateTime.Now:HH:mm:ss}] ERROR in {feature}: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}\n";
                System.IO.File.AppendAllText(@"C:\Users\antob\AppData\LocalLow\TeamSoda\Duckov\mod_profiler.txt", msg);
            }
            catch { }
        }

        private void WriteProfLog(int[] spikes)
        {
            try
            {
                var indices = new int[_profNames.Length];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                System.Array.Sort(indices, (a, b) => _profAvgMs[b].CompareTo(_profAvgMs[a]));
                float fps = _fpsDeltaAccum > 0f ? _fpsFrameCount / _fpsDeltaAccum : _fpsValue;
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"[{System.DateTime.Now:HH:mm:ss}] Window #{_profWindowNum} ({PROF_WINDOW}f) FPS~{fps:F1}");
                sb.AppendLine($"  {"Feature",-14} {"Avg",8} {"Max",8} {"Spk(>1ms)",10}");
                sb.AppendLine($"  {new string('-', 46)}");
                foreach (int i in indices)
                    sb.AppendLine($"  {_profNames[i],-14} {_profAvgMs[i],6:F3}ms {_profMaxMs[i],6:F3}ms {spikes[i],10}");
                sb.AppendLine($"  -- Events (avg/call) --");
                sb.AppendLine($"  {"Feature",-14} {"Avg/c",8} {"Max",8} {"Calls",10}");
                sb.AppendLine($"  {new string('-', 46)}");
                for (int i = 0; i < _evtNames.Length; i++)
                    sb.AppendLine($"  {_evtNames[i],-14} {_evtAvgMs[i],6:F3}ms {_evtMaxMs[i],6:F3}ms {_evtCalls[i],10}");
                System.IO.File.AppendAllText(@"C:\Users\antob\AppData\LocalLow\TeamSoda\Duckov\mod_profiler.txt", sb.ToString());
            }
            catch { }
        }

        private void FlushTraceLog()
        {
            if (_traceBuf.Length == 0) return;
            try
            {
                System.IO.File.AppendAllText(@"C:\Users\antob\AppData\LocalLow\TeamSoda\Duckov\mod_trace.txt", _traceBuf.ToString());
                _traceBuf.Clear();
            }
            catch { }
        }

        private void UpdateProfilerDisplay(int[] spikes)
        {
            EnsureProfilerCanvas();
            var indices = new int[_profNames.Length];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            System.Array.Sort(indices, (a, b) => _profAvgMs[b].CompareTo(_profAvgMs[a]));
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"<b>Profiler #{_profWindowNum}</b>");
            sb.AppendLine($"<size=9><color=#aaaaaa>{"Feature",-14} {"Avg",5} {"Max",5} {"Spk",3}</color></size>");
            foreach (int i in indices)
            {
                float avg = _profAvgMs[i];
                string color = avg >= 0.5f ? "red" : avg >= 0.1f ? "yellow" : "white";
                sb.AppendLine($"<size=10><color={color}>{_profNames[i],-14} {avg,4:F2} {_profMaxMs[i],4:F2} {spikes[i],3}</color></size>");
            }
            sb.AppendLine($"<size=9><color=#aaaaaa>{"-- Events --",-14} {"Avg/c",5} {"Max",5} {"N",3}</color></size>");
            for (int i = 0; i < _evtNames.Length; i++)
            {
                float avg = _evtAvgMs[i];
                string color = avg >= 2f ? "red" : avg >= 0.5f ? "yellow" : "white";
                sb.AppendLine($"<size=10><color={color}>{_evtNames[i],-14} {avg,4:F2} {_evtMaxMs[i],4:F2} {_evtCalls[i],3}</color></size>");
            }
            _profTMP!.text = sb.ToString();
        }

        void OnEnable()
        {
            ItemHoveringUI.onSetupItem += OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta += OnSetupMeta;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Health.OnDead += OnKillFeedDeadDirect;
            InteractableLootbox.OnStartLoot += OnRarityStartLoot;
            ItemHoveringUI.onSetupItem += OnSlotScanTrigger;
            View.OnActiveViewChanged += OnBossMapViewChanged;
            ManagedUIElement.onOpen += OnAnyUIPanelOpen;
            ManagedUIElement.onClose += OnAnyUIPanelClose;
            InitRaritySystem();
        }

        void OnDisable()
        {
            ItemHoveringUI.onSetupItem -= OnSetupItemHoveringUI;
            ItemHoveringUI.onSetupMeta -= OnSetupMeta;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Health.OnDead -= OnKillFeedDeadDirect;
            InteractableLootbox.OnStartLoot -= OnRarityStartLoot;
            ItemHoveringUI.onSetupItem -= OnSlotScanTrigger;
            View.OnActiveViewChanged -= OnBossMapViewChanged;
            ManagedUIElement.onOpen -= OnAnyUIPanelOpen;
            ManagedUIElement.onClose -= OnAnyUIPanelClose;
            ClearBossMarkers();
            CleanupRaritySystem();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _traceGraceTimer = TRACE_GRACE;
            ClearBossMarkers();
            _cachedBossSpawners = null;
            _bossScanTimer = 0f;
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
            _badgeScanTimer = 0f; // scan on next Update to recolor and rebadge
            _cachedSlots = null;
            _scanCursor = 0;
            _slotActivityTimer = 0.5f; // stay in fast-scan mode for first 3s after scene load
            _lastSlotScanCount = 0;
            _cachedHealthBars = null;
            _cachedAllHealth = null;
            _cachedPetProxies = null;
            _cachedBMSupplyEntries = null;
            _cachedBMDemandEntries = null;
            _forcedBarIds.Clear();
            _hookedSortButtons.Clear(); _sortOrigBtns.Clear(); _sortCustomButtons.Clear(); _sortStoreAllTmps.Clear();
            _sortBtnScanTimer = 0f;
            _hiddenBarScanTimer = 0f;
            // Schedule stash cache rebuild; only useful outside raids (stash is in scene then).
            // Use a 3s delay so PlayerStorage has time to finish its async Load.
            _stashCacheRebuildTimer = 3f;
            foreach (var kvp in _slotLabelGraphics)
                if (kvp.Value.g != null) kvp.Value.g.color = kvp.Value.orig;
            _slotLabelGraphics.Clear();
            _nameStyleCache.Clear();
            _slotBadges.Clear();
            _isHudSlotCache.Clear();
            _rarityLevelCache.Clear();
        }

        private Transform? _simpleIndicators;
        private bool _simpleIndicatorsFound;
        private float _simpleIndicatorSearchTimer;
        void Update()
        {
            bool prof = _profilerEnabled;
            if (prof) _profSw.Restart();

            // Stash cache rebuild: fires 3s after each hideout scene load
            if (prof) _profSw.Restart();
            if (_stashCacheRebuildTimer > 0f)
            {
                _stashCacheRebuildTimer -= Time.unscaledDeltaTime;
                if (_stashCacheRebuildTimer <= 0f)
                {
                    _stashCacheRebuildTimer = -1f;
                    if (LevelManager.Instance?.IsRaidMap != true)
                        try { RebuildStashCache(); } catch (Exception ex) { LogError("StashCache", ex); }
                }
            }
            if (prof) ProfMark(PI_STASH_CACHE);

            if (_traceGraceTimer > 0f) _traceGraceTimer -= Time.unscaledDeltaTime;

            // Only poll for tab injection when needed and in the right context.
            // - DDOL tab: inject once; flag is never reset on scene load (panel persists).
            // - MM tab: only attempt when not in a raid (LevelManager is null in menus).
            if (!_ddolTabInited || (!_mmTabInited && LevelManager.Instance == null))
            {
                _tabInjectTimer -= Time.unscaledDeltaTime;
                if (_tabInjectTimer <= 0f)
                {
                    _tabInjectTimer = 0.5f;
                    try { TryInjectSettingsTab(); } catch (Exception ex) { LogError("TabInject", ex); }
                }
            }
            if (prof) ProfMark(PI_TAB);

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
            if (prof) ProfMark(PI_LANG);
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
            if (prof) ProfMark(PI_HUD);
            // Keep enforcing: if hide is on and the game re-enabled it, hide it again
            if (_hideCtrlHint && _simpleIndicators != null && _simpleIndicators.gameObject.activeSelf)
                _simpleIndicators.gameObject.SetActive(false);
            if (prof) ProfMark(PI_CTRL);

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
            if (prof) ProfMark(PI_ADS);

            // Camera view: restore saved preference once on scene load
            if (_cameraViewMode != 0 && _viewRestorePending && LevelManager.Instance != null)
            {
                _viewRestorePending = false;
                bool wantTopDown = _cameraViewMode == 2;
                if (CameraArmGetTopDown() != wantTopDown)
                    CameraArm.ToggleView();
            }

            if (prof) ProfMark(PI_CAM);

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
            if (prof) ProfMark(PI_SCROLL);

            if (_questFavEnabled && Input.GetKeyDown(KeyCode.N))
                try { TryToggleQuestFavorite(); } catch (Exception ex) { LogError("QuestFavToggle", ex); }

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.D))
                try { DumpAllItems(); } catch (Exception ex) { LogError("DumpItems", ex); }

            if (Input.GetKey(KeyCode.LeftControl) && Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.T))
            {
                _traceEnabled = !_traceEnabled;
                if (_traceEnabled)
                {
                    _traceBuf.Clear();
                }
                else
                {
                    FlushTraceLog();
                }
                Debug.Log($"[Mod] Per-call trace: {(_traceEnabled ? "ON" : "OFF")}");
            }

            if (_questFavEnabled)
            {
                _questFavReorderTimer -= Time.unscaledDeltaTime;
                if (_questFavReorderTimer <= 0f)
                {
                    _questFavReorderTimer = 0.15f;
                    try { TryReorderQuestView(); } catch (Exception ex) { LogError("QuestFav", ex); }
                }
            }
            if (prof) ProfMark(PI_QUEST);

            if (_sleepPresetsEnabled)
                try { CheckSleepViewInjection(); } catch (Exception ex) { LogError("SleepPreset", ex); }
            if (prof) ProfMark(PI_SLEEP);

            if (_transferEnabled && Input.GetMouseButtonDown(0))
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
                bool mod = _transferModifier == TransferModifier.Shift ? shift : alt;
                if (mod) try { TryShiftClickTransfer(); } catch (Exception ex) { LogError("Transfer", ex); }
            }
            if (prof) ProfMark(PI_TRANSFER);

            if (_showEnemyNames)
            {
                _nameUpdateTimer -= Time.deltaTime;
                if (_nameUpdateTimer <= 0f)
                {
                    _nameUpdateTimer = 0.5f;
                    try { UpdateEnemyNameBars(); } catch (Exception ex) { LogError("EnemyNames", ex); }
                }
            }
            if (prof) ProfMark(PI_ENAMES);

            if (_showHiddenBars && LevelManager.Instance != null && LevelManager.Instance.IsRaidMap)
            {
                _hiddenBarScanTimer -= Time.deltaTime;
                if (_hiddenBarScanTimer <= 0f)
                {
                    _hiddenBarScanTimer = 5f;
                    _cachedAllHealth = FindObjectsOfType<Health>();
                    try { ForceShowHiddenBars(); } catch (Exception ex) { LogError("HiddenBars", ex); }
                }
            }
            if (prof) ProfMark(PI_HIDDEN_BARS);

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
                try { CheckAutoCloseContainer(activeLootView); } catch (Exception ex) { LogError("AutoClose", ex); }
            if (prof) ProfMark(PI_AUTOCLOSE);

            if ((_showRecorderBadge || _rarityDisplayEnabled) && _inventoryOpen)
            {
                // Periodic cache refresh every 3s
                _slotCacheTimer -= Time.deltaTime;
                if (_slotCacheTimer <= 0f) { _slotCacheTimer = 30f; _cachedSlots = null; _scanCursor = 0; }
                if (_slotActivityTimer > 0f) _slotActivityTimer -= Time.deltaTime;
                _badgeScanTimer -= Time.deltaTime;
                if (_badgeScanTimer <= 0f)
                {
                    const float actInterval = 0.2f;
                    bool cycleDone;
                    try { cycleDone = ScanSlots(int.MaxValue); } catch (Exception ex) { LogError("SlotScan", ex); cycleDone = true; }

                    if (!cycleDone)
                    {
                        _badgeScanTimer = 0f;
                    }
                    else
                    {
                        int prevCount = _lastSlotScanCount;
                        int newCount = _scanSeenIds.Count;
                        _lastSlotScanCount = newCount;
                        if (newCount > prevCount)
                        {
                            _slotActivityTimer = 0.5f;
                            _cachedSlots = null;
                            _badgeScanTimer = actInterval;
                        }
                        else if (_slotActivityTimer > 0f)
                        {
                            _cachedSlots = null;
                            _badgeScanTimer = actInterval;
                        }
                        else
                            _badgeScanTimer = actInterval;
                    }
                }
            }
            if (prof) ProfMark(PI_SLOTS);

            if (_autoUnloadEnabled)
                try { TryAutoUnloadLoot(activeLootView); } catch (Exception ex) { LogError("AutoUnload", ex); }
            if (prof) ProfMark(PI_AUTOUNLOAD);

            if (_lootboxHLMode > 0 && LevelManager.Instance != null && LevelManager.Instance.IsRaidMap)
                try { UpdateLootboxHighlight(); } catch (Exception ex) { LogError("LootboxHL", ex); }
            if (prof) ProfMark(PI_LOOTBOX);

            if (_killFeedEnabled)
                try { UpdateKillFeedEntries(); } catch (Exception ex) { LogError("KillFeed", ex); }
            if (prof) ProfMark(PI_KILLFEED);

            // Sort button hook scan
            if (prof) _profSw.Restart();
            if (_valueSortEnabled)
            {
                _sortBtnScanTimer -= Time.unscaledDeltaTime;
                if (_sortBtnScanTimer <= 0f)
                {
                    _sortBtnScanTimer = 1f;
                    try { TrySortButtonHook(); } catch (Exception ex) { LogError("SortHook", ex); }
                }
            }
            // Sort tooltip position follows cursor
            if (_sortTooltipGo != null && _sortTooltipGo.activeSelf && _sortTooltipRT != null)
                _sortTooltipRT.position = Input.mousePosition + new Vector3(0f, 36f, 0f);
            // Sort toast fade
            if (_sortToastCanvas != null && _sortToastCanvas.activeSelf)
            {
                _sortToastTimer -= Time.unscaledDeltaTime;
                if (_sortToastTimer <= 0f)
                    _sortToastCanvas.SetActive(false);
                else if (_sortToastTimer < SORT_TOAST_FADE && _sortToastTMP != null)
                {
                    var c = _sortToastTMP.color;
                    _sortToastTMP.color = new Color(c.r, c.g, c.b, _sortToastTimer / SORT_TOAST_FADE);
                }
            }
            if (prof) ProfMark(PI_SORT);

            if (prof) _profSw.Restart();
            if (_bmBasePriceEnabled && BlackMarket.Instance != null)
            {
                _bmScanTimer -= Time.unscaledDeltaTime;
                if (_bmScanTimer <= 0f)
                {
                    _bmScanTimer = 0.5f;
                    try { UpdateBMBasePriceInfo(); } catch (Exception ex) { LogError("BMBasePrice", ex); }
                }
            }
            if (prof) ProfMark(PI_BM);

            if (prof) _profSw.Restart();
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
            if (prof) ProfMark(PI_FPS);

            if (prof)
            {
                _profFrame++;
                if (_profFrame >= PROF_WINDOW)
                {
                    _profFrame = 0;
                    var snapSpikes = new int[_profSpikes.Length];
                    for (int i = 0; i < _profAccum.Length; i++)
                    {
                        _profAvgMs[i] = _profAccum[i] / (_profFreqMs * PROF_WINDOW);
                        _profMaxMs[i] = _profMax[i] == 0 ? 0f : _profMax[i] / _profFreqMs;
                        snapSpikes[i] = _profSpikes[i];
                        _profAccum[i] = 0;
                        _profMin[i] = long.MaxValue;
                        _profMax[i] = 0;
                        _profSpikes[i] = 0;
                    }
                    for (int i = 0; i < _evtAccum.Length; i++)
                    {
                        _evtAvgMs[i] = _evtCalls[i] > 0 ? _evtAccum[i] / (_profFreqMs * _evtCalls[i]) : 0f;
                        _evtMaxMs[i] = _evtMax[i] == 0 ? 0f : _evtMax[i] / _profFreqMs;
                        _evtAccum[i] = 0;
                        _evtMax[i] = 0;
                        _evtCalls[i] = 0;
                    }
                    _profWindowNum++;
                    UpdateProfilerDisplay(snapSpikes);
                    WriteProfLog(snapSpikes);
                    _profSw.Restart();
                    if (_traceEnabled) FlushTraceLog();
                    ProfMark(PI_TRACE);
                }
            }
        }

        void LateUpdate()
        {
            bool profBoss = _profilerEnabled;
            if (profBoss) _profSw.Restart();

            // Boss markers: update positions every frame
            if (_bossMarkersEnabled && _bossEntries.Count > 0)
            {
                try
                {
                    foreach (var e in _bossEntries.Values)
                    {
                        if (e.Alive && e.Go != null && e.Char != null)
                            e.Go.transform.position = e.Char.transform.position;
                    }
                }
                catch { }
            }

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

            // Boss markers: scan periodically during raids
            if (_bossMarkersEnabled && LevelManager.Instance != null)
            {
                _bossScanTimer -= Time.deltaTime;
                if (_bossScanTimer <= 0f)
                {
                    _bossScanTimer = 5f;
                    try { ScanBosses(); } catch (Exception ex) { LogError("BossScan", ex); }
                }
            }

            // Boss list HUD: refresh when map is open
            if (_bossMarkersEnabled && _mapOpen)
            {
                _bossListRefreshTimer -= Time.deltaTime;
                if (_bossListRefreshTimer <= 0f)
                {
                    _bossListRefreshTimer = 0.3f;
                    try { UpdateBossListHUD(); } catch { }
                }
            }
            if (profBoss) ProfMark(PI_BOSS);
        }

        // ── Enemy Name Bars ───────────────────────────────────────────────

        private bool _nameDebugLogged;
        private void UpdateEnemyNameBars()
        {
            // Refresh HealthBar list every 3s; text update runs every 0.5s on cached list
            _hbCacheTimer -= Time.deltaTime;
            if (_hbCacheTimer <= 0f || _cachedHealthBars == null)
            {
                _hbCacheTimer = 3f;
                _cachedHealthBars = FindObjectsOfType<HealthBar>();
                if (_petProxyType != null) _cachedPetProxies = FindObjectsOfType(_petProxyType);

                // Log once when we first find bars in scene
                if (!_nameDebugLogged && _cachedHealthBars.Length > 0)
                {
                    _nameDebugLogged = true;
                    var sb = new System.Text.StringBuilder();
                    sb.AppendLine($"[NameDebug] bars={_cachedHealthBars.Length}");
                    foreach (var hb in _cachedHealthBars)
                    {
                        if (hb == null) { sb.AppendLine("  bar=null"); continue; }
                        var h2 = hb.target;
                        var ch = h2?.TryGetCharacter();
                        var pr = ch != null ? _characterPresetField?.GetValue(ch) : null;
                        var dn = pr != null ? _displayNameProp?.GetValue(pr) as string : null;
                        var nt = _hbNameTextField?.GetValue(hb) as TextMeshProUGUI;
                        sb.AppendLine($"  active={hb.gameObject.activeInHierarchy} target={h2 != null} char={ch != null} isMain={ch?.IsMainCharacter} preset={pr != null} name='{dn}' tmp={nt != null}");
                    }
                    System.IO.File.AppendAllText(@"C:\Users\antob\AppData\LocalLow\TeamSoda\Duckov\mod_debug.txt", sb.ToString());
                }
            }

            foreach (var hb in _cachedHealthBars)
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

        private void ForceShowHiddenBars()
        {
            if (_healthShowBarProp == null) return;
            if (_cachedAllHealth == null) return;

            // Build covered set from already-cached health bars (no FindObjectsOfType)
            var covered = new HashSet<int>();
            if (_cachedHealthBars != null)
                foreach (var bar in _cachedHealthBars)
                    if (bar != null && bar.target != null)
                        covered.Add(bar.target.GetInstanceID());

            bool anyNew = false;
            var playerChar = LevelManager.Instance?.MainCharacter;
            foreach (var h in _cachedAllHealth)
            {
                if (h == null || !h.isActiveAndEnabled) continue;
                var id = h.GetInstanceID();
                if (_forcedBarIds.Contains(id)) continue; // already processed
                if (covered.Contains(id)) { _forcedBarIds.Add(id); continue; } // bar exists, mark done
                if (h.Invincible) continue;
                var character = h.TryGetCharacter();
                if (character == null || character.IsMainCharacter) continue;
                if (playerChar != null && character.Team == playerChar.Team) continue;
                _healthShowBarProp.SetValue(h, true);
                HealthBarManager.RequestHealthBar(h, null);
                _forcedBarIds.Add(id);
                anyNew = true;
            }
            if (anyNew) _hbCacheTimer = 0f;
        }

        // ── Auto-unload enemy weapons ──────���─────��────────��──��───────────��
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
            if (plug.MaxStackCount <= 1) return false;
            int plugCount = plug.StackCount;
            if (plugCount <= 0) return false;

            Item? target = null;
            if (inv.Content != null)
                foreach (var candidate in inv.Content)
                {
                    if (candidate == null || candidate == plug) continue;
                    if (candidate.TypeID != plug.TypeID) continue;
                    if (candidate.StackCount < candidate.MaxStackCount) { target = candidate; break; }
                }
            if (target == null) return false;

            int canFit = target.MaxStackCount - target.StackCount;
            int moved = Math.Min(plugCount, canFit);
            target.StackCount += moved;
            if (moved >= plugCount) return true;
            plug.StackCount -= moved;
            return false;
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
                        {
                            _lbType = t;
                            _lbInvRefField = t.GetField("inventoryReference",
                                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                        }
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
                _lootboxUpdateTimer = 0.5f;
                RefreshLootboxOutlines();
            }
        }

        private void ScanLootboxes()
        {
            EnsureLootboxTypes();
            if (_lbType == null) return;

            bool foundNew = false;
            foreach (UnityEngine.Object lb in FindObjectsOfType(_lbType))
            {
                var go = (lb as Component)?.gameObject;
                if (go == null) continue;
                int id = go.GetInstanceID();
                if (_lootboxOutlines.ContainsKey(id)) continue;
                var ol = go.GetComponent<Outlinable>() ?? go.AddComponent<Outlinable>();
                ol.AddAllChildRenderersToRenderingList();
                // Cache component refs once - avoids GetComponentInChildren on every refresh
                Component? marker = _imType != null ? go.GetComponentInChildren(_imType) : null;
                Inventory? inv = go.GetComponentInChildren<Inventory>();
                if (inv == null && _lbInvRefField != null)
                {
                    var lbComp = go.GetComponent(_lbType);
                    if (lbComp != null) inv = _lbInvRefField.GetValue(lbComp) as Inventory;
                }
                var entry = new LbEntry { Ol = ol, Marker = marker, Inv = inv };
                try { ol.OutlineParameters.Color = GetLootboxRarityColor(inv); } catch { }
                ol.enabled = true;
                _lootboxOutlines[id] = entry;
                foundNew = true;
            }
            // Back off to 30s when nothing new was found - new lootboxes mid-raid are rare
            if (!foundNew) _lootboxScanTimer = 30f;
        }

        private readonly List<int> _lbToRemove = new List<int>();
        private void RefreshLootboxOutlines()
        {
            _lbToRemove.Clear();
            foreach (var kvp in _lootboxOutlines)
            {
                var e = kvp.Value;
                var ol = e.Ol;
                if (ol == null) { _lbToRemove.Add(kvp.Key); continue; }
                var go = ol.gameObject;
                if (go == null) { _lbToRemove.Add(kvp.Key); continue; }

                bool show = go.activeInHierarchy;
                if (show)
                {
                    bool searched = false;
                    if (_imMarkedAsUsed != null && e.Marker != null)
                        try { searched = (bool)(_imMarkedAsUsed.GetValue(e.Marker) ?? false); } catch { }
                    if (!searched && _invInspectedField != null && e.Inv != null)
                        try { searched = (bool)(_invInspectedField.GetValue(e.Inv) ?? false); } catch { }
                    show = !searched;
                }
                if (show)
                {
                    if (_lootboxHLMode == 2)
                    {
                        // Only recompute rarity color when inventory content changes
                        int count = e.Inv?.Content?.Count ?? 0;
                        if (count != e.LastItemCount)
                        {
                            e.CachedColor = GetLootboxRarityColor(e.Inv);
                            e.LastItemCount = count;
                        }
                        try { ol.OutlineParameters.Color = e.CachedColor; } catch { }
                    }
                    else
                    {
                        try { ol.OutlineParameters.Color = new Color(1f, 0.75f, 0f, 1f); } catch { }
                    }
                }
                ol.enabled = show;
            }
            foreach (var k in _lbToRemove) _lootboxOutlines.Remove(k);
        }

        private static Color GetLootboxRarityColor(Inventory? inv)
        {
            var best = ItemValueLevel.White;
            if (inv != null)
            {
                foreach (var item in inv.Content)
                {
                    if (item == null) continue;
                    var lvl = RarityGetItemValueLevel(item);
                    if (lvl > best) best = lvl;
                    if (best == ItemValueLevel.Red) break;
                }
            }
            return RarityGetColor(best);
        }

        private void ClearLootboxOutlines()
        {
            foreach (var e in _lootboxOutlines.Values)
            {
                if (e.Ol == null) continue;
                e.Ol.enabled = false;
                try { Destroy(e.Ol); } catch { }
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
            // Use LevelManager or cached player ctrl - avoids FindObjectsOfType
            var main = LevelManager.Instance?.MainCharacter ?? _playerCtrl;
            if (main == null)
            {
                foreach (var ch in FindObjectsOfType<CharacterMainControl>())
                    if (ch.IsMainCharacter) { main = ch; break; }
            }
            if (main == null) return;
            foreach (var comp in main.GetComponents<Component>())
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

        private static bool IsRecipeRecorded(Item item)
        {
            try { return ItemUtilities.IsRegistered(item); }
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

        // Returns true when the full cycle is complete, false when more slots remain (incremental mode).
        private bool ScanSlots(int budget)
        {
            if (_slotCompType != null)
                return FastScanSlots(budget);
            else
            {
                BroadScanSlots();
                return true;
            }
        }

        private static bool IsHudSlot(MonoBehaviour mb)
        {
            var t = mb.transform.parent;
            while (t != null)
            {
                var n = t.name;
                if (n.IndexOf("Quick", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Hotbar", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("HUD", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("Toolbar", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
                t = t.parent;
            }
            return false;
        }

        // Returns true when the full cycle is complete (all slots scanned), false when budget was exhausted mid-cycle.
        private bool FastScanSlots(int budget)
        {
            if (_cachedSlots == null)
            {
                _cachedSlots = FindObjectsOfType(_slotCompType!);
                _scanCursor = 0;
            }

            // Start of a new cycle: clear seen-IDs accumulator
            if (_scanCursor == 0)
                _scanSeenIds.Clear();

            var seen = _scanSeenIds;
            int end = Math.Min(_scanCursor + budget, _cachedSlots.Length);

            for (int i = _scanCursor; i < end; i++)
            {
                var obj = _cachedSlots[i];
                var mb = obj as MonoBehaviour;
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                int id = mb.GetInstanceID();
                if (!_isHudSlotCache.TryGetValue(id, out bool isHud))
                {
                    isHud = IsHudSlot(mb);
                    _isHudSlotCache[id] = isHud;
                }
                if (isHud) continue;

                seen.Add(id);

                // Name style (one-shot cache per slot)
                if (!_nameStyleCache.ContainsKey(id))
                {
                    var nc = mb.transform.Find("Layout/NameContainer");
                    Graphic? ncG = nc?.GetComponent<Graphic>();
                    TextMeshProUGUI? tmp = nc?.GetComponentInChildren<TextMeshProUGUI>(true);
                    HorizontalLayoutGroup? hlg = nc?.GetComponent<HorizontalLayoutGroup>();
                    LayoutElement? le = nc != null ? (nc.GetComponent<LayoutElement>() ?? nc.gameObject.AddComponent<LayoutElement>()) : null;
                    _nameStyleCache[id] = (
                        ncG, ncG?.enabled ?? true,
                        tmp, tmp?.fontStyle ?? FontStyles.Bold, tmp?.alignment ?? TextAlignmentOptions.Right,
                        hlg, hlg?.childAlignment ?? TextAnchor.MiddleRight,
                        le, le?.flexibleWidth ?? -1f
                    );
                    ApplyNameStyle(id);
                }

                var item = ReadItemFromMember(_slotItemMember!, mb);

                bool inspected = item == null || item.InInventory == null
                    || !item.InInventory.NeedInspection || item.Inspected;

                // Badge
                if (_showRecorderBadge)
                {
                    bool showBadge = item != null && inspected && IsRecipeRecorded(item);
                    if (!_slotBadges.TryGetValue(id, out var badge))
                    {
                        if (showBadge) { badge = CreateSlotBadge(mb); _slotBadges[id] = badge; }
                    }
                    else badge?.SetActive(showBadge);
                }

                // Rarity overlay
                if (_rarityDisplayEnabled)
                    ApplyRarityOverlay(mb, id, item, inspected);
            }

            _scanCursor = end;

            if (_scanCursor >= _cachedSlots.Length)
            {
                // Full cycle complete - cleanup stale slots and reset cursor for next cycle
                _scanCursor = 0;
                CleanupDict(_slotBadges, seen);
                CleanupLabelGraphics(seen);
                return true;
            }
            return false; // budget exhausted - more slots remain
        }

        private void BroadScanSlots()
        {
            _scanSeenIds.Clear();
            var seen = _scanSeenIds;

            foreach (var mb in FindObjectsOfType<MonoBehaviour>())
            {
                if (mb == null || !mb.gameObject.activeInHierarchy) continue;
                if (!(mb.transform is RectTransform)) continue;
                if (mb.GetType() == GetType() || mb is ItemHoveringUI) continue;

                var tn = mb.GetType().Name;
                if (tn.Contains("HUD") || tn.Contains("Status") || tn.Contains("Stamina") ||
                    tn.Contains("Health") || tn.Contains("Energy") || tn.Contains("Equip") ||
                    tn.Contains("Button") || tn.Contains("Weapon") || tn.Contains("Action")) continue;

                var member = FindItemMember(mb.GetType());
                if (member == null) continue;

                Item? item;
                try { item = ReadItemFromMember(member, mb); } catch { continue; }

                if (item == null) continue;

                if (_slotCompType == null) { _slotCompType = mb.GetType(); _slotItemMember = member; }

                int id = mb.GetInstanceID();
                seen.Add(id);

                // Name style (one-shot cache per slot)
                if (!_nameStyleCache.ContainsKey(id))
                {
                    var nc = mb.transform.Find("Layout/NameContainer");
                    Graphic? ncG = nc?.GetComponent<Graphic>();
                    TextMeshProUGUI? tmp = nc?.GetComponentInChildren<TextMeshProUGUI>(true);
                    HorizontalLayoutGroup? hlg = nc?.GetComponent<HorizontalLayoutGroup>();
                    LayoutElement? le = nc != null ? (nc.GetComponent<LayoutElement>() ?? nc.gameObject.AddComponent<LayoutElement>()) : null;
                    _nameStyleCache[id] = (
                        ncG, ncG?.enabled ?? true,
                        tmp, tmp?.fontStyle ?? FontStyles.Bold, tmp?.alignment ?? TextAlignmentOptions.Right,
                        hlg, hlg?.childAlignment ?? TextAnchor.MiddleRight,
                        le, le?.flexibleWidth ?? -1f
                    );
                    ApplyNameStyle(id);
                }

                bool inspected = item.InInventory == null
                    || !item.InInventory.NeedInspection || item.Inspected;

                if (_showRecorderBadge)
                {
                    bool showBadge = inspected && IsRecipeRecorded(item);
                    if (!_slotBadges.TryGetValue(id, out var badge))
                    {
                        if (showBadge) { badge = CreateSlotBadge(mb); _slotBadges[id] = badge; }
                    }
                    else badge?.SetActive(showBadge);
                }

                if (_rarityDisplayEnabled)
                    ApplyRarityOverlay(mb, id, item, inspected);
            }

            CleanupDict(_slotBadges, seen);
            CleanupLabelGraphics(seen);
        }

        private void ApplyRarityOverlay(MonoBehaviour slot, int id, Item? item, bool inspected)
        {
            if (!_slotLabelGraphics.TryGetValue(id, out var entry))
            {
                var fr = slot.transform.Find("Frame");
                var g = fr?.GetComponent<Graphic>();
                if (g == null) return;
                entry = (g, g.color);
                _slotLabelGraphics[id] = entry;
            }

            if (item != null && !inspected)
            {
                if (!_inspectingGraphics.ContainsKey(item))
                    item.onInspectionStateChanged += OnRarityItemInspected;
                _inspectingGraphics[item] = entry;
            }

            if (item == null) { entry.g.color = entry.orig; return; }
            ItemValueLevel level;
            if (!inspected)
            {
                level = ItemValueLevel.White;
            }
            else
            {
                int itemId = item.GetInstanceID();
                if (!_rarityLevelCache.TryGetValue(itemId, out level))
                {
                    level = RarityGetItemValueLevel(item);
                    _rarityLevelCache[itemId] = level;
                }
            }
            var c = RarityGetColor(level);
            entry.g.color = new Color(c.r, c.g, c.b, 0.60f);
        }

        private static void SetSelectionIndicatorGlow(Graphic g, Color glowColor)
        {
            if (!_trueShadowSearched)
            {
                _trueShadowSearched = true;
                _trueShadowType = AppDomain.CurrentDomain.GetAssemblies()
                    .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                    .FirstOrDefault(t => t.FullName == "LeTai.TrueShadow.TrueShadow");
                _tsPropColor = _trueShadowType?.GetProperty("Color", BindingFlags.Public | BindingFlags.Instance);
            }
            if (_trueShadowType == null || g == null) return;
            var ts = g.GetComponent(_trueShadowType);
            if (ts == null) return;
            try { _tsPropColor?.SetValue(ts, glowColor); } catch { }
        }

        private void ApplyNameStyle(int id)
        {
            if (!_nameStyleCache.TryGetValue(id, out var e)) return;
            bool hideBg = _removeNameBg;
            bool removeBold = _removeNameBg;
            bool center = hideBg;
            if (e.g != null) e.g.enabled = !hideBg;
            if (e.le != null) e.le.flexibleWidth = center ? 1f : e.origFlexW;
            if (e.hlg != null) e.hlg.childAlignment = center ? TextAnchor.MiddleCenter : e.origHlgAlign;
            if (e.tmp != null) { e.tmp.fontStyle = removeBold ? FontStyles.Normal : e.origFontStyle; e.tmp.alignment = center ? TextAlignmentOptions.Center : e.origTmpAlign; }
        }

        private void RefreshNameStyle()
        {
            foreach (var kvp in _nameStyleCache) ApplyNameStyle(kvp.Key);
        }

        private void CleanupLabelGraphics(HashSet<int> seen)
        {
            var stale = new List<int>();
            foreach (var kvp in _slotLabelGraphics)
            {
                if (!seen.Contains(kvp.Key))
                {
                    if (kvp.Value.g != null) kvp.Value.g.color = kvp.Value.orig;
                    stale.Add(kvp.Key);
                }
            }
            foreach (var k in stale) { _slotLabelGraphics.Remove(k); _nameStyleCache.Remove(k); }
        }

        private void CleanupDict(Dictionary<int, GameObject> dict, HashSet<int> seen)
        {
            var stale = new List<int>();
            foreach (var kvp in dict)
            {
                if (!seen.Contains(kvp.Key))
                {
                    if (kvp.Value != null) Destroy(kvp.Value);
                    stale.Add(kvp.Key);
                }
            }
            foreach (var k in stale) dict.Remove(k);
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
            ["Default"] = new[] { "Défaut", "Standard", "默认", "預設", "デフォルト", "기본", "Padrão", "По умолчанию", "Predeterminado" },
            ["Combined"] = new[] { "Combiné", "Kombiniert", "组合", "組合", "複合", "복합", "Combinado", "Комбинированный", "Combinado" },
            ["Single"] = new[] { "Unité", "Einzeln", "单个", "單個", "単体", "단일", "Unitário", "Единица", "Unidad" },
            ["Stack"] = new[] { "Pile", "Stapel", "堆叠", "堆疊", "スタック", "스택", "Pilha", "Стопка", "Pila" },
            ["By item rarity"] = new[] { "Par rareté d'objet", "Nach Seltenheit", "按物品稀有度", "按物品稀有度", "アイテムレアリティ別", "아이템 희귀도별", "Por raridade", "По редкости предмета", "Por rareza" },
            ["Performance profiler"] = new[] { "Profileur de performance", "Leistungsprofiler", "性能分析器", "效能分析器", "パフォーマンスプロファイラー", "성능 프로파일러", "Analisador de performance", "Профилировщик", "Analizador de rendimiento" },
            ["Hide all"] = new[] { "Tout masquer", "Alles ausblenden", "隐藏全部", "隱藏全部", "全て非表示", "모두 숨기기", "Ocultar tudo", "Скрыть всё", "Ocultar todo" },
            ["Show Only Ammo"] = new[] { "Afficher seulement munitions", "Nur Munition anzeigen", "仅显示弹药", "僅顯示彈藥", "弾薬のみ表示", "탄약만 표시", "Mostrar só munição", "Только патроны", "Solo munición" },
            // Feature labels
            ["Show item value on hover"] = new[] { "Afficher la valeur de l'objet", "Artikelwert beim Hover", "悬停显示物品价值", "懸停顯示物品價值", "ホバー時に価値を表示", "호버 시 아이템 가치 표시", "Mostrar valor do item", "Стоимость при наведении", "Mostrar valor al pasar" },
            ["Quick item transfer"] = new[] { "Transfert rapide", "Schnelltransfer", "快速转移物品", "快速轉移物品", "クイックアイテム移動", "빠른 아이템 이동", "Transferir item rapidamente", "Быстрый перенос", "Transferencia rápida" },
            ["Lootbox highlight"] = new[] { "Surligner les caisses", "Loot-Kisten hervorheben", "高亮战利品箱", "高亮戰利品箱", "ルートボックス強調", "루트박스 강조", "Destacar caixas de loot", "Подсветка контейнеров", "Resaltar cajas de botín" },
            ["Badge on recorded keys and Blueprints"] = new[] { "Badge sur clés/plans enregistrés", "Badge erfasste Schlüssel/Pläne", "已记录钥匙和蓝图徽章", "已記錄鑰匙和藍圖徽章", "記録済みキー/設計図バッジ", "기록된 열쇠/청사진 뱃지", "Badge em chaves/plantas registradas", "Значок записанных ключей/схем", "Insignia en llaves/planos registrados" },
            ["Show enemy name"] = new[] { "Afficher le nom ennemi", "Feindnamen anzeigen", "显示敌人名称", "顯示敵人名稱", "敵の名前を表示", "적 이름 표시", "Mostrar nome inimigo", "Показывать имя врага", "Mostrar nombre enemigo" },
            ["Show hidden enemy health bars"] = new[] { "Afficher les barres de vie cachées", "Versteckte Lebensbalken anzeigen", "显示隐藏的敌人血条", "顯示隱藏的敵人血條", "隠れた敵の体力バーを表示", "숨겨진 적 체력 바 표시", "Mostrar barras de vida ocultas", "Показывать скрытые полосы здоровья", "Mostrar barras de vida ocultas" },
            ["Auto-unload gun on kill"] = new[] { "Décharger arme à la mort", "Waffe bei Tod entladen", "击杀时自动卸弹", "擊殺時自動卸彈", "キル時に自動アンロード", "처치 시 총알 제거", "Descarregar arma ao matar", "Авто-разрядка при убийстве", "Descargar arma al matar" },
            ["Kill feed"] = new[] { "Fil de mort", "Kill-Feed", "击杀信息流", "擊殺信息流", "キルフィード", "킬 피드", "Feed de abates", "Лента убийств", "Feed de bajas" },
            ["Boss map markers"] = new[] { "Marqueurs boss sur la carte", "Boss-Kartenmarkierungen", "地图上的Boss标记", "地圖上的Boss標記", "ボスマップマーカー", "보스 지도 마커", "Marcadores de boss no mapa", "Маркеры боссов на карте", "Marcadores de jefes en el mapa" },
            ["Inventory count on hover"] = new[] { "Nombre d'objets au survol", "Anzahl im Inventar (Hover)", "悬停时显示物品数量", "懸停時顯示物品數量", "ホバー時在庫数表示", "호버 시 인벤토리 수량 표시", "Contagem no inventário ao passar", "Кол-во предметов при наведении", "Cantidad en inventario al pasar" },
            ["Carried"] = new[] { "Porté", "Getragen", "携带", "攜帶", "所持", "소지", "Carregado", "В инвентаре", "Llevado" },
            ["Stash"] = new[] { "Coffre", "Lager", "仓库", "倉庫", "ストレージ", "보관함", "Estoque", "Склад", "Almacén" },
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
            ["Performance trace log"] = new[] { "Journal de performance", "Leistungsprotokoll", "性能追踪日志", "效能追蹤日誌", "パフォーマンストレースログ", "성능 추적 로그", "Log de rastreamento", "Лог трассировки", "Registro de rendimiento" },
            ["Hide controls hint"] = new[] { "Masquer l'aide de contrôle", "Steuerungshinweis ausblenden", "隐藏操作提示", "隱藏操作提示", "操作ヒントを非表示", "조작 힌트 숨기기", "Ocultar dica de controles", "Скрыть подсказку управления", "Ocultar ayuda de controles" },
            ["Hide HUD on ADS"] = new[] { "Masquer HUD en visée", "HUD beim Zielen ausblenden", "瞄准时隐藏HUD", "瞄準時隱藏HUD", "ADS中HUDを非表示", "ADS 시 HUD 숨기기", "Ocultar HUD ao mirar", "Скрывать HUD при прицеливании", "Ocultar HUD al apuntar" },
            ["Camera view"] = new[] { "Vue caméra", "Kameraansicht", "相机视角", "相機視角", "カメラビュー", "카메라 뷰", "Vista de câmera", "Вид камеры", "Vista de cámara" },
            ["Top-down"] = new[] { "Vue du dessus", "Von oben", "俯视", "俯視", "トップダウン", "탑다운", "Vista de cima", "Сверху вниз", "Vista superior" },
            ["Quest favorites (N key)"] = new[] { "Favoris (touche N)", "Favoriten (Taste N)", "任务收藏 (N键)", "任務收藏 (N鍵)", "お気に入り (Nキー)", "즐겨찾기 (N키)", "Favoritos (tecla N)", "Избранные (клавиша N)", "Favoritos (tecla N)" },
            ["Item rarity display"] = new[] { "Affichage rareté objet", "Seltenheitsanzeige", "物品稀有度显示", "物品稀有度顯示", "アイテムレアリティ表示", "아이템 희귀도 표시", "Exibir raridade do item", "Отображение редкости", "Mostrar rareza del ítem" },
            ["Sound on item reveal"] = new[] { "Son à la révélation", "Ton bei Enthüllung", "物品揭示音效", "物品揭示音效", "アイテム解析音", "아이템 공개 효과음", "Som ao revelar item", "Звук при раскрытии", "Sonido al revelar ítem" },
            ["Remove item name background"] = new[] { "Retirer fond du nom", "Namenshintergrund entf.", "去除物品名称背景", "去除物品名稱背景", "アイテム名背景を削除", "아이템 이름 배경 제거", "Remover fundo do nome", "Убрать фон имени", "Quitar fondo del nombre" },
            ["Sort by value (sort button)"] = new[] { "Tri par valeur (bouton)", "Nach Wert sortieren (Schaltfläche)", "按价值排序（按钮）", "按價值排序（按鈕）", "価値でソート（ボタン）", "가치별 정렬 (버튼)", "Ordenar por valor (botão)", "Сортировка по ценности (кнопка)", "Ordenar por valor (botón)" },
            ["Black market base price"] = new[] { "Prix de base marché noir", "Schwarzmarkt Basispreis", "黑市基础价格", "黑市基礎價格", "ブラックマーケット基準価格", "암시장 기본 가격", "Preço base mercado negro", "Базовая цена черного рынка", "Precio base mercado negro" },
            ["Sort: Native"] = new[] { "Tri : Natif", "Sortieren: Nativ", "排序：原始", "排序：原生", "ソート：ネイティブ", "정렬: 기본", "Ordenar: Nativo", "Сортировка: Стандарт", "Orden: Nativo" },
            ["Sort: Value"] = new[] { "Tri : Valeur", "Sortieren: Wert", "排序：单价", "排序：單價", "ソート：価値", "정렬: 가치", "Ordenar: Valor", "Сортировка: Ценность", "Orden: Valor" },
            ["Sort: Stack value"] = new[] { "Tri : Valeur pile", "Sortieren: Stapelwert", "排序：堆叠价值", "排序：堆疊價值", "ソート：スタック価値", "정렬: 스택 가치", "Ordenar: Valor pilha", "Сортировка: Ценность стопки", "Orden: Valor pila" },
            ["Sort: Value per kg"] = new[] { "Tri : Valeur/kg", "Sortieren: Wert/kg", "排序：每千克价值", "排序：每公斤價值", "ソート：kg当たり価値", "정렬: kg당 가치", "Ordenar: Valor/kg", "Сортировка: Ценность за кг", "Orden: Valor/kg" },
            ["Native sort\nDefault game ordering"] = new[] { "Tri natif\nOrdre par défaut du jeu", "Natives Sortieren\nStandardreihenfolge des Spiels", "原始排序\n游戏默认排列顺序", "原始排序\n遊戲預設排列順序", "デフォルト並べ替え\nゲームの標準順序", "기본 정렬\n게임 기본 순서", "Ordenação nativa\nOrdem padrão do jogo", "Стандартная сортировка\nПорядок по умолчанию", "Orden nativo\nOrden predeterminado del juego" },
            ["Sort by value\nMost valuable items first"] = new[] { "Tri par valeur\nObjets les plus chers en premier", "Nach Wert sortieren\nWertvollste Items zuerst", "按价值排序\n最贵的物品排在最前", "按價值排序\n最貴的物品排在最前", "価値順で並べ替え\n最も価値の高いアイテムを先頭へ", "가치순 정렬\n가장 비싼 아이템 먼저", "Ordenar por valor\nItens mais valiosos primeiro", "Сортировка по цене\nСамые ценные предметы первыми", "Ordenar por valor\nObjetos más valiosos primero" },
            ["Sort by stack total\nHighest stack price first"] = new[] { "Tri par valeur de pile\nPile la plus chère en premier", "Nach Stapelwert sortieren\nTeuerster Stapel zuerst", "按堆叠总价排序\n最高总价的堆叠排在最前", "按堆疊總價排序\n最高總價的堆疊排在最前", "スタック合計価値順\n合計価値が最も高いスタックを先頭へ", "스택 총가치순 정렬\n스택 총가격이 가장 높은 것 먼저", "Ordenar por valor de pilha\nPilha mais cara primeiro", "Сортировка по цене стека\nСтек с наибольшей ценой первым", "Ordenar por valor de pila\nPila más cara primero" },
            ["Sort by value / kg\nBest price-to-weight ratio"] = new[] { "Tri par valeur / kg\nMeilleur rapport qualité-poids", "Nach Wert/kg sortieren\nBestes Preis-Gewicht-Verhältnis", "按每千克价值排序\n最佳性价比排在最前", "按每千克價值排序\n最佳性價比排在最前", "kg当たり価値順\n最良の価格重量比を先頭へ", "kg당 가치순 정렬\n최고의 가격 대비 무게 비율 먼저", "Ordenar por valor / kg\nMelhor relação preço-peso", "Сортировка по цене за кг\nЛучшее соотношение цена-вес", "Ordenar por valor / kg\nMejor relación precio-peso" },
            ["Development"] = new[] { "Développement", "Entwicklung", "开发", "開發", "開発", "개발", "Desenvolvimento", "Разработка", "Desarrollo" },
        };

        private static string L(string key)
        {
            var lang = GetGameLanguage();
            int idx = Array.IndexOf(_langOrder, lang);
            if (idx >= 0 && _t.TryGetValue(key, out var arr) && idx < arr.Length)
                return arr[idx];
            return key;
        }

        // ── Item Rarity Display & Search Sounds ───────────────────────────

        private void InitRaritySystem()
        {
            ColorUtility.TryParseHtmlString("#FFFFFFBF", out S_CWhite);
            ColorUtility.TryParseHtmlString("#00C800BF", out S_CGreen);
            ColorUtility.TryParseHtmlString("#0070DDBF", out S_CBlue);
            ColorUtility.TryParseHtmlString("#A335EEBF", out S_CPurple);
            ColorUtility.TryParseHtmlString("#FF8C00BF", out S_COrange);
            ColorUtility.TryParseHtmlString("#FF4040BF", out S_CLightRed);
            ColorUtility.TryParseHtmlString("#CC0000BF", out S_CRed);
            try
            {
                S_DynEntryMap = typeof(ItemAssetsCollection)
                    .GetField("dynamicDic", BindingFlags.Static | BindingFlags.NonPublic)
                    ?.GetValue(null) as System.Collections.IDictionary;
            }
            catch { }
        }

        private void CleanupRaritySystem()
        {
            foreach (var kvp in _inspectingGraphics)
                kvp.Key.onInspectionStateChanged -= OnRarityItemInspected;
            _inspectingGraphics.Clear();
            foreach (var kvp in _slotLabelGraphics)
                if (kvp.Value.g != null) kvp.Value.g.color = kvp.Value.orig;
            _slotLabelGraphics.Clear();
        }

        private void OnRarityStartLoot(InteractableLootbox lootbox)
        {
            if (_profilerEnabled) _evtSw.Restart();
            if (!_rarityDisplayEnabled && !_showRecorderBadge) { if (_profilerEnabled) ProfMarkEvt(EVT_LOOT); return; }
            _badgeScanTimer = 0f;
            _lootboxScanTimer = 0f;
            if (_profilerEnabled) ProfMarkEvt(EVT_LOOT);
        }

        // Fires on every hover - triggers a scan soon after interaction
        // (covers item pickup, transfer, move without hammering every frame)
        private void OnSlotScanTrigger(ItemHoveringUI ui, Item item)
        {
            if ((_rarityDisplayEnabled || _showRecorderBadge) && _badgeScanTimer > 0.15f)
                _badgeScanTimer = 0.15f;
        }

        private void OnRarityItemInspected(Item item)
        {
            if (!item.Inspected) return;
            item.onInspectionStateChanged -= OnRarityItemInspected;
            var level = RarityGetItemValueLevel(item);
            // Update glow immediately - no scan delay
            if (_inspectingGraphics.TryGetValue(item, out var entry))
            {
                _inspectingGraphics.Remove(item);
                if (entry.g != null)
                {
                    var c = RarityGetColor(level);
                    entry.g.color = new Color(c.r, c.g, c.b, 0.60f);
                }
            }
            if (_raritySoundEnabled)
            {
                var soundLevel = ForceWhiteTypeIDs.Contains(item.TypeID) ? ItemValueLevel.White : level;
                RarityPlaySound(soundLevel);
            }
            _badgeScanTimer = 0f; // trigger scan to show badge if item is recorded key/blueprint
        }

        private static MethodInfo? _fmodCreateInstance;

        private static void RarityPlaySound(ItemValueLevel level)
        {
            try
            {
                string eventName;
                float volume;
                if (level == ItemValueLevel.Orange || level == ItemValueLevel.LightRed || level == ItemValueLevel.Red)
                { eventName = "UI/game_start"; volume = 1f; }
                else if (level == ItemValueLevel.Blue || level == ItemValueLevel.Purple)
                { eventName = "UI/sceneloader_click"; volume = 3f; }
                else
                { eventName = "UI/hover"; volume = 5f; }

                if (_fmodCreateInstance == null)
                {
                    var rmType = AppDomain.CurrentDomain.GetAssemblies()
                        .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                        .FirstOrDefault(t => t.FullName == "FMODUnity.RuntimeManager");
                    _fmodCreateInstance = rmType?.GetMethod("CreateInstance",
                        BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                }
                var inst = _fmodCreateInstance?.Invoke(null, new object[] { "event:/" + eventName });
                if (inst == null) return;
                var it = inst.GetType();
                it.GetMethod("setVolume")?.Invoke(inst, new object[] { volume });
                it.GetMethod("start")?.Invoke(inst, null);
                it.GetMethod("release")?.Invoke(inst, null);
            }
            catch { }
        }

        private static ItemValueLevel RarityGetItemValueLevel(Item item)
        {
            if (item == null) return ItemValueLevel.White;
            float sell = item.Value / 2f;

            if (S_DynEntryMap != null && S_DynEntryMap.Contains(item.TypeID))
            {
                if ((int)item.DisplayQuality != 0) return RarityParseDisplayQuality(item);
                return RarityParseQuality(item.Quality);
            }
            if (item.Tags.Contains("Bullet"))
            {
                if ((int)item.DisplayQuality != 0)
                    return (int)item.DisplayQuality == 5 ? ItemValueLevel.LightRed : RarityParseDisplayQuality(item);
                if (item.Quality == 1) return ItemValueLevel.White;
                if (item.Quality == 2) return ItemValueLevel.Green;
                var bl = RarityCalcLevel((int)(sell * 30f));
                return bl > ItemValueLevel.Orange ? ItemValueLevel.Orange : bl;
            }
            if (item.Tags.Contains("Equipment"))
            {
                if (item.Tags.Contains("Special"))
                {
                    if (((UnityEngine.Object)item).name.Contains("StormProtection"))
                        return (ItemValueLevel)Math.Clamp(item.Quality - 1, 0, 6);
                    int q = item.Quality - 2;
                    if (q > 6) return ItemValueLevel.Red;
                    if (q < 0) return ItemValueLevel.White;
                    return (ItemValueLevel)q;
                }
                if (item.Quality <= 7) return (ItemValueLevel)Math.Clamp(item.Quality - 1, 0, 6);
                return RarityCalcLevel((int)sell);
            }
            if (item.Tags.Contains("Accessory"))
            {
                if (item.Quality <= 7) return (ItemValueLevel)Math.Clamp(item.Quality - 1, 0, 6);
                return RarityParseDisplayQuality(item);
            }
            if (item.TypeID == 862 || item.TypeID == 1238) return ItemValueLevel.Orange;

            var calc = RarityCalcLevel((int)sell);
            var disp = RarityParseDisplayQuality(item);
            return disp > calc ? disp : calc;
        }

        private static ItemValueLevel RarityCalcLevel(int value)
        {
            if (value >= 10000) return ItemValueLevel.Red;
            if (value >= 5000) return ItemValueLevel.LightRed;
            if (value >= 2500) return ItemValueLevel.Orange;
            if (value >= 1200) return ItemValueLevel.Purple;
            if (value >= 600) return ItemValueLevel.Blue;
            if (value >= 200) return ItemValueLevel.Green;
            return ItemValueLevel.White;
        }

        private static ItemValueLevel RarityParseDisplayQuality(Item item)
        {
            switch ((int)item.DisplayQuality)
            {
                case 2: return ItemValueLevel.Green;
                case 3: return ItemValueLevel.Blue;
                case 4: return ItemValueLevel.Purple;
                case 5: return ItemValueLevel.Orange;
                case 6: return item.Quality == 6 ? ItemValueLevel.LightRed : ItemValueLevel.Red;
                case 7:
                case 8: return ItemValueLevel.Red;
                default: return ItemValueLevel.White;
            }
        }

        private static ItemValueLevel RarityParseQuality(int quality) =>
            (ItemValueLevel)Math.Clamp(quality - 1, 0, 6);

        private static Color RarityGetColor(ItemValueLevel level) => level switch
        {
            ItemValueLevel.Red => S_CRed,
            ItemValueLevel.LightRed => S_CLightRed,
            ItemValueLevel.Orange => S_COrange,
            ItemValueLevel.Purple => S_CPurple,
            ItemValueLevel.Blue => S_CBlue,
            ItemValueLevel.Green => S_CGreen,
            _ => S_CWhite,
        };

        // ── Item Hover UI ─────────────────────────────────────────────────

        private void OnSetupMeta(ItemHoveringUI ui, ItemMetaData data)
        {
            _lastHoveredItem = null;
            ValueText.gameObject.SetActive(false);
            if (_invCountText != null) _invCountText.gameObject.SetActive(false);
        }

        private void OnSetupItemHoveringUI(ItemHoveringUI uiInstance, Item item)
        {
            if (_profilerEnabled) _evtSw.Restart();
            _lastHoveredItem = item;

            // Use hover event to discover slot component type (most reliable method)
            if (_showRecorderBadge && item != null && _slotCompType == null)
                TryCacheSlotTypeFromHover(item);


            if (!_showValue || item == null)
            {
                ValueText.gameObject.SetActive(false);
                ShowInvCount(uiInstance, item);
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

            ShowInvCount(uiInstance, item);
            if (_profilerEnabled) ProfMarkEvt(EVT_HOVER);
        }

        private void RebuildStashCache()
        {
            // Find PlayerStorage type once via reflection (it's in TeamSoda.Duckov.Core, namespace unknown)
            if (!_psReflectionSearched)
            {
                _psReflectionSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        _psType = asm.GetType("PlayerStorage");
                        if (_psType == null)
                            foreach (var t in asm.GetTypes())
                                if (t.Name == "PlayerStorage") { _psType = t; break; }
                        if (_psType != null)
                        {
                            _psInvProp = _psType.GetProperty("Inventory")
                                      ?? _psType.GetProperty("PlayerStorageInventory")
                                      ?? _psType.GetProperty("StorageInventory");
                            break;
                        }
                    }
                    catch { }
                }
            }

            if (_psType == null || _psInvProp == null) return;

            // Get the PlayerStorage instance - try as Unity Component first, then static Instance
            object? ps = null;
            if (typeof(UnityEngine.Component).IsAssignableFrom(_psType))
                ps = FindObjectOfType(_psType);
            if (ps == null)
                ps = _psType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null)
                  ?? _psType.GetProperty("instance", BindingFlags.Public | BindingFlags.Static)?.GetValue(null);
            if (ps == null) return;

            var inv = _psInvProp.GetValue(ps) as Inventory;
            if (inv?.Content == null) return;

            _stashCountCache.Clear();
            foreach (var item in inv.Content)
            {
                if (item == null) continue;
                _stashCountCache.TryGetValue(item.TypeID, out var cur);
                _stashCountCache[item.TypeID] = cur + item.StackCount;
            }

        }

        private void ShowInvCount(ItemHoveringUI uiInstance, Item item)
        {
            if (!_invCountEnabled || item == null) { if (_invCountText != null) _invCountText.gameObject.SetActive(false); return; }
            try
            {
                int typeId = item.TypeID;
                int carried = 0, inStash = 0;

                var playerChar = LevelManager.Instance?.MainCharacter;
                var charItem = playerChar?.CharacterItem;

                // Player backpack
                if (charItem?.Inventory?.Content != null)
                    foreach (var e in charItem.Inventory.Content)
                        if (e != null && e.TypeID == typeId) carried += e.StackCount;

                // Player equipment slots: weapons, armor, helmet, face mask
                // Use public CharacterMainControl methods - no reflection needed
                if (playerChar != null)
                {
                    var equipItems = new Item?[] {
                        playerChar.PrimWeaponSlot()?.Content,
                        playerChar.SecWeaponSlot()?.Content,
                        playerChar.MeleeWeaponSlot()?.Content,
                        playerChar.GetArmorItem(),
                        playerChar.GetHelmatItem(),
                        playerChar.GetFaceMaskItem(),
                    };
                    foreach (var equipped in equipItems)
                    {
                        if (equipped == null) continue;
                        if (equipped.TypeID == typeId) carried += equipped.StackCount;
                        // ammo/mods loaded into weapons
                        foreach (var nested in GetItemPlugs(equipped))
                            if (nested != null && nested.TypeID == typeId) carried += nested.StackCount;
                    }
                }

                // Pet inventory via PetProxy (type pre-discovered in Awake, instances cached every 3s)
                if (_petProxyType != null && _cachedPetProxies != null)
                    foreach (var proxy in _cachedPetProxies)
                    {
                        var petInv = _petProxyInvProp?.GetValue(proxy) as Inventory;
                        if (petInv?.Content != null)
                            foreach (var e in petInv.Content)
                                if (e != null && e.TypeID == typeId) carried += e.StackCount;
                    }

                // GetItemCount only works outside raids (stash not loaded in raid scene).
                // Cache the value from the hideout and reuse it during raids.
                bool inRaid = LevelManager.Instance != null && LevelManager.Instance.IsRaidMap;
                if (inRaid)
                {
                    _stashCountCache.TryGetValue(typeId, out inStash);
                }
                else
                {
                    inStash = ItemUtilities.GetItemCount(typeId);
                    _stashCountCache[typeId] = inStash;
                }

                string txt = carried > 0
                    ? $"<color=#AAAAAA>{L("Carried")}: {carried} | {L("Stash")}: {inStash}</color>"
                    : $"<color=#AAAAAA>{L("Stash")}: {inStash}</color>";

                var t = InvCountText;
                t.gameObject.SetActive(true);
                t.transform.SetParent(uiInstance.LayoutParent);
                t.transform.localScale = Vector3.one;
                t.text = txt;
                t.fontSize = 18f;
            }
            catch { if (_invCountText != null) _invCountText.gameObject.SetActive(false); }
        }

        // ── Kill Feed ─────────────────────────────────────────────────────

        // Subscribed to the static Health.OnDead event (same approach as the KillFeed mod).
        // Fires for every death in the game - no scanning, no polling needed.
        private void OnKillFeedDeadDirect(Health health, DamageInfo dmgInfo)
        {
            if (_profilerEnabled) _evtSw.Restart();
            // Enemy died - may drop a loot container, trigger a lootbox rescan shortly after
            if (_lootboxHLMode != 0) _lootboxScanTimer = 0f;
            // Boss marker death tracking
            if (_bossMarkersEnabled && health != null)
            {
                try
                {
                    var c = health.GetComponent<CharacterMainControl>();
                    if (c != null && _bossEntries.TryGetValue(c, out var bossEntry))
                    {
                        bossEntry.Alive = false;
                        bossEntry.Poi.Color = new Color(0.5f, 0.5f, 0.5f, 0.6f);
                        if (_mapOpen) UpdateBossListHUD();
                    }
                }
                catch { }
            }
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
            if (_profilerEnabled) ProfMarkEvt(EVT_KILL);
        }

        private void OnAnyUIPanelOpen(ManagedUIElement panel)
        {
            if (!(_showRecorderBadge || _rarityDisplayEnabled)) return;
            if (!IsInventoryPanel(panel.GetType().Name)) return;
            _inventoryOpen = true;
            _badgeScanTimer = 0f;
            _slotActivityTimer = 0.5f;
            _cachedSlots = null;
            _scanCursor = 0;
        }

        private void OnAnyUIPanelClose(ManagedUIElement panel)
        {
            if (!(_showRecorderBadge || _rarityDisplayEnabled)) return;
            if (!IsInventoryPanel(panel.GetType().Name)) return;
            _inventoryOpen = false;
            _lastSlotScanCount = 0;
            _cachedSlots = null;
            _scanCursor = 0;
        }

        private static bool IsInventoryPanel(string typeName) =>
            typeName == "LootView" || typeName == "StockShopView" ||
            typeName == "InventoryView" || typeName == "StorageDock";

        // ── Boss map markers ──────────────────────────────────────────────

        private void OnBossMapViewChanged()
        {
            try
            {
                // Any view change (inventory, trader, stash, map...) - wake up slot scan immediately
                if (_showRecorderBadge || _rarityDisplayEnabled)
                    _badgeScanTimer = 0f;

                bool open = View.ActiveView is MiniMapView;
                if (open == _mapOpen) return;
                _mapOpen = open;
                if (open)
                {
                    if (_bossMarkersEnabled)
                    {
                        EnsureBossListCanvas();
                        _bossListCanvas!.SetActive(true);
                        UpdateBossListHUD();
                    }
                }
                else
                {
                    if (_bossListCanvas != null) _bossListCanvas.SetActive(false);
                }
            }
            catch { }
        }

        private void EnsureBossListCanvas()
        {
            if (_bossListCanvas != null) return;
            _bossListCanvas = new GameObject("BossListHUD");
            DontDestroyOnLoad(_bossListCanvas);
            var canvas = _bossListCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _bossListCanvas.AddComponent<CanvasScaler>();
            _bossListCanvas.AddComponent<GraphicRaycaster>();

            var go = new GameObject("BossListText");
            go.transform.SetParent(_bossListCanvas.transform, false);
            _bossListTMP = go.AddComponent<TextMeshProUGUI>();
            _bossListTMP.fontSize = 16f;
            _bossListTMP.color = Color.white;
            _bossListTMP.alignment = TextAlignmentOptions.TopLeft;
            _bossListTMP.enableWordWrapping = false;
            _bossListTMP.richText = true;
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(10f, -10f);
            rt.sizeDelta = new Vector2(320f, 500f);
            var shadow = go.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.9f);
            shadow.effectDistance = new Vector2(1f, -1f);
            _bossListCanvas.SetActive(false);
        }

        private void ScanBosses()
        {
            if (!_bossMarkersEnabled) return;
            try
            {
                if (_cachedBossSpawners == null || _cachedBossSpawners.Length == 0 ||
                    System.Array.Exists(_cachedBossSpawners, r => r == null))
                    _cachedBossSpawners = FindObjectsOfType<CharacterSpawnerRoot>(true);

                foreach (var root in _cachedBossSpawners)
                {
                    if (root == null) continue;
                    var chars = root.CreatedCharacters;
                    if (chars == null) continue;
                    foreach (var c in chars)
                    {
                        if (c == null || _bossEntries.ContainsKey(c)) continue;
                        if (!IsBossCharacter(c)) continue;
                        AddBossMarker(c, GetBossDisplayName(c));
                    }
                }
            }
            catch (Exception ex) { LogError("BossScan", ex); }
        }

        private bool IsBossCharacter(CharacterMainControl c)
        {
            try
            {
                if (!_charIconTypeSearched)
                {
                    _charIconTypeSearched = true;
                    var presetType = c.characterPreset?.GetType();
                    if (presetType != null)
                        _charIconTypeField = presetType.GetField("characterIconType",
                            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
                if (_charIconTypeField == null || c.characterPreset == null) return false;
                int iconType = (int)_charIconTypeField.GetValue(c.characterPreset);
                return iconType == 3; // CharacterIconTypes.boss
            }
            catch { return false; }
        }

        private string GetBossDisplayName(CharacterMainControl c)
        {
            try
            {
                return c.characterPreset != null ? c.characterPreset.DisplayName : "Boss";
            }
            catch { return "Boss"; }
        }

        private void AddBossMarker(CharacterMainControl c, string name)
        {
            try
            {
                var go = new GameObject("BossMarker_" + name);
                go.transform.position = c.transform.position;
                if (MultiSceneCore.MainScene.HasValue)
                    SceneManager.MoveGameObjectToScene(go, MultiSceneCore.MainScene.Value);
                var poi = go.AddComponent<SimplePointOfInterest>();
                poi.Color = new Color(1f, 0.25f, 0.25f, 1f);
                var icon = GetBossMapIcon();
                poi.Setup(icon, name, true, null);
                _bossEntries[c] = new BossEntry { Char = c, Go = go, Poi = poi, Alive = true, Name = name };
            }
            catch (Exception ex) { LogError("BossAddMarker", ex); }
        }

        private static Sprite? GetBossMapIcon()
        {
            try
            {
                var icons = MapMarkerManager.Icons;
                if (icons != null && icons.Count > 3) return icons[3];
                if (icons != null && icons.Count > 0) return icons[0];
            }
            catch { }
            return null;
        }

        private void ClearBossMarkers()
        {
            try
            {
                foreach (var e in _bossEntries.Values)
                    if (e.Go != null) Destroy(e.Go);
            }
            catch { }
            _bossEntries.Clear();
            _mapOpen = false;
            if (_bossListCanvas != null) _bossListCanvas.SetActive(false);
        }

        private void UpdateBossListHUD()
        {
            if (_bossListTMP == null) return;
            try
            {
                if (_bossEntries.Count == 0) { _bossListTMP.text = ""; return; }
                var sb = new StringBuilder();
                foreach (var e in _bossEntries.Values)
                {
                    string color = e.Alive ? "FF6060" : "888888";
                    string nameStr = e.Alive ? e.Name : $"<s>{e.Name}</s>";
                    sb.AppendLine($"<color=#{color}>{nameStr}</color>");
                }
                _bossListTMP.text = sb.ToString().TrimEnd();
            }
            catch { }
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
            var panels = FindObjectsOfType<OptionsPanel>();
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

            // Version header row - same style as section headers but centered
            {
                GameObject hdrWrapper;
                if (wrapperTemplate != null)
                {
                    hdrWrapper = Instantiate(wrapperTemplate, area);
                    hdrWrapper.name = "ModVersion";
                    for (int i = hdrWrapper.transform.childCount - 1; i >= 0; i--)
                        DestroyImmediate(hdrWrapper.transform.GetChild(i).gameObject);
                    var wVlg = hdrWrapper.GetComponent<VerticalLayoutGroup>();
                    if (wVlg != null) wVlg.childForceExpandWidth = true;
                }
                else
                {
                    hdrWrapper = new GameObject("ModVersion");
                    hdrWrapper.transform.SetParent(area, false);
                    var wVlg = hdrWrapper.AddComponent<VerticalLayoutGroup>();
                    wVlg.childForceExpandWidth = true;
                    wVlg.childForceExpandHeight = false;
                    hdrWrapper.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                }
                var labelGo = new GameObject("VersionLabel");
                labelGo.transform.SetParent(hdrWrapper.transform, false);
                var le = labelGo.AddComponent<LayoutElement>();
                le.preferredHeight = 30f;
                var tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.text = MOD_VERSION.ToUpper();
                tmp.fontSize = 12f;
                tmp.color = Color.white;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.MidlineGeoAligned;
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
                // idx: 0=Off, 1=Normal, 2=By item rarity
                AddCycle(L("Lootbox highlight"), new[] { L("Off"), L("Normal"), L("By item rarity") },
                    _lootboxHLMode, idx => { _lootboxHLMode = idx; PlayerPrefs.SetInt(PREF_LOOTBOX_HL, idx); PlayerPrefs.Save(); });
                AddToggle(L("Inventory count on hover"), _invCountEnabled, v => { _invCountEnabled = v; PlayerPrefs.SetInt(PREF_INV_COUNT, v ? 1 : 0); PlayerPrefs.Save(); if (!v && _invCountText != null) _invCountText.gameObject.SetActive(false); });
                AddToggle(L("Badge on recorded keys and Blueprints"), _showRecorderBadge, v => { _showRecorderBadge = v; PlayerPrefs.SetInt(PREF_RECORDER_BADGE, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Item rarity display"), _rarityDisplayEnabled, v => { _rarityDisplayEnabled = v; PlayerPrefs.SetInt(PREF_RARITY_DISPLAY, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Sound on item reveal"), _raritySoundEnabled, v => { _raritySoundEnabled = v; PlayerPrefs.SetInt(PREF_RARITY_SOUND, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Remove item name background"), _removeNameBg, v => { _removeNameBg = v; PlayerPrefs.SetInt(PREF_REMOVE_NAME_BG, v ? 1 : 0); PlayerPrefs.Save(); RefreshNameStyle(); });
                AddToggle(L("Sort by value (sort button)"), _valueSortEnabled, v =>
                {
                    _valueSortEnabled = v;
                    PlayerPrefs.SetInt(PREF_VALUE_SORT, v ? 1 : 0);
                    PlayerPrefs.Save();
                    _hookedSortButtons.Clear(); _sortOrigBtns.Clear(); _sortCustomButtons.Clear(); _sortStoreAllTmps.Clear();
                    _sortBtnScanTimer = 0f;
                });
                AddToggle(L("Black market base price"), _bmBasePriceEnabled, v => { _bmBasePriceEnabled = v; PlayerPrefs.SetInt(PREF_BM_BASE_PRICE, v ? 1 : 0); PlayerPrefs.Save(); });
            });

            // ── Combat ────────────────────────────────────────────────────────
            AddSection(L("Combat"), () =>
            {
                AddToggle(L("Show enemy name"), _showEnemyNames, v => { _showEnemyNames = v; PlayerPrefs.SetInt(PREF_ENEMY_NAMES, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Show hidden enemy health bars"), _showHiddenBars, v => { _showHiddenBars = v; PlayerPrefs.SetInt(PREF_SHOW_HIDDEN_BARS, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Auto-unload gun on kill"), _autoUnloadEnabled, v => { _autoUnloadEnabled = v; PlayerPrefs.SetInt(PREF_AUTO_UNLOAD, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Kill feed"), _killFeedEnabled, v => { _killFeedEnabled = v; PlayerPrefs.SetInt(PREF_KILL_FEED, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Skip melee on scroll"), _skipMeleeOnScroll, v => { _skipMeleeOnScroll = v; PlayerPrefs.SetInt(PREF_SKIP_MELEE, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Boss map markers"), _bossMarkersEnabled, v => { _bossMarkersEnabled = v; PlayerPrefs.SetInt(PREF_BOSS_MARKERS, v ? 1 : 0); PlayerPrefs.Save(); if (!v) ClearBossMarkers(); });
            });

            // ── Survival ──────────────────────────────────────────────────────
            AddSection(L("Survival"), () =>
            {
                AddToggle(L("Close on movement"), _autoCloseOnWASD, v => { _autoCloseOnWASD = v; PlayerPrefs.SetInt(PREF_AC_WASD, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Close on Shift"), _autoCloseOnShift, v => { _autoCloseOnShift = v; PlayerPrefs.SetInt(PREF_AC_SHIFT, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Close on Space"), _autoCloseOnSpace, v => { _autoCloseOnSpace = v; PlayerPrefs.SetInt(PREF_AC_SPACE, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Close on damage"), _autoCloseOnDamage, v => { _autoCloseOnDamage = v; PlayerPrefs.SetInt(PREF_AC_DAMAGE, v ? 1 : 0); PlayerPrefs.Save(); });
                AddToggle(L("Wake-up presets"), _sleepPresetsEnabled, v => { _sleepPresetsEnabled = v; PlayerPrefs.SetInt(PREF_SLEEP_ENABLED, v ? 1 : 0); PlayerPrefs.Save(); });
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
                AddToggle(L("FPS counter"), _showFps, v =>
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
                AddCycle(L("Camera view"), new[] { L("Off"), L("Default"), L("Top-down") },
                    _cameraViewMode, idx =>
                    {
                        _cameraViewMode = idx;
                        PlayerPrefs.SetInt(PREF_CAMERA_VIEW, idx);
                        PlayerPrefs.Save();
                        if (idx != 0)
                        {
                            bool wantTopDown = idx == 2;
                            if (CameraArmGetTopDown() != wantTopDown)
                                CameraArm.ToggleView();
                        }
                    });
            });

            // ── Quests ────────────────────────────────────────────────────────
            AddSection(L("Quests"), () =>
            {
                AddToggle(L("Quest favorites (N key)"), _questFavEnabled, v => { _questFavEnabled = v; PlayerPrefs.SetInt(PREF_QUEST_FAV, v ? 1 : 0); PlayerPrefs.Save(); });
            });

            // ── Developement ─────────────────────────────────────────────────────
            AddSection(L("Development"), () =>
            {
                AddToggle(L("Performance profiler"), _profilerEnabled, v =>
                {
                    _profilerEnabled = v; PlayerPrefs.SetInt(PREF_PROFILER, v ? 1 : 0); PlayerPrefs.Save();
                    if (v) EnsureProfilerCanvas();
                    if (_profCanvas != null) _profCanvas.SetActive(v);
                    if (!v) { System.Array.Clear(_profAccum, 0, _profAccum.Length); _profFrame = 0; }
                });
                AddToggle(L("Performance trace log"), _traceEnabled, v =>
                {
                    _traceEnabled = v; PlayerPrefs.SetInt(PREF_TRACE_LOG, v ? 1 : 0); PlayerPrefs.Save();
                    if (!v) FlushTraceLog();
                });
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

        // ── Backpack value sort ───────────────────────────────────────────────
        private static readonly string[] _sortSymbols = { "↕", "$", "#", "%" };

        private void TrySortButtonHook()
        {
            if (_invDisplaySortBtnField == null) return;
            foreach (var display in FindObjectsOfType<InventoryDisplay>())
            {
                int id = display.GetInstanceID();
                if (_hookedSortButtons.Contains(id))
                {
                    OnSortDisplayEnabled(id);
                    continue;
                }
                if (!display.ShowSortButton) continue;
                var origBtn = _invDisplaySortBtnField.GetValue(display) as Button;
                if (origBtn == null) continue;
                _hookedSortButtons.Add(id);
                _sortOrigBtns[id] = origBtn;

                if (!_valueSortEnabled)
                {
                    origBtn.onClick.RemoveAllListeners();
                    var cd = display;
                    origBtn.onClick.AddListener(() => _invDisplaySortNativeMethod?.Invoke(cd, null));
                    continue;
                }

                var origRT = origBtn.GetComponent<RectTransform>();
                var origLE = origBtn.GetComponent<LayoutElement>();
                var parent = origBtn.transform.parent;
                int sibIdx = origBtn.transform.GetSiblingIndex();

                var container = new GameObject("ValueSortButtons");
                container.transform.SetParent(parent, false);
                container.transform.SetSiblingIndex(sibIdx);

                var contRT = container.AddComponent<RectTransform>();
                contRT.anchorMin = origRT.anchorMin;
                contRT.anchorMax = origRT.anchorMax;
                contRT.pivot = origRT.pivot;
                contRT.anchoredPosition = origRT.anchoredPosition;
                contRT.sizeDelta = origRT.sizeDelta;

                var contLE = container.AddComponent<LayoutElement>();
                if (origLE != null)
                {
                    contLE.minWidth = origLE.minWidth;
                    contLE.minHeight = origLE.minHeight;
                    contLE.preferredWidth = origLE.preferredWidth;
                    contLE.preferredHeight = origLE.preferredHeight;
                    contLE.flexibleWidth = origLE.flexibleWidth;
                    contLE.flexibleHeight = origLE.flexibleHeight;
                }
                else
                {
                    contLE.preferredWidth = origRT.rect.width > 0 ? origRT.rect.width : 80f;
                    contLE.preferredHeight = origRT.rect.height > 0 ? origRT.rect.height : 30f;
                }

                var hlg = container.AddComponent<HorizontalLayoutGroup>();
                hlg.spacing = 1f;
                hlg.childForceExpandWidth = true;
                hlg.childForceExpandHeight = true;
                hlg.childAlignment = TextAnchor.MiddleCenter;
                hlg.padding = new RectOffset(0, 0, 0, 0);

                var buttons = new Button[4];
                for (int i = 0; i < 4; i++)
                {
                    int modeIdx = i;
                    var btnGo = Instantiate(origBtn.gameObject, container.transform);
                    btnGo.name = "SortBtn_" + i;
                    // Strip localization components so the game can't reset our text
                    foreach (var comp in btnGo.GetComponentsInChildren<MonoBehaviour>(true))
                    {
                        var n = comp.GetType().Name;
                        if (n.Contains("Locali") || n.Contains("i18n") || n.Contains("Translat") || n.Contains("Lang"))
                            Destroy(comp);
                    }
                    var btn = btnGo.GetComponent<Button>();
                    btn.onClick.RemoveAllListeners();
                    var capturedDisplay = display;
                    var capturedButtons = buttons;
                    btn.onClick.AddListener(() => OnModSortModeSet(capturedDisplay, modeIdx, capturedButtons));

                    var tmp = btnGo.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) { tmp.text = _sortSymbols[i]; tmp.fontSize = Mathf.Min(tmp.fontSize, 14f); }

                    var tooltipHelper = btnGo.AddComponent<SortTooltipHelper>();
                    tooltipHelper.Mod = this;
                    tooltipHelper.TooltipKey = _sortTooltipKeys[i];

                    var le = btnGo.GetComponent<LayoutElement>() ?? btnGo.AddComponent<LayoutElement>();
                    le.flexibleWidth = 1f;
                    le.flexibleHeight = 1f;
                    le.minWidth = -1f;
                    le.preferredWidth = -1f;

                    buttons[i] = btn;
                }

                // Store references for immediate re-apply on re-enable
                _sortCustomButtons[id] = buttons;

                // Shrink "Store All" button to a compact symbol in the same UI row
                var dispParent = display.transform.parent;
                if (dispParent != null)
                {
                    foreach (var btn in dispParent.GetComponentsInChildren<Button>(true))
                    {
                        if (btn.transform.IsChildOf(display.transform)) continue;
                        var staTmp = btn.GetComponentInChildren<TextMeshProUGUI>(true);
                        if (staTmp == null) continue;
                        if (string.Equals(staTmp.text.Trim(), "Store All", StringComparison.OrdinalIgnoreCase))
                        {
                            _sortStoreAllTmps[id] = staTmp;
                            break;
                        }
                    }
                }

                // Attach helper so re-enable fires our patch immediately (no flash)
                var helper = display.gameObject.AddComponent<SortHookHelper>();
                helper.Mod = this;
                helper.DisplayId = id;

                // Apply immediately for this first appearance
                OnSortDisplayEnabled(id);
            }
        }

        private void OnSortDisplayEnabled(int id)
        {
            if (_sortOrigBtns.TryGetValue(id, out var ob) && ob != null)
                ob.gameObject.SetActive(false);
            if (_sortCustomButtons.TryGetValue(id, out var btns) && btns != null)
            {
                for (int i = 0; i < btns.Length && i < _sortSymbols.Length; i++)
                {
                    if (btns[i] == null) continue;
                    var tmp = btns[i].GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = _sortSymbols[i];
                }
                RefreshSortButtonHighlights(btns, _valueSortMode);
            }
            if (_sortStoreAllTmps.TryGetValue(id, out var staTmp) && staTmp != null)
                staTmp.text = "↑";
        }

        private class SortHookHelper : MonoBehaviour
        {
            public ModBehaviour? Mod;
            public int DisplayId;

            void OnEnable()
            {
                // Subscribe to willRenderCanvases so our patch runs after all OnEnables
                // (InventoryDisplay.OnEnable may re-enable the sort button after us)
                // but before the frame is drawn - guaranteeing zero flash.
                Canvas.willRenderCanvases += ApplyPatch;
            }

            void OnDisable()
            {
                Canvas.willRenderCanvases -= ApplyPatch;
            }

            void ApplyPatch()
            {
                Canvas.willRenderCanvases -= ApplyPatch;
                Mod?.OnSortDisplayEnabled(DisplayId);
            }
        }

        private static readonly string[] _sortTooltipKeys =
        {
            "Native sort\nDefault game ordering",
            "Sort by value\nMost valuable items first",
            "Sort by stack total\nHighest stack price first",
            "Sort by value / kg\nBest price-to-weight ratio"
        };
        private static Type? _proceduralImageType;
        private static Type? _uniformModifierType;
        private static bool _piTypesSearched;

        private void EnsureSortTooltip()
        {
            if (_sortTooltipCanvas != null) return;
            _sortTooltipCanvas = new GameObject("SortTooltipCanvas");
            DontDestroyOnLoad(_sortTooltipCanvas);
            var c = _sortTooltipCanvas.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 32767;
            _sortTooltipCanvas.AddComponent<CanvasScaler>();

            _sortTooltipGo = new GameObject("SortTooltip");
            _sortTooltipGo.transform.SetParent(_sortTooltipCanvas.transform, false);

            AddRoundedBackground(_sortTooltipGo, new Color(0.08f, 0.08f, 0.08f, 0.95f), 8f);

            _sortTooltipRT = _sortTooltipGo.GetComponent<RectTransform>();
            _sortTooltipRT.sizeDelta = new Vector2(210f, 52f);
            _sortTooltipRT.pivot = new Vector2(0.5f, 0f);

            var textGo = new GameObject("Text");
            textGo.transform.SetParent(_sortTooltipGo.transform, false);
            _sortTooltipTmp = textGo.AddComponent<TextMeshProUGUI>();
            _sortTooltipTmp.fontSize = 12f;
            _sortTooltipTmp.lineSpacing = -10f;
            _sortTooltipTmp.alignment = TextAlignmentOptions.Center;
            _sortTooltipTmp.color = Color.white;
            var textRT = textGo.GetComponent<RectTransform>();
            textRT.anchorMin = Vector2.zero;
            textRT.anchorMax = Vector2.one;
            textRT.offsetMin = new Vector2(5f, 2f);
            textRT.offsetMax = new Vector2(-5f, -2f);

            _sortTooltipGo.SetActive(false);
        }

        private static void AddRoundedBackground(GameObject go, Color color, float radius)
        {
            if (!_piTypesSearched)
            {
                _piTypesSearched = true;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        foreach (var t in asm.GetTypes())
                        {
                            if (t.FullName == "UnityEngine.UI.ProceduralImage.ProceduralImage")
                                _proceduralImageType = t;
                            else if (t.Name == "UniformModifier")
                                _uniformModifierType = t;
                        }
                    }
                    catch { }
                }
            }

            if (_proceduralImageType != null && _uniformModifierType != null)
            {
                var pi = go.AddComponent(_proceduralImageType) as Graphic;
                if (pi != null) pi.color = color;
                var um = go.AddComponent(_uniformModifierType);
                _uniformModifierType.GetProperty("Radius")?.SetValue(um, radius);
            }
            else
            {
                go.AddComponent<Image>().color = color;
            }
        }

        internal void ShowSortTooltip(string text)
        {
            EnsureSortTooltip();
            if (_sortTooltipTmp != null) _sortTooltipTmp.text = text;
            if (_sortTooltipGo != null) _sortTooltipGo.SetActive(true);
        }

        internal void HideSortTooltip()
        {
            if (_sortTooltipGo != null) _sortTooltipGo.SetActive(false);
        }

        private class SortTooltipHelper : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
        {
            public ModBehaviour? Mod;
            public string TooltipKey = "";
            public void OnPointerEnter(PointerEventData _) => Mod?.ShowSortTooltip(L(TooltipKey));
            public void OnPointerExit(PointerEventData _) => Mod?.HideSortTooltip();
            void OnDisable() => Mod?.HideSortTooltip();
        }

        private void OnModSortModeSet(InventoryDisplay invDisplay, int mode, Button[] buttons)
        {
            if (!_valueSortEnabled) { _invDisplaySortNativeMethod?.Invoke(invDisplay, null); return; }
            _valueSortMode = mode;
            PlayerPrefs.SetInt(PREF_VALUE_SORT_MODE, _valueSortMode);
            PlayerPrefs.Save();

            var inv = _invDisplayTargetProp?.GetValue(invDisplay) as Inventory;
            if (inv != null)
            {
                switch (_valueSortMode)
                {
                    case 0: _invDisplaySortNativeMethod?.Invoke(invDisplay, null); break;
                    case 1: SortRespectingLocks(inv, (a, b) => b.Value.CompareTo(a.Value)); break;
                    case 2: SortRespectingLocks(inv, (a, b) => (b.Value * b.StackCount).CompareTo(a.Value * a.StackCount)); break;
                    case 3:
                        SortRespectingLocks(inv, (a, b) =>
                        {
                            float va = a.UnitSelfWeight > 0.001f ? a.Value / a.UnitSelfWeight : 0f;
                            float vb = b.UnitSelfWeight > 0.001f ? b.Value / b.UnitSelfWeight : 0f;
                            return vb.CompareTo(va);
                        });
                        break;
                }
            }
            RefreshSortButtonHighlights(buttons, _valueSortMode);
            string[] modeLabels = { L("Sort: Native"), L("Sort: Value"), L("Sort: Stack value"), L("Sort: Value per kg") };
            ShowSortToast(modeLabels[_valueSortMode]);
        }

        private static void RefreshSortButtonHighlights(Button[] buttons, int activeMode)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i] == null) continue;
                var img = buttons[i].GetComponent<Image>();
                if (img != null)
                    img.color = i == activeMode ? Color.white : new Color(1f, 1f, 1f, 0.35f);
            }
        }

        private static void SortRespectingLocks(Inventory inv, Comparison<Item> comparison)
        {
            int cap = inv.Capacity;
            var slots = new List<int>();
            var items = new List<Item>();
            for (int i = 0; i < cap; i++)
            {
                if (inv.IsIndexLocked(i)) continue;
                var item = inv.GetItemAt(i);
                if (item != null) { slots.Add(i); items.Add(item); }
            }
            if (items.Count == 0) return;
            items.Sort(comparison);
            foreach (int idx in slots)
                inv.RemoveAt(idx, out _);
            for (int i = 0; i < items.Count; i++)
                inv.AddAt(items[i], slots[i]);
        }

        private void ShowSortToast(string text)
        {
            if (_sortToastCanvas == null)
            {
                _sortToastCanvas = new GameObject("SortToast");
                DontDestroyOnLoad(_sortToastCanvas);
                var canvas = _sortToastCanvas.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 100;
                _sortToastCanvas.AddComponent<CanvasScaler>();
                var go = new GameObject("ToastText");
                go.transform.SetParent(_sortToastCanvas.transform, false);
                _sortToastTMP = go.AddComponent<TextMeshProUGUI>();
                _sortToastTMP.fontSize = 18f;
                _sortToastTMP.fontStyle = FontStyles.Bold;
                _sortToastTMP.alignment = TextAlignmentOptions.Center;
                var shadow = go.AddComponent<UnityEngine.UI.Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
                shadow.effectDistance = new Vector2(1f, -1f);
                var rt = go.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = new Vector2(0f, -100f);
                rt.sizeDelta = new Vector2(0f, 40f);
            }
            _sortToastTMP!.text = text;
            _sortToastTMP.color = Color.white;
            _sortToastCanvas.SetActive(true);
            _sortToastTimer = SORT_TOAST_DISPLAY;
        }

        // ── Item data dump (Ctrl+Shift+D) ────────────────────────────────────
        private void DumpAllItems()
        {
            var col = ItemAssetsCollection.Instance;
            if (col == null || col.entries == null)
            {
                Debug.LogWarning("[ItemDump] ItemAssetsCollection not available.");
                return;
            }

            var statType = typeof(ItemStatsSystem.StatCollection).Assembly
                .GetType("ItemStatsSystem.Stat");
            var statValueProp = statType?.GetProperty("Value", BindingFlags.Public | BindingFlags.Instance);
            var statKeyProp = statType?.GetProperty("Key", BindingFlags.Public | BindingFlags.Instance);

            var sb = new StringBuilder();
            sb.AppendLine("TypeID,Name,Value,SellValue,Weight,Quality,MaxStack,CanBeSold,MaxDurability,Tags,Stats");

            foreach (var entry in col.entries.OrderBy(e => e.typeID))
            {
                var item = entry?.prefab;
                if (item == null) continue;

                string tags = string.Join("|", item.Tags
                    .Where(t => t != null)
                    .Select(t => t.name));

                var statsCol = typeof(Item)
                    .GetField("stats", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.GetValue(item);

                string statsStr = "";
                if (statsCol is System.Collections.IEnumerable statsEnum)
                {
                    var parts = new List<string>();
                    foreach (var stat in statsEnum)
                    {
                        if (stat == null) continue;
                        var k = statKeyProp?.GetValue(stat) as string;
                        var v = statValueProp?.GetValue(stat);
                        if (!string.IsNullOrEmpty(k))
                        {
                            string vs = (v is float f) ? f.ToString(System.Globalization.CultureInfo.InvariantCulture) : $"{v}";
                            parts.Add($"{k}={vs}");
                        }
                    }
                    statsStr = string.Join("|", parts);
                }

                var inv = System.Globalization.CultureInfo.InvariantCulture;
                string name = (item.DisplayName ?? "").Replace("\"", "\"\"");
                sb.AppendLine(
                    $"{item.TypeID}," +
                    $"\"{name}\"," +
                    $"{item.Value}," +
                    $"{item.Value / 2}," +
                    $"{item.UnitSelfWeight.ToString(inv)}," +
                    $"{item.Quality}," +
                    $"{item.MaxStackCount}," +
                    $"{item.CanBeSold}," +
                    $"{item.MaxDurability.ToString(inv)}," +
                    $"\"{tags}\"," +
                    $"\"{statsStr}\""
                );
            }

            string path = @"C:\tmp\duckov_items.csv";
            File.WriteAllText(path, sb.ToString(), System.Text.Encoding.UTF8);
            Debug.Log($"[ItemDump] {col.entries.Count} items written to {path}");
        }

        // ── Black Market base price display ───────────────────────────────

        private void UpdateBMBasePriceInfo()
        {
            // Cache each type independently - one tab may be inactive when the other is first seen
            if (_cachedBMSupplyEntries == null || _cachedBMSupplyEntries.Length == 0)
                _cachedBMSupplyEntries = FindObjectsOfType<SupplyPanel_Entry>();
            if (_cachedBMDemandEntries == null || _cachedBMDemandEntries.Length == 0)
                _cachedBMDemandEntries = FindObjectsOfType<DemandPanel_Entry>();

            // BM not yet opened at all - back off
            if (_cachedBMSupplyEntries.Length == 0 && _cachedBMDemandEntries.Length == 0)
            {
                _bmScanTimer = 2f;
                return;
            }

            bool anyActive = false;
            foreach (var entry in _cachedBMSupplyEntries)
            {
                if (entry == null || !entry.gameObject.activeInHierarchy || entry.Target == null) continue;
                anyActive = true;
                ApplyBMAnnotation(_supplyEntryTitleField, entry, entry.Target, isSupply: true);
            }
            foreach (var entry in _cachedBMDemandEntries)
            {
                if (entry == null || !entry.gameObject.activeInHierarchy || entry.Target == null) continue;
                anyActive = true;
                ApplyBMAnnotation(_demandEntryTitleField, entry, entry.Target, isSupply: false);
            }
            // Back off when BM is closed
            _bmScanTimer = anyActive ? 0.5f : 2f;
        }

        private static void ApplyBMAnnotation(FieldInfo? titleField, MonoBehaviour entry,
            BlackMarket.DemandSupplyEntry target, bool isSupply)
        {
            if (titleField == null || _dseFactorField == null) return;
            var titleTmp = titleField.GetValue(entry) as TextMeshProUGUI;
            if (titleTmp == null) return;

            // Already annotated this cycle - skip
            if (titleTmp.text.Contains(BM_ANNOT_MARKER)) return;

            var factorVal = _dseFactorField.GetValue(target);
            if (factorVal == null) return;
            float priceFactor = (float)factorVal;
            if (priceFactor < 0.001f) return;

            float diffPct = (priceFactor - 1f) * 100f;

            int basePrice = Mathf.RoundToInt(target.TotalPrice / priceFactor);

            // Supply = you buy; cheaper is good for you. Demand = market buys from you; higher is good.
            bool benefitsPlayer = isSupply ? (diffPct < 0f) : (diffPct > 0f);
            bool atBase = Mathf.Abs(diffPct) < 0.5f;
            string color = atBase ? "#FFFFFF" : (benefitsPlayer ? "#44FF44" : "#FF4444");
            string sign = diffPct > 0f ? "+" : "";
            string pctText = atBase ? "~0%" : $"{sign}{diffPct:F0}%";
            string annotation = $"\n{BM_ANNOT_MARKER}<b><size=16><color={color}>Base {basePrice:N0} ({pctText})</color></size></b>";
            titleTmp.text += annotation;
        }
    }
}
