using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Generic;

public class LocalNetworkDiscovery : MonoBehaviour
{
    private const int BROADCAST_PORT = 47777;
    private const int GAME_PORT      = 7777;

    // Persistent clients — created once, reused every broadcast cycle
    private UdpClient broadcastClient;
    private UdpClient receiveClient;

    private string serverIP = "";

    // Thread-safe queue for server discovery events
    private readonly Queue<string> discoveredServers = new Queue<string>();
    private readonly object        queueLock         = new object();

    public event Action<string> OnServerFound;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Update()
    {
        // Process queued discoveries on the main thread
        lock (queueLock)
        {
            while (discoveredServers.Count > 0)
            {
                string ip = discoveredServers.Dequeue();
                Debug.Log($"[Discovery] Processing server on main thread: {ip}");
                OnServerFound?.Invoke(ip);
            }
        }
    }

    private void OnDestroy()
    {
        CancelInvoke();
        CloseClient(ref broadcastClient, "broadcast");
        CloseClient(ref receiveClient,   "receive");
    }

    // ─── Server (host) ───────────────────────────────────────────────────────

    public void StartServer()
    {
        serverIP = GetLocalIPAddress();

        Debug.Log($"[Discovery] Server broadcast starting — {serverIP}:{GAME_PORT}");

        // Create the broadcast client once and keep it alive
        try
        {
            broadcastClient                = new UdpClient();
            broadcastClient.EnableBroadcast = true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Failed to create broadcast client: {e.Message}");
            return;
        }

        InvokeRepeating(nameof(BroadcastServer), 0f, 1f);
    }

    private void BroadcastServer()
    {
        if (broadcastClient == null) return;

        try
        {
            string message = $"GAME_SERVER:{serverIP}:{GAME_PORT}";
            byte[] data    = Encoding.UTF8.GetBytes(message);

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);
            broadcastClient.Send(data, data.Length, endPoint);

            Debug.Log($"[Discovery] Broadcasting: {message}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Broadcast error: {e.Message}");
        }
    }

    // ─── Client (joiner) ─────────────────────────────────────────────────────

    public void StartClient()
    {
        Debug.Log($"[Discovery] Client listening on port {BROADCAST_PORT}...");

        try
        {
            receiveClient = new UdpClient();
            receiveClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiveClient.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));
            receiveClient.BeginReceive(OnBroadcastReceived, null);
            Debug.Log("[Discovery] ✅ Listening for servers.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Failed to start client listener: {e.Message}");
        }
    }

    private void OnBroadcastReceived(IAsyncResult result)
    {
        try
        {
            if (receiveClient == null) return;

            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, BROADCAST_PORT);
            byte[]     data     = receiveClient.EndReceive(result, ref endPoint);
            string     message  = Encoding.UTF8.GetString(data);

            Debug.Log($"[Discovery] Received: {message} from {endPoint.Address}");

            if (message.StartsWith("GAME_SERVER:"))
            {
                string[] parts = message.Split(':');
                if (parts.Length >= 3)
                {
                    string foundIP = parts[1];
                    Debug.Log($"[Discovery] ✅ Server found at {foundIP}");
                    lock (queueLock)
                        discoveredServers.Enqueue(foundIP);
                }
                else
                {
                    Debug.LogWarning($"[Discovery] Invalid message format: {message}");
                }
            }

            // Continue listening
            if (receiveClient != null)
                receiveClient.BeginReceive(OnBroadcastReceived, null);
        }
        catch (ObjectDisposedException)
        {
            Debug.Log("[Discovery] Client socket closed.");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Receive error: {e.Message}\n{e.StackTrace}");
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private string GetLocalIPAddress()
    {
        // Method 1: Hostname lookup
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    Debug.Log($"[Discovery] Local IP (hostname): {ip}");
                    return ip.ToString();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Discovery] Hostname resolution failed: {e.Message}");
        }

        // Method 2: Dummy UDP connect to determine outbound interface
        try
        {
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint ep = socket.LocalEndPoint as IPEndPoint;
                if (ep != null)
                {
                    Debug.Log($"[Discovery] Local IP (socket): {ep.Address}");
                    return ep.Address.ToString();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Discovery] Socket IP detection failed: {e.Message}");
        }

        Debug.LogError("[Discovery] Could not determine local IP — falling back to loopback.");
        return "127.0.0.1";
    }

    private void CloseClient(ref UdpClient client, string label)
    {
        if (client == null) return;
        try
        {
            client.Close();
            Debug.Log($"[Discovery] Closed {label} client.");
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Discovery] Error closing {label} client: {e.Message}");
        }
        finally
        {
            client = null;
        }
    }
}
