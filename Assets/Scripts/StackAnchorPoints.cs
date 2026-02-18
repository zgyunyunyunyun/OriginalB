using UnityEngine;

[DisallowMultipleComponent]
public class StackAnchorPoints : MonoBehaviour
{
    [SerializeField] private Transform bottomAnchor;
    [SerializeField] private Transform topAnchor;

    public Transform BottomAnchor => bottomAnchor;
    public Transform TopAnchor => topAnchor;

    public bool HasValidAnchors => bottomAnchor != null && topAnchor != null;
}
