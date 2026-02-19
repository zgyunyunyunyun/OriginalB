using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public enum GameState
    {
        Idle,
        Playing,
        Win,
        Lose
    }

    public enum BoxColor
    {
        Red,
        Blue,
        Green,
        Yellow,
        Purple,
        Orange
    }

    public enum ToolType
    {
        RevealShelf,
        UndoMove,
        CatHint
    }

    [Serializable]
    public class BoxSlot
    {
        public BoxColor color;
        public bool startVisible;
    }

    [Serializable]
    public class ShelfLayout
    {
        public int capacity = 12;
        public List<BoxSlot> boxes = new List<BoxSlot>();
    }

    [Serializable]
    public class LevelDefinition
    {
        public string levelName = "Level";
        public int difficultyRating = 1;
        public int rewardScore = 100;
        public bool onlyTopVisible = true;
        public int extraShelves = 1;
        public int adRewardShelves = 1;
        public List<ShelfLayout> shelves = new List<ShelfLayout>();
    }

    public class LevelEvaluation
    {
        public bool likelySolvable;
        public float solvableRate;
        public int estimatedDifficulty;
    }

    private class BoxData
    {
        public int id;
        public BoxColor color;
        public bool colorVisible;
        public bool hasCat;
    }

    private class ShelfData
    {
        public int capacity;
        public List<BoxData> boxes = new List<BoxData>();
    }

    private class RuntimeLevel
    {
        public int difficulty;
        public int rewardScore;
        public int adRewardShelvesRemaining;
        public bool catFound;
        public int score;
        public List<ShelfData> shelves = new List<ShelfData>();
    }

    private class MoveRecord
    {
        public int from;
        public int to;
        public int boxId;
    }

    [Header("Game")]
    [SerializeField] private List<LevelDefinition> predefinedLevels = new List<LevelDefinition>();
    [SerializeField] private int startLevelIndex;
    [SerializeField] private bool autoStartOnPlay = true;

    [Header("Attempts")]
    [SerializeField] private int dailyPlayLimit = 5;

    [Header("Tools")]
    [SerializeField] private int initialRevealToolCount = 1;
    [SerializeField] private int initialUndoToolCount = 1;
    [SerializeField] private int initialCatHintToolCount = 1;

    [Header("Shelf Integration")]
    [SerializeField] private ShelfSpawnManager shelfSpawnManager;

    [Header("Box Generation")]
    [SerializeField] private BoxGenerationManager boxGenerationManager;
    [SerializeField] private bool logBoxGenerationManagerMissing = true;

    private const string DailyDateKey = "GM_DailyDate";
    private const string DailyCountKey = "GM_DailyCount";
    private const string TotalPointsKey = "GM_TotalPoints";

    private RuntimeLevel currentLevel;
    private readonly Dictionary<ToolType, int> toolInventory = new Dictionary<ToolType, int>();
    private readonly Stack<MoveRecord> moveHistory = new Stack<MoveRecord>();
    private int boxIdGenerator;
    private bool printedBoxGenerationManagerMissingWarning;

    public GameState State { get; private set; } = GameState.Idle;
    public int LastHintShelfIndex { get; private set; } = -1;
    public int TotalPoints => PlayerPrefs.GetInt(TotalPointsKey, 0);

    private void Awake()
    {
        EnsureShelfSpawnManager();
        ResetDailyCounterIfNeeded();
        ResetToolInventory();
    }

    private void Start()
    {
        if (autoStartOnPlay && predefinedLevels.Count > 0)
        {
            TryStartLevel(startLevelIndex, true, null, null);
        }

        TryRefreshShelfSpawn();
    }

    public int GetRemainingDailyAttempts()
    {
        ResetDailyCounterIfNeeded();
        var used = PlayerPrefs.GetInt(DailyCountKey, 0);
        return Mathf.Max(0, dailyPlayLimit - used);
    }

    public bool TryStartLevel(int levelIndex, bool randomCatPlacement, int? catShelfIndex, int? catDepthIndex)
    {
        if (levelIndex < 0 || levelIndex >= predefinedLevels.Count)
        {
            return false;
        }

        if (GetRemainingDailyAttempts() <= 0)
        {
            return false;
        }

        ConsumeDailyAttempt();
        ResetToolInventory();
        LastHintShelfIndex = -1;
        moveHistory.Clear();

        currentLevel = BuildRuntimeLevel(predefinedLevels[levelIndex]);
        var catPlaced = PlaceCat(currentLevel, randomCatPlacement, catShelfIndex, catDepthIndex);
        if (!catPlaced)
        {
            return false;
        }

        State = GameState.Playing;
        TryRefreshShelfSpawn();

        return true;
    }

    public bool TryMoveTopBox(int fromShelfIndex, int toShelfIndex)
    {
        if (State != GameState.Playing || currentLevel == null)
        {
            return false;
        }

        if (!IsValidShelfIndex(fromShelfIndex) || !IsValidShelfIndex(toShelfIndex) || fromShelfIndex == toShelfIndex)
        {
            return false;
        }

        var fromShelf = currentLevel.shelves[fromShelfIndex];
        var toShelf = currentLevel.shelves[toShelfIndex];

        if (fromShelf.boxes.Count == 0 || toShelf.boxes.Count >= toShelf.capacity)
        {
            return false;
        }

        var moving = fromShelf.boxes[fromShelf.boxes.Count - 1];
        if (!moving.colorVisible)
        {
            return false;
        }

        if (toShelf.boxes.Count > 0)
        {
            var top = toShelf.boxes[toShelf.boxes.Count - 1];
            if (top.color != moving.color)
            {
                return false;
            }
        }

        fromShelf.boxes.RemoveAt(fromShelf.boxes.Count - 1);
        toShelf.boxes.Add(moving);
        EnsureTopVisible(fromShelf);
        EnsureTopVisible(toShelf);

        moveHistory.Push(new MoveRecord
        {
            from = fromShelfIndex,
            to = toShelfIndex,
            boxId = moving.id
        });

        ResolveEliminations();
        CheckEndState();
        return true;
    }

    public bool UseTool(ToolType toolType, int shelfIndex = -1)
    {
        if (State != GameState.Playing || currentLevel == null)
        {
            return false;
        }

        if (!toolInventory.ContainsKey(toolType) || toolInventory[toolType] <= 0)
        {
            return false;
        }

        var success = false;
        switch (toolType)
        {
            case ToolType.RevealShelf:
                success = RevealShelf(shelfIndex);
                break;
            case ToolType.UndoMove:
                success = UndoLastMove();
                break;
            case ToolType.CatHint:
                success = RevealCatShelfHint();
                break;
        }

        if (!success)
        {
            return false;
        }

        toolInventory[toolType]--;
        return true;
    }

    public int GetToolCount(ToolType toolType)
    {
        if (!toolInventory.ContainsKey(toolType))
        {
            return 0;
        }

        return toolInventory[toolType];
    }

    public bool UseAdRewardShelf()
    {
        if (State != GameState.Playing || currentLevel == null)
        {
            return false;
        }

        if (currentLevel.adRewardShelvesRemaining <= 0)
        {
            return false;
        }

        var capacityRef = currentLevel.shelves.Count > 0 ? currentLevel.shelves[0].capacity : 12;
        currentLevel.shelves.Add(new ShelfData
        {
            capacity = capacityRef
        });
        currentLevel.adRewardShelvesRemaining--;

        TryRefreshShelfSpawn();

        return true;
    }

    public void RefreshShelfSpawn()
    {
        TryRefreshShelfSpawn();
    }

    public LevelDefinition CreateControlledLevel(int seed, int difficulty)
    {
        var rng = new System.Random(seed);
        var normalizedDifficulty = Mathf.Clamp(difficulty, 1, 10);
        var shelfCount = 4 + normalizedDifficulty;
        var capacity = 8 + normalizedDifficulty;
        var boxesPerShelf = Mathf.Min(4, capacity);

        var level = new LevelDefinition
        {
            levelName = "Generated_" + seed,
            difficultyRating = normalizedDifficulty,
            rewardScore = 50 + normalizedDifficulty * 20,
            onlyTopVisible = true,
            extraShelves = 1,
            adRewardShelves = 1,
            shelves = new List<ShelfLayout>()
        };

        for (var i = 0; i < shelfCount; i++)
        {
            level.shelves.Add(new ShelfLayout
            {
                capacity = capacity,
                boxes = new List<BoxSlot>()
            });
        }

        if (boxGenerationManager != null
            && boxGenerationManager.TryGenerateColorLayout(shelfCount, boxesPerShelf, rng, out var shelfColorLayout, out _))
        {
            for (var s = 0; s < shelfColorLayout.Count && s < level.shelves.Count; s++)
            {
                var shelf = level.shelves[s];
                var colors = shelfColorLayout[s];
                for (var i = 0; i < colors.Count; i++)
                {
                    var startVisible = rng.NextDouble() < (0.15 + 0.25 / normalizedDifficulty);
                    shelf.boxes.Add(new BoxSlot
                    {
                        color = colors[i],
                        startVisible = startVisible
                    });
                }
            }
        }
        else
        {
            if (boxGenerationManager == null && logBoxGenerationManagerMissing && !printedBoxGenerationManagerMissingWarning)
            {
                printedBoxGenerationManagerMissingWarning = true;
                Debug.LogWarning("[GameManager] BoxGenerationManager 未绑定，CreateControlledLevel 将使用后备颜色生成逻辑。");
            }

            var fallbackColors = (BoxColor[])Enum.GetValues(typeof(BoxColor));
            var groupSize = 4;
            var totalBoxes = shelfCount * boxesPerShelf;
            var groupCount = totalBoxes / groupSize;

            var expandedPool = new List<BoxColor>(totalBoxes);
            for (var i = 0; i < groupCount; i++)
            {
                var color = fallbackColors[i % fallbackColors.Length];
                for (var c = 0; c < groupSize; c++)
                {
                    expandedPool.Add(color);
                }
            }

            Shuffle(expandedPool, rng);

            for (var i = 0; i < expandedPool.Count; i++)
            {
                var placed = false;
                var shelfOrder = Enumerable.Range(0, shelfCount).OrderBy(_ => rng.Next()).ToList();
                for (var k = 0; k < shelfOrder.Count; k++)
                {
                    var shelf = level.shelves[shelfOrder[k]];
                    if (shelf.boxes.Count >= boxesPerShelf)
                    {
                        continue;
                    }

                    var color = expandedPool[i];
                    var hasSameColor = shelf.boxes.Any(b => b.color == color);
                    if (hasSameColor)
                    {
                        continue;
                    }

                    var startVisible = rng.NextDouble() < (0.15 + 0.25 / normalizedDifficulty);
                    shelf.boxes.Add(new BoxSlot
                    {
                        color = color,
                        startVisible = startVisible
                    });
                    placed = true;
                    break;
                }

                if (!placed)
                {
                    break;
                }
            }
        }

        EnsureAtLeastOneVisibleTop(level);
        return level;
    }

    public LevelEvaluation EvaluateLevel(LevelDefinition level, int simulationRuns = 24, int maxSteps = 300)
    {
        var runs = Mathf.Max(4, simulationRuns);
        var success = 0;
        var moveBudget = Mathf.Max(50, maxSteps);

        for (var i = 0; i < runs; i++)
        {
            if (RunSolveSimulation(level, 3107 + i * 7919, moveBudget))
            {
                success++;
            }
        }

        var rate = (float)success / runs;
        var estimatedDifficulty = Mathf.Clamp(Mathf.RoundToInt((1f - rate) * 10f), 1, 10);
        return new LevelEvaluation
        {
            likelySolvable = rate >= 0.25f,
            solvableRate = rate,
            estimatedDifficulty = estimatedDifficulty
        };
    }

    private bool RunSolveSimulation(LevelDefinition level, int seed, int maxSteps)
    {
        var rng = new System.Random(seed);
        var sim = BuildRuntimeLevel(level);

        for (var step = 0; step < maxSteps; step++)
        {
            var remainingBoxes = sim.shelves.Sum(s => s.boxes.Count);
            if (remainingBoxes == 0)
            {
                return true;
            }

            var moves = CollectValidMoves(sim);
            if (moves.Count == 0)
            {
                return false;
            }

            var prioritized = moves
                .OrderByDescending(m => WouldCreateElimination(sim, m.from, m.to))
                .ThenBy(_ => rng.Next())
                .ToList();

            var selected = prioritized[0];
            ApplyMove(sim, selected.from, selected.to);
            ResolveEliminations(sim);
        }

        return false;
    }

    private List<(int from, int to)> CollectValidMoves(RuntimeLevel level)
    {
        var moves = new List<(int from, int to)>();
        for (var i = 0; i < level.shelves.Count; i++)
        {
            var from = level.shelves[i];
            if (from.boxes.Count == 0)
            {
                continue;
            }

            var sourceTop = from.boxes[from.boxes.Count - 1];
            if (!sourceTop.colorVisible)
            {
                continue;
            }

            for (var j = 0; j < level.shelves.Count; j++)
            {
                if (i == j)
                {
                    continue;
                }

                var to = level.shelves[j];
                if (to.boxes.Count >= to.capacity)
                {
                    continue;
                }

                if (to.boxes.Count == 0 || to.boxes[to.boxes.Count - 1].color == sourceTop.color)
                {
                    moves.Add((i, j));
                }
            }
        }

        return moves;
    }

    private bool WouldCreateElimination(RuntimeLevel level, int from, int to)
    {
        var toShelf = level.shelves[to];
        var fromTop = level.shelves[from].boxes[level.shelves[from].boxes.Count - 1];
        var sameCount = 1;

        for (var i = toShelf.boxes.Count - 1; i >= 0; i--)
        {
            if (toShelf.boxes[i].color == fromTop.color)
            {
                sameCount++;
            }
            else
            {
                break;
            }
        }

        return sameCount >= 3;
    }

    private void ApplyMove(RuntimeLevel level, int fromShelfIndex, int toShelfIndex)
    {
        var from = level.shelves[fromShelfIndex];
        var to = level.shelves[toShelfIndex];
        var moving = from.boxes[from.boxes.Count - 1];
        from.boxes.RemoveAt(from.boxes.Count - 1);
        to.boxes.Add(moving);
        EnsureTopVisible(from);
        EnsureTopVisible(to);
    }

    private RuntimeLevel BuildRuntimeLevel(LevelDefinition definition)
    {
        boxIdGenerator = 1;
        var runtime = new RuntimeLevel
        {
            difficulty = definition.difficultyRating,
            rewardScore = definition.rewardScore,
            adRewardShelvesRemaining = definition.adRewardShelves,
            catFound = false,
            score = 0,
            shelves = new List<ShelfData>()
        };

        for (var i = 0; i < definition.shelves.Count; i++)
        {
            var srcShelf = definition.shelves[i];
            var shelf = new ShelfData
            {
                capacity = Mathf.Max(1, srcShelf.capacity),
                boxes = new List<BoxData>()
            };

            for (var j = 0; j < srcShelf.boxes.Count; j++)
            {
                var srcBox = srcShelf.boxes[j];
                shelf.boxes.Add(new BoxData
                {
                    id = boxIdGenerator++,
                    color = srcBox.color,
                    colorVisible = !definition.onlyTopVisible || srcBox.startVisible,
                    hasCat = false
                });
            }

            EnsureTopVisible(shelf);
            runtime.shelves.Add(shelf);
        }

        if (runtime.shelves.Count > 0)
        {
            var capacity = runtime.shelves[0].capacity;
            for (var i = 0; i < definition.extraShelves; i++)
            {
                runtime.shelves.Add(new ShelfData
                {
                    capacity = capacity,
                    boxes = new List<BoxData>()
                });
            }
        }

        return runtime;
    }

    private bool PlaceCat(RuntimeLevel level, bool randomCatPlacement, int? catShelfIndex, int? catDepthIndex)
    {
        var allBoxes = level.shelves.SelectMany(s => s.boxes).ToList();
        if (allBoxes.Count == 0)
        {
            return false;
        }

        if (randomCatPlacement)
        {
            var idx = UnityEngine.Random.Range(0, allBoxes.Count);
            allBoxes[idx].hasCat = true;
            return true;
        }

        if (!catShelfIndex.HasValue || !catDepthIndex.HasValue)
        {
            return false;
        }

        if (!IsValidShelfIndex(catShelfIndex.Value))
        {
            return false;
        }

        var shelf = level.shelves[catShelfIndex.Value];
        if (catDepthIndex.Value < 0 || catDepthIndex.Value >= shelf.boxes.Count)
        {
            return false;
        }

        shelf.boxes[catDepthIndex.Value].hasCat = true;
        return true;
    }

    private void ResolveEliminations()
    {
        ResolveEliminations(currentLevel);
    }

    private void ResolveEliminations(RuntimeLevel level)
    {
        var changed = true;
        while (changed)
        {
            changed = false;
            for (var s = 0; s < level.shelves.Count; s++)
            {
                var shelf = level.shelves[s];
                if (shelf.boxes.Count < 3)
                {
                    continue;
                }

                var runStart = 0;
                while (runStart < shelf.boxes.Count)
                {
                    var runColor = shelf.boxes[runStart].color;
                    var runEnd = runStart + 1;
                    while (runEnd < shelf.boxes.Count && shelf.boxes[runEnd].color == runColor)
                    {
                        runEnd++;
                    }

                    var runLength = runEnd - runStart;
                    if (runLength >= 3)
                    {
                        var removed = shelf.boxes.GetRange(runStart, runLength);
                        if (removed.Any(b => b.hasCat))
                        {
                            level.catFound = true;
                        }

                        shelf.boxes.RemoveRange(runStart, runLength);
                        level.score += runLength * 10;
                        EnsureTopVisible(shelf);
                        changed = true;
                        break;
                    }

                    runStart = runEnd;
                }

                if (changed)
                {
                    break;
                }
            }
        }
    }

    private void CheckEndState()
    {
        if (currentLevel.catFound)
        {
            State = GameState.Win;
            GrantWinRewards();
            return;
        }

        if (!HasAnyValidMove(currentLevel))
        {
            State = GameState.Lose;
        }
    }

    private bool HasAnyValidMove(RuntimeLevel level)
    {
        return CollectValidMoves(level).Count > 0;
    }

    private void GrantWinRewards()
    {
        var reward = currentLevel.rewardScore + currentLevel.score;
        var remainToolBonus = toolInventory.Values.Sum() * 5;
        reward += remainToolBonus;
        var total = PlayerPrefs.GetInt(TotalPointsKey, 0);
        PlayerPrefs.SetInt(TotalPointsKey, total + reward);
        PlayerPrefs.Save();
    }

    private bool RevealShelf(int shelfIndex)
    {
        if (!IsValidShelfIndex(shelfIndex))
        {
            return false;
        }

        var shelf = currentLevel.shelves[shelfIndex];
        if (shelf.boxes.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < shelf.boxes.Count; i++)
        {
            shelf.boxes[i].colorVisible = true;
        }

        return true;
    }

    private bool UndoLastMove()
    {
        if (moveHistory.Count == 0)
        {
            return false;
        }

        var record = moveHistory.Pop();
        if (!IsValidShelfIndex(record.from) || !IsValidShelfIndex(record.to))
        {
            return false;
        }

        var toShelf = currentLevel.shelves[record.to];
        var fromShelf = currentLevel.shelves[record.from];
        if (toShelf.boxes.Count == 0 || fromShelf.boxes.Count >= fromShelf.capacity)
        {
            return false;
        }

        var moved = toShelf.boxes[toShelf.boxes.Count - 1];
        if (moved.id != record.boxId)
        {
            return false;
        }

        toShelf.boxes.RemoveAt(toShelf.boxes.Count - 1);
        fromShelf.boxes.Add(moved);
        EnsureTopVisible(toShelf);
        EnsureTopVisible(fromShelf);
        return true;
    }

    private bool RevealCatShelfHint()
    {
        if (currentLevel.catFound)
        {
            return false;
        }

        for (var i = 0; i < currentLevel.shelves.Count; i++)
        {
            if (currentLevel.shelves[i].boxes.Any(b => b.hasCat))
            {
                LastHintShelfIndex = i;
                return true;
            }
        }

        return false;
    }

    private bool IsValidShelfIndex(int shelfIndex)
    {
        return currentLevel != null && shelfIndex >= 0 && shelfIndex < currentLevel.shelves.Count;
    }

    private static void EnsureTopVisible(ShelfData shelf)
    {
        if (shelf.boxes.Count == 0)
        {
            return;
        }

        shelf.boxes[shelf.boxes.Count - 1].colorVisible = true;
    }

    private void EnsureAtLeastOneVisibleTop(LevelDefinition level)
    {
        for (var i = 0; i < level.shelves.Count; i++)
        {
            var shelf = level.shelves[i];
            if (shelf.boxes.Count == 0)
            {
                continue;
            }

            shelf.boxes[shelf.boxes.Count - 1].startVisible = true;
        }
    }

    private void ResetToolInventory()
    {
        toolInventory[ToolType.RevealShelf] = Mathf.Max(0, initialRevealToolCount);
        toolInventory[ToolType.UndoMove] = Mathf.Max(0, initialUndoToolCount);
        toolInventory[ToolType.CatHint] = Mathf.Max(0, initialCatHintToolCount);
    }

    private void ResetDailyCounterIfNeeded()
    {
        var today = DateTime.Now.ToString("yyyyMMdd");
        var stored = PlayerPrefs.GetString(DailyDateKey, string.Empty);
        if (stored == today)
        {
            return;
        }

        PlayerPrefs.SetString(DailyDateKey, today);
        PlayerPrefs.SetInt(DailyCountKey, 0);
        PlayerPrefs.Save();
    }

    private void ConsumeDailyAttempt()
    {
        ResetDailyCounterIfNeeded();
        var count = PlayerPrefs.GetInt(DailyCountKey, 0);
        PlayerPrefs.SetInt(DailyCountKey, count + 1);
        PlayerPrefs.Save();
    }

    private void EnsureShelfSpawnManager()
    {
        if (shelfSpawnManager != null)
        {
            return;
        }

        shelfSpawnManager = GetComponentInChildren<ShelfSpawnManager>(true);
        if (shelfSpawnManager != null)
        {
            return;
        }

        var managerNode = new GameObject("ShelfSpawnManager");
        managerNode.transform.SetParent(transform, false);
        shelfSpawnManager = managerNode.AddComponent<ShelfSpawnManager>();
    }

    private void TryRefreshShelfSpawn()
    {
        EnsureShelfSpawnManager();
        if (shelfSpawnManager == null || !shelfSpawnManager.AutoSpawnShelves)
        {
            return;
        }

        shelfSpawnManager.SetBoxGenerationManager(boxGenerationManager);

        shelfSpawnManager.RefreshShelves(GetTargetShelfCount());
    }

    private int GetTargetShelfCount()
    {
        if (currentLevel != null && currentLevel.shelves != null)
        {
            return currentLevel.shelves.Count;
        }

        return 0;
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
