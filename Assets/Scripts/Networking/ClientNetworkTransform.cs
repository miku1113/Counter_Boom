using Unity.Netcode.Components;
using UnityEngine;

[AddComponentMenu("Netcode/Client Network Transform")]
public class ClientNetworkTransform : NetworkTransform
{
    protected override bool OnIsServerAuthoritative()
    {
        return false;
    }
}
