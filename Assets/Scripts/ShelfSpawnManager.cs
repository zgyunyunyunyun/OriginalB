using System;
using System.Collections.Generic;
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

    [Header("UI")]
    [SerializeField] private bool showRegenerateButton = true;
    [SerializeField] private string regenerateButtonText = "重新产生货架";
    [SerializeField] private Vector2 regenerateButtonSize = new Vector2(220f, 56f);
    [SerializeField] private Vector2 regenerateButtonPosition = new Vector2(0f, -48f);

    [Header("Legacy Migration")]
    [SerializeField] private bool migrateSceneShelvesOnRefresh = true;
    [SerializeField] private bool disableLegacyShelvesAfterMigration = true;
    [SerializeField] private bool destroyLegacyShelvesAfterMigration;
    [SerializeField] private string legacyShelfNameKeyword = "shelf";

    [Header("Debug Log")]
    [SerializeField] private bool enableDebugLog = true;
    [SerializeField] private bool enableDebugFileLog = true;

    private readonly List<GameObject> spawnedShelves = new List<GameObject>();
    private IPlatformContext platformContext;
    private Transform runtimeShelfRoot;
    private Transform runtimeBoxRoot;
    private Transform legacyShelfRoot;
    private int refreshSequence;
    private Button regenerateButton;
    private const string LogTag = "ShelfSpawn";
    private bool printedBoxGenerationManagerMissingWarning;

    public bool AutoSpawnShelves => autoSpawnShelves;

    public void SetBoxGenerationManager(BoxGenerationManager manager)
    {
        boxGenerationManager = manager;
        printedBoxGenerationManagerMissingWarning = false;
    }

    private void Awake()
    {
        if (!ServiceLocator.TryResolve<IPlatformContext>(out platformContext))
        {
            platformContext = new CommonPlatformContext();
        }

        ConfigureLogger();
        LogInfo("Awake start");
        NormalizeConfiguredCounts();
        EnsureShelfRoot();
        EnsureShelfSubRoots();
        TryAutoBindShelfPrefab();
        TryAutoBindBoxPrefab();
        EnsureRegenerateButton();
        LogInfo($"Awake end | shelfPrefab={(shelfPrefab != null ? shelfPrefab.name : "NULL")} | boxPrefab={(boxPrefab != null ? boxPrefab.name : "NULL")}");
    }

    public void RefreshShelves(int shelfCount)
    {
        ConfigureLogger();
        NormalizeConfiguredCounts();
        LogInfo($"RefreshShelves begin | inputCount={shelfCount} | autoSpawn={autoSpawnShelves}");
        EnsureRegenerateButton();
        EnsureShelfRoot();
        EnsureShelfSubRoots();
        TryAutoBindShelfPrefab();
        TryAutoBindBoxPrefab();

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

        var spawnPositions = GenerateShelfPositions(cameraRef, targetCount);
        LogInfo($"Generated shelf positions: {spawnPositions.Count}");
        for (var i = 0; i < spawnPositions.Count; i++)
        {
            var shelf = Instantiate(shelfPrefab, spawnPositions[i], shelfPrefab.transform.rotation, runtimeShelfRoot);
            shelf.name = $"Shelf_{i + 1}";
            spawnedShelves.Add(shelf);
            LogInfo($"Shelf spawned: {shelf.name} at {shelf.transform.position}", shelf);

            var shelfColors = colorLayout != null && i < colorLayout.Count ? colorLayout[i] : null;
            var shelfGrayCount = grayCountsPerShelf != null && i < grayCountsPerShelf.Count ? grayCountsPerShelf[i] : 0;
            var shelfBoxCount = shelfBoxCounts != null && i < shelfBoxCounts.Count ? Mathf.Max(0, shelfBoxCounts[i]) : Mathf.Max(1, boxesPerShelf);
            SpawnBoxesForShelf(shelf, shelfColors, shelfGrayCount, shelfBoxCount);
            EnsureShelfInteractionForRuntime(shelf, i);
        }

        LogInfo($"RefreshShelves end | spawnedShelves={spawnedShelves.Count}");
    }

    public void RegenerateShelves()
    {
        var fallbackCount = Mathf.Max(1, configuredTotalShelfCount);
        var targetCount = spawnedShelves.Count > 0 ? spawnedShelves.Count : fallbackCount;
        RefreshShelves(targetCount);
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
    }

    private List<Vector3> GenerateShelfPositions(Camera cameraRef, int shelfCount)
    {
        var positions = new List<Vector3>(shelfCount);
        var random = new System.Random(ResolveSpawnSeed(shelfCount));
        var shelfHalfExtents = ResolveShelfHalfExtents();
        var extraGap = Mathf.Max(0f, minShelfDistance);
        var inner = Mathf.Clamp(viewportInnerRadius, 0f, 0.45f);
        var outer = Mathf.Clamp(viewportOuterRadius, inner + 0.01f, 0.8f);
        var horizontalPad = Mathf.Clamp(viewportHorizontalPadding, 0.02f, 0.35f);
        var verticalPad = Mathf.Clamp(viewportVerticalPadding, 0.02f, 0.35f);
        var center = new Vector2(0.5f, 0.5f);
        const float goldenAngle = 2.39996323f;

        for (var i = 0; i < shelfCount; i++)
        {
            var placed = false;
            for (var attempt = 0; attempt < 280; attempt++)
            {
                var ratio = (i + 0.5f + attempt * 0.11f) / Mathf.Max(1f, shelfCount);
                var radius = Mathf.Lerp(inner, outer, Mathf.Sqrt(Mathf.Clamp01(ratio)));
                var angle = i * goldenAngle + attempt * 0.71f + (float)random.NextDouble() * 0.6f;
                var jitterScale = 0.015f + (float)random.NextDouble() * 0.02f;
                var jitter = new Vector2(
                    ((float)random.NextDouble() * 2f - 1f) * jitterScale,
                    ((float)random.NextDouble() * 2f - 1f) * jitterScale);

                var viewportPoint = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius + jitter;
                viewportPoint.x = Mathf.Clamp(viewportPoint.x, horizontalPad, 1f - horizontalPad);
                viewportPoint.y = Mathf.Clamp(viewportPoint.y, verticalPad, 1f - verticalPad);

                var worldPoint = ViewportToWorldOnPlane(cameraRef, viewportPoint);
                if (!IsOverlapping(worldPoint, positions, shelfHalfExtents, extraGap))
                {
                    positions.Add(worldPoint);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                var fallbackPlaced = TryFindGridFallbackPosition(
                    cameraRef,
                    positions,
                    shelfHalfExtents,
                    extraGap,
                    horizontalPad,
                    verticalPad,
                    out var fallbackPosition);

                if (fallbackPlaced)
                {
                    positions.Add(fallbackPosition);
                    placed = true;
                }
            }

            if (!placed)
            {
                Debug.LogWarning($"ShelfSpawnManager: 无法在当前间距配置下放置第 {i + 1} 个货架，已跳过以避免重叠。可调小 minShelfDistance 或 padding。", this);
                break;
            }
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
            buttonRect.anchorMin = new Vector2(0.5f, 1f);
            buttonRect.anchorMax = new Vector2(0.5f, 1f);
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

        regenerateButton.gameObject.SetActive(true);
        var rect = regenerateButton.GetComponent<RectTransform>();
        rect.sizeDelta = regenerateButtonSize;
        rect.anchoredPosition = regenerateButtonPosition;

        var label = regenerateButton.GetComponentInChildren<Text>(true);
        if (label != null)
        {
            label.text = regenerateButtonText;
        }

        regenerateButton.onClick.RemoveListener(RegenerateShelves);
        regenerateButton.onClick.AddListener(RegenerateShelves);
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
        if (shelfColors != null && boxIndex >= 0 && boxIndex < shelfColors.Count)
        {
            return shelfColors[boxIndex];
        }

        var enumColors = (GameManager.BoxColor[])Enum.GetValues(typeof(GameManager.BoxColor));
        if (enumColors.Length == 0)
        {
            return GameManager.BoxColor.Red;
        }

        var idx = Mathf.Abs(boxIndex) % enumColors.Length;
        return enumColors[idx];
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

        var collider2D = boxTransform.GetComponent<Collider2D>();
        if (collider2D == null)
        {
            collider2D = boxTransform.gameObject.AddComponent<BoxCollider2D>();
        }

        var interaction = boxTransform.GetComponent<BoxInteractionController>();
        if (interaction == null)
        {
            boxTransform.gameObject.AddComponent<BoxInteractionController>();
        }
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
