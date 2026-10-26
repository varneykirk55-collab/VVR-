using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(EnemyHealth))]
public class EnemyAI : MonoBehaviour
{
    public Transform target;
    public float attackRange = 2f;
    public float attackCooldown = 2f;
    public float moveSpeed = 2.5f;
    public EnemyWeapon enemyWeapon;

    private NavMeshAgent agent;
    private Animator animator;
    private EnemyHealth health;
    private float lastAttackTime = -999f;
    private bool isDead = false;
    private Coroutine attackWindowCoroutine;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();
        if (enemyWeapon == null)
        {
            enemyWeapon = GetComponentInChildren<EnemyWeapon>();
        }
    }

    private bool loggedFirstUpdate = false;

    void Start()
    {
        Debug.Log($"VVR_DIAG enemy Start onNavMesh={(agent != null && agent.isOnNavMesh)} t={Time.realtimeSinceStartup:F2}");
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                target = player.transform;
            }
            else if (PlayerHealth.Instance != null)
            {
                target = PlayerHealth.Instance.transform;
            }
        }

        if (health != null)
        {
            health.OnDeath.AddListener(HandleDeath);
        }
    }

    void Update()
    {
        if (isDead || target == null) return;

        if (!loggedFirstUpdate)
        {
            loggedFirstUpdate = true;
            Debug.Log($"VVR_DIAG enemy first Update target={target.name} t={Time.realtimeSinceStartup:F2}");
        }

        // Calculate horizontal (XZ plane) distance so VR headset height offset doesn't skew melee range
        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(target.position.x, 0f, target.position.z);
        float distanceToTarget = Vector3.Distance(flatSelf, flatTarget);

        bool canUseNavMesh = agent != null && agent.enabled && agent.isOnNavMesh;

        if (distanceToTarget <= attackRange)
        {
            if (canUseNavMesh)
            {
                agent.isStopped = true;
            }
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetFloat("Speed", 0f);
            }

            // Face target horizontally
            Vector3 direction = (flatTarget - flatSelf).normalized;
            if (direction != Vector3.zero)
            {
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
            }

            if (Time.time >= lastAttackTime + attackCooldown)
            {
                Attack();
            }
        }
        else
        {
            Vector3 direction = (flatTarget - flatSelf).normalized;
            if (canUseNavMesh)
            {
                agent.isStopped = false;
                agent.SetDestination(target.position);
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    animator.SetFloat("Speed", agent.velocity.magnitude);
                }
            }
            else
            {
                // Fallback direct movement if NavMesh is not baked in a test scene
                transform.position += direction * (moveSpeed * Time.deltaTime);
                if (direction != Vector3.zero)
                {
                    transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), Time.deltaTime * 5f);
                }
                if (animator != null && animator.runtimeAnimatorController != null)
                {
                    animator.SetFloat("Speed", moveSpeed);
                }
            }
        }
    }

    public void Attack()
    {
        if (isDead) return;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger("Attack");
        }
        lastAttackTime = Time.time;

        if (enemyWeapon != null)
        {
            if (attackWindowCoroutine != null)
            {
                StopCoroutine(attackWindowCoroutine);
            }
            attackWindowCoroutine = StartCoroutine(AttackDamageWindowRoutine());
        }
    }

    private IEnumerator AttackDamageWindowRoutine()
    {
        yield return new WaitForSeconds(0.15f);
        if (!isDead && enemyWeapon != null)
        {
            enemyWeapon.EnableDamage();
        }
        yield return new WaitForSeconds(0.45f);
        if (enemyWeapon != null)
        {
            enemyWeapon.DisableDamage();
        }
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        if (attackWindowCoroutine != null)
        {
            StopCoroutine(attackWindowCoroutine);
        }
        if (enemyWeapon != null)
        {
            enemyWeapon.DisableDamage();
        }

        if (agent != null)
        {
            if (agent.enabled && agent.isOnNavMesh)
            {
                agent.isStopped = true;
            }
            agent.enabled = false;
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger("Die");
        }

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
