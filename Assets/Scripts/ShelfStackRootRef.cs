using UnityEngine;

[DisallowMultipleComponent]
public class ShelfStackRootRef : MonoBehaviour
{
    [SerializeField] private ShelfInteractionController shelf;

    public ShelfInteractionController Shelf => shelf;

    public void Bind(ShelfInteractionController targetShelf)
    {
        shelf = targetShelf;
    }
}