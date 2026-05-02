using System;
using System.Security.Cryptography;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Rendering;
public struct InputFrame
{
    public bool left;
    public bool right;
    public bool sprint;
    public bool attack;
}

public enum ActionState : byte
{
    Idle,
    Attacking
}

public struct PlayerState
{
    public int playerID;
    
    public float positionX;
    public float positionY;
    public float velocityX;
    public int sprintBar;

    //handles framedata of the attack
    public ActionState actionState;
    public int attackFrame;

    //handles if player landed a hit
    public bool hasHit;
}

public struct GameState
{
    public PlayerState p1;
    public PlayerState p2;
}
public class GameSimulator
{
    const float fixedDeltaTime = 1f / 60f;
    const int MAXFRAMES = 6000; //10s

    public GameState gameState;
    GameState[] stateStorage = new GameState[MAXFRAMES];

    InputFrame[] inputStorage1 = new InputFrame[MAXFRAMES];
    InputFrame[] inputStorage2 = new InputFrame[MAXFRAMES];

    InputFrame lastInput1;
    InputFrame lastInput2;

    private int currFrame = 0;
    public int winner = -1;

    //GAME VARS--------------------------------------------------------------
    public Vector2 p1StartPos = new Vector2(-6f, -4f);
    public Vector2 p2StartPos = new Vector2(6f, -4f);
    public int maxSprint = 100;
    const float speed = 4f;
    //ATTACKS
    //frame data
    public int startupFrames = 6;
    public int activeFrames = 3;
    public int recoveryFrames = 22;
    //move properies
    public float attackRange = 2f;

    public void Init()
    {
        gameState = new GameState
        {
            p1 = new PlayerState
            {
                positionX = p1StartPos.x,
                positionY = p1StartPos.y,
                sprintBar = maxSprint,
                playerID = 0,
                actionState = ActionState.Idle,
                attackFrame = 0,
                velocityX = 0,
                hasHit = false
            },
            p2 = new PlayerState
            {
                positionX = p2StartPos.x,
                positionY = p2StartPos.y,
                sprintBar = maxSprint,
                playerID = 1,
                actionState = ActionState.Idle,
                attackFrame = 0,
                velocityX = 0,
                hasHit = false
            }
        };
    }

    public void Simulate(InputFrame player1, InputFrame player2)
    {
        SimulatePlayer(ref gameState.p1, ref gameState.p2, player1);
        SimulatePlayer(ref gameState.p2, ref gameState.p1, player2);
    }

    //direct logic
    private void SimulatePlayer(ref PlayerState player, ref PlayerState opponent, InputFrame input)
    {
        switch(player.actionState)
        {
            case ActionState.Idle:
                {
                    float direction = 0f;

                    if (input.left) direction -= 1f;
                    if (input.right) direction += 1f;
                    if (input.sprint && player.sprintBar > 0)
                    {
                        direction *= 1.5f;
                        player.sprintBar--;
                    }
                    if (input.attack) direction = 0;

                    // Apply velocity directly
                    player.velocityX = direction * speed;

                    // Integrate position
                    player.positionX += player.velocityX * fixedDeltaTime;

                    if(input.attack)
                    {
                        player.actionState = ActionState.Attacking;
                        player.attackFrame = 0;
                    }

                    break;
                }

            case ActionState.Attacking:
                {
                    if (player.attackFrame == 0)
                    {
                        player.hasHit = false;
                    }
                    //startup

                    //ATTACK
                    PlayerState attacker = player;
                    PlayerState target = opponent;
                    if (player.attackFrame >= startupFrames && player.attackFrame <= startupFrames + activeFrames && !player.hasHit)
                    {
                        //try and hit opponent
                        if(Mathf.Abs(attacker.positionX - target.positionX) <= attackRange)
                        {
                            if (winner == -1)
                            {
                                winner = player.playerID;
                            }

                            player.hasHit = true;
                        }

                    }

                    player.attackFrame++;

                    //endlag
                    if (player.attackFrame >= startupFrames + activeFrames + recoveryFrames)
                    {
                        player.actionState = ActionState.Idle;
                        player.attackFrame = 0;
                    }

                    break;
                }
        }
        
    }

    //can take null
    public void AdvanceFrame(InputFrame input1, InputFrame? inputReal2)
    {
        int index = currFrame % MAXFRAMES;

        stateStorage[index] = gameState;
        
        //local
        inputStorage1[index] = input1;
        lastInput1 = input1;

        //if new value, swap. If null, predict based on last input
        InputFrame input2;
        if(inputReal2.HasValue)
        {
            input2 = inputReal2.Value;
            lastInput2 = input2;
        }
        else
        {
            input2 = lastInput2;
        }

        inputStorage2[index] = input2;

        Simulate(input1, input2);
        
        currFrame++;
    }

    //"rollback" part
    public void OnLateInputRecieved(int frame, InputFrame realInput)
    {
        int index = frame % MAXFRAMES;

        if (CheckIfInputsMatch(inputStorage2[index], realInput))
            return; // no rollback needed

        inputStorage2[index] = realInput;
        RollbackFrame(frame);
    }

    public void RollbackFrame(int frame)
    {
        int rollbackIndex = frame % MAXFRAMES;
        //old state
        gameState = stateStorage[rollbackIndex];

        int tempframe = frame;

        //fastforward
        while(tempframe < currFrame)
        {
            //Debug.Log("ROLLBACK @ Frame " + frame);
            int index = tempframe % MAXFRAMES;
            Simulate(inputStorage1[index], inputStorage2[index]);
            tempframe++;
        }
    }

    private bool CheckIfInputsMatch(InputFrame a, InputFrame b)
    {
        return a.left == b.left && 
            a.right == b.right &&
            a.attack == b.attack &&
            a.sprint == b.sprint;
    }

    public int GetFrame()
    {
        return currFrame;
    }

    public InputFrame GetLastRemoteInput()
    {
        return lastInput2;
    }

    public void WinState(int playerID)
    {
        Debug.Log("Player" + (playerID + 1) + " Wins");
        //pause game
        
        //ui
        //reset
        ResetGame();
    }

    public void ResetGame()
    {
        Init();

        currFrame = 0;
        winner = -1;

        Array.Clear(stateStorage, 0, stateStorage.Length);
        Array.Clear(inputStorage1, 0, inputStorage1.Length);
        Array.Clear(inputStorage2, 0, inputStorage2.Length);

        lastInput1 = new InputFrame { left = false, right = false, sprint = false, attack = false };
        lastInput2 = new InputFrame { left = false, right = false, sprint = false, attack = false };
    }
}
