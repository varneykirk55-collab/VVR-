using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float attackRange = 2f;
    public float attackCooldown = 2f;

    private NavMeshAgent agent;
    private Animator animator;
    private EnemyHealth health;
    private float lastAttackTime;
    private bool isDead = false;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();

        if (target == null)
        {
            // Find player if not assigned
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }

        health.OnDeath.AddListener(HandleDeath);
    }

    void Update()
    {
        if (isDead || target == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= attackRange)
        {
            agent.isStopped = true;
            animator.SetFloat("Speed", 0);

            // Face target
            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
            }

            if (Time.time > lastAttackTime + attackCooldown)
            {
                Attack();
            }
        }
        else
        {
            agent.isStopped = false;
            agent.SetDestination(target.position);
            animator.SetFloat("Speed", agent.velocity.magnitude);
        }
    }

    void Attack()
    {
        animator.SetTrigger("Attack");
        lastAttackTime = Time.time;
    }

    void HandleDeath()
    {
        isDead = true;
        agent.isStopped = true;
        agent.enabled = false;
        animator.SetTrigger("Die");

        // Disable collisions
        Collider[] colliders = GetComponentsInChildren<Collider>();
        foreach (var col in colliders)
        {
            col.enabled = false;
        }

        // Notify WaveManager
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.EnemyDefeated();
        }

        Destroy(gameObject, 5f);
    }
}
