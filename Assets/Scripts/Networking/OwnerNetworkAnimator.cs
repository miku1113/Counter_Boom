using Unity.Netcode.Components;
using UnityEngine;

[AddComponentMenu("Netcode/Owner Network Animator")]
public class OwnerNetworkAnimator : NetworkAnimator
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
