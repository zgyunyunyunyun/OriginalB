using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CatUnlockItemView : MonoBehaviour
{
    [SerializeField] private Image catImage;
    [SerializeField] private TMP_Text catNameText;
    [SerializeField] private TMP_Text stateText;
    [SerializeField] private GameObject lockedMaskRoot;
    [SerializeField] private string unlockedText = "已解锁";
    [SerializeField] private string lockedText = "未解锁";

    public void Bind(Sprite sprite, string catName, bool unlocked)
    {
        if (catImage != null)
        {
            catImage.sprite = sprite;
            catImage.enabled = sprite != null;
            catImage.color = unlocked ? Color.white : Color.black;
        }

        if (catNameText != null)
        {
            catNameText.text = unlocked
                ? (string.IsNullOrWhiteSpace(catName) ? "未命名小猫" : catName)
                : "未解锁";
        }

        if (stateText != null)
        {
            stateText.text = unlocked ? unlockedText : lockedText;
        }

        if (lockedMaskRoot != null)
        {
            lockedMaskRoot.SetActive(!unlocked);
        }
    }
}
