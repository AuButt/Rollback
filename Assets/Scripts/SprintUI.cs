using UnityEngine;

public class SprintUI : MonoBehaviour
{
    [SerializeField]
    public FrameLoop frameLoop;

    [SerializeField]
    public RectTransform p1SprintBar;
    [SerializeField]
    public RectTransform p2SprintBar;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float p1Sprint = frameLoop.sim.gameState.p1.sprintBar;
        float p2Sprint = frameLoop.sim.gameState.p2.sprintBar;
        float width1 = (p1Sprint / frameLoop.sim.maxSprint) * 361.6796f;
        float width2 = (p2Sprint / frameLoop.sim.maxSprint) * 361.6796f;

        p1SprintBar.sizeDelta = new Vector2(width1, 42.2419f);
        p2SprintBar.sizeDelta = new Vector2(width2, 42.2419f);
    }
}
