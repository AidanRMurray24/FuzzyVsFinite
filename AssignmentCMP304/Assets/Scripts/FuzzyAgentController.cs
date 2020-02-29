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
    [SerializeField] private GameObject reloadingText = null;
    [SerializeField] private Transform hidingSpotsTransform = null;
    [SerializeField] private ParticleSystem muzzleFlashParticle = null;
    [SerializeField] private float reloadTime = 2f;
    [SerializeField] private int ammoPerClip = 10;

    #region Linguistic variables
    private LinguisticVariable distToTarget = new LinguisticVariable("distToTarget");
    private LinguisticVariable health = new LinguisticVariable("health");
    private LinguisticVariable targetsHealth = new LinguisticVariable("targetsHealth");
    private LinguisticVariable actionToTake = new LinguisticVariable("actionToTake");
    private LinguisticVariable ammo = new LinguisticVariable("ammo");
    private LinguisticVariable canSeeTarget = new LinguisticVariable("canSeeTarget");
    private LinguisticVariable isFinishedReloading = new LinguisticVariable("isFinishedReloading");
	#endregion

	#region Membership functions

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
        public IMembershipFunction reload;
    }
    private ActionToTakeMF actionToTakeMF;

    struct AmmoMF
    {
        public IMembershipFunction low;
        public IMembershipFunction moderate;
        public IMembershipFunction high;
    }
    AmmoMF ammoMF;
    
    struct CanSeeTargetMF
    {
        public IMembershipFunction can;
        public IMembershipFunction cant;
    }
    private CanSeeTargetMF canSeeTargetMF;

    struct IsFinishedReloadingMF
    {
        public IMembershipFunction finished;
        public IMembershipFunction notFinished;
    }
    private IsFinishedReloadingMF isFinishedReloadingMF;

	#endregion


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
        RELOAD = 5,
        NUM_STATES = 6
    }

    // Public variables
    public FuzzyAgentState State { get; private set; } = FuzzyAgentState.IDLE;
    public int CurrentHealth { get { return currentHealth; } }

    // Local variables
    private int currentHealth = 0;
    private int targetsCurrentHealth = 0;
    private List<FuzzyRule> fuzzyRules = new List<FuzzyRule>();
    private float elapsedMuzzleFlashTime = 0f;
    private bool stateRulesSet = false;
    private float elapsedReloadTime = 0f;
    private int ammoLeftInClip = 0;
    private bool finishedReloading = true;

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

        // Set the agent to have full ammo at the start of the round
        ammoLeftInClip = ammoPerClip;

        // Setup the health bar
        healthBar.SetMaxHealth(maxHealth);

		#region Setup the membership functions for the linguistic variables
		{
			if (distToTarget == null)
            {
                Debug.Log("distToTarget is null");
            }

            // Dist To Target
            distToTargetMF.close = distToTarget.MembershipFunctions.AddTrapezoid("close", 0, 0, 3, 7);
            distToTargetMF.moderate = distToTarget.MembershipFunctions.AddTrapezoid("moderate", 3, 7, 10, 16);
            distToTargetMF.far = distToTarget.MembershipFunctions.AddTrapezoid("far", 12, 16, 20, 20);

            // Health
            healthMF.low = health.MembershipFunctions.AddTrapezoid("low", 0, 0, 20, 47);
            healthMF.moderate = health.MembershipFunctions.AddTrapezoid("moderate", 20, 40, 60, 80);
            healthMF.high = health.MembershipFunctions.AddTrapezoid("high", 53, 80, 100, 100);

            // Targets Health
            targetsHealthMF.low = targetsHealth.MembershipFunctions.AddTrapezoid("low", 0, 0, 20, 47);
            targetsHealthMF.moderate = targetsHealth.MembershipFunctions.AddTrapezoid("moderate", 20, 40, 60, 80);
            targetsHealthMF.high = targetsHealth.MembershipFunctions.AddTrapezoid("high", 53, 80, 100, 100);

            // Ammo left
            ammoMF.low = ammo.MembershipFunctions.AddTrapezoid("low", 0, 0, 1, 1);
            ammoMF.moderate = ammo.MembershipFunctions.AddTrapezoid("moderate", 0, 0, 1, 1);
            ammoMF.high = ammo.MembershipFunctions.AddTrapezoid("high", 0, 0, 1, 1);

            // Can See Target
            canSeeTargetMF.can = canSeeTarget.MembershipFunctions.AddTrapezoid("can", 0, 0, 1, 1);
            canSeeTargetMF.cant = canSeeTarget.MembershipFunctions.AddTrapezoid("cant", 1, 1, 2, 2);

            // If finished reloading
            isFinishedReloadingMF.finished = isFinishedReloading.MembershipFunctions.AddTrapezoid("finished", 0, 0, 1, 1);
            isFinishedReloadingMF.notFinished = isFinishedReloading.MembershipFunctions.AddTrapezoid("notFinished", 1, 1, 2, 2);

            // Action To Take
            actionToTakeMF.shootTarget = actionToTake.MembershipFunctions.AddTriangle("shootTarget", 0, 0.5f, 1);
            actionToTakeMF.hideFromTarget = actionToTake.MembershipFunctions.AddTriangle("hideFromTarget", 1, 1.5f, 2);
            actionToTakeMF.moveCloserToTarget = actionToTake.MembershipFunctions.AddTriangle("moveCloserToTarget", 2, 2.5f, 3);
            actionToTakeMF.reload = actionToTake.MembershipFunctions.AddTriangle("reload", 3, 3.5f, 4);
        }
		#endregion
	}

	// Update is called once per frame
	private void Update()
    {
        // Change what gets updated depending on the current state
        switch (State)
        {
            case FuzzyAgentState.IDLE:
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
            case FuzzyAgentState.RELOAD:
                UpdateReloadState();
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
            reloadingText.transform.position = healthBar.transform.position + new Vector3(0, 30, 0);

            if (State == FuzzyAgentState.RELOAD)
                reloadingText.SetActive(true);
            else
                reloadingText.SetActive(false);
        }

        // Get the targets current health
        targetsCurrentHealth = target.GetComponent<FiniteAgentController>().CurrentHealth;

        // Check if the state should be changed and charge the rules depending on which state is active ( If the state is already at dead, no need to change)
        if (State != FuzzyAgentState.DEAD)
        {
            CheckForStateChange();
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
                target.GetComponent<FiniteAgentController>().TakeDamage(10);
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

    private void UpdateReloadState()
    {
        // Start the reload timer
        if (finishedReloading)
        {
            finishedReloading = false;
            elapsedReloadTime = reloadTime;
        }

        // Count done until done reloading
        if (elapsedReloadTime > 0)
        {
            elapsedReloadTime -= Time.deltaTime;
            if (elapsedReloadTime <= 0)
            {
                finishedReloading = true;
                ammoLeftInClip = ammoPerClip;
            }
        }
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
            if (hit.transform.CompareTag("FiniteAgent"))
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

    private void CheckForStateChange()
    {
        // Check if the rules have been set or not for the current state
        if (!stateRulesSet)
        {
            stateRulesSet = true;

            // Clear the rules on the fuzzy engine before adding new ones
            fuzzyEngine.Rules.Clear();
            fuzzyRules.Clear();

            #region Depending on which state the agent is currently in, apply different rules
            switch (State)
            {
                case FuzzyAgentState.IDLE:
                    {
                        Debug.Log("Idle rules set!");
                        // If the target is within shooting range and can see the target, go to the shooting state
                        fuzzyRules.Add(Rule.If(distToTarget.IsNot(distToTargetMF.far).And(canSeeTarget.Is(canSeeTargetMF.can))).Then(actionToTake.Is(actionToTakeMF.shootTarget)));

                        // If can't see the target or too far away from the target, go to the move state
                        fuzzyRules.Add(Rule.If(distToTarget.Is(distToTargetMF.far).Or(canSeeTarget.Is(canSeeTargetMF.cant))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

                        // If current health is low and the target isn't close, go to the hiding state
                        fuzzyRules.Add(Rule.If(health.Is(healthMF.low).And(distToTarget.IsNot(distToTargetMF.close))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));
                    }
                    break;
                case FuzzyAgentState.SHOOT_TARGET:
                    {
                        Debug.Log("Shooting rules set!");
                        // If target is too far away or can't see target, go to the move to target state
                        fuzzyRules.Add(Rule.If(distToTarget.Is(distToTargetMF.far).Or(canSeeTarget.Is(canSeeTargetMF.cant))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

                        // If current health is low and target isn't close, go to the hiding state
                        fuzzyRules.Add(Rule.If(health.Is(healthMF.low).And(distToTarget.IsNot(distToTargetMF.close))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));

                        // If ammo is low or empty and can't see target or dist to target is far, go to reload state
                        fuzzyRules.Add(Rule.If(ammo.Is(ammoMF.low).And(canSeeTarget.Is(canSeeTargetMF.cant)).Or(distToTarget.Is(distToTargetMF.far))).Then(actionToTake.Is(actionToTakeMF.reload)));
                    }
                    break;
                case FuzzyAgentState.HIDE:
                    {
                        Debug.Log("Hiding rules set!");
                        // If target's health is low and target is not close, go to the move to target state
                        fuzzyRules.Add(Rule.If(targetsHealth.Is(targetsHealthMF.low).And(distToTarget.IsNot(distToTargetMF.close))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

                        // If target is close and can see target go to the shooting state
                        fuzzyRules.Add(Rule.If(distToTarget.Is(distToTargetMF.close).And(canSeeTarget.Is(canSeeTargetMF.can))).Then(actionToTake.Is(actionToTakeMF.shootTarget)));

                        // If ammo is low or empty and can't see target or dist to target is far, go to reload state
                        fuzzyRules.Add(Rule.If(ammo.Is(ammoMF.low).And(canSeeTarget.Is(canSeeTargetMF.cant)).Or(distToTarget.Is(distToTargetMF.far))).Then(actionToTake.Is(actionToTakeMF.reload)));
                    }
                    break;
                case FuzzyAgentState.MOVE_TO_TARGET:
                    {
                        Debug.Log("Moving rules set!");
                        // If the target is within shooting distance and can see the target, go to the shooting state
                        fuzzyRules.Add(Rule.If(distToTarget.IsNot(distToTargetMF.far).And(canSeeTarget.Is(canSeeTargetMF.can))).Then(actionToTake.Is(actionToTakeMF.shootTarget)));

                        // If current health is low and target isn't close, go to hiding state
                        fuzzyRules.Add(Rule.If(health.Is(healthMF.low).And(distToTarget.IsNot(distToTargetMF.close))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));
                    }
                    break;
                case FuzzyAgentState.RELOAD:
                    {
                        Debug.Log("Reload rules set!");
                        // If finished reloading and dist to target is far then go to the move to target state
                        fuzzyRules.Add(Rule.If(distToTarget.Is(distToTargetMF.far).Or(canSeeTarget.Is(canSeeTargetMF.cant))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

                        // If finished reloading and distance to target isn't far and can see target, go to the shooting state
                        fuzzyRules.Add(Rule.If(distToTarget.IsNot(distToTargetMF.far).And(canSeeTarget.Is(canSeeTargetMF.can))).Then(actionToTake.Is(actionToTakeMF.shootTarget)));

                        // If can see target and current health is low or not finished reloading, go to the hiding state
                        fuzzyRules.Add(Rule.If(canSeeTarget.Is(canSeeTargetMF.can).And(health.Is(healthMF.low))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));
                    }
                    break;
                default:
                    break;
            }
			#endregion

			// Add the rules to the engine
			for (int i = 0; i < fuzzyRules.Count; i++)
            {
                fuzzyEngine.Rules.Add(fuzzyRules[i]);
            }

            // Set the destination to be where the agent already is to avoid the agent running off while in a state where they should not move
            agent.SetDestination(transform.position);
        }

        // Get the results from the fuzzy engine
        float canSeeTargetValue = 0f;
        if (CanSeeTarget())
            canSeeTargetValue = .5f;
        else
            canSeeTargetValue = 1.5f;

        float isFinishedReloadingValue = 0f;
        if (finishedReloading)
            isFinishedReloadingValue = .5f;
        else
            isFinishedReloadingValue = 1.5f;

        double result = fuzzyEngine.Defuzzify(new 
        { 
            distToTarget = (double)Vector3.Distance(transform.position,target.position),
            health = (double)currentHealth,
            targetsHealth = (double)targetsCurrentHealth,
            ammo = (double)ammoLeftInClip,
            canSeeTarget = (double)canSeeTargetValue,
            isFinishedReloading = (double)isFinishedReloadingValue 
        });

        Debug.Log("Result: " + result);
        Debug.Log("No Of Rules: " + fuzzyEngine.Rules.Count);
        Debug.Log("Can see target: " + canSeeTargetValue);

        // Check what the result was and if it is valid
        if (result >= 0 && result < 1)
        {
            State = FuzzyAgentState.SHOOT_TARGET;
            stateRulesSet = false;
        }
        else if (result >= 1 && result < 2)
        {
            State = FuzzyAgentState.HIDE;
            stateRulesSet = false;
        }
        else if (result >= 2 && result < 3)
        {
            State = FuzzyAgentState.MOVE_TO_TARGET;
            stateRulesSet = false;
        }
        else if (result >= 3 && result < 4)
        {
            State = FuzzyAgentState.RELOAD;
            stateRulesSet = false;
        }
        

        // Crisp state changes
        if (targetsCurrentHealth <= 0)
        {
            State = FuzzyAgentState.IDLE;
            stateRulesSet = false;
        }
        if (currentHealth <= 0)
        {
            State = FuzzyAgentState.DEAD;
            stateRulesSet = false;
        }
    }
}
