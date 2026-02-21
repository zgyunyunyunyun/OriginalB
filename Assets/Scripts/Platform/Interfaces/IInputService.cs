using UnityEngine;

namespace OriginalB.Platform.Interfaces
{
    public interface IInputService
    {
        bool GetPointerDown(int pointerId = 0);
        bool GetPointer(int pointerId = 0);
        bool GetPointerUp(int pointerId = 0);
        Vector2 GetPointerScreenPosition(int pointerId = 0);
        bool TryGetPointerWorld(Camera cameraRef, float worldDepth, out Vector3 worldPosition, int pointerId = 0);
    }
}
