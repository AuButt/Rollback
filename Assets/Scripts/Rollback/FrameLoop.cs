using System;
using Unity.Netcode;
using Unity.Services.Qos.V2.Models;
using UnityEngine;
using static GameSimulator;

public class FrameLoop : NetworkBehaviour
{
    public GameSimulator sim = new GameSimulator(); // reference to simulation
    public InputRelay relay;
    public GameObject player1;
    public GameObject player2;

    [SerializeField] public GameObject player1hb;
    [SerializeField] public GameObject player2hb;

    private SpriteRenderer spriteRenderer1;
    private SpriteRenderer spriteRenderer2;

    InputFrame[] remoteInputs = new InputFrame[6000];
    bool[] hasRemoteInput = new bool[6000];

    InputFrame currentInput;
    InputFrame[] localInputs = new InputFrame[6000];

    public bool simRunning = false;

    public bool localReady = false;
    public bool remoteReady = false;

    public void BeginSimulation()
    {
        sim.Init();
        simRunning = true;
    }

    private async void Start()
    {
        spriteRenderer1 = player1hb.GetComponent<SpriteRenderer>();
        spriteRenderer2 = player2hb.GetComponent<SpriteRenderer>();

        while (NetworkManager.Singleton == null ||
               NetworkManager.Singleton.CustomMessagingManager == null)
        {
            await System.Threading.Tasks.Task.Yield();
        }

        Debug.Log("StartMatch handler registered safely");
    }

    //input packet tried for network
    [Serializable]
    public struct NetInputPacket
    {
        public int frame;
        public bool left;
        public bool right;
        public bool sprint;
        public bool attack;
    }
    //takes input, stores buffer, sends on ntwork
    private void Update()
    {
        int frame = sim.GetFrame();

        currentInput = new InputFrame
        {
            left = Input.GetKey(KeyCode.A),
            right = Input.GetKey(KeyCode.D),
            sprint = Input.GetKey(KeyCode.W),
            attack = Input.GetKeyDown(KeyCode.S)
        };
        localInputs[frame % 6000] = currentInput;
        if (relay != null)
            SendInputToNetwork(frame, currentInput);
    }
    //simulation: rollback tick
    void FixedUpdate()
    {
        if (!simRunning) return;

        int frame = sim.GetFrame();

        //store local inputs
        InputFrame p1 = localInputs[frame % 6000];

        InputFrame p2;

        if (hasRemoteInput[frame % 6000])
            p2 = remoteInputs[frame % 6000];
        else
            p2 = sim.GetLastRemoteInput(); 

        sim.AdvanceFrame(p1, p2);

    }
    //called when server triggers
    [ClientRpc]
    public void StartMatchClientRpc()
    {
        Debug.Log("START MATCH RECEIVED (CLIENT)");

        // THIS is where you start rollback / simulation
        // sim.Start();
        BeginSimulation();
    }
    // start for client
    public void StartMatch()
    {
        if (!IsServer) return;

        Debug.Log("HOST STARTING MATCH");
        BeginSimulation();
        //broadcast
        StartMatchClientRpc();
    }
    // returnign remote input
    InputFrame? TryGetRemoteInput(int frame)
    {
        if (hasRemoteInput[frame % 6000])
            return remoteInputs[frame % 6000];

        return null;
    }

    //localinput reading
    InputFrame ReadInput()
    {
        return new InputFrame
        {
            left = Input.GetKey(KeyCode.A),
            right = Input.GetKey(KeyCode.D),
            sprint = Input.GetKey(KeyCode.LeftShift),
            attack = Input.GetKey(KeyCode.Q)
        };
    }
    void LateUpdate()
    {
        player1.transform.position = new Vector3(
            sim.gameState.p1.positionX,
            sim.gameState.p1.positionY,
            0f
        );

        player2.transform.position = new Vector3(
            sim.gameState.p2.positionX,
            sim.gameState.p2.positionY,
            0f
        );

        float newWidth1 = Mathf.Lerp(0, 3, sim.gameState.p1.attackFrame / 9f);
        float newWidth2 = Mathf.Lerp(0, 3, sim.gameState.p2.attackFrame / 9f);
        player1hb.transform.localScale = new Vector3(newWidth1, 0.3146875f, 1);
        HitboxVisuals(sim.gameState.p1);
        player2hb.transform.localScale = new Vector3(newWidth2, 0.3146875f, 1);
        HitboxVisuals(sim.gameState.p2);
    }


    /* sim player 2 locally
    void InjectDelayedRemoteInput(int currentFrame)
    {
        int delayedFrame = currentFrame - delay;

        if (delayedFrame >= 0)
        {
            InputFrame realInput = fakeP2Inputs[delayedFrame % 6000];

            // Tell simulation the "real" input arrived
            sim.OnLateInputRecieved(delayedFrame, realInput);
        }
    }
    */

    //relay functions
    public void SendInputToNetwork(int frame, InputFrame input)
    {
        relay.SendInput(frame, input);
    }
    public void OnRemoteInput(int frame, InputFrame input)
    {
        int index = frame % 6000;

        if (hasRemoteInput[index] &&
            remoteInputs[index].attack == input.attack &&
            remoteInputs[index].left == input.left &&
            remoteInputs[index].right == input.right &&
            remoteInputs[index].sprint == input.sprint)
        {
            return;
        }

        remoteInputs[index] = input;
        hasRemoteInput[index] = true;

        sim.OnLateInputRecieved(frame, input);
    }

    public void OnReadyReceived(ulong clientId, FastBufferReader reader)
    {
        Debug.Log("READY RECEIVED in FrameLoop from: " + clientId);

        remoteReady = true;
    }

    //sloppy implementation (fix later)
    public void HitboxVisuals(PlayerState player)
    {
        if(player.playerID == 0)
        {
            if(sim.gameState.p1.attackFrame < sim.startupFrames)
            {
                spriteRenderer1.color = Color.yellow;
            }
            else if (sim.gameState.p1.attackFrame >= sim.startupFrames && sim.gameState.p1.attackFrame <= sim.startupFrames + sim.activeFrames)
            {
                spriteRenderer1.color = Color.red;
            }
            else
            {
                spriteRenderer1.color = Color.gray;
            }

        }
        else
        {
            if (sim.gameState.p2.attackFrame < sim.startupFrames)
            {
                spriteRenderer2.color = Color.yellow;
            }
            else if (sim.gameState.p2.attackFrame >= sim.startupFrames && sim.gameState.p2.attackFrame <= sim.startupFrames + sim.activeFrames)
            {
                spriteRenderer2.color = Color.red;
            }
            else
            {
                spriteRenderer2.color = Color.gray;
            }
        }
    }
}
