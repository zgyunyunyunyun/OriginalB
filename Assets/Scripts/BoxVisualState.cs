using UnityEngine;

[DisallowMultipleComponent]
public class BoxVisualState : MonoBehaviour
{
    [SerializeField] private GameManager.BoxColor originalColorType;
    [SerializeField] private Color originalDisplayColor = Color.white;
    [SerializeField] private Color currentDisplayColor = Color.white;
    [SerializeField] private bool isGrayed;

    public GameManager.BoxColor OriginalColorType => originalColorType;
    public Color OriginalDisplayColor => originalDisplayColor;
    public Color CurrentDisplayColor => currentDisplayColor;
    public bool IsGrayed => isGrayed;

    public void SetState(GameManager.BoxColor colorType, Color originalColor, Color currentColor, bool grayed)
    {
        originalColorType = colorType;
        originalDisplayColor = originalColor;
        currentDisplayColor = currentColor;
        isGrayed = grayed;
    }
}