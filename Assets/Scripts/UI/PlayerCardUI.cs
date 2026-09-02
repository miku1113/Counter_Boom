using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace CounterBoom.UI
{
    /// <summary>
    /// Attach this script to your Player Card Prefab to directly assign profileImage, playerNameText, inviteButton, and friendIcon in the Inspector.
    /// </summary>
    public class PlayerCardUI : MonoBehaviour
    {
        [Header("Player Card UI References")]
        public Image           profileImage;
        public TextMeshProUGUI playerNameText;
        public Button          inviteButton;
        public GameObject      friendIconObject;
        public GameObject      hostIndicator;

        public void SetupCard(string playerName, bool isHost, bool isFriend, Sprite avatar = null, System.Action onInviteClicked = null)
        {
            if (playerNameText != null)
            {
                playerNameText.text = playerName;
            }

            if (profileImage != null && avatar != null)
            {
                profileImage.sprite = avatar;
                profileImage.preserveAspect = true;
                profileImage.color = Color.white;
            }

            if (hostIndicator != null)
            {
                hostIndicator.SetActive(isHost);
            }

            if (isFriend)
            {
                if (inviteButton != null) inviteButton.gameObject.SetActive(false);
                if (friendIconObject != null) friendIconObject.SetActive(true);
            }
            else
            {
                if (friendIconObject != null) friendIconObject.SetActive(false);
                if (inviteButton != null)
                {
                    inviteButton.gameObject.SetActive(true);
                    inviteButton.onClick.RemoveAllListeners();
                    inviteButton.onClick.AddListener(() =>
                    {
                        onInviteClicked?.Invoke();
                        inviteButton.gameObject.SetActive(false);
                        if (friendIconObject != null) friendIconObject.SetActive(true);
                    });
                }
            }
        }
    }
}
