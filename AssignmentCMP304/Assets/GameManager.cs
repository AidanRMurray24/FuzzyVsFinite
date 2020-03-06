using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [SerializeField] private float timeScaleValue = 1f;
    [SerializeField] private int numOfRounds = 10;
    [SerializeField] private TextMeshProUGUI roundNumText = null;
    [SerializeField] private TextMeshProUGUI fsmScoreText = null;
    [SerializeField] private TextMeshProUGUI fusmScoreText = null;
    [SerializeField] private FuzzyAgentController fuzzyAgentController = null;
    [SerializeField] private FiniteAgentController finiteAgentController = null;

    private int fsmCurrentScore = 0;
    private int fusmCurrentScore = 0;
    private int roundsPast = 1;

    public enum Agent
    {
        FSM,
        FUSM
    }

    private void Awake()
    {
        if (instance != null)
        {
            if (instance != this)
            {
                Destroy(this.gameObject);
            }
        }
        else
        {
            instance = this;
            DontDestroyOnLoad(this);
        }
    }

    private void Start()
    {
        // Change the time scale of the game to the set value
        Time.timeScale = timeScaleValue;

        // Set the text up at the start of the game
        fsmScoreText.text = "FSM Agent: " + fsmCurrentScore;
        fusmScoreText.text = "FuSM Agent: " + fusmCurrentScore;
        roundNumText.text = "ROUND " + roundsPast;
    }

    public void AddPoint(Agent agent)
    {
        // Update the agents' scores
        switch (agent)
        {
            case Agent.FSM:
                fsmCurrentScore++;
                fsmScoreText.text = "FSM Agent: " + fsmCurrentScore;
                break;
            case Agent.FUSM:
                fusmCurrentScore++;
                fusmScoreText.text = "FuSM Agent: " + fusmCurrentScore;
                break;
            default:
                break;
        }

        // Advance to the next round if the num of rounds has not been reached
        if (roundsPast < numOfRounds)
        {
            roundsPast++;
            roundNumText.text = "ROUND " + roundsPast;
            Reset();
        }
    }

    private void Reset()
    {
        fuzzyAgentController.Reset();
        finiteAgentController.Reset();
    }


}
