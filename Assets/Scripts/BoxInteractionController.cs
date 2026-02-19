using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public class BoxInteractionController : MonoBehaviour
{
    [SerializeField, Min(0f)] private float clickDragThreshold = 0.08f;
    [SerializeField, Min(0f)] private float liftHeight = 0.25f;
    [SerializeField, Min(0f)] private float liftDuration = 0.08f;
    [SerializeField, Min(0f)] private float snapBackDuration = 0.12f;
    [SerializeField, Min(0f)] private float swayFrequency = 2.2f;
    [SerializeField, Min(0f)] private float swayAngle = 7f;

    private static BoxInteractionController activeSwayingBox;

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
    private Quaternion shelfTopRotation;
    private Coroutine animationRoutine;

    private void Awake()
    {
        cachedCamera = Camera.main;
        shelfTopPosition = transform.position;
        shelfTopRotation = transform.rotation;
    }

    private void Update()
    {
        if (!isSwaying)
        {
            return;
        }

        if (Input.GetMouseButtonDown(0) && !IsPointerOnThisBox())
        {
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
        if (!CanInteract())
        {
            pointerHeld = false;
            dragging = false;
            return;
        }

        if (cachedCamera == null)
        {
            cachedCamera = Camera.main;
        }

        if (cachedCamera == null)
        {
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
    }

    private void OnMouseDrag()
    {
        if (!pointerHeld || !CanInteract())
        {
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
            SnapBackToShelfTop(false);
            return;
        }

        if (!CanInteract())
        {
            return;
        }

        animationRoutine = StartCoroutine(PlayLiftAndEnterSway());
    }

    private void OnDisable()
    {
        if (activeSwayingBox == this)
        {
            activeSwayingBox = null;
        }

        isSwaying = false;
        pointerHeld = false;
        dragging = false;
    }

    private bool CanInteract()
    {
        var parent = transform.parent;
        if (parent == null)
        {
            return true;
        }

        return transform.GetSiblingIndex() == parent.childCount - 1;
    }

    private Vector3 ReadPointerWorld()
    {
        if (cachedCamera == null)
        {
            return transform.position;
        }

        var screen = Input.mousePosition;
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

        var world = cachedCamera.ScreenToWorldPoint(Input.mousePosition);
        var point = new Vector2(world.x, world.y);
        var hit = Physics2D.Raycast(point, Vector2.zero);
        if (hit.collider == null)
        {
            return false;
        }

        return hit.collider.transform == transform;
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
    }
}