using UnityEngine;

[DisallowMultipleComponent]
public class BoxVisualState : MonoBehaviour
{
    [SerializeField] private GameManager.BoxColor originalColorType;
    [SerializeField] private Color originalDisplayColor = Color.white;
    [SerializeField] private Color currentDisplayColor = Color.white;
    [SerializeField] private bool isGrayed;
    [SerializeField] private bool hasRevealedOriginalPermanently;

    public GameManager.BoxColor OriginalColorType => originalColorType;
    public Color OriginalDisplayColor => originalDisplayColor;
    public Color CurrentDisplayColor => currentDisplayColor;
    public bool IsGrayed => isGrayed;
    public bool HasRevealedOriginalPermanently => hasRevealedOriginalPermanently;

    public void SetState(GameManager.BoxColor colorType, Color originalColor, Color currentColor, bool grayed)
    {
        originalColorType = colorType;
        originalDisplayColor = originalColor;
        currentDisplayColor = currentColor;
        isGrayed = grayed;
        hasRevealedOriginalPermanently = !grayed || ApproximatelyColor(currentColor, originalColor);
    }

    public void ApplyDisplayByTopState(bool isTop)
    {
        if (isTop)
        {
            hasRevealedOriginalPermanently = true;
        }

        var targetColor = hasRevealedOriginalPermanently ? originalDisplayColor : currentDisplayColor;
        ApplyDisplayColor(targetColor);
    }

    private static bool ApproximatelyColor(Color a, Color b)
    {
        const float tolerance = 0.0001f;
        return Mathf.Abs(a.r - b.r) <= tolerance
            && Mathf.Abs(a.g - b.g) <= tolerance
            && Mathf.Abs(a.b - b.b) <= tolerance
            && Mathf.Abs(a.a - b.a) <= tolerance;
    }

    private void ApplyDisplayColor(Color displayColor)
    {
        var spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (var i = 0; i < spriteRenderers.Length; i++)
        {
            spriteRenderers[i].color = displayColor;
        }

        var renderers = GetComponentsInChildren<Renderer>(true);
        var propertyBlock = new MaterialPropertyBlock();
        for (var i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] is SpriteRenderer)
            {
                continue;
            }

            renderers[i].GetPropertyBlock(propertyBlock);
            var hasBaseColor = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_BaseColor");
            var hasColor = renderers[i].sharedMaterial != null && renderers[i].sharedMaterial.HasProperty("_Color");

            if (hasBaseColor)
            {
                propertyBlock.SetColor("_BaseColor", displayColor);
            }

            if (hasColor)
            {
                propertyBlock.SetColor("_Color", displayColor);
            }

            renderers[i].SetPropertyBlock(propertyBlock);
        }
    }
}