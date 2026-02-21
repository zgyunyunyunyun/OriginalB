using System.Collections;
using OriginalB.Platform.Core;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;
using UnityEngine;

[DisallowMultipleComponent]
public class BoxInteractionController : MonoBehaviour
{
    [Header("Debug")]
    [SerializeField] private bool enableInteractionDebugLog = true;

    [SerializeField, Min(0f)] private float clickDragThreshold = 0.08f;
    [SerializeField, Min(0f)] private float liftHeight = 0.25f;
    [SerializeField, Min(0f)] private float liftDuration = 0.08f;
    [SerializeField, Min(0f)] private float snapBackDuration = 0.12f;
    [SerializeField, Min(0f)] private float swayFrequency = 2.2f;
    [SerializeField, Min(0f)] private float swayAngle = 7f;
    [SerializeField, Min(0f)] private float clickMoveSpeed = 8f;
    [SerializeField]
    private AnimationCurve clickMoveEase = new AnimationCurve(
        new Keyframe(0f, 0f, 2.5f, 2.5f),
        new Keyframe(1f, 1f, 0f, 0f));

    private static BoxInteractionController activeSwayingBox;
    private static int lastGlobalClickProbeFrame = -1;
    private const string LogTag = "BoxInteract";

    private Camera cachedCamera;
    private Vector3 pointerDownWorld;
    private Vector3 dragOffset;
    private Vector3 shelfTopPosition;
    private Vector3 swayPivotPosition;
    private float cameraDepth;
    private float swayStartTime;
    private bool pointerHeld;
    private bool dragging;
    private bool isSwaying;
    private bool isMovingToShelf;
    private Quaternion shelfTopRotation;
    private Coroutine animationRoutine;
    private IInputService inputService;

    private void Awake()
    {
        if (!ServiceLocator.TryResolve<IInputService>(out inputService))
        {
            inputService = new CommonInputService();
        }

        cachedCamera = Camera.main;
        shelfTopPosition = transform.position;
        shelfTopRotation = transform.rotation;
    }

    private void Update()
    {
        TryRunGlobalClickProbe();

        if (!isSwaying)
        {
            return;
        }

        if (IsPointerDown() && !IsPointerOnThisBox())
        {
            if (TryMoveToShelfUnderPointer(true))
            {
                return;
            }

            SnapBackToShelfTop(false);
            return;
        }

        transform.position = swayPivotPosition;
        var elapsed = Time.time - swayStartTime;
        var angle = Mathf.Sin(elapsed * Mathf.PI * 2f * Mathf.Max(0.01f, swayFrequency)) * swayAngle;
        transform.rotation = shelfTopRotation * Quaternion.Euler(0f, 0f, angle);
    }

    private void OnMouseDown()
    {
        LogInteraction("OnMouseDown enter");

        if (activeSwayingBox != null && activeSwayingBox != this)
        {
            var movedByClickMode = activeSwayingBox.TryMoveToShelfAtPointer(true);
            if (movedByClickMode)
            {
                LogInteraction("OnMouseDown consumed by active swaying box move");
                pointerHeld = false;
                dragging = false;
                return;
            }
        }

        if (!CanInteract())
        {
            pointerHeld = false;
            dragging = false;
            LogInteraction("OnMouseDown blocked: CanInteract=false");
            LogAllBoxStates("OnMouseDown blocked");
            return;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            LogWarn("OnMouseDown failed: Camera.main is null");
            return;
        }

        if (activeSwayingBox != null && activeSwayingBox != this)
        {
            activeSwayingBox.SnapBackToShelfTop(false);
        }

        if (isSwaying)
        {
            SnapBackToShelfTop(true);
        }

        shelfTopPosition = transform.position;
        shelfTopRotation = transform.rotation;

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        pointerHeld = true;
        dragging = false;
        pointerDownWorld = ReadPointerWorld();
        dragOffset = transform.position - pointerDownWorld;
        cameraDepth = Mathf.Abs(transform.position.z - cachedCamera.transform.position.z);
        LogInteraction($"OnMouseDown ready | pointer={pointerDownWorld} | dragOffset={dragOffset}");
    }

    private void OnMouseDrag()
    {
        if (!pointerHeld || !CanInteract())
        {
            if (pointerHeld)
            {
                LogInteraction("OnMouseDrag blocked: CanInteract=false");
            }

            return;
        }

        var pointerWorld = ReadPointerWorld();
        if (!dragging)
        {
            var distance = Vector2.Distance(pointerWorld, pointerDownWorld);
            if (distance < clickDragThreshold)
            {
                return;
            }

            dragging = true;
            LogInteraction("OnMouseDrag begin dragging");
        }

        var target = pointerWorld + dragOffset;
        target.z = transform.position.z;
        transform.position = target;
    }

    private void OnMouseUp()
    {
        if (!pointerHeld)
        {
            return;
        }

        pointerHeld = false;
        if (dragging)
        {
            dragging = false;
            LogInteraction("OnMouseUp after drag, try place");
            if (TryMoveToShelfUnderPointer(false))
            {
                LogInteraction("OnMouseUp drag place success");
                return;
            }

            LogInteraction("OnMouseUp drag place failed, snap back");
            SnapBackToShelfTop(false);
            return;
        }

        if (!CanInteract())
        {
            LogInteraction("OnMouseUp click blocked: CanInteract=false");
            return;
        }

        LogInteraction("OnMouseUp click -> lift and sway");
        animationRoutine = StartCoroutine(PlayLiftAndEnterSway());
    }

    private void OnDisable()
    {
        if (activeSwayingBox == this)
        {
            activeSwayingBox = null;
        }

        isSwaying = false;
        isMovingToShelf = false;
        pointerHeld = false;
        dragging = false;
    }

    private bool CanInteract()
    {
        if (isMovingToShelf)
        {
            return false;
        }

        var parent = transform.parent;
        if (parent == null)
        {
            return true;
        }

        var isTop = transform.GetSiblingIndex() == parent.childCount - 1;
        return isTop;
    }

    private Vector3 ReadPointerWorld()
    {
        if (cachedCamera == null)
        {
            return transform.position;
        }

        var pointer = GetPointerScreenPosition();
        var screen = new Vector3(pointer.x, pointer.y, 0f);
        screen.z = cameraDepth > 0.01f ? cameraDepth : Mathf.Abs(transform.position.z - cachedCamera.transform.position.z);
        var world = cachedCamera.ScreenToWorldPoint(screen);
        world.z = transform.position.z;
        return world;
    }

    private bool IsPointerOnThisBox()
    {
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            return false;
        }

        if (!TryGetPointerPoint(out var point))
        {
            return false;
        }

        var hits = Physics2D.OverlapPointAll(point, ~0);
        if (hits == null || hits.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            var box = hits[i].GetComponentInParent<BoxInteractionController>();
            if (box == this)
            {
                return true;
            }
        }

        return false;
    }

    private void SnapBackToShelfTop(bool immediate)
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        isSwaying = false;
        if (activeSwayingBox == this)
        {
            activeSwayingBox = null;
        }

        if (immediate)
        {
            transform.position = shelfTopPosition;
            transform.rotation = shelfTopRotation;
            return;
        }

        animationRoutine = StartCoroutine(SnapBackRoutine());
    }

    private bool TryMoveToShelfUnderPointer(bool animateMove)
    {
        return TryMoveToShelfAtPointer(animateMove);
    }

    private bool TryMoveToShelfAtPointer(bool animateMove)
    {
        if (!TryResolveShelfUnderPointer(out var targetShelf))
        {
            LogInteraction("TryMoveToShelfUnderPointer failed: no target shelf under pointer");
            return false;
        }

        var success = TryPlaceOnShelf(targetShelf, animateMove);
        LogInteraction($"TryMoveToShelfUnderPointer target={targetShelf.name} animate={animateMove} result={success}");
        if (!success)
        {
            LogAllBoxStates("TryMoveToShelfUnderPointer failed");
        }

        return success;
    }

    private bool TryPlaceOnShelf(ShelfInteractionController targetShelf, bool animateMove)
    {
        if (targetShelf == null || targetShelf.StackRoot == null)
        {
            LogInteraction("TryPlaceOnShelf fail: target shelf/stackRoot null");
            return false;
        }

        if (!TryGetCurrentShelf(out var sourceShelf) || sourceShelf == null)
        {
            LogInteraction("TryPlaceOnShelf fail: source shelf unresolved");
            return false;
        }

        if (sourceShelf == targetShelf || !sourceShelf.IsTopBox(transform))
        {
            LogInteraction($"TryPlaceOnShelf fail: invalid source/target | sameShelf={sourceShelf == targetShelf} | sourceIsTop={sourceShelf.IsTopBox(transform)}");
            return false;
        }

        if (!TryGetColorType(out var movingColor) || !targetShelf.CanAcceptColor(movingColor))
        {
            LogInteraction($"TryPlaceOnShelf fail: color/capacity check failed | colorResolved={TryGetColorType(out _)} | movingColor={movingColor}");
            return false;
        }

        if (!targetShelf.TryGetPlacementBottomPosition(out var targetBottom))
        {
            LogInteraction("TryPlaceOnShelf fail: target bottom not resolved");
            return false;
        }

        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        var sourcePosition = transform.position;
        var sourceRotation = transform.rotation;

        isSwaying = false;
        if (activeSwayingBox == this)
        {
            activeSwayingBox = null;
        }

        transform.SetParent(targetShelf.StackRoot, true);
        transform.rotation = targetShelf.transform.rotation;

        var targetRotation = transform.rotation;
        if (!TryAlignBoxAndGetTop(transform, targetBottom, out _))
        {
            transform.position = targetBottom;
        }

        var targetPosition = transform.position;

        transform.SetAsLastSibling();
        isMovingToShelf = animateMove;

        if (!animateMove || clickMoveSpeed <= 0.01f)
        {
            isMovingToShelf = false;
            TryApplySortingOrder(sourceShelf);
            TryApplySortingOrder(targetShelf);
            sourceShelf.RefreshBoxVisualStates();
            targetShelf.RefreshBoxVisualStates();
            shelfTopPosition = transform.position;
            shelfTopRotation = transform.rotation;
            NotifyMoveResolved(sourceShelf, targetShelf);
            LogInteraction($"TryPlaceOnShelf success immediate | from={sourceShelf.ShelfIndex} to={targetShelf.ShelfIndex}");
            return true;
        }

        transform.position = sourcePosition;
        transform.rotation = sourceRotation;
        animationRoutine = StartCoroutine(MoveToShelfRoutine(targetPosition, targetRotation, sourceShelf, targetShelf));
        LogInteraction($"TryPlaceOnShelf success animated | from={sourceShelf.ShelfIndex} to={targetShelf.ShelfIndex}");
        return true;
    }

    private bool TryResolveShelfUnderPointer(out ShelfInteractionController shelf)
    {
        shelf = null;

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            return false;
        }

        if (!TryGetPointerPoint(out var point))
        {
            return false;
        }

        var hits = Physics2D.OverlapPointAll(point, ~0);
        for (var i = 0; i < hits.Length; i++)
        {
            if (hits[i] == null)
            {
                continue;
            }

            var targetShelf = hits[i].GetComponentInParent<ShelfInteractionController>();
            if (targetShelf != null)
            {
                shelf = targetShelf;
                return true;
            }

            var otherBox = hits[i].GetComponent<BoxInteractionController>();
            if (otherBox != null && otherBox != this && otherBox.TryGetCurrentShelf(out var boxShelf) && boxShelf != null)
            {
                shelf = boxShelf;
                return true;
            }
        }

        return false;
    }

    private bool TryGetCurrentShelf(out ShelfInteractionController shelf)
    {
        shelf = null;
        if (transform.parent == null)
        {
            return false;
        }

        var rootRef = transform.parent.GetComponent<ShelfStackRootRef>();
        if (rootRef == null || rootRef.Shelf == null)
        {
            return false;
        }

        shelf = rootRef.Shelf;
        return true;
    }

    private bool TryGetColorType(out GameManager.BoxColor color)
    {
        color = GameManager.BoxColor.Red;
        var state = GetComponent<BoxVisualState>();
        if (state == null)
        {
            return false;
        }

        color = state.OriginalColorType;
        return true;
    }

    private static bool TryAlignBoxAndGetTop(Transform boxTransform, Vector3 targetBottom, out Vector3 top)
    {
        if (TryGetAnchorPair(boxTransform, out var bottomAnchor, out var topAnchor))
        {
            var delta = targetBottom - bottomAnchor.position;
            boxTransform.position += delta;
            top = topAnchor.position;
            return true;
        }

        var renderer = boxTransform.GetComponentInChildren<Renderer>(true);
        if (renderer != null)
        {
            var deltaY = targetBottom.y - renderer.bounds.min.y;
            boxTransform.position += new Vector3(0f, deltaY, 0f);
            top = new Vector3(renderer.bounds.center.x, renderer.bounds.max.y, boxTransform.position.z);
            return true;
        }

        var spriteRenderer = boxTransform.GetComponentInChildren<SpriteRenderer>(true);
        if (spriteRenderer != null)
        {
            var deltaY = targetBottom.y - spriteRenderer.bounds.min.y;
            boxTransform.position += new Vector3(0f, deltaY, 0f);
            top = new Vector3(spriteRenderer.bounds.center.x, spriteRenderer.bounds.max.y, boxTransform.position.z);
            return true;
        }

        top = targetBottom;
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

        return false;
    }

    private static void TryApplySortingOrder(ShelfInteractionController shelf)
    {
        if (shelf == null || shelf.StackRoot == null)
        {
            return;
        }

        for (var i = 0; i < shelf.StackRoot.childCount; i++)
        {
            var box = shelf.StackRoot.GetChild(i);
            var order = Mathf.Clamp(i, 0, 3);

            var spriteRenderers = box.GetComponentsInChildren<SpriteRenderer>(true);
            for (var s = 0; s < spriteRenderers.Length; s++)
            {
                spriteRenderers[s].sortingOrder = order;
            }

            var renderers = box.GetComponentsInChildren<Renderer>(true);
            for (var r = 0; r < renderers.Length; r++)
            {
                if (renderers[r] is SpriteRenderer)
                {
                    continue;
                }

                renderers[r].sortingOrder = order;
            }
        }
    }

    private static void NotifyMoveResolved(ShelfInteractionController sourceShelf, ShelfInteractionController targetShelf)
    {
        var manager = FindObjectOfType<ShelfSpawnManager>();
        if (manager == null)
        {
            return;
        }

        manager.HandleBoxMoved(sourceShelf, targetShelf);
    }

    private IEnumerator MoveToShelfRoutine(
        Vector3 targetPosition,
        Quaternion targetRotation,
        ShelfInteractionController sourceShelf,
        ShelfInteractionController targetShelf)
    {
        var startPosition = transform.position;
        var startRotation = transform.rotation;
        var speed = Mathf.Max(0.01f, clickMoveSpeed);
        var distance = Vector3.Distance(startPosition, targetPosition);
        var duration = distance / speed;
        if (duration <= 0.0001f)
        {
            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }
        else
        {
            var elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                var t = Mathf.Clamp01(elapsed / duration);
                var easedT = clickMoveEase != null ? Mathf.Clamp01(clickMoveEase.Evaluate(t)) : t;
                transform.position = Vector3.LerpUnclamped(startPosition, targetPosition, easedT);
                transform.rotation = Quaternion.Slerp(startRotation, targetRotation, easedT);
                yield return null;
            }

            transform.position = targetPosition;
            transform.rotation = targetRotation;
        }

        isMovingToShelf = false;
        TryApplySortingOrder(sourceShelf);
        TryApplySortingOrder(targetShelf);
        sourceShelf.RefreshBoxVisualStates();
        targetShelf.RefreshBoxVisualStates();
        shelfTopPosition = transform.position;
        shelfTopRotation = transform.rotation;
        animationRoutine = null;
        NotifyMoveResolved(sourceShelf, targetShelf);
        LogInteraction($"MoveToShelfRoutine complete | from={sourceShelf.ShelfIndex} to={targetShelf.ShelfIndex}");
    }

    private IEnumerator SnapBackRoutine()
    {
        var fromPosition = transform.position;
        var fromRotation = transform.rotation;
        var duration = Mathf.Max(0.01f, snapBackDuration);
        var t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            var p = Mathf.Clamp01(t / duration);
            transform.position = Vector3.Lerp(fromPosition, shelfTopPosition, p);
            transform.rotation = Quaternion.Slerp(fromRotation, shelfTopRotation, p);
            yield return null;
        }

        transform.position = shelfTopPosition;
        transform.rotation = shelfTopRotation;
        animationRoutine = null;
        LogInteraction("SnapBackRoutine complete");
    }

    private IEnumerator PlayLiftAndEnterSway()
    {
        var startPosition = shelfTopPosition;
        var liftedPosition = shelfTopPosition + Vector3.up * liftHeight;

        var upTime = Mathf.Max(0.01f, liftDuration);
        var t = 0f;
        while (t < upTime)
        {
            t += Time.deltaTime;
            var p = Mathf.Clamp01(t / upTime);
            transform.position = Vector3.Lerp(startPosition, liftedPosition, p);
            transform.rotation = shelfTopRotation;
            yield return null;
        }

        transform.position = liftedPosition;
        transform.rotation = shelfTopRotation;
        swayPivotPosition = liftedPosition;
        swayStartTime = Time.time;
        isSwaying = true;
        activeSwayingBox = this;
        animationRoutine = null;
        LogInteraction("PlayLiftAndEnterSway complete");
    }

    private void TryRunGlobalClickProbe()
    {
        if (!enableInteractionDebugLog || !IsPointerDown())
        {
            return;
        }

        if (lastGlobalClickProbeFrame == Time.frameCount)
        {
            return;
        }

        lastGlobalClickProbeFrame = Time.frameCount;
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            LogWarn("GlobalClickProbe failed: Camera.main is null");
            return;
        }

        if (!TryGetPointerPoint(out var point))
        {
            return;
        }

        var hits = Physics2D.OverlapPointAll(point, ~0);
        LogInfo($"GlobalClickProbe | point={point} | hitCount={(hits != null ? hits.Length : 0)}");

        if (hits != null)
        {
            for (var i = 0; i < hits.Length; i++)
            {
                var collider = hits[i];
                if (collider == null)
                {
                    continue;
                }

                var box = collider.GetComponentInParent<BoxInteractionController>();
                var shelf = collider.GetComponentInParent<ShelfInteractionController>();
                var boxName = box != null ? box.name : "none";
                var shelfName = shelf != null ? shelf.name : "none";
                LogInfo($"GlobalClickProbe hit[{i}] collider={collider.name} layer={LayerMask.LayerToName(collider.gameObject.layer)} box={boxName} shelf={shelfName}");
            }
        }

        LogAllBoxStates("GlobalClickProbe snapshot");
    }

    private void LogAllBoxStates(string reason)
    {
        if (!enableInteractionDebugLog)
        {
            return;
        }

        var all = FindObjectsOfType<BoxInteractionController>(true);
        LogInfo($"BoxStateSnapshot begin | reason={reason} | total={all.Length}");
        for (var i = 0; i < all.Length; i++)
        {
            if (all[i] == null)
            {
                continue;
            }

            var box = all[i];
            var canInteract = box.CanInteract();
            var siblingIndex = box.transform.parent != null ? box.transform.GetSiblingIndex() : -1;
            var siblingCount = box.transform.parent != null ? box.transform.parent.childCount : 0;
            var hasCollider = box.TryGetComponent<Collider2D>(out var collider);
            var colliderEnabled = hasCollider && collider != null && collider.enabled;
            var isActive = box.isActiveAndEnabled && box.gameObject.activeInHierarchy;
            var colorText = box.TryGetColorType(out var c) ? c.ToString() : "Unknown";
            var shelfIndex = box.TryGetCurrentShelf(out var s) && s != null ? s.ShelfIndex : -1;
            LogInfo($"BoxState | box={box.name}#{box.GetInstanceID()} shelf={shelfIndex} sibling={siblingIndex}/{siblingCount} canInteract={canInteract} active={isActive} collider={colliderEnabled} moving={box.isMovingToShelf} swaying={box.isSwaying} color={colorText} pos={box.transform.position}");
        }

        LogInfo("BoxStateSnapshot end");
    }

    private void LogInteraction(string message)
    {
        if (!enableInteractionDebugLog)
        {
            return;
        }

        LogInfo($"box={name}#{GetInstanceID()} msg={message}");
    }

    private static void LogInfo(string message)
    {
        GameDebugLogger.Info(LogTag, message);
    }

    private static void LogWarn(string message)
    {
        GameDebugLogger.Warn(LogTag, message);
    }

    private bool IsPointerDown()
    {
        return inputService != null ? inputService.GetPointerDown() : Input.GetMouseButtonDown(0);
    }

    private Vector2 GetPointerScreenPosition()
    {
        return inputService != null ? inputService.GetPointerScreenPosition() : Input.mousePosition;
    }

    private bool TryGetPointerPoint(out Vector2 point)
    {
        point = default;
        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
            return false;
        }

        var pointer = GetPointerScreenPosition();
        var depth = cameraDepth > 0.01f ? cameraDepth : Mathf.Abs(transform.position.z - cachedCamera.transform.position.z);
        var world = cachedCamera.ScreenToWorldPoint(new Vector3(pointer.x, pointer.y, depth));
        point = new Vector2(world.x, world.y);
        return true;
    }
}