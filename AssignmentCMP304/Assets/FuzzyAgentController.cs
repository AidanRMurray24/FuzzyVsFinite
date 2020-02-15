using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using FLS;
using FLS.Rules;
using FLS.MembershipFunctions;

public class FuzzyAgentController : MonoBehaviour
{
    [SerializeField] private int maxHealth = 100;
    [SerializeField] Transform target = null;
    [SerializeField] private HealthBar healthBar = null;
    [SerializeField] private Transform healthBarPos = null;
    private float distanceToTarget = 0f;
    private int currentHealth = 0;
    private NavMeshAgent agent = null;
    private Animator animator = null;
    private Rigidbody rb = null;
    private Camera mainCamera = null;

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
    public FuzzyAgentState State { get; private set; } = FuzzyAgentState.MOVE_TO_TARGET;

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
    }

    private void UpdateIdleState()
    {

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
