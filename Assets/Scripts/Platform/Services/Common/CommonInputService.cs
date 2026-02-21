using OriginalB.Platform.Interfaces;
using UnityEngine;

namespace OriginalB.Platform.Services.Common
{
    public class CommonInputService : IInputService
    {
        public bool GetPointerDown(int pointerId = 0)
        {
            if (TryGetTouch(pointerId, out var touch))
            {
                return touch.phase == TouchPhase.Began;
            }

            return Input.GetMouseButtonDown(0);
        }

        public bool GetPointer(int pointerId = 0)
        {
            if (TryGetTouch(pointerId, out var touch))
            {
                return touch.phase == TouchPhase.Began ||
                       touch.phase == TouchPhase.Moved ||
                       touch.phase == TouchPhase.Stationary;
            }

            return Input.GetMouseButton(0);
        }

        public bool GetPointerUp(int pointerId = 0)
        {
            if (TryGetTouch(pointerId, out var touch))
            {
                return touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;
            }

            return Input.GetMouseButtonUp(0);
        }

        public Vector2 GetPointerScreenPosition(int pointerId = 0)
        {
            if (TryGetTouch(pointerId, out var touch))
            {
                return touch.position;
            }

            return Input.mousePosition;
        }

        public bool TryGetPointerWorld(Camera cameraRef, float worldDepth, out Vector3 worldPosition, int pointerId = 0)
        {
            worldPosition = default;
            if (cameraRef == null)
            {
                return false;
            }

            var screenPosition = GetPointerScreenPosition(pointerId);
            var depth = Mathf.Max(0.01f, worldDepth);
            worldPosition = cameraRef.ScreenToWorldPoint(new Vector3(screenPosition.x, screenPosition.y, depth));
            return true;
        }

        private static bool TryGetTouch(int pointerId, out Touch touch)
        {
            if (Input.touchCount > pointerId)
            {
                touch = Input.GetTouch(pointerId);
                return true;
            }

            touch = default;
            return false;
        }
    }
}
