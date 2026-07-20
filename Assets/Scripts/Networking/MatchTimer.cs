using UnityEngine;
using Unity.Netcode;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;

public class MatchTimer : NetworkBehaviour
{
    public static MatchTimer Instance { get; private set; }

    [SerializeField] private float countdownDuration = 30f;
    
    // Synced countdown time remaining
    private readonly NetworkVariable<float> timeRemaining = new NetworkVariable<float>(
        30f, 
        NetworkVariableReadPermission.Everyone, 
        NetworkVariableWritePermission.Server
    );

    private bool lobbyLocked = false;

    private void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        if (IsServer)
        {
            timeRemaining.Value = countdownDuration;
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            if (timeRemaining.Value > 0f)
            {
                timeRemaining.Value -= Time.deltaTime;
                if (timeRemaining.Value <= 0f)
                {
                    timeRemaining.Value = 0f;
                    LockLobby();
                }
            }
        }

        // Update the HUD display
        if (HUDManager.Instance != null && RelayNetworkManager.Instance != null)
        {
            string code = RelayNetworkManager.Instance.CurrentJoinCode;
            HUDManager.Instance.UpdateRoomCodeAndTimer(code, timeRemaining.Value);
        }
    }

    private async void LockLobby()
    {
        if (lobbyLocked) return;
        lobbyLocked = true;
        Debug.Log("[MatchTimer] Countdown finished. Locking lobby to new players.");

        if (RelayNetworkManager.Instance != null && !string.IsNullOrEmpty(RelayNetworkManager.Instance.CurrentLobbyId))
        {
            string lobbyId = RelayNetworkManager.Instance.CurrentLobbyId;
            try
            {
                // Update lobby to lock it and mark match as started
                var options = new UpdateLobbyOptions
                {
                    IsLocked = true,
                    Data = new System.Collections.Generic.Dictionary<string, DataObject>
                    {
                        {
                            "MatchStarted", new DataObject(
                                visibility: DataObject.VisibilityOptions.Public,
                                value: "true"
                            )
                        }
                    }
                };
                await LobbyService.Instance.UpdateLobbyAsync(lobbyId, options);
                Debug.Log("[MatchTimer] Lobby successfully locked and MatchStarted set to true.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[MatchTimer] Failed to lock lobby: {e.Message}");
            }
        }
    }
}
