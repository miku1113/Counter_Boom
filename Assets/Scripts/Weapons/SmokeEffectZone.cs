using UnityEngine;
using System.Collections.Generic;

public class SmokeEffectZone : MonoBehaviour
{
    private bool localPlayerInside = false;
    private List<PlayerController> playersInside = new List<PlayerController>();

    private void OnTriggerEnter2D(Collider2D other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            if (pc.IsLocal)
            {
                localPlayerInside = true;
                PlayerController.TriggerEnterSmoke();
                Debug.Log($"[SmokeEffectZone] Local player entered smoke zone.");
            }
            else
            {
                // Hide remote player inside the smoke
                CharacterAssembler ca = pc.GetComponentInChildren<CharacterAssembler>();
                if (ca != null)
                {
                    ca.SetVisibility(0f);
                }
                Debug.Log($"[SmokeEffectZone] Remote player '{pc.name}' entered smoke zone — stealth hidden.");
            }

            if (!playersInside.Contains(pc))
            {
                playersInside.Add(pc);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        PlayerController pc = other.GetComponent<PlayerController>();
        if (pc != null)
        {
            if (pc.IsLocal)
            {
                localPlayerInside = false;
                PlayerController.TriggerExitSmoke();
                Debug.Log($"[SmokeEffectZone] Local player exited smoke zone.");
            }
            else
            {
                // Make remote player visible again
                CharacterAssembler ca = pc.GetComponentInChildren<CharacterAssembler>();
                if (ca != null)
                {
                    ca.SetVisibility(1f);
                }
                Debug.Log($"[SmokeEffectZone] Remote player '{pc.name}' exited smoke zone — stealth revealed.");
            }

            if (playersInside.Contains(pc))
            {
                playersInside.Remove(pc);
            }
        }
    }

    private void OnDestroy()
    {
        // Safe exit trigger if the smoke screen is destroyed/dissipated while local player is still inside
        if (localPlayerInside)
        {
            localPlayerInside = false;
            PlayerController.TriggerExitSmoke();
            Debug.Log($"[SmokeEffectZone] Smoke screen destroyed while local player was inside — exit triggered.");
        }

        // Restore visibility to all players who were inside when the smoke dissipates
        foreach (var pc in playersInside)
        {
            if (pc != null && !pc.IsLocal)
            {
                CharacterAssembler ca = pc.GetComponentInChildren<CharacterAssembler>();
                if (ca != null)
                {
                    ca.SetVisibility(1f);
                }
            }
        }
        playersInside.Clear();
    }
}
