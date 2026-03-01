using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OriginalB.Platform.Core;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class ShelfSpawnManager : MonoBehaviour
{
    public enum RuntimeMode
    {
        GameMode,
        LevelDesignMode
    }

    [Header("Shelf Spawn")]
    [SerializeField] private GameObject shelfPrefab;
    [SerializeField] private Transform shelfRoot;
    [SerializeField] private bool autoSpawnShelves = true;
    [SerializeField] private int previewShelfCount = 8;
    [SerializeField, Range(0.02f, 0.3f)] private float viewportHorizontalPadding = 0.08f;
    [SerializeField, Range(0.02f, 0.3f)] private float viewportVerticalPadding = 0.08f;
    [SerializeField, Range(0f, 0.25f)] private float viewportInnerRadius = 0.05f;
    [SerializeField, Range(0.15f, 0.65f)] private float viewportOuterRadius = 0.42f;
    [SerializeField, Min(0f)] private float minShelfDistance = 0f;
    [SerializeField, Range(0.5f, 1.5f)] private float shelfVerticalSpacingScale = 0.8f;
    [SerializeField, Range(-1f, 2f)] private float shelfVerticalGapOffset = 0f;
    [SerializeField, Range(1f, 3f)] private float distanceScaleFromPrefab = 1f;
    [SerializeField] private float spawnPlaneZ = 0f;
    [SerializeField] private int spawnSeed = 20260218;
    [SerializeField] private bool randomizeOnEachRefresh = true;

    [Header("Box Spawn")]
    [SerializeField] private GameObject boxPrefab;
    [SerializeField] private BoxGenerationManager boxGenerationManager;
    [SerializeField, Min(1)] private int boxesPerShelf = 4;
    [SerializeField, Min(1)] private int configuredTotalShelfCount = 8;
    [SerializeField, Min(0)] private int configuredEmptyShelfCount = 0;
    [FormerlySerializedAs("configuredTotalBoxCount")]
    [SerializeField, Min(1)] private int configuredFilledShelfCount = 6;
    [SerializeField] private bool alignBoxRotationToShelf = true;
    [SerializeField] private bool useBoxSortingLayer = true;
    [SerializeField] private string boxSortingLayerName = "Default";
    [SerializeField] private int boxSortingOrderStart = 0;

    [Header("Level Authoring")]
    [SerializeField] private RuntimeMode runtimeMode = RuntimeMode.GameMode;
    [SerializeField] private string designedLevelId = "level_001";
    [SerializeField] private bool useDesignedLevelInGameMode = true;
    [SerializeField] private bool useDesignedLevelInDesignMode = true;
    [SerializeField, Min(1)] private int currentLevelIndex = 1;
    [SerializeField] private bool useNumericLevelId = true;
    [SerializeField] private string levelIdPrefix = "level_";
    [SerializeField, Min(1)] private int levelNumberPadding = 3;

    [Header("Game Link")]
    [SerializeField] private GameManager gameManager;

    [Header("Truck Elimination")]
    [SerializeField] private GameObject carPrefab;
    [SerializeField, Min(1)] private int maxTruckCount = 4;
    [SerializeField, Range(0.05f, 0.95f)] private float truckViewportY = 0.9f;
    [SerializeField, Range(0.02f, 0.3f)] private float truckHorizontalPadding = 0.08f;
    [SerializeField, Min(0.01f)] private float boxToTruckMoveDuration = 0.2f;
    [SerializeField, Min(0.01f)] private float truckShiftDuration = 0.18f;
    [SerializeField, Min(0.1f)] private float truckExitSpeed = 6f;

    [Header("Cat Win Flow")]
    [SerializeField] private GameObject catPrefab;
    [SerializeField] private bool catShrinkTowardTargetBox = true;
    [SerializeField, Min(0f)] private float catStayDuration = 0.3f;
    [SerializeField, Min(10f)] private float catMoveSpeed = 900f;
    [SerializeField, Min(0.01f)] private float catShrinkSpeed = 3.2f;
    [SerializeField, Range(0.01f, 1f)] private float catMinScaleFactor = 0.15f;
    [SerializeField] private string catHintMessage = "找到躲在箱子里的小猫";
    [SerializeField, Min(0f)] private float catHintDuration = 2f;
    [SerializeField] private string catWinMessage = "找到了小猫，游戏通关";
    [SerializeField] private string finalDesignedLevelWinMessage = "您已通过最后一关\n后续更多关卡敬请期待！";

    [Header("UI")]
    [SerializeField] private bool showRegenerateButton = true;
    [SerializeField] private string regenerateButtonText = "重新产生货架";
    [SerializeField] private Vector2 regenerateButtonSize = new Vector2(220f, 56f);
    [SerializeField] private Vector2 regenerateButtonPosition = new Vector2(220f, -24f);
    [SerializeField] private bool showLevelNavigationInGameMode = true;
    [SerializeField] private bool showLevelNavigationInDesignMode = true;
    [SerializeField] private string previousLevelButtonText = "上一关";
    [SerializeField] private string nextLevelButtonText = "下一关";
    [SerializeField] private Vector2 levelNavButtonSize = new Vector2(160f, 52f);
    [SerializeField] private Vector2 previousLevelButtonPosition = new Vector2(-200f, -48f);
    [SerializeField] private Vector2 nextLevelButtonPosition = new Vector2(200f, -48f);
    [SerializeField] private Vector2 levelIndicatorSize = new Vector2(260f, 52f);
    [SerializeField] private Vector2 levelIndicatorPosition = new Vector2(0f, -24f);
    [SerializeField, Min(10)] private int levelIndicatorFontSize = 30;
    [SerializeField] private bool centerLevelIndicator = true;
    [SerializeField] private bool showSaveLevelButtonInDesignMode = true;
    [SerializeField] private string saveLevelButtonText = "保存关卡";
    [SerializeField] private Vector2 saveLevelButtonSize = new Vector2(180f, 52f);
    [SerializeField] private Vector2 saveLevelButtonPosition = new Vector2(-120f, -24f);
    [SerializeField] private bool showDesignToolsPanel = true;
    [SerializeField, Min(1)] private int shelfColumnCount = 3;
    [SerializeField, Min(1)] private int currentColorTypeCount = 4;
    [SerializeField] private string refreshColorsButtonText = "刷新";
    [SerializeField] private Vector2 designPanelSize = new Vector2(420f, 190f);
    [SerializeField] private Vector2 designPanelPosition = new Vector2(0f, -96f);
    [SerializeField] private Vector2 columnControlAreaSize = new Vector2(0f, 220f);
    [SerializeField] private Vector2 columnControlAreaPosition = new Vector2(0f, 96f);
    [SerializeField, Range(0.005f, 0.2f)] private float columnVerticalMoveStep = 0.04f;
    [SerializeField, Range(0.02f, 0.45f)] private float maxColumnVerticalOffset = 0.22f;
    [SerializeField] private string enterDesignModeButtonText = "进入设计模式";
    [SerializeField] private string enterGameModeButtonText = "进入游戏模式";
    [SerializeField] private Vector2 modeToggleButtonSize = new Vector2(260f, 56f);
    [SerializeField] private Vector2 modeToggleButtonPosition = new Vector2(-120f, -96f);

    [Header("Gameplay Action Buttons")]
    [SerializeField] private bool showGameplayActionButtons = true;
    [SerializeField] private Button refreshActionButton;
    [SerializeField] private Button undoActionButton;
    [SerializeField] private Button addEmptyShelfActionButton;
    [SerializeField] private string refreshActionButtonText = "刷新";
    [SerializeField] private string undoActionButtonText = "回退";
    [SerializeField] private string addEmptyShelfActionButtonText = "新增空货架";
    [SerializeField] private Vector2 gameplayActionButtonSize = new Vector2(180f, 56f);
    [SerializeField] private Vector2 refreshActionButtonPosition = new Vector2(-220f, 24f);
    [SerializeField] private Vector2 undoActionButtonPosition = new Vector2(0f, 24f);
    [SerializeField] private Vector2 addEmptyShelfActionButtonPosition = new Vector2(220f, 24f);
    [SerializeField, Min(0.01f)] private float refreshShakeDuration = 0.45f;
    [SerializeField, Min(0f)] private float refreshShakeAmplitude = 0.12f;
    [SerializeField, Min(0.1f)] private float refreshShakeFrequency = 8f;

    [Header("Restart Settings UI")]
    [SerializeField] private bool showRestartSettingsButton = true;
    [SerializeField] private string restartSettingsButtonText = "设置";
    [SerializeField] private Vector2 restartSettingsButtonSize = new Vector2(132f, 52f);
    [SerializeField] private Vector2 restartSettingsButtonPosition = new Vector2(-80f, -24f);
    [SerializeField] private Color restartSettingsOverlayColor = new Color(0f, 0f, 0f, 0.58f);
    [SerializeField] private Vector2 restartSettingsPanelSize = new Vector2(420f, 240f);
    [SerializeField] private Vector2 restartSettingsPanelPosition = Vector2.zero;
    [SerializeField] private Color restartSettingsPanelColor = new Color(0.08f, 0.12f, 0.18f, 0.96f);
    [SerializeField] private string restartConfirmButtonText = "重新开始";
    [SerializeField] private Vector2 restartConfirmButtonSize = new Vector2(240f, 64f);
    [SerializeField] private Vector2 restartConfirmButtonPosition = new Vector2(0f, -80f);
    [SerializeField, Range(0f, 1f)] private float restartColorChangeRatioMin = 0.2f;
    [SerializeField, Range(0f, 1f)] private float restartColorChangeRatioMax = 0.45f;

    [Header("Test Tools")]
    [SerializeField] private bool showTestClearSaveButton = true;
    [SerializeField] private string testClearSaveButtonText = "测试清档";
    [SerializeField] private Vector2 testClearSaveButtonSize = new Vector2(180f, 52f);
    [SerializeField] private Vector2 testClearSaveButtonPosition = new Vector2(-120f, 24f);

    [Header("Economy")]
    [SerializeField] private bool showEconomyHud = true;
    [SerializeField] private string coinIconText = "🪙";
    [SerializeField] private string staminaIconText = "⚡";
    [SerializeField] private Vector2 coinHudPosition = new Vector2(-280f, -24f);
    [SerializeField] private Vector2 staminaHudPosition = new Vector2(-96f, -24f);
    [SerializeField] private Vector2 economyHudItemSize = new Vector2(170f, 52f);
    [SerializeField] private Vector2 staminaHudItemSize = new Vector2(320f, 52f);
    [SerializeField, Min(12)] private int economyHudFontSize = 28;
    [SerializeField, Min(0)] private int initialCoinCount = 0;
    [SerializeField, Min(0)] private int initialStaminaCount = 5;
    [SerializeField, Min(1)] private int staminaRecoveryMax = 5;
    [SerializeField, Min(1)] private int staminaRecoveryIntervalMinutes = 10;
    [FormerlySerializedAs("staminaCostOnFirstMove")]
    [SerializeField, Min(0)] private int staminaCostOnRestart = 1;
    [SerializeField] private string staminaInsufficientBubbleText = "爱心不足，无法重新开始";
    [SerializeField] private Vector2 staminaInsufficientBubbleSize = new Vector2(620f, 72f);
    [SerializeField] private Vector2 staminaInsufficientBubblePosition = new Vector2(0f, 120f);
    [SerializeField, Min(0.1f)] private float staminaInsufficientBubbleDuration = 1.8f;
    [SerializeField, Min(0.1f)] private float colorCountWarningBubbleDuration = 2.2f;
    [SerializeField, Min(0)] private int winRewardCoinCount = 10;
    [SerializeField] private bool persistEconomyData = true;

    [Header("Win Result Panel")]
    [SerializeField] private Vector2 winResultPanelSize = new Vector2(760f, 460f);
    [SerializeField] private Vector2 winResultPanelPosition = Vector2.zero;
    [SerializeField] private Color winResultPanelBackgroundColor = new Color(0.40f, 0.27f, 0.12f, 1f);
    [SerializeField] private Vector2 winResultTitleSize = new Vector2(680f, 96f);
    [SerializeField] private Vector2 winResultTitlePosition = new Vector2(0f, -120f);
    [SerializeField, Min(12)] private int winResultTitleFontSize = 52;
    [SerializeField] private Vector2 winResultRewardSize = new Vector2(680f, 82f);
    [SerializeField] private Vector2 winResultRewardPosition = new Vector2(0f, -220f);
    [SerializeField, Min(12)] private int winResultRewardFontSize = 42;
    [SerializeField] private string winRewardTextPrefix = "获得金币";
    [SerializeField] private Vector2 winResultNextLevelButtonSize = new Vector2(280f, 68f);
    [SerializeField] private Vector2 winResultNextLevelButtonPosition = new Vector2(0f, -330f);
    [SerializeField] private string winResultNextLevelButtonText = "下一关";

    [Header("Add Shelf Placement")]
    [SerializeField] private Transform additionalShelfPlacementArea;
    [SerializeField] private Vector2 additionalShelfPlacementAreaSize = new Vector2(10f, 8f);
    [SerializeField, Min(0f)] private float addShelfMinGap = 0.12f;
    [SerializeField] private bool avoidUiButtonsWhenAddShelf = true;
    [SerializeField] private bool allowAddShelfOutsideAreaWhenFull = true;
    [SerializeField] private bool enableAddShelfPlacementReport = true;

    [Header("Legacy Migration")]
    [SerializeField] private bool migrateSceneShelvesOnRefresh = true;
    [SerializeField] private bool disableLegacyShelvesAfterMigration = true;
    [SerializeField] private bool destroyLegacyShelvesAfterMigration;
    [SerializeField] private string legacyShelfNameKeyword = "shelf";

    [Header("Debug Log")]
    [SerializeField] private bool enableDebugLog = true;
    [SerializeField] private bool enableDebugFileLog = true;
    [SerializeField] private bool enableEliminationSnapshotLog = true;
    [SerializeField, Min(0f)] private float eliminationSnapshotLogCooldown = 0.1f;

    private readonly List<GameObject> spawnedShelves = new List<GameObject>();
    private IPlatformContext platformContext;
    private Transform runtimeShelfRoot;
    private Transform runtimeBoxRoot;
    private Transform runtimeTruckRoot;
    private Transform legacyShelfRoot;
    private int refreshSequence;
    private Button regenerateButton;
    private Button previousLevelButton;
    private Button nextLevelButton;
    private Button saveLevelButton;
    private Text levelIndicatorTextRef;
    private Text coinHudTextRef;
    private Text staminaHudTextRef;
    private RectTransform designToolsPanelRoot;
    private Text shelfColumnCountTextRef;
    private Text colorTypeCountTextRef;
    private Button refreshColorsButton;
    private Button modeToggleButton;
    private Button restartSettingsButton;
    private RectTransform restartSettingsOverlayRoot;
    private RectTransform restartSettingsPanelRoot;
    private Button restartFromSettingsButton;
    private RectTransform staminaInsufficientBubbleRoot;
    private Text staminaInsufficientBubbleTextRef;
    private Coroutine staminaInsufficientBubbleRoutine;
    private Button testClearSaveButton;
    private RectTransform columnControlAreaRoot;
    private readonly List<int> columnShelfCounts = new List<int>();
    private readonly List<float> columnVerticalOffsets = new List<float>();
    private bool skipDesignedLayoutOnce;
    private const string LogTag = "ShelfSpawn";
    private bool printedBoxGenerationManagerMissingWarning;
    private const int ColorGroupSize = 4;
    private readonly List<TruckRuntimeData> activeTrucks = new List<TruckRuntimeData>();
    private bool truckEliminationRunning;
    private bool gameWon;
    private bool isFinalDesignedLevelWin;
    private Coroutine catIntroRoutine;
    private Coroutine catHintRoutine;
    private Coroutine refreshShakeRoutine;
    private GameObject runtimeCatIntro;
    private GameObject runtimeCatIntroUi;
    private Text catHintTextRef;
    private Text catWinTextRef;
    private Image dimOverlayImage;
    private RectTransform winResultPanelRoot;
    private Text winResultTitleTextRef;
    private Text winResultRewardTextRef;
    private Button winResultNextLevelButtonRef;
    private RectTransform shelfConfigOverlayRoot;
    private RectTransform shelfConfigContentRoot;
    private Text shelfConfigTitleText;
    private Text shelfConfigBoxCountText;
    private Text shelfConfigGrayCountText;
    private Text shelfConfigColorConfigText;
    private readonly List<Button> shelfConfigColorButtons = new List<Button>();
    private ShelfInteractionController editingShelfConfig;
    private int editingShelfBoxCount;
    private int editingShelfGrayCount;
    private bool waitingRefreshShelfSelection;
    private bool hidePrimaryControlButtons;
    private bool staminaConsumedForCurrentRound;
    private bool applyRestartColorVariationOnNextRefresh;
    private int currentCoinCount;
    private int currentStaminaCount;
    private long staminaRecoveryStartUtcTicks = -1L;
    private float nextStaminaRecoveryUiRefreshTime;
    private int addShelfAttemptSequence;
    private float nextEliminationSnapshotLogTime;
    private const string DesignedLevelFolderName = "DesignedLevels";
    private const string DesignedLevelsFileName = "designed_levels.json";
    private const string EconomyCoinPrefKey = "ShelfSpawn.Economy.Coin";
    private const string EconomyStaminaPrefKey = "ShelfSpawn.Economy.Stamina";
    private const string EconomyStaminaRecoveryStartTicksPrefKey = "ShelfSpawn.Economy.StaminaRecoveryStartUtcTicks";
    private readonly Stack<VisualMoveRecord> visualMoveHistory = new Stack<VisualMoveRecord>();
    private static readonly Vector2 LegacyModeToggleButtonPosition = new Vector2(0f, 32f);
    private static readonly Vector2 DefaultTopRightModeToggleButtonPosition = new Vector2(-120f, -96f);

    private class TruckRuntimeData
    {
        public GameObject truck;
        public Transform bottomAnchor;
        public GameManager.BoxColor color;
        public bool busy;
    }

    private class VisualMoveRecord
    {
        public int fromShelfIndex;
        public int toShelfIndex;
        public Transform movedBox;
    }

    private class AddShelfPlacementReport
    {
        public int attempt;
        public bool areaConfigured;
        public bool areaResolved;
        public bool areaIntersectionApplied;
        public bool areaIntersectionInvalid;
        public bool areaResolveFailed;
        public bool usedScreenFallback;
        public bool fallbackUsed;
        public bool fallbackOverlaps;
        public int existingShelfCount;
        public int candidateCount;
        public int rejectedByShelfOverlap;
        public int rejectedByButtons;
        public int acceptedCount;
        public int bestUpdatedCount;
        public Vector2 halfExtents;
        public Rect screenRect;
        public Rect areaRect;
        public Rect placementRect;
        public float bestMinDistance;
        public float requiredSpacingX;
        public float requiredSpacingY;
        public int estimatedGridCapacity;
        public string selectionPath;
    }

    [Serializable]
    private class DesignedLevelData
    {
        public int version = 1;
        public int levelIndex;
        public string levelId;
        public int shelfColumnCount = 1;
        public List<int> columnShelfCounts = new List<int>();
        public List<float> columnVerticalOffsets = new List<float>();
        public List<DesignedShelfData> shelves = new List<DesignedShelfData>();
    }

    [Serializable]
    private class DesignedLevelCollectionData
    {
        public int version = 1;
        public List<DesignedLevelData> levels = new List<DesignedLevelData>();
    }

    [Serializable]
    private class DesignedShelfData
    {
        public Vector3 position;
        public Quaternion rotation;
        public List<DesignedBoxData> boxes = new List<DesignedBoxData>();
    }

    [Serializable]
    private class DesignedBoxData
    {
        public GameManager.BoxColor color;
        public bool grayed;
    }

    private enum CatUiType
    {
        Hint,
        Win
    }

    public bool AutoSpawnShelves => autoSpawnShelves;
    public RuntimeMode CurrentRuntimeMode => runtimeMode;
    public bool IsLevelDesignMode => runtimeMode == RuntimeMode.LevelDesignMode;
    public int CurrentLevelIndex => Mathf.Max(1, currentLevelIndex);

    public void SetPrimaryControlButtonsHidden(bool hidden)
    {
        hidePrimaryControlButtons = hidden;
        ApplyPrimaryControlButtonsVisibility();
    }

    public void SetBoxGenerationManager(BoxGenerationManager manager)
    {
        boxGenerationManager = manager;
        printedBoxGenerationManagerMissingWarning = false;
    }

    private void Awake()
    {
        if (gameManager == null)
        {
            gameManager = FindObjectOfType<GameManager>(true);
        }

        if (!ServiceLocator.TryResolve<IPlatformContext>(out platformContext))
        {
            platformContext = new CommonPlatformContext();
        }

        ConfigureLogger();
        LogInfo("Awake start");
        if (IsLevelDesignMode)
        {
            currentLevelIndex = ResolveNextDesignLevelIndex();
        }

        NormalizeConfiguredCounts();
        EnsureShelfRoot();
        EnsureShelfSubRoots();
        TryAutoBindShelfPrefab();
        TryAutoBindBoxPrefab();
        TryAutoBindCarPrefab();
        TryAutoBindCatPrefab();
        EnsureRegenerateButton();
        EnsureLevelNavigationUI();
        EnsureSaveLevelButton();
        EnsureModeToggleButton();
        EnsureEconomyHud();
        EnsureWinResultPanel();
        EnsureGameplayActionButtons();
        EnsureRestartSettingsButton();
        EnsureTestClearSaveButton();
        EnsureDesignToolsPanel();
        EnsureColumnControlArea();
        UpdateLevelIndicator();
        InitializeEconomyState();
        UpdateEconomyHud();
        UpdateDesignToolsTexts();
        ApplyDesignToolsVisibility();
        UpdateModeToggleButtonLabel();
        ApplyPrimaryControlButtonsVisibility();
        LogInfo($"Awake end | shelfPrefab={(shelfPrefab != null ? shelfPrefab.name : "NULL")} | boxPrefab={(boxPrefab != null ? boxPrefab.name : "NULL")} | carPrefab={(carPrefab != null ? carPrefab.name : "NULL")} | catPrefab={(catPrefab != null ? catPrefab.name : "NULL")}");
    }

    private void Update()
    {
        HandleStaminaRecoveryTick();
        HandleCatOverlayDismissInput();
        HandleRefreshShelfSelectionInput();
        HandleDesignShelfConfigInput();
    }

    private void HandleCatOverlayDismissInput()
    {
        if (!IsCatOverlayInteractiveState())
        {
            return;
        }

        if (!IsAnyPointerDown())
        {
            return;
        }

        DismissCatOverlayFlow();
    }

    private bool IsCatOverlayInteractiveState()
    {
        if (dimOverlayImage == null || !dimOverlayImage.gameObject.activeInHierarchy)
        {
            return false;
        }

        if (runtimeCatIntroUi != null || catIntroRoutine != null || catHintRoutine != null)
        {
            return true;
        }

        return catHintTextRef != null && catHintTextRef.gameObject.activeInHierarchy;
    }

    private void DismissCatOverlayFlow()
    {
        HideDimOverlay();

        if (catIntroRoutine != null)
        {
            StopCoroutine(catIntroRoutine);
            catIntroRoutine = null;
        }

        if (catHintRoutine != null)
        {
            StopCoroutine(catHintRoutine);
            catHintRoutine = null;
        }

        StopCatIntroVisual();
        HideHintMessage();
    }

    public void RefreshShelves(int shelfCount)
    {
        ConfigureLogger();
        NormalizeConfiguredCounts();
        LogInfo($"RefreshShelves begin | inputCount={shelfCount} | autoSpawn={autoSpawnShelves}");
        EnsureRegenerateButton();
        EnsureLevelNavigationUI();
        EnsureSaveLevelButton();
        EnsureModeToggleButton();
        EnsureEconomyHud();
        EnsureWinResultPanel();
        EnsureGameplayActionButtons();
        EnsureRestartSettingsButton();
        EnsureTestClearSaveButton();
        EnsureDesignToolsPanel();
        EnsureColumnControlArea();
        UpdateLevelIndicator();
        UpdateEconomyHud();
        UpdateDesignToolsTexts();
        ApplyDesignToolsVisibility();
        UpdateModeToggleButtonLabel();
        ApplyPrimaryControlButtonsVisibility();
        EnsureShelfRoot();
        EnsureShelfSubRoots();
        TryAutoBindShelfPrefab();
        TryAutoBindBoxPrefab();
        TryAutoBindCarPrefab();
        TryAutoBindCatPrefab();

        gameWon = false;
        if (Mathf.Abs(Time.timeScale) < 0.0001f)
        {
            Time.timeScale = 1f;
        }

        HideWinMessage();
        HideHintMessage();
        StopCatIntroVisual();
        HideWinResultPanel();
        HideRestartSettingsOverlay();
        CloseShelfConfigOverlay();
        waitingRefreshShelfSelection = false;
        staminaConsumedForCurrentRound = false;
        visualMoveHistory.Clear();

        if (migrateSceneShelvesOnRefresh)
        {
            MigrateLegacyShelves();
        }

        ClearSpawnedShelves();

        if (shelfPrefab == null)
        {
            LogWarn("Refresh aborted: shelfPrefab is NULL. 请在ShelfSpawnManager里配置shelfPrefab。", this);
            return;
        }

        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            LogWarn("Refresh aborted: Camera.main is NULL。", this);
            return;
        }

        var targetCount = shelfCount > 0 ? shelfCount : Mathf.Max(1, configuredTotalShelfCount);
        if (targetCount <= 0)
        {
            LogWarn("Refresh aborted: target shelf count <= 0。");
            return;
        }

        var shouldUseDesignedLayout = ShouldUseDesignedLayoutForCurrentMode() && !skipDesignedLayoutOnce;
        skipDesignedLayoutOnce = false;
        if (shouldUseDesignedLayout && TryLoadDesignedLevel(out var designedLevelData) && designedLevelData != null && designedLevelData.shelves != null && designedLevelData.shelves.Count > 0)
        {
            SpawnDesignedLayout(designedLevelData);
            FinalizeAfterShelvesSpawned();
            LogInfo($"RefreshShelves end | mode={runtimeMode} | source=designed | spawnedShelves={spawnedShelves.Count}");
            return;
        }

        if (shouldUseDesignedLayout && runtimeMode == RuntimeMode.GameMode)
        {
            LogWarn($"Current level designed layout not found, fallback to random generation | levelIndex={Mathf.Max(1, currentLevelIndex)} | levelId={GetCurrentLevelId()}", this);
        }

        var derivedTotalBoxCount = ResolveConfiguredTotalBoxCount(targetCount);
        LogInfo($"Refresh config | targetCount={targetCount} | boxesPerShelf={boxesPerShelf} | fillShelfCount={configuredFilledShelfCount} | totalBoxes={derivedTotalBoxCount} | sortingLayer={boxSortingLayerName} | sortingStart={boxSortingOrderStart}");

        List<List<GameManager.BoxColor>> colorLayout = null;
        List<int> shelfBoxCounts = null;
        List<int> grayCountsPerShelf = null;
        if (boxGenerationManager != null)
        {
            var colorSeed = randomizeOnEachRefresh
                ? unchecked((int)DateTime.UtcNow.Ticks) ^ spawnSeed ^ (targetCount * 127) ^ (boxesPerShelf * 911)
                : spawnSeed ^ (targetCount * 127) ^ (boxesPerShelf * 911);
            var colorRandom = new System.Random(colorSeed);
            if (boxGenerationManager.TryGenerateColorLayoutByCounts(
                targetCount,
                Mathf.Clamp(configuredEmptyShelfCount, 0, Mathf.Max(0, targetCount - 1)),
                derivedTotalBoxCount,
                Mathf.Max(1, boxesPerShelf),
                colorRandom,
                out colorLayout,
                out shelfBoxCounts,
                out var error))
            {
                LogInfo($"Generate constrained color layout success | shelves={targetCount}");
            }
            else
            {
                LogWarn($"Generate constrained color layout failed: {error}");
            }

            if (shelfBoxCounts != null && boxGenerationManager.TryGenerateGrayCountsPerShelf(shelfBoxCounts, out grayCountsPerShelf, out var grayTarget, out var grayMessage))
            {
                if (!string.IsNullOrWhiteSpace(grayMessage))
                {
                    LogInfo($"Gray allocation adjusted: {grayMessage}");
                }

                var grayDistribution = grayCountsPerShelf != null ? string.Join(",", grayCountsPerShelf) : "none";
                LogInfo($"Gray allocation success | percentage={boxGenerationManager.GrayPercentage:F2} | targetGray={grayTarget} | perShelf=[{grayDistribution}]");
            }
            else
            {
                LogWarn("Gray allocation failed, fallback to no-gray mode.");
            }
        }
        else if (!printedBoxGenerationManagerMissingWarning)
        {
            printedBoxGenerationManagerMissingWarning = true;
            LogWarn("BoxGenerationManager 未绑定，颜色分组与置灰配置将使用默认后备逻辑。请手动在 Inspector 绑定。", this);
        }
        else
        {
            shelfBoxCounts = BuildFallbackShelfBoxCounts(targetCount);
        }

        if (shelfBoxCounts == null || shelfBoxCounts.Count != targetCount)
        {
            shelfBoxCounts = BuildFallbackShelfBoxCounts(targetCount);
        }

        EnsureShelfBoxCountsFollowColorGroupRule(shelfBoxCounts);

        var groupedColorLayout = BuildGroupedColorLayoutByCounts(
            shelfBoxCounts,
            spawnSeed ^ targetCount * 173 ^ Mathf.Max(1, currentColorTypeCount) * 997 ^ unchecked((int)DateTime.UtcNow.Ticks));

        var spawnPositions = GenerateShelfPositions(cameraRef, targetCount);
        LogInfo($"Generated shelf positions: {spawnPositions.Count}");
        for (var i = 0; i < spawnPositions.Count; i++)
        {
            var shelf = Instantiate(shelfPrefab, spawnPositions[i], shelfPrefab.transform.rotation, runtimeShelfRoot);
            shelf.name = $"Shelf_{i + 1}";
            spawnedShelves.Add(shelf);
            LogInfo($"Shelf spawned: {shelf.name} at {shelf.transform.position}", shelf);

            IReadOnlyList<GameManager.BoxColor> shelfColors = groupedColorLayout != null && i < groupedColorLayout.Count
                ? groupedColorLayout[i]
                : (colorLayout != null && i < colorLayout.Count ? colorLayout[i] : null);
            shelfColors = NormalizeShelfColorsByPool(shelfColors);
            var shelfGrayCount = grayCountsPerShelf != null && i < grayCountsPerShelf.Count ? grayCountsPerShelf[i] : 0;
            var shelfBoxCount = shelfBoxCounts != null && i < shelfBoxCounts.Count ? Mathf.Max(0, shelfBoxCounts[i]) : Mathf.Max(1, boxesPerShelf);
            SpawnBoxesForShelf(shelf, shelfColors, shelfGrayCount, shelfBoxCount);
            EnsureShelfInteractionForRuntime(shelf, i);
        }

        FinalizeAfterShelvesSpawned();

        LogInfo($"RefreshShelves end | spawnedShelves={spawnedShelves.Count}");
    }

    public void HandleBoxMoved(ShelfInteractionController sourceShelf, ShelfInteractionController targetShelf)
    {
        if (!isActiveAndEnabled || gameWon || IsLevelDesignMode)
        {
            return;
        }

        if (sourceShelf != null
            && targetShelf != null
            && targetShelf.StackRoot != null
            && targetShelf.StackRoot.childCount > 0)
        {
            var moved = targetShelf.StackRoot.GetChild(targetShelf.StackRoot.childCount - 1);
            if (moved != null)
            {
                visualMoveHistory.Push(new VisualMoveRecord
                {
                    fromShelfIndex = sourceShelf.ShelfIndex,
                    toShelfIndex = targetShelf.ShelfIndex,
                    movedBox = moved
                });
            }
        }

        if (!truckEliminationRunning)
        {
            StartCoroutine(ProcessTruckEliminationsRoutine());
        }
    }

    public void RegenerateShelves()
    {
        var fallbackCount = Mathf.Max(1, configuredTotalShelfCount);
        var targetCount = spawnedShelves.Count > 0 ? spawnedShelves.Count : fallbackCount;
        RefreshShelves(targetCount);
    }

    public void SetRuntimeMode(RuntimeMode mode, bool refreshImmediately = true)
    {
        runtimeMode = mode;
        if (!IsLevelDesignMode)
        {
            CloseShelfConfigOverlay();
        }

        if (IsLevelDesignMode)
        {
            currentLevelIndex = ResolveNextDesignLevelIndex();
        }

        if (refreshImmediately)
        {
            RegenerateShelves();
        }

        ApplyLevelNavigationVisibility();
        ApplySaveLevelButtonVisibility();
        ApplyDesignToolsVisibility();
        ApplyRestartSettingsButtonVisibility();
        UpdateModeToggleButtonLabel();
        UpdateLevelIndicator();
        UpdateDesignToolsTexts();
    }

    [ContextMenu("LevelDesign/Save Current Layout")]
    public void SaveCurrentLayoutAsDesignedLevel()
    {
        var data = BuildDesignedLevelDataFromRuntime();
        if (data == null || data.shelves == null || data.shelves.Count == 0)
        {
            LogWarn("Save designed level skipped: runtime shelves is empty.", this);
            return;
        }

        SaveDesignedLevelToFile(data);
        LogInfo($"Designed level saved(file) | id={data.levelId} | shelfCount={data.shelves.Count}");
    }

    [ContextMenu("LevelDesign/Load Saved Layout")]
    public void LoadDesignedLevelAsCurrentScene()
    {
        if (TryLoadDesignedLevel(out var data) && data != null)
        {
            SpawnDesignedLayout(data);
            FinalizeAfterShelvesSpawned();
            LogInfo($"Designed level loaded to scene | id={data.levelId} | shelfCount={data.shelves.Count}");
            return;
        }

        LogWarn("Load designed level failed: no valid saved level data.", this);
    }

    [ContextMenu("LevelDesign/Delete Saved Layout")]
    public void DeleteDesignedLevelData()
    {
        if (!TryLoadDesignedLevelCollection(out var collection) || collection == null || collection.levels == null)
        {
            return;
        }

        var targetIndex = Mathf.Max(1, currentLevelIndex);
        collection.levels = collection.levels
            .Where(level => level != null && ResolveDesignedLevelIndex(level) != targetIndex)
            .ToList();

        var folderPath = GetDesignedLevelFolderPath();
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        File.WriteAllText(GetDesignedLevelsFilePath(), JsonUtility.ToJson(collection, true));

        LogInfo($"Designed level deleted | id={GetCurrentLevelId()}");
    }

    public void GoToNextLevelByButton()
    {
        if (runtimeMode == RuntimeMode.GameMode && isFinalDesignedLevelWin)
        {
            LogInfo($"Next level blocked: already passed last designed level | level={Mathf.Max(1, currentLevelIndex)}");
            return;
        }

        currentLevelIndex = Mathf.Max(1, currentLevelIndex + 1);
        PrepareForLevelSwitch();
        UpdateLevelIndicator();
        RegenerateShelves();
    }

    public void GoToPreviousLevelByButton()
    {
        currentLevelIndex = Mathf.Max(1, currentLevelIndex - 1);
        PrepareForLevelSwitch();
        UpdateLevelIndicator();
        RegenerateShelves();
    }

    public void IncreaseShelfColumnCount()
    {
        shelfColumnCount = Mathf.Max(1, shelfColumnCount + 1);
        RebuildColumnConfigsEvenly(ResolveRuntimeShelfCountForLayout());
        UpdateDesignToolsTexts();
        ReflowCurrentShelvesByColumns();
    }

    public void DecreaseShelfColumnCount()
    {
        shelfColumnCount = Mathf.Max(1, shelfColumnCount - 1);
        RebuildColumnConfigsEvenly(ResolveRuntimeShelfCountForLayout());
        UpdateDesignToolsTexts();
        ReflowCurrentShelvesByColumns();
    }

    public void IncreaseCurrentColumnShelfCount(int columnIndex)
    {
        if (!TryValidateColumnIndex(columnIndex))
        {
            return;
        }

        columnShelfCounts[columnIndex] = Mathf.Max(0, columnShelfCounts[columnIndex]) + 1;
        RefreshShelvesByCurrentColumnConfig();
    }

    public void DecreaseCurrentColumnShelfCount(int columnIndex)
    {
        if (!TryValidateColumnIndex(columnIndex))
        {
            return;
        }

        columnShelfCounts[columnIndex] = Mathf.Max(0, columnShelfCounts[columnIndex] - 1);
        var total = GetTotalShelvesFromColumns();
        if (total <= 0)
        {
            columnShelfCounts[columnIndex] = 1;
        }

        RefreshShelvesByCurrentColumnConfig();
    }

    public void MoveCurrentColumnUp(int columnIndex)
    {
        if (!TryValidateColumnIndex(columnIndex))
        {
            return;
        }

        columnVerticalOffsets[columnIndex] = Mathf.Clamp(columnVerticalOffsets[columnIndex] + Mathf.Max(0.005f, columnVerticalMoveStep), -maxColumnVerticalOffset, maxColumnVerticalOffset);
        UpdateDesignToolsTexts();
        ReflowCurrentShelvesByColumns();
    }

    public void MoveCurrentColumnDown(int columnIndex)
    {
        if (!TryValidateColumnIndex(columnIndex))
        {
            return;
        }

        columnVerticalOffsets[columnIndex] = Mathf.Clamp(columnVerticalOffsets[columnIndex] - Mathf.Max(0.005f, columnVerticalMoveStep), -maxColumnVerticalOffset, maxColumnVerticalOffset);
        UpdateDesignToolsTexts();
        ReflowCurrentShelvesByColumns();
    }

    public void IncreaseColorTypeCount()
    {
        currentColorTypeCount = Mathf.Max(1, currentColorTypeCount + 1);
        UpdateDesignToolsTexts();
    }

    public void DecreaseColorTypeCount()
    {
        currentColorTypeCount = Mathf.Max(1, currentColorTypeCount - 1);
        UpdateDesignToolsTexts();
    }

    public void RefreshCurrentBoxesColors()
    {
        RefreshColorsWithGroupConstraint();
    }

    private void RefreshColorsWithGroupConstraint()
    {
        var colorPool = ResolveLimitedColorPool();
        if (colorPool.Count <= 0)
        {
            return;
        }

        var rng = new System.Random(unchecked((int)DateTime.UtcNow.Ticks) ^ spawnSeed ^ Mathf.Max(1, currentColorTypeCount));
        var boxEntries = new List<(Transform box, ShelfInteractionController shelf, int stackIndex, bool keepGray)>();

        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                continue;
            }

            for (var b = 0; b < shelf.StackRoot.childCount; b++)
            {
                var box = shelf.StackRoot.GetChild(b);
                if (box == null)
                {
                    continue;
                }

                var state = box.GetComponent<BoxVisualState>();
                var keepGray = state != null && state.IsGrayed;
                boxEntries.Add((box, shelf, b, keepGray));
            }
        }

        if (boxEntries.Count <= 0)
        {
            return;
        }

        if (boxEntries.Count % ColorGroupSize != 0)
        {
            ShowBottomBubble($"当前箱子总数为{boxEntries.Count}，不是4的倍数，无法按4连组分配颜色", colorCountWarningBubbleDuration);
            return;
        }

        var groupCount = boxEntries.Count / ColorGroupSize;
        var colorGroupCounts = new Dictionary<GameManager.BoxColor, int>();
        for (var i = 0; i < colorPool.Count; i++)
        {
            colorGroupCounts[colorPool[i]] = 0;
        }

        var baseGroupsPerColor = groupCount / colorPool.Count;
        var remainderGroups = groupCount % colorPool.Count;
        for (var i = 0; i < colorPool.Count; i++)
        {
            colorGroupCounts[colorPool[i]] = baseGroupsPerColor;
        }

        var shuffledPool = new List<GameManager.BoxColor>(colorPool);
        Shuffle(shuffledPool, rng);
        for (var i = 0; i < remainderGroups; i++)
        {
            colorGroupCounts[shuffledPool[i]]++;
        }

        var distributedColors = new List<GameManager.BoxColor>(boxEntries.Count);
        foreach (var color in colorPool)
        {
            var groupsForColor = colorGroupCounts[color];
            for (var g = 0; g < groupsForColor; g++)
            {
                for (var repeat = 0; repeat < ColorGroupSize; repeat++)
                {
                    distributedColors.Add(color);
                }
            }
        }

        Shuffle(distributedColors, rng);

        var changedShelves = new HashSet<ShelfInteractionController>();
        for (var i = 0; i < boxEntries.Count && i < distributedColors.Count; i++)
        {
            var entry = boxEntries[i];
            ApplyColorForBox(entry.box, distributedColors[i], entry.keepGray);
            ApplySortingForBox(entry.box, entry.stackIndex);
            if (entry.shelf != null)
            {
                changedShelves.Add(entry.shelf);
            }
        }

        foreach (var shelf in changedShelves)
        {
            shelf.RefreshBoxVisualStates();
        }
    }

    private void ApplyRestartColorVariationOnCurrentBoard()
    {
        var colorPool = ResolveLimitedColorPool();
        if (colorPool == null || colorPool.Count <= 1)
        {
            return;
        }

        var candidateBoxes = new List<Transform>();
        var ownerShelves = new List<ShelfInteractionController>();
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            var shelf = shelfGo != null ? shelfGo.GetComponent<ShelfInteractionController>() : null;
            if (shelf == null || shelf.StackRoot == null)
            {
                continue;
            }

            for (var b = 0; b < shelf.StackRoot.childCount; b++)
            {
                var box = shelf.StackRoot.GetChild(b);
                if (box == null)
                {
                    continue;
                }

                var state = box.GetComponent<BoxVisualState>();
                if (state == null || state.IsGrayed)
                {
                    continue;
                }

                candidateBoxes.Add(box);
                ownerShelves.Add(shelf);
            }
        }

        if (candidateBoxes.Count <= 0)
        {
            return;
        }

        var minRatio = Mathf.Clamp01(restartColorChangeRatioMin);
        var maxRatio = Mathf.Clamp01(restartColorChangeRatioMax);
        if (maxRatio < minRatio)
        {
            var tmp = minRatio;
            minRatio = maxRatio;
            maxRatio = tmp;
        }

        var randomSeed = unchecked((int)DateTime.UtcNow.Ticks) ^ spawnSeed ^ candidateBoxes.Count * 197;
        var rng = new System.Random(randomSeed);
        var ratio = (float)(minRatio + (maxRatio - minRatio) * rng.NextDouble());
        var targetChangeCount = Mathf.Clamp(Mathf.RoundToInt(candidateBoxes.Count * ratio), 1, candidateBoxes.Count);

        var changedShelves = new HashSet<ShelfInteractionController>();
        for (var i = candidateBoxes.Count - 1; i > 0; i--)
        {
            var swap = rng.Next(i + 1);
            var tmpBox = candidateBoxes[i];
            candidateBoxes[i] = candidateBoxes[swap];
            candidateBoxes[swap] = tmpBox;

            var tmpShelf = ownerShelves[i];
            ownerShelves[i] = ownerShelves[swap];
            ownerShelves[swap] = tmpShelf;
        }

        var changedCount = 0;
        for (var i = 0; i < targetChangeCount; i++)
        {
            var box = candidateBoxes[i];
            if (box == null)
            {
                continue;
            }

            var state = box.GetComponent<BoxVisualState>();
            if (state == null)
            {
                continue;
            }

            var nextColor = ResolveRandomChangedColor(state.OriginalColorType, state.OriginalColorType, colorPool, rng);
            ApplyColorForBox(box, nextColor, false);
            ApplySortingForBox(box, box.GetSiblingIndex());
            changedCount++;

            var owner = ownerShelves[i];
            if (owner != null)
            {
                changedShelves.Add(owner);
            }
        }

        foreach (var shelf in changedShelves)
        {
            if (shelf != null)
            {
                shelf.RefreshBoxVisualStates();
            }
        }

        LogInfo($"Restart color variation applied | changed={changedCount}/{candidateBoxes.Count} | ratio={ratio:F2}");
    }

    private GameManager.BoxColor ResolveRandomChangedColor(GameManager.BoxColor previousColor, GameManager.BoxColor candidateColor, IReadOnlyList<GameManager.BoxColor> pool, System.Random rng)
    {
        if (pool == null || pool.Count <= 0)
        {
            return candidateColor;
        }

        if (pool.Count == 1)
        {
            return pool[0];
        }

        if (!candidateColor.Equals(previousColor))
        {
            return candidateColor;
        }

        var safety = 0;
        var picked = candidateColor;
        while (picked.Equals(previousColor) && safety < 16)
        {
            picked = pool[rng.Next(pool.Count)];
            safety++;
        }

        if (!picked.Equals(previousColor))
        {
            return picked;
        }

        for (var i = 0; i < pool.Count; i++)
        {
            if (!pool[i].Equals(previousColor))
            {
                return pool[i];
            }
        }

        return candidateColor;
    }

    public void ToggleRuntimeModeByButton()
    {
        var nextMode = runtimeMode == RuntimeMode.GameMode ? RuntimeMode.LevelDesignMode : RuntimeMode.GameMode;
        SetRuntimeMode(nextMode, true);
    }

    private bool ShouldUseDesignedLayoutForCurrentMode()
    {
        if (runtimeMode == RuntimeMode.GameMode)
        {
            return useDesignedLevelInGameMode;
        }

        return useDesignedLevelInDesignMode;
    }

    private void FinalizeAfterShelvesSpawned()
    {
        EnsureColumnConfigState(spawnedShelves.Count, false);
        if (!IsLevelDesignMode && applyRestartColorVariationOnNextRefresh)
        {
            ApplyRestartColorVariationOnCurrentBoard();
            applyRestartColorVariationOnNextRefresh = false;
        }

        ApplyBoxInteractionMode();
        ApplyLevelNavigationVisibility();
        ApplySaveLevelButtonVisibility();
        ApplyDesignToolsVisibility();
        ApplyRestartSettingsButtonVisibility();
        UpdateModeToggleButtonLabel();
        UpdateLevelIndicator();
        UpdateEconomyHud();
        UpdateDesignToolsTexts();

        if (IsLevelDesignMode)
        {
            HideDimOverlay();
            HideHintMessage();
            HideWinMessage();
            StopCatIntroVisual();
            return;
        }

        SpawnTrucks();
        if (!truckEliminationRunning)
        {
            StartCoroutine(ProcessTruckEliminationsRoutine());
        }

        SetupCatFlowForCurrentBoard();
    }

    private void ApplyBoxInteractionMode()
    {
        var enableInteraction = !IsLevelDesignMode;

        var allBoxes = runtimeBoxRoot != null
            ? runtimeBoxRoot.GetComponentsInChildren<BoxInteractionController>(true)
            : FindObjectsOfType<BoxInteractionController>(true);
        for (var i = 0; i < allBoxes.Length; i++)
        {
            if (allBoxes[i] != null)
            {
                allBoxes[i].enabled = enableInteraction;
            }
        }

        var colliders = runtimeBoxRoot != null
            ? runtimeBoxRoot.GetComponentsInChildren<Collider2D>(true)
            : FindObjectsOfType<Collider2D>(true);
        for (var i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
            {
                colliders[i].enabled = enableInteraction;
            }
        }
    }

    private DesignedLevelData BuildDesignedLevelDataFromRuntime()
    {
        EnsureColumnConfigState(spawnedShelves.Count, false);
        var data = new DesignedLevelData
        {
            version = 1,
            levelIndex = Mathf.Max(1, currentLevelIndex),
            levelId = GetCurrentLevelId(),
            shelfColumnCount = Mathf.Max(1, shelfColumnCount),
            columnShelfCounts = new List<int>(columnShelfCounts),
            columnVerticalOffsets = new List<float>(columnVerticalOffsets),
            shelves = new List<DesignedShelfData>()
        };

        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                continue;
            }

            var shelfData = new DesignedShelfData
            {
                position = shelfGo.transform.position,
                rotation = shelfGo.transform.rotation,
                boxes = new List<DesignedBoxData>()
            };

            for (var b = 0; b < shelf.StackRoot.childCount; b++)
            {
                var box = shelf.StackRoot.GetChild(b);
                var state = box != null ? box.GetComponent<BoxVisualState>() : null;
                if (state == null)
                {
                    continue;
                }

                shelfData.boxes.Add(new DesignedBoxData
                {
                    color = state.OriginalColorType,
                    grayed = state.IsGrayed
                });
            }

            data.shelves.Add(shelfData);
        }

        return data;
    }

    private bool TryLoadDesignedLevel(out DesignedLevelData data)
    {
        data = null;
        if (!TryLoadDesignedLevelCollection(out var collection) || collection == null || collection.levels == null || collection.levels.Count == 0)
        {
            return false;
        }

        var targetIndex = Mathf.Max(1, currentLevelIndex);
        var targetId = GetCurrentLevelId();
        var best = FindDesignedLevel(collection, targetIndex, targetId);
        if (best == null || best.shelves == null || best.shelves.Count <= 0)
        {
            return false;
        }

        data = best;
        return true;
    }

    private void SpawnDesignedLayout(DesignedLevelData data)
    {
        if (data == null || data.shelves == null)
        {
            return;
        }

        var designedShelfCount = data.shelves.Count;
        if (data.shelfColumnCount > 0)
        {
            shelfColumnCount = Mathf.Max(1, data.shelfColumnCount);
        }

        EnsureColumnConfigState(designedShelfCount, true);
        if (data.columnShelfCounts != null && data.columnShelfCounts.Count > 0)
        {
            var expectedColumns = Mathf.Clamp(shelfColumnCount, 1, Mathf.Max(1, designedShelfCount));
            columnShelfCounts.Clear();
            for (var i = 0; i < expectedColumns; i++)
            {
                var value = i < data.columnShelfCounts.Count ? Mathf.Max(0, data.columnShelfCounts[i]) : 0;
                columnShelfCounts.Add(value);
            }

            var currentTotal = GetTotalShelvesFromColumns();
            if (currentTotal != designedShelfCount)
            {
                RebuildColumnConfigsEvenly(designedShelfCount);
            }
        }

        if (data.columnVerticalOffsets != null && data.columnVerticalOffsets.Count > 0)
        {
            for (var i = 0; i < columnVerticalOffsets.Count; i++)
            {
                var value = i < data.columnVerticalOffsets.Count ? data.columnVerticalOffsets[i] : 0f;
                columnVerticalOffsets[i] = Mathf.Clamp(value, -maxColumnVerticalOffset, maxColumnVerticalOffset);
            }
        }

        var spawnCount = 0;
        for (var i = 0; i < data.shelves.Count; i++)
        {
            var shelfData = data.shelves[i];
            if (shelfData == null)
            {
                continue;
            }

            var shelf = Instantiate(shelfPrefab, shelfData.position, shelfData.rotation, runtimeShelfRoot);
            shelf.name = $"Shelf_{spawnCount + 1}";
            spawnedShelves.Add(shelf);

            var colors = new List<GameManager.BoxColor>();
            var grayStates = new List<bool>();
            if (shelfData.boxes != null)
            {
                for (var b = 0; b < shelfData.boxes.Count; b++)
                {
                    if (shelfData.boxes[b] != null)
                    {
                        colors.Add(shelfData.boxes[b].color);
                        grayStates.Add(shelfData.boxes[b].grayed);
                    }
                }
            }

            SpawnBoxesForShelf(shelf, colors, 0, colors.Count);
            EnsureShelfInteractionForRuntime(shelf, spawnCount);
            ApplyDesignedGrayStates(shelf, colors, grayStates);
            spawnCount++;
        }

        LogInfo($"Spawn designed layout complete | shelfCount={spawnCount}");
    }

    private void ApplyDesignedGrayStates(GameObject shelf, IReadOnlyList<GameManager.BoxColor> colors, IReadOnlyList<bool> grayStates)
    {
        if (shelf == null || grayStates == null || grayStates.Count <= 0)
        {
            return;
        }

        var shelfInteraction = shelf.GetComponent<ShelfInteractionController>();
        if (shelfInteraction == null || shelfInteraction.StackRoot == null || shelfInteraction.StackRoot.childCount <= 0)
        {
            return;
        }

        var count = Mathf.Min(shelfInteraction.StackRoot.childCount, grayStates.Count);
        for (var i = 0; i < count; i++)
        {
            var box = shelfInteraction.StackRoot.GetChild(i);
            if (box == null)
            {
                continue;
            }

            var state = box.GetComponent<BoxVisualState>();
            var colorType = state != null
                ? state.OriginalColorType
                : ResolveColorType(colors, i);

            ApplyColorForBox(box, colorType, grayStates[i]);
        }

        shelfInteraction.RefreshBoxVisualStates();
    }

    private string GetDesignedLevelStorageKey()
    {
        var safeId = string.IsNullOrWhiteSpace(designedLevelId) ? "level_001" : designedLevelId.Trim();
        return $"ShelfSpawn.DesignedLevel.{safeId}";
    }

    private void SaveDesignedLevelToFile(DesignedLevelData data)
    {
        if (data == null)
        {
            return;
        }

        var folderPath = GetDesignedLevelFolderPath();
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        var collection = LoadDesignedLevelCollectionOrCreate();
        UpsertDesignedLevel(collection, data);

        var path = GetDesignedLevelsFilePath();
        var json = JsonUtility.ToJson(collection, true);
        File.WriteAllText(path, json);
    }

    private string GetDesignedLevelFolderPath()
    {
        return Path.Combine(Application.dataPath, DesignedLevelFolderName);
    }

    private string GetDesignedLevelsFilePath()
    {
        return Path.Combine(GetDesignedLevelFolderPath(), DesignedLevelsFileName);
    }

    private bool TryLoadDesignedLevelCollection(out DesignedLevelCollectionData collection)
    {
        collection = null;
        var path = GetDesignedLevelsFilePath();
        if (!File.Exists(path))
        {
            return false;
        }

        var json = File.ReadAllText(path);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            collection = JsonUtility.FromJson<DesignedLevelCollectionData>(json);
            return collection != null;
        }
        catch
        {
            collection = null;
            return false;
        }
    }

    private DesignedLevelCollectionData LoadDesignedLevelCollectionOrCreate()
    {
        if (TryLoadDesignedLevelCollection(out var collection) && collection != null)
        {
            if (collection.levels == null)
            {
                collection.levels = new List<DesignedLevelData>();
            }

            return collection;
        }

        return new DesignedLevelCollectionData
        {
            version = 1,
            levels = new List<DesignedLevelData>()
        };
    }

    private DesignedLevelData FindDesignedLevel(DesignedLevelCollectionData collection, int targetIndex, string targetId)
    {
        if (collection == null || collection.levels == null)
        {
            return null;
        }

        for (var i = 0; i < collection.levels.Count; i++)
        {
            var level = collection.levels[i];
            if (level == null)
            {
                continue;
            }

            var levelIndex = ResolveDesignedLevelIndex(level);
            if (levelIndex == targetIndex)
            {
                return level;
            }
        }

        for (var i = 0; i < collection.levels.Count; i++)
        {
            var level = collection.levels[i];
            if (level == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(targetId) && string.Equals(level.levelId, targetId, StringComparison.OrdinalIgnoreCase))
            {
                return level;
            }
        }

        return null;
    }

    private void UpsertDesignedLevel(DesignedLevelCollectionData collection, DesignedLevelData data)
    {
        if (collection == null)
        {
            return;
        }

        if (collection.levels == null)
        {
            collection.levels = new List<DesignedLevelData>();
        }

        var targetIndex = Mathf.Max(1, ResolveDesignedLevelIndex(data));
        data.levelIndex = targetIndex;

        for (var i = 0; i < collection.levels.Count; i++)
        {
            var existing = collection.levels[i];
            if (existing == null)
            {
                continue;
            }

            var existingIndex = ResolveDesignedLevelIndex(existing);
            if (existingIndex == targetIndex)
            {
                collection.levels[i] = data;
                return;
            }
        }

        collection.levels.Add(data);
        collection.levels = collection.levels
            .Where(level => level != null)
            .OrderBy(ResolveDesignedLevelIndex)
            .ToList();
    }

    private int ResolveDesignedLevelIndex(DesignedLevelData data)
    {
        if (data == null)
        {
            return 1;
        }

        if (data.levelIndex > 0)
        {
            return data.levelIndex;
        }

        if (!string.IsNullOrWhiteSpace(data.levelId))
        {
            var digits = new string(data.levelId.Where(char.IsDigit).ToArray());
            if (!string.IsNullOrWhiteSpace(digits) && int.TryParse(digits, out var parsed) && parsed > 0)
            {
                return parsed;
            }
        }

        return 1;
    }

    private int ResolveNextDesignLevelIndex()
    {
        if (!TryLoadDesignedLevelCollection(out var collection) || collection == null || collection.levels == null || collection.levels.Count == 0)
        {
            return 1;
        }

        var max = 0;
        for (var i = 0; i < collection.levels.Count; i++)
        {
            var idx = ResolveDesignedLevelIndex(collection.levels[i]);
            if (idx > max)
            {
                max = idx;
            }
        }

        return Mathf.Max(1, max + 1);
    }

    private bool TryLoadFirstDesignedLevel(out DesignedLevelData data, out int levelIndex)
    {
        data = null;
        levelIndex = 1;
        if (!TryLoadDesignedLevelCollection(out var collection) || collection == null || collection.levels == null || collection.levels.Count == 0)
        {
            return false;
        }

        var sorted = collection.levels
            .Where(level => level != null && level.shelves != null && level.shelves.Count > 0)
            .OrderBy(ResolveDesignedLevelIndex)
            .ToList();
        if (sorted.Count <= 0)
        {
            return false;
        }

        data = sorted[0];
        levelIndex = Mathf.Max(1, ResolveDesignedLevelIndex(data));
        return true;
    }

    private bool IsCurrentLevelFinalDesignedLevel()
    {
        if (runtimeMode != RuntimeMode.GameMode || !useDesignedLevelInGameMode)
        {
            return false;
        }

        if (!TryLoadDesignedLevelCollection(out var collection) || collection == null || collection.levels == null || collection.levels.Count == 0)
        {
            return false;
        }

        var maxIndex = 0;
        for (var i = 0; i < collection.levels.Count; i++)
        {
            var level = collection.levels[i];
            if (level == null || level.shelves == null || level.shelves.Count <= 0)
            {
                continue;
            }

            var idx = ResolveDesignedLevelIndex(level);
            if (idx > maxIndex)
            {
                maxIndex = idx;
            }
        }

        if (maxIndex <= 0)
        {
            return false;
        }

        return Mathf.Max(1, currentLevelIndex) >= maxIndex;
    }

    private string GetCurrentLevelId()
    {
        if (useNumericLevelId)
        {
            var index = Mathf.Max(1, currentLevelIndex);
            var prefix = string.IsNullOrWhiteSpace(levelIdPrefix) ? "level_" : levelIdPrefix.Trim();
            var padding = Mathf.Max(1, levelNumberPadding);
            return prefix + index.ToString().PadLeft(padding, '0');
        }

        return string.IsNullOrWhiteSpace(designedLevelId) ? "level_001" : designedLevelId.Trim();
    }

    private void PrepareForLevelSwitch()
    {
        staminaConsumedForCurrentRound = false;
        gameWon = false;
        isFinalDesignedLevelWin = false;
        HideDimOverlay();
        HideWinResultPanel();
        if (Mathf.Abs(Time.timeScale) < 0.0001f)
        {
            Time.timeScale = 1f;
        }

        if (catIntroRoutine != null)
        {
            StopCoroutine(catIntroRoutine);
            catIntroRoutine = null;
        }

        if (catHintRoutine != null)
        {
            StopCoroutine(catHintRoutine);
            catHintRoutine = null;
        }

        StopCatIntroVisual();
        HideHintMessage();
        HideWinMessage();
    }

    private void UpdateLevelIndicator()
    {
        var levelText = EnsureLevelIndicatorText();
        if (levelText == null)
        {
            return;
        }

        levelText.text = $"第{Mathf.Max(1, currentLevelIndex)}关";
    }

    private void ApplyLevelNavigationVisibility()
    {
        var visible = (runtimeMode == RuntimeMode.GameMode && showLevelNavigationInGameMode)
            || (runtimeMode == RuntimeMode.LevelDesignMode && showLevelNavigationInDesignMode);
        var navButtonsVisible = visible && !hidePrimaryControlButtons;

        if (previousLevelButton != null)
        {
            previousLevelButton.gameObject.SetActive(navButtonsVisible);
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.gameObject.SetActive(navButtonsVisible);
        }

        if (levelIndicatorTextRef != null)
        {
            levelIndicatorTextRef.gameObject.SetActive(visible);
        }
    }

    private void ApplyDesignToolsVisibility()
    {
        var designVisible = runtimeMode == RuntimeMode.LevelDesignMode && showDesignToolsPanel;
        if (designToolsPanelRoot != null)
        {
            designToolsPanelRoot.gameObject.SetActive(designVisible);
        }

        if (refreshColorsButton != null)
        {
            refreshColorsButton.gameObject.SetActive(designVisible);
        }

        if (columnControlAreaRoot != null)
        {
            columnControlAreaRoot.gameObject.SetActive(designVisible);
        }

        if (modeToggleButton != null)
        {
            modeToggleButton.gameObject.SetActive(!hidePrimaryControlButtons);
        }

        if (shelfConfigOverlayRoot != null && !designVisible)
        {
            shelfConfigOverlayRoot.gameObject.SetActive(false);
        }
    }

    private void UpdateModeToggleButtonLabel()
    {
        if (modeToggleButton == null)
        {
            return;
        }

        var label = modeToggleButton.GetComponentInChildren<Text>(true);
        if (label == null)
        {
            return;
        }

        label.text = runtimeMode == RuntimeMode.GameMode ? enterDesignModeButtonText : enterGameModeButtonText;
    }

    private void ApplyPrimaryControlButtonsVisibility()
    {
        ApplyLevelNavigationVisibility();
        ApplyRegenerateButtonVisibility();
        ApplyDesignToolsVisibility();
        ApplyRestartSettingsButtonVisibility();
        ApplyTestClearSaveButtonVisibility();
    }

    private void UpdateDesignToolsTexts()
    {
        EnsureColumnConfigState(ResolveRuntimeShelfCountForLayout(), false);

        if (shelfColumnCountTextRef != null)
        {
            shelfColumnCountTextRef.text = $"分成几列\n{Mathf.Max(1, shelfColumnCount)}";
        }

        var totalColorCount = ResolveLimitedColorPool().Count;
        var usedColorCount = ResolveCurrentUsedColorCount(totalColorCount);
        if (colorTypeCountTextRef != null)
        {
            colorTypeCountTextRef.text = $"颜色数量\n{Mathf.Max(1, totalColorCount)}/{Mathf.Max(0, usedColorCount)}";
        }

        RebuildColumnControlAreaChildren();
    }

    private void HandleDesignShelfConfigInput()
    {
        if (!IsLevelDesignMode)
        {
            return;
        }

        if (!TryReadPrimaryPointerDown(out var screenPos, out var pointerId))
        {
            return;
        }

        if (EventSystem.current != null)
        {
            var overUI = pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
            if (overUI)
            {
                return;
            }
        }

        if (!TryResolveShelfByScreenPosition(screenPos, out var shelf))
        {
            return;
        }

        OpenShelfConfigOverlayForShelf(shelf);
    }

    private bool TryReadPrimaryPointerDown(out Vector2 screenPos, out int pointerId)
    {
        if (Input.touchCount > 0)
        {
            var touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Began)
            {
                screenPos = touch.position;
                pointerId = touch.fingerId;
                return true;
            }
        }

        if (Input.GetMouseButtonDown(0))
        {
            screenPos = Input.mousePosition;
            pointerId = -1;
            return true;
        }

        screenPos = default;
        pointerId = -1;
        return false;
    }

    private bool TryResolveShelfByScreenPosition(Vector2 screenPos, out ShelfInteractionController shelf)
    {
        shelf = null;
        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            return false;
        }

        var world = cameraRef.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, Mathf.Abs(spawnPlaneZ - cameraRef.transform.position.z)));
        var hits = Physics2D.OverlapPointAll(new Vector2(world.x, world.y), ~0);
        if (hits == null || hits.Length <= 0)
        {
            return false;
        }

        for (var i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            var candidate = hits[i].GetComponentInParent<ShelfInteractionController>();
            if (candidate == null || candidate.gameObject == null)
            {
                continue;
            }

            if (spawnedShelves.Contains(candidate.gameObject))
            {
                shelf = candidate;
                return true;
            }
        }

        return false;
    }

    private void OpenShelfConfigOverlayForShelf(ShelfInteractionController shelf)
    {
        if (shelf == null)
        {
            return;
        }

        EnsureShelfConfigOverlay();
        if (shelfConfigOverlayRoot == null)
        {
            return;
        }

        editingShelfConfig = shelf;
        editingShelfBoxCount = Mathf.Clamp(shelf.BoxCount, 0, 4);
        editingShelfGrayCount = Mathf.Clamp(CountShelfGrayBoxes(shelf), 0, Mathf.Min(3, Mathf.Max(0, editingShelfBoxCount - 1)));
        UpdateShelfConfigOverlayTexts();
        shelfConfigOverlayRoot.gameObject.SetActive(true);
    }

    private void CloseShelfConfigOverlay()
    {
        editingShelfConfig = null;
        if (shelfConfigOverlayRoot != null)
        {
            shelfConfigOverlayRoot.gameObject.SetActive(false);
        }
    }

    private void EnsureShelfConfigOverlay()
    {
        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (shelfConfigOverlayRoot == null)
        {
            var existing = canvas.transform.Find("ShelfConfigOverlay") as RectTransform;
            if (existing != null)
            {
                shelfConfigOverlayRoot = existing;
            }
            else
            {
                var overlayObj = new GameObject("ShelfConfigOverlay", typeof(RectTransform), typeof(Image));
                shelfConfigOverlayRoot = overlayObj.GetComponent<RectTransform>();
                overlayObj.transform.SetParent(canvas.transform, false);
                var overlayImage = overlayObj.GetComponent<Image>();
                overlayImage.color = new Color(0f, 0f, 0f, 0.72f);
                overlayImage.raycastTarget = true;

                var contentObj = new GameObject("Content", typeof(RectTransform), typeof(Image));
                shelfConfigContentRoot = contentObj.GetComponent<RectTransform>();
                contentObj.transform.SetParent(shelfConfigOverlayRoot, false);
                var contentImage = contentObj.GetComponent<Image>();
                contentImage.color = new Color(0.08f, 0.12f, 0.18f, 0.96f);
                contentImage.raycastTarget = true;

                shelfConfigTitleText = CreateConfigText(shelfConfigContentRoot, "Title", new Vector2(0f, -32f), new Vector2(420f, 48f), 30);
                shelfConfigBoxCountText = CreateConfigText(shelfConfigContentRoot, "BoxCountText", new Vector2(0f, -92f), new Vector2(420f, 42f), 24);
                shelfConfigGrayCountText = CreateConfigText(shelfConfigContentRoot, "GrayCountText", new Vector2(0f, -202f), new Vector2(420f, 42f), 24);
                shelfConfigColorConfigText = CreateConfigText(shelfConfigContentRoot, "ColorConfigText", new Vector2(0f, -302f), new Vector2(440f, 42f), 22);

                CreateConfigOptionButtons(shelfConfigContentRoot, true);
                CreateConfigOptionButtons(shelfConfigContentRoot, false);
                CreateConfigColorButtons(shelfConfigContentRoot);
                CreateConfigCloseButton(shelfConfigContentRoot);
            }
        }

        if (shelfConfigOverlayRoot == null)
        {
            return;
        }

        shelfConfigOverlayRoot.anchorMin = Vector2.zero;
        shelfConfigOverlayRoot.anchorMax = Vector2.one;
        shelfConfigOverlayRoot.offsetMin = Vector2.zero;
        shelfConfigOverlayRoot.offsetMax = Vector2.zero;

        if (shelfConfigContentRoot == null)
        {
            shelfConfigContentRoot = shelfConfigOverlayRoot.Find("Content") as RectTransform;
        }

        if (shelfConfigContentRoot != null)
        {
            shelfConfigContentRoot.anchorMin = new Vector2(0.5f, 0.5f);
            shelfConfigContentRoot.anchorMax = new Vector2(0.5f, 0.5f);
            shelfConfigContentRoot.pivot = new Vector2(0.5f, 0.5f);
            shelfConfigContentRoot.sizeDelta = new Vector2(500f, 460f);
            shelfConfigContentRoot.anchoredPosition = Vector2.zero;
        }

        if (shelfConfigTitleText == null)
        {
            shelfConfigTitleText = shelfConfigOverlayRoot.Find("Content/Title")?.GetComponent<Text>();
        }

        if (shelfConfigBoxCountText == null)
        {
            shelfConfigBoxCountText = shelfConfigOverlayRoot.Find("Content/BoxCountText")?.GetComponent<Text>();
        }

        if (shelfConfigGrayCountText == null)
        {
            shelfConfigGrayCountText = shelfConfigOverlayRoot.Find("Content/GrayCountText")?.GetComponent<Text>();
        }

        if (shelfConfigColorConfigText == null)
        {
            shelfConfigColorConfigText = shelfConfigOverlayRoot.Find("Content/ColorConfigText")?.GetComponent<Text>();
        }

        EnsureShelfConfigColorButtons();
    }

    private Text CreateConfigText(RectTransform root, string name, Vector2 anchoredPos, Vector2 size, int fontSize)
    {
        var textObj = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(root, false);
        var rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private void CreateConfigOptionButtons(RectTransform root, bool boxCount)
    {
        var startX = -180f;
        var stepX = 90f;
        var rowY = boxCount ? -140f : -250f;
        var max = boxCount ? 4 : 3;

        for (var i = 0; i <= max; i++)
        {
            var value = i;
            var button = CreatePanelButton(
                root,
                boxCount ? $"BoxCount_{value}" : $"GrayCount_{value}",
                value.ToString(),
                new Vector2(startX + stepX * i, rowY),
                new Vector2(72f, 44f),
                boxCount
                    ? (UnityEngine.Events.UnityAction)(() => SetEditingShelfBoxCount(value))
                    : (UnityEngine.Events.UnityAction)(() => SetEditingShelfGrayCount(value)));

            if (button != null)
            {
                button.gameObject.SetActive(true);
            }
        }
    }

    private void CreateConfigCloseButton(RectTransform root)
    {
        CreatePanelButton(root, "CloseButton", "关闭", new Vector2(0f, -404f), new Vector2(140f, 46f), CloseShelfConfigOverlay);
    }

    private void CreateConfigColorButtons(RectTransform root)
    {
        if (root == null)
        {
            return;
        }

        shelfConfigColorButtons.Clear();
        var startX = -180f;
        var stepX = 120f;
        var rowY = -352f;

        for (var i = 0; i < 4; i++)
        {
            var index = i;
            var button = CreatePanelButton(
                root,
                $"ColorSlot_{index}",
                $"{index + 1}:空",
                new Vector2(startX + stepX * index, rowY),
                new Vector2(104f, 44f),
                () => CycleEditingShelfBoxColor(index));

            if (button != null)
            {
                shelfConfigColorButtons.Add(button);
            }
        }
    }

    private void EnsureShelfConfigColorButtons()
    {
        if (shelfConfigContentRoot == null)
        {
            return;
        }

        if (shelfConfigColorConfigText == null)
        {
            shelfConfigColorConfigText = shelfConfigContentRoot.Find("ColorConfigText")?.GetComponent<Text>();
        }

        if (shelfConfigColorConfigText == null)
        {
            shelfConfigColorConfigText = CreateConfigText(shelfConfigContentRoot, "ColorConfigText", new Vector2(0f, -302f), new Vector2(440f, 42f), 22);
        }

        shelfConfigColorButtons.Clear();
        for (var i = 0; i < 4; i++)
        {
            var child = shelfConfigContentRoot.Find($"ColorSlot_{i}");
            Button button = null;
            if (child != null)
            {
                button = child.GetComponent<Button>();
            }

            if (button == null)
            {
                var index = i;
                button = CreatePanelButton(
                    shelfConfigContentRoot,
                    $"ColorSlot_{index}",
                    $"{index + 1}:空",
                    new Vector2(-180f + 120f * index, -352f),
                    new Vector2(104f, 44f),
                    () => CycleEditingShelfBoxColor(index));
            }

            if (button != null)
            {
                var index = i;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => CycleEditingShelfBoxColor(index));

                var rect = button.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 1f);
                rect.anchorMax = new Vector2(0.5f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(104f, 44f);
                rect.anchoredPosition = new Vector2(-180f + 120f * i, -352f);

                shelfConfigColorButtons.Add(button);
            }
        }
    }

    private void CycleEditingShelfBoxColor(int boxIndex)
    {
        if (editingShelfConfig == null || editingShelfConfig.StackRoot == null)
        {
            return;
        }

        if (boxIndex < 0 || boxIndex >= editingShelfConfig.StackRoot.childCount)
        {
            return;
        }

        var box = editingShelfConfig.StackRoot.GetChild(boxIndex);
        if (box == null)
        {
            return;
        }

        var state = box.GetComponent<BoxVisualState>();
        if (state == null)
        {
            return;
        }

        var pool = ResolveLimitedColorPool();
        if (pool == null || pool.Count <= 0)
        {
            pool = new List<GameManager.BoxColor>((GameManager.BoxColor[])Enum.GetValues(typeof(GameManager.BoxColor)));
        }

        if (pool.Count <= 0)
        {
            return;
        }

        var currentIndex = pool.IndexOf(state.OriginalColorType);
        var nextIndex = currentIndex >= 0 ? (currentIndex + 1) % pool.Count : 0;
        var nextColor = pool[nextIndex];

        ApplyColorForBox(box, nextColor, state.IsGrayed);
        ApplySortingForBox(box, boxIndex);
        editingShelfConfig.RefreshBoxVisualStates();
        UpdateShelfConfigOverlayTexts();
    }

    private static string ResolveBoxColorButtonLabel(GameManager.BoxColor color)
    {
        switch (color)
        {
            case GameManager.BoxColor.Red:
                return "红";
            case GameManager.BoxColor.Blue:
                return "蓝";
            case GameManager.BoxColor.Green:
                return "绿";
            case GameManager.BoxColor.Yellow:
                return "黄";
            case GameManager.BoxColor.Purple:
                return "紫";
            case GameManager.BoxColor.Orange:
                return "橙";
            default:
                return color.ToString();
        }
    }

    private void SetEditingShelfBoxCount(int boxCount)
    {
        if (editingShelfConfig == null)
        {
            return;
        }

        editingShelfBoxCount = Mathf.Clamp(boxCount, 0, 4);
        editingShelfGrayCount = Mathf.Clamp(editingShelfGrayCount, 0, Mathf.Min(3, Mathf.Max(0, editingShelfBoxCount - 1)));
        ApplyEditingShelfConfig();
    }

    private void SetEditingShelfGrayCount(int grayCount)
    {
        if (editingShelfConfig == null)
        {
            return;
        }

        editingShelfGrayCount = Mathf.Clamp(grayCount, 0, Mathf.Min(3, Mathf.Max(0, editingShelfBoxCount - 1)));
        ApplyEditingShelfConfig();
    }

    private void ApplyEditingShelfConfig()
    {
        if (editingShelfConfig == null || editingShelfConfig.gameObject == null)
        {
            return;
        }

        var targetBoxCount = Mathf.Clamp(editingShelfBoxCount, 0, 4);
        var targetGrayCount = Mathf.Clamp(editingShelfGrayCount, 0, Mathf.Min(3, Mathf.Max(0, targetBoxCount - 1)));
        editingShelfBoxCount = targetBoxCount;
        editingShelfGrayCount = targetGrayCount;

        var colors = BuildDesignShelfColors(editingShelfConfig, targetBoxCount);
        ClearShelfBoxes(editingShelfConfig);
        SpawnBoxesForShelf(editingShelfConfig.gameObject, colors, targetGrayCount, targetBoxCount);
        editingShelfConfig.RefreshBoxVisualStates();
        ApplyBoxInteractionMode();
        UpdateDesignToolsTexts();
        UpdateShelfConfigOverlayTexts();
    }

    private List<GameManager.BoxColor> BuildDesignShelfColors(ShelfInteractionController shelf, int targetBoxCount)
    {
        var result = new List<GameManager.BoxColor>(Mathf.Max(0, targetBoxCount));
        if (targetBoxCount <= 0)
        {
            return result;
        }

        if (shelf != null && shelf.StackRoot != null)
        {
            for (var i = 0; i < shelf.StackRoot.childCount && result.Count < targetBoxCount; i++)
            {
                var box = shelf.StackRoot.GetChild(i);
                var state = box != null ? box.GetComponent<BoxVisualState>() : null;
                if (state != null)
                {
                    result.Add(state.OriginalColorType);
                }
            }
        }

        var pool = ResolveLimitedColorPool();
        if (pool.Count <= 0)
        {
            pool.AddRange((GameManager.BoxColor[])Enum.GetValues(typeof(GameManager.BoxColor)));
        }

        while (result.Count < targetBoxCount)
        {
            var idx = Mathf.Abs((shelf != null ? shelf.ShelfIndex : 0) + result.Count) % pool.Count;
            result.Add(pool[idx]);
        }

        return result;
    }

    private void ClearShelfBoxes(ShelfInteractionController shelf)
    {
        if (shelf == null || shelf.StackRoot == null)
        {
            return;
        }

        for (var i = shelf.StackRoot.childCount - 1; i >= 0; i--)
        {
            var child = shelf.StackRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }
    }

    private int CountShelfGrayBoxes(ShelfInteractionController shelf)
    {
        if (shelf == null || shelf.StackRoot == null)
        {
            return 0;
        }

        var grayCount = 0;
        var topIndex = shelf.StackRoot.childCount - 1;
        for (var i = 0; i < shelf.StackRoot.childCount; i++)
        {
            if (i == topIndex)
            {
                continue;
            }

            var box = shelf.StackRoot.GetChild(i);
            var state = box != null ? box.GetComponent<BoxVisualState>() : null;
            if (state != null && state.IsGrayed)
            {
                grayCount++;
            }
        }

        return grayCount;
    }

    private void UpdateShelfConfigOverlayTexts()
    {
        if (shelfConfigTitleText != null)
        {
            var shelfId = editingShelfConfig != null ? editingShelfConfig.ShelfIndex + 1 : 0;
            shelfConfigTitleText.text = shelfId > 0 ? $"货架{shelfId}配置" : "货架配置";
        }

        if (shelfConfigBoxCountText != null)
        {
            shelfConfigBoxCountText.text = $"箱子数量：{Mathf.Clamp(editingShelfBoxCount, 0, 4)}";
        }

        if (shelfConfigGrayCountText != null)
        {
            shelfConfigGrayCountText.text = $"隐藏(置灰)数量：{Mathf.Clamp(editingShelfGrayCount, 0, 3)}（最上层保持原色）";
        }

        if (shelfConfigColorConfigText != null)
        {
            shelfConfigColorConfigText.text = "箱子颜色（自下而上，点击切换）";
        }

        EnsureShelfConfigColorButtons();
        for (var i = 0; i < shelfConfigColorButtons.Count; i++)
        {
            var button = shelfConfigColorButtons[i];
            if (button == null)
            {
                continue;
            }

            var label = button.GetComponentInChildren<Text>(true);
            if (label == null)
            {
                continue;
            }

            var hasBox = editingShelfConfig != null
                && editingShelfConfig.StackRoot != null
                && i < editingShelfConfig.StackRoot.childCount;
            if (!hasBox)
            {
                label.text = $"{i + 1}:空";
                continue;
            }

            var box = editingShelfConfig.StackRoot.GetChild(i);
            var state = box != null ? box.GetComponent<BoxVisualState>() : null;
            if (state == null)
            {
                label.text = $"{i + 1}:空";
                continue;
            }

            label.text = $"{i + 1}:{ResolveBoxColorButtonLabel(state.OriginalColorType)}";
        }
    }

    private List<GameManager.BoxColor> ResolveLimitedColorPool()
    {
        var source = new List<GameManager.BoxColor>();
        if (boxGenerationManager != null)
        {
            source.AddRange(boxGenerationManager.GetActiveColorTypes());
        }

        if (source.Count == 0)
        {
            source.AddRange((GameManager.BoxColor[])Enum.GetValues(typeof(GameManager.BoxColor)));
        }

        source = source.Distinct().ToList();
        var count = Mathf.Clamp(currentColorTypeCount, 1, Mathf.Max(1, source.Count));
        return source.Take(count).ToList();
    }

    private int ResolveCurrentUsedColorCount(int totalColorCount)
    {
        if (totalColorCount <= 0)
        {
            return 0;
        }

        if (spawnedShelves.Count > 0)
        {
            var used = new HashSet<GameManager.BoxColor>();
            for (var i = 0; i < spawnedShelves.Count; i++)
            {
                var shelfGo = spawnedShelves[i];
                var shelf = shelfGo != null ? shelfGo.GetComponent<ShelfInteractionController>() : null;
                if (shelf == null || shelf.StackRoot == null)
                {
                    continue;
                }

                for (var b = 0; b < shelf.StackRoot.childCount; b++)
                {
                    var box = shelf.StackRoot.GetChild(b);
                    var state = box != null ? box.GetComponent<BoxVisualState>() : null;
                    if (state != null)
                    {
                        used.Add(state.OriginalColorType);
                    }
                }
            }

            return Mathf.Clamp(used.Count, 0, totalColorCount);
        }

        var targetCount = ResolveRuntimeShelfCountForLayout();
        var totalBoxes = ResolveConfiguredTotalBoxCount(targetCount);
        var groupCount = Mathf.Max(0, totalBoxes / ColorGroupSize);
        return Mathf.Clamp(groupCount, 0, totalColorCount);
    }

    private void EnsureShelfBoxCountsFollowColorGroupRule(List<int> shelfBoxCounts)
    {
        if (shelfBoxCounts == null || shelfBoxCounts.Count == 0)
        {
            return;
        }

        var totalBoxes = 0;
        for (var i = 0; i < shelfBoxCounts.Count; i++)
        {
            shelfBoxCounts[i] = Mathf.Max(0, shelfBoxCounts[i]);
            totalBoxes += shelfBoxCounts[i];
        }

        var remainder = totalBoxes % ColorGroupSize;
        if (remainder <= 0)
        {
            return;
        }

        for (var i = shelfBoxCounts.Count - 1; i >= 0 && remainder > 0; i--)
        {
            if (shelfBoxCounts[i] <= 0)
            {
                continue;
            }

            var removable = Mathf.Min(remainder, shelfBoxCounts[i]);
            shelfBoxCounts[i] -= removable;
            remainder -= removable;
        }
    }

    private List<List<GameManager.BoxColor>> BuildGroupedColorLayoutByCounts(IReadOnlyList<int> shelfBoxCounts, int seed)
    {
        if (shelfBoxCounts == null || shelfBoxCounts.Count == 0)
        {
            return null;
        }

        var pool = ResolveLimitedColorPool();
        if (pool.Count <= 0)
        {
            return null;
        }

        var totalBoxes = 0;
        for (var i = 0; i < shelfBoxCounts.Count; i++)
        {
            totalBoxes += Mathf.Max(0, shelfBoxCounts[i]);
        }

        if (totalBoxes <= 0)
        {
            return new List<List<GameManager.BoxColor>>();
        }

        var groupCount = totalBoxes / ColorGroupSize;
        var usedColorCount = Mathf.Clamp(Mathf.Min(pool.Count, groupCount > 0 ? groupCount : 1), 1, pool.Count);
        var usedPalette = pool.Take(usedColorCount).ToList();

        var rng = new System.Random(seed);
        var groupColors = new List<GameManager.BoxColor>(Mathf.Max(1, groupCount));
        for (var i = 0; i < groupCount; i++)
        {
            if (i < usedPalette.Count)
            {
                groupColors.Add(usedPalette[i]);
            }
            else
            {
                groupColors.Add(usedPalette[rng.Next(usedPalette.Count)]);
            }
        }

        Shuffle(groupColors, rng);

        var flatColors = new List<GameManager.BoxColor>(totalBoxes);
        for (var i = 0; i < groupColors.Count; i++)
        {
            for (var repeat = 0; repeat < ColorGroupSize; repeat++)
            {
                flatColors.Add(groupColors[i]);
            }
        }

        var remainder = totalBoxes - flatColors.Count;
        for (var i = 0; i < remainder; i++)
        {
            flatColors.Add(usedPalette[rng.Next(usedPalette.Count)]);
        }

        var result = new List<List<GameManager.BoxColor>>(shelfBoxCounts.Count);
        var cursor = 0;
        for (var i = 0; i < shelfBoxCounts.Count; i++)
        {
            var count = Mathf.Max(0, shelfBoxCounts[i]);
            var shelfColors = new List<GameManager.BoxColor>(count);
            for (var b = 0; b < count && cursor < flatColors.Count; b++, cursor++)
            {
                shelfColors.Add(flatColors[cursor]);
            }

            result.Add(shelfColors);
        }

        return result;
    }

    private IReadOnlyList<GameManager.BoxColor> NormalizeShelfColorsByPool(IReadOnlyList<GameManager.BoxColor> source)
    {
        var pool = ResolveLimitedColorPool();
        if (pool.Count <= 0)
        {
            return source;
        }

        if (source == null)
        {
            return null;
        }

        var normalized = new List<GameManager.BoxColor>(source.Count);
        for (var i = 0; i < source.Count; i++)
        {
            var color = source[i];
            var idx = pool.IndexOf(color);
            if (idx >= 0)
            {
                normalized.Add(color);
            }
            else
            {
                normalized.Add(pool[i % pool.Count]);
            }
        }

        return normalized;
    }

    private void ReflowCurrentShelvesByColumns()
    {
        if (spawnedShelves.Count == 0)
        {
            return;
        }

        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            return;
        }

        EnsureColumnConfigState(spawnedShelves.Count, false);
        var positions = GenerateShelfPositions(cameraRef, spawnedShelves.Count);
        for (var i = 0; i < spawnedShelves.Count && i < positions.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            shelfGo.transform.position = positions[i];
            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf != null && shelf.StackRoot != null)
            {
                shelf.StackRoot.position = positions[i];
                shelf.StackRoot.rotation = shelfGo.transform.rotation;
            }
        }
    }

    public void RestartRoundByButton()
    {
        if (!TryConsumeStaminaOnRestart())
        {
            return;
        }

        PrepareForLevelSwitch();
        RegenerateShelves();
    }

    public void RefreshShelfColorByButton()
    {
        if (!isActiveAndEnabled || IsLevelDesignMode || gameWon)
        {
            return;
        }

        waitingRefreshShelfSelection = true;
        if (refreshShakeRoutine != null)
        {
            StopCoroutine(refreshShakeRoutine);
        }

        refreshShakeRoutine = StartCoroutine(PlayRefreshShakeRoutine());
    }

    public void UndoLastMoveByButton()
    {
        if (!isActiveAndEnabled || IsLevelDesignMode || gameWon)
        {
            return;
        }

        TryUndoLastVisualMove();
    }

    public void AddEmptyShelfByButton()
    {
        if (!isActiveAndEnabled || gameWon)
        {
            return;
        }

        TryAddEmptyShelf();
    }

    private IEnumerator PlayRefreshShakeRoutine()
    {
        EnsureShelfSubRoots();

        var shelfRootOrigin = runtimeShelfRoot != null ? runtimeShelfRoot.localPosition : Vector3.zero;
        var boxRootOrigin = runtimeBoxRoot != null ? runtimeBoxRoot.localPosition : Vector3.zero;
        var duration = Mathf.Max(0.01f, refreshShakeDuration);
        var amplitude = Mathf.Max(0f, refreshShakeAmplitude);
        var freq = Mathf.Max(0.1f, refreshShakeFrequency);

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var damper = 1f - Mathf.Clamp01(elapsed / duration);
            var offset = Mathf.Sin(elapsed * Mathf.PI * 2f * freq) * amplitude * damper;

            if (runtimeShelfRoot != null)
            {
                runtimeShelfRoot.localPosition = shelfRootOrigin + Vector3.right * offset;
            }

            if (runtimeBoxRoot != null)
            {
                runtimeBoxRoot.localPosition = boxRootOrigin + Vector3.right * offset;
            }

            yield return null;
        }

        if (runtimeShelfRoot != null)
        {
            runtimeShelfRoot.localPosition = shelfRootOrigin;
        }

        if (runtimeBoxRoot != null)
        {
            runtimeBoxRoot.localPosition = boxRootOrigin;
        }

        refreshShakeRoutine = null;
    }

    private void HandleRefreshShelfSelectionInput()
    {
        if (!waitingRefreshShelfSelection || IsLevelDesignMode)
        {
            return;
        }

        if (!TryReadPrimaryPointerDown(out var screenPos, out var pointerId))
        {
            return;
        }

        if (EventSystem.current != null)
        {
            var overUI = pointerId >= 0
                ? EventSystem.current.IsPointerOverGameObject(pointerId)
                : EventSystem.current.IsPointerOverGameObject();
            if (overUI)
            {
                return;
            }
        }

        if (!TryResolveShelfByScreenPosition(screenPos, out var shelf) || shelf == null)
        {
            return;
        }

        waitingRefreshShelfSelection = false;
        RefreshColorsWithGroupConstraint();
    }

    private bool TryUndoLastVisualMove()
    {
        while (visualMoveHistory.Count > 0)
        {
            var record = visualMoveHistory.Pop();
            if (record == null || record.movedBox == null)
            {
                continue;
            }

            if (!TryGetRuntimeShelfByIndex(record.fromShelfIndex, out var fromShelf)
                || !TryGetRuntimeShelfByIndex(record.toShelfIndex, out var toShelf)
                || fromShelf == null
                || toShelf == null
                || fromShelf.StackRoot == null
                || toShelf.StackRoot == null)
            {
                continue;
            }

            if (record.movedBox.parent != toShelf.StackRoot)
            {
                continue;
            }

            if (toShelf.StackRoot.childCount <= 0 || toShelf.StackRoot.GetChild(toShelf.StackRoot.childCount - 1) != record.movedBox)
            {
                continue;
            }

            if (fromShelf.BoxCount >= fromShelf.MaxBoxCount)
            {
                continue;
            }

            var targetBottom = fromShelf.transform.position;
            if (!fromShelf.TryGetPlacementBottomPosition(out targetBottom))
            {
                targetBottom = fromShelf.transform.position;
            }

            record.movedBox.SetParent(fromShelf.StackRoot, true);
            record.movedBox.rotation = fromShelf.transform.rotation;
            if (!TryAlignBoxAndGetTop(record.movedBox, targetBottom, out _))
            {
                record.movedBox.position = targetBottom;
            }

            record.movedBox.SetAsLastSibling();
            ApplySortingForShelfStack(fromShelf);
            ApplySortingForShelfStack(toShelf);
            fromShelf.RefreshBoxVisualStates();
            toShelf.RefreshBoxVisualStates();
            return true;
        }

        return false;
    }

    private bool TryAddEmptyShelf()
    {
        var attempt = ++addShelfAttemptSequence;

        if (shelfPrefab == null)
        {
            LogWarn("新增空货架失败：shelfPrefab 未配置。", this);
            return false;
        }

        EnsureShelfRoot();
        EnsureShelfSubRoots();

        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            LogWarn("新增空货架失败：Camera.main 为空。", this);
            return false;
        }

        var existingBeforeAdd = new List<Vector3>(spawnedShelves.Count);
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            if (spawnedShelves[i] != null)
            {
                existingBeforeAdd.Add(spawnedShelves[i].transform.position);
            }
        }

        var spawnPos = ResolveAdditionalShelfSpawnPosition(cameraRef, attempt, out var placementReport, out var canPlaceWithoutOverlap);
        var halfExtents = ResolveShelfHalfExtents();
        var selectedOverlaps = IsOverlapping(spawnPos, existingBeforeAdd, halfExtents, Mathf.Max(0f, addShelfMinGap));
        if (enableAddShelfPlacementReport)
        {
            LogInfo($"AddShelfPlacement | {FormatAddShelfPlacementReport(placementReport, spawnPos, selectedOverlaps)}");
        }

        if (!canPlaceWithoutOverlap)
        {
            if (enableAddShelfPlacementReport)
            {
                LogWarn($"AddShelfPlacementResult | attempt={attempt} | success=false | reason=no_non_overlap_slot | totalShelves={spawnedShelves.Count}", this);
            }

            return false;
        }

        var shelf = Instantiate(shelfPrefab, spawnPos, shelfPrefab.transform.rotation, runtimeShelfRoot);
        shelf.name = $"Shelf_{spawnedShelves.Count + 1}";
        spawnedShelves.Add(shelf);

        EnsureShelfInteractionForRuntime(shelf, ResolveNextShelfIndex());
        SpawnBoxesForShelf(shelf, null, 0, 0);

        EnsureColumnConfigState(spawnedShelves.Count, true);
        UpdateLevelIndicator();
        UpdateDesignToolsTexts();
        if (enableAddShelfPlacementReport)
        {
            LogInfo($"AddShelfPlacementResult | attempt={attempt} | success=true | shelf={shelf.name} | totalShelves={spawnedShelves.Count}", shelf);
        }

        return true;
    }

    private Vector3 ResolveAdditionalShelfSpawnPosition(Camera cameraRef, int attempt, out AddShelfPlacementReport report, out bool canPlaceWithoutOverlap)
    {
        canPlaceWithoutOverlap = false;
        report = new AddShelfPlacementReport
        {
            attempt = attempt,
            areaConfigured = additionalShelfPlacementArea != null,
            existingShelfCount = spawnedShelves.Count,
            bestMinDistance = float.MinValue,
            selectionPath = "none"
        };

        var halfExtents = ResolveShelfHalfExtents();
        report.halfExtents = halfExtents;
        if (!TryResolveScreenPlacementRect(cameraRef, halfExtents, out var screenRect))
        {
            report.usedScreenFallback = true;
            report.selectionPath = "screenRectUnavailable_centerFallback";
            return ViewportToWorldOnPlane(cameraRef, new Vector2(0.5f, 0.5f));
        }

        report.screenRect = screenRect;

        var placementRect = screenRect;
        if (report.areaConfigured)
        {
            if (!TryResolveAdditionalAreaRect(cameraRef, out var areaRect))
            {
                report.areaResolveFailed = true;
                report.selectionPath = "areaConfigured_but_unresolved";
                return new Vector3(screenRect.center.x, screenRect.center.y, spawnPlaneZ);
            }

            report.areaResolved = true;
            report.areaRect = areaRect;

            var ixMin = Mathf.Max(screenRect.xMin, areaRect.xMin);
            var ixMax = Mathf.Min(screenRect.xMax, areaRect.xMax);
            var iyMin = Mathf.Max(screenRect.yMin, areaRect.yMin);
            var iyMax = Mathf.Min(screenRect.yMax, areaRect.yMax);
            var intersection = Rect.MinMaxRect(ixMin, iyMin, ixMax, iyMax);

            if (intersection.width > 0.01f && intersection.height > 0.01f)
            {
                placementRect = intersection;
                report.areaIntersectionApplied = true;
            }
            else
            {
                report.areaIntersectionInvalid = true;
                report.selectionPath = "areaIntersectionInvalid";
                return new Vector3(screenRect.center.x, screenRect.center.y, spawnPlaneZ);
            }
        }

        report.placementRect = placementRect;
        report.requiredSpacingX = halfExtents.x * 2f + Mathf.Max(0f, addShelfMinGap);
        report.requiredSpacingY = halfExtents.y * 2f + Mathf.Max(0f, addShelfMinGap);
        if (report.requiredSpacingX > 0.01f && report.requiredSpacingY > 0.01f)
        {
            var capX = Mathf.FloorToInt(Mathf.Max(0f, placementRect.width) / report.requiredSpacingX);
            var capY = Mathf.FloorToInt(Mathf.Max(0f, placementRect.height) / report.requiredSpacingY);
            report.estimatedGridCapacity = Mathf.Max(0, capX * capY);
        }

        var existing = new List<Vector3>(spawnedShelves.Count);
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            if (spawnedShelves[i] != null)
            {
                existing.Add(spawnedShelves[i].transform.position);
            }
        }

        var candidates = new List<Vector3>();
        var autoCandidates = GenerateShelfPositions(cameraRef, Mathf.Max(1, spawnedShelves.Count + 6));
        for (var i = 0; i < autoCandidates.Count; i++)
        {
            candidates.Add(autoCandidates[i]);
        }

        var stepX = Mathf.Max(halfExtents.x * 2f + Mathf.Max(0f, addShelfMinGap), 0.2f);
        var stepY = Mathf.Max(halfExtents.y * 2f + Mathf.Max(0f, addShelfMinGap), 0.2f);
        for (var y = placementRect.yMax; y >= placementRect.yMin; y -= stepY)
        {
            for (var x = placementRect.xMin; x <= placementRect.xMax; x += stepX)
            {
                candidates.Add(new Vector3(x, y, spawnPlaneZ));
            }
        }

        report.candidateCount = candidates.Count;

        if (existing.Count == 0)
        {
            var candidate = ClampToRect(candidates[0], placementRect);
            report.acceptedCount++;
            canPlaceWithoutOverlap = true;
            report.selectionPath = "firstValidCandidate_noExistingShelves";
            return candidate;
        }

        Vector3 best = new Vector3(placementRect.center.x, placementRect.center.y, spawnPlaneZ);
        var bestMinDistance = float.MinValue;
        for (var i = 0; i < candidates.Count; i++)
        {
            var candidate = ClampToRect(candidates[i], placementRect);
            if (IsOverlapping(candidate, existing, halfExtents, Mathf.Max(0f, addShelfMinGap)))
            {
                report.rejectedByShelfOverlap++;
                continue;
            }

            var minDistance = float.MaxValue;
            for (var j = 0; j < existing.Count; j++)
            {
                var distance = Vector3.Distance(candidate, existing[j]);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            report.acceptedCount++;

            if (minDistance > bestMinDistance)
            {
                bestMinDistance = minDistance;
                best = candidate;
                report.bestUpdatedCount++;
            }
        }

        report.bestMinDistance = bestMinDistance;

        if (bestMinDistance > 0.01f)
        {
            canPlaceWithoutOverlap = true;
            report.selectionPath = "bestMaxMinDistance";
            return best;
        }

        report.selectionPath = "no_non_overlap_slot_in_range";
        return new Vector3(placementRect.center.x, placementRect.center.y, spawnPlaneZ);
    }

    private string FormatAddShelfPlacementReport(AddShelfPlacementReport report, Vector3 selectedPos, bool selectedOverlaps)
    {
        if (report == null)
        {
            return $"attempt={addShelfAttemptSequence} | report=null";
        }

        var areaTarget = report.areaConfigured
            ? (additionalShelfPlacementArea != null ? additionalShelfPlacementArea.name : "NULL")
            : "none";

        return
            $"attempt={report.attempt}"
            + $" | areaTarget={areaTarget}"
            + $" | areaResolved={report.areaResolved}"
            + $" | areaIntersectionApplied={report.areaIntersectionApplied}"
            + $" | areaIntersectionInvalid={report.areaIntersectionInvalid}"
            + $" | areaResolveFailed={report.areaResolveFailed}"
            + $" | usedScreenFallback={report.usedScreenFallback}"
            + $" | existing={report.existingShelfCount}"
            + $" | candidates={report.candidateCount}"
            + $" | accepted={report.acceptedCount}"
            + $" | rejectOverlap={report.rejectedByShelfOverlap}"
            + $" | rejectButton={report.rejectedByButtons}"
            + $" | bestUpdates={report.bestUpdatedCount}"
            + $" | bestMinDist={report.bestMinDistance:F3}"
            + $" | requiredSpacing=({report.requiredSpacingX:F2},{report.requiredSpacingY:F2})"
            + $" | estimatedCapacity={report.estimatedGridCapacity}"
            + $" | fallbackUsed={report.fallbackUsed}"
            + $" | fallbackOverlaps={report.fallbackOverlaps}"
            + $" | path={report.selectionPath}"
            + $" | halfExtents=({report.halfExtents.x:F2},{report.halfExtents.y:F2})"
            + $" | screenRect={FormatRectForLog(report.screenRect)}"
            + $" | areaRect={FormatRectForLog(report.areaRect)}"
            + $" | placementRect={FormatRectForLog(report.placementRect)}"
            + $" | selected=({selectedPos.x:F2},{selectedPos.y:F2},{selectedPos.z:F2})"
            + $" | selectedOverlaps={selectedOverlaps}";
    }

    private static string FormatRectForLog(Rect rect)
    {
        return $"[{rect.xMin:F2},{rect.yMin:F2}]-[{rect.xMax:F2},{rect.yMax:F2}]({rect.width:F2}x{rect.height:F2})";
    }

    private bool TryResolveScreenPlacementRect(Camera cameraRef, Vector2 halfExtents, out Rect rect)
    {
        rect = default;
        if (cameraRef == null)
        {
            return false;
        }

        var leftWorld = ViewportToWorldOnPlane(cameraRef, new Vector2(0f, 0.5f)).x;
        var rightWorld = ViewportToWorldOnPlane(cameraRef, new Vector2(1f, 0.5f)).x;
        var bottomWorld = ViewportToWorldOnPlane(cameraRef, new Vector2(0.5f, 0f)).y;
        var topWorld = ViewportToWorldOnPlane(cameraRef, new Vector2(0.5f, 1f)).y;

        var xMin = Mathf.Min(leftWorld, rightWorld) + halfExtents.x;
        var xMax = Mathf.Max(leftWorld, rightWorld) - halfExtents.x;
        var yMin = Mathf.Min(bottomWorld, topWorld) + halfExtents.y;
        var yMax = Mathf.Max(bottomWorld, topWorld) - halfExtents.y;

        rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        return rect.width > 0.01f && rect.height > 0.01f;
    }

    private bool TryResolveAdditionalShelfPlacementRect(Camera cameraRef, Vector2 halfExtents, out Rect rect)
    {
        rect = default;
        if (!TryResolveScreenPlacementRect(cameraRef, halfExtents, out var screenRect))
        {
            return false;
        }
        if (screenRect.width <= 0.01f || screenRect.height <= 0.01f)
        {
            return false;
        }

        if (!TryResolveAdditionalAreaRect(cameraRef, out var areaRect))
        {
            rect = screenRect;
            return true;
        }

        var ixMin = Mathf.Max(screenRect.xMin, areaRect.xMin);
        var ixMax = Mathf.Min(screenRect.xMax, areaRect.xMax);
        var iyMin = Mathf.Max(screenRect.yMin, areaRect.yMin);
        var iyMax = Mathf.Min(screenRect.yMax, areaRect.yMax);
        var intersection = Rect.MinMaxRect(ixMin, iyMin, ixMax, iyMax);

        if (intersection.width <= 0.01f || intersection.height <= 0.01f)
        {
            rect = screenRect;
            return true;
        }

        rect = intersection;
        return true;
    }

    private bool TryResolveAdditionalAreaRect(Camera cameraRef, out Rect rect)
    {
        rect = default;
        if (additionalShelfPlacementArea == null)
        {
            return false;
        }

        var uiRect = additionalShelfPlacementArea as RectTransform;
        var canvas = uiRect != null ? uiRect.GetComponentInParent<Canvas>() : null;
        if (uiRect != null && cameraRef != null && canvas != null)
        {
            var corners = new Vector3[4];
            uiRect.GetWorldCorners(corners);

            var canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;

            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;

            for (var i = 0; i < 4; i++)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(canvasCamera, corners[i]);
                var world = ScreenToWorldOnSpawnPlane(cameraRef, screen);
                minX = Mathf.Min(minX, world.x);
                minY = Mathf.Min(minY, world.y);
                maxX = Mathf.Max(maxX, world.x);
                maxY = Mathf.Max(maxY, world.y);
            }

            rect = Rect.MinMaxRect(minX, minY, maxX, maxY);
            return rect.width > 0.01f && rect.height > 0.01f;
        }

        if (uiRect != null && canvas == null)
        {
            var worldCenter = uiRect.position;
            var worldSize = new Vector2(
                Mathf.Abs(uiRect.rect.width * uiRect.lossyScale.x),
                Mathf.Abs(uiRect.rect.height * uiRect.lossyScale.y));

            if (worldSize.x <= 0.01f || worldSize.y <= 0.01f)
            {
                return false;
            }

            var worldHalfX = worldSize.x * 0.5f;
            var worldHalfY = worldSize.y * 0.5f;
            rect = Rect.MinMaxRect(worldCenter.x - worldHalfX, worldCenter.y - worldHalfY, worldCenter.x + worldHalfX, worldCenter.y + worldHalfY);
            return rect.width > 0.01f && rect.height > 0.01f;
        }

        var box2D = additionalShelfPlacementArea.GetComponent<BoxCollider2D>();
        if (box2D != null)
        {
            var b = box2D.bounds;
            rect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
            return rect.width > 0.01f && rect.height > 0.01f;
        }

        var box3D = additionalShelfPlacementArea.GetComponent<BoxCollider>();
        if (box3D != null)
        {
            var b = box3D.bounds;
            rect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
            return rect.width > 0.01f && rect.height > 0.01f;
        }

        var renderer = additionalShelfPlacementArea.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            var b = renderer.bounds;
            rect = Rect.MinMaxRect(b.min.x, b.min.y, b.max.x, b.max.y);
            return rect.width > 0.01f && rect.height > 0.01f;
        }

        return false;
    }

    private Vector3 ScreenToWorldOnSpawnPlane(Camera cameraRef, Vector2 screenPoint)
    {
        if (cameraRef == null)
        {
            return Vector3.zero;
        }

        var depth = Mathf.Abs(spawnPlaneZ - cameraRef.transform.position.z);
        if (depth < 0.01f)
        {
            depth = 10f;
        }

        var world = cameraRef.ScreenToWorldPoint(new Vector3(screenPoint.x, screenPoint.y, depth));
        world.z = spawnPlaneZ;
        return world;
    }

    private static Vector3 ClampToRect(Vector3 pos, Rect rect)
    {
        return new Vector3(
            Mathf.Clamp(pos.x, rect.xMin, rect.xMax),
            Mathf.Clamp(pos.y, rect.yMin, rect.yMax),
            pos.z);
    }

    private bool IsShelfCandidateOverlappingButtons(Camera cameraRef, Vector3 candidate, Vector2 halfExtents, bool avoidButtons)
    {
        if (!avoidButtons)
        {
            return false;
        }

        var buttonRects = CollectActiveButtonScreenRects();
        if (buttonRects.Count == 0)
        {
            return false;
        }

        var depth = Mathf.Abs(spawnPlaneZ - cameraRef.transform.position.z);
        if (depth < 0.01f)
        {
            depth = 10f;
        }

        var p1 = cameraRef.WorldToScreenPoint(new Vector3(candidate.x - halfExtents.x, candidate.y - halfExtents.y, cameraRef.transform.position.z + depth));
        var p2 = cameraRef.WorldToScreenPoint(new Vector3(candidate.x + halfExtents.x, candidate.y + halfExtents.y, cameraRef.transform.position.z + depth));
        var shelfRect = Rect.MinMaxRect(
            Mathf.Min(p1.x, p2.x),
            Mathf.Min(p1.y, p2.y),
            Mathf.Max(p1.x, p2.x),
            Mathf.Max(p1.y, p2.y));

        for (var i = 0; i < buttonRects.Count; i++)
        {
            if (shelfRect.Overlaps(buttonRects[i]))
            {
                return true;
            }
        }

        return false;
    }

    private List<Rect> CollectActiveButtonScreenRects()
    {
        var result = new List<Rect>();
        var buttons = FindObjectsOfType<Button>(true);
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                continue;
            }

            var rectTransform = button.transform as RectTransform;
            if (rectTransform == null)
            {
                continue;
            }

            var corners = new Vector3[4];
            rectTransform.GetWorldCorners(corners);
            var minX = float.MaxValue;
            var minY = float.MaxValue;
            var maxX = float.MinValue;
            var maxY = float.MinValue;
            for (var c = 0; c < 4; c++)
            {
                var s = RectTransformUtility.WorldToScreenPoint(null, corners[c]);
                minX = Mathf.Min(minX, s.x);
                minY = Mathf.Min(minY, s.y);
                maxX = Mathf.Max(maxX, s.x);
                maxY = Mathf.Max(maxY, s.y);
            }

            if (maxX - minX <= 1f || maxY - minY <= 1f)
            {
                continue;
            }

            result.Add(Rect.MinMaxRect(minX, minY, maxX, maxY));
        }

        return result;
    }

    private int ResolveNextShelfIndex()
    {
        var maxIndex = -1;
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var interaction = shelfGo.GetComponent<ShelfInteractionController>();
            if (interaction != null)
            {
                maxIndex = Mathf.Max(maxIndex, interaction.ShelfIndex);
            }
        }

        return maxIndex + 1;
    }

    private bool TryGetRuntimeShelfByIndex(int shelfIndex, out ShelfInteractionController shelf)
    {
        shelf = null;
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var interaction = shelfGo.GetComponent<ShelfInteractionController>();
            if (interaction != null && interaction.ShelfIndex == shelfIndex)
            {
                shelf = interaction;
                return true;
            }
        }

        return false;
    }

    private void ApplySortingForShelfStack(ShelfInteractionController shelf)
    {
        if (shelf == null || shelf.StackRoot == null)
        {
            return;
        }

        for (var i = 0; i < shelf.StackRoot.childCount; i++)
        {
            var box = shelf.StackRoot.GetChild(i);
            if (box != null)
            {
                ApplySortingForBox(box, i);
            }
        }
    }

    private void NormalizeConfiguredCounts()
    {
        configuredTotalShelfCount = Mathf.Max(1, configuredTotalShelfCount);
        configuredEmptyShelfCount = Mathf.Clamp(configuredEmptyShelfCount, 0, configuredTotalShelfCount - 1);
        configuredFilledShelfCount = Mathf.Max(1, configuredFilledShelfCount);
    }

    private int ResolveConfiguredTotalBoxCount(int targetShelfCount)
    {
        var shelfCount = Mathf.Max(1, targetShelfCount);
        var emptyCount = Mathf.Clamp(configuredEmptyShelfCount, 0, shelfCount - 1);
        var activeShelfCount = Mathf.Max(1, shelfCount - emptyCount);
        var filledShelves = Mathf.Clamp(configuredFilledShelfCount, 1, activeShelfCount);
        var totalBoxes = filledShelves * Mathf.Max(1, boxesPerShelf);
        return totalBoxes;
    }

    private List<int> BuildFallbackShelfBoxCounts(int targetCount)
    {
        var counts = new List<int>(targetCount);
        for (var i = 0; i < targetCount; i++)
        {
            counts.Add(0);
        }

        var emptyCount = Mathf.Clamp(configuredEmptyShelfCount, 0, Mathf.Max(0, targetCount - 1));
        var activeCount = targetCount - emptyCount;
        if (activeCount <= 0)
        {
            return counts;
        }

        var capacityPerShelf = Mathf.Max(1, boxesPerShelf);
        var maxTotal = activeCount * capacityPerShelf;
        var requestedTotalBoxes = ResolveConfiguredTotalBoxCount(targetCount);
        var totalBoxes = Mathf.Min(requestedTotalBoxes, maxTotal);
        if (totalBoxes <= 0)
        {
            return counts;
        }

        for (var i = 0; i < activeCount && i < counts.Count; i++)
        {
            counts[i] = 1;
            totalBoxes--;
        }

        var cursor = 0;
        while (totalBoxes > 0)
        {
            if (counts[cursor] < capacityPerShelf)
            {
                counts[cursor]++;
                totalBoxes--;
            }

            cursor = (cursor + 1) % activeCount;
        }

        return counts;
    }

    private void EnsureShelfRoot()
    {
        if (shelfRoot != null)
        {
            return;
        }

        var existing = transform.Find("ShelfRoot");
        if (existing != null)
        {
            shelfRoot = existing;
            return;
        }

        var root = new GameObject("ShelfRoot");
        root.transform.SetParent(transform, false);
        shelfRoot = root.transform;
    }

    private void EnsureShelfSubRoots()
    {
        if (shelfRoot == null)
        {
            return;
        }

        runtimeShelfRoot = shelfRoot.Find("RuntimeShelves");
        if (runtimeShelfRoot == null)
        {
            var runtime = new GameObject("RuntimeShelves");
            runtime.transform.SetParent(shelfRoot, false);
            runtimeShelfRoot = runtime.transform;
        }

        runtimeBoxRoot = shelfRoot.Find("RuntimeBoxes");
        if (runtimeBoxRoot == null)
        {
            var runtimeBoxes = new GameObject("RuntimeBoxes");
            runtimeBoxes.transform.SetParent(shelfRoot, false);
            runtimeBoxRoot = runtimeBoxes.transform;
        }

        runtimeTruckRoot = shelfRoot.Find("RuntimeTrucks");
        if (runtimeTruckRoot == null)
        {
            var runtimeTrucks = new GameObject("RuntimeTrucks");
            runtimeTrucks.transform.SetParent(shelfRoot, false);
            runtimeTruckRoot = runtimeTrucks.transform;
        }

        legacyShelfRoot = shelfRoot.Find("LegacyShelves");
        if (legacyShelfRoot == null)
        {
            var legacy = new GameObject("LegacyShelves");
            legacy.transform.SetParent(shelfRoot, false);
            legacyShelfRoot = legacy.transform;
        }
    }

    private void TryAutoBindShelfPrefab()
    {
        if (shelfPrefab != null)
        {
            return;
        }

        var candidatePaths = new[]
        {
            "Prefabs/Shelf",
            "Prefabs/shelf",
            "Shelf",
            "shelf"
        };

        for (var i = 0; i < candidatePaths.Length; i++)
        {
            var loaded = Resources.Load<GameObject>(candidatePaths[i]);
            if (loaded != null)
            {
                shelfPrefab = loaded;
                return;
            }
        }
    }

    private void TryAutoBindBoxPrefab()
    {
        if (boxPrefab != null)
        {
            return;
        }

        var candidatePaths = new[]
        {
            "Prefabs/Box",
            "Prefabs/box",
            "Box",
            "box"
        };

        for (var i = 0; i < candidatePaths.Length; i++)
        {
            var loaded = Resources.Load<GameObject>(candidatePaths[i]);
            if (loaded != null)
            {
                boxPrefab = loaded;
                LogInfo($"Auto bind boxPrefab success: {candidatePaths[i]}");
                return;
            }
        }

        LogWarn("Auto bind boxPrefab failed。请手动拖拽 boxPrefab 或放在 Resources/Prefabs/Box。", this);
    }

    private void TryAutoBindCarPrefab()
    {
        if (carPrefab != null)
        {
            return;
        }

        var candidatePaths = new[]
        {
            "Prefabs/Car",
            "Prefabs/car",
            "Car",
            "car"
        };

        for (var i = 0; i < candidatePaths.Length; i++)
        {
            var loaded = Resources.Load<GameObject>(candidatePaths[i]);
            if (loaded != null)
            {
                carPrefab = loaded;
                LogInfo($"Auto bind carPrefab success: {candidatePaths[i]}");
                return;
            }
        }
    }

    private void TryAutoBindCatPrefab()
    {
        if (catPrefab != null)
        {
            return;
        }

        var candidatePaths = new[]
        {
            "Prefabs/Cat",
            "Prefabs/cat",
            "Cat",
            "cat"
        };

        for (var i = 0; i < candidatePaths.Length; i++)
        {
            var loaded = Resources.Load<GameObject>(candidatePaths[i]);
            if (loaded != null)
            {
                catPrefab = loaded;
                LogInfo($"Auto bind catPrefab success: {candidatePaths[i]}");
                return;
            }
        }
    }

    private void MigrateLegacyShelves()
    {
        if (legacyShelfRoot == null || shelfRoot == null)
        {
            return;
        }

        var keyword = string.IsNullOrWhiteSpace(legacyShelfNameKeyword) ? "shelf" : legacyShelfNameKeyword.Trim();
        var keywordLower = keyword.ToLowerInvariant();
        var allTransforms = FindObjectsOfType<Transform>(true);

        for (var i = 0; i < allTransforms.Length; i++)
        {
            var current = allTransforms[i];
            if (current == null)
            {
                continue;
            }

            if (current == transform || current == shelfRoot || current == runtimeShelfRoot || current == legacyShelfRoot)
            {
                continue;
            }

            if (current.IsChildOf(shelfRoot))
            {
                continue;
            }

            if (current.gameObject == shelfPrefab)
            {
                continue;
            }

            if (!current.name.ToLowerInvariant().Contains(keywordLower))
            {
                continue;
            }

            if (!current.GetComponentInChildren<Renderer>(true) && !current.GetComponentInChildren<SpriteRenderer>(true))
            {
                continue;
            }

            if (destroyLegacyShelvesAfterMigration)
            {
                if (Application.isPlaying)
                {
                    Destroy(current.gameObject);
                }
                else
                {
                    DestroyImmediate(current.gameObject);
                }

                continue;
            }

            current.SetParent(legacyShelfRoot, true);
            if (disableLegacyShelvesAfterMigration)
            {
                current.gameObject.SetActive(false);
            }
        }
    }

    private void ClearSpawnedShelves()
    {
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            if (spawnedShelves[i] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                spawnedShelves[i].transform.SetParent(null, true);
            }

            if (Application.isPlaying)
            {
                Destroy(spawnedShelves[i]);
            }
            else
            {
                DestroyImmediate(spawnedShelves[i]);
            }
        }

        spawnedShelves.Clear();

        if (runtimeShelfRoot == null)
        {
            return;
        }

        var children = new List<Transform>();
        for (var i = 0; i < runtimeShelfRoot.childCount; i++)
        {
            children.Add(runtimeShelfRoot.GetChild(i));
        }

        for (var i = 0; i < children.Count; i++)
        {
            if (Application.isPlaying)
            {
                children[i].SetParent(null, true);
            }

            if (Application.isPlaying)
            {
                Destroy(children[i].gameObject);
            }
            else
            {
                DestroyImmediate(children[i].gameObject);
            }
        }

        if (runtimeBoxRoot == null)
        {
            return;
        }

        children.Clear();
        for (var i = 0; i < runtimeBoxRoot.childCount; i++)
        {
            children.Add(runtimeBoxRoot.GetChild(i));
        }

        for (var i = 0; i < children.Count; i++)
        {
            if (Application.isPlaying)
            {
                children[i].SetParent(null, true);
            }

            if (Application.isPlaying)
            {
                Destroy(children[i].gameObject);
            }
            else
            {
                DestroyImmediate(children[i].gameObject);
            }
        }

        activeTrucks.Clear();
        truckEliminationRunning = false;
        if (catIntroRoutine != null)
        {
            StopCoroutine(catIntroRoutine);
            catIntroRoutine = null;
        }

        if (catHintRoutine != null)
        {
            StopCoroutine(catHintRoutine);
            catHintRoutine = null;
        }

        StopCatIntroVisual();
        HideHintMessage();
        HideWinMessage();

        if (runtimeTruckRoot == null)
        {
            return;
        }

        children.Clear();
        for (var i = 0; i < runtimeTruckRoot.childCount; i++)
        {
            children.Add(runtimeTruckRoot.GetChild(i));
        }

        for (var i = 0; i < children.Count; i++)
        {
            if (Application.isPlaying)
            {
                children[i].SetParent(null, true);
            }

            if (Application.isPlaying)
            {
                Destroy(children[i].gameObject);
            }
            else
            {
                DestroyImmediate(children[i].gameObject);
            }
        }
    }

    private void SpawnTrucks()
    {
        activeTrucks.Clear();

        if (carPrefab == null || runtimeTruckRoot == null)
        {
            if (carPrefab == null)
            {
                LogWarn("Skip truck spawn: carPrefab is NULL。请在 ShelfSpawnManager 绑定 car 预制体。", this);
            }

            return;
        }

        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            LogWarn("Skip truck spawn: Camera.main is NULL。", this);
            return;
        }

        var totalDemand = BuildRemainingDemandByColor(true);
        var demandCount = totalDemand.Values.Sum();
        var truckCount = Mathf.Clamp(demandCount, 0, Mathf.Max(1, maxTruckCount));
        LogInfo($"Spawn trucks demand summary | demandTrips={FormatTruckColorCountMap(totalDemand)} | demandCount={demandCount} | maxTruckCount={Mathf.Max(1, maxTruckCount)}");
        LogInfo($"Spawn trucks box snapshot | detail={BuildTruckDemandDetailedSnapshot()}");
        if (demandCount > Mathf.Max(1, maxTruckCount))
        {
            LogWarn($"Spawn trucks demand exceeds capacity | demandCount={demandCount} | maxTruckCount={Mathf.Max(1, maxTruckCount)} | some colors may be deferred", this);
        }
        if (truckCount <= 0)
        {
            LogWarn($"Spawn trucks skipped: remaining demand is 0 | shelfSnapshot={BuildTruckDemandShelfSnapshot()}", this);
            return;
        }

        var colors = BuildTruckColors(truckCount, null, totalDemand);
        truckCount = Mathf.Min(truckCount, colors.Count);
        if (truckCount <= 0)
        {
            LogWarn($"Spawn trucks skipped: BuildTruckColors returned empty | demandTrips={FormatTruckColorCountMap(totalDemand)}", this);
            return;
        }

        LogInfo($"Spawn trucks color plan | count={truckCount} | colors=[{string.Join(",", colors)}]");

        for (var i = 0; i < truckCount; i++)
        {
            var spawnPos = GetTruckLaneWorldPosition(cameraRef, i, truckCount);
            var truck = Instantiate(carPrefab, spawnPos, carPrefab.transform.rotation, runtimeTruckRoot);
            truck.name = $"Truck_{i + 1}";

            if (!TryResolveTruckBottomAnchor(truck.transform, out var bottomAnchor))
            {
                bottomAnchor = truck.transform;
                LogWarn($"Truck {truck.name} 未找到 BottomAnchor，改用车体根节点。", truck);
            }

            var colorType = colors[i];
            ApplyColorForTruck(truck.transform, colorType);
            activeTrucks.Add(new TruckRuntimeData
            {
                truck = truck,
                bottomAnchor = bottomAnchor,
                color = colorType,
                busy = false
            });
        }

        LogInfo($"Spawn trucks finished | count={activeTrucks.Count} | trucks=[{FormatActiveTruckColorList()}]");
        LogInfo(
            $"TruckQuickSummary | level={Mathf.Max(1, currentLevelIndex)}"
            + $" | demandTrips={FormatTruckColorCountMap(totalDemand)}"
            + $" | planned=[{string.Join(",", colors)}]"
            + $" | active=[{FormatActiveTruckColorList()}]");
    }

    private List<GameManager.BoxColor> BuildTruckColors(
        int desiredCount,
        Dictionary<GameManager.BoxColor, int> existingSupply,
        Dictionary<GameManager.BoxColor, int> demandOverride = null)
    {
        var targetCount = Mathf.Max(0, desiredCount);
        var result = new List<GameManager.BoxColor>(targetCount);
        if (targetCount <= 0)
        {
            return result;
        }

        var colorPool = BuildAvailableTruckColorPool();
        if (colorPool.Count == 0)
        {
            return result;
        }

        var demand = demandOverride ?? BuildPendingEliminationDemand();
        var supply = new Dictionary<GameManager.BoxColor, int>();
        if (existingSupply != null)
        {
            foreach (var pair in existingSupply)
            {
                supply[pair.Key] = Mathf.Max(0, pair.Value);
            }
        }

        var rng = CreateTruckRandom();
        for (var i = 0; i < targetCount; i++)
        {
            var picked = PickNextTruckColor(colorPool, demand, supply, rng);
            result.Add(picked);
            if (supply.ContainsKey(picked))
            {
                supply[picked]++;
            }
            else
            {
                supply[picked] = 1;
            }

            var remainDeficit = FormatTruckColorDeficitMap(demand, supply);
            LogInfo($"BuildTruckColors pick | index={i} | picked={picked} | demand={FormatTruckColorCountMap(demand)} | supply={FormatTruckColorCountMap(supply)} | remainDeficit={remainDeficit}");
        }

        return result;
    }

    private Dictionary<GameManager.BoxColor, int> BuildRemainingDemandByColor(bool logDetails = false)
    {
        var countsByColor = new Dictionary<GameManager.BoxColor, int>();
        var scannedShelfCount = 0;
        var totalBoxes = 0;
        var grayBoxes = 0;
        var missingStateBoxes = 0;

        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                continue;
            }

            scannedShelfCount++;

            for (var b = 0; b < shelf.StackRoot.childCount; b++)
            {
                var box = shelf.StackRoot.GetChild(b);
                if (box == null)
                {
                    continue;
                }

                var state = box.GetComponent<BoxVisualState>();
                if (state == null)
                {
                    missingStateBoxes++;
                    continue;
                }

                totalBoxes++;
                if (state.IsGrayed)
                {
                    grayBoxes++;
                }

                if (countsByColor.ContainsKey(state.OriginalColorType))
                {
                    countsByColor[state.OriginalColorType]++;
                }
                else
                {
                    countsByColor[state.OriginalColorType] = 1;
                }
            }
        }

        var demand = new Dictionary<GameManager.BoxColor, int>();
        foreach (var pair in countsByColor)
        {
            var trips = pair.Value / 4;
            if (trips > 0)
            {
                demand[pair.Key] = trips;
            }
        }

        if (logDetails)
        {
            LogInfo(
                $"TruckDemandAnalyze | shelves={scannedShelfCount}/{spawnedShelves.Count}"
                + $" | boxes={totalBoxes}"
                + $" | grayBoxes={grayBoxes}"
                + $" | missingStateBoxes={missingStateBoxes}"
                + $" | colorBoxCounts={FormatTruckColorCountMap(countsByColor)}"
                + $" | demandTrips={FormatTruckColorCountMap(demand)}");
        }

        return demand;
    }

    private static string FormatTruckColorCountMap(Dictionary<GameManager.BoxColor, int> data)
    {
        if (data == null || data.Count == 0)
        {
            return "none";
        }

        return string.Join(",", data.OrderBy(pair => pair.Key.ToString()).Select(pair => $"{pair.Key}:{pair.Value}"));
    }

    private string BuildTruckDemandShelfSnapshot()
    {
        var parts = new List<string>();
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                parts.Add($"#{i}:null");
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                parts.Add($"#{i}:{shelfGo.name}|no-stack");
                continue;
            }

            var colorTags = new List<string>();
            for (var b = 0; b < shelf.StackRoot.childCount; b++)
            {
                var box = shelf.StackRoot.GetChild(b);
                if (box == null)
                {
                    colorTags.Add("null-box");
                    continue;
                }

                var state = box.GetComponent<BoxVisualState>();
                if (state == null)
                {
                    colorTags.Add("missing-state");
                    continue;
                }

                colorTags.Add(state.IsGrayed ? $"{state.OriginalColorType}*" : state.OriginalColorType.ToString());
            }

            parts.Add($"#{i}:{shelfGo.name}|count={shelf.StackRoot.childCount}|[{string.Join(",", colorTags)}]");
        }

        return parts.Count > 0 ? string.Join(" ; ", parts) : "no-shelves";
    }

    private string BuildTruckDemandDetailedSnapshot()
    {
        var parts = new List<string>();
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                parts.Add($"#{i}:null");
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                parts.Add($"#{i}:{shelfGo.name}|no-stack");
                continue;
            }

            var boxTags = new List<string>();
            for (var b = 0; b < shelf.StackRoot.childCount; b++)
            {
                var box = shelf.StackRoot.GetChild(b);
                if (box == null)
                {
                    boxTags.Add($"{b}:null-box");
                    continue;
                }

                var state = box.GetComponent<BoxVisualState>();
                if (state == null)
                {
                    boxTags.Add($"{b}:missing-state");
                    continue;
                }

                boxTags.Add(
                    $"{b}:origType={state.OriginalColorType}"
                    + $"|gray={state.IsGrayed}"
                    + $"|origColor={FormatColorForLog(state.OriginalDisplayColor)}"
                    + $"|currentColor={FormatColorForLog(state.CurrentDisplayColor)}");
            }

            parts.Add($"#{i}:{shelfGo.name}|boxes=[{string.Join(",", boxTags)}]");
        }

        return parts.Count > 0 ? string.Join(" ; ", parts) : "no-shelves";
    }

    private string FormatActiveTruckColorList()
    {
        if (activeTrucks == null || activeTrucks.Count <= 0)
        {
            return "none";
        }

        var items = new List<string>();
        for (var i = 0; i < activeTrucks.Count; i++)
        {
            var truck = activeTrucks[i];
            if (truck == null)
            {
                items.Add($"#{i}:null");
                continue;
            }

            var name = truck.truck != null ? truck.truck.name : "nullGo";
            items.Add($"#{i}:{name}|color={truck.color}|busy={truck.busy}");
        }

        return string.Join(";", items);
    }

    private static string FormatTruckColorDeficitMap(Dictionary<GameManager.BoxColor, int> demand, Dictionary<GameManager.BoxColor, int> supply)
    {
        if (demand == null || demand.Count == 0)
        {
            return "none";
        }

        var parts = new List<string>();
        foreach (var pair in demand.OrderBy(p => p.Key.ToString()))
        {
            var supplyCount = supply != null && supply.TryGetValue(pair.Key, out var s) ? s : 0;
            var deficit = Mathf.Max(0, pair.Value - supplyCount);
            parts.Add($"{pair.Key}:{deficit}");
        }

        return string.Join(",", parts);
    }

    private static string FormatColorForLog(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
    }

    private Vector3 GetTruckLaneWorldPosition(Camera cameraRef, int laneIndex, int slotCount)
    {
        var safeSlotCount = Mathf.Max(1, slotCount);
        var left = Mathf.Clamp01(truckHorizontalPadding);
        var right = Mathf.Clamp01(1f - truckHorizontalPadding);
        if (right <= left)
        {
            left = 0.08f;
            right = 0.92f;
        }

        var t = safeSlotCount <= 1 ? 0.5f : Mathf.Clamp01((float)laneIndex / (safeSlotCount - 1));
        var x = Mathf.Lerp(left, right, t);
        return ViewportToWorldOnPlane(cameraRef, new Vector2(x, Mathf.Clamp01(truckViewportY)));
    }

    private List<GameManager.BoxColor> BuildAvailableTruckColorPool()
    {
        var colorPool = new List<GameManager.BoxColor>();
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                continue;
            }

            for (var b = 0; b < shelf.StackRoot.childCount; b++)
            {
                var box = shelf.StackRoot.GetChild(b);
                if (box == null)
                {
                    continue;
                }

                var state = box.GetComponent<BoxVisualState>();
                if (state == null)
                {
                    continue;
                }

                colorPool.Add(state.OriginalColorType);
            }
        }

        if (boxGenerationManager != null)
        {
            colorPool.AddRange(boxGenerationManager.GetActiveColorTypes());
        }

        if (colorPool.Count == 0)
        {
            colorPool.AddRange((GameManager.BoxColor[])Enum.GetValues(typeof(GameManager.BoxColor)));
        }

        return colorPool.Distinct().ToList();
    }

    private Dictionary<GameManager.BoxColor, int> BuildPendingEliminationDemand()
    {
        var demand = new Dictionary<GameManager.BoxColor, int>();
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var shelfInteraction = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelfInteraction == null || shelfInteraction.StackRoot == null || shelfInteraction.StackRoot.childCount != 4)
            {
                continue;
            }

            if (!TryGetUniformShelfColor(shelfInteraction.StackRoot, out var color, out _))
            {
                continue;
            }

            if (demand.ContainsKey(color))
            {
                demand[color]++;
            }
            else
            {
                demand[color] = 1;
            }
        }

        return demand;
    }

    private static GameManager.BoxColor PickNextTruckColor(
        List<GameManager.BoxColor> colorPool,
        Dictionary<GameManager.BoxColor, int> demand,
        Dictionary<GameManager.BoxColor, int> supply,
        System.Random rng)
    {
        var deficitCandidates = new List<(GameManager.BoxColor color, int deficit, int demandCount)>();
        for (var i = 0; i < colorPool.Count; i++)
        {
            var color = colorPool[i];
            var demandCount = demand != null && demand.TryGetValue(color, out var d) ? d : 0;
            var supplyCount = supply != null && supply.TryGetValue(color, out var s) ? s : 0;
            var deficit = demandCount - supplyCount;
            if (deficit > 0)
            {
                deficitCandidates.Add((color, deficit, demandCount));
            }
        }

        if (deficitCandidates.Count > 0)
        {
            var picked = deficitCandidates
                .OrderByDescending(item => item.deficit)
                .ThenByDescending(item => item.demandCount)
                .ThenBy(item => item.color.ToString())
                .First();
            return picked.color;
        }

        return colorPool[rng.Next(colorPool.Count)];
    }

    private System.Random CreateTruckRandom()
    {
        var seed = randomizeOnEachRefresh
            ? unchecked((int)DateTime.UtcNow.Ticks) ^ spawnSeed ^ (refreshSequence * 977) ^ activeTrucks.Count
            : spawnSeed ^ (refreshSequence * 977) ^ activeTrucks.Count;
        return new System.Random(seed);
    }

    private bool TryResolveTruckBottomAnchor(Transform truckRoot, out Transform bottomAnchor)
    {
        bottomAnchor = null;
        var anchorComp = truckRoot.GetComponentInChildren<StackAnchorPoints>(true);
        if (anchorComp != null && anchorComp.BottomAnchor != null)
        {
            bottomAnchor = anchorComp.BottomAnchor;
            return true;
        }

        bottomAnchor = FindAnchorByName(truckRoot, "bottomanchor", "anchorbottom", "bottom");
        return bottomAnchor != null;
    }

    private void ApplyColorForTruck(Transform truckTransform, GameManager.BoxColor colorType)
    {
        var originalColor = ResolveDefaultDisplayColor(colorType);
        if (boxGenerationManager != null)
        {
            boxGenerationManager.TryGetDisplayColor(colorType, out originalColor);
        }

        var spriteRenderers = truckTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].color = originalColor;
        }

        var renderers = truckTransform.GetComponentsInChildren<Renderer>(true);
        var propertyBlock = new MaterialPropertyBlock();
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is SpriteRenderer)
            {
                continue;
            }

            renderers[i].GetPropertyBlock(propertyBlock);
            var hasBaseColor = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_BaseColor");
            var hasColor = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_Color");
            if (hasBaseColor)
            {
                propertyBlock.SetColor("_BaseColor", originalColor);
            }

            if (hasColor)
            {
                propertyBlock.SetColor("_Color", originalColor);
            }

            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }

    private IEnumerator ProcessTruckEliminationsRoutine()
    {
        truckEliminationRunning = true;
        var eliminatedCount = 0;

        while (!gameWon && TryFindTruckEliminationCandidate(out var shelf, out var truck, out var orderedBoxes, out var uniformColor))
        {
            truck.busy = true;
            eliminatedCount++;
            LogInfo($"Truck elimination start | shelf={shelf.name} | color={uniformColor} | truck={truck.truck.name}");
            yield return StartCoroutine(PlayTruckEliminationRoutine(shelf, truck, orderedBoxes));
        }

        if (!gameWon)
        {
            LogEliminationSnapshot($"scan-end eliminated={eliminatedCount}");
        }

        truckEliminationRunning = false;
    }

    private void LogEliminationSnapshot(string reason)
    {
        if (!enableEliminationSnapshotLog || (!enableDebugLog && !enableDebugFileLog))
        {
            return;
        }

        if (Time.unscaledTime < nextEliminationSnapshotLogTime)
        {
            return;
        }

        nextEliminationSnapshotLogTime = Time.unscaledTime + Mathf.Max(0f, eliminationSnapshotLogCooldown);

        var truckStates = new List<string>();
        for (var i = 0; i < activeTrucks.Count; i++)
        {
            var truck = activeTrucks[i];
            if (truck == null)
            {
                truckStates.Add($"#{i}:null");
                continue;
            }

            var truckName = truck.truck != null ? truck.truck.name : "nullGo";
            truckStates.Add($"#{i}:{truckName}|{truck.color}|busy={truck.busy}");
        }

        LogInfo($"EliminationSnapshot start | reason={reason} | shelves={spawnedShelves.Count} | trucks=[{string.Join(" ; ", truckStates)}]");

        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                LogInfo($"EliminationSnapshot shelf#{i} | state=null");
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                LogInfo($"EliminationSnapshot shelf#{i}:{shelfGo.name} | state=no-shelf-or-stack");
                continue;
            }

            var stackRoot = shelf.StackRoot;
            var childCount = stackRoot.childCount;
            if (childCount != 4)
            {
                LogInfo($"EliminationSnapshot shelf#{i}:{shelfGo.name} | boxes={childCount} | state=not-full");
                continue;
            }

            var colors = new List<string>();
            var hasGray = false;
            var hasMissingState = false;
            GameManager.BoxColor? firstColor = null;
            var uniform = true;

            for (var b = 0; b < childCount; b++)
            {
                var box = stackRoot.GetChild(b);
                var state = box != null ? box.GetComponent<BoxVisualState>() : null;
                if (state == null)
                {
                    hasMissingState = true;
                    colors.Add($"{b}:missing-state");
                    uniform = false;
                    continue;
                }

                colors.Add($"{b}:{state.OriginalColorType}|gray={state.IsGrayed}");
                if (state.IsGrayed)
                {
                    hasGray = true;
                    uniform = false;
                }

                if (!firstColor.HasValue)
                {
                    firstColor = state.OriginalColorType;
                }
                else if (state.OriginalColorType != firstColor.Value)
                {
                    uniform = false;
                }
            }

            if (hasMissingState)
            {
                LogInfo($"EliminationSnapshot shelf#{i}:{shelfGo.name} | boxes=[{string.Join(", ", colors)}] | state=missing-box-state");
                continue;
            }

            if (hasGray)
            {
                LogInfo($"EliminationSnapshot shelf#{i}:{shelfGo.name} | boxes=[{string.Join(", ", colors)}] | state=has-gray");
                continue;
            }

            if (!uniform || !firstColor.HasValue)
            {
                LogInfo($"EliminationSnapshot shelf#{i}:{shelfGo.name} | boxes=[{string.Join(", ", colors)}] | state=non-uniform");
                continue;
            }

            var matchedTruck = activeTrucks.FirstOrDefault(t => t != null && !t.busy && t.truck != null && t.color == firstColor.Value);
            if (matchedTruck == null)
            {
                LogInfo($"EliminationSnapshot shelf#{i}:{shelfGo.name} | color={firstColor.Value} | boxes=[{string.Join(", ", colors)}] | state=no-idle-truck");
                continue;
            }

            LogInfo($"EliminationSnapshot shelf#{i}:{shelfGo.name} | color={firstColor.Value} | boxes=[{string.Join(", ", colors)}] | state=ready | truck={matchedTruck.truck.name}");
        }
    }

    private bool TryFindTruckEliminationCandidate(
        out ShelfInteractionController shelf,
        out TruckRuntimeData truck,
        out List<Transform> orderedBoxes,
        out GameManager.BoxColor uniformColor)
    {
        shelf = null;
        truck = null;
        orderedBoxes = null;
        uniformColor = GameManager.BoxColor.Red;

        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var shelfInteraction = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelfInteraction == null || shelfInteraction.StackRoot == null || shelfInteraction.StackRoot.childCount != 4)
            {
                continue;
            }

            if (!TryGetUniformShelfColor(shelfInteraction.StackRoot, out uniformColor, out orderedBoxes))
            {
                continue;
            }

            for (var t = 0; t < activeTrucks.Count; t++)
            {
                var candidateTruck = activeTrucks[t];
                if (candidateTruck == null || candidateTruck.busy || candidateTruck.truck == null)
                {
                    continue;
                }

                if (candidateTruck.color != uniformColor)
                {
                    continue;
                }

                shelf = shelfInteraction;
                truck = candidateTruck;
                return true;
            }
        }

        return false;
    }

    private static bool TryGetUniformShelfColor(Transform stackRoot, out GameManager.BoxColor color, out List<Transform> orderedBoxes)
    {
        color = GameManager.BoxColor.Red;
        orderedBoxes = new List<Transform>();
        if (stackRoot == null || stackRoot.childCount != 4)
        {
            return false;
        }

        for (var i = 0; i < stackRoot.childCount; i++)
        {
            var box = stackRoot.GetChild(i);
            if (box == null)
            {
                return false;
            }

            var state = box.GetComponent<BoxVisualState>();
            if (state == null)
            {
                return false;
            }

            if (state.IsGrayed)
            {
                return false;
            }

            if (i == 0)
            {
                color = state.OriginalColorType;
            }
            else if (state.OriginalColorType != color)
            {
                return false;
            }

            orderedBoxes.Add(box);
        }

        return true;
    }

    private IEnumerator PlayTruckEliminationRoutine(ShelfInteractionController shelf, TruckRuntimeData truck, List<Transform> orderedBoxes)
    {
        if (shelf == null || truck == null || truck.truck == null || orderedBoxes == null || orderedBoxes.Count == 0)
        {
            if (truck != null)
            {
                truck.busy = false;
            }

            yield break;
        }

        var currentBoxCount = CountRuntimeBoxesOnBoard();
        var catMovedToTruck = ShouldFindCatForCurrentElimination(currentBoxCount, orderedBoxes.Count);

        var stackRoot = shelf.StackRoot;
        for (var i = 0; i < orderedBoxes.Count; i++)
        {
            var box = orderedBoxes[i];
            if (box == null)
            {
                continue;
            }

            var interaction = box.GetComponent<BoxInteractionController>();
            if (interaction != null)
            {
                interaction.enabled = false;
            }

            var collider2D = box.GetComponent<Collider2D>();
            if (collider2D != null)
            {
                collider2D.enabled = false;
            }

            if (stackRoot != null && box.parent == stackRoot)
            {
                box.SetParent(runtimeBoxRoot != null ? runtimeBoxRoot : transform, true);
            }
        }

        if (shelf.gameObject != null)
        {
            spawnedShelves.Remove(shelf.gameObject);
            Destroy(shelf.gameObject);
        }

        var nextBottom = truck.bottomAnchor != null ? truck.bottomAnchor.position : truck.truck.transform.position;
        for (var i = 0; i < orderedBoxes.Count; i++)
        {
            var box = orderedBoxes[i];
            if (box == null)
            {
                continue;
            }

            var startPos = box.position;
            var startRot = box.rotation;
            var targetPos = nextBottom;
            var targetRot = truck.truck.transform.rotation;

            if (TryAlignBoxAndGetTop(box, nextBottom, out var snappedTop))
            {
                targetPos = box.position;
                nextBottom = snappedTop;
            }

            box.position = startPos;
            box.rotation = startRot;

            var elapsed = 0f;
            var duration = Mathf.Max(0.01f, boxToTruckMoveDuration);
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var p = Mathf.Clamp01(elapsed / duration);
                box.position = Vector3.Lerp(startPos, targetPos, p);
                box.rotation = Quaternion.Slerp(startRot, targetRot, p);
                yield return null;
            }

            box.position = targetPos;
            box.rotation = targetRot;
            box.SetParent(truck.truck.transform, true);
        }

        if (catMovedToTruck)
        {
            truck.busy = false;
            TriggerGameWin();
            yield break;
        }

        var cameraRef = Camera.main;
        var truckStart = truck.truck.transform.position;
        var truckTarget = truckStart + Vector3.left * 20f;
        if (cameraRef != null)
        {
            var viewportTarget = ViewportToWorldOnPlane(cameraRef, new Vector2(-0.3f, cameraRef.WorldToViewportPoint(truckStart).y));
            truckTarget = new Vector3(viewportTarget.x, truckStart.y, truckStart.z);
        }

        var travelDistance = Mathf.Abs(truckTarget.x - truckStart.x);
        var moveDuration = Mathf.Max(0.01f, travelDistance / Mathf.Max(0.1f, truckExitSpeed));
        var moveTime = 0f;
        while (moveTime < moveDuration)
        {
            moveTime += Time.deltaTime;
            var p = Mathf.Clamp01(moveTime / moveDuration);
            truck.truck.transform.position = Vector3.Lerp(truckStart, truckTarget, p);
            yield return null;
        }

        truck.truck.transform.position = truckTarget;

        for (var i = 0; i < orderedBoxes.Count; i++)
        {
            if (orderedBoxes[i] != null)
            {
                Destroy(orderedBoxes[i].gameObject);
            }
        }

        if (truck.truck != null)
        {
            Destroy(truck.truck);
        }

        var removedIndex = activeTrucks.IndexOf(truck);
        activeTrucks.Remove(truck);

        yield return StartCoroutine(ShiftTrucksForwardRoutine(removedIndex));

        if (ShouldSpawnReplacementTruck())
        {
            SpawnReplacementTruck();
        }

        RenameActiveTrucks();
    }

    private IEnumerator ShiftTrucksForwardRoutine(int startIndex)
    {
        if (activeTrucks.Count == 0)
        {
            yield break;
        }

        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            yield break;
        }

        var fromPositions = new List<Vector3>(activeTrucks.Count);
        var targetPositions = new List<Vector3>(activeTrucks.Count);
        for (var i = 0; i < activeTrucks.Count; i++)
        {
            var truckData = activeTrucks[i];
            if (truckData == null || truckData.truck == null)
            {
                fromPositions.Add(Vector3.zero);
                targetPositions.Add(Vector3.zero);
                continue;
            }

            fromPositions.Add(truckData.truck.transform.position);
            targetPositions.Add(GetTruckLaneWorldPosition(cameraRef, i, Mathf.Max(1, maxTruckCount)));
        }

        var shouldAnimate = false;
        var safeStart = Mathf.Max(0, startIndex);
        for (var i = safeStart; i < activeTrucks.Count; i++)
        {
            if ((fromPositions[i] - targetPositions[i]).sqrMagnitude > 0.0001f)
            {
                shouldAnimate = true;
                break;
            }
        }

        if (!shouldAnimate)
        {
            yield break;
        }

        var elapsed = 0f;
        var duration = Mathf.Max(0.01f, truckShiftDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var p = Mathf.Clamp01(elapsed / duration);
            for (var i = safeStart; i < activeTrucks.Count; i++)
            {
                var truckData = activeTrucks[i];
                if (truckData == null || truckData.truck == null)
                {
                    continue;
                }

                truckData.truck.transform.position = Vector3.Lerp(fromPositions[i], targetPositions[i], p);
            }

            yield return null;
        }

        for (var i = safeStart; i < activeTrucks.Count; i++)
        {
            var truckData = activeTrucks[i];
            if (truckData == null || truckData.truck == null)
            {
                continue;
            }

            truckData.truck.transform.position = targetPositions[i];
        }
    }

    private bool ShouldSpawnReplacementTruck()
    {
        if (activeTrucks.Count >= Mathf.Max(1, maxTruckCount))
        {
            return false;
        }

        var remainingDemand = BuildRemainingDemandByColor();
        if (remainingDemand.Count == 0)
        {
            return false;
        }

        return !CanCurrentTrucksCoverDemand(remainingDemand);
    }

    private bool CanCurrentTrucksCoverDemand(Dictionary<GameManager.BoxColor, int> demand)
    {
        if (demand == null || demand.Count == 0)
        {
            return true;
        }

        var supply = new Dictionary<GameManager.BoxColor, int>();
        for (var i = 0; i < activeTrucks.Count; i++)
        {
            var truck = activeTrucks[i];
            if (truck == null || truck.truck == null)
            {
                continue;
            }

            if (supply.ContainsKey(truck.color))
            {
                supply[truck.color]++;
            }
            else
            {
                supply[truck.color] = 1;
            }
        }

        foreach (var pair in demand)
        {
            var have = supply.TryGetValue(pair.Key, out var count) ? count : 0;
            if (have < pair.Value)
            {
                return false;
            }
        }

        return true;
    }

    private void SpawnReplacementTruck()
    {
        if (carPrefab == null || runtimeTruckRoot == null)
        {
            return;
        }

        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            return;
        }

        var existingSupply = new Dictionary<GameManager.BoxColor, int>();
        for (var i = 0; i < activeTrucks.Count; i++)
        {
            var truck = activeTrucks[i];
            if (truck == null || truck.truck == null)
            {
                continue;
            }

            if (existingSupply.ContainsKey(truck.color))
            {
                existingSupply[truck.color]++;
            }
            else
            {
                existingSupply[truck.color] = 1;
            }
        }

        var remainingDemand = BuildRemainingDemandByColor();
        var colors = BuildTruckColors(1, existingSupply, remainingDemand);
        if (colors.Count == 0)
        {
            return;
        }

        var laneIndex = activeTrucks.Count;
        var spawnPos = GetTruckLaneWorldPosition(cameraRef, laneIndex, Mathf.Max(1, maxTruckCount));
        var truckGo = Instantiate(carPrefab, spawnPos, carPrefab.transform.rotation, runtimeTruckRoot);
        truckGo.name = $"Truck_{laneIndex + 1}";

        if (!TryResolveTruckBottomAnchor(truckGo.transform, out var bottomAnchor))
        {
            bottomAnchor = truckGo.transform;
            LogWarn($"Truck {truckGo.name} 未找到 BottomAnchor，改用车体根节点。", truckGo);
        }

        var colorType = colors[0];
        ApplyColorForTruck(truckGo.transform, colorType);
        activeTrucks.Add(new TruckRuntimeData
        {
            truck = truckGo,
            bottomAnchor = bottomAnchor,
            color = colorType,
            busy = false
        });
    }

    private void EnsureLevelNavigationUI()
    {
        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        previousLevelButton = EnsureLevelNavButton(previousLevelButton, "PreviousLevelButton", previousLevelButtonText, previousLevelButtonPosition, GoToPreviousLevelByButton);
        nextLevelButton = EnsureLevelNavButton(nextLevelButton, "NextLevelButton", nextLevelButtonText, nextLevelButtonPosition, GoToNextLevelByButton);
        EnsureLevelIndicatorText();
        ApplyLevelNavigationVisibility();
        BringOverlayForegroundElements();
    }

    private void EnsureSaveLevelButton()
    {
        var button = saveLevelButton;
        if (button == null)
        {
            button = EnsureSimpleButton("SaveLevelButton", saveLevelButtonSize, saveLevelButtonPosition, SaveCurrentLevelByButton);
            saveLevelButton = button;
        }

        if (saveLevelButton == null)
        {
            return;
        }

        var rect = saveLevelButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = saveLevelButtonSize;
        rect.anchoredPosition = saveLevelButtonPosition;

        var label = saveLevelButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = saveLevelButtonText;
        }

        saveLevelButton.onClick.RemoveListener(SaveCurrentLevelByButton);
        saveLevelButton.onClick.AddListener(SaveCurrentLevelByButton);
        ApplySaveLevelButtonVisibility();
    }

    private void ApplySaveLevelButtonVisibility()
    {
        if (saveLevelButton == null)
        {
            return;
        }

        var visible = runtimeMode == RuntimeMode.LevelDesignMode && showSaveLevelButtonInDesignMode;
        saveLevelButton.gameObject.SetActive(visible);
    }

    public void SaveCurrentLevelByButton()
    {
        SaveCurrentLayoutAsDesignedLevel();
    }

    private Button EnsureLevelNavButton(Button existingButton, string objectName, string buttonText, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var button = existingButton;
        if (button == null)
        {
            var allButtons = FindObjectsOfType<Button>(true);
            for (var i = 0; i < allButtons.Length; i++)
            {
                if (allButtons[i] != null && allButtons[i].name == objectName)
                {
                    button = allButtons[i];
                    break;
                }
            }
        }

        if (button == null)
        {
            var canvas = EnsureUICanvas();
            var buttonObj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(canvas.transform, false);

            var image = buttonObj.GetComponent<Image>();
            image.color = new Color(0.16f, 0.2f, 0.28f, 0.92f);

            button = buttonObj.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.38f, 0.96f);
            colors.pressedColor = new Color(0.1f, 0.14f, 0.2f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(buttonObj.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.text = buttonText;
        }

        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = levelNavButtonSize;
        rect.anchoredPosition = anchoredPos;

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = buttonText;
        }

        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);
        return button;
    }

    private Text EnsureLevelIndicatorText()
    {
        if (levelIndicatorTextRef != null)
        {
            return levelIndicatorTextRef;
        }

        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return null;
        }

        var existing = canvas.transform.Find("LevelIndicatorText");
        Text text;
        if (existing != null)
        {
            text = existing.GetComponent<Text>() ?? existing.gameObject.AddComponent<Text>();
        }
        else
        {
            var go = new GameObject("LevelIndicatorText", typeof(RectTransform), typeof(Text));
            go.transform.SetParent(canvas.transform, false);
            text = go.GetComponent<Text>();
        }

        var rect = text.GetComponent<RectTransform>();
        if (centerLevelIndicator)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
        }
        else
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
        }

        rect.sizeDelta = levelIndicatorSize;
        rect.anchoredPosition = levelIndicatorPosition;

        text.font = text.font ?? (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
        text.fontSize = Mathf.Max(10, levelIndicatorFontSize);
        text.alignment = centerLevelIndicator ? TextAnchor.MiddleCenter : TextAnchor.MiddleLeft;
        text.color = Color.white;
        text.raycastTarget = false;
        levelIndicatorTextRef = text;
        return levelIndicatorTextRef;
    }

    private void EnsureEconomyHud()
    {
        if (!showEconomyHud)
        {
            if (coinHudTextRef != null)
            {
                coinHudTextRef.gameObject.SetActive(false);
            }

            if (staminaHudTextRef != null)
            {
                staminaHudTextRef.gameObject.SetActive(false);
            }

            return;
        }

        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        coinHudTextRef = EnsureEconomyHudText(coinHudTextRef, canvas.transform, "CoinHudText", coinHudPosition);
        staminaHudTextRef = EnsureEconomyHudText(staminaHudTextRef, canvas.transform, "StaminaHudText", staminaHudPosition, staminaHudItemSize);
        UpdateEconomyHud();
        BringOverlayForegroundElements();
    }

    private void EnsureWinResultPanel()
    {
        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (winResultPanelRoot == null)
        {
            var existing = canvas.transform.Find("WinResultPanel") as RectTransform;
            if (existing != null)
            {
                winResultPanelRoot = existing;
            }
            else
            {
                var panelObj = new GameObject("WinResultPanel", typeof(RectTransform), typeof(Image));
                winResultPanelRoot = panelObj.GetComponent<RectTransform>();
                panelObj.transform.SetParent(canvas.transform, false);
            }
        }

        if (winResultPanelRoot == null)
        {
            return;
        }

        winResultPanelRoot.anchorMin = new Vector2(0.5f, 0.5f);
        winResultPanelRoot.anchorMax = new Vector2(0.5f, 0.5f);
        winResultPanelRoot.pivot = new Vector2(0.5f, 0.5f);
        winResultPanelRoot.sizeDelta = winResultPanelSize;
        winResultPanelRoot.anchoredPosition = winResultPanelPosition;

        var panelBg = winResultPanelRoot.GetComponent<Image>();
        if (panelBg == null)
        {
            panelBg = winResultPanelRoot.gameObject.AddComponent<Image>();
        }

        panelBg.color = winResultPanelBackgroundColor;
        panelBg.raycastTarget = true;

        winResultTitleTextRef = EnsureWinPanelText(
            winResultTitleTextRef,
            winResultPanelRoot,
            "TitleText",
            winResultTitleSize,
            winResultTitlePosition,
            winResultTitleFontSize,
            FontStyle.Bold);

        winResultRewardTextRef = EnsureWinPanelText(
            winResultRewardTextRef,
            winResultPanelRoot,
            "RewardText",
            winResultRewardSize,
            winResultRewardPosition,
            winResultRewardFontSize,
            FontStyle.Normal);

        winResultNextLevelButtonRef = EnsureWinPanelButton(
            winResultNextLevelButtonRef,
            winResultPanelRoot,
            "NextLevelButton",
            winResultNextLevelButtonText,
            winResultNextLevelButtonSize,
            winResultNextLevelButtonPosition,
            GoToNextLevelFromWinPanelByButton);

        HideWinResultPanel();
        BringOverlayForegroundElements();
    }

    private Text EnsureWinPanelText(
        Text target,
        RectTransform panelRoot,
        string childName,
        Vector2 size,
        Vector2 anchoredPosition,
        int fontSize,
        FontStyle fontStyle)
    {
        var textRef = target;
        if (textRef == null)
        {
            var existing = panelRoot.Find(childName);
            if (existing != null)
            {
                textRef = existing.GetComponent<Text>() ?? existing.gameObject.AddComponent<Text>();
            }
        }

        if (textRef == null)
        {
            var textObj = new GameObject(childName, typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(panelRoot, false);
            textRef = textObj.GetComponent<Text>();
        }

        var rect = textRef.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        textRef.font = textRef.font ?? (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
        textRef.fontSize = Mathf.Max(12, fontSize);
        textRef.fontStyle = fontStyle;
        textRef.alignment = TextAnchor.MiddleCenter;
        textRef.color = Color.white;
        textRef.raycastTarget = false;
        return textRef;
    }

    private Button EnsureWinPanelButton(
        Button target,
        RectTransform panelRoot,
        string childName,
        string buttonLabel,
        Vector2 size,
        Vector2 anchoredPosition,
        UnityEngine.Events.UnityAction onClick)
    {
        var button = target;
        if (button == null)
        {
            var existing = panelRoot.Find(childName);
            if (existing != null)
            {
                button = existing.GetComponent<Button>() ?? existing.gameObject.AddComponent<Button>();
                if (existing.GetComponent<Image>() == null)
                {
                    existing.gameObject.AddComponent<Image>();
                }
            }
        }

        if (button == null)
        {
            var buttonObj = new GameObject(childName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(panelRoot, false);
            button = buttonObj.GetComponent<Button>();

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(buttonObj.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
        }

        var rect = button.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        var image = button.GetComponent<Image>();
        if (image != null)
        {
            image.color = new Color(0.2f, 0.3f, 0.46f, 0.98f);
        }

        var colors = button.colors;
        colors.normalColor = image != null ? image.color : colors.normalColor;
        colors.highlightedColor = new Color(0.28f, 0.4f, 0.58f, 1f);
        colors.pressedColor = new Color(0.14f, 0.22f, 0.34f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var label = button.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.font = label.font ?? (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
            label.fontSize = 30;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.text = buttonLabel;
            label.raycastTarget = false;
        }

        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);
        return button;
    }

    private void ShowWinResultPanel(int reward)
    {
        EnsureWinResultPanel();
        if (winResultPanelRoot == null)
        {
            return;
        }

        if (winResultTitleTextRef != null)
        {
            var title = isFinalDesignedLevelWin
                ? (string.IsNullOrWhiteSpace(finalDesignedLevelWinMessage) ? catWinMessage : finalDesignedLevelWinMessage)
                : catWinMessage;
            winResultTitleTextRef.text = title;
        }

        if (winResultRewardTextRef != null)
        {
            winResultRewardTextRef.text = $"{winRewardTextPrefix} {Mathf.Max(0, reward)}";
        }

        if (winResultNextLevelButtonRef != null)
        {
            var label = winResultNextLevelButtonRef.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = winResultNextLevelButtonText;
            }

            winResultNextLevelButtonRef.gameObject.SetActive(!isFinalDesignedLevelWin);
        }

        winResultPanelRoot.gameObject.SetActive(true);
        BringOverlayForegroundElements();
    }

    private void HideWinResultPanel()
    {
        if (winResultPanelRoot != null)
        {
            winResultPanelRoot.gameObject.SetActive(false);
        }
    }

    private void GoToNextLevelFromWinPanelByButton()
    {
        if (isFinalDesignedLevelWin)
        {
            LogInfo($"Win panel next blocked: final designed level reached | level={Mathf.Max(1, currentLevelIndex)}");
            return;
        }

        Time.timeScale = 1f;
        HideWinResultPanel();
        HideDimOverlay();
        GoToNextLevelByButton();
    }

    private Text EnsureEconomyHudText(Text target, Transform parent, string objectName, Vector2 anchoredPosition)
    {
        return EnsureEconomyHudText(target, parent, objectName, anchoredPosition, economyHudItemSize);
    }

    private Text EnsureEconomyHudText(Text target, Transform parent, string objectName, Vector2 anchoredPosition, Vector2 itemSize)
    {
        var textRef = target;
        if (textRef == null)
        {
            var existing = parent.Find(objectName);
            if (existing != null)
            {
                textRef = existing.GetComponent<Text>() ?? existing.gameObject.AddComponent<Text>();
            }
        }

        if (textRef == null)
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(parent, false);
            textRef = go.GetComponent<Text>();
        }

        var rect = textRef.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = itemSize;
        rect.anchoredPosition = anchoredPosition;

        textRef.font = textRef.font ?? (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
        textRef.fontSize = Mathf.Max(12, economyHudFontSize);
        textRef.alignment = TextAnchor.MiddleLeft;
        textRef.color = Color.white;
        textRef.raycastTarget = false;
        textRef.gameObject.SetActive(true);
        return textRef;
    }

    private void InitializeEconomyState()
    {
        if (persistEconomyData)
        {
            if (!UnityEngine.PlayerPrefs.HasKey(EconomyCoinPrefKey))
            {
                UnityEngine.PlayerPrefs.SetInt(EconomyCoinPrefKey, Mathf.Max(0, initialCoinCount));
            }

            if (!UnityEngine.PlayerPrefs.HasKey(EconomyStaminaPrefKey))
            {
                UnityEngine.PlayerPrefs.SetInt(EconomyStaminaPrefKey, Mathf.Max(0, initialStaminaCount));
            }

            currentCoinCount = Mathf.Max(0, UnityEngine.PlayerPrefs.GetInt(EconomyCoinPrefKey, Mathf.Max(0, initialCoinCount)));
            currentStaminaCount = Mathf.Max(0, UnityEngine.PlayerPrefs.GetInt(EconomyStaminaPrefKey, Mathf.Max(0, initialStaminaCount)));
            var savedTicks = UnityEngine.PlayerPrefs.GetString(EconomyStaminaRecoveryStartTicksPrefKey, string.Empty);
            if (!string.IsNullOrWhiteSpace(savedTicks) && long.TryParse(savedTicks, out var parsedTicks) && parsedTicks > 0)
            {
                staminaRecoveryStartUtcTicks = parsedTicks;
            }
            else
            {
                staminaRecoveryStartUtcTicks = -1L;
            }

            ProcessStaminaRecoveryByElapsedTime(true);
            UnityEngine.PlayerPrefs.Save();
        }
        else
        {
            currentCoinCount = Mathf.Max(0, initialCoinCount);
            currentStaminaCount = Mathf.Max(0, initialStaminaCount);
            staminaRecoveryStartUtcTicks = currentStaminaCount < Mathf.Max(1, staminaRecoveryMax) ? DateTime.UtcNow.Ticks : -1L;
        }

        nextStaminaRecoveryUiRefreshTime = Time.unscaledTime + 1f;
    }

    private void SaveEconomyState()
    {
        if (!persistEconomyData)
        {
            return;
        }

        UnityEngine.PlayerPrefs.SetInt(EconomyCoinPrefKey, Mathf.Max(0, currentCoinCount));
        UnityEngine.PlayerPrefs.SetInt(EconomyStaminaPrefKey, Mathf.Max(0, currentStaminaCount));
        if (staminaRecoveryStartUtcTicks > 0 && currentStaminaCount < Mathf.Max(1, staminaRecoveryMax))
        {
            UnityEngine.PlayerPrefs.SetString(EconomyStaminaRecoveryStartTicksPrefKey, staminaRecoveryStartUtcTicks.ToString());
        }
        else
        {
            UnityEngine.PlayerPrefs.DeleteKey(EconomyStaminaRecoveryStartTicksPrefKey);
        }

        UnityEngine.PlayerPrefs.Save();
    }

    private void UpdateEconomyHud()
    {
        if (!showEconomyHud)
        {
            return;
        }

        if (coinHudTextRef != null)
        {
            coinHudTextRef.text = $"{coinIconText} {Mathf.Max(0, currentCoinCount)}";
        }

        if (staminaHudTextRef != null)
        {
            var staminaValue = Mathf.Max(0, currentStaminaCount);
            var suffix = string.Empty;
            if (staminaValue < Mathf.Max(1, staminaRecoveryMax) && TryGetStaminaRecoveryCountdownText(out var countdown))
            {
                suffix = $"  ({countdown})";
            }

            staminaHudTextRef.text = $"{staminaIconText} {staminaValue}{suffix}";
        }

        UpdateRestartConfirmButtonVisualState();
    }

    private bool TryConsumeStaminaOnRestart()
    {
        if (IsLevelDesignMode)
        {
            return true;
        }

        var cost = Mathf.Max(0, staminaCostOnRestart);
        if (cost <= 0)
        {
            return true;
        }

        if (currentStaminaCount < cost)
        {
            LogWarn($"体力不足，无法重新开始当前局 | stamina={currentStaminaCount} | cost={cost}", this);
            ShowStaminaInsufficientBubble();
            UpdateRestartConfirmButtonVisualState();
            return false;
        }

        currentStaminaCount = Mathf.Max(0, currentStaminaCount - cost);
        if (currentStaminaCount < Mathf.Max(1, staminaRecoveryMax) && staminaRecoveryStartUtcTicks <= 0)
        {
            staminaRecoveryStartUtcTicks = DateTime.UtcNow.Ticks;
        }

        SaveEconomyState();
        UpdateEconomyHud();
        ApplyBoxInteractionMode();
        LogInfo($"重新开始消耗体力成功 | cost={cost} | remain={currentStaminaCount}");
        return true;
    }

    private void HandleStaminaRecoveryTick()
    {
        if (IsLevelDesignMode)
        {
            return;
        }

        if (Time.unscaledTime < nextStaminaRecoveryUiRefreshTime)
        {
            return;
        }

        nextStaminaRecoveryUiRefreshTime = Time.unscaledTime + 1f;
        var changed = ProcessStaminaRecoveryByElapsedTime(true);
        if (changed || (showEconomyHud && staminaHudTextRef != null))
        {
            UpdateEconomyHud();
        }
    }

    private bool ProcessStaminaRecoveryByElapsedTime(bool saveWhenChanged)
    {
        var maxStamina = Mathf.Max(1, staminaRecoveryMax);
        var intervalMinutes = Mathf.Max(1, staminaRecoveryIntervalMinutes);
        var intervalTicks = TimeSpan.FromMinutes(intervalMinutes).Ticks;
        var changed = false;

        if (currentStaminaCount >= maxStamina)
        {
            if (staminaRecoveryStartUtcTicks > 0)
            {
                staminaRecoveryStartUtcTicks = -1L;
                changed = true;
            }

            if (changed && saveWhenChanged)
            {
                SaveEconomyState();
            }

            return changed;
        }

        var nowTicks = DateTime.UtcNow.Ticks;
        if (staminaRecoveryStartUtcTicks <= 0 || staminaRecoveryStartUtcTicks > nowTicks)
        {
            staminaRecoveryStartUtcTicks = nowTicks;
            changed = true;
        }

        var elapsedTicks = nowTicks - staminaRecoveryStartUtcTicks;
        if (elapsedTicks >= intervalTicks)
        {
            var recoverCount = (int)(elapsedTicks / intervalTicks);
            if (recoverCount > 0)
            {
                var add = Mathf.Min(recoverCount, maxStamina - currentStaminaCount);
                if (add > 0)
                {
                    currentStaminaCount = Mathf.Max(0, currentStaminaCount + add);
                    staminaRecoveryStartUtcTicks += intervalTicks * add;
                    changed = true;
                }

                if (currentStaminaCount >= maxStamina)
                {
                    staminaRecoveryStartUtcTicks = -1L;
                }
            }
        }

        if (changed && saveWhenChanged)
        {
            SaveEconomyState();
        }

        return changed;
    }

    private bool TryGetStaminaRecoveryCountdownText(out string countdownText)
    {
        countdownText = string.Empty;
        if (currentStaminaCount >= Mathf.Max(1, staminaRecoveryMax) || staminaRecoveryStartUtcTicks <= 0)
        {
            return false;
        }

        var interval = TimeSpan.FromMinutes(Mathf.Max(1, staminaRecoveryIntervalMinutes));
        var elapsed = TimeSpan.FromTicks(Math.Max(0L, DateTime.UtcNow.Ticks - staminaRecoveryStartUtcTicks));
        var remaining = interval - TimeSpan.FromTicks(elapsed.Ticks % interval.Ticks);
        if (remaining <= TimeSpan.Zero)
        {
            remaining = interval;
        }

        countdownText = $"{Mathf.Max(0, remaining.Minutes):00}:{Mathf.Max(0, remaining.Seconds):00}";
        return true;
    }

    private void EnsureStaminaInsufficientBubble()
    {
        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (staminaInsufficientBubbleRoot == null)
        {
            var existing = canvas.transform.Find("StaminaInsufficientBubble") as RectTransform;
            if (existing != null)
            {
                staminaInsufficientBubbleRoot = existing;
            }
            else
            {
                var bubbleObj = new GameObject("StaminaInsufficientBubble", typeof(RectTransform), typeof(Image));
                staminaInsufficientBubbleRoot = bubbleObj.GetComponent<RectTransform>();
                bubbleObj.transform.SetParent(canvas.transform, false);
                var image = bubbleObj.GetComponent<Image>();
                image.color = new Color(0.08f, 0.08f, 0.08f, 0.88f);
                image.raycastTarget = false;

                var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
                textObj.transform.SetParent(bubbleObj.transform, false);
                staminaInsufficientBubbleTextRef = textObj.GetComponent<Text>();
            }
        }

        if (staminaInsufficientBubbleRoot == null)
        {
            return;
        }

        staminaInsufficientBubbleRoot.anchorMin = new Vector2(0.5f, 0f);
        staminaInsufficientBubbleRoot.anchorMax = new Vector2(0.5f, 0f);
        staminaInsufficientBubbleRoot.pivot = new Vector2(0.5f, 0f);
        staminaInsufficientBubbleRoot.sizeDelta = staminaInsufficientBubbleSize;
        staminaInsufficientBubbleRoot.anchoredPosition = staminaInsufficientBubblePosition;

        if (staminaInsufficientBubbleTextRef == null)
        {
            staminaInsufficientBubbleTextRef = staminaInsufficientBubbleRoot.Find("Text")?.GetComponent<Text>();
        }

        if (staminaInsufficientBubbleTextRef != null)
        {
            var textRect = staminaInsufficientBubbleTextRef.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            staminaInsufficientBubbleTextRef.font = staminaInsufficientBubbleTextRef.font
                ?? (Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf"));
            staminaInsufficientBubbleTextRef.fontSize = Mathf.Max(18, economyHudFontSize - 2);
            staminaInsufficientBubbleTextRef.alignment = TextAnchor.MiddleCenter;
            staminaInsufficientBubbleTextRef.color = Color.white;
            staminaInsufficientBubbleTextRef.raycastTarget = false;
            staminaInsufficientBubbleTextRef.text = staminaInsufficientBubbleText;
        }

        staminaInsufficientBubbleRoot.gameObject.SetActive(false);
    }

    private void ShowStaminaInsufficientBubble()
    {
        ShowBottomBubble(staminaInsufficientBubbleText, staminaInsufficientBubbleDuration);
    }

    private void ShowBottomBubble(string message, float duration)
    {
        EnsureStaminaInsufficientBubble();
        if (staminaInsufficientBubbleRoot == null)
        {
            return;
        }

        if (staminaInsufficientBubbleRoutine != null)
        {
            StopCoroutine(staminaInsufficientBubbleRoutine);
            staminaInsufficientBubbleRoutine = null;
        }

        if (staminaInsufficientBubbleTextRef != null)
        {
            staminaInsufficientBubbleTextRef.text = string.IsNullOrWhiteSpace(message)
                ? staminaInsufficientBubbleText
                : message;
        }

        staminaInsufficientBubbleRoot.gameObject.SetActive(true);
        staminaInsufficientBubbleRoutine = StartCoroutine(HideStaminaInsufficientBubbleRoutine(duration));
        BringOverlayForegroundElements();
    }

    private IEnumerator HideStaminaInsufficientBubbleRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0.1f, duration));
        if (staminaInsufficientBubbleRoot != null)
        {
            staminaInsufficientBubbleRoot.gameObject.SetActive(false);
        }

        staminaInsufficientBubbleRoutine = null;
    }

    private void UpdateRestartConfirmButtonVisualState()
    {
        if (restartFromSettingsButton == null)
        {
            return;
        }

        var image = restartFromSettingsButton.GetComponent<Image>();
        if (image == null)
        {
            return;
        }

        var hasStamina = currentStaminaCount > 0;
        image.color = hasStamina
            ? new Color(0.16f, 0.2f, 0.28f, 0.92f)
            : new Color(0.35f, 0.35f, 0.35f, 0.92f);

        var colors = restartFromSettingsButton.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = hasStamina ? new Color(0.22f, 0.28f, 0.38f, 0.96f) : image.color;
        colors.pressedColor = hasStamina ? new Color(0.1f, 0.14f, 0.2f, 1f) : image.color;
        colors.selectedColor = colors.highlightedColor;
        restartFromSettingsButton.colors = colors;
    }

    private int GrantCoinsByWin()
    {
        var reward = Mathf.Max(0, winRewardCoinCount);
        if (reward <= 0)
        {
            return 0;
        }

        currentCoinCount = Mathf.Max(0, currentCoinCount + reward);
        SaveEconomyState();
        UpdateEconomyHud();
        LogInfo($"通关奖励金币 | reward={reward} | totalCoin={currentCoinCount}");
        return reward;
    }

    private void EnsureModeToggleButton()
    {
        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (Vector2.Distance(modeToggleButtonPosition, LegacyModeToggleButtonPosition) < 0.001f)
        {
            modeToggleButtonPosition = DefaultTopRightModeToggleButtonPosition;
        }

        var button = modeToggleButton;
        if (button == null)
        {
            button = EnsureSimpleButton("ModeToggleButton", modeToggleButtonSize, modeToggleButtonPosition, ToggleRuntimeModeByButton);
            modeToggleButton = button;
        }

        if (modeToggleButton == null)
        {
            return;
        }

        if (modeToggleButton.transform.parent != canvas.transform)
        {
            modeToggleButton.transform.SetParent(canvas.transform, false);
        }

        var rect = modeToggleButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = modeToggleButtonSize;
        rect.anchoredPosition = modeToggleButtonPosition;

        modeToggleButton.onClick.RemoveListener(ToggleRuntimeModeByButton);
        modeToggleButton.onClick.AddListener(ToggleRuntimeModeByButton);
        UpdateModeToggleButtonLabel();
    }

    private void EnsureRestartSettingsButton()
    {
        if (!showRestartSettingsButton)
        {
            ApplyRestartSettingsButtonVisibility();
            return;
        }

        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (restartSettingsButton == null)
        {
            restartSettingsButton = EnsureSimpleButton(
                "RestartSettingsButton",
                restartSettingsButtonSize,
                restartSettingsButtonPosition,
                OpenRestartSettingsOverlay);
        }

        if (restartSettingsButton == null)
        {
            return;
        }

        if (restartSettingsButton.transform.parent != canvas.transform)
        {
            restartSettingsButton.transform.SetParent(canvas.transform, false);
        }

        var rect = restartSettingsButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = restartSettingsButtonSize;
        rect.anchoredPosition = restartSettingsButtonPosition;

        var label = restartSettingsButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = restartSettingsButtonText;
        }

        restartSettingsButton.onClick.RemoveListener(OpenRestartSettingsOverlay);
        restartSettingsButton.onClick.AddListener(OpenRestartSettingsOverlay);
        ApplyRestartSettingsButtonVisibility();
    }

    private void ApplyRestartSettingsButtonVisibility()
    {
        if (restartSettingsButton == null)
        {
            return;
        }

        var visible = showRestartSettingsButton && runtimeMode == RuntimeMode.GameMode;
        restartSettingsButton.gameObject.SetActive(visible);
        if (!visible)
        {
            HideRestartSettingsOverlay();
        }
    }

    private void EnsureTestClearSaveButton()
    {
        if (!showTestClearSaveButton)
        {
            ApplyTestClearSaveButtonVisibility();
            return;
        }

        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (testClearSaveButton == null)
        {
            testClearSaveButton = EnsureSimpleButton(
                "TestClearSaveButton",
                testClearSaveButtonSize,
                testClearSaveButtonPosition,
                ClearTestSaveDataByButton);
        }

        if (testClearSaveButton == null)
        {
            return;
        }

        if (testClearSaveButton.transform.parent != canvas.transform)
        {
            testClearSaveButton.transform.SetParent(canvas.transform, false);
        }

        var rect = testClearSaveButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 0f);
        rect.anchorMax = new Vector2(1f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.sizeDelta = testClearSaveButtonSize;
        rect.anchoredPosition = testClearSaveButtonPosition;

        var label = testClearSaveButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = testClearSaveButtonText;
        }

        testClearSaveButton.onClick.RemoveListener(ClearTestSaveDataByButton);
        testClearSaveButton.onClick.AddListener(ClearTestSaveDataByButton);
        ApplyTestClearSaveButtonVisibility();
    }

    private void ApplyTestClearSaveButtonVisibility()
    {
        if (testClearSaveButton == null)
        {
            return;
        }

        testClearSaveButton.gameObject.SetActive(showTestClearSaveButton);
    }

    private void ClearTestSaveDataByButton()
    {
        UnityEngine.PlayerPrefs.DeleteKey(EconomyCoinPrefKey);
        UnityEngine.PlayerPrefs.DeleteKey(EconomyStaminaPrefKey);
        UnityEngine.PlayerPrefs.DeleteKey(EconomyStaminaRecoveryStartTicksPrefKey);
        UnityEngine.PlayerPrefs.Save();

        staminaConsumedForCurrentRound = false;
        staminaRecoveryStartUtcTicks = -1L;
        InitializeEconomyState();
        UpdateEconomyHud();
        ApplyBoxInteractionMode();
        LogInfo("测试清档完成：金币与体力存档已重置。", this);
    }

    private void OpenRestartSettingsOverlay()
    {
        if (runtimeMode != RuntimeMode.GameMode)
        {
            return;
        }

        EnsureRestartSettingsOverlay();
        if (restartSettingsOverlayRoot == null)
        {
            return;
        }

        restartSettingsOverlayRoot.gameObject.SetActive(true);
        UpdateRestartConfirmButtonVisualState();
        BringOverlayForegroundElements();
    }

    private void HideRestartSettingsOverlay()
    {
        if (restartSettingsOverlayRoot != null)
        {
            restartSettingsOverlayRoot.gameObject.SetActive(false);
        }
    }

    private void EnsureRestartSettingsOverlay()
    {
        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (restartSettingsOverlayRoot == null)
        {
            var existing = canvas.transform.Find("RestartSettingsOverlay") as RectTransform;
            if (existing != null)
            {
                restartSettingsOverlayRoot = existing;
            }
            else
            {
                var overlayObj = new GameObject("RestartSettingsOverlay", typeof(RectTransform), typeof(Image), typeof(Button));
                restartSettingsOverlayRoot = overlayObj.GetComponent<RectTransform>();
                overlayObj.transform.SetParent(canvas.transform, false);

                var panelObj = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                panelObj.transform.SetParent(restartSettingsOverlayRoot, false);
                restartSettingsPanelRoot = panelObj.GetComponent<RectTransform>();
                CreatePanelButton(
                    restartSettingsPanelRoot,
                    "RestartButton",
                    restartConfirmButtonText,
                    restartConfirmButtonPosition,
                    restartConfirmButtonSize,
                    RestartRoundFromSettingsByButton);
            }
        }

        if (restartSettingsOverlayRoot == null)
        {
            return;
        }

        var overlayImage = restartSettingsOverlayRoot.GetComponent<Image>();
        if (overlayImage == null)
        {
            overlayImage = restartSettingsOverlayRoot.gameObject.AddComponent<Image>();
        }

        overlayImage.color = restartSettingsOverlayColor;
        overlayImage.raycastTarget = true;

        var overlayButton = restartSettingsOverlayRoot.GetComponent<Button>();
        if (overlayButton == null)
        {
            overlayButton = restartSettingsOverlayRoot.gameObject.AddComponent<Button>();
        }

        var overlayColors = overlayButton.colors;
        overlayColors.normalColor = Color.white;
        overlayColors.highlightedColor = Color.white;
        overlayColors.pressedColor = Color.white;
        overlayColors.selectedColor = Color.white;
        overlayButton.colors = overlayColors;
        overlayButton.targetGraphic = overlayImage;
        overlayButton.onClick.RemoveListener(HideRestartSettingsOverlay);
        overlayButton.onClick.AddListener(HideRestartSettingsOverlay);

        restartSettingsOverlayRoot.anchorMin = Vector2.zero;
        restartSettingsOverlayRoot.anchorMax = Vector2.one;
        restartSettingsOverlayRoot.offsetMin = Vector2.zero;
        restartSettingsOverlayRoot.offsetMax = Vector2.zero;

        if (restartSettingsPanelRoot == null)
        {
            restartSettingsPanelRoot = restartSettingsOverlayRoot.Find("Panel") as RectTransform;
        }

        if (restartSettingsPanelRoot != null)
        {
            restartSettingsPanelRoot.anchorMin = new Vector2(0.5f, 0.5f);
            restartSettingsPanelRoot.anchorMax = new Vector2(0.5f, 0.5f);
            restartSettingsPanelRoot.pivot = new Vector2(0.5f, 0.5f);
            restartSettingsPanelRoot.sizeDelta = restartSettingsPanelSize;
            restartSettingsPanelRoot.anchoredPosition = restartSettingsPanelPosition;

            var panelImage = restartSettingsPanelRoot.GetComponent<Image>();
            if (panelImage == null)
            {
                panelImage = restartSettingsPanelRoot.gameObject.AddComponent<Image>();
            }

            panelImage.color = restartSettingsPanelColor;
            panelImage.raycastTarget = true;
        }

        if (restartFromSettingsButton == null && restartSettingsPanelRoot != null)
        {
            restartFromSettingsButton = restartSettingsPanelRoot.Find("RestartButton")?.GetComponent<Button>();
        }

        if (restartFromSettingsButton != null)
        {
            var buttonRect = restartFromSettingsButton.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = restartConfirmButtonSize;
            buttonRect.anchoredPosition = restartConfirmButtonPosition;

            var label = restartFromSettingsButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = restartConfirmButtonText;
            }

            restartFromSettingsButton.onClick.RemoveListener(RestartRoundFromSettingsByButton);
            restartFromSettingsButton.onClick.AddListener(RestartRoundFromSettingsByButton);
        }

        UpdateRestartConfirmButtonVisualState();

        restartSettingsOverlayRoot.gameObject.SetActive(false);
    }

    private void RestartRoundFromSettingsByButton()
    {
        applyRestartColorVariationOnNextRefresh = true;
        HideRestartSettingsOverlay();
        RestartRoundByButton();
    }

    private void EnsureDesignToolsPanel()
    {
        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (designToolsPanelRoot == null)
        {
            var existing = canvas.transform.Find("DesignToolsPanel") as RectTransform;
            if (existing != null)
            {
                designToolsPanelRoot = existing;
            }
            else
            {
                var panelObj = new GameObject("DesignToolsPanel", typeof(RectTransform), typeof(Image));
                designToolsPanelRoot = panelObj.GetComponent<RectTransform>();
                panelObj.transform.SetParent(canvas.transform, false);
                var image = panelObj.GetComponent<Image>();
                image.color = new Color(0.06f, 0.08f, 0.12f, 0.72f);
                image.raycastTarget = false;

                var columnText = CreatePanelText(designToolsPanelRoot, "ShelfColumnText", new Vector2(-110f, -34f), new Vector2(120f, 80f));
                shelfColumnCountTextRef = columnText;

                var colorText = CreatePanelText(designToolsPanelRoot, "ColorTypeText", new Vector2(110f, -34f), new Vector2(120f, 80f));
                colorTypeCountTextRef = colorText;

                CreatePanelButton(designToolsPanelRoot, "ColumnPlus", "+", new Vector2(-160f, -108f), new Vector2(40f, 40f), IncreaseShelfColumnCount);
                CreatePanelButton(designToolsPanelRoot, "ColumnMinus", "-", new Vector2(-90f, -108f), new Vector2(40f, 40f), DecreaseShelfColumnCount);

                CreatePanelButton(designToolsPanelRoot, "ColorPlus", "+", new Vector2(60f, -108f), new Vector2(40f, 40f), IncreaseColorTypeCount);
                CreatePanelButton(designToolsPanelRoot, "ColorMinus", "-", new Vector2(130f, -108f), new Vector2(40f, 40f), DecreaseColorTypeCount);

                refreshColorsButton = CreatePanelButton(designToolsPanelRoot, "RefreshColorsButton", refreshColorsButtonText, new Vector2(110f, -156f), new Vector2(110f, 40f), RefreshCurrentBoxesColors);
            }
        }

        EnsureColumnControlArea();

        if (designToolsPanelRoot == null)
        {
            return;
        }

        var panelRect = designToolsPanelRoot;
        panelRect.anchorMin = new Vector2(0.5f, 1f);
        panelRect.anchorMax = new Vector2(0.5f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.sizeDelta = designPanelSize;
        panelRect.anchoredPosition = designPanelPosition;

        if (shelfColumnCountTextRef == null)
        {
            shelfColumnCountTextRef = designToolsPanelRoot.Find("ShelfColumnText")?.GetComponent<Text>();
        }

        if (colorTypeCountTextRef == null)
        {
            colorTypeCountTextRef = designToolsPanelRoot.Find("ColorTypeText")?.GetComponent<Text>();
        }

        if (refreshColorsButton == null)
        {
            refreshColorsButton = designToolsPanelRoot.Find("RefreshColorsButton")?.GetComponent<Button>();
        }

        if (refreshColorsButton != null)
        {
            var label = refreshColorsButton.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = refreshColorsButtonText;
            }

            refreshColorsButton.onClick.RemoveListener(RefreshCurrentBoxesColors);
            refreshColorsButton.onClick.AddListener(RefreshCurrentBoxesColors);
        }

        UpdateDesignToolsPanelLayout();

        RebuildColumnControlAreaChildren();
    }

    private void UpdateDesignToolsPanelLayout()
    {
        if (designToolsPanelRoot == null)
        {
            return;
        }

        ApplyPanelChildLayout("ShelfColumnText", new Vector2(-110f, -34f), new Vector2(120f, 80f));
        ApplyPanelChildLayout("ColorTypeText", new Vector2(110f, -34f), new Vector2(120f, 80f));
        ApplyPanelChildLayout("ColumnPlus", new Vector2(-160f, -108f), new Vector2(40f, 40f));
        ApplyPanelChildLayout("ColumnMinus", new Vector2(-90f, -108f), new Vector2(40f, 40f));
        ApplyPanelChildLayout("ColorPlus", new Vector2(60f, -108f), new Vector2(40f, 40f));
        ApplyPanelChildLayout("ColorMinus", new Vector2(130f, -108f), new Vector2(40f, 40f));
        ApplyPanelChildLayout("RefreshColorsButton", new Vector2(110f, -156f), new Vector2(110f, 40f));
    }

    private void ApplyPanelChildLayout(string childName, Vector2 anchoredPos, Vector2 size)
    {
        var child = designToolsPanelRoot.Find(childName) as RectTransform;
        if (child == null)
        {
            return;
        }

        child.anchorMin = new Vector2(0.5f, 1f);
        child.anchorMax = new Vector2(0.5f, 1f);
        child.pivot = new Vector2(0.5f, 1f);
        child.anchoredPosition = anchoredPos;
        child.sizeDelta = size;
    }

    private void EnsureColumnControlArea()
    {
        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return;
        }

        if (columnControlAreaRoot == null)
        {
            var existing = canvas.transform.Find("DesignColumnControlArea") as RectTransform;
            if (existing != null)
            {
                columnControlAreaRoot = existing;
            }
            else
            {
                var rootObj = new GameObject("DesignColumnControlArea", typeof(RectTransform));
                columnControlAreaRoot = rootObj.GetComponent<RectTransform>();
                rootObj.transform.SetParent(canvas.transform, false);
            }
        }

        if (columnControlAreaRoot == null)
        {
            return;
        }

        columnControlAreaRoot.anchorMin = new Vector2(0f, 0f);
        columnControlAreaRoot.anchorMax = new Vector2(1f, 0f);
        columnControlAreaRoot.pivot = new Vector2(0.5f, 0f);
        columnControlAreaRoot.sizeDelta = columnControlAreaSize;
        columnControlAreaRoot.anchoredPosition = columnControlAreaPosition;
    }

    private void RebuildColumnControlAreaChildren()
    {
        if (columnControlAreaRoot == null)
        {
            return;
        }

        for (var i = columnControlAreaRoot.childCount - 1; i >= 0; i--)
        {
            var child = columnControlAreaRoot.GetChild(i);
            if (child == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(child.gameObject);
            }
            else
            {
                DestroyImmediate(child.gameObject);
            }
        }

        var runtimeShelfCount = ResolveRuntimeShelfCountForLayout();
        EnsureColumnConfigState(runtimeShelfCount, false);
        var columnCount = Mathf.Clamp(shelfColumnCount, 1, Mathf.Max(1, runtimeShelfCount));
        var left = 0.08f;
        var right = 0.92f;

        for (var c = 0; c < columnCount; c++)
        {
            var columnRootObj = new GameObject($"ColumnGroup_{c + 1}", typeof(RectTransform));
            var columnRoot = columnRootObj.GetComponent<RectTransform>();
            columnRoot.SetParent(columnControlAreaRoot, false);
            var t = columnCount <= 1 ? 0.5f : (float)c / (columnCount - 1);
            var x = Mathf.Lerp(left, right, t);
            columnRoot.anchorMin = new Vector2(x, 0f);
            columnRoot.anchorMax = new Vector2(x, 0f);
            columnRoot.pivot = new Vector2(0.5f, 0f);
            columnRoot.sizeDelta = new Vector2(160f, 180f);
            columnRoot.anchoredPosition = Vector2.zero;

            var labelObj = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObj.transform.SetParent(columnRoot, false);
            var labelRect = labelObj.GetComponent<RectTransform>();
            labelRect.anchorMin = new Vector2(0.5f, 0f);
            labelRect.anchorMax = new Vector2(0.5f, 0f);
            labelRect.pivot = new Vector2(0.5f, 0f);
            labelRect.sizeDelta = new Vector2(152f, 56f);
            labelRect.anchoredPosition = new Vector2(0f, 126f);

            var labelText = labelObj.GetComponent<Text>();
            labelText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            labelText.fontSize = 20;
            labelText.alignment = TextAnchor.MiddleCenter;
            labelText.color = Color.white;
            labelText.raycastTarget = false;
            var offsetValue = c < columnVerticalOffsets.Count ? columnVerticalOffsets[c] : 0f;
            var shelfValue = c < columnShelfCounts.Count ? columnShelfCounts[c] : 0;
            labelText.text = $"第{c + 1}列\n数量:{shelfValue} 偏移:{offsetValue:+0.00;-0.00;0.00}";

            var capturedIndex = c;
            CreateColumnControlButton(columnRoot, "AddShelf", "+", new Vector2(-36f, 82f), () => IncreaseCurrentColumnShelfCount(capturedIndex));
            CreateColumnControlButton(columnRoot, "RemoveShelf", "-", new Vector2(36f, 82f), () => DecreaseCurrentColumnShelfCount(capturedIndex));
            CreateColumnControlButton(columnRoot, "MoveUp", "上+", new Vector2(-36f, 30f), () => MoveCurrentColumnUp(capturedIndex));
            CreateColumnControlButton(columnRoot, "MoveDown", "下-", new Vector2(36f, 30f), () => MoveCurrentColumnDown(capturedIndex));
        }
    }

    private Button CreateColumnControlButton(RectTransform root, string name, string label, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(root, false);
        var rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0f);
        rect.anchorMax = new Vector2(0.5f, 0f);
        rect.pivot = new Vector2(0.5f, 0f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(64f, 42f);

        var image = buttonObj.GetComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.28f, 0.92f);

        var button = buttonObj.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.22f, 0.28f, 0.38f, 0.96f);
        colors.pressedColor = new Color(0.1f, 0.14f, 0.2f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(buttonObj.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 18;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = label;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(onClick);
        return button;
    }

    private bool TryValidateColumnIndex(int columnIndex)
    {
        var shelfCount = ResolveRuntimeShelfCountForLayout();
        EnsureColumnConfigState(shelfCount, false);
        return columnIndex >= 0 && columnIndex < columnShelfCounts.Count && columnIndex < columnVerticalOffsets.Count;
    }

    private int ResolveRuntimeShelfCountForLayout()
    {
        if (columnShelfCounts.Count > 0)
        {
            return Mathf.Max(1, GetTotalShelvesFromColumns());
        }

        if (spawnedShelves.Count > 0)
        {
            return Mathf.Max(1, spawnedShelves.Count);
        }

        return Mathf.Max(1, configuredTotalShelfCount);
    }

    private int GetTotalShelvesFromColumns()
    {
        var total = 0;
        for (var i = 0; i < columnShelfCounts.Count; i++)
        {
            total += Mathf.Max(0, columnShelfCounts[i]);
        }

        return total;
    }

    private void EnsureColumnConfigState(int shelfCount, bool forceRebuildEven)
    {
        var targetShelfCount = Mathf.Max(1, shelfCount);
        var targetColumnCount = Mathf.Clamp(shelfColumnCount, 1, targetShelfCount);
        shelfColumnCount = targetColumnCount;

        while (columnShelfCounts.Count < targetColumnCount)
        {
            columnShelfCounts.Add(0);
        }

        while (columnVerticalOffsets.Count < targetColumnCount)
        {
            columnVerticalOffsets.Add(0f);
        }

        if (columnShelfCounts.Count > targetColumnCount)
        {
            columnShelfCounts.RemoveRange(targetColumnCount, columnShelfCounts.Count - targetColumnCount);
        }

        if (columnVerticalOffsets.Count > targetColumnCount)
        {
            columnVerticalOffsets.RemoveRange(targetColumnCount, columnVerticalOffsets.Count - targetColumnCount);
        }

        for (var i = 0; i < columnVerticalOffsets.Count; i++)
        {
            columnVerticalOffsets[i] = Mathf.Clamp(columnVerticalOffsets[i], -maxColumnVerticalOffset, maxColumnVerticalOffset);
        }

        var total = GetTotalShelvesFromColumns();
        if (forceRebuildEven || total != targetShelfCount)
        {
            RebuildColumnConfigsEvenly(targetShelfCount);
        }
    }

    private void RebuildColumnConfigsEvenly(int targetShelfCount)
    {
        var shelfCount = Mathf.Max(1, targetShelfCount);
        var columnCount = Mathf.Clamp(shelfColumnCount, 1, shelfCount);
        shelfColumnCount = columnCount;

        columnShelfCounts.Clear();
        columnVerticalOffsets.Clear();

        var baseCount = shelfCount / columnCount;
        var remain = shelfCount % columnCount;
        for (var i = 0; i < columnCount; i++)
        {
            var count = baseCount + (i < remain ? 1 : 0);
            columnShelfCounts.Add(Mathf.Max(0, count));
            columnVerticalOffsets.Add(0f);
        }
    }

    private void RefreshShelvesByCurrentColumnConfig()
    {
        var targetShelfCount = Mathf.Max(1, GetTotalShelvesFromColumns());
        configuredTotalShelfCount = targetShelfCount;
        configuredEmptyShelfCount = Mathf.Clamp(configuredEmptyShelfCount, 0, Mathf.Max(0, targetShelfCount - 1));
        configuredFilledShelfCount = Mathf.Clamp(configuredFilledShelfCount, 1, Mathf.Max(1, targetShelfCount - configuredEmptyShelfCount));
        skipDesignedLayoutOnce = true;
        RefreshShelves(targetShelfCount);
    }

    private Button EnsureSimpleButton(string objectName, Vector2 size, Vector2 anchoredPos, UnityEngine.Events.UnityAction onClick)
    {
        Button button = null;
        var allButtons = FindObjectsOfType<Button>(true);
        for (var i = 0; i < allButtons.Length; i++)
        {
            if (allButtons[i] != null && allButtons[i].name == objectName)
            {
                button = allButtons[i];
                break;
            }
        }

        if (button == null)
        {
            var canvas = EnsureUICanvas();
            var buttonObj = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(canvas.transform, false);

            var image = buttonObj.GetComponent<Image>();
            image.color = new Color(0.16f, 0.2f, 0.28f, 0.92f);

            button = buttonObj.GetComponent<Button>();
            var colors = button.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.38f, 0.96f);
            colors.pressedColor = new Color(0.1f, 0.14f, 0.2f, 1f);
            colors.selectedColor = colors.highlightedColor;
            button.colors = colors;

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(buttonObj.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        var rect = button.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPos;
        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);
        return button;
    }

    private Text CreatePanelText(RectTransform root, string name, Vector2 anchoredPos, Vector2 size)
    {
        var textObj = new GameObject(name, typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(root, false);
        var rect = textObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 24;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private Button CreatePanelButton(RectTransform root, string name, string labelText, Vector2 anchoredPos, Vector2 size, UnityEngine.Events.UnityAction onClick)
    {
        var buttonObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObj.transform.SetParent(root, false);
        var rect = buttonObj.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;

        var image = buttonObj.GetComponent<Image>();
        image.color = new Color(0.16f, 0.2f, 0.28f, 0.92f);

        var button = buttonObj.GetComponent<Button>();
        var colors = button.colors;
        colors.normalColor = image.color;
        colors.highlightedColor = new Color(0.22f, 0.28f, 0.38f, 0.96f);
        colors.pressedColor = new Color(0.1f, 0.14f, 0.2f, 1f);
        colors.selectedColor = colors.highlightedColor;
        button.colors = colors;

        var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textObj.transform.SetParent(buttonObj.transform, false);
        var textRect = textObj.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        var text = textObj.GetComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 20;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.text = labelText;

        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);
        return button;
    }

    private void RenameActiveTrucks()
    {
        for (var i = 0; i < activeTrucks.Count; i++)
        {
            var truck = activeTrucks[i];
            if (truck == null || truck.truck == null)
            {
                continue;
            }

            truck.truck.name = $"Truck_{i + 1}";
        }
    }

    private void SetupCatFlowForCurrentBoard()
    {
        ShowDimOverlay();

        var allBoxes = CollectAllRuntimeBoxes();
        for (var i = 0; i < allBoxes.Count; i++)
        {
            var state = allBoxes[i] != null ? allBoxes[i].GetComponent<BoxVisualState>() : null;
            if (state != null)
            {
                state.SetCatHidden(false);
            }
        }

        if (allBoxes.Count == 0)
        {
            return;
        }

        if (catIntroRoutine != null)
        {
            StopCoroutine(catIntroRoutine);
            catIntroRoutine = null;
        }

        if (catHintRoutine != null)
        {
            StopCoroutine(catHintRoutine);
            catHintRoutine = null;
        }

        catIntroRoutine = StartCoroutine(PlayCatIntroRoutine());

        catHintRoutine = StartCoroutine(ShowCatHintRoutine());
    }

    private List<Transform> CollectAllRuntimeBoxes()
    {
        var list = new List<Transform>();
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                continue;
            }

            for (var b = 0; b < shelf.StackRoot.childCount; b++)
            {
                var box = shelf.StackRoot.GetChild(b);
                if (box != null)
                {
                    list.Add(box);
                }
            }
        }

        return list;
    }

    private IEnumerator PlayCatIntroRoutine()
    {
        StopCatIntroVisual();

        if (catPrefab == null)
        {
            yield break;
        }

        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            yield break;
        }

        var canvasRect = canvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            yield break;
        }

        var catIntroNode = new GameObject("CatIntroUI", typeof(RectTransform), typeof(Image));
        catIntroNode.transform.SetParent(canvas.transform, false);
        runtimeCatIntroUi = catIntroNode;

        var image = catIntroNode.GetComponent<Image>();
        if (!TryResolveCatVisual(out var catSprite, out var catColor))
        {
            catColor = Color.white;
        }

        image.sprite = catSprite;
        image.color = catColor;
        image.preserveAspect = true;
        image.raycastTarget = false;

        var rect = catIntroNode.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = catSprite != null
            ? new Vector2(Mathf.Max(80f, catSprite.rect.width) * 2f, Mathf.Max(80f, catSprite.rect.height) * 2f)
            : new Vector2(360f, 360f);

        var startPosition = ViewportToCanvasPosition(canvasRect, new Vector2(0.5f, 0.5f));
        rect.anchoredPosition = startPosition;
        rect.localScale = Vector3.one;
        rect.SetAsLastSibling();
        BringOverlayForegroundElements();

        if (catStayDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(catStayDuration);
        }

        HideDimOverlay();

        if (runtimeCatIntroUi == null)
        {
            yield break;
        }

        var minScale = rect.localScale * Mathf.Clamp(catMinScaleFactor, 0.01f, 1f);
        while (runtimeCatIntroUi != null)
        {
            if ((rect.localScale - minScale).sqrMagnitude <= 0.000001f)
            {
                rect.localScale = minScale;
                break;
            }

            var scaleStep = Mathf.Max(0.01f, catShrinkSpeed) * Time.unscaledDeltaTime;
            rect.localScale = Vector3.MoveTowards(rect.localScale, minScale, scaleStep);

            yield return null;
        }

        StopCatIntroVisual();
        catIntroRoutine = null;
    }

    private IEnumerator ShowCatHintRoutine()
    {
        var hintText = EnsureOverlayText(CatUiType.Hint);
        if (hintText != null)
        {
            hintText.text = catHintMessage;
            //设置提示文案的位置
            hintText.rectTransform.anchoredPosition = new Vector2(0f, -400f);
            hintText.gameObject.SetActive(true);
        }

        var elapsed = 0f;
        var duration = Mathf.Max(0f, catHintDuration);
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        HideHintMessage();
        catHintRoutine = null;
    }

    private bool IsAnyPointerDown()
    {
        if (Input.GetMouseButtonDown(0))
        {
            return true;
        }

        if (Input.touchCount <= 0)
        {
            return false;
        }

        for (var i = 0; i < Input.touchCount; i++)
        {
            if (Input.GetTouch(i).phase == TouchPhase.Began)
            {
                return true;
            }
        }

        return false;
    }

    private Text EnsureOverlayText(CatUiType type)
    {
        if (type == CatUiType.Hint && catHintTextRef != null)
        {
            return catHintTextRef;
        }

        if (type == CatUiType.Win && catWinTextRef != null)
        {
            return catWinTextRef;
        }

        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return null;
        }

        var objectName = type == CatUiType.Hint ? "CatHintText" : "CatWinText";
        var existing = canvas.transform.Find(objectName);
        Text text;
        if (existing != null)
        {
            text = existing.GetComponent<Text>();
            if (text == null)
            {
                text = existing.gameObject.AddComponent<Text>();
            }
        }
        else
        {
            var go = new GameObject(objectName, typeof(RectTransform), typeof(Text));
            go.transform.SetParent(canvas.transform, false);
            text = go.GetComponent<Text>();
        }

        var rect = text.GetComponent<RectTransform>();
        if (type == CatUiType.Hint)
        {
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.sizeDelta = new Vector2(960f, 80f);
            rect.anchoredPosition = new Vector2(0f, -110f);
            text.fontSize = 42;
        }
        else
        {
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(1100f, 120f);
            rect.anchoredPosition = Vector2.zero;
            text.fontSize = 52;
            text.fontStyle = FontStyle.Bold;
        }

        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        if (text.font == null)
        {
            var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null)
            {
                builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.font = builtinFont;
        }

        text.gameObject.SetActive(false);

        if (type == CatUiType.Hint)
        {
            catHintTextRef = text;
        }
        else
        {
            catWinTextRef = text;
        }

        BringOverlayForegroundElements();

        return text;
    }

    private void ShowDimOverlay()
    {
        var overlay = EnsureDimOverlay();
        if (overlay == null)
        {
            return;
        }

        overlay.gameObject.SetActive(true);
        BringOverlayForegroundElements();
    }

    private void HideDimOverlay()
    {
        if (dimOverlayImage != null)
        {
            dimOverlayImage.gameObject.SetActive(false);
        }
    }

    private Image EnsureDimOverlay()
    {
        if (dimOverlayImage != null)
        {
            return dimOverlayImage;
        }

        var canvas = EnsureUICanvas();
        if (canvas == null)
        {
            return null;
        }

        var existing = canvas.transform.Find("DimOverlay");
        if (existing != null)
        {
            dimOverlayImage = existing.GetComponent<Image>();
            if (dimOverlayImage == null)
            {
                dimOverlayImage = existing.gameObject.AddComponent<Image>();
            }
        }
        else
        {
            var overlayObj = new GameObject("DimOverlay", typeof(RectTransform), typeof(Image));
            overlayObj.transform.SetParent(canvas.transform, false);
            dimOverlayImage = overlayObj.GetComponent<Image>();
        }

        var rect = dimOverlayImage.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        dimOverlayImage.color = new Color(0f, 0f, 0f, 0.6f);
        dimOverlayImage.raycastTarget = false;
        dimOverlayImage.transform.SetSiblingIndex(0);

        return dimOverlayImage;
    }

    private void BringOverlayForegroundElements()
    {
        if (dimOverlayImage != null)
        {
            dimOverlayImage.transform.SetSiblingIndex(0);
        }

        if (regenerateButton != null)
        {
            regenerateButton.transform.SetAsLastSibling();
        }

        if (previousLevelButton != null)
        {
            previousLevelButton.transform.SetAsLastSibling();
        }

        if (nextLevelButton != null)
        {
            nextLevelButton.transform.SetAsLastSibling();
        }

        if (levelIndicatorTextRef != null)
        {
            levelIndicatorTextRef.transform.SetAsLastSibling();
        }

        if (designToolsPanelRoot != null)
        {
            designToolsPanelRoot.transform.SetAsLastSibling();
        }

        if (modeToggleButton != null)
        {
            modeToggleButton.transform.SetAsLastSibling();
        }

        if (restartSettingsButton != null)
        {
            restartSettingsButton.transform.SetAsLastSibling();
        }

        if (testClearSaveButton != null)
        {
            testClearSaveButton.transform.SetAsLastSibling();
        }

        if (winResultPanelRoot != null)
        {
            winResultPanelRoot.transform.SetAsLastSibling();
        }

        if (coinHudTextRef != null)
        {
            coinHudTextRef.transform.SetAsLastSibling();
        }

        if (staminaHudTextRef != null)
        {
            staminaHudTextRef.transform.SetAsLastSibling();
        }

        if (catHintTextRef != null)
        {
            catHintTextRef.transform.SetAsLastSibling();
        }

        if (catWinTextRef != null)
        {
            catWinTextRef.transform.SetAsLastSibling();
        }

        if (runtimeCatIntroUi != null)
        {
            runtimeCatIntroUi.transform.SetAsLastSibling();
        }

        if (restartSettingsOverlayRoot != null && restartSettingsOverlayRoot.gameObject.activeInHierarchy)
        {
            restartSettingsOverlayRoot.transform.SetAsLastSibling();
        }

        if (staminaInsufficientBubbleRoot != null && staminaInsufficientBubbleRoot.gameObject.activeInHierarchy)
        {
            staminaInsufficientBubbleRoot.transform.SetAsLastSibling();
        }
    }

    private static Vector2 ViewportToCanvasPosition(RectTransform canvasRect, Vector2 viewport)
    {
        var x = Mathf.Lerp(-canvasRect.rect.width * 0.5f, canvasRect.rect.width * 0.5f, viewport.x);
        var y = Mathf.Lerp(-canvasRect.rect.height * 0.5f, canvasRect.rect.height * 0.5f, viewport.y);
        return new Vector2(x, y);
    }

    private static Vector3 ResolveTargetCenterWorldPosition(Transform targetBox)
    {
        if (targetBox == null)
        {
            return Vector3.zero;
        }

        var renderer = targetBox.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            return renderer.bounds.center;
        }

        var spriteRenderer = targetBox.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            return spriteRenderer.bounds.center;
        }

        var collider2D = targetBox.GetComponentInChildren<Collider2D>(true);
        if (collider2D != null)
        {
            return collider2D.bounds.center;
        }

        return targetBox.position;
    }

    private bool TryResolveCatVisual(out Sprite sprite, out Color color)
    {
        sprite = null;
        color = Color.white;
        if (catPrefab == null)
        {
            return false;
        }

        var uiImage = catPrefab.GetComponentInChildren<Image>(true);
        if (uiImage != null && uiImage.sprite != null)
        {
            sprite = uiImage.sprite;
            color = uiImage.color;
            return true;
        }

        var spriteRenderer = catPrefab.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            sprite = spriteRenderer.sprite;
            color = spriteRenderer.color;
            return sprite != null;
        }

        return false;
    }

    private void HideHintMessage()
    {
        if (catHintTextRef != null)
        {
            catHintTextRef.gameObject.SetActive(false);
        }
    }

    private void HideWinMessage()
    {
        HideWinResultPanel();
        if (catWinTextRef != null)
        {
            catWinTextRef.gameObject.SetActive(false);
        }
    }

    private void StopCatIntroVisual()
    {
        if (runtimeCatIntroUi != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeCatIntroUi);
            }
            else
            {
                DestroyImmediate(runtimeCatIntroUi);
            }

            runtimeCatIntroUi = null;
        }

        if (runtimeCatIntro != null)
        {
            if (Application.isPlaying)
            {
                Destroy(runtimeCatIntro);
            }
            else
            {
                DestroyImmediate(runtimeCatIntro);
            }

            runtimeCatIntro = null;
        }
    }

    private bool HasCatHidden(Transform box)
    {
        if (box == null)
        {
            return false;
        }

        var state = box.GetComponent<BoxVisualState>();
        return state != null && state.HasCatHidden;
    }

    private void TriggerGameWin()
    {
        if (gameWon)
        {
            return;
        }

        isFinalDesignedLevelWin = IsCurrentLevelFinalDesignedLevel();
        gameWon = true;
        var reward = GrantCoinsByWin();
        if (catIntroRoutine != null)
        {
            StopCoroutine(catIntroRoutine);
            catIntroRoutine = null;
        }

        if (catHintRoutine != null)
        {
            StopCoroutine(catHintRoutine);
            catHintRoutine = null;
        }

        StopCatIntroVisual();
        HideHintMessage();
        ShowDimOverlay();
        ShowWinResultPanel(reward);

        var allBoxInteractions = FindObjectsOfType<BoxInteractionController>(true);
        for (var i = 0; i < allBoxInteractions.Length; i++)
        {
            if (allBoxInteractions[i] != null)
            {
                allBoxInteractions[i].enabled = false;
            }
        }

        Time.timeScale = 0f;
        LogInfo($"Game win triggered: cat box reached truck | finalDesignedLevelWin={isFinalDesignedLevelWin}");
    }

    private List<Vector3> GenerateShelfPositions(Camera cameraRef, int shelfCount)
    {
        var positions = new List<Vector3>(shelfCount);
        if (shelfCount <= 0)
        {
            return positions;
        }

        EnsureColumnConfigState(shelfCount, false);
        var columnCount = Mathf.Clamp(shelfColumnCount, 1, shelfCount);

        var horizontalPad = Mathf.Clamp(viewportHorizontalPadding, 0.02f, 0.35f);
        var verticalPad = Mathf.Clamp(viewportVerticalPadding, 0.02f, 0.35f);

        var left = horizontalPad;
        var right = 1f - horizontalPad;
        var top = 1f - verticalPad;
        var bottom = verticalPad;

        var topWorldY = ViewportToWorldOnPlane(cameraRef, new Vector2(0.5f, top)).y;
        var bottomWorldY = ViewportToWorldOnPlane(cameraRef, new Vector2(0.5f, bottom)).y;
        var worldTop = Mathf.Max(topWorldY, bottomWorldY);
        var worldBottom = Mathf.Min(topWorldY, bottomWorldY);
        var availableWorldHeight = Mathf.Max(0.01f, worldTop - worldBottom);

        var shelfHalfExtents = ResolveShelfHalfExtents();
        var shelfHeight = Mathf.Max(0.5f, shelfHalfExtents.y * 2f);
        var baseCenterStep = shelfHeight + Mathf.Max(0f, minShelfDistance);
        var desiredCenterStep = Mathf.Max(shelfHeight * 0.6f, baseCenterStep * Mathf.Max(0.5f, shelfVerticalSpacingScale) + shelfVerticalGapOffset);

        for (var column = 0; column < columnCount; column++)
        {
            var xT = columnCount <= 1 ? 0.5f : (float)column / (columnCount - 1);
            var viewportX = Mathf.Lerp(left, right, xT);
            var worldX = ViewportToWorldOnPlane(cameraRef, new Vector2(viewportX, 0.5f)).x;
            var shelfPerColumn = column < columnShelfCounts.Count ? Mathf.Max(0, columnShelfCounts[column]) : 0;
            var offset = column < columnVerticalOffsets.Count ? columnVerticalOffsets[column] : 0f;
            var centerY = (worldTop + worldBottom) * 0.5f + offset * availableWorldHeight;

            for (var row = 0; row < shelfPerColumn; row++)
            {
                float worldY;
                if (shelfPerColumn <= 1)
                {
                    worldY = Mathf.Clamp(centerY, worldBottom, worldTop);
                }
                else
                {
                    var maxStepByViewport = availableWorldHeight / (shelfPerColumn - 1);
                    var finalStep = Mathf.Min(desiredCenterStep, Mathf.Max(0.01f, maxStepByViewport));
                    var totalSpan = finalStep * (shelfPerColumn - 1);
                    var maxCenter = worldTop - totalSpan * 0.5f;
                    var minCenter = worldBottom + totalSpan * 0.5f;
                    var clampedCenter = Mathf.Clamp(centerY, minCenter, maxCenter);
                    worldY = clampedCenter + totalSpan * 0.5f - finalStep * row;
                }

                positions.Add(new Vector3(worldX, worldY, spawnPlaneZ));
            }
        }

        while (positions.Count < shelfCount)
        {
            positions.Add(ViewportToWorldOnPlane(cameraRef, new Vector2(0.5f, 0.5f)));
        }

        if (positions.Count > shelfCount)
        {
            positions.RemoveRange(shelfCount, positions.Count - shelfCount);
        }

        return positions;
    }

    private bool IsOverlapping(Vector3 candidate, List<Vector3> existingPositions, Vector2 halfExtents, float extraGap)
    {
        var requiredX = halfExtents.x * 2f + extraGap;
        var requiredY = halfExtents.y * 2f + extraGap;

        for (var i = 0; i < existingPositions.Count; i++)
        {
            var delta = candidate - existingPositions[i];
            if (Mathf.Abs(delta.x) < requiredX && Mathf.Abs(delta.y) < requiredY)
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindGridFallbackPosition(
        Camera cameraRef,
        List<Vector3> existingPositions,
        Vector2 halfExtents,
        float extraGap,
        float horizontalPad,
        float verticalPad,
        out Vector3 position)
    {
        var gridStepX = Mathf.Max(halfExtents.x * 2f + extraGap, 0.2f);
        var gridStepY = Mathf.Max(halfExtents.y * 2f + extraGap, 0.2f);
        var topLeft = ViewportToWorldOnPlane(cameraRef, new Vector2(horizontalPad, 1f - verticalPad));
        var bottomRight = ViewportToWorldOnPlane(cameraRef, new Vector2(1f - horizontalPad, verticalPad));

        var minX = Mathf.Min(topLeft.x, bottomRight.x);
        var maxX = Mathf.Max(topLeft.x, bottomRight.x);
        var minY = Mathf.Min(topLeft.y, bottomRight.y);
        var maxY = Mathf.Max(topLeft.y, bottomRight.y);

        for (var y = maxY; y >= minY; y -= gridStepY)
        {
            for (var x = minX; x <= maxX; x += gridStepX)
            {
                var candidate = new Vector3(x, y, spawnPlaneZ);
                if (!IsOverlapping(candidate, existingPositions, halfExtents, extraGap))
                {
                    position = candidate;
                    return true;
                }
            }
        }

        position = Vector3.zero;
        return false;
    }

    private int ResolveSpawnSeed(int shelfCount)
    {
        if (!randomizeOnEachRefresh)
        {
            return spawnSeed + shelfCount * 97;
        }

        refreshSequence++;
        var timeSeed = unchecked((int)DateTime.UtcNow.Ticks);
        return spawnSeed ^ (shelfCount * 97) ^ (refreshSequence * 7919) ^ timeSeed;
    }

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void EnsureRegenerateButton()
    {
        if (!showRegenerateButton)
        {
            if (regenerateButton != null)
            {
                regenerateButton.gameObject.SetActive(false);
            }

            return;
        }

        if (regenerateButton == null)
        {
            var allButtons = FindObjectsOfType<Button>(true);
            for (var i = 0; i < allButtons.Length; i++)
            {
                if (allButtons[i] != null && allButtons[i].name == "RegenerateShelfButton")
                {
                    regenerateButton = allButtons[i];
                    break;
                }
            }
        }

        if (regenerateButton == null)
        {
            var canvas = EnsureUICanvas();
            var buttonObj = new GameObject("RegenerateShelfButton", typeof(RectTransform), typeof(Image), typeof(Button));
            buttonObj.transform.SetParent(canvas.transform, false);

            var buttonRect = buttonObj.GetComponent<RectTransform>();
            buttonRect.anchorMin = new Vector2(0f, 1f);
            buttonRect.anchorMax = new Vector2(0f, 1f);
            buttonRect.pivot = new Vector2(0.5f, 1f);
            buttonRect.sizeDelta = regenerateButtonSize;
            buttonRect.anchoredPosition = regenerateButtonPosition;

            var image = buttonObj.GetComponent<Image>();
            image.color = new Color(0.16f, 0.2f, 0.28f, 0.92f);

            regenerateButton = buttonObj.GetComponent<Button>();
            var colors = regenerateButton.colors;
            colors.normalColor = image.color;
            colors.highlightedColor = new Color(0.22f, 0.28f, 0.38f, 0.96f);
            colors.pressedColor = new Color(0.1f, 0.14f, 0.2f, 1f);
            colors.selectedColor = colors.highlightedColor;
            regenerateButton.colors = colors;

            var textObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            textObj.transform.SetParent(buttonObj.transform, false);
            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;

            var text = textObj.GetComponent<Text>();
            text.text = regenerateButtonText;
            var builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (builtinFont == null)
            {
                builtinFont = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }

            text.font = builtinFont;
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
        }

        ApplyRegenerateButtonVisibility();
        var rect = regenerateButton.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.sizeDelta = regenerateButtonSize;
        rect.anchoredPosition = regenerateButtonPosition;

        var label = regenerateButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = regenerateButtonText;
        }

        regenerateButton.onClick.RemoveListener(RestartRoundByButton);
        regenerateButton.onClick.RemoveListener(RegenerateShelves);
        regenerateButton.onClick.AddListener(RestartRoundByButton);
        BringOverlayForegroundElements();
    }

    private void ApplyRegenerateButtonVisibility()
    {
        if (regenerateButton == null)
        {
            return;
        }

        regenerateButton.gameObject.SetActive(showRegenerateButton && !hidePrimaryControlButtons);
    }

    private void EnsureGameplayActionButtons()
    {
        if (!showGameplayActionButtons)
        {
            if (refreshActionButton != null)
            {
                refreshActionButton.gameObject.SetActive(false);
            }

            if (undoActionButton != null)
            {
                undoActionButton.gameObject.SetActive(false);
            }

            if (addEmptyShelfActionButton != null)
            {
                addEmptyShelfActionButton.gameObject.SetActive(false);
            }

            return;
        }

        refreshActionButton = EnsureGameplayActionButton(
            refreshActionButton,
            "RefreshActionButton",
            refreshActionButtonText,
            refreshActionButtonPosition,
            RefreshShelfColorByButton);

        undoActionButton = EnsureGameplayActionButton(
            undoActionButton,
            "UndoActionButton",
            undoActionButtonText,
            undoActionButtonPosition,
            UndoLastMoveByButton);

        addEmptyShelfActionButton = EnsureGameplayActionButton(
            addEmptyShelfActionButton,
            "AddEmptyShelfActionButton",
            addEmptyShelfActionButtonText,
            addEmptyShelfActionButtonPosition,
            AddEmptyShelfByButton);

        ApplyGameplayActionButtonsVisibility();
    }

    private Button EnsureGameplayActionButton(
        Button existing,
        string objectName,
        string buttonText,
        Vector2 anchoredPos,
        UnityEngine.Events.UnityAction onClick)
    {
        var button = existing;
        var createdByManager = false;

        if (button == null)
        {
            var allButtons = FindObjectsOfType<Button>(true);
            for (var i = 0; i < allButtons.Length; i++)
            {
                if (allButtons[i] != null && allButtons[i].name == objectName)
                {
                    button = allButtons[i];
                    break;
                }
            }
        }

        if (button == null)
        {
            button = EnsureSimpleButton(objectName, gameplayActionButtonSize, anchoredPos, onClick);
            createdByManager = true;
        }

        if (button == null)
        {
            return null;
        }

        button.onClick.RemoveListener(onClick);
        button.onClick.AddListener(onClick);

        if (createdByManager)
        {
            var rect = button.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.sizeDelta = gameplayActionButtonSize;
            rect.anchoredPosition = anchoredPos;

            var label = button.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = buttonText;
            }
        }

        return button;
    }

    private void ApplyGameplayActionButtonsVisibility()
    {
        var visible = showGameplayActionButtons && runtimeMode == RuntimeMode.GameMode;
        if (refreshActionButton != null)
        {
            refreshActionButton.gameObject.SetActive(visible);
        }

        if (undoActionButton != null)
        {
            undoActionButton.gameObject.SetActive(visible);
        }

        if (addEmptyShelfActionButton != null)
        {
            addEmptyShelfActionButton.gameObject.SetActive(visible);
        }
    }

    private Canvas EnsureUICanvas()
    {
        var canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasObj = new GameObject("UIRoot", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        EnsureEventSystem();
        return canvas;
    }

    private void EnsureEventSystem()
    {
        var eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem != null)
        {
            return;
        }

        var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem));
        TryAttachInputModule(eventSystemObj);
        eventSystemObj.transform.SetParent(null, false);
    }

    private void TryAttachInputModule(GameObject eventSystemObj)
    {
        if (eventSystemObj == null)
        {
            return;
        }

#if UNITY_EDITOR
        eventSystemObj.AddComponent<StandaloneInputModule>();
#else
        if (platformContext != null && platformContext.Current != PlatformType.Common)
        {
            var touchModuleType = Type.GetType("UnityEngine.EventSystems.TouchInputModule, UnityEngine.UI");
            if (touchModuleType != null && typeof(BaseInputModule).IsAssignableFrom(touchModuleType))
            {
                eventSystemObj.AddComponent(touchModuleType);
                return;
            }
        }

        eventSystemObj.AddComponent<StandaloneInputModule>();
#endif
    }

    private Vector3 ViewportToWorldOnPlane(Camera cameraRef, Vector2 viewportPoint)
    {
        var depth = Mathf.Abs(spawnPlaneZ - cameraRef.transform.position.z);
        if (depth < 0.01f)
        {
            depth = 10f;
        }

        var world = cameraRef.ViewportToWorldPoint(new Vector3(viewportPoint.x, viewportPoint.y, depth));
        world.z = spawnPlaneZ;
        return world;
    }

    private Vector2 ResolveShelfHalfExtents()
    {
        if (shelfPrefab == null)
        {
            var defaultHalf = 0.5f * Mathf.Max(1f, distanceScaleFromPrefab);
            return new Vector2(defaultHalf, defaultHalf);
        }

        var renderers = shelfPrefab.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            var defaultHalf = 0.5f * Mathf.Max(1f, distanceScaleFromPrefab);
            return new Vector2(defaultHalf, defaultHalf);
        }

        var bounds = renderers[0].bounds;
        for (var i = 1; i < renderers.Length; i++)
        {
            bounds.Encapsulate(renderers[i].bounds);
        }

        var scale = Mathf.Max(1f, distanceScaleFromPrefab);
        var halfX = Mathf.Max(0.25f, bounds.extents.x * scale);
        var halfY = Mathf.Max(0.25f, bounds.extents.y * scale);
        return new Vector2(halfX, halfY);
    }

    private void SpawnBoxesForShelf(GameObject shelf, IReadOnlyList<GameManager.BoxColor> shelfColors, int shelfGrayCount, int targetBoxCount)
    {
        if (boxPrefab == null || shelf == null)
        {
            if (boxPrefab == null)
            {
                LogWarn($"Skip box spawn: boxPrefab is NULL on shelf {(shelf != null ? shelf.name : "NULL")}", this);
            }

            return;
        }

        if (!TryGetShelfAnchorRange(shelf.transform, out var shelfBottom, out var shelfTop))
        {
            LogWarn($"货架 {shelf.name} 缺少有效锚点，已跳过箱子堆叠。", shelf);
            return;
        }

        LogInfo($"Spawn boxes on {shelf.name} | shelfBottom={shelfBottom} | shelfTop={shelfTop}", shelf);

        var stackRoot = EnsureBoxRootNode(shelf.transform);
        var boxCount = Mathf.Max(0, targetBoxCount);
        if (boxCount <= 0)
        {
            return;
        }

        var clampedGrayCount = Mathf.Clamp(shelfGrayCount, 0, Mathf.Max(0, boxCount - 1));

        var nextBottom = shelfBottom;
        var limitY = Mathf.Max(shelfBottom.y, shelfTop.y);
        var spawnedBoxCount = 0;
        for (var i = 0; i < boxCount; i++)
        {
            var rotation = alignBoxRotationToShelf ? shelf.transform.rotation : boxPrefab.transform.rotation;
            var box = Instantiate(boxPrefab, nextBottom, rotation, stackRoot);
            box.name = $"Box_{i + 1}";

            if (!TryAlignBoxAndGetTop(box.transform, nextBottom, out var boxTop))
            {
                LogWarn($"箱子 {box.name} 缺少有效锚点，已按当前位置放置。", box);
                break;
            }

            var colorType = ResolveColorType(shelfColors, i);
            var shouldGray = i < clampedGrayCount;
            ApplyColorForBox(box.transform, colorType, shouldGray);
            EnsureInteractionForBox(box.transform);

            ApplySortingForBox(box.transform, i);
            spawnedBoxCount++;
            LogInfo($"Box spawned: {box.name} | pos={box.transform.position} | nextBottom={boxTop} | sorting={boxSortingOrderStart + Mathf.Clamp(i, 0, 3)}", box);

            if (boxTop.y > limitY + 0.001f)
            {
                LogWarn($"货架 {shelf.name} 的箱子堆叠超过顶部锚点，请检查锚点或减少 boxesPerShelf。", shelf);
            }

            nextBottom = boxTop;
        }

        LogInfo($"Spawn boxes finished on {shelf.name} | spawned={spawnedBoxCount}", shelf);
    }

    private GameManager.BoxColor ResolveColorType(IReadOnlyList<GameManager.BoxColor> shelfColors, int boxIndex)
    {
        var pool = ResolveLimitedColorPool();
        if (shelfColors != null && boxIndex >= 0 && boxIndex < shelfColors.Count)
        {
            var source = shelfColors[boxIndex];
            if (pool.Count <= 0)
            {
                return source;
            }

            var sourceIndex = pool.IndexOf(source);
            if (sourceIndex >= 0)
            {
                return source;
            }

            var fallbackIndex = Mathf.Abs(boxIndex) % pool.Count;
            return pool[fallbackIndex];
        }

        if (pool.Count <= 0)
        {
            return GameManager.BoxColor.Red;
        }

        var idx = Mathf.Abs(boxIndex) % pool.Count;
        return pool[idx];
    }

    private void ApplyColorForBox(Transform boxTransform, GameManager.BoxColor colorType, bool grayed)
    {
        var originalColor = ResolveDefaultDisplayColor(colorType);
        if (boxGenerationManager != null)
        {
            boxGenerationManager.TryGetDisplayColor(colorType, out originalColor);
        }

        var displayColor = grayed && boxGenerationManager != null ? boxGenerationManager.GrayDisplayColor : originalColor;

        var spriteRenderers = boxTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].color = displayColor;
        }

        var renderers = boxTransform.GetComponentsInChildren<Renderer>(true);
        var propertyBlock = new MaterialPropertyBlock();
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is SpriteRenderer)
            {
                continue;
            }

            renderers[i].GetPropertyBlock(propertyBlock);
            var hasBaseColor = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_BaseColor");
            var hasColor = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_Color");

            if (hasBaseColor)
            {
                propertyBlock.SetColor("_BaseColor", displayColor);
            }

            if (hasColor)
            {
                propertyBlock.SetColor("_Color", displayColor);
            }

            renderers[i].SetPropertyBlock(propertyBlock);
        }

        var visualState = boxTransform.GetComponent<BoxVisualState>();
        if (visualState == null)
        {
            visualState = boxTransform.gameObject.AddComponent<BoxVisualState>();
        }

        visualState.SetState(colorType, originalColor, displayColor, grayed);
    }

    private void EnsureInteractionForBox(Transform boxTransform)
    {
        if (boxTransform == null)
        {
            return;
        }

        var enableInteraction = !IsLevelDesignMode;

        var collider2D = boxTransform.GetComponent<Collider2D>();
        if (collider2D == null)
        {
            collider2D = boxTransform.gameObject.AddComponent<BoxCollider2D>();
        }
        collider2D.enabled = enableInteraction;

        var interaction = boxTransform.GetComponent<BoxInteractionController>();
        if (interaction == null)
        {
            interaction = boxTransform.gameObject.AddComponent<BoxInteractionController>();
        }

        interaction.enabled = enableInteraction;
    }

    private void EnsureShelfInteractionForRuntime(GameObject shelf, int shelfIndex)
    {
        if (shelf == null)
        {
            return;
        }

        SetLayerRecursively(shelf.transform, LayerMask.NameToLayer("Ignore Raycast"));
        EnsureShelfCollider(shelf.transform);

        var stackRoot = EnsureBoxRootNode(shelf.transform);
        var shelfInteraction = shelf.GetComponent<ShelfInteractionController>();
        if (shelfInteraction == null)
        {
            shelfInteraction = shelf.AddComponent<ShelfInteractionController>();
        }

        shelfInteraction.Initialize(shelfIndex, 4, stackRoot);

        var rootRef = stackRoot.GetComponent<ShelfStackRootRef>();
        if (rootRef == null)
        {
            rootRef = stackRoot.gameObject.AddComponent<ShelfStackRootRef>();
        }

        rootRef.Bind(shelfInteraction);
        shelfInteraction.RefreshBoxVisualStates();
    }

    private static void SetLayerRecursively(Transform root, int targetLayer)
    {
        if (root == null || targetLayer < 0)
        {
            return;
        }

        root.gameObject.layer = targetLayer;
        for (var i = 0; i < root.childCount; i++)
        {
            SetLayerRecursively(root.GetChild(i), targetLayer);
        }
    }

    private static void EnsureShelfCollider(Transform shelfTransform)
    {
        if (shelfTransform == null)
        {
            return;
        }

        var boxCollider = shelfTransform.GetComponent<BoxCollider2D>();
        if (boxCollider == null)
        {
            boxCollider = shelfTransform.gameObject.AddComponent<BoxCollider2D>();
        }

        if (TryGetCombinedBounds(shelfTransform, out var bounds))
        {
            var centerLocal = shelfTransform.InverseTransformPoint(bounds.center);
            var lossyScale = shelfTransform.lossyScale;
            var scaleX = Mathf.Abs(lossyScale.x);
            var scaleY = Mathf.Abs(lossyScale.y);
            if (scaleX < 0.0001f)
            {
                scaleX = 1f;
            }

            if (scaleY < 0.0001f)
            {
                scaleY = 1f;
            }

            boxCollider.offset = new Vector2(centerLocal.x, centerLocal.y);
            boxCollider.size = new Vector2(bounds.size.x / scaleX, bounds.size.y / scaleY);
        }

        boxCollider.isTrigger = true;
    }

    private static bool TryGetCombinedBounds(Transform root, out Bounds bounds)
    {
        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return true;
        }

        var spriteRenderers = root.GetComponentsInChildren<SpriteRenderer>(true);
        if (spriteRenderers.Length > 0)
        {
            bounds = spriteRenderers[0].bounds;
            for (var i = 1; i < spriteRenderers.Length; i++)
            {
                bounds.Encapsulate(spriteRenderers[i].bounds);
            }

            return true;
        }

        bounds = default;
        return false;
    }

    private static Color ResolveDefaultDisplayColor(GameManager.BoxColor colorType)
    {
        switch (colorType)
        {
            case GameManager.BoxColor.Red:
                return new Color(0.88f, 0.25f, 0.25f, 1f);
            case GameManager.BoxColor.Blue:
                return new Color(0.27f, 0.5f, 0.9f, 1f);
            case GameManager.BoxColor.Green:
                return new Color(0.28f, 0.74f, 0.38f, 1f);
            case GameManager.BoxColor.Yellow:
                return new Color(0.95f, 0.83f, 0.24f, 1f);
            case GameManager.BoxColor.Purple:
                return new Color(0.62f, 0.39f, 0.86f, 1f);
            case GameManager.BoxColor.Orange:
                return new Color(0.95f, 0.56f, 0.21f, 1f);
            default:
                return Color.white;
        }
    }

    private bool TryGetShelfAnchorRange(Transform shelfTransform, out Vector3 bottom, out Vector3 top)
    {
        if (TryGetAnchorPair(shelfTransform, out var bottomAnchor, out var topAnchor))
        {
            bottom = bottomAnchor.position;
            top = topAnchor.position;
            LogInfo($"Use shelf anchors from StackAnchorPoints: {shelfTransform.name}", shelfTransform.gameObject);
            return true;
        }

        var renderer = shelfTransform.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            var bounds = renderer.bounds;
            bottom = new Vector3(bounds.center.x, bounds.min.y, shelfTransform.position.z);
            top = new Vector3(bounds.center.x, bounds.max.y, shelfTransform.position.z);
            LogInfo($"Use shelf renderer bounds as anchors: {shelfTransform.name}", shelfTransform.gameObject);
            return true;
        }

        var spriteRenderer = shelfTransform.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            var bounds = spriteRenderer.bounds;
            bottom = new Vector3(bounds.center.x, bounds.min.y, shelfTransform.position.z);
            top = new Vector3(bounds.center.x, bounds.max.y, shelfTransform.position.z);
            LogInfo($"Use shelf sprite bounds as anchors: {shelfTransform.name}", shelfTransform.gameObject);
            return true;
        }

        bottom = Vector3.zero;
        top = Vector3.zero;
        return false;
    }

    private bool TryAlignBoxAndGetTop(Transform boxTransform, Vector3 targetBottom, out Vector3 top)
    {
        if (TryGetAnchorPair(boxTransform, out var bottomAnchor, out var topAnchor))
        {
            var delta = targetBottom - bottomAnchor.position;
            boxTransform.position += delta;
            top = topAnchor.position;
            LogInfo($"Use box anchors: {boxTransform.name}", boxTransform.gameObject);
            return true;
        }

        var renderer = boxTransform.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            var deltaY = targetBottom.y - renderer.bounds.min.y;
            boxTransform.position += new Vector3(0f, deltaY, 0f);
            top = new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, boxTransform.position.z);
            LogInfo($"Use box renderer bounds: {boxTransform.name}", boxTransform.gameObject);
            return true;
        }

        var spriteRenderer = boxTransform.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            var deltaY = targetBottom.y - spriteRenderer.bounds.min.y;
            boxTransform.position += new Vector3(0f, deltaY, 0f);
            top = new Vector3(spriteRenderer.bounds.center.x, spriteRenderer.bounds.max.y, boxTransform.position.z);
            LogInfo($"Use box sprite bounds: {boxTransform.name}", boxTransform.gameObject);
            return true;
        }

        top = targetBottom;
        LogWarn($"No anchors or renderer found for box {boxTransform.name}", boxTransform.gameObject);
        return false;
    }

    private bool TryGetAnchorPair(Transform root, out Transform bottom, out Transform top)
    {
        bottom = null;
        top = null;

        var anchorComp = root.GetComponentInChildren<StackAnchorPoints>(true);
        if (anchorComp != null)
        {
            bottom = anchorComp.BottomAnchor;
            top = anchorComp.TopAnchor;
            if (bottom != null && top != null)
            {
                return true;
            }
        }

        bottom = FindAnchorByName(root, "bottomanchor", "anchorbottom", "bottom");
        top = FindAnchorByName(root, "topanchor", "anchortop", "top");
        return bottom != null && top != null;
    }

    private Transform FindAnchorByName(Transform root, params string[] aliases)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        for (var i = 0; i < all.Length; i++)
        {
            var n = all[i].name.Replace(" ", string.Empty).ToLowerInvariant();
            for (var j = 0; j < aliases.Length; j++)
            {
                if (n == aliases[j])
                {
                    return all[i];
                }
            }
        }

        return null;
    }

    private int CountRuntimeBoxesOnBoard()
    {
        var count = 0;
        for (var i = 0; i < spawnedShelves.Count; i++)
        {
            var shelfGo = spawnedShelves[i];
            if (shelfGo == null)
            {
                continue;
            }

            var shelf = shelfGo.GetComponent<ShelfInteractionController>();
            if (shelf == null || shelf.StackRoot == null)
            {
                continue;
            }

            count += shelf.StackRoot.childCount;
        }

        return Mathf.Max(0, count);
    }

    private bool ShouldFindCatForCurrentElimination(int currentBoxCount, int removedCount)
    {
        if (gameManager != null && gameManager.CurrentCatFindMode == GameManager.CatFindMode.OnAllBoxesCleared)
        {
            var remainingAfterRemoval = Mathf.Max(0, currentBoxCount - Mathf.Max(0, removedCount));
            return remainingAfterRemoval <= 0;
        }

        var safeBoxCount = Mathf.Max(ColorGroupSize, currentBoxCount);
        var currentGroupCount = Mathf.Max(1, Mathf.CeilToInt(safeBoxCount / (float)ColorGroupSize));
        if (currentGroupCount <= 1)
        {
            return true;
        }

        var findChance = 1f / currentGroupCount;
        return UnityEngine.Random.value <= findChance;
    }

    private Transform EnsureBoxRootNode(Transform shelfTransform)
    {
        EnsureShelfSubRoots();

        var nodeName = $"{shelfTransform.name}_Boxes";
        var existing = runtimeBoxRoot != null ? runtimeBoxRoot.Find(nodeName) : null;
        if (existing != null)
        {
            existing.position = shelfTransform.position;
            existing.rotation = shelfTransform.rotation;
            existing.localScale = Vector3.one;
            return existing;
        }

        var node = new GameObject(nodeName).transform;
        if (runtimeBoxRoot != null)
        {
            node.SetParent(runtimeBoxRoot, false);
        }

        node.position = shelfTransform.position;
        node.rotation = shelfTransform.rotation;
        node.localScale = Vector3.one;
        return node;
    }

    private void ApplySortingForBox(Transform boxTransform, int stackIndex)
    {
        var clampedOrder = boxSortingOrderStart + Mathf.Clamp(stackIndex, 0, 3);

        var spriteRenderers = boxTransform.GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            if (useBoxSortingLayer && !string.IsNullOrWhiteSpace(boxSortingLayerName))
            {
                spriteRenderers[i].sortingLayerName = boxSortingLayerName;
            }

            spriteRenderers[i].sortingOrder = clampedOrder;
        }

        var renderers = boxTransform.GetComponentsInChildren<Renderer>(true);
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is SpriteRenderer)
            {
                continue;
            }

            if (useBoxSortingLayer && !string.IsNullOrWhiteSpace(boxSortingLayerName))
            {
                renderers[i].sortingLayerName = boxSortingLayerName;
            }

            renderers[i].sortingOrder = clampedOrder;
        }

        LogInfo($"Apply sorting for {boxTransform.name} => layer={boxSortingLayerName}, order={clampedOrder}", boxTransform.gameObject);
    }

    private void ConfigureLogger()
    {
        GameDebugLogger.EnableConsoleLog = enableDebugLog;
        GameDebugLogger.EnableFileLog = enableDebugFileLog;
    }

    private void LogInfo(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLog && !enableDebugFileLog)
        {
            return;
        }

        var ctx = context != null ? $" | ctx={context.name}" : string.Empty;
        GameDebugLogger.Info(LogTag, message + ctx);
    }

    private void LogWarn(string message, UnityEngine.Object context = null)
    {
        if (!enableDebugLog && !enableDebugFileLog)
        {
            return;
        }

        var ctx = context != null ? $" | ctx={context.name}" : string.Empty;
        GameDebugLogger.Warn(LogTag, message + ctx);
    }
}
