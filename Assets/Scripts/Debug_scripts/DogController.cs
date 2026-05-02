using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent), typeof(Animator))]
public class DogController : MonoBehaviour
{
    NavMeshAgent agent;
    Animator animator;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
    }

    void Update()
    {
        // Geschwindigkeit ermitteln (ohne Y-Komponente)
        Vector3 velocity = agent.velocity;
        velocity.y = 0;
        float speed = velocity.magnitude;

        // Animator-Parameter setzen
        animator.SetFloat("Speed", speed);

        // Debug: Ziel setzen mit rechter Maustaste
        if (Input.GetMouseButtonDown(1))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                agent.SetDestination(hit.point);
            }
        }
    }
}
