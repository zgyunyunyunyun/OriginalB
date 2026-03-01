using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class GameBackgroundPlacer : MonoBehaviour
{
    public enum BackgroundFitMode
    {
        Cover,
        FitInside
    }

    [Header("Background")]
    [SerializeField] private Sprite backgroundSprite;
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private BackgroundFitMode fitMode = BackgroundFitMode.Cover;

    [Header("Camera")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private bool followCamera = true;
    [SerializeField] private Vector2 positionOffset = Vector2.zero;
    [SerializeField] private float zPosition = 20f;

    [Header("Render Order")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = -1000;

    private Vector2 lastScreenSize = Vector2.zero;

    private void Awake()
    {
        EnsureRenderer();
        ApplyLayout();
    }

    private void OnEnable()
    {
        EnsureRenderer();
        ApplyLayout();
    }

    private void OnValidate()
    {
        EnsureRenderer();
        ApplyLayout();
    }

    private void LateUpdate()
    {
        if (!followCamera)
        {
            return;
        }

        var cameraRef = ResolveCamera();
        if (cameraRef == null)
        {
            return;
        }

        var currentScreenSize = new Vector2(Screen.width, Screen.height);
        if ((currentScreenSize - lastScreenSize).sqrMagnitude > 0.01f)
        {
            ApplyLayout();
            return;
        }

        if (targetRenderer != null)
        {
            var targetPos = new Vector3(
                cameraRef.transform.position.x + positionOffset.x,
                cameraRef.transform.position.y + positionOffset.y,
                zPosition);
            if ((targetRenderer.transform.position - targetPos).sqrMagnitude > 0.0001f)
            {
                targetRenderer.transform.position = targetPos;
            }
        }
    }

    [ContextMenu("Apply Background Layout")]
    public void ApplyLayout()
    {
        EnsureRenderer();
        var cameraRef = ResolveCamera();
        if (targetRenderer == null || backgroundSprite == null || cameraRef == null)
        {
            return;
        }

        targetRenderer.sprite = backgroundSprite;
        targetRenderer.sortingLayerName = sortingLayerName;
        targetRenderer.sortingOrder = sortingOrder;

        var spriteSize = backgroundSprite.bounds.size;
        if (spriteSize.x <= 0.0001f || spriteSize.y <= 0.0001f)
        {
            return;
        }

        var worldHeight = cameraRef.orthographicSize * 2f;
        var worldWidth = worldHeight * cameraRef.aspect;

        var scaleX = worldWidth / spriteSize.x;
        var scaleY = worldHeight / spriteSize.y;
        var uniformScale = fitMode == BackgroundFitMode.Cover
            ? Mathf.Max(scaleX, scaleY)
            : Mathf.Min(scaleX, scaleY);

        targetRenderer.transform.localScale = new Vector3(uniformScale, uniformScale, 1f);
        targetRenderer.transform.position = new Vector3(
            cameraRef.transform.position.x + positionOffset.x,
            cameraRef.transform.position.y + positionOffset.y,
            zPosition);

        lastScreenSize = new Vector2(Screen.width, Screen.height);
    }

    private void EnsureRenderer()
    {
        if (targetRenderer != null)
        {
            return;
        }

        targetRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (targetRenderer != null)
        {
            return;
        }

        var node = new GameObject("BackgroundSprite");
        node.transform.SetParent(transform, false);
        targetRenderer = node.AddComponent<SpriteRenderer>();
    }

    private Camera ResolveCamera()
    {
        if (targetCamera != null)
        {
            return targetCamera;
        }

        if (Camera.main != null)
        {
            return Camera.main;
        }

        return Camera.current;
    }
}
