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
    private float recordedTime = 0;

    public enum Agent
    {
        FSM,
        FUSM
    }
    public struct TestData
    {
        public int roundNumber;
        public Agent agentThatWon;
        public float timeTaken;
        public int fuzzyBulletsFired;
        public int fuzzyBulletsHit;
        public int finiteBulletsFired;
        public int finiteBulletsHit;
        public int fuzzyIdleCount;
        public int fuzzyMoveToTargetCount;
        public int fuzzyHidingCount;
        public int fuzzyShootingCount;
        public int fuzzyReloadCount;
        public int finiteIdleCount;
        public int finiteMoveToTargetCount;
        public int finiteHidingCount;
        public int finiteShootingCount;
        public int finiteReloadCount;
    }
    private TestData testData;

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

        // Create a new report
        CSVManager.CreateReport();
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

        UpdateTestData(agent);

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

    private void UpdateTestData(Agent agent)
    {
        // Update the test data
        testData.roundNumber = roundsPast;
        testData.agentThatWon = agent;
        testData.timeTaken = Time.time - recordedTime;
        recordedTime = Time.time;
        testData.fuzzyBulletsFired = fuzzyAgentController.BulletsFired;
        testData.fuzzyBulletsHit = fuzzyAgentController.BulletsHit;
        testData.finiteBulletsFired = finiteAgentController.BulletsFired;
        testData.finiteBulletsHit = finiteAgentController.BulletsHit;
        testData.fuzzyIdleCount = fuzzyAgentController.StateChanges[(int)FuzzyAgentController.FuzzyAgentState.IDLE];
        testData.fuzzyMoveToTargetCount = fuzzyAgentController.StateChanges[(int)FuzzyAgentController.FuzzyAgentState.MOVE_TO_TARGET];
        testData.fuzzyHidingCount = fuzzyAgentController.StateChanges[(int)FuzzyAgentController.FuzzyAgentState.HIDE];
        testData.fuzzyShootingCount = fuzzyAgentController.StateChanges[(int)FuzzyAgentController.FuzzyAgentState.SHOOT_TARGET];
        testData.fuzzyReloadCount = fuzzyAgentController.StateChanges[(int)FuzzyAgentController.FuzzyAgentState.RELOAD];
        testData.finiteIdleCount = finiteAgentController.StateChanges[(int)FiniteAgentController.FiniteAgentState.IDLE];
        testData.finiteMoveToTargetCount = finiteAgentController.StateChanges[(int)FiniteAgentController.FiniteAgentState.MOVE_TO_TARGET];
        testData.finiteHidingCount = finiteAgentController.StateChanges[(int)FiniteAgentController.FiniteAgentState.HIDE];
        testData.finiteShootingCount = finiteAgentController.StateChanges[(int)FiniteAgentController.FiniteAgentState.SHOOT_TARGET];
        testData.finiteReloadCount = finiteAgentController.StateChanges[(int)FiniteAgentController.FiniteAgentState.RELOAD];
        CSVManager.AppendToReport(GetReportLine());
    }

    private string[] GetReportLine()
    {
        string[] returnable = new string[15];
        returnable[0] = testData.roundNumber.ToString();
        returnable[1] = testData.agentThatWon.ToString();
        returnable[2] = testData.timeTaken.ToString();
        returnable[3] = testData.fuzzyBulletsFired.ToString();
        returnable[4] = testData.fuzzyBulletsHit.ToString();
        returnable[5] = testData.finiteBulletsFired.ToString();
        returnable[6] = testData.finiteBulletsHit.ToString();
        returnable[7] = testData.fuzzyMoveToTargetCount.ToString();
        returnable[8] = testData.fuzzyHidingCount.ToString();
        returnable[9] = testData.fuzzyShootingCount.ToString();
        returnable[10] = testData.fuzzyReloadCount.ToString();
        returnable[11] = testData.finiteMoveToTargetCount.ToString();
        returnable[12] = testData.finiteHidingCount.ToString();
        returnable[13] = testData.finiteShootingCount.ToString();
        returnable[14] = testData.finiteReloadCount.ToString();
        return returnable;
    }



}
