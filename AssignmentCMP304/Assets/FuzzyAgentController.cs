using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FLS;
using FLS.Rules;
using FLS.MembershipFunctions;

public class FuzzyAgentController : MonoBehaviour
{
    // Editor accessable variables
    [SerializeField] private int maxHealth = 100;
    [SerializeField] Transform target = null;
    [SerializeField] private HealthBar healthBar = null;
    [SerializeField] private Transform healthBarPos = null;

    // Linguistic variables
    private LinguisticVariable distToTarget = new LinguisticVariable("distToTarget");
    private LinguisticVariable health = new LinguisticVariable("health");
    private LinguisticVariable targetsHealth = new LinguisticVariable("targetsHealth");
    private LinguisticVariable actionToTake = new LinguisticVariable("actionToTake");

    // Membership functions
    struct DistToTargetMF
    {
        public IMembershipFunction veryClose;
        public IMembershipFunction close;
        public IMembershipFunction far;
        public IMembershipFunction veryFar;
    }
    private DistToTargetMF distToTargetMF;

    struct HealthMF
    {
        public IMembershipFunction veryLow;
        public IMembershipFunction low;
        public IMembershipFunction moderate;
        public IMembershipFunction high;
        public IMembershipFunction veryHigh;
    }
    private HealthMF healthMF;

    struct TargetsHealthMF
    {
        public IMembershipFunction veryLow;
        public IMembershipFunction low;
        public IMembershipFunction moderate;
        public IMembershipFunction high;
        public IMembershipFunction veryHigh;
    }
    private TargetsHealthMF targetsHealthMF;

    struct ActionToTakeMF
    {
        public IMembershipFunction stabTarget;
        public IMembershipFunction shootTarget;
        public IMembershipFunction hideFromTarget;
        public IMembershipFunction moveCloserToTarget;
    }
    private ActionToTakeMF actionToTakeMF;

    // Components
    private NavMeshAgent agent = null;
    private Animator animator = null;
    private Rigidbody rb = null;
    private Camera mainCamera = null;
    private IFuzzyEngine fuzzyEngine = null;

    // State enum
    public enum FuzzyAgentState
    {
        IDLE = 0,
        STAB = 1,
        SHOOT_TARGET = 2,
        HIDE = 3,
        MOVE_TO_TARGET = 4,
        DEAD = 5,
        NUM_STATES = 6
    }

    // Public variables
    public FuzzyAgentState State { get; private set; } = FuzzyAgentState.IDLE;
    public int CurrentHealth { get { return currentHealth; } }

    // Local variables
    private int currentHealth = 0;
    private int targetsCurrentHealth = 0;
    private List<FuzzyRule> fuzzyRules = new List<FuzzyRule>();

    private void Awake()
    {
        // Get the nav mesh agent component
        agent = GetComponent<NavMeshAgent>();
        if (agent == null)
        {
            Debug.Log("NavMeshAgent missing on FiniteAgentController script, Object: " + this.gameObject);
        }

        // Get the animator component from the child
        animator = transform.GetChild(0).GetComponent<Animator>();
        if (animator == null)
        {
            Debug.Log("Animator missing on FiniteAgentController script, Object: " + this.gameObject);
        }

        // Get the rigidbody componet from this gameobject
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.Log("Rigidbody missing on FiniteAgentController script, Object: " + this.gameObject);
        }

        // Get the main camera
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.Log("Camera missing on FiniteAgentController script, Object: " + this.gameObject);
        }

        // Create a new instance of the FuzzyEngine
        fuzzyEngine = new FuzzyEngineFactory().Default();
        if (fuzzyEngine == null)
        {
            Debug.Log("FuzzyEngineFactory missing on FiniteAgentController script, Object: " + this.gameObject);
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        // Set the current halth to be the max at the start of the game
        currentHealth = maxHealth;

        // Setup the health bar
        healthBar.SetMaxHealth(maxHealth);

        // Setup the membership functions for the linguistic variables
        {
            if (distToTarget == null)
            {
                Debug.Log("distToTarget is null");
            }

            // Dist To Target
            distToTargetMF.veryClose = distToTarget.MembershipFunctions.AddTrapezoid("veryClose", -50, -50, -5, -1);
            distToTargetMF.close = distToTarget.MembershipFunctions.AddTrapezoid("close", -50, -50, -5, -1);
            distToTargetMF.far = distToTarget.MembershipFunctions.AddTrapezoid("far", -50, -50, -5, -1);
            distToTargetMF.veryFar = distToTarget.MembershipFunctions.AddTrapezoid("veryFar", -50, -50, -5, -1);

            // Health
            healthMF.veryLow = distToTarget.MembershipFunctions.AddTrapezoid("veryLow", -50, -50, -5, -1);
            healthMF.low = distToTarget.MembershipFunctions.AddTrapezoid("low", -50, -50, -5, -1);
            healthMF.moderate = distToTarget.MembershipFunctions.AddTrapezoid("moderate", -50, -50, -5, -1);
            healthMF.high = distToTarget.MembershipFunctions.AddTrapezoid("high", -50, -50, -5, -1);
            healthMF.veryHigh = distToTarget.MembershipFunctions.AddTrapezoid("veryHigh", -50, -50, -5, -1);

            // Targets Health
            targetsHealthMF.veryLow = distToTarget.MembershipFunctions.AddTrapezoid("veryLow", -50, -50, -5, -1);
            targetsHealthMF.low = distToTarget.MembershipFunctions.AddTrapezoid("low", -50, -50, -5, -1);
            targetsHealthMF.moderate = distToTarget.MembershipFunctions.AddTrapezoid("moderate", -50, -50, -5, -1);
            targetsHealthMF.high = distToTarget.MembershipFunctions.AddTrapezoid("high", -50, -50, -5, -1);
            targetsHealthMF.veryHigh = distToTarget.MembershipFunctions.AddTrapezoid("veryHigh", -50, -50, -5, -1);

            // Action To Take
            actionToTakeMF.stabTarget = distToTarget.MembershipFunctions.AddTriangle("stabTarget", -50, -50, -5);
            actionToTakeMF.shootTarget = distToTarget.MembershipFunctions.AddTriangle("shootTarget", -50, -50, -5);
            actionToTakeMF.hideFromTarget = distToTarget.MembershipFunctions.AddTriangle("hideFromTarget", -50, -50, -5);
            actionToTakeMF.moveCloserToTarget = distToTarget.MembershipFunctions.AddTriangle("moveCloserToTarget", -50, -50, -5);
        }

        // Setup the rules for the fuzzy engine
        {
            fuzzyRules.Add(Rule.If(distToTarget.Is(distToTargetMF.veryClose)).Then(actionToTake.Is(actionToTakeMF.stabTarget)));
            
            // Add the rules to the engine
            for (int i = 0; i < fuzzyRules.Count; i++)
            {
                fuzzyEngine.Rules.Add(fuzzyRules[i]);
            }
        }

    }

    // Update is called once per frame
    private void Update()
    {
        // TESTING
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                TakeDamage(10);
            }
        } 

        // Change what gets updated depending on the current state
        switch (State)
        {
            case FuzzyAgentState.IDLE:
                UpdateIdleState();
                break;
            case FuzzyAgentState.STAB:
                UpdateStabState();
                break;
            case FuzzyAgentState.SHOOT_TARGET:
                UpdateShootTargetState();
                break;
            case FuzzyAgentState.HIDE:
                UpdateHideState();
                break;
            case FuzzyAgentState.MOVE_TO_TARGET:
                UpdateMoveToTargetState();
                break;
            case FuzzyAgentState.DEAD:
                break;
            default:
                Debug.Log("No state has been set!");
                break;
        }

        // Update the animator's speed variable to be the current velocity in the forward vector
        animator.SetFloat("Speed", Vector3.Dot(agent.velocity, transform.forward));

        // Update the position of the health bar only if the camera is looking at the agent
        if (Vector3.Dot(transform.position - mainCamera.transform.position, mainCamera.transform.forward) >= 0)
        {
            healthBar.transform.position = mainCamera.WorldToScreenPoint(healthBarPos.position);
        }

        // Get the targets current health
        targetsCurrentHealth = target.GetComponent<FiniteAgentController>().CurrentHealth;
    }

    private void UpdateIdleState()
    {
        // Get the results from the fuzzy engine
        double result = fuzzyEngine.Defuzzify(new { distToTarget = (double)Vector3.Distance(transform.position, target.position), health = (double)currentHealth, targetsHealth = (double)targetsCurrentHealth});

        Debug.Log("Result: " + result);
    }

    private void UpdateStabState()
    {

    }

    private void UpdateShootTargetState()
    {

    }

    private void UpdateHideState()
    {

    }

    private void UpdateMoveToTargetState()
    {
        agent.SetDestination(target.position);
    }

    public void TakeDamage(int amount)
    {
        // Take the amount off of the current health
        currentHealth -= amount;

        // If the current health is equal to or lower than 0 then the agent is dead
        if (currentHealth <= 0)
        {
            currentHealth = 0;

            // The agent is dead, change states
            State = FuzzyAgentState.DEAD;
        }

        // Update the health bar
        healthBar.SetHealth(currentHealth);
    }
}
