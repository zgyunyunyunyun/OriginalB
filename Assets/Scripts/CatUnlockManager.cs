using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class CatUnlockManager : MonoBehaviour
{
    [Serializable]
    public class CatUnlockConfig
    {
        public string catId;
        public string catName = "小猫";
        public Sprite catSprite;
    }

    [Header("Unlock Rules")]
    [SerializeField, Min(1)] private int firstUnlockLevel = 5;
    [SerializeField, Min(1)] private int unlockInterval = 10;
    [SerializeField] private string unlockedCountSaveKey = "CatUnlock.UnlockedCount";

    [Header("Cat Config")]
    [SerializeField] private List<CatUnlockConfig> cats = new List<CatUnlockConfig>();

    [Header("Collection Popup")]
    [SerializeField] private Button openPopupButton;
    [SerializeField] private GameObject popupRoot;
    [SerializeField] private Button closePopupButton;
    [SerializeField] private TMP_Text popupTitleText;
    [SerializeField] private string popupTitle = "解锁小猫";
    [SerializeField] private ScrollRect popupScrollRect;
    [SerializeField] private RectTransform popupContentRoot;
    [SerializeField] private GameObject popupItemPrefab;

    [Header("Unlock Win Panel")]
    [SerializeField] private GameObject unlockWinPanelRoot;
    [SerializeField] private TMP_Text unlockWinCoinRewardText;
    [SerializeField] private TMP_Text unlockWinTipText;
    [SerializeField] private Image unlockWinCatImage;
    [SerializeField] private Button unlockWinNextLevelButton;
    [SerializeField] private string unlockWinCoinRewardPrefix = "获得金币";
    [SerializeField] private string unlockWinTipTemplate = "解锁{0}猫";

    private readonly List<GameObject> itemPool = new List<GameObject>();
    private Action unlockWinNextAction;

    public int UnlockedCount => Mathf.Clamp(PlayerPrefs.GetInt(unlockedCountSaveKey, 0), 0, cats.Count);

    public bool TryGetCatForLevel(int levelIndex, out CatUnlockConfig catConfig)
    {
        catConfig = null;
        if (cats == null || cats.Count <= 0)
        {
            return false;
        }

        var catStep = ResolveCatStepByLevel(levelIndex);
        catConfig = FindCatByStep(catStep);
        return catConfig != null;
    }

    public bool TryGetCatVisualForLevel(int levelIndex, out Sprite sprite, out Color color)
    {
        sprite = null;
        color = Color.white;
        if (!TryGetCatForLevel(levelIndex, out var catConfig) || catConfig == null)
        {
            return false;
        }

        sprite = catConfig.catSprite;
        color = Color.white;
        return sprite != null;
    }

    private void Awake()
    {
        BindButtons();
        ApplyPopupTitle();
        RefreshPopup();
        ClearWinUnlockDisplay();
        HideUnlockWinPanel();

        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    private void OnEnable()
    {
        BindButtons();
        ApplyPopupTitle();
    }

    public bool ShowUnlockWinPanel(int reward, CatUnlockConfig unlockedCat, Action onNextLevel, bool showNextLevelButton)
    {
        if (unlockWinPanelRoot == null || unlockedCat == null)
        {
            return false;
        }

        unlockWinNextAction = onNextLevel;
        BindButtons();

        if (unlockWinCoinRewardText != null)
        {
            unlockWinCoinRewardText.text = $"{unlockWinCoinRewardPrefix} {Mathf.Max(0, reward)}";
        }

        if (unlockWinTipText != null)
        {
            var catName = string.IsNullOrWhiteSpace(unlockedCat.catName) ? "小" : unlockedCat.catName;
            var tipTemplate = string.IsNullOrWhiteSpace(unlockWinTipTemplate) ? "解锁{0}猫" : unlockWinTipTemplate;
            unlockWinTipText.text = string.Format(tipTemplate, catName);
        }

        if (unlockWinCatImage != null)
        {
            unlockWinCatImage.sprite = unlockedCat.catSprite;
            unlockWinCatImage.enabled = unlockedCat.catSprite != null;
            unlockWinCatImage.color = Color.white;
        }

        if (unlockWinNextLevelButton != null)
        {
            unlockWinNextLevelButton.gameObject.SetActive(showNextLevelButton);
        }

        unlockWinPanelRoot.SetActive(true);
        return true;
    }

    public void HideUnlockWinPanel()
    {
        if (unlockWinPanelRoot != null)
        {
            unlockWinPanelRoot.SetActive(false);
        }

        unlockWinNextAction = null;
    }

    public void OpenPopup()
    {
        if (popupRoot == null)
        {
            return;
        }

        popupRoot.SetActive(true);
        RefreshPopup();
        if (popupScrollRect != null)
        {
            popupScrollRect.verticalNormalizedPosition = 1f;
        }
    }

    public void ClosePopup()
    {
        if (popupRoot != null)
        {
            popupRoot.SetActive(false);
        }
    }

    public void RefreshPopup()
    {
        if (popupContentRoot == null || popupItemPrefab == null)
        {
            return;
        }

        var unlockedCount = UnlockedCount;
        EnsureItemPool(cats.Count);

        for (var i = 0; i < itemPool.Count; i++)
        {
            var itemGo = itemPool[i];
            if (itemGo == null)
            {
                continue;
            }

            var shouldShow = i < cats.Count;
            itemGo.SetActive(shouldShow);
            if (!shouldShow)
            {
                continue;
            }

            var cat = cats[i];
            var step = ResolveCatStep(cat, i);
            var unlocked = step < unlockedCount;
            BindPopupItem(itemGo, cat != null ? cat.catSprite : null, cat != null ? cat.catName : string.Empty, unlocked);
        }

        ResizePopupContent(cats.Count);
    }

    public bool TryHandleLevelWin(int levelIndex, out CatUnlockConfig unlockedCat)
    {
        unlockedCat = null;

        if (cats.Count <= 0)
        {
            ClearWinUnlockDisplay();
            return false;
        }

        var safeLevel = Mathf.Max(1, levelIndex);
        if (!IsUnlockCheckpointLevel(safeLevel))
        {
            ClearWinUnlockDisplay();
            return false;
        }

        var unlockedCount = UnlockedCount;
        var targetStep = ResolveCatStepByLevel(safeLevel);
        var targetUnlockedCount = Mathf.Clamp(targetStep + 1, 0, cats.Count);
        if (targetUnlockedCount <= unlockedCount)
        {
            ClearWinUnlockDisplay();
            return false;
        }

        unlockedCat = FindCatByStep(targetStep);
        var newUnlockedCount = targetUnlockedCount;

        PlayerPrefs.SetInt(unlockedCountSaveKey, newUnlockedCount);
        PlayerPrefs.Save();
        RefreshPopup();
        return unlockedCat != null;
    }

    public void ClearWinUnlockDisplay()
    {
        HideUnlockWinPanel();
    }

    public void ClearLocalUnlockData()
    {
        if (!string.IsNullOrWhiteSpace(unlockedCountSaveKey))
        {
            PlayerPrefs.DeleteKey(unlockedCountSaveKey);
            PlayerPrefs.Save();
        }

        HideUnlockWinPanel();
        RefreshPopup();
    }

    private int CalculateUnlockCountByLevel(int levelIndex)
    {
        var safeLevel = Mathf.Max(1, levelIndex);
        if (safeLevel < Mathf.Max(1, firstUnlockLevel))
        {
            return 0;
        }

        return 1 + (safeLevel - Mathf.Max(1, firstUnlockLevel)) / Mathf.Max(1, unlockInterval);
    }

    private bool IsUnlockCheckpointLevel(int levelIndex)
    {
        var safeLevel = Mathf.Max(1, levelIndex);
        if (safeLevel < Mathf.Max(1, firstUnlockLevel))
        {
            return false;
        }

        return (safeLevel - firstUnlockLevel) % Mathf.Max(1, unlockInterval) == 0;
    }

    private int ResolveCatStepByLevel(int levelIndex)
    {
        var safeLevel = Mathf.Max(1, levelIndex);
        if (safeLevel <= firstUnlockLevel)
        {
            return 0;
        }

        var step = 1 + (safeLevel - (firstUnlockLevel + 1)) / Mathf.Max(1, unlockInterval);
        return Mathf.Max(0, step);
    }

    private CatUnlockConfig FindCatByStep(int step)
    {
        if (cats == null || cats.Count <= 0)
        {
            return null;
        }

        var safeStep = Mathf.Max(0, step);
        for (var i = 0; i < cats.Count; i++)
        {
            var cat = cats[i];
            if (cat == null)
            {
                continue;
            }

            if (ResolveCatStep(cat, i) == safeStep)
            {
                return cat;
            }
        }

        if (safeStep >= 0 && safeStep < cats.Count)
        {
            return cats[safeStep];
        }

        return cats[cats.Count - 1];
    }

    private static int ResolveCatStep(CatUnlockConfig cat, int fallbackIndex)
    {
        if (cat != null && !string.IsNullOrWhiteSpace(cat.catId)
            && int.TryParse(cat.catId, out var parsed)
            && parsed >= 0)
        {
            return parsed;
        }

        return Mathf.Max(0, fallbackIndex);
    }

    private void BindButtons()
    {
        if (openPopupButton != null)
        {
            openPopupButton.onClick.RemoveListener(OpenPopup);
            openPopupButton.onClick.AddListener(OpenPopup);
        }

        if (closePopupButton != null)
        {
            closePopupButton.onClick.RemoveListener(ClosePopup);
            closePopupButton.onClick.AddListener(ClosePopup);
        }

        if (unlockWinNextLevelButton != null)
        {
            unlockWinNextLevelButton.onClick.RemoveListener(OnUnlockWinNextLevelClicked);
            unlockWinNextLevelButton.onClick.AddListener(OnUnlockWinNextLevelClicked);
        }
    }

    private void OnUnlockWinNextLevelClicked()
    {
        unlockWinNextAction?.Invoke();
    }

    private void EnsureItemPool(int targetCount)
    {
        if (popupContentRoot == null || popupItemPrefab == null)
        {
            return;
        }

        while (itemPool.Count < targetCount)
        {
            var item = Instantiate(popupItemPrefab, popupContentRoot);
            item.name = $"CatItem_{itemPool.Count + 1}";
            itemPool.Add(item);
        }
    }

    private static void BindPopupItem(GameObject itemGo, Sprite sprite, string catName, bool unlocked)
    {
        if (itemGo == null)
        {
            return;
        }

        var itemView = itemGo.GetComponent<CatUnlockItemView>();
        if (itemView != null)
        {
            itemView.Bind(sprite, catName, unlocked);
            return;
        }

        var image = itemGo.GetComponentInChildren<Image>(true);
        if (image != null)
        {
            image.sprite = sprite;
            image.enabled = sprite != null;
            image.color = unlocked ? Color.white : Color.black;
        }

        var texts = itemGo.GetComponentsInChildren<TMP_Text>(true);
        if (texts != null && texts.Length > 0 && texts[0] != null)
        {
            texts[0].text = unlocked
                ? (string.IsNullOrWhiteSpace(catName) ? "未命名小猫" : catName)
                : "未解锁";
        }
    }

    private void ResizePopupContent(int itemCount)
    {
        if (popupContentRoot == null)
        {
            return;
        }

        var count = Mathf.Max(0, itemCount);
        var targetHeight = popupContentRoot.sizeDelta.y;

        var gridLayout = popupContentRoot.GetComponent<GridLayoutGroup>();
        if (gridLayout != null)
        {
            var columns = ResolveGridColumnCount(gridLayout, count);
            var rows = Mathf.Max(1, Mathf.CeilToInt(count / (float)Mathf.Max(1, columns)));
            targetHeight = gridLayout.padding.top
                + gridLayout.padding.bottom
                + rows * gridLayout.cellSize.y
                + Mathf.Max(0, rows - 1) * gridLayout.spacing.y;
        }
        else
        {
            var preferredHeight = LayoutUtility.GetPreferredHeight(popupContentRoot);
            if (!float.IsNaN(preferredHeight) && !float.IsInfinity(preferredHeight))
            {
                targetHeight = preferredHeight;
            }
        }

        var size = popupContentRoot.sizeDelta;
        size.y = Mathf.Max(0f, targetHeight);
        popupContentRoot.sizeDelta = size;
        LayoutRebuilder.ForceRebuildLayoutImmediate(popupContentRoot);
    }

    private int ResolveGridColumnCount(GridLayoutGroup gridLayout, int itemCount)
    {
        if (gridLayout == null)
        {
            return 1;
        }

        if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
        {
            return Mathf.Max(1, gridLayout.constraintCount);
        }

        if (gridLayout.constraint == GridLayoutGroup.Constraint.FixedRowCount)
        {
            var rows = Mathf.Max(1, gridLayout.constraintCount);
            return Mathf.Max(1, Mathf.CeilToInt(Mathf.Max(1, itemCount) / (float)rows));
        }

        var width = popupContentRoot.rect.width;
        if (width <= 0.01f)
        {
            width = popupContentRoot.sizeDelta.x;
        }

        var availableWidth = Mathf.Max(0f, width - gridLayout.padding.left - gridLayout.padding.right);
        var cellWidth = Mathf.Max(1f, gridLayout.cellSize.x);
        var stepWidth = Mathf.Max(1f, cellWidth + gridLayout.spacing.x);
        var columns = Mathf.FloorToInt((availableWidth + gridLayout.spacing.x) / stepWidth);
        return Mathf.Max(1, columns);
    }

    private void ApplyPopupTitle()
    {
        if (popupTitleText != null)
        {
            popupTitleText.text = string.IsNullOrWhiteSpace(popupTitle) ? "解锁小猫" : popupTitle;
        }
    }
}
