using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CounterBoom.UI
{
    public class PlayerRowItemUI : MonoBehaviour
    {
        [Header("Profile Info")]
        [SerializeField] private Image avatarImage;
        [SerializeField] private TextMeshProUGUI nameText;
        [SerializeField] private TextMeshProUGUI levelText;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Action Buttons")]
        [SerializeField] private Button addButton;
        [SerializeField] private Button acceptButton;
        [SerializeField] private Button declineButton;
        [SerializeField] private Button inviteButton;
        [SerializeField] private Button removeButton;

        [Header("Status Indicators")]
        [SerializeField] private GameObject friendStatusBadge;
        [SerializeField] private TextMeshProUGUI friendStatusBadgeText;

        public enum RowMode { Search, Requests, Friends }

        private void Awake()
        {
            AutoWireMissingFields();
        }

        private void AutoWireMissingFields()
        {
            Button[] buttons = GetComponentsInChildren<Button>(true);
            foreach (var btn in buttons)
            {
                string n = btn.gameObject.name.ToLower();
                if (addButton == null && (n.Contains("add") || n.Contains("plus"))) addButton = btn;
                else if (acceptButton == null && (n.Contains("accept") || n.Contains("confirm") || n.Contains("yes"))) acceptButton = btn;
                else if (declineButton == null && (n.Contains("decline") || n.Contains("reject") || n.Contains("no") || n.Contains("cancel"))) declineButton = btn;
                else if (inviteButton == null && (n.Contains("invite") || n.Contains("game"))) inviteButton = btn;
                else if (removeButton == null && (n.Contains("remove") || n.Contains("delete"))) removeButton = btn;
            }

            if (avatarImage == null)
            {
                var imgs = GetComponentsInChildren<Image>(true);
                foreach (var img in imgs)
                {
                    string n = img.gameObject.name.ToLower();
                    if (n.Contains("profile") || n.Contains("avatar") || n.Contains("icon"))
                    {
                        avatarImage = img;
                        break;
                    }
                }
                if (avatarImage == null && imgs.Length > 0) avatarImage = imgs[0];
            }

            if (nameText == null)
            {
                var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in tmps)
                {
                    string n = t.gameObject.name.ToLower();
                    if (n.Contains("name") || n.Contains("user") || n.Contains("title"))
                    {
                        nameText = t;
                        break;
                    }
                }
                if (nameText == null && tmps.Length > 0) nameText = tmps[0];
            }

            if (levelText == null)
            {
                var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in tmps)
                {
                    string n = t.gameObject.name.ToLower();
                    if (n.Contains("level") || n.Contains("lvl"))
                    {
                        levelText = t;
                        break;
                    }
                }
            }

            if (statusText == null)
            {
                var tmps = GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in tmps)
                {
                    string n = t.gameObject.name.ToLower();
                    if (n.Contains("id") || n.Contains("status"))
                    {
                        statusText = t;
                        break;
                    }
                }
            }

            if (friendStatusBadge == null)
            {
                var trans = GetComponentsInChildren<Transform>(true);
                foreach (var tr in trans)
                {
                    string n = tr.gameObject.name.ToLower();
                    if (tr.gameObject != gameObject && (n.Contains("friendicon") || n.Contains("badge") || n.Contains("status")))
                    {
                        friendStatusBadge = tr.gameObject;
                        break;
                    }
                }
            }
        }

        public void SetRequestedState()
        {
            if (addButton != null)
            {
                addButton.gameObject.SetActive(true);
                addButton.interactable = false;
                var tmp = addButton.GetComponentInChildren<TextMeshProUGUI>();
                if (tmp != null) tmp.text = "REQUESTED";
            }
            else if (friendStatusBadge != null)
            {
                friendStatusBadge.SetActive(true);
                if (friendStatusBadgeText != null) friendStatusBadgeText.text = "REQUESTED ⏳";
            }
        }

        public void SetupRow(
            string uid,
            string displayName,
            int level,
            Sprite avatarSprite,
            RowMode mode,
            bool isAlreadyFriend,
            bool isRequestPending,
            Action onAddClicked,
            Action onAcceptClicked,
            Action onDeclineClicked,
            Action<Button> onInviteClicked,
            Action onRemoveClicked,
            Action onRowClicked = null,
            string statusSuffix = "",
            string inviteButtonText = "INVITE",
            bool inviteButtonInteractable = true,
            Color? inviteButtonColor = null)
        {
            AutoWireMissingFields();

            Button rootBtn = GetComponent<Button>();
            if (rootBtn == null)
            {
                rootBtn = gameObject.AddComponent<Button>();
            }
            rootBtn.onClick.RemoveAllListeners();
            if (onRowClicked != null)
            {
                rootBtn.onClick.AddListener(() => onRowClicked.Invoke());
            }

            if (nameText != null)
            {
                string formatted = string.IsNullOrEmpty(statusSuffix) ? displayName : $"{displayName} {statusSuffix}";
                nameText.text = formatted;
                var tmps = nameText.GetComponentsInChildren<TextMeshProUGUI>(true);
                foreach (var t in tmps)
                {
                    t.text = formatted;
                }
            }

            if (levelText != null) levelText.text = $"Level {level}";
            if (statusText != null) statusText.text = $"ID: {uid}";

            if (avatarImage != null && avatarSprite != null)
            {
                avatarImage.sprite = avatarSprite;
                avatarImage.enabled = true;
            }

            // Deactivate all action buttons & status indicators first
            if (addButton != null) addButton.gameObject.SetActive(false);
            if (acceptButton != null) acceptButton.gameObject.SetActive(false);
            if (declineButton != null) declineButton.gameObject.SetActive(false);
            if (inviteButton != null) inviteButton.gameObject.SetActive(false);
            if (removeButton != null) removeButton.gameObject.SetActive(false);
            if (friendStatusBadge != null) friendStatusBadge.SetActive(false);

            if (mode == RowMode.Search)
            {
                if (isAlreadyFriend)
                {
                    if (friendStatusBadge != null)
                    {
                        friendStatusBadge.SetActive(true);
                        if (friendStatusBadgeText != null) friendStatusBadgeText.text = "FRIENDS ✔";
                    }
                }
                else if (isRequestPending)
                {
                    SetRequestedState();
                }
                else if (addButton != null)
                {
                    addButton.gameObject.SetActive(true);
                    addButton.interactable = true;
                    var tmp = addButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) tmp.text = "ADD";

                    addButton.onClick.RemoveAllListeners();
                    addButton.onClick.AddListener(() =>
                    {
                        onAddClicked?.Invoke();
                        SetRequestedState();
                    });
                }
            }
            else if (mode == RowMode.Requests)
            {
                if (acceptButton != null)
                {
                    acceptButton.gameObject.SetActive(true);
                    acceptButton.onClick.RemoveAllListeners();
                    if (onAcceptClicked != null) acceptButton.onClick.AddListener(() => onAcceptClicked.Invoke());
                }

                if (declineButton != null)
                {
                    declineButton.gameObject.SetActive(true);
                    declineButton.onClick.RemoveAllListeners();
                    if (onDeclineClicked != null) declineButton.onClick.AddListener(() => onDeclineClicked.Invoke());
                }
            }
            else if (mode == RowMode.Friends)
            {
                if (inviteButton != null)
                {
                    inviteButton.gameObject.SetActive(true);
                    inviteButton.interactable = inviteButtonInteractable;
                    var tmp = inviteButton.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null)
                    {
                        tmp.text = inviteButtonText;
                    }
                    if (inviteButtonColor.HasValue)
                    {
                        var img = inviteButton.GetComponent<Image>();
                        if (img != null) img.color = inviteButtonColor.Value;
                    }

                    inviteButton.onClick.RemoveAllListeners();
                    if (inviteButtonInteractable && onInviteClicked != null)
                    {
                        inviteButton.onClick.AddListener(() => onInviteClicked.Invoke(inviteButton));
                    }
                }

                if (removeButton != null)
                {
                    removeButton.gameObject.SetActive(true);
                    removeButton.onClick.RemoveAllListeners();
                    if (onRemoveClicked != null) removeButton.onClick.AddListener(() => onRemoveClicked.Invoke());
                }
            }
        }
    }
}
