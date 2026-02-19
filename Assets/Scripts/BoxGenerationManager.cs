using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DisallowMultipleComponent]
public class BoxGenerationManager : MonoBehaviour
{
    [Serializable]
    public class ColorConfig
    {
        public GameManager.BoxColor colorType;
        public Color displayColor = Color.white;
        public bool enabled = true;
    }

    private class ShelfBucket
    {
        public readonly List<GameManager.BoxColor> colors = new List<GameManager.BoxColor>();
        public readonly HashSet<GameManager.BoxColor> colorSet = new HashSet<GameManager.BoxColor>();
    }

    [Header("Color Config")]
    [SerializeField] private List<ColorConfig> colorConfigs = new List<ColorConfig>();

    [Header("Generation Constraints")]
    [SerializeField, Min(4)] private int sameColorGroupSize = 4;
    [SerializeField, Min(1)] private int maxGenerateAttempts = 96;

    [Header("Occlusion")]
    [SerializeField, Range(0f, 1f)] private float grayPercentage = 0.35f;
    [SerializeField] private Color grayDisplayColor = new Color(0.45f, 0.45f, 0.45f, 1f);

    private readonly Dictionary<GameManager.BoxColor, Color> displayColorMap = new Dictionary<GameManager.BoxColor, Color>();
    private readonly List<GameManager.BoxColor> activeColorTypes = new List<GameManager.BoxColor>();

    public int SameColorGroupSize => Mathf.Max(4, sameColorGroupSize);
    public Color GrayDisplayColor => grayDisplayColor;
    public float GrayPercentage => Mathf.Clamp01(grayPercentage);

    private void Awake()
    {
        EnsureColorConfigs();
        RebuildCache();
    }

    private void OnValidate()
    {
        EnsureColorConfigs();
        RebuildCache();
    }

    public List<GameManager.BoxColor> GetActiveColorTypes()
    {
        if (activeColorTypes.Count == 0)
        {
            RebuildCache();
        }

        return new List<GameManager.BoxColor>(activeColorTypes);
    }

    public bool TryGetDisplayColor(GameManager.BoxColor colorType, out Color displayColor)
    {
        if (displayColorMap.Count == 0)
        {
            RebuildCache();
        }

        if (displayColorMap.TryGetValue(colorType, out displayColor))
        {
            return true;
        }

        displayColor = GetDefaultDisplayColor(colorType);
        return false;
    }

    public bool TryGenerateColorLayout(
        int shelfCount,
        int boxesPerShelf,
        System.Random random,
        out List<List<GameManager.BoxColor>> shelfColorLayout,
        out string error)
    {
        return TryGenerateColorLayoutByCounts(
            shelfCount,
            0,
            shelfCount * boxesPerShelf,
            boxesPerShelf,
            random,
            out shelfColorLayout,
            out _,
            out error);
    }

    public bool TryGenerateColorLayoutByCounts(
        int totalShelfCount,
        int emptyShelfCount,
        int totalBoxCount,
        int maxBoxesPerShelf,
        System.Random random,
        out List<List<GameManager.BoxColor>> shelfColorLayout,
        out List<int> shelfBoxCounts,
        out string error)
    {
        shelfColorLayout = null;
        shelfBoxCounts = null;
        error = string.Empty;

        var activeColors = GetActiveColorTypes();
        if (activeColors.Count == 0)
        {
            error = "没有可用的颜色类型，请在 BoxGenerationManager 中启用至少一种颜色。";
            return false;
        }

        if (totalShelfCount <= 0)
        {
            error = "货架总数必须大于 0。";
            return false;
        }

        var safeEmptyShelfCount = Mathf.Clamp(emptyShelfCount, 0, totalShelfCount);
        var nonEmptyShelfCount = totalShelfCount - safeEmptyShelfCount;
        if (nonEmptyShelfCount <= 0)
        {
            error = "置空货架数量过大，至少保留 1 个可放箱子货架。";
            return false;
        }

        if (maxBoxesPerShelf <= 0)
        {
            error = "单货架最大箱子数必须大于 0。";
            return false;
        }

        var groupSize = SameColorGroupSize;
        if (nonEmptyShelfCount < groupSize)
        {
            error = $"非空货架数不足。当前可放箱子货架 {nonEmptyShelfCount} 小于同色分组大小 {groupSize}。";
            return false;
        }

        var normalizedTotalBoxCount = Mathf.Max(0, totalBoxCount - totalBoxCount % groupSize);
        if (normalizedTotalBoxCount <= 0)
        {
            error = $"箱子总数必须至少为 {groupSize}，且为 {groupSize} 的倍数。";
            return false;
        }

        var maxCapacity = nonEmptyShelfCount * maxBoxesPerShelf;
        if (normalizedTotalBoxCount > maxCapacity)
        {
            error = $"箱子总数 {normalizedTotalBoxCount} 超过容量上限 {maxCapacity}。请减少箱子总数或提高单货架上限。";
            return false;
        }

        if (normalizedTotalBoxCount < nonEmptyShelfCount)
        {
            error = $"箱子总数 {normalizedTotalBoxCount} 不足以保证 {nonEmptyShelfCount} 个非空货架至少各有 1 个箱子。";
            return false;
        }

        if (maxBoxesPerShelf > activeColors.Count)
        {
            error = $"单货架最多箱子数为 {maxBoxesPerShelf}，但启用颜色仅 {activeColors.Count} 种，无法保证同色不落同货架。";
            return false;
        }

        var rng = random ?? new System.Random(unchecked(Environment.TickCount));
        var attempts = Mathf.Max(1, maxGenerateAttempts);
        var groups = normalizedTotalBoxCount / groupSize;

        for (var attempt = 0; attempt < attempts; attempt++)
        {
            var allShelfIndexes = Enumerable.Range(0, totalShelfCount).ToList();
            Shuffle(allShelfIndexes, rng);
            var activeShelfIndexes = allShelfIndexes.Take(nonEmptyShelfCount).ToList();

            var targetCounts = new int[totalShelfCount];
            for (var i = 0; i < activeShelfIndexes.Count; i++)
            {
                targetCounts[activeShelfIndexes[i]] = 1;
            }

            var remainingBoxes = normalizedTotalBoxCount - nonEmptyShelfCount;
            while (remainingBoxes > 0)
            {
                var candidates = activeShelfIndexes.Where(idx => targetCounts[idx] < maxBoxesPerShelf).ToList();
                if (candidates.Count == 0)
                {
                    break;
                }

                var minCount = candidates.Min(idx => targetCounts[idx]);
                var minCandidates = candidates.Where(idx => targetCounts[idx] == minCount).ToList();
                var selectedShelf = minCandidates[rng.Next(minCandidates.Count)];
                targetCounts[selectedShelf]++;
                remainingBoxes--;
            }

            if (remainingBoxes > 0)
            {
                continue;
            }

            var shelves = new List<ShelfBucket>(totalShelfCount);
            for (var i = 0; i < totalShelfCount; i++)
            {
                shelves.Add(new ShelfBucket());
            }

            var shelfRemaining = new int[totalShelfCount];
            Array.Copy(targetCounts, shelfRemaining, totalShelfCount);

            var success = true;
            for (var g = 0; g < groups; g++)
            {
                var shelfCandidates = activeShelfIndexes.Where(idx => shelfRemaining[idx] > 0).ToList();
                if (shelfCandidates.Count < groupSize)
                {
                    success = false;
                    break;
                }

                var selectedShelves = new List<int>(groupSize);
                for (var pick = 0; pick < groupSize; pick++)
                {
                    var selectable = shelfCandidates.Where(idx => !selectedShelves.Contains(idx)).ToList();
                    if (selectable.Count == 0)
                    {
                        success = false;
                        break;
                    }

                    var maxRemain = selectable.Max(idx => shelfRemaining[idx]);
                    var topSelectable = selectable.Where(idx => shelfRemaining[idx] == maxRemain).ToList();
                    selectedShelves.Add(topSelectable[rng.Next(topSelectable.Count)]);
                }

                if (!success)
                {
                    break;
                }

                var candidateColors = activeColors.Where(color => selectedShelves.All(shelfIdx => !shelves[shelfIdx].colorSet.Contains(color))).ToList();
                if (candidateColors.Count == 0)
                {
                    success = false;
                    break;
                }

                var selectedColor = candidateColors[rng.Next(candidateColors.Count)];
                for (var i = 0; i < selectedShelves.Count; i++)
                {
                    var shelfIndex = selectedShelves[i];
                    shelves[shelfIndex].colors.Add(selectedColor);
                    shelves[shelfIndex].colorSet.Add(selectedColor);
                    shelfRemaining[shelfIndex]--;
                }
            }

            if (!success)
            {
                continue;
            }

            for (var i = 0; i < totalShelfCount; i++)
            {
                if (shelves[i].colors.Count != targetCounts[i])
                {
                    success = false;
                    break;
                }

                Shuffle(shelves[i].colors, rng);
            }

            if (!success)
            {
                continue;
            }

            shelfColorLayout = shelves.Select(s => s.colors).ToList();
            shelfBoxCounts = targetCounts.ToList();
            return true;
        }

        error = "多次尝试后仍未找到满足约束的随机布局，请调整货架、空货架、箱子总数或颜色种类。";
        return false;
    }

    public bool TryGenerateGrayCountsPerShelf(
        IReadOnlyList<int> shelfBoxCounts,
        out List<int> grayCountsPerShelf,
        out int targetGrayCount,
        out string message)
    {
        grayCountsPerShelf = null;
        targetGrayCount = 0;
        message = string.Empty;

        if (shelfBoxCounts == null || shelfBoxCounts.Count == 0)
        {
            message = "货架箱子分布为空。";
            return false;
        }

        var shelfCount = shelfBoxCounts.Count;
        var totalBoxes = shelfBoxCounts.Sum(v => Mathf.Max(0, v));
        var expectedGray = Mathf.RoundToInt(totalBoxes * Mathf.Clamp01(grayPercentage));

        var grayCapacity = new List<int>(shelfCount);
        for (var i = 0; i < shelfCount; i++)
        {
            grayCapacity.Add(Mathf.Max(0, shelfBoxCounts[i] - 1));
        }

        var maxGrayByTopConstraint = grayCapacity.Sum();
        var feasibleGray = Mathf.Clamp(expectedGray, 0, maxGrayByTopConstraint);

        grayCountsPerShelf = new List<int>(shelfCount);
        for (var i = 0; i < shelfCount; i++)
        {
            grayCountsPerShelf.Add(0);
        }

        var remaining = feasibleGray;
        while (remaining > 0)
        {
            var candidates = new List<int>();
            var minGray = int.MaxValue;
            for (var i = 0; i < shelfCount; i++)
            {
                if (grayCountsPerShelf[i] >= grayCapacity[i])
                {
                    continue;
                }

                minGray = Mathf.Min(minGray, grayCountsPerShelf[i]);
            }

            if (minGray == int.MaxValue)
            {
                break;
            }

            for (var i = 0; i < shelfCount; i++)
            {
                if (grayCountsPerShelf[i] >= grayCapacity[i])
                {
                    continue;
                }

                if (grayCountsPerShelf[i] == minGray)
                {
                    candidates.Add(i);
                }
            }

            if (candidates.Count == 0)
            {
                break;
            }

            var picked = candidates[UnityEngine.Random.Range(0, candidates.Count)];
            grayCountsPerShelf[picked]++;
            remaining--;
        }

        targetGrayCount = feasibleGray;
        if (feasibleGray != expectedGray)
        {
            message = $"置灰目标从 {expectedGray} 调整为 {feasibleGray}（受顶层不可置灰约束）。";
        }

        return true;
    }

    private void EnsureColorConfigs()
    {
        var enumValues = (GameManager.BoxColor[])Enum.GetValues(typeof(GameManager.BoxColor));
        if (colorConfigs == null)
        {
            colorConfigs = new List<ColorConfig>();
        }

        for (var i = 0; i < enumValues.Length; i++)
        {
            var colorType = enumValues[i];
            var exists = colorConfigs.Any(c => c.colorType == colorType);
            if (exists)
            {
                continue;
            }

            colorConfigs.Add(new ColorConfig
            {
                colorType = colorType,
                displayColor = GetDefaultDisplayColor(colorType),
                enabled = true
            });
        }
    }

    private void RebuildCache()
    {
        displayColorMap.Clear();
        activeColorTypes.Clear();

        for (var i = 0; i < colorConfigs.Count; i++)
        {
            var config = colorConfigs[i];
            displayColorMap[config.colorType] = config.displayColor;
            if (config.enabled)
            {
                activeColorTypes.Add(config.colorType);
            }
        }
    }

    private static Color GetDefaultDisplayColor(GameManager.BoxColor colorType)
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

    private static void Shuffle<T>(IList<T> list, System.Random rng)
    {
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}