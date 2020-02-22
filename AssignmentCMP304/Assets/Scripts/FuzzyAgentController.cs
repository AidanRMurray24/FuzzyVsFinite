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
    [SerializeField] private Transform hidingSpotsTransform = null;
    [SerializeField] private ParticleSystem muzzleFlashParticle = null;

    // Linguistic variables
    private LinguisticVariable distToTarget = new LinguisticVariable("distToTarget");
    private LinguisticVariable health = new LinguisticVariable("health");
    private LinguisticVariable targetsHealth = new LinguisticVariable("targetsHealth");
    private LinguisticVariable actionToTake = new LinguisticVariable("actionToTake");

    // Membership functions
    struct DistToTargetMF
    {
        public IMembershipFunction close;
        public IMembershipFunction moderate;
        public IMembershipFunction far;
    }
    private DistToTargetMF distToTargetMF;

    struct HealthMF
    {
        public IMembershipFunction low;
        public IMembershipFunction moderate;
        public IMembershipFunction high;
    }
    private HealthMF healthMF;

    struct TargetsHealthMF
    {
        public IMembershipFunction low;
        public IMembershipFunction moderate;
        public IMembershipFunction high;
    }
    private TargetsHealthMF targetsHealthMF;

    struct ActionToTakeMF
    {
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
        SHOOT_TARGET = 1,
        HIDE = 2,
        MOVE_TO_TARGET = 3,
        DEAD = 4,
        NUM_STATES = 5
    }

    // Public variables
    public FuzzyAgentState State { get; private set; } = FuzzyAgentState.IDLE;
    public int CurrentHealth { get { return currentHealth; } }

    // Local variables
    private int currentHealth = 0;
    private int targetsCurrentHealth = 0;
    private List<FuzzyRule> fuzzyRules = new List<FuzzyRule>();
    private float elapsedMuzzleFlashTime = 0f;

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
            distToTargetMF.close = distToTarget.MembershipFunctions.AddTrapezoid("close", -50, -50, -5, -1);
            distToTargetMF.moderate = distToTarget.MembershipFunctions.AddTrapezoid("moderate", -50, -50, -5, -1);
            distToTargetMF.far = distToTarget.MembershipFunctions.AddTrapezoid("far", -50, -50, -5, -1);

            // Health
            healthMF.low = distToTarget.MembershipFunctions.AddTrapezoid("low", -50, -50, -5, -1);
            healthMF.moderate = distToTarget.MembershipFunctions.AddTrapezoid("moderate", -50, -50, -5, -1);
            healthMF.high = distToTarget.MembershipFunctions.AddTrapezoid("high", -50, -50, -5, -1);

            // Targets Health
            targetsHealthMF.low = distToTarget.MembershipFunctions.AddTrapezoid("low", -50, -50, -5, -1);
            targetsHealthMF.moderate = distToTarget.MembershipFunctions.AddTrapezoid("moderate", -50, -50, -5, -1);
            targetsHealthMF.high = distToTarget.MembershipFunctions.AddTrapezoid("high", -50, -50, -5, -1);

            // Action To Take
            actionToTakeMF.shootTarget = distToTarget.MembershipFunctions.AddTriangle("shootTarget", -50, -50, -5);
            actionToTakeMF.hideFromTarget = distToTarget.MembershipFunctions.AddTriangle("hideFromTarget", -50, -50, -5);
            actionToTakeMF.moveCloserToTarget = distToTarget.MembershipFunctions.AddTriangle("moveCloserToTarget", -50, -50, -5);
        }

        // Setup the rules for the fuzzy engine
        {
            fuzzyRules.Add(Rule.If(distToTarget.Is(distToTargetMF.close)).Then(actionToTake.Is(actionToTakeMF.shootTarget)));
            
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
            //if (Input.GetKeyDown(KeyCode.G))
            //{
            //    TakeDamage(10);
            //}
        } 

        // Change what gets updated depending on the current state
        switch (State)
        {
            case FuzzyAgentState.IDLE:
                UpdateIdleState();
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
                animator.SetTrigger("Dead");
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

        // If the targets health is not zero then do something
        if (targetsCurrentHealth > 0)
        {
            // State transitions
            {
                // Get the results from the fuzzy engine
                //double result = fuzzyEngine.Defuzzify(new { distToTarget = (double)Vector3.Distance(transform.position, target.position), health = (double)currentHealth, targetsHealth = (double)targetsCurrentHealth});

                //Debug.Log("Result: " + result);
            }
        }
    }

    private void UpdateShootTargetState()
    {
        // Rotate the agent to face the target
        Vector3 dirToTarget = Vector3.Normalize(target.position - transform.position);
        transform.rotation = Quaternion.LookRotation(dirToTarget);

        // Time between shots
        if (elapsedMuzzleFlashTime <= Time.time)
        {
            elapsedMuzzleFlashTime = Time.time + 0.2f;
            muzzleFlashParticle.Play();

            // Generate a random number which will be the accuracy of the shot and damage the target if it hits
            int randomNum = Random.Range(1, 11);
            if (randomNum <= 5)
            {
                target.GetComponent<FuzzyAgentController>().TakeDamage(10);
            }
        }
    }

    private void UpdateHideState()
    {
        // Find closest hiding spot that the target can't see
        Transform closestAvailableHidingSpot = null;
        foreach (Transform t in hidingSpotsTransform)
        {
            if (!CanTargetSeePosition(t.position))
            {
                if (closestAvailableHidingSpot == null)
                {
                    closestAvailableHidingSpot = t;
                }

                if (Vector3.Distance(transform.position, closestAvailableHidingSpot.position) >= Vector3.Distance(transform.position, t.position))
                {
                    closestAvailableHidingSpot = t;
                }
            }
        }

        // If there is a hiding spot available go to it
        if (closestAvailableHidingSpot != null)
        {
            agent.SetDestination(closestAvailableHidingSpot.position);
        }
        else
        {
            Debug.Log("Closest available spot is null");
        }
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

    private bool CanSeeTarget()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, target.position - transform.position, out hit))
        {
            if (hit.transform.CompareTag("FuzzyAgent"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }

    private bool CanTargetSeePosition(Vector3 position)
    {
        RaycastHit hit;
        if (Physics.Raycast(target.position, position - target.position, out hit))
        {
            if (hit.transform.CompareTag("HidingSpot"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }
        else
        {
            return false;
        }
    }
}
