using UnityEngine;
using TMPro;

public class PlayerListItem : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI playerNameText;
    
    private string playerName;
    
    /// <summary>
    /// Sets the player name to be displayed
    /// </summary>
    /// <param name="name">The player's name</param>
    public void SetPlayerName(string name)
    {
        playerName = name;
        UpdateDisplay();
    }
    
    /// <summary>
    /// Updates the text display
    /// </summary>
    private void UpdateDisplay()
    {
        if (playerNameText != null)
        {
            playerNameText.text = playerName;
        }
        else
        {
            Debug.LogError("PlayerListItem: PlayerNameText is not assigned!");
        }
    }
    
    /// <summary>
    /// Gets the current player name
    /// </summary>
    public string GetPlayerName()
    {
        return playerName;
    }
}
