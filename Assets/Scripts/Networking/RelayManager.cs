using System.Threading.Tasks;
using TMPro;
using Unity.Collections;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

public class RelayManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI joinCodeText;
    [SerializeField] private TMP_InputField joinCodeInput;

    private FrameLoop loop;

    private bool clientReadySent;

    private Task initTask;

    private void Awake()
    {
        loop = FindObjectOfType<FrameLoop>();
        initTask = InitServices();
    }

    private async Task InitServices()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }

        Debug.Log("Unity Services Initialized");
    }

    public async void StartRelay()
    {
        await initTask;

        string code = await StartHostRelay();
        joinCodeText.text = code;
    }

    private async Task<string> StartHostRelay(int maxConnections = 2)
    {
        if (RelayService.Instance == null)
        {
            Debug.LogError("RelayService not ready");
            return null;
        }

        Allocation allocation =
            await RelayService.Instance.CreateAllocationAsync(maxConnections);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("UnityTransport missing on NetworkManager");
            return null;
        }

        transport.SetRelayServerData(
            allocation.RelayServer.IpV4,
            (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes,
            allocation.Key,
            allocation.ConnectionData,
            allocation.ConnectionData,
            true
        );

        NetworkManager.Singleton.NetworkConfig.PlayerPrefab = null;

        NetworkManager.Singleton.StartHost();

        Debug.Log($"Host started | IsServer: {NetworkManager.Singleton.IsServer}");

        loop.localReady = true;

        string joinCode =
            await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        Debug.Log("Join Code: " + joinCode);

        return joinCode;
    }

    public async void JoinRelay()
    {
        await initTask;

        await StartClientRelay(joinCodeInput.text.Trim());
    }

    private async Task<bool> StartClientRelay(string joinCode)
    {
        try
        {
            if (string.IsNullOrEmpty(joinCode))
            {
                Debug.LogError("Join code is empty");
                return false;
            }

            if (RelayService.Instance == null)
            {
                Debug.LogError("RelayService not ready");
                return false;
            }

            JoinAllocation joinAllocation =
                await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();

            if (transport == null)
            {
                Debug.LogError("UnityTransport missing on NetworkManager");
                return false;
            }

            transport.SetRelayServerData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData,
                true
            );

            NetworkManager.Singleton.StartClient();

            Debug.Log($"Client started | IsClient: {NetworkManager.Singleton.IsClient}");

            while (
                !NetworkManager.Singleton.IsConnectedClient ||
                NetworkManager.Singleton.CustomMessagingManager == null
            )
            {
                await Task.Yield();
            }

            Debug.Log("CLIENT FULLY CONNECTED");

            SendReady();

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError("Relay join failed: " + e.Message);
            return false;
        }
    }

    // ---------------- READY ----------------
    private void SendReady()
    {
        if (clientReadySent) return;
        clientReadySent = true;

        var writer = new FastBufferWriter(1, Allocator.Temp);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            "Ready",
            NetworkManager.ServerClientId,
            writer
        );

        writer.Dispose();

        Debug.Log("CLIENT SENT READY");
    }
}