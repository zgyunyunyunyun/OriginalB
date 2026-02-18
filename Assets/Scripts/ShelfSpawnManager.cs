using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
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

    private readonly List<GameObject> spawnedShelves = new List<GameObject>();
    private Transform runtimeShelfRoot;
    private Transform legacyShelfRoot;
    private int refreshSequence;
    private Button regenerateButton;

    public bool AutoSpawnShelves => autoSpawnShelves;

    private void Awake()
    {
        EnsureShelfRoot();
        EnsureShelfSubRoots();
        TryAutoBindShelfPrefab();
        EnsureRegenerateButton();
    }

    public void RefreshShelves(int shelfCount)
    {
        EnsureRegenerateButton();
        EnsureShelfRoot();
        EnsureShelfSubRoots();
        TryAutoBindShelfPrefab();

        if (migrateSceneShelvesOnRefresh)
        {
            MigrateLegacyShelves();
        }

        ClearSpawnedShelves();

        if (shelfPrefab == null)
        {
            return;
        }

        var cameraRef = Camera.main;
        if (cameraRef == null)
        {
            return;
        }

        var targetCount = shelfCount > 0 ? shelfCount : Mathf.Max(0, previewShelfCount);
        if (targetCount <= 0)
        {
            return;
        }

        var spawnPositions = GenerateShelfPositions(cameraRef, targetCount);
        for (var i = 0; i < spawnPositions.Count; i++)
        {
            var shelf = Instantiate(shelfPrefab, spawnPositions[i], shelfPrefab.transform.rotation, runtimeShelfRoot);
            shelf.name = $"Shelf_{i + 1}";
            spawnedShelves.Add(shelf);
        }
    }

    public void RegenerateShelves()
    {
        RefreshShelves(0);
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

        var eventSystemObj = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        eventSystemObj.transform.SetParent(null, false);
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
}
