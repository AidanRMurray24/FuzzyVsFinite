using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class FiniteAgentController : MonoBehaviour
{
    // Editor accessable variables
    [SerializeField] private Transform target = null;
    [SerializeField] private HealthBar healthBar = null;
    [SerializeField] private Transform healthBarPos = null;
    [SerializeField] private ParticleSystem muzzleFlashParticle = null;
    [SerializeField] private Transform hidingSpotsTransform = null;
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private float distToStartShooting = 0f;
    [SerializeField] private float distToHide = 0f;
    [SerializeField] private int healthValueToRunAwayAt = 0;
    [SerializeField] private int lowHealth = 30;

    // Components
    private NavMeshAgent agent = null;
    private Animator animator = null;
    private Rigidbody rb = null;
    private Camera mainCamera = null;

    // State enum
    public enum FiniteAgentState
    {
        IDLE = 0,
        SHOOT_TARGET = 1,
        HIDE = 2,
        MOVE_TO_TARGET = 3,
        DEAD = 4,
        NUM_STATES = 5
    }

    // Public variables
    public FiniteAgentState State { get; private set; } = FiniteAgentState.IDLE;
    public int CurrentHealth { get { return currentHealth; } }

    // Local variables
    private float distToTarget = 0f;
    private int currentHealth = 0;
    private int targetsHealth = 0;
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
    }

    // Start is called before the first frame update
    private void Start()
    {
        // Set the current halth to be the max at the start of the game
        currentHealth = maxHealth;

        // Setup the health bar
        healthBar.SetMaxHealth(maxHealth);
    }

    // Update is called once per frame
    private void Update()
    {
        // TEST CODE
        {
            if (Input.GetKeyDown(KeyCode.G))
            {
                TakeDamage(10);
            }
        }

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
            case FiniteAgentState.DEAD:
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
    }

    private void UpdateIdleState()
    {
        // If the targets health is not zero then do something
        if (targetsHealth > 0)
        {
            // State transitions
            {
                // If the agent can see the target and they are within shooting distance, go to the shooting state
                if (CanSeeTarget() && distToTarget <= distToStartShooting)
                {
                    State = FiniteAgentState.SHOOT_TARGET;
                }

                // If the agent can't see the target or they are too far away from the target, go the the move to target state
                if (!CanSeeTarget() && distToTarget > distToStartShooting)
                {
                    State = FiniteAgentState.MOVE_TO_TARGET;
                }

                // If current health is low and the target isn't close, go to the hide state
                if (currentHealth <= lowHealth && distToTarget >= distToHide)
                {
                    State = FiniteAgentState.HIDE;
                }

                // If current health is less than or equal to zero then the agent is dead, go to the dead state
                if (currentHealth <= 0)
                {
                    State = FiniteAgentState.DEAD;
                }
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

        // State transitions
        {
            // If the agent can't see the target or they are too far away from the target, go the the move to target state
            if (!CanSeeTarget() && distToTarget > distToStartShooting)
            {
                State = FiniteAgentState.MOVE_TO_TARGET;
            }

            // If current health is low and the target isn't close, go to the hide state
            if (currentHealth <= lowHealth && distToTarget >= distToHide)
            {
                State = FiniteAgentState.HIDE;
            }

            // If the target's health is less than or equal to zero then the target is dead, go to the idle state
            if (targetsHealth <= 0)
            {
                State = FiniteAgentState.IDLE;
            }

            // If current health is less than or equal to zero then the agent is dead, go to the dead state
            if (currentHealth <= 0)
            {
                State = FiniteAgentState.DEAD;
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

        // State transitions
        {
            // If the target's health is low and target is not close, go to the move to target state
            if (targetsHealth <= lowHealth && distToTarget > distToHide)
            {
                State = FiniteAgentState.MOVE_TO_TARGET;
                agent.SetDestination(transform.position);
            }

            // If the target is close and can see target, go to the shooting state
            if (distToTarget <= distToHide && CanSeeTarget())
            {
                State = FiniteAgentState.SHOOT_TARGET;
                agent.SetDestination(transform.position);
            }

            // If current health is less than or equal to zero then the agent is dead, go to the dead state
            if (currentHealth <= 0)
            {
                State = FiniteAgentState.DEAD;
                agent.SetDestination(transform.position);
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
            if (distToTarget <= distToStartShooting && CanSeeTarget())
            {
                State = FiniteAgentState.SHOOT_TARGET;
                agent.SetDestination(transform.position);
            }

            // If current health is low and target isn't close, go to hiding state
            if (currentHealth <= lowHealth && distToTarget >= distToHide && targetsHealth > lowHealth)
            {
                State = FiniteAgentState.HIDE;
                agent.SetDestination(transform.position);
            }

            // If current health is less than or equal to zero then the agent is dead, go to the dead state
            if (currentHealth <= 0)
            {
                State = FiniteAgentState.DEAD;
                agent.SetDestination(transform.position);
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
            State = FiniteAgentState.DEAD;
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
