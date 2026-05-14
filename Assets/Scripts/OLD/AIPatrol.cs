using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.AI;

public class AIPatrol: MonoBehaviour
{
    GameObject player;

    NavMeshAgent agent;
    private Animator animator;

    [SerializeField] LayerMask groundLayer, playerLayer;

    Vector3 destPoint;
    bool walkpointSet;
    [SerializeField] float range;

    private float speed;
    private float direction;
    int randomSpeed;
    float runSpeedTimer;
    float idleTime;
    float idleDuration;
    bool sitting;
    int randomIdle;
    bool isIdle;
    bool pickIdle = true;
    int idleId;

    void Start()
    {
        animator = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();
        player = GameObject.Find("Player");

        runSpeedTimer = 0f; // Random.Range(5f, 10f);
        idleTime = Random.Range(10f,15f);
        idleDuration = 0;//Random.Range(5f,10f);
        idleId = 0;
    }


    void Update()
    {
        runSpeedTimer -= Time.deltaTime;
        if (idleTime <= 0 | idleDuration > 0)
        {
            if(pickIdle) 
            {
                idleId = IdleAnimations();
            }
            else 
            {
                idleDuration -= Time.deltaTime;
            }
        }
        else {
            idleTime -= Time.deltaTime;
            Patrol();
            CancelIdleAnimations(idleId);
            UpdateRunningAnimations();
        }
    }

    void Patrol()
    {
        if (!walkpointSet) SearchForDest();
        if (walkpointSet) 
        {
            agent.SetDestination(destPoint);
            Vector3 movement = transform.forward * Time.deltaTime * 1f;
            agent.Move(movement);
            DetermineSpeed();
        }
        if(Vector3.Distance(transform.position, destPoint) < 1) walkpointSet = false;
    }

    void SearchForDest()
    {
        float z = Random.Range(-range, range);
        float x = Random.Range(-range, range);

        destPoint = new Vector3(transform.position.x + x, transform.position.y, transform.position.z + z);

        if (Physics.Raycast(destPoint, Vector3.down, groundLayer))
        {
            walkpointSet = true;
        }
    }

    void UpdateRunningAnimations()
    {
        speed = agent.velocity.magnitude;
        direction = Vector3.Dot(agent.velocity.normalized, transform.right);
        if (speed < 0.1f)
        {
            animator.SetFloat("Speed", 0);
        }
        else
        {
            animator.SetFloat("Speed", speed);
            animator.SetFloat("Direction", direction);
        }

    }

    void DetermineSpeed()
    {
        if (runSpeedTimer <= 0)
        {
            runSpeedTimer = Random.Range(10f, 15f);
            randomSpeed = Random.Range(0,3);
            if (randomSpeed == 0) // walking
            {
                if (Vector3.Distance(transform.position, destPoint) < 10)
                {
                    GetComponent<NavMeshAgent>().speed = 0.5f;
                }
                else 
                {
                    GetComponent<NavMeshAgent>().speed = 1f;
                }
            }
            if (randomSpeed == 1) // running
            {
                GetComponent<NavMeshAgent>().speed = 5f;
            }
            if (randomSpeed == 2) // fast running
            {
                GetComponent<NavMeshAgent>().speed = 10f;
            }
        }
    }

    int IdleAnimations()
    {
        pickIdle = false;
        idleTime = Random.Range(10f, 15f);
        idleDuration = Random.Range(10f, 15f);
        agent.ResetPath();
        randomIdle = 0;// Random.Range(0,5);
        if (randomIdle==0) // sitting
        {
            animator.SetFloat("Speed", 0);
            animator.SetBool("isSitting", true);
        }
        return randomIdle;
    }

    void CancelIdleAnimations(int idleId)
    {
        pickIdle = true;
        if (idleId == 0)
        {
            animator.SetBool("isSitting", false);
        }
    }
}
