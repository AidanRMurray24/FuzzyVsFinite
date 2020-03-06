using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FLS;
using FLS.Rules;
using FLS.MembershipFunctions;
using TMPro;

public class FuzzyAgentController : MonoBehaviour
{
    // Editor accessable variables
    [SerializeField] private int maxHealth = 100;
    [SerializeField] Transform target = null;
    [SerializeField] private HealthBar healthBar = null;
    [SerializeField] private Transform healthBarPos = null;
    [SerializeField] private GameObject stateTextObject = null;
    [SerializeField] private Transform hidingSpotsTransform = null;
    [SerializeField] private ParticleSystem muzzleFlashParticle = null;
    [SerializeField] private LayerMask viewMask = 0;
    [SerializeField] private float reloadTime = 2f;
    [SerializeField] private int ammoPerClip = 10;
    [SerializeField] private int bulletDamage = 8;
    [SerializeField] private Transform spawnPos = null;

    #region Linguistic variables
    private LinguisticVariable distToTarget = new LinguisticVariable("distToTarget");
    private LinguisticVariable health = new LinguisticVariable("health");
    private LinguisticVariable targetsHealth = new LinguisticVariable("targetsHealth");
    private LinguisticVariable actionToTake = new LinguisticVariable("actionToTake");
    private LinguisticVariable ammo = new LinguisticVariable("ammo");
    private LinguisticVariable canSeeTarget = new LinguisticVariable("canSeeTarget");
    private LinguisticVariable isFinishedReloading = new LinguisticVariable("isFinishedReloading");
    private LinguisticVariable isAtHidingSpot = new LinguisticVariable("isAtHidingSpot");
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

    struct IsAtHidingSpot
    {
        public IMembershipFunction there;
        public IMembershipFunction notThere;
    }
    private IsAtHidingSpot isAtHidingSpotMF;

	#endregion


	// Components
	private NavMeshAgent agent = null;
    private Animator animator = null;
    private Rigidbody rb = null;
    private Camera mainCamera = null;
    private IFuzzyEngine fuzzyEngine = null;
    private TextMeshProUGUI stateText = null;

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
    private List<FuzzyRule> idleRules = new List<FuzzyRule>();
    private List<FuzzyRule> shootingRules = new List<FuzzyRule>();
    private List<FuzzyRule> hidingRules = new List<FuzzyRule>();
    private List<FuzzyRule> moveToTargetRules = new List<FuzzyRule>();
    private List<FuzzyRule> reloadRules = new List<FuzzyRule>();
    private float elapsedMuzzleFlashTime = 0f;
    private bool stateRulesSet = false;
    private float elapsedReloadTime = 0f;
    private int ammoLeftInClip = 0;
    private bool finishedReloading = true;
    private bool atHidingSpot = false;

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

        // Get the state text component
        if (stateTextObject != null)
        {
            stateText = stateTextObject.GetComponent<TextMeshProUGUI>();
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
            ammoMF.low = ammo.MembershipFunctions.AddTrapezoid("low", 0, 0, ammoPerClip * .15f, ammoPerClip * .4f);
            ammoMF.moderate = ammo.MembershipFunctions.AddTrapezoid("moderate", ammoPerClip * .2f, ammoPerClip * .4f, ammoPerClip * .6f, ammoPerClip * .8f);
            ammoMF.high = ammo.MembershipFunctions.AddTrapezoid("high", ammoPerClip * .6f, ammoPerClip * .85f, ammoPerClip, ammoPerClip);

            // Can See Target
            canSeeTargetMF.can = canSeeTarget.MembershipFunctions.AddTrapezoid("can", 0, 0, 1, 1);
            canSeeTargetMF.cant = canSeeTarget.MembershipFunctions.AddTrapezoid("cant", 1, 1, 2, 2);

            // If finished reloading
            isFinishedReloadingMF.finished = isFinishedReloading.MembershipFunctions.AddTrapezoid("finished", 0, 0, 1, 1);
            isFinishedReloadingMF.notFinished = isFinishedReloading.MembershipFunctions.AddTrapezoid("notFinished", 1, 1, 2, 2);

            // If at hiding spot
            isAtHidingSpotMF.there = isAtHidingSpot.MembershipFunctions.AddTrapezoid("there", 0, 0, 1, 1);
            isAtHidingSpotMF.notThere = isAtHidingSpot.MembershipFunctions.AddTrapezoid("notThere", 1, 1, 2, 2);

            // Action To Take
            actionToTakeMF.shootTarget = actionToTake.MembershipFunctions.AddTriangle("shootTarget", 0, 0.5f, 1);
            actionToTakeMF.hideFromTarget = actionToTake.MembershipFunctions.AddTriangle("hideFromTarget", 1, 1.5f, 2);
            actionToTakeMF.moveCloserToTarget = actionToTake.MembershipFunctions.AddTriangle("moveCloserToTarget", 2, 2.5f, 3);
            actionToTakeMF.reload = actionToTake.MembershipFunctions.AddTriangle("reload", 3, 3.5f, 4);
        }
        #endregion

        #region Setup rules for each state

        // Idle rules
        {
            // If the target is within shooting range and can see the target, go to the shooting state
            idleRules.Add(Rule.If(distToTarget.IsNot(distToTargetMF.far).And(canSeeTarget.Is(canSeeTargetMF.can))).Then(actionToTake.Is(actionToTakeMF.shootTarget)));

            // If can't see the target or too far away from the target, go to the move state
            idleRules.Add(Rule.If(distToTarget.Is(distToTargetMF.far).Or(canSeeTarget.Is(canSeeTargetMF.cant))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

            // If current health is low and the target isn't close, go to the hiding state
            idleRules.Add(Rule.If(health.Is(healthMF.low).And(distToTarget.IsNot(distToTargetMF.close))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));
        }

        // Shooting rules
        {
            // If target is too far away or can't see target, go to the move to target state
            shootingRules.Add(Rule.If(distToTarget.Is(distToTargetMF.far).Or(canSeeTarget.Is(canSeeTargetMF.cant))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

            // If current health is low and target isn't close, go to the hiding state
            shootingRules.Add(Rule.If(health.Is(healthMF.low).And(distToTarget.IsNot(distToTargetMF.close))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));

            // If ammo is low and can see target, go to the hiding state
            shootingRules.Add(Rule.If(ammo.Is(ammoMF.low).And(canSeeTarget.Is(canSeeTargetMF.can))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));

            // If ammo is low and can't see target, go to reload state
            shootingRules.Add(Rule.If(ammo.Is(ammoMF.low).And(canSeeTarget.Is(canSeeTargetMF.cant))).Then(actionToTake.Is(actionToTakeMF.reload)));

            // If ammo is low and dist to target is far, go to reload state
            shootingRules.Add(Rule.If(ammo.Is(ammoMF.low).And(distToTarget.Is(distToTargetMF.far))).Then(actionToTake.Is(actionToTakeMF.reload)));
        }

        // Hiding rules
        {
            // If target's health is low and target is not close and emmo is not low, go to the move to target state
            hidingRules.Add(Rule.If(targetsHealth.Is(targetsHealthMF.low).And(distToTarget.IsNot(distToTargetMF.close)).And(ammo.IsNot(ammoMF.low))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

            // If target is close and can see target and ammo is not low, go to the shooting state
            hidingRules.Add(Rule.If(distToTarget.Is(distToTargetMF.close).And(canSeeTarget.Is(canSeeTargetMF.can)).And(ammo.IsNot(ammoMF.low))).Then(actionToTake.Is(actionToTakeMF.shootTarget)));

            // If ammo is low or empty and can't see target, go to reload state
            hidingRules.Add(Rule.If(ammo.Is(ammoMF.low).And(canSeeTarget.Is(canSeeTargetMF.cant)).And(isAtHidingSpot.Is(isAtHidingSpotMF.there))).Then(actionToTake.Is(actionToTakeMF.reload)));
        }

        // Move to target rules
        {
            // If the target is within shooting distance and can see the target, go to the shooting state
            moveToTargetRules.Add(Rule.If(distToTarget.IsNot(distToTargetMF.far).And(canSeeTarget.Is(canSeeTargetMF.can))).Then(actionToTake.Is(actionToTakeMF.shootTarget)));

            // If current health is low and target isn't close and targets health is not low, go to hiding state
            moveToTargetRules.Add(Rule.If(health.Is(healthMF.low).And(distToTarget.IsNot(distToTargetMF.close)).And(targetsHealth.IsNot(targetsHealthMF.low))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));
        }

        // Reloading Rules
        {
            // If finished reloading and dist to target is far then go to the move to target state
            reloadRules.Add(Rule.If(isFinishedReloading.Is(isFinishedReloadingMF.finished).And(distToTarget.Is(distToTargetMF.far))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

            // If finished reloading and can't see target then go to the move to target state
            reloadRules.Add(Rule.If(isFinishedReloading.Is(isFinishedReloadingMF.finished).And(canSeeTarget.Is(canSeeTargetMF.cant))).Then(actionToTake.Is(actionToTakeMF.moveCloserToTarget)));

            // If finished reloading and distance to target isn't far and can see target, go to the shooting state
            reloadRules.Add(Rule.If(isFinishedReloading.Is(isFinishedReloadingMF.finished).And(distToTarget.IsNot(distToTargetMF.far)).And(canSeeTarget.Is(canSeeTargetMF.can))).Then(actionToTake.Is(actionToTakeMF.shootTarget)));

            // If can see target and not finished reloading, go to the hiding state
            reloadRules.Add(Rule.If(canSeeTarget.Is(canSeeTargetMF.can).And(isFinishedReloading.Is(isFinishedReloadingMF.notFinished))).Then(actionToTake.Is(actionToTakeMF.hideFromTarget)));
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
                stateText.text = "IDLE";
                agent.SetDestination(transform.position);
                break;
            case FuzzyAgentState.SHOOT_TARGET:
                UpdateShootTargetState();
                stateText.text = "SHOOTING";
                agent.SetDestination(transform.position);
                break;
            case FuzzyAgentState.HIDE:
                UpdateHideState();
                stateText.text = "HIDING";
                break;
            case FuzzyAgentState.MOVE_TO_TARGET:
                UpdateMoveToTargetState();
                stateText.text = "MOVING TO TARGET";
                break;
            case FuzzyAgentState.RELOAD:
                UpdateReloadState();
                stateText.text = "RELOADING";
                agent.SetDestination(transform.position);
                break;
            case FuzzyAgentState.DEAD:
                animator.SetTrigger("Dead");
                stateText.text = "DEAD";
                agent.SetDestination(transform.position);
                break;
            default:
                Debug.Log("No state has been set!");
                stateText.text = "ERROR";
                break;
        }

        // Update the animator's speed variable to be the current velocity in the forward vector
        animator.SetFloat("Speed", Vector3.Dot(agent.velocity, transform.forward));

        // Update the position of the health bar only if the camera is looking at the agent
        if (Vector3.Dot(transform.position - mainCamera.transform.position, mainCamera.transform.forward) >= 0)
        {
            healthBar.transform.position = mainCamera.WorldToScreenPoint(healthBarPos.position);
            stateTextObject.transform.position = healthBar.transform.position + new Vector3(0, 30, 0);
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
        if (elapsedMuzzleFlashTime <= Time.time && ammoLeftInClip > 0)
        {
            elapsedMuzzleFlashTime = Time.time + 0.2f;
            muzzleFlashParticle.Play();
            ammoLeftInClip--;

            // Generate a random number which will be the accuracy of the shot and damage the target if it hits
            int randomNum = Random.Range(1, 11);
            if (randomNum <= 5)
            {
                target.GetComponent<FiniteAgentController>().TakeDamage(bulletDamage);
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

            // If the agent is close enough to the hiding spot then the agent is at the hiding spot
            if (Vector3.Distance(transform.position, closestAvailableHidingSpot.position) < 1f)
            {
                atHidingSpot = true;
            }
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
        // Count done until done reloading
        if (elapsedReloadTime > 0)
        {
            finishedReloading = false;
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
        if (currentHealth <= 0 && State != FuzzyAgentState.DEAD)
        {
            currentHealth = 0;
            State = FuzzyAgentState.DEAD;
            stateRulesSet = false;
            GameManager.instance.AddPoint(GameManager.Agent.FSM);
            agent.SetDestination(transform.position);
        }

        // Update the health bar
        healthBar.SetHealth(currentHealth);
    }

    private bool CanSeeTarget()
    {
        if (!Physics.Linecast(transform.position + new Vector3(0, 2, 0), target.position + new Vector3(0, 2, 0), viewMask))
        {
            Debug.DrawLine(transform.position + new Vector3(0, 2, 0), target.position + new Vector3(0, 2, 0));
            return true;
        }
        else
        {
            return false;
        }
    }

    private bool CanTargetSeePosition(Vector3 position)
    {
        if (!Physics.Linecast(target.position + new Vector3(0, 2, 0), position, viewMask))
        {
            Debug.DrawLine(target.position + new Vector3(0, 2, 0), position);
            return true;
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
                        //Debug.Log("Idle rules set");
                        ChangeRules(idleRules);
                    }
                    break;
                case FuzzyAgentState.SHOOT_TARGET:
                    {
                        //Debug.Log("Shooting rules set!");
                        ChangeRules(shootingRules);
                    }
                    break;
                case FuzzyAgentState.HIDE:
                    {
                        //Debug.Log("Hiding rules set!");
                        ChangeRules(hidingRules);
                    }
                    break;
                case FuzzyAgentState.MOVE_TO_TARGET:
                    {
                        //Debug.Log("Moving rules set!");
                        ChangeRules(moveToTargetRules);
                    }
                    break;
                case FuzzyAgentState.RELOAD:
                    {
                        //Debug.Log("Reload rules set!");
                        ChangeRules(reloadRules);
                    }
                    break;
                default:
                    break;
            }
			#endregion

            // Set the destination to be where the agent already is to avoid the agent running off while in a state where they should not move
            agent.SetDestination(transform.position);
            atHidingSpot = false;
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

        float isAtHidingSpotValue = 0f;
        if (atHidingSpot)
            isAtHidingSpotValue = .5f;
        else
            isAtHidingSpotValue = 1.5f;

        double result = fuzzyEngine.Defuzzify(new 
        { 
            distToTarget = (double)Vector3.Distance(transform.position,target.position),
            health = (double)currentHealth,
            targetsHealth = (double)targetsCurrentHealth,
            ammo = (double)ammoLeftInClip,
            canSeeTarget = (double)canSeeTargetValue,
            isFinishedReloading = (double)isFinishedReloadingValue,
            isAtHidingSpot = (double)isAtHidingSpotValue
        });

        // Check what the result was and if it is valid
        if (result > 0 && result <= 1)
        {
            State = FuzzyAgentState.SHOOT_TARGET;
            stateRulesSet = false;
        }
        else if (result > 1 && result <= 2)
        {
            State = FuzzyAgentState.HIDE;
            stateRulesSet = false;
        }
        else if (result > 2 && result <= 3)
        {
            State = FuzzyAgentState.MOVE_TO_TARGET;
            stateRulesSet = false;
        }
        else if (result > 3 && result <= 4)
        {
            State = FuzzyAgentState.RELOAD;
            elapsedReloadTime = reloadTime;
            stateRulesSet = false;
        }
        

        // Crisp state changes
        if (targetsCurrentHealth <= 0)
        {
            State = FuzzyAgentState.IDLE;
            stateRulesSet = false;
        }
    }

    private void ChangeRules(List<FuzzyRule> rules)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            fuzzyEngine.Rules.Add(rules[i]);
        }
    }

    public void Reset()
    {
        // Set the spawn position
        transform.position = spawnPos.position;

        // Set the state
        State = FuzzyAgentState.IDLE;

        // Set the current halth to be the max at the start of the round
        currentHealth = maxHealth;

        // Set the agent to have full ammo at the start of the round
        ammoLeftInClip = ammoPerClip;

        // Setup the health bar
        healthBar.SetMaxHealth(maxHealth);
    }
}
