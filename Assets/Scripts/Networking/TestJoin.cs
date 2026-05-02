using Unity.Netcode;
using UnityEngine;

public class TestJoin : MonoBehaviour
{
    public RelayManager relayManager;
    public FrameLoop frameLoop;

    private bool started = false;

    public void StartHost()
    {
        relayManager.StartRelay();
        NetworkManager.Singleton.StartHost();
        started = true;
    }

    public void StartClient(string code)
    {
        relayManager.JoinRelay();
        NetworkManager.Singleton.StartClient();
        started = true;
    }
}