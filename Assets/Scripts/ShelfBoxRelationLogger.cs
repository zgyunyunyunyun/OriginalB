using System.Collections.Generic;
using UnityEngine;

public static class ShelfBoxRelationLogger
{
    private const string LogTag = "ShelfBoxRelation";

    public static void LogShelfSnapshot(string reason, IList<GameObject> shelfObjects)
    {
        if (shelfObjects == null)
        {
            GameDebugLogger.Warn(LogTag, $"Snapshot skipped: shelfObjects is null | reason={reason}");
            return;
        }

        var rootOwners = new Dictionary<int, List<string>>();
        GameDebugLogger.Info(LogTag, $"Snapshot begin | reason={reason} | shelfCount={shelfObjects.Count}");

        for (var i = 0; i < shelfObjects.Count; i++)
        {
            var shelfObject = shelfObjects[i];
            if (shelfObject == null)
            {
                GameDebugLogger.Warn(LogTag, $"ShelfState index={i} | shelf=null");
                continue;
            }

            var shelf = shelfObject.GetComponent<ShelfInteractionController>();
            LogSingleShelfState($"Snapshot shelf[{i}]", shelf);

            if (shelf == null || shelf.StackRoot == null)
            {
                continue;
            }

            var rootId = shelf.StackRoot.GetInstanceID();
            if (!rootOwners.TryGetValue(rootId, out var owners))
            {
                owners = new List<string>();
                rootOwners[rootId] = owners;
            }

            owners.Add($"{shelfObject.name}(index={shelf.ShelfIndex})");
        }

        foreach (var pair in rootOwners)
        {
            if (pair.Value.Count <= 1)
            {
                continue;
            }

            GameDebugLogger.Warn(LogTag, $"DuplicateStackRoot detected | rootId={pair.Key} | owners={string.Join(" , ", pair.Value)}");
        }

        GameDebugLogger.Info(LogTag, "Snapshot end");
    }

    public static void LogMoveValidationFailure(string reason, Transform boxTransform, ShelfInteractionController sourceShelf, ShelfInteractionController targetShelf)
    {
        var boxName = boxTransform != null ? boxTransform.name : "null";
        var boxId = boxTransform != null ? boxTransform.GetInstanceID() : 0;
        var sourceName = sourceShelf != null ? sourceShelf.name : "null";
        var targetName = targetShelf != null ? targetShelf.name : "null";

        GameDebugLogger.Warn(LogTag,
            $"MoveValidationFail | reason={reason} | box={boxName}#{boxId} | source={sourceName} | target={targetName}");

        LogSingleShelfState("MoveValidationFail source", sourceShelf);
        LogSingleShelfState("MoveValidationFail target", targetShelf);
    }

    private static void LogSingleShelfState(string prefix, ShelfInteractionController shelf)
    {
        if (shelf == null)
        {
            GameDebugLogger.Warn(LogTag, $"{prefix} | shelf=null");
            return;
        }

        var stackRoot = shelf.StackRoot;
        if (stackRoot == null)
        {
            GameDebugLogger.Warn(LogTag, $"{prefix} | shelf={shelf.name} index={shelf.ShelfIndex} stackRoot=null");
            return;
        }

        var rootRef = stackRoot.GetComponent<ShelfStackRootRef>();
        var boundShelf = rootRef != null ? rootRef.Shelf : null;
        var boundName = boundShelf != null ? boundShelf.name : "null";
        var boundIndex = boundShelf != null ? boundShelf.ShelfIndex.ToString() : "-1";
        var bindingOk = boundShelf == shelf;

        var boxStates = new List<string>();
        for (var i = 0; i < stackRoot.childCount; i++)
        {
            var box = stackRoot.GetChild(i);
            if (box == null)
            {
                boxStates.Add($"{i}:null");
                continue;
            }

            var state = box.GetComponent<BoxVisualState>();
            var color = state != null ? state.OriginalColorType.ToString() : "Unknown";
            var gray = state != null && state.IsGrayed;
            boxStates.Add($"{i}:{box.name}#{box.GetInstanceID()}:{color}:gray={gray}");
        }

        GameDebugLogger.Info(LogTag,
            $"{prefix} | shelf={shelf.name} index={shelf.ShelfIndex} max={shelf.MaxBoxCount} "
            + $"stackRoot={stackRoot.name}#{stackRoot.GetInstanceID()} count={stackRoot.childCount} "
            + $"boundShelf={boundName}#{boundIndex} bindingOk={bindingOk} "
            + $"boxes=[{string.Join(" | ", boxStates)}]");
    }
}
