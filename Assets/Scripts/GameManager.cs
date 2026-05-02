using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    private FrameLoop frameLoop;
    [SerializeField] public Text uiWinText;

    private float resetTimer = 3f;
    private float countTimer = 0f;
    private void Start()
    {
        frameLoop = GetComponent<FrameLoop>();
    }

    // Update is called once per frame
    void Update()
    {
        if(frameLoop.sim.winner == 0)
        {
            uiWinText.text = "Player 1 Wins";
            countTimer += Time.deltaTime;
        }
        else if(frameLoop.sim.winner == 1)
        {
            uiWinText.text = "Player 2 Wins";
            countTimer += Time.deltaTime;
        }

        if(countTimer > resetTimer)
        {
            countTimer = 0;
            frameLoop.sim.ResetGame();
            uiWinText.text = "";
        }

    }
}
