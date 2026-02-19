using UnityEngine;

[DisallowMultipleComponent]
public class ShelfInteractionController : MonoBehaviour
{
    [SerializeField] private int shelfIndex = -1;
    [SerializeField, Min(1)] private int maxBoxCount = 4;
    [SerializeField] private Transform stackRoot;

    public int ShelfIndex => shelfIndex;
    public int MaxBoxCount => maxBoxCount;
    public Transform StackRoot => stackRoot;
    public int BoxCount => stackRoot != null ? stackRoot.childCount : 0;

    public void Initialize(int index, int capacity, Transform root)
    {
        shelfIndex = index;
        maxBoxCount = Mathf.Max(1, capacity);
        stackRoot = root;
    }

    public bool ContainsBox(Transform boxTransform)
    {
        return boxTransform != null && stackRoot != null && boxTransform.parent == stackRoot;
    }

    public bool IsTopBox(Transform boxTransform)
    {
        return ContainsBox(boxTransform) && boxTransform.GetSiblingIndex() == stackRoot.childCount - 1;
    }

    public bool CanAcceptColor(GameManager.BoxColor incomingColor)
    {
        if (stackRoot == null || stackRoot.childCount >= maxBoxCount)
        {
            return false;
        }

        if (stackRoot.childCount == 0)
        {
            return true;
        }

        var topBox = stackRoot.GetChild(stackRoot.childCount - 1);
        var topState = topBox != null ? topBox.GetComponent<BoxVisualState>() : null;
        if (topState == null)
        {
            return false;
        }

        return topState.OriginalColorType == incomingColor;
    }

    public void RefreshBoxVisualStates()
    {
        if (stackRoot == null)
        {
            return;
        }

        var topIndex = stackRoot.childCount - 1;
        for (var i = 0; i < stackRoot.childCount; i++)
        {
            var box = stackRoot.GetChild(i);
            if (box == null)
            {
                continue;
            }

            var state = box.GetComponent<BoxVisualState>();
            if (state == null)
            {
                continue;
            }

            state.ApplyDisplayByTopState(i == topIndex);
        }
    }

    public bool TryGetPlacementBottomPosition(out Vector3 bottom)
    {
        if (stackRoot != null && stackRoot.childCount > 0)
        {
            var topBox = stackRoot.GetChild(stackRoot.childCount - 1);
            if (TryGetAnchorPair(topBox, out _, out var topAnchor))
            {
                bottom = topAnchor.position;
                return true;
            }

            if (TryGetTopByBounds(topBox, out var topByBounds))
            {
                bottom = topByBounds;
                return true;
            }
        }

        if (TryGetAnchorPair(transform, out var shelfBottomAnchor, out _))
        {
            bottom = shelfBottomAnchor.position;
            return true;
        }

        if (TryGetBottomByBounds(transform, out var bottomByBounds))
        {
            bottom = bottomByBounds;
            return true;
        }

        bottom = transform.position;
        return false;
    }

    private static bool TryGetTopByBounds(Transform target, out Vector3 top)
    {
        var renderer = target.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            top = new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, target.position.z);
            return true;
        }

        var spriteRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            top = new Vector3(spriteRenderer.bounds.center.x, spriteRenderer.bounds.max.y, target.position.z);
            return true;
        }

        top = target.position;
        return false;
    }

    private static bool TryGetBottomByBounds(Transform target, out Vector3 bottom)
    {
        var renderer = target.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            bottom = new Vector3(renderer.bounds.center.x, renderer.bounds.min.y, target.position.z);
            return true;
        }

        var spriteRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            bottom = new Vector3(spriteRenderer.bounds.center.x, spriteRenderer.bounds.min.y, target.position.z);
            return true;
        }

        bottom = target.position;
        return false;
    }

    private static bool TryGetAnchorPair(Transform root, out Transform bottom, out Transform top)
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

    private static Transform FindAnchorByName(Transform root, params string[] aliases)
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
}