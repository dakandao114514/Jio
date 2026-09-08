using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

public class NetUI : MonoBehaviour
{
    string ipInput = "127.0.0.1";
    string statusText = "";

    /// <summary>确保 NetworkManager 上有 UnityTransport 并正确注册</summary>
    UnityTransport EnsureTransport()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null) return null;

        var transport = nm.GetComponent<UnityTransport>();
        if (transport == null)
        {
            transport = nm.gameObject.AddComponent<UnityTransport>();
        }

        // NGO 1.8.x 需要显式赋值 NetworkTransport
        if (nm.NetworkConfig.NetworkTransport == null)
            nm.NetworkConfig.NetworkTransport = transport;

        return transport;
    }

    void OnGUI()
    {
        if (NetworkManager.Singleton == null)
        {
            GUI.Label(new Rect(10, Screen.height - 80, 300, 24), "无 NetworkManager");
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            if (NetworkManager.Singleton.IsServer)
                statusText = NetworkManager.Singleton.IsHost ? "主机模式（Host）" : "服务端";
            else
                statusText = "已连接（Client）";
            GUI.Label(new Rect(10, Screen.height - 30, 200, 24), statusText);
            return;
        }

        float y = Screen.height - 80;
        GUI.Label(new Rect(10, y, 300, 24), "电极弹射联机");
        y += 24;

        if (GUI.Button(new Rect(10, y, 100, 30), "启动主机"))
        {
            var transport = EnsureTransport();
            if (transport != null)
                transport.SetConnectionData("0.0.0.0", 7777);
            NetworkManager.Singleton.StartHost();
        }

        GUI.Label(new Rect(120, y + 5, 20, 20), "IP:");
        ipInput = GUI.TextField(new Rect(145, y, 120, 24), ipInput);

        if (GUI.Button(new Rect(275, y, 80, 30), "连接"))
        {
            var transport = EnsureTransport();
            if (transport != null)
                transport.SetConnectionData(ipInput, 7777);
            NetworkManager.Singleton.StartClient();
        }
    }
}
