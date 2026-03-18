using System;
using System.Collections;
using System.Collections.Generic;
using OriginalB.Platform.Core;
using OriginalB.Platform.Interfaces;
using OriginalB.Platform.Services.Common;
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
        [Min(0)] public int unlockLevel;
        [Min(0)] public int unlockCoinCost;
    }

    [Header("Unlock Rules")]
    [SerializeField, Min(1)] private int defaultFirstUnlockLevel = 5;
    [SerializeField, Min(1)] private int defaultUnlockStep = 10;
    [SerializeField] private string unlockedCatIdsSaveKey = "CatUnlock.UnlockedIds";
    [SerializeField, HideInInspector] private int lastValidatedCatCount;

    [Header("Legacy Migration")]
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

    [Header("Cat Detail Popup")]
    [SerializeField] private GameObject catDetailPopupRoot;
    [SerializeField] private Image catDetailImage;
    [SerializeField] private TMP_Text catDetailNameText;
    [SerializeField] private TMP_Text catDetailConditionText;
    [SerializeField] private Button catDetailMaskButton;
    [SerializeField] private Button catDetailUnlockButton;
    [SerializeField] private string lockedCatNameText = "未解锁";
    [SerializeField] private string unlockConditionPrefix = "解锁：";
    [SerializeField] private string unlockConditionSeparator = "、";
    [SerializeField] private string unlockConditionCoinTemplate = "{0}金币";
    [SerializeField] private string unlockConditionLevelTemplate = "通过第{0}关";
    [SerializeField] private string unlockConditionNoneText = "未配置解锁条件";
    [SerializeField] private string catDetailUnlockButtonText = "解锁";

    [Header("Coin Unlock Bubble")]
    [SerializeField] private RectTransform coinUnlockFailedBubbleRootRef;
    [SerializeField] private TMP_Text coinUnlockFailedBubbleTextRefConfig;
    [SerializeField] private string coinUnlockFailedBubbleText = "解锁失败，金币不足";
    [SerializeField] private string coinUnlockLevelLockedBubbleText = "解锁失败，未达到关卡条件";
    [SerializeField] private Vector2 coinUnlockFailedBubbleSize = new Vector2(520f, 72f);
    [SerializeField] private Vector2 coinUnlockFailedBubblePosition = new Vector2(0f, 120f);
    [SerializeField, Min(0.1f)] private float coinUnlockFailedBubbleDuration = 1.8f;
    [SerializeField] private string economyCoinSaveKey = "ShelfSpawn.Economy.Coin";
    [SerializeField] private string levelProgressSaveKey = "ShelfSpawn.Progress.CurrentLevelIndex";

    private readonly List<GameObject> itemPool = new List<GameObject>();
    private Action unlockWinNextAction;
    private IStorageService storageService;
    private ShelfSpawnManager shelfSpawnManager;
    private CatUnlockConfig activeDetailCat;
    private int activeDetailCatIndex = -1;
    private bool activeDetailUnlocked;
    private int activeDetailUnlockLevel;
    private int activeDetailUnlockCoinCost;
    private RectTransform coinUnlockFailedBubbleRoot;
    private TMP_Text coinUnlockFailedBubbleTextRef;
    private Coroutine coinUnlockFailedBubbleRoutine;

    public int UnlockedCount => Mathf.Clamp(GetUnlockedCountBySet(), 0, cats.Count);

    public bool TryGetCatForLevel(int levelIndex, out CatUnlockConfig catConfig)
    {
        catConfig = null;
        if (cats == null || cats.Count <= 0)
        {
            return false;
        }

        catConfig = FindCatForLevelDisplay(Mathf.Max(1, levelIndex));
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
        if (!ServiceLocator.TryResolve<IStorageService>(out storageService))
        {
            storageService = new CommonStorageService();
        }

        BindButtons();
        MigrateLegacyUnlockCountIfNeeded();
        ApplyPopupTitle();
        RefreshPopup();
        ClearWinUnlockDisplay();
        HideUnlockWinPanel();
        HideCatDetailPopup();

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
        HideCatDetailPopup();
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

        HideCatDetailPopup();
    }

    public void RefreshPopup()
    {
        if (popupContentRoot == null || popupItemPrefab == null)
        {
            return;
        }

        var unlockedSet = GetUnlockedCatTokenSet();
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
            var unlocked = IsCatUnlocked(cat, i, unlockedSet);
            BindPopupItem(itemGo, cat != null ? cat.catSprite : null, cat != null ? cat.catName : string.Empty, unlocked);
            BindPopupItemClick(itemGo, cat, i, unlocked, ResolveDisplayUnlockLevel(cat, i), ResolveDisplayUnlockCoinCost(cat));
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
        var unlockedSet = GetUnlockedCatTokenSet();
        CatUnlockConfig latestUnlockedCat = null;

        for (var i = 0; i < cats.Count; i++)
        {
            var cat = cats[i];
            if (cat == null || IsCatUnlocked(cat, i, unlockedSet))
            {
                continue;
            }

            if (!CanUnlockByLevel(cat, i, safeLevel) || ResolveDisplayUnlockCoinCost(cat) > 0)
            {
                continue;
            }

            unlockedSet.Add(BuildCatUnlockToken(cat, i));
            latestUnlockedCat = cat;
        }

        if (latestUnlockedCat == null)
        {
            ClearWinUnlockDisplay();
            return false;
        }

        SaveUnlockedCatTokenSet(unlockedSet);
        RefreshPopup();
        unlockedCat = latestUnlockedCat;
        return true;
    }

    public bool TryUnlockCatByCoin(string catId, int currentCoinCount, out CatUnlockConfig unlockedCat, out int cost)
    {
        unlockedCat = null;
        cost = 0;
        if (cats == null || cats.Count <= 0 || string.IsNullOrWhiteSpace(catId))
        {
            return false;
        }

        var unlockedSet = GetUnlockedCatTokenSet();
        for (var i = 0; i < cats.Count; i++)
        {
            var cat = cats[i];
            if (cat == null)
            {
                continue;
            }

            if (!string.Equals(cat.catId, catId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsCatUnlocked(cat, i, unlockedSet))
            {
                return false;
            }

            var currentLevel = GetCurrentProgressLevel();
            if (!IsLevelRequirementSatisfied(cat, i, currentLevel))
            {
                return false;
            }

            cost = ResolveDisplayUnlockCoinCost(cat);
            if (cost <= 0 || currentCoinCount < cost)
            {
                return false;
            }

            unlockedSet.Add(BuildCatUnlockToken(cat, i));
            SaveUnlockedCatTokenSet(unlockedSet);
            RefreshPopup();
            unlockedCat = cat;
            return true;
        }

        return false;
    }

    public bool CanUnlockCatByCoin(string catId, int currentCoinCount, out int cost)
    {
        cost = 0;
        if (cats == null || cats.Count <= 0 || string.IsNullOrWhiteSpace(catId))
        {
            return false;
        }

        var unlockedSet = GetUnlockedCatTokenSet();
        for (var i = 0; i < cats.Count; i++)
        {
            var cat = cats[i];
            if (cat == null)
            {
                continue;
            }

            if (!string.Equals(cat.catId, catId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (IsCatUnlocked(cat, i, unlockedSet))
            {
                return false;
            }

            var currentLevel = GetCurrentProgressLevel();
            if (!IsLevelRequirementSatisfied(cat, i, currentLevel))
            {
                return false;
            }

            cost = ResolveDisplayUnlockCoinCost(cat);
            return cost > 0 && currentCoinCount >= cost;
        }

        return false;
    }

    public void ClearWinUnlockDisplay()
    {
        HideUnlockWinPanel();
    }

    public void ClearLocalUnlockData()
    {
        storageService.SetString(GetUnlockedCatIdsStorageKey(), string.Empty);
        storageService.SetInt(GetLegacyUnlockedCountStorageKey(), 0);
        storageService.Save();

        HideUnlockWinPanel();
        RefreshPopup();
    }

    private int GetUnlockedCountBySet()
    {
        var unlockedSet = GetUnlockedCatTokenSet();
        var count = 0;
        if (cats == null)
        {
            return 0;
        }

        for (var i = 0; i < cats.Count; i++)
        {
            if (IsCatUnlocked(cats[i], i, unlockedSet))
            {
                count++;
            }
        }

        return count;
    }

    private string GetUnlockedCatIdsStorageKey()
    {
        return string.IsNullOrWhiteSpace(unlockedCatIdsSaveKey) ? "CatUnlock.UnlockedIds" : unlockedCatIdsSaveKey;
    }

    private string GetLegacyUnlockedCountStorageKey()
    {
        return string.IsNullOrWhiteSpace(unlockedCountSaveKey) ? "CatUnlock.UnlockedCount" : unlockedCountSaveKey;
    }

    private HashSet<string> GetUnlockedCatTokenSet()
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (storageService == null)
        {
            return result;
        }

        var raw = storageService.GetString(GetUnlockedCatIdsStorageKey(), string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
        {
            return result;
        }

        var parts = raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
        for (var i = 0; i < parts.Length; i++)
        {
            var token = parts[i] != null ? parts[i].Trim() : string.Empty;
            if (!string.IsNullOrWhiteSpace(token))
            {
                result.Add(token);
            }
        }

        return result;
    }

    private void SaveUnlockedCatTokenSet(HashSet<string> unlockedSet)
    {
        if (storageService == null)
        {
            return;
        }

        if (unlockedSet == null || unlockedSet.Count <= 0)
        {
            storageService.SetString(GetUnlockedCatIdsStorageKey(), string.Empty);
            storageService.Save();
            return;
        }

        var raw = string.Join(",", unlockedSet);
        storageService.SetString(GetUnlockedCatIdsStorageKey(), raw);
        storageService.Save();
    }

    private void MigrateLegacyUnlockCountIfNeeded()
    {
        if (storageService == null)
        {
            return;
        }

        var existingIds = storageService.GetString(GetUnlockedCatIdsStorageKey(), string.Empty);
        if (!string.IsNullOrWhiteSpace(existingIds))
        {
            return;
        }

        var legacyCount = Mathf.Max(0, storageService.GetInt(GetLegacyUnlockedCountStorageKey(), 0));
        if (legacyCount <= 0 || cats == null || cats.Count <= 0)
        {
            return;
        }

        var unlockedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var max = Mathf.Min(legacyCount, cats.Count);
        for (var i = 0; i < max; i++)
        {
            unlockedSet.Add(BuildCatUnlockToken(cats[i], i));
        }

        SaveUnlockedCatTokenSet(unlockedSet);
    }

    private CatUnlockConfig FindCatForLevelDisplay(int levelIndex)
    {
        if (cats == null || cats.Count <= 0)
        {
            return null;
        }

        var safeLevel = Mathf.Max(1, levelIndex);
        CatUnlockConfig best = null;
        var bestLevel = int.MinValue;

        for (var i = 0; i < cats.Count; i++)
        {
            var cat = cats[i];
            if (cat == null)
            {
                continue;
            }

            var level = ResolveDisplayUnlockLevel(cat, i);
            if (level > 0 && level <= safeLevel && level > bestLevel)
            {
                best = cat;
                bestLevel = level;
            }
        }

        if (best != null)
        {
            return best;
        }

        for (var i = 0; i < cats.Count; i++)
        {
            if (cats[i] != null)
            {
                return cats[i];
            }
        }

        return null;
    }

    private bool IsCatUnlocked(CatUnlockConfig cat, int index, HashSet<string> unlockedSet)
    {
        if (unlockedSet == null)
        {
            return false;
        }

        return unlockedSet.Contains(BuildCatUnlockToken(cat, index));
    }

    private static string BuildCatUnlockToken(CatUnlockConfig cat, int index)
    {
        if (cat != null && !string.IsNullOrWhiteSpace(cat.catId))
        {
            return cat.catId.Trim();
        }

        return $"index_{Mathf.Max(0, index)}";
    }

    private bool CanUnlockByLevel(CatUnlockConfig cat, int index, int levelIndex)
    {
        var unlockLevel = ResolveDisplayUnlockLevel(cat, index);
        if (unlockLevel <= 0)
        {
            return false;
        }

        // Level-win flow represents "just passed this level", so equal level should unlock.
        return Mathf.Max(1, levelIndex) >= unlockLevel;
    }

    private bool IsLevelRequirementSatisfied(CatUnlockConfig cat, int index, int levelIndex)
    {
        var unlockLevel = ResolveDisplayUnlockLevel(cat, index);
        if (unlockLevel <= 0)
        {
            return true;
        }

        return Mathf.Max(1, levelIndex) > unlockLevel;
    }

    private int ResolveDisplayUnlockLevel(CatUnlockConfig cat, int index)
    {
        return cat != null ? Mathf.Max(0, cat.unlockLevel) : 0;
    }

    private int ResolveDisplayUnlockCoinCost(CatUnlockConfig cat)
    {
        return cat != null ? Mathf.Max(0, cat.unlockCoinCost) : 0;
    }

    private int ResolveLegacyUnlockLevelByIndex(int index)
    {
        var safeIndex = Mathf.Max(0, index);
        var startLevel = Mathf.Max(1, firstUnlockLevel);
        var interval = Mathf.Max(1, unlockInterval);
        return startLevel + safeIndex * interval;
    }

    private void EnsureConfigDefaultsForExistingEntries(bool applyForNewEntriesOnly)
    {
        if (cats == null || cats.Count <= 0)
        {
            lastValidatedCatCount = 0;
            return;
        }

        if (!applyForNewEntriesOnly)
        {
            for (var i = 0; i < cats.Count; i++)
            {
                ApplyConfigDefaultForIndex(i);
            }

            lastValidatedCatCount = cats.Count;
            return;
        }

        var previousCount = Mathf.Clamp(lastValidatedCatCount, 0, cats.Count);
        if (cats.Count <= previousCount)
        {
            lastValidatedCatCount = cats.Count;
            return;
        }

        for (var i = previousCount; i < cats.Count; i++)
        {
            ApplyConfigDefaultForIndex(i);
        }

        lastValidatedCatCount = cats.Count;
    }

    private void ApplyConfigDefaultForIndex(int index)
    {
        if (cats == null || index < 0 || index >= cats.Count)
        {
            return;
        }

        var cat = cats[index];
        if (cat == null)
        {
            return;
        }

        if (index == 0)
        {
            if (cat.unlockLevel <= 0)
            {
                cat.unlockLevel = Mathf.Max(1, defaultFirstUnlockLevel);
            }

            return;
        }

        var prev = cats[index - 1];
        var prevUnlockLevel = prev != null ? Mathf.Max(0, prev.unlockLevel) : 0;
        if (prevUnlockLevel <= 0)
        {
            prevUnlockLevel = ResolveLegacyUnlockLevelByIndex(index - 1);
        }

        if (cat.unlockLevel <= 0)
        {
            cat.unlockLevel = prevUnlockLevel + Mathf.Max(1, defaultUnlockStep);
        }

        var prevUnlockCoinCost = prev != null ? Mathf.Max(0, prev.unlockCoinCost) : 0;
        if (cat.unlockCoinCost <= 0)
        {
            cat.unlockCoinCost = prevUnlockCoinCost + Mathf.Max(1, defaultUnlockStep);
        }
    }

    private void OnValidate()
    {
        EnsureConfigDefaultsForExistingEntries(true);
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

        if (catDetailMaskButton != null)
        {
            catDetailMaskButton.onClick.RemoveListener(HideCatDetailPopup);
            catDetailMaskButton.onClick.AddListener(HideCatDetailPopup);
        }

        if (catDetailUnlockButton != null)
        {
            catDetailUnlockButton.onClick.RemoveListener(OnCatDetailUnlockByCoinClicked);
            catDetailUnlockButton.onClick.AddListener(OnCatDetailUnlockByCoinClicked);
            ApplyButtonLabel(catDetailUnlockButton, catDetailUnlockButtonText);
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

    private void BindPopupItemClick(GameObject itemGo, CatUnlockConfig cat, int catIndex, bool unlocked, int unlockLevel, int unlockCoinCost)
    {
        var button = ResolveOrCreateItemButton(itemGo);
        if (button == null)
        {
            return;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => ShowCatDetailPopup(cat, catIndex, unlocked, unlockLevel, unlockCoinCost));
    }

    private static Button ResolveOrCreateItemButton(GameObject itemGo)
    {
        if (itemGo == null)
        {
            return null;
        }

        var button = itemGo.GetComponent<Button>();
        if (button != null)
        {
            return button;
        }

        button = itemGo.GetComponentInChildren<Button>(true);
        if (button != null)
        {
            return button;
        }

        button = itemGo.AddComponent<Button>();
        if (button.targetGraphic == null)
        {
            var graphic = itemGo.GetComponentInChildren<Graphic>(true);
            if (graphic != null)
            {
                button.targetGraphic = graphic;
            }
        }

        return button;
    }

    private void ShowCatDetailPopup(CatUnlockConfig cat, int catIndex, bool unlocked, int unlockLevel, int unlockCoinCost)
    {
        if (catDetailPopupRoot == null)
        {
            return;
        }

        activeDetailCat = cat;
        activeDetailCatIndex = catIndex;
        activeDetailUnlocked = unlocked;
        activeDetailUnlockLevel = Mathf.Max(0, unlockLevel);
        activeDetailUnlockCoinCost = Mathf.Max(0, unlockCoinCost);

        if (catDetailImage != null)
        {
            catDetailImage.sprite = cat != null ? cat.catSprite : null;
            catDetailImage.enabled = catDetailImage.sprite != null;
            catDetailImage.color = unlocked ? Color.white : Color.black;
        }

        if (catDetailNameText != null)
        {
            var catName = cat != null ? cat.catName : string.Empty;
            catDetailNameText.text = string.IsNullOrWhiteSpace(catName) ? "未命名小猫" : catName;
        }

        if (catDetailConditionText != null)
        {
            catDetailConditionText.text = BuildCatUnlockConditionText(activeDetailUnlockLevel, activeDetailUnlockCoinCost);
        }

        ApplyCatDetailUnlockButtonVisibility();

        catDetailPopupRoot.SetActive(true);
    }

    private void HideCatDetailPopup()
    {
        activeDetailCat = null;
        activeDetailCatIndex = -1;
        activeDetailUnlocked = false;
        activeDetailUnlockLevel = 0;
        activeDetailUnlockCoinCost = 0;

        if (catDetailPopupRoot != null)
        {
            catDetailPopupRoot.SetActive(false);
        }
    }

    private void OnCatDetailUnlockByCoinClicked()
    {
        if (activeDetailCat == null || activeDetailUnlocked || activeDetailUnlockCoinCost <= 0)
        {
            return;
        }

        var currentLevel = GetCurrentProgressLevel();
        if (!IsLevelRequirementSatisfied(activeDetailCat, activeDetailCatIndex, currentLevel))
        {
            ShowCoinUnlockFailedBubble(coinUnlockLevelLockedBubbleText);
            return;
        }

        var cost = Mathf.Max(0, activeDetailUnlockCoinCost);
        if (!TrySpendCoins(cost))
        {
            ShowCoinUnlockFailedBubble(coinUnlockFailedBubbleText);
            return;
        }

        if (!TryUnlockCatByCoinInternal(activeDetailCat, activeDetailCatIndex, currentLevel, out _))
        {
            RefundCoins(cost);
            return;
        }

        HideCatDetailPopup();
        RefreshPopup();
    }

    private string BuildCatUnlockConditionText(int unlockLevel, int unlockCoinCost)
    {
        var parts = new List<string>(2);
        if (unlockCoinCost > 0)
        {
            var coinTemplate = string.IsNullOrWhiteSpace(unlockConditionCoinTemplate)
                ? "{0}金币"
                : unlockConditionCoinTemplate;
            parts.Add(string.Format(coinTemplate, Mathf.Max(0, unlockCoinCost)));
        }

        if (unlockLevel > 0)
        {
            var levelTemplate = string.IsNullOrWhiteSpace(unlockConditionLevelTemplate)
                ? "通过第{0}关"
                : unlockConditionLevelTemplate;
            parts.Add(string.Format(levelTemplate, Mathf.Max(1, unlockLevel)));
        }

        var body = parts.Count > 0
            ? string.Join(string.IsNullOrWhiteSpace(unlockConditionSeparator) ? "、" : unlockConditionSeparator, parts)
            : (string.IsNullOrWhiteSpace(unlockConditionNoneText) ? "未配置解锁条件" : unlockConditionNoneText);
        var prefix = string.IsNullOrWhiteSpace(unlockConditionPrefix) ? string.Empty : unlockConditionPrefix;
        return prefix + body;
    }

    private void ApplyCatDetailUnlockButtonVisibility()
    {
        if (catDetailUnlockButton == null)
        {
            return;
        }

        var visible = !activeDetailUnlocked && activeDetailUnlockCoinCost > 0;
        catDetailUnlockButton.gameObject.SetActive(visible);
        if (visible)
        {
            ApplyButtonLabel(catDetailUnlockButton, catDetailUnlockButtonText);
        }
    }

    private string GetEconomyCoinStorageKey()
    {
        return string.IsNullOrWhiteSpace(economyCoinSaveKey) ? "ShelfSpawn.Economy.Coin" : economyCoinSaveKey;
    }

    private bool TrySpendCoins(int amount)
    {
        var cost = Mathf.Max(0, amount);
        if (cost <= 0)
        {
            return true;
        }

        if (TryResolveShelfSpawnManager(out var manager) && manager != null)
        {
            return manager.TrySpendCoins(cost);
        }

        var currentCoin = Mathf.Max(0, storageService != null ? storageService.GetInt(GetEconomyCoinStorageKey(), 0) : 0);
        if (currentCoin < cost)
        {
            return false;
        }

        storageService.SetInt(GetEconomyCoinStorageKey(), Mathf.Max(0, currentCoin - cost));
        storageService.Save();
        return true;
    }

    private void RefundCoins(int amount)
    {
        var value = Mathf.Max(0, amount);
        if (value <= 0)
        {
            return;
        }

        if (TryResolveShelfSpawnManager(out var manager) && manager != null)
        {
            manager.AddCoins(value);
            return;
        }

        var currentCoin = Mathf.Max(0, storageService != null ? storageService.GetInt(GetEconomyCoinStorageKey(), 0) : 0);
        storageService.SetInt(GetEconomyCoinStorageKey(), Mathf.Max(0, currentCoin + value));
        storageService.Save();
    }

    private bool TryResolveShelfSpawnManager(out ShelfSpawnManager manager)
    {
        manager = shelfSpawnManager;
        if (manager != null)
        {
            return true;
        }

        manager = FindObjectOfType<ShelfSpawnManager>(true);
        shelfSpawnManager = manager;
        return manager != null;
    }

    private string GetLevelProgressStorageKey()
    {
        return string.IsNullOrWhiteSpace(levelProgressSaveKey) ? "ShelfSpawn.Progress.CurrentLevelIndex" : levelProgressSaveKey;
    }

    private int GetCurrentProgressLevel()
    {
        return Mathf.Max(1, storageService != null ? storageService.GetInt(GetLevelProgressStorageKey(), 1) : 1);
    }

    private bool TryUnlockCatByCoinInternal(CatUnlockConfig targetCat, int targetIndex, int currentLevel, out int cost)
    {
        cost = 0;
        if (targetCat == null)
        {
            return false;
        }

        var unlockedSet = GetUnlockedCatTokenSet();
        if (targetIndex < 0 || targetIndex >= cats.Count || cats[targetIndex] != targetCat)
        {
            targetIndex = ResolveCatIndex(targetCat);
        }

        if (targetIndex < 0)
        {
            return false;
        }

        if (IsCatUnlocked(targetCat, targetIndex, unlockedSet))
        {
            return false;
        }

        if (!IsLevelRequirementSatisfied(targetCat, targetIndex, currentLevel))
        {
            return false;
        }

        cost = ResolveDisplayUnlockCoinCost(targetCat);
        if (cost <= 0)
        {
            return false;
        }

        unlockedSet.Add(BuildCatUnlockToken(targetCat, targetIndex));
        SaveUnlockedCatTokenSet(unlockedSet);
        return true;
    }

    private int ResolveCatIndex(CatUnlockConfig targetCat)
    {
        if (cats == null || targetCat == null)
        {
            return -1;
        }

        for (var i = 0; i < cats.Count; i++)
        {
            if (ReferenceEquals(cats[i], targetCat))
            {
                return i;
            }
        }

        for (var i = 0; i < cats.Count; i++)
        {
            var cat = cats[i];
            if (cat == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(cat.catId)
                && !string.IsNullOrWhiteSpace(targetCat.catId)
                && string.Equals(cat.catId, targetCat.catId, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private void ShowCoinUnlockFailedBubble(string message)
    {
        EnsureCoinUnlockFailedBubble();
        if (coinUnlockFailedBubbleRoot == null)
        {
            return;
        }

        if (coinUnlockFailedBubbleRoutine != null)
        {
            StopCoroutine(coinUnlockFailedBubbleRoutine);
            coinUnlockFailedBubbleRoutine = null;
        }

        if (coinUnlockFailedBubbleTextRef != null)
        {
            coinUnlockFailedBubbleTextRef.text = string.IsNullOrWhiteSpace(message)
                ? "解锁失败"
                : message;
        }

        coinUnlockFailedBubbleRoot.gameObject.SetActive(true);
        coinUnlockFailedBubbleRoutine = StartCoroutine(HideCoinUnlockFailedBubbleRoutine(Mathf.Max(0.1f, coinUnlockFailedBubbleDuration)));
    }

    private IEnumerator HideCoinUnlockFailedBubbleRoutine(float duration)
    {
        yield return new WaitForSecondsRealtime(duration);
        if (coinUnlockFailedBubbleRoot != null)
        {
            coinUnlockFailedBubbleRoot.gameObject.SetActive(false);
        }

        coinUnlockFailedBubbleRoutine = null;
    }

    private void EnsureCoinUnlockFailedBubble()
    {
        if (coinUnlockFailedBubbleRoot == null)
        {
            coinUnlockFailedBubbleRoot = coinUnlockFailedBubbleRootRef;
        }

        if (coinUnlockFailedBubbleTextRef == null)
        {
            coinUnlockFailedBubbleTextRef = coinUnlockFailedBubbleTextRefConfig;
        }

        if (coinUnlockFailedBubbleRoot != null && coinUnlockFailedBubbleTextRef == null)
        {
            coinUnlockFailedBubbleTextRef = coinUnlockFailedBubbleRoot.GetComponentInChildren<TMP_Text>(true);
        }

        if (coinUnlockFailedBubbleRoot != null)
        {
            return;
        }
    }

    private Canvas ResolveUiCanvas()
    {
        if (catDetailPopupRoot != null)
        {
            var canvas = catDetailPopupRoot.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                return canvas;
            }
        }

        if (popupRoot != null)
        {
            var canvas = popupRoot.GetComponentInParent<Canvas>(true);
            if (canvas != null)
            {
                return canvas;
            }
        }

        return FindObjectOfType<Canvas>(true);
    }

    private static void ApplyButtonLabel(Button button, string textValue)
    {
        if (button == null)
        {
            return;
        }

        var tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            tmp.text = textValue;
        }
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
            texts[0].text = string.IsNullOrWhiteSpace(catName) ? "未命名小猫" : catName;
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
