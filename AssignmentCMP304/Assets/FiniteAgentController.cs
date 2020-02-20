using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class FiniteAgentController : MonoBehaviour
{
    // Editor accessable variables
    [SerializeField] private int maxHealth = 100;
    [SerializeField] Transform target = null;
    [SerializeField] private HealthBar healthBar = null;
    [SerializeField] private Transform healthBarPos = null;
    [SerializeField] private float distanceToStartStabbing = 0f;
    [SerializeField] private float distanceToStartShooting = 0f;
    [SerializeField] private float distanceToMoveTowardsTarget = 0f;
    [SerializeField] private int healthValueToRunAwayAt = 0;
    [SerializeField] private ParticleSystem muzzleFlashParticle = null;

    // Components
    private NavMeshAgent agent = null;
    private Animator animator = null;
    private Rigidbody rb = null;
    private Camera mainCamera = null;

    // State enum
    public enum FiniteAgentState
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
    public FiniteAgentState State { get; private set; } = FiniteAgentState.IDLE;
    public int CurrentHealth { get { return currentHealth; } }

    // Local variables
    private float distanceToTarget = 0f;
    private int currentHealth = 0;
    private int targetsHealth = 0;
    private Vector3 targetsLastPosSeen = Vector3.zero;

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
        if (Input.GetKeyDown(KeyCode.G))
        {
            TakeDamage(10);
        }

        // Change what gets updated depending on the current state
        switch (State)
        {
            case FiniteAgentState.IDLE:
                UpdateIdleState();
                break;
            case FiniteAgentState.STAB:
                UpdateStabState();
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
        targetsHealth = target.GetComponent<FuzzyAgentController>().CurrentHealth;
    }

    private void UpdateIdleState()
    {
        // If the targets health is not zero then do something
        if (targetsHealth > 0)
        {
            if (!CanSeeTarget())
            {
                State = FiniteAgentState.MOVE_TO_TARGET;
            }
        }
    }

    private void UpdateStabState()
    {

    }

    private void UpdateShootTargetState()
    {
        muzzleFlashParticle.Play();
    }

    private void UpdateHideState()
    {

    }

    private void UpdateMoveToTargetState()
    {
        // Check the distance between the agent and the target
        float distToTarget = Vector3.Distance(transform.position, target.position);
        if (distToTarget >= distanceToMoveTowardsTarget)
        {
            // Set the destination to the position that the agent last seen the target
            agent.SetDestination(targetsLastPosSeen);
        }
        else if (distToTarget >= distanceToStartShooting)
        {
            // Set the state to the shooting state
            State = FiniteAgentState.SHOOT_TARGET;
        }
        else if (distToTarget >= distanceToStartShooting)
        {
            // Set the state to the stabbing state
            State = FiniteAgentState.STAB;
        }

        // If the agent's health is low ignore the other conditions and hide
        if (currentHealth <= healthValueToRunAwayAt)
        {
            State = FiniteAgentState.HIDE;
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
                targetsLastPosSeen = target.position;
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
