using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class BagUI : MonoBehaviour
{
    public static BagUI Instance;

    [Header("UI Panels")]
    public RectTransform sideWindow;
    public Transform     itemContainer;
    public TextMeshProUGUI weightText;
    public Button        closeButton;

    [Header("Prefabs")]
    public GameObject itemSlotPrefab;

    [Header("Icons (Fallback)")]
    public Sprite weaponIcon;
    public Sprite ammoIcon;
    public Sprite grenadeIcon;
    public Sprite medikitIcon;
    public Sprite proteinShakeIcon;

    private void Awake()
    {
        Instance = this;
        EnsureBagStructure();
        if (sideWindow != null) sideWindow.gameObject.SetActive(false);
    }

    private void Start()
    {
        EnsureBagStructure();

        if (BagManager.Instance != null)
            BagManager.Instance.OnBagUpdated += RefreshUI;

        if (WeaponController.Instance != null)
            WeaponController.Instance.OnWeaponSlotUpdated += _ => RefreshUIIfOpen();

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ToggleBag);
            closeButton.onClick.AddListener(ToggleBag);
        }
    }

    private void Update()
    {
        // Keyboard shortcuts to toggle bag on PC / testing (B, Tab, I)
        if (Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
        {
            ToggleBag();
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;

        if (BagManager.Instance != null)
            BagManager.Instance.OnBagUpdated -= RefreshUI;

        if (WeaponController.Instance != null)
            WeaponController.Instance.OnWeaponSlotUpdated -= _ => RefreshUIIfOpen();
    }

    public void ToggleBag()
    {
        EnsureBagStructure();

        if (sideWindow == null)
        {
            Debug.LogError("[BagUI] ❌ Cannot ToggleBag because sideWindow is null!");
            return;
        }

        bool newState = !sideWindow.gameObject.activeSelf;
        sideWindow.gameObject.SetActive(newState);
        Debug.Log($"[BagUI] 🎒 ToggleBag called! Backpack is now: {(newState ? "OPEN" : "CLOSED")}");

        if (newState)
        {
            sideWindow.SetAsLastSibling();
            RefreshUI();
        }
    }

    private void RefreshUIIfOpen()
    {
        if (sideWindow != null && sideWindow.gameObject.activeSelf)
            RefreshUI();
    }

    public void EnsureBagStructure()
    {
        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (canvas == null) canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            Debug.LogError("[BagUI] ❌ No Canvas found in scene!");
            return;
        }

        if (transform.parent != canvas.transform && transform.parent == null)
        {
            transform.SetParent(canvas.transform, false);
        }

        if (sideWindow == null)
        {
            // Check if existing SideWindow exists in children
            Transform existingWin = canvas.transform.Find("BagSideWindow") ?? canvas.transform.Find("SideWindow") ?? canvas.transform.Find("BagPanel");
            if (existingWin != null)
            {
                sideWindow = existingWin.GetComponent<RectTransform>();
            }
            else
            {
                // Auto-create modern, sleek Backpack Side Window
                GameObject winGO = new GameObject("BagSideWindow", typeof(RectTransform), typeof(Image));
                winGO.transform.SetParent(canvas.transform, false);

                sideWindow = winGO.GetComponent<RectTransform>();
                sideWindow.anchorMin = new Vector2(0f, 0.5f);
                sideWindow.anchorMax = new Vector2(0f, 0.5f);
                sideWindow.pivot = new Vector2(0f, 0.5f);
                sideWindow.sizeDelta = new Vector2(360f, 480f);
                sideWindow.anchoredPosition = new Vector2(25f, 0f);

                Image bgImg = winGO.GetComponent<Image>();
                bgImg.color = new Color(0.08f, 0.1f, 0.15f, 0.96f);

                Outline winOutline = winGO.AddComponent<Outline>();
                winOutline.effectColor = new Color(0.2f, 0.55f, 0.85f, 0.7f);
                winOutline.effectDistance = new Vector2(2f, -2f);

                // Top Header Bar
                GameObject headerGO = new GameObject("HeaderBar", typeof(RectTransform), typeof(Image));
                headerGO.transform.SetParent(winGO.transform, false);
                RectTransform headRt = headerGO.GetComponent<RectTransform>();
                headRt.anchorMin = new Vector2(0f, 1f); headRt.anchorMax = new Vector2(1f, 1f);
                headRt.pivot = new Vector2(0.5f, 1f);
                headRt.sizeDelta = new Vector2(0f, 48f);
                headRt.anchoredPosition = Vector2.zero;
                headerGO.GetComponent<Image>().color = new Color(0.12f, 0.16f, 0.24f, 1f);

                // Title Text
                GameObject titleGO = new GameObject("TitleText", typeof(RectTransform), typeof(TextMeshProUGUI));
                titleGO.transform.SetParent(headerGO.transform, false);
                RectTransform titleRt = titleGO.GetComponent<RectTransform>();
                titleRt.anchorMin = new Vector2(0f, 0f); titleRt.anchorMax = new Vector2(1f, 1f);
                titleRt.offsetMin = new Vector2(15f, 0f); titleRt.offsetMax = new Vector2(-50f, 0f);

                TextMeshProUGUI titleTmp = titleGO.GetComponent<TextMeshProUGUI>();
                titleTmp.text = "🎒 INVENTORY";
                titleTmp.fontSize = 20;
                titleTmp.fontStyle = FontStyles.Bold;
                titleTmp.alignment = TextAlignmentOptions.MidlineLeft;
                titleTmp.color = new Color(1f, 0.85f, 0.2f, 1f);

                // Close Button
                GameObject closeBtnGO = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
                closeBtnGO.transform.SetParent(headerGO.transform, false);
                RectTransform closeRt = closeBtnGO.GetComponent<RectTransform>();
                closeRt.anchorMin = new Vector2(1f, 0.5f); closeRt.anchorMax = new Vector2(1f, 0.5f);
                closeRt.pivot = new Vector2(1f, 0.5f);
                closeRt.sizeDelta = new Vector2(36f, 36f);
                closeRt.anchoredPosition = new Vector2(-8f, 0f);

                closeBtnGO.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 0.9f);
                closeButton = closeBtnGO.GetComponent<Button>();

                GameObject closeTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
                closeTxtGO.transform.SetParent(closeBtnGO.transform, false);
                RectTransform cTxtRt = closeTxtGO.GetComponent<RectTransform>();
                cTxtRt.anchorMin = Vector2.zero; cTxtRt.anchorMax = Vector2.one; cTxtRt.sizeDelta = Vector2.zero;
                TextMeshProUGUI cTmp = closeTxtGO.GetComponent<TextMeshProUGUI>();
                cTmp.text = "✖";
                cTmp.fontSize = 18;
                cTmp.alignment = TextAlignmentOptions.Center;
                cTmp.color = Color.white;

                closeButton.onClick.AddListener(ToggleBag);

                // Weight Subheader
                GameObject weightGO = new GameObject("WeightText", typeof(RectTransform), typeof(TextMeshProUGUI));
                weightGO.transform.SetParent(winGO.transform, false);
                RectTransform weightRt = weightGO.GetComponent<RectTransform>();
                weightRt.anchorMin = new Vector2(0f, 1f); weightRt.anchorMax = new Vector2(1f, 1f);
                weightRt.pivot = new Vector2(0.5f, 1f);
                weightRt.sizeDelta = new Vector2(0f, 26f);
                weightRt.anchoredPosition = new Vector2(0f, -50f);

                weightText = weightGO.GetComponent<TextMeshProUGUI>();
                weightText.text = "Weight: 0/100 kg";
                weightText.fontSize = 13;
                weightText.fontStyle = FontStyles.Bold;
                weightText.alignment = TextAlignmentOptions.Center;
                weightText.color = new Color(0.4f, 0.9f, 0.6f, 1f);

                // Scroll Area
                GameObject scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
                scrollGO.transform.SetParent(winGO.transform, false);
                RectTransform sRt = scrollGO.GetComponent<RectTransform>();
                sRt.anchorMin = Vector2.zero; sRt.anchorMax = Vector2.one;
                sRt.offsetMin = new Vector2(10f, 12f); sRt.offsetMax = new Vector2(-10f, -80f);

                scrollGO.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.08f, 0.6f);
                ScrollRect sr = scrollGO.GetComponent<ScrollRect>();
                sr.horizontal = false;
                sr.vertical = true;

                // Viewport
                GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
                viewportGO.transform.SetParent(scrollGO.transform, false);
                RectTransform vRt = viewportGO.GetComponent<RectTransform>();
                vRt.anchorMin = Vector2.zero; vRt.anchorMax = Vector2.one; vRt.sizeDelta = Vector2.zero;
                viewportGO.GetComponent<Image>().color = Color.white;
                viewportGO.GetComponent<Mask>().showMaskGraphic = false;

                // Content Container
                GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
                contentGO.transform.SetParent(viewportGO.transform, false);
                RectTransform cRt = contentGO.GetComponent<RectTransform>();
                cRt.anchorMin = new Vector2(0f, 1f); cRt.anchorMax = new Vector2(1f, 1f);
                cRt.pivot = new Vector2(0.5f, 1f);
                cRt.sizeDelta = new Vector2(0f, 0f);

                VerticalLayoutGroup vlg = contentGO.GetComponent<VerticalLayoutGroup>();
                vlg.spacing = 8f;
                vlg.padding = new RectOffset(6, 6, 8, 8);
                vlg.childControlWidth = true;
                vlg.childControlHeight = false;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;

                ContentSizeFitter csf = contentGO.GetComponent<ContentSizeFitter>();
                csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

                sr.viewport = vRt;
                sr.content = cRt;
                itemContainer = contentGO.transform;
            }
        }

        if (itemContainer == null && sideWindow != null)
        {
            var content = sideWindow.GetComponentInChildren<VerticalLayoutGroup>(true);
            if (content != null) itemContainer = content.transform;
            else itemContainer = sideWindow.transform;
        }

        if (weightText == null && sideWindow != null)
        {
            foreach (var t in sideWindow.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (t.gameObject.name.ToLower().Contains("weight"))
                {
                    weightText = t;
                    break;
                }
            }
        }

        if (closeButton == null && sideWindow != null)
        {
            foreach (var b in sideWindow.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name.ToLower().Contains("close"))
                {
                    closeButton = b;
                    closeButton.onClick.RemoveListener(ToggleBag);
                    closeButton.onClick.AddListener(ToggleBag);
                    break;
                }
            }
        }
    }

    public void RefreshUI()
    {
        EnsureBagStructure();

        if (itemContainer == null)
        {
            Debug.LogError("[BagUI] ❌ itemContainer is not assigned! Cannot render bag items.");
            return;
        }

        // Clear existing slots
        foreach (Transform child in itemContainer)
            Destroy(child.gameObject);

        // Auto-discover BagManager if not yet initialized
        if (BagManager.Instance == null)
        {
            if (PlayerController.LocalPlayer != null)
            {
                BagManager.Instance = PlayerController.LocalPlayer.GetComponent<BagManager>();
            }
            if (BagManager.Instance == null)
            {
                var allBags = FindObjectsOfType<BagManager>();
                foreach (var b in allBags)
                {
                    if (b.gameObject.CompareTag("Player") || !b.gameObject.name.ToLower().Contains("bot"))
                    {
                        BagManager.Instance = b;
                        break;
                    }
                }
            }
        }

        int totalSlots = 0;

        if (BagManager.Instance != null)
        {
            // ── 1. Equipped Weapons ────────────────────────────────────────────────
            for (int i = 0; i < 2; i++)
            {
                HandheldWeapon w = BagManager.Instance.GetWeaponInSlot(i);
                if (w == null) continue;

                InventoryItemData data = w.itemData;
                if (data == null)
                {
                    data          = ScriptableObject.CreateInstance<InventoryItemData>();
                    data.itemName = w.weaponName;
                    data.itemType = ItemType.Weapon;
                    data.icon     = weaponIcon;
                }

                CreateSlot(data, 1, i);
                totalSlots++;
            }

            // ── 2. Ammo ────────────────────────────────────────────────────────────
            foreach (var pair in BagManager.Instance.ammoInventory)
            {
                if (pair.Value <= 0) continue;

                InventoryItemData data = BagManager.Instance.GetItemData(pair.Key);
                if (data == null)
                {
                    data          = ScriptableObject.CreateInstance<InventoryItemData>();
                    data.itemName = pair.Key.ToString() + " Ammo";
                    data.itemType = ItemType.Ammo;
                    data.ammoType = pair.Key;
                    data.icon     = ammoIcon;
                }

                CreateSlot(data, pair.Value, -1);
                totalSlots++;
            }

            // ── 3. Grenades ────────────────────────────────────────────────────────
            foreach (GrenadeType gType in System.Enum.GetValues(typeof(GrenadeType)))
            {
                if (gType == GrenadeType.None) continue;
                int count = BagManager.Instance.GetGrenadeCount(gType);
                if (count > 0)
                {
                    InventoryItemData data = BagManager.Instance.allItemData?.Find(x => x.itemType == ItemType.Grenade && x.grenadeType == gType);
                    if (data == null)
                    {
                        data          = ScriptableObject.CreateInstance<InventoryItemData>();
                        data.itemName = gType.ToString() + " Grenade";
                        data.itemType = ItemType.Grenade;
                        data.grenadeType = gType;
                        data.icon     = grenadeIcon;
                    }
                    CreateSlot(data, count, -1);
                    totalSlots++;
                }
            }

            // ── 4. Medikit ─────────────────────────────────────────────────────────
            if (BagManager.Instance.medikitCount > 0)
            {
                InventoryItemData data = BagManager.Instance.GetMedikitData();
                if (data == null)
                {
                    data          = ScriptableObject.CreateInstance<InventoryItemData>();
                    data.itemName = "Medikit";
                    data.itemType = ItemType.Medikit;
                    data.icon     = medikitIcon;
                }
                CreateSlot(data, BagManager.Instance.medikitCount, -1);
                totalSlots++;
            }

            // ── 5. Protein Shake ───────────────────────────────────────────────────
            if (BagManager.Instance.proteinShakeCount > 0)
            {
                InventoryItemData data = BagManager.Instance.GetProteinShakeData();
                if (data == null)
                {
                    data          = ScriptableObject.CreateInstance<InventoryItemData>();
                    data.itemName = "Protein Shake";
                    data.itemType = ItemType.ProteinShake;
                    data.icon     = proteinShakeIcon;
                }
                CreateSlot(data, BagManager.Instance.proteinShakeCount, -1);
                totalSlots++;
            }

            // ── 6. Scopes / Attachments ────────────────────────────────────────────
            if (BagManager.Instance.scopeCount > 0)
            {
                InventoryItemData data = ScriptableObject.CreateInstance<InventoryItemData>();
                data.itemName = "Scope";
                data.itemType = ItemType.Scope;
                CreateSlot(data, BagManager.Instance.scopeCount, -1);
                totalSlots++;
            }
        }

        // ── 7. Weight display ──────────────────────────────────────────────────
        if (weightText != null)
        {
            if (BagManager.Instance != null)
                weightText.text = $"Weight: {BagManager.Instance.currentWeight}/{BagManager.Instance.maxWeight} kg";
            else
                weightText.text = "Weight: 0/100 kg";
        }

        if (totalSlots == 0)
        {
            CreateEmptyPlaceholder();
        }
    }

    private void CreateSlot(InventoryItemData data, int count, int weaponSlotIdx)
    {
        if (itemSlotPrefab != null)
        {
            GameObject slotObj = Instantiate(itemSlotPrefab, itemContainer);
            BagItemSlot prefabSlot = slotObj.GetComponent<BagItemSlot>();
            if (prefabSlot != null)
            {
                prefabSlot.weaponSlotIndex = weaponSlotIdx;
                prefabSlot.Setup(data, count);
                return;
            }
        }

        // Dynamic Slot Creation
        GameObject slotGO = new GameObject($"Slot_{data?.itemName}", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(BagItemSlot));
        slotGO.transform.SetParent(itemContainer, false);

        RectTransform sRt = slotGO.GetComponent<RectTransform>();
        sRt.sizeDelta = new Vector2(320f, 54f);

        LayoutElement le = slotGO.GetComponent<LayoutElement>();
        le.preferredHeight = 54f;
        le.minHeight = 54f;

        Image bg = slotGO.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.16f, 0.22f, 0.96f);

        Outline o = slotGO.AddComponent<Outline>();
        o.effectColor = new Color(0.25f, 0.35f, 0.5f, 0.6f);
        o.effectDistance = new Vector2(1f, -1f);

        // Icon Image
        GameObject iconGO = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGO.transform.SetParent(slotGO.transform, false);
        RectTransform iconRt = iconGO.GetComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f); iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = new Vector2(40f, 40f);
        iconRt.anchoredPosition = new Vector2(8f, 0f);
        Image iconImg = iconGO.GetComponent<Image>();
        iconImg.preserveAspect = true;

        // Name Text
        GameObject nameGO = new GameObject("NameText", typeof(RectTransform), typeof(TextMeshProUGUI));
        nameGO.transform.SetParent(slotGO.transform, false);
        RectTransform nameRt = nameGO.GetComponent<RectTransform>();
        nameRt.anchorMin = new Vector2(0f, 0.5f); nameRt.anchorMax = new Vector2(0f, 0.5f);
        nameRt.pivot = new Vector2(0f, 0.5f);
        nameRt.sizeDelta = new Vector2(140f, 24f);
        nameRt.anchoredPosition = new Vector2(56f, 6f);
        TextMeshProUGUI nameTmp = nameGO.GetComponent<TextMeshProUGUI>();
        nameTmp.fontSize = 14;
        nameTmp.fontStyle = FontStyles.Bold;
        nameTmp.color = Color.white;

        // Amount Text
        GameObject amountGO = new GameObject("AmountText", typeof(RectTransform), typeof(TextMeshProUGUI));
        amountGO.transform.SetParent(slotGO.transform, false);
        RectTransform amountRt = amountGO.GetComponent<RectTransform>();
        amountRt.anchorMin = new Vector2(0f, 0.5f); amountRt.anchorMax = new Vector2(0f, 0.5f);
        amountRt.pivot = new Vector2(0f, 0.5f);
        amountRt.sizeDelta = new Vector2(140f, 20f);
        amountRt.anchoredPosition = new Vector2(56f, -10f);
        TextMeshProUGUI amountTmp = amountGO.GetComponent<TextMeshProUGUI>();
        amountTmp.fontSize = 11;
        amountTmp.color = new Color(0.7f, 0.85f, 1f, 1f);

        // Action / Use Button
        GameObject useBtnGO = new GameObject("UseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        useBtnGO.transform.SetParent(slotGO.transform, false);
        RectTransform useRt = useBtnGO.GetComponent<RectTransform>();
        useRt.anchorMin = new Vector2(1f, 0.5f); useRt.anchorMax = new Vector2(1f, 0.5f);
        useRt.pivot = new Vector2(1f, 0.5f);
        useRt.sizeDelta = new Vector2(56f, 32f);
        useRt.anchoredPosition = new Vector2(-64f, 0f);
        useBtnGO.GetComponent<Image>().color = new Color(0.18f, 0.65f, 0.35f, 1f);

        GameObject useTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        useTxtGO.transform.SetParent(useBtnGO.transform, false);
        RectTransform uTextRt = useTxtGO.GetComponent<RectTransform>();
        uTextRt.anchorMin = Vector2.zero; uTextRt.anchorMax = Vector2.one; uTextRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI useTmp = useTxtGO.GetComponent<TextMeshProUGUI>();
        useTmp.text = "USE";
        useTmp.fontSize = 12;
        useTmp.fontStyle = FontStyles.Bold;
        useTmp.alignment = TextAlignmentOptions.Center;
        useTmp.color = Color.white;

        // Drop Button
        GameObject dropBtnGO = new GameObject("DropButton", typeof(RectTransform), typeof(Image), typeof(Button));
        dropBtnGO.transform.SetParent(slotGO.transform, false);
        RectTransform dropRt = dropBtnGO.GetComponent<RectTransform>();
        dropRt.anchorMin = new Vector2(1f, 0.5f); dropRt.anchorMax = new Vector2(1f, 0.5f);
        dropRt.pivot = new Vector2(1f, 0.5f);
        dropRt.sizeDelta = new Vector2(52f, 32f);
        dropRt.anchoredPosition = new Vector2(-6f, 0f);
        dropBtnGO.GetComponent<Image>().color = new Color(0.72f, 0.22f, 0.22f, 1f);

        GameObject dropTxtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        dropTxtGO.transform.SetParent(dropBtnGO.transform, false);
        RectTransform dTextRt = dropTxtGO.GetComponent<RectTransform>();
        dTextRt.anchorMin = Vector2.zero; dTextRt.anchorMax = Vector2.one; dTextRt.sizeDelta = Vector2.zero;
        TextMeshProUGUI dropTmp = dropTxtGO.GetComponent<TextMeshProUGUI>();
        dropTmp.text = "DROP";
        dropTmp.fontSize = 12;
        dropTmp.fontStyle = FontStyles.Bold;
        dropTmp.alignment = TextAlignmentOptions.Center;
        dropTmp.color = Color.white;

        BagItemSlot slot = slotGO.GetComponent<BagItemSlot>();
        slot.iconImage = iconImg;
        slot.itemNameText = nameTmp;
        slot.amountText = amountTmp;
        slot.useButton = useBtnGO.GetComponent<Button>();
        slot.dropButton = dropBtnGO.GetComponent<Button>();
        slot.weaponSlotIndex = weaponSlotIdx;
        slot.Setup(data, count);
    }

    private void CreateEmptyPlaceholder()
    {
        GameObject emptyGO = new GameObject("EmptyPlaceholder", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        emptyGO.transform.SetParent(itemContainer, false);
        
        RectTransform eRt = emptyGO.GetComponent<RectTransform>();
        eRt.sizeDelta = new Vector2(320f, 150f);

        LayoutElement le = emptyGO.GetComponent<LayoutElement>();
        le.preferredHeight = 150f;
        le.minHeight = 150f;

        Image bg = emptyGO.GetComponent<Image>();
        bg.color = new Color(0.12f, 0.16f, 0.22f, 0.5f);

        GameObject txtGO = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(emptyGO.transform, false);
        RectTransform tRt = txtGO.GetComponent<RectTransform>();
        tRt.anchorMin = Vector2.zero; tRt.anchorMax = Vector2.one; tRt.sizeDelta = Vector2.zero;

        TextMeshProUGUI eTmp = txtGO.GetComponent<TextMeshProUGUI>();
        eTmp.text = "<size=30>🎒</size>\n<size=16><b>Backpack is Empty</b></size>\n<size=12><color=#88aacc>Scavenge rooms & floors to pick up\nweapons, ammo & supplies!</color></size>";
        eTmp.alignment = TextAlignmentOptions.Center;
        eTmp.color = Color.white;
    }
}
