using UnityEngine;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System;
using System.Collections.Generic;

public class LocalNetworkDiscovery : MonoBehaviour
{
    private const int BROADCAST_PORT = 47777;
    private const int GAME_PORT = 7777;
    private UdpClient broadcastClient;
    private UdpClient receiveClient;
    private bool isServer = false;
    private string serverIP = "";
    
    // Queue for thread-safe server discovery
    private readonly Queue<string> discoveredServers = new Queue<string>();
    private readonly object queueLock = new object();
    
    public event Action<string> OnServerFound;
    
    private void Update()
    {
        // Process discovered servers on main thread
        lock (queueLock)
        {
            while (discoveredServers.Count > 0)
            {
                string discoveredIP = discoveredServers.Dequeue();
                Debug.Log($"[Discovery] Processing server discovery on main thread: {discoveredIP}");
                OnServerFound?.Invoke(discoveredIP);
            }
        }
    }
    
    public void StartServer()
    {
        isServer = true;
        serverIP = GetLocalIPAddress();
        
        Debug.Log($"[Discovery] Starting server broadcast on {serverIP}:{GAME_PORT}");
        Debug.Log($"[Discovery] Broadcasting to port {BROADCAST_PORT} every 1 second");
        
        // Start broadcasting
        InvokeRepeating(nameof(BroadcastServer), 0f, 1f);
    }
    
    public void StartClient()
    {
        isServer = false;
        
        Debug.Log($"[Discovery] Starting client, listening on port {BROADCAST_PORT}");
        
        try
        {
            // Start listening for broadcasts
            // Use IPAddress.Any to avoid conflicts when host is on same machine
            receiveClient = new UdpClient();
            receiveClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            receiveClient.Client.Bind(new IPEndPoint(IPAddress.Any, BROADCAST_PORT));
            receiveClient.BeginReceive(OnBroadcastReceived, null);
            Debug.Log("[Discovery] ✅ Client listening for servers...");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] ❌ Failed to start client: {e.Message}");
        }
    }
    
    private void BroadcastServer()
    {
        try
        {
            broadcastClient = new UdpClient();
            broadcastClient.EnableBroadcast = true;
            
            string message = $"GAME_SERVER:{serverIP}:{GAME_PORT}";
            byte[] data = Encoding.UTF8.GetBytes(message);
            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Broadcast, BROADCAST_PORT);
            broadcastClient.Send(data, data.Length, endPoint);
            broadcastClient.Close();
            
            Debug.Log($"[Discovery] Broadcasting: {message} to {IPAddress.Broadcast}:{BROADCAST_PORT}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Broadcast error: {e.Message}");
        }
    }
    
    private void OnBroadcastReceived(IAsyncResult result)
    {
        try
        {
            if (receiveClient == null || result == null)
            {
                return; // Client was disposed
            }
            
            IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, BROADCAST_PORT);
            byte[] data = receiveClient.EndReceive(result, ref endPoint);
            string message = Encoding.UTF8.GetString(data);
            
            Debug.Log($"[Discovery] Received broadcast: {message} from {endPoint.Address}");
            
            if (message.StartsWith("GAME_SERVER:"))
            {
                string[] parts = message.Split(':');
                if (parts.Length >= 3)
                {
                    string foundServerIP = parts[1];
                    Debug.Log($"[Discovery] ✅ Server found at {foundServerIP}");
                    
                    // Queue for processing on main thread
                    lock (queueLock)
                    {
                        discoveredServers.Enqueue(foundServerIP);
                    }
                }
                else
                {
                    Debug.LogWarning($"[Discovery] Invalid message format: {message}");
                }
            }
            
            // Continue listening
            if (receiveClient != null)
            {
                receiveClient.BeginReceive(OnBroadcastReceived, null);
            }
        }
        catch (ObjectDisposedException)
        {
            // UdpClient was disposed, this is expected on cleanup
            Debug.Log("[Discovery] Client closed");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Discovery] Receive error: {e.Message}\n{e.StackTrace}");
        }
    }
    
    private string GetLocalIPAddress()
    {
        try
        {
            // Method 1: Try using NetworkInterface (most reliable)
            var host = Dns.GetHostEntry(Dns.GetHostName());
            foreach (var ip in host.AddressList)
            {
                if (ip.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(ip))
                {
                    Debug.Log($"[Discovery] Found IP via hostname: {ip}");
                    return ip.ToString();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Discovery] Hostname resolution failed: {e.Message}. Trying alternative method...");
        }
        
        try
        {
            // Method 2: Connect to external IP to find local IP (works without internet)
            using (Socket socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 65530);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                if (endPoint != null)
                {
                    Debug.Log($"[Discovery] Found IP via socket: {endPoint.Address}");
                    return endPoint.Address.ToString();
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Discovery] Socket method failed: {e.Message}");
        }
        
        Debug.LogError("[Discovery] Could not determine local IP address, using loopback");
        return "127.0.0.1";
    }
    
    private void OnDestroy()
    {
        CancelInvoke();
        
        try
        {
            if (broadcastClient != null)
            {
                broadcastClient.Close();
                broadcastClient = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Discovery] Error closing broadcast client: {e.Message}");
        }
        
        try
        {
            if (receiveClient != null)
            {
                receiveClient.Close();
                receiveClient = null;
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Discovery] Error closing receive client: {e.Message}");
        }
    }
}
