using Unity.Netcode.Components;
using UnityEngine;

[AddComponentMenu("Netcode/Client Network Transform")]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }

    protected override void Update()
    {
        if (Unity.Netcode.NetworkManager.Singleton == null || !Unity.Netcode.NetworkManager.Singleton.IsListening || !IsSpawned)
        {
            return;
        }
        base.Update();
    }
}
