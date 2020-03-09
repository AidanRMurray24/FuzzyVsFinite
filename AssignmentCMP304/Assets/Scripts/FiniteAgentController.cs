using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using TMPro;
using UnityEngine.Profiling;

public class FiniteAgentController : MonoBehaviour
{
    // Editor accessable variables
    [SerializeField] private Transform target = null;
    [SerializeField] private HealthBar healthBar = null;
    [SerializeField] private Transform healthBarPos = null;
    [SerializeField] private GameObject stateTextObject = null;
    [SerializeField] private ParticleSystem muzzleFlashParticle = null;
    [SerializeField] private Transform hidingSpotsTransform = null;
    [SerializeField] private Transform spawnPos = null;
    [SerializeField] private LayerMask viewMask = 0;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float distToStartShooting = 0f;
    [SerializeField] private float distToHide = 0f;
    [SerializeField] private int lowHealth = 30;
    [SerializeField] private int lowAmmo = 3;
    [SerializeField] private float reloadTime = 3f;
    [SerializeField] private int ammoPerClip = 10;
    [SerializeField] private int bulletDamage = 8;

    // Components
    private NavMeshAgent agent = null;
    private Animator animator = null;
    private Rigidbody rb = null;
    private Camera mainCamera = null;
    private TextMeshProUGUI stateText = null;

    // State enum
    public enum FiniteAgentState
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
    public FiniteAgentState State { get; private set; } = FiniteAgentState.IDLE;
    public int CurrentHealth { get { return currentHealth; } }
    public int BulletsFired { get; private set; } = 0;
    public int BulletsHit { get; private set; } = 0;
    public int[] StateChanges { get; private set; } = new int[6] { 0, 0, 0, 0, 0, 0 };

    // Local variables
    private float distToTarget = 0f;
    private int currentHealth = 0;
    private int targetsHealth = 0;
    private float elapsedMuzzleFlashTime = 0f;
    private float elapsedReloadTime = 0f;
    private bool finishedReloading = true;
    private int ammoLeftInClip = 0;
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

        // Get the state text component
        if (stateTextObject != null)
        {
            stateText = stateTextObject.GetComponent<TextMeshProUGUI>();
        }
    }

    // Start is called before the first frame update
    private void Start()
    {
        // Set the current health to be the max at the start of the game
        currentHealth = maxHealth;

        // Set the agents ammo to full
        ammoLeftInClip = ammoPerClip;

        // Setup the health bar
        healthBar.SetMaxHealth(maxHealth);
    }

    // Update is called once per frame
    private void Update()
    {
        Profiler.BeginSample("Finite");

        // Caluculate the distance from the agent to the target
        distToTarget = Vector3.Distance(transform.position, target.position);

        // Get the targets current health
        targetsHealth = target.GetComponent<FuzzyAgentController>().CurrentHealth;

        // Change what gets updated depending on the current state
        switch (State)
        {
            case FiniteAgentState.IDLE:
                UpdateIdleState();
                break;
            case FiniteAgentState.SHOOT_TARGET:
                UpdateShootTargetState();
                break;
            case FiniteAgentState.HIDE:
                UpdateHideState();
                break;
            case FiniteAgentState.MOVE_TO_TARGET:
                UpdateMoveToTargetState();
                break;
            case FiniteAgentState.RELOAD:
                UpdateReloadState();
                break;
            case FiniteAgentState.DEAD:
                break;
            default:
                Debug.Log("No state has been set!");
                stateText.text = "ERROR";
                break;
        }

        // If the target's health is less than or equal to zero then the target is dead, go to the idle state
        if (targetsHealth <= 0)
        {
            ChangeState(FiniteAgentState.IDLE);
        }

        // Update the animator's speed variable to be the current velocity in the forward vector
        animator.SetFloat("Speed", Vector3.Dot(agent.velocity, transform.forward));

        // Update the position of the health bar only if the camera is looking at the agent
        if (Vector3.Dot(transform.position - mainCamera.transform.position, mainCamera.transform.forward) >= 0)
        {
            healthBar.transform.position = mainCamera.WorldToScreenPoint(healthBarPos.position);
            stateTextObject.transform.position = healthBar.transform.position + new Vector3(0, 30, 0);
        }

        Profiler.EndSample();
    }

    private void UpdateIdleState()
    {
        // If the targets health is not zero then do something
        if (targetsHealth > 0)
        {
            // State transitions
            {
                // If the agent can see the target and they are within shooting distance, go to the shooting state
                if (CanSeeTarget() && distToTarget <= distToStartShooting && ammoLeftInClip > 0)
                {
                    ChangeState(FiniteAgentState.SHOOT_TARGET);
                }

                // If the agent can't see the target or they are too far away from the target, go the the move to target state
                if (!CanSeeTarget() || distToTarget > distToStartShooting)
                {
                    ChangeState(FiniteAgentState.MOVE_TO_TARGET);
                }

                // If current health is low and the target isn't close, go to the hide state
                if (currentHealth <= lowHealth && distToTarget >= distToHide)
                {
                    ChangeState(FiniteAgentState.HIDE);
                }
            }
        }
    }

    private void UpdateShootTargetState()
    {
        // Rotate the agent to face the target
        Vector3 dirToTarget = Vector3.Normalize(target.position - transform.position);
        transform.rotation = Quaternion.LookRotation(dirToTarget);

        // Shooting
        if (elapsedMuzzleFlashTime <= Time.time && ammoLeftInClip > 0)
        {
            elapsedMuzzleFlashTime = Time.time + 0.2f;

            // Play the muzzle flash particles
            muzzleFlashParticle.Play();

            // Decrement the ammo left and increment the number of bullets fired
            ammoLeftInClip--;
            BulletsFired++;

            // Generate a random number which will be the accuracy of the shot and damage the target if it hits
            int randomNum = Random.Range(1, 11);
            if (randomNum <= 5)
            {
                target.GetComponent<FuzzyAgentController>().TakeDamage(bulletDamage);
                BulletsHit++;
            }
        }

        // State transitions
        {
            // If the agent can't see the target or they are too far away from the target, go the the move to target state or the reload state if they are low on ammo
            if (!CanSeeTarget() || distToTarget > distToStartShooting)
            {
                // If ammo is low, go to the reload state
                if (ammoLeftInClip <= lowAmmo)
                {
                    ChangeState(FiniteAgentState.RELOAD);
                }
                else
                {
                    ChangeState(FiniteAgentState.MOVE_TO_TARGET);
                }
            }

            // If current health is low and target isn't close and targets health is not low, go to hiding state
            if (currentHealth <= lowHealth && distToTarget >= distToHide && targetsHealth > lowHealth)
            {
                ChangeState(FiniteAgentState.HIDE);
            }

            // If ammo empty, go to the hiding state
            if (ammoLeftInClip <= 0)
            {
                ChangeState(FiniteAgentState.HIDE);
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

        // State transitions
        {
            // Check if the agent has ammo left
            if (ammoLeftInClip > 0)
            {
                if ((!CanSeeTarget() || distToTarget >= distToStartShooting) && targetsHealth <= lowHealth)
                {
                    ChangeState(FiniteAgentState.MOVE_TO_TARGET);
                }
                else if (distToTarget <= distToStartShooting)
                {
                    ChangeState(FiniteAgentState.SHOOT_TARGET);
                }
            }
            else if (atHidingSpot)
            {
                ChangeState(FiniteAgentState.RELOAD);  
            }
        }
    }

    private void UpdateMoveToTargetState()
    {
        // Walk towards the target
        agent.SetDestination(target.position);

        // State transitions
        {
            // If the target is within shooting distance, go to the shooting state
            if (distToTarget <= distToStartShooting && CanSeeTarget() && ammoLeftInClip > 0)
            {
                ChangeState(FiniteAgentState.SHOOT_TARGET);
            }

            // If current health is low and target isn't close and targets health is not low, go to hiding state
            if (currentHealth <= lowHealth && distToTarget >= distToHide && targetsHealth > lowHealth)
            {
                ChangeState(FiniteAgentState.HIDE);
            }
        }
    }

    private void UpdateReloadState()
    {
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

        // State transitions
        {
            // Check if the agent can see the target and they are within shooting range else if they have finished reloading, go to the move to target state
            if (CanSeeTarget() && distToTarget <= distToStartShooting)
            {
                // If they have finished reloading, go to the shooting state else go to the hiding state
                if (finishedReloading)
                {
                    ChangeState(FiniteAgentState.SHOOT_TARGET);  
                }
                else
                {
                    ChangeState(FiniteAgentState.HIDE);
                }
            }
            else if(finishedReloading)
            {
                ChangeState(FiniteAgentState.MOVE_TO_TARGET);
            }
        }
    }

    public void TakeDamage(int amount)
    {
        // Take the amount off of the current health
        currentHealth -= amount;

        // If current health is less than or equal to zero, go to the dead state
        if (currentHealth <= 0 && State != FiniteAgentState.DEAD)
        {
            currentHealth = 0;
            ChangeState(FiniteAgentState.DEAD);
            GameManager.instance.AddPoint(GameManager.Agent.FUSM);
        }

        // Update the health bar
        healthBar.SetHealth(currentHealth);
    }

    private bool CanSeeTarget()
    {
        // Draws a line from the agent to the target and returns true if nothing else was hit
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
        // Draws a line from the position to the target and returns true if nothing else was hit
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

    private void ChangeState(FiniteAgentState newState)
    {
        // Set the state to the new state and update the number of state changes
        State = newState;
        StateChanges[(int)newState]++;

        // Reset all values before going to the new state
        agent.SetDestination(transform.position);
        elapsedReloadTime = reloadTime;
        atHidingSpot = false;
        finishedReloading = false;

        // Change the text the appears above the agents head depending on the state
        switch (newState)
        {
            case FiniteAgentState.IDLE:
                stateText.text = "IDLE";
                break;
            case FiniteAgentState.SHOOT_TARGET:
                stateText.text = "SHOOTING";
                break;
            case FiniteAgentState.HIDE:
                stateText.text = "HIDING";
                break;
            case FiniteAgentState.MOVE_TO_TARGET:
                stateText.text = "MOVING TO TARGET";
                break;
            case FiniteAgentState.DEAD:
                animator.SetTrigger("Dead");
                stateText.text = "DEAD";
                break;
            case FiniteAgentState.RELOAD:
                stateText.text = "RELOADING";
                break;
            case FiniteAgentState.NUM_STATES:
                break;
            default:
                break;
        }
    }

    public void Reset()
    {
        // Set the spawn position
        transform.position = spawnPos.position;

        // Set the state
        ChangeState(FiniteAgentState.IDLE);

        // Set the current halth to be the max at the start of the round
        currentHealth = maxHealth;

        // Set the agent to have full ammo at the start of the round
        ammoLeftInClip = ammoPerClip;

        // Setup the health bar
        healthBar.SetMaxHealth(maxHealth);

        // Rest the animation
        animator.ResetTrigger("Dead");

        // Reset the bullets fired ans bullets hit
        BulletsFired = 0;
        BulletsHit = 0;

        // Reset the state changes
        for (int i = 0; i < StateChanges.Length; i++)
        {
            StateChanges[i] = 0;
        }
    }

}
