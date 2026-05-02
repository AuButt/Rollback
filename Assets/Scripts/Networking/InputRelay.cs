using System.Collections;
using UnityEngine;
using Unity.Collections;
using Unity.Netcode;

public class InputRelay : NetworkBehaviour
{
    public FrameLoop loop;
    private int lastSentFrame = -1;

    private int readyCount = 0;
    private bool matchStarted = false;

    private void Awake()
    {
        if (loop == null)
            loop = FindObjectOfType<FrameLoop>();
    }

    private void Start()
    {
        StartCoroutine(RegisterWhenReady());
    }

    private IEnumerator RegisterWhenReady()
    {
        while (NetworkManager.Singleton == null ||
               NetworkManager.Singleton.CustomMessagingManager == null)
        {
            yield return null;
        }

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            "Ready",
            OnReadyReceived
        );

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            "Input",
            OnReceiveInput
        );

        NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(
            "StartMatch",
            OnStartMatchReceived
        );

        Debug.Log("InputRelay handlers registered safely");
    }


    //Manual Serializtaion, raw
    public void SendInput(int frame, InputFrame input)
    {
        if (frame == lastSentFrame) return;
        lastSentFrame = frame;

        var writer = new FastBufferWriter(16, Allocator.Temp);

        writer.WriteValueSafe(frame);
        writer.WriteValueSafe(input.left);
        writer.WriteValueSafe(input.right);
        writer.WriteValueSafe(input.sprint);
        writer.WriteValueSafe(input.attack);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessage(
            "Input",
            NetworkManager.ServerClientId,
            writer
        );

        writer.Dispose();
    }

    private void OnReceiveInput(ulong clientId, FastBufferReader reader)
    {
        //deserialize from binary
        reader.ReadValueSafe(out int frame);
        reader.ReadValueSafe(out bool left);
        reader.ReadValueSafe(out bool right);
        reader.ReadValueSafe(out bool sprint);
        reader.ReadValueSafe(out bool attack);

        Debug.Log("INPUT RECEIVED frame: " + frame);

        loop.OnRemoteInput(frame, new InputFrame
        {
            left = left,
            right = right,
            sprint = sprint,
            attack = attack
        });
    }

    //when server ready
    private void OnReadyReceived(ulong clientId, FastBufferReader reader)
    {
        if (!NetworkManager.Singleton.IsServer) return;

        Debug.Log("HOST RECEIVED READY from client: " + clientId);

        readyCount++;

        if (!matchStarted && NetworkManager.Singleton.ConnectedClients.Count >= 2)
        {
           StartCoroutine(StartMatch());
        }
    }

    private IEnumerator StartMatch()
    {
        if (matchStarted) yield break;
        matchStarted = true;

        Debug.Log("HOST STARTING MATCH (waiting for clients)");

        // wait until at least 2 players exist
        while (NetworkManager.Singleton.ConnectedClients.Count < 2)
        {
            yield return null;
        }

        var writer = new FastBufferWriter(1, Allocator.Temp);

        NetworkManager.Singleton.CustomMessagingManager.SendNamedMessageToAll(
            "StartMatch",
            writer
        );

        writer.Dispose();

        loop.BeginSimulation();
    }

    private void OnStartMatchReceived(ulong clientId, FastBufferReader reader)
    {
        Debug.Log("CLIENT RECEIVED START MATCH");

        if (loop != null)
        {
            loop.BeginSimulation();
        }
    }
}