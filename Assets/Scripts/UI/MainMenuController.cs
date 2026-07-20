using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class MainMenuController : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private GameObject mainPanel;
    [SerializeField] private GameObject nameEntryPanel;
    [SerializeField] private GameObject playPanel;
    [SerializeField] private GameObject cabinetPanel;
    [SerializeField] private GameObject shopPanel;
    [SerializeField] private GameObject settingsPanel;

    [Header("Navigation Buttons")]
    [SerializeField] private Button navPlayButton;
    [SerializeField] private Button navCabinetButton;
    [SerializeField] private Button navShopButton;
    [SerializeField] private Button navSettingsButton;
    [SerializeField] private Button navExitButton;

    [Header("Name Entry Panel")]
    [SerializeField] private TMP_InputField nameInputField;
    [SerializeField] private Button nameSubmitButton;

    [Header("Cabinet / Customization")]
    [SerializeField] private CharacterAssembler previewAssembler;
    [SerializeField] private Button cabinetPrevButton;
    [SerializeField] private Button cabinetNextButton;
    [SerializeField] private Button cabinetEquipButton;
    [SerializeField] private TextMeshProUGUI cabinetSkinNameText;
    [SerializeField] private TextMeshProUGUI cabinetSkinStatusText;
    [SerializeField] private Button cabinetBackButton;

    [Header("Shop")]
    [SerializeField] private Button shopPrevButton;
    [SerializeField] private Button shopNextButton;
    [SerializeField] private Button shopBuyButton;
    [SerializeField] private TextMeshProUGUI shopSkinNameText;
    [SerializeField] private TextMeshProUGUI shopSkinPriceText;
    [SerializeField] private TextMeshProUGUI shopCoinsText;
    [SerializeField] private Button shopBackButton;

    [Header("Lobby Play Panel (Lobby + Relay)")]
    [SerializeField] private Button hostButton; // Repurposed as "Quick Play" to preserve editor serialization
    [SerializeField] private Button joinButton; // Repurposed as "Manual Join" to preserve editor serialization
    [SerializeField] private TMP_InputField joinCodeInputField; // Repurposed to preserve editor serialization
    [SerializeField] private TextMeshProUGUI generatedCodeText; // Displays code if host
    [SerializeField] private TextMeshProUGUI playStatusText;
    [SerializeField] private Button playBackButton;

    // State Variables
    private CharacterSkinData[] skins;
    private int cabinetSelectedIndex = 0;
    private int shopSelectedIndex = 0;
    private int coins = 1000;

    private void Start()
    {
        // 1. Initialize Player Coins
        coins = PlayerPrefs.GetInt("Coins", 1000);
        PlayerPrefs.SetInt("Coins", coins);

        // 2. Fetch Available Skins from Preview Assembler
        if (previewAssembler != null)
        {
            skins = previewAssembler.GetAvailableSkins();
            // Ensure index 0 (default skin) is always unlocked
            PlayerPrefs.SetInt("Skin_Unlocked_0", 1);
        }
        else
        {
            Debug.LogError("[MainMenu] Preview Assembler is not assigned in the Inspector!");
        }

        // 3. Register Navigation Listeners
        if (navPlayButton != null) navPlayButton.onClick.AddListener(() => ShowPanel(playPanel));
        if (navCabinetButton != null) navCabinetButton.onClick.AddListener(() => ShowPanel(cabinetPanel));
        if (navShopButton != null) navShopButton.onClick.AddListener(() => ShowPanel(shopPanel));
        if (navSettingsButton != null) navSettingsButton.onClick.AddListener(() => ShowPanel(settingsPanel));
        if (navExitButton != null) navExitButton.onClick.AddListener(ExitGame);

        // Back Buttons
        if (playBackButton != null) playBackButton.onClick.AddListener(() => ShowPanel(mainPanel));
        if (cabinetBackButton != null) cabinetBackButton.onClick.AddListener(() => ShowPanel(mainPanel));
        if (shopBackButton != null) shopBackButton.onClick.AddListener(() => ShowPanel(mainPanel));

        // 4. Register Panel Specific Listeners
        if (nameSubmitButton != null) nameSubmitButton.onClick.AddListener(SubmitName);
        
        if (cabinetPrevButton != null) cabinetPrevButton.onClick.AddListener(CycleCabinetPrev);
        if (cabinetNextButton != null) cabinetNextButton.onClick.AddListener(CycleCabinetNext);
        if (cabinetEquipButton != null) cabinetEquipButton.onClick.AddListener(EquipSelectedSkin);

        if (shopPrevButton != null) shopPrevButton.onClick.AddListener(CycleShopPrev);
        if (shopNextButton != null) shopNextButton.onClick.AddListener(CycleShopNext);
        if (shopBuyButton != null) shopBuyButton.onClick.AddListener(BuySelectedSkin);

        // Quick Play Matchmaking (using hostButton) & Manual Join (using joinButton)
        if (hostButton != null) hostButton.onClick.AddListener(OnQuickPlayClicked);
        if (joinButton != null) joinButton.onClick.AddListener(OnManualJoinClicked);

        // 5. Initial Display Selection
        cabinetSelectedIndex = PlayerPrefs.GetInt("EquippedSkinIndex", 0);
        shopSelectedIndex = 0;

        // 6. Direct to Name Entry or Main Panel based on if player name exists
        string savedName = PlayerPrefs.GetString("PlayerName", "");
        if (string.IsNullOrEmpty(savedName))
        {
            ShowPanel(nameEntryPanel);
        }
        else
        {
            ShowPanel(mainPanel);
        }
    }

    private void ShowPanel(GameObject panelToShow)
    {
        // Hide all views
        if (mainPanel != null) mainPanel.SetActive(false);
        if (nameEntryPanel != null) nameEntryPanel.SetActive(false);
        if (playPanel != null) playPanel.SetActive(false);
        if (cabinetPanel != null) cabinetPanel.SetActive(false);
        if (shopPanel != null) shopPanel.SetActive(false);
        if (settingsPanel != null) settingsPanel.SetActive(false);

        // Enable target view
        if (panelToShow != null)
        {
            panelToShow.SetActive(true);
            
            // View transition updates
            if (panelToShow == cabinetPanel)
            {
                UpdateCabinetUI();
            }
            else if (panelToShow == shopPanel)
            {
                UpdateShopUI();
            }
            else if (panelToShow == playPanel)
            {
                UpdatePlayStatus("Ready to search or host lobby");
                if (generatedCodeText != null) generatedCodeText.text = "JOIN CODE: -";
                SetPlayInputInteractable(true);
            }
        }
    }

    #region Name Entry Logic
    private void SubmitName()
    {
        if (nameInputField != null && !string.IsNullOrEmpty(nameInputField.text.Trim()))
        {
            string cleanName = nameInputField.text.Trim();
            PlayerPrefs.SetString("PlayerName", cleanName);
            Debug.Log($"[MainMenu] Player nickname saved: {cleanName}");
            ShowPanel(mainPanel);
        }
        else
        {
            Debug.LogWarning("[MainMenu] Cannot submit name: Input field is empty.");
        }
    }
    #endregion

    #region Cabinet (Customization) Logic
    private void CycleCabinetPrev()
    {
        if (skins == null || skins.Length == 0) return;
        cabinetSelectedIndex = (cabinetSelectedIndex - 1 + skins.Length) % skins.Length;
        UpdateCabinetUI();
    }

    private void CycleCabinetNext()
    {
        if (skins == null || skins.Length == 0) return;
        cabinetSelectedIndex = (cabinetSelectedIndex + 1) % skins.Length;
        UpdateCabinetUI();
    }

    private void EquipSelectedSkin()
    {
        if (skins == null || skins.Length == 0) return;

        // Check if selected skin is unlocked
        if (IsSkinUnlocked(cabinetSelectedIndex))
        {
            PlayerPrefs.SetInt("EquippedSkinIndex", cabinetSelectedIndex);
            PlayerPrefs.Save();
            Debug.Log($"[MainMenu] Equipped skin index: {cabinetSelectedIndex}");
            UpdateCabinetUI();
        }
    }

    private void UpdateCabinetUI()
    {
        if (skins == null || skins.Length == 0 || previewAssembler == null) return;

        // Apply skin to assembler preview model
        previewAssembler.SetCharacterSkin(skins[cabinetSelectedIndex]);

        // Render details
        if (cabinetSkinNameText != null)
        {
            cabinetSkinNameText.text = skins[cabinetSelectedIndex].skinName;
        }

        bool isUnlocked = IsSkinUnlocked(cabinetSelectedIndex);
        bool isEquipped = PlayerPrefs.GetInt("EquippedSkinIndex", 0) == cabinetSelectedIndex;

        if (cabinetSkinStatusText != null)
        {
            if (isEquipped)
            {
                cabinetSkinStatusText.text = "<color=green>EQUIPPED</color>";
                if (cabinetEquipButton != null) cabinetEquipButton.interactable = false;
            }
            else if (isUnlocked)
            {
                cabinetSkinStatusText.text = "<color=yellow>UNLOCKED</color>";
                if (cabinetEquipButton != null) cabinetEquipButton.interactable = true;
            }
            else
            {
                cabinetSkinStatusText.text = "<color=red>LOCKED (Go to Shop)</color>";
                if (cabinetEquipButton != null) cabinetEquipButton.interactable = false;
            }
        }
    }
    #endregion

    #region Shop Logic
    private void CycleShopPrev()
    {
        if (skins == null || skins.Length == 0) return;
        shopSelectedIndex = (shopSelectedIndex - 1 + skins.Length) % skins.Length;
        UpdateShopUI();
    }

    private void CycleShopNext()
    {
        if (skins == null || skins.Length == 0) return;
        shopSelectedIndex = (shopSelectedIndex + 1) % skins.Length;
        UpdateShopUI();
    }

    private void BuySelectedSkin()
    {
        if (skins == null || skins.Length == 0) return;

        CharacterSkinData targetSkin = skins[shopSelectedIndex];
        if (IsSkinUnlocked(shopSelectedIndex)) return;

        if (coins >= targetSkin.price)
        {
            // Deduct coins and save unlock state
            coins -= targetSkin.price;
            PlayerPrefs.SetInt("Coins", coins);
            PlayerPrefs.SetInt($"Skin_Unlocked_{shopSelectedIndex}", 1);
            PlayerPrefs.Save();

            Debug.Log($"[MainMenu] Purchased skin '{targetSkin.skinName}' for {targetSkin.price} coins.");
            UpdateShopUI();
        }
        else
        {
            Debug.LogWarning("[MainMenu] Not enough coins to purchase skin.");
        }
    }

    private void UpdateShopUI()
    {
        if (skins == null || skins.Length == 0 || previewAssembler == null) return;

        // Apply skin to assembler preview model
        previewAssembler.SetCharacterSkin(skins[shopSelectedIndex]);

        // Render coins balance
        if (shopCoinsText != null)
        {
            shopCoinsText.text = $"COINS: {coins}";
        }

        // Render skin info
        if (shopSkinNameText != null)
        {
            shopSkinNameText.text = skins[shopSelectedIndex].skinName;
        }

        bool isUnlocked = IsSkinUnlocked(shopSelectedIndex);

        if (shopSkinPriceText != null)
        {
            if (isUnlocked)
            {
                shopSkinPriceText.text = "UNLOCKED";
                if (shopBuyButton != null) shopBuyButton.gameObject.SetActive(false);
            }
            else
            {
                shopSkinPriceText.text = $"Price: {skins[shopSelectedIndex].price} Coins";
                if (shopBuyButton != null)
                {
                    shopBuyButton.gameObject.SetActive(true);
                    // Disable if player can't afford
                    shopBuyButton.interactable = coins >= skins[shopSelectedIndex].price;
                }
            }
        }
    }

    private bool IsSkinUnlocked(int index)
    {
        if (index == 0) return true; // Default skin is always unlocked
        return PlayerPrefs.GetInt($"Skin_Unlocked_{index}", 0) == 1;
    }
    #endregion

    #region Automatic Quick Play & Matchmaking Logic
    private async void OnQuickPlayClicked()
    {
        if (RelayNetworkManager.Instance == null)
        {
            UpdatePlayStatus("<color=red>Error: Relay Manager Missing</color>");
            return;
        }

        SetPlayInputInteractable(false);
        UpdatePlayStatus("Searching for active game lobbies...");

        // Attempts to search and quick join, or hosts automatically if none exist
        bool success = await RelayNetworkManager.Instance.QuickPlayMatchmaking();

        if (success)
        {
            UpdatePlayStatus("<color=green>Connecting and spawning player...</color>");
            if (generatedCodeText != null && !string.IsNullOrEmpty(RelayNetworkManager.Instance.CurrentJoinCode))
            {
                generatedCodeText.text = $"JOIN CODE: {RelayNetworkManager.Instance.CurrentJoinCode}";
            }
        }
        else
        {
            UpdatePlayStatus("<color=red>Failed to connect or host match.</color>");
            SetPlayInputInteractable(true);
        }
    }

    private async void OnManualJoinClicked()
    {
        if (RelayNetworkManager.Instance == null)
        {
            UpdatePlayStatus("<color=red>Error: Relay Manager Missing</color>");
            return;
        }

        if (joinCodeInputField == null || string.IsNullOrEmpty(joinCodeInputField.text))
        {
            UpdatePlayStatus("<color=yellow>Enter a room join code</color>");
            return;
        }

        string rawCode = joinCodeInputField.text.Trim().ToUpper();

        SetPlayInputInteractable(false);
        UpdatePlayStatus($"Connecting to room {rawCode}...");

        bool success = await RelayNetworkManager.Instance.StartClientWithRelay(rawCode);

        if (success)
        {
            UpdatePlayStatus("<color=green>Room found! Spawning...</color>");
            if (generatedCodeText != null)
            {
                generatedCodeText.text = $"JOIN CODE: {rawCode}";
            }
        }
        else
        {
            UpdatePlayStatus("<color=red>Room not found.</color>");
            SetPlayInputInteractable(true);
        }
    }

    private void UpdatePlayStatus(string message)
    {
        if (playStatusText != null)
        {
            playStatusText.text = message;
        }
        Debug.Log($"[MainMenuUI] {message}");
    }

    private void SetPlayInputInteractable(bool state)
    {
        if (hostButton != null) hostButton.interactable = state;
        if (joinButton != null) joinButton.interactable = state;
        if (joinCodeInputField != null) joinCodeInputField.interactable = state;
    }
    #endregion

    private void ExitGame()
    {
        Debug.Log("[MainMenu] Exiting Game...");
        Application.Quit();
    }
}
