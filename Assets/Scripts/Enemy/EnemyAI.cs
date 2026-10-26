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

    [Header("Navigation")]
    [Tooltip("Seconds between path requests. Re-pathing every frame is wasted work on Quest.")]
    public float repathInterval = 0.2f;
    [Tooltip("Only request a new path when the player has moved at least this far (metres).")]
    public float repathMinTargetMove = 0.25f;
    [Tooltip("Search radius used to snap the agent / player position onto the NavMesh.")]
    public float navMeshSampleRadius = 2.5f;
    public float despawnDelay = 5f;

    [Header("Attack Swing (procedural, the Animator has no clips)")]
    public float windUpAngle = -35f;
    public float strikeAngle = 100f;

    private NavMeshAgent agent;
    private Animator animator;
    private EnemyHealth health;
    private Collider[] colliders;
    private Transform targetHead;
    private float lastAttackTime = -999f;
    private float nextRepathTime;
    private Vector3 lastRequestedTargetPos = new Vector3(float.MaxValue, 0f, 0f);
    private bool isDead = false;
    private bool loggedFirstUpdate = false;
    private Coroutine attackWindowCoroutine;
    private Transform weaponTransform;
    private Quaternion weaponRestRotation;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int AttackHash = Animator.StringToHash("Attack");
    private static readonly int DieHash = Animator.StringToHash("Die");

    public bool IsDead => isDead;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();
        health = GetComponent<EnemyHealth>();
        colliders = GetComponentsInChildren<Collider>(true);
        if (enemyWeapon == null)
        {
            enemyWeapon = GetComponentInChildren<EnemyWeapon>();
        }
        if (enemyWeapon != null)
        {
            weaponTransform = enemyWeapon.transform;
            weaponRestRotation = weaponTransform.localRotation;
        }
    }

    void Start()
    {
        VVRDiag.Mark("enemy:Start");
        ResolveTarget();
        EnsureOnNavMesh();

        if (health != null)
        {
            health.OnDeath.AddListener(HandleDeath);
        }

        VVRDiag.Log($"enemy Start name={name} onNavMesh={(agent != null && agent.isOnNavMesh)} " +
                    $"target={(target != null ? target.name : "NONE")} head={(targetHead != null ? targetHead.name : "NONE")} " +
                    $"t={Time.realtimeSinceStartup:F2}");
    }

    private void ResolveTarget()
    {
        if (target == null)
        {
            if (PlayerHealth.Instance != null)
            {
                target = PlayerHealth.Instance.transform;
            }
            else
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null)
                {
                    target = player.transform;
                }
            }
        }

        // In room-scale VR the XR Origin root stays put while the player walks around it; chase the headset instead.
        if (targetHead == null && target != null)
        {
            Camera cam = Camera.main;
            if (cam != null && cam.transform.IsChildOf(target))
            {
                targetHead = cam.transform;
            }
        }
    }

    private Vector3 TargetPosition => targetHead != null ? targetHead.position : target.position;

    /// <summary>
    /// Makes sure the agent is bound to the NavMesh. Spawning a hair off the mesh would otherwise leave the
    /// agent unusable and the enemy falls back to walking in a straight line through obstacles.
    /// </summary>
    private void EnsureOnNavMesh()
    {
        if (agent == null || !agent.enabled || agent.isOnNavMesh) return;

        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, navMeshSampleRadius, agent.areaMask))
        {
            agent.Warp(hit.position);
        }

        if (!agent.isOnNavMesh)
        {
            VVRDiag.Warn($"enemy {name} could not be placed on the NavMesh at {transform.position}; using direct movement");
        }
    }

    /// <summary>
    /// Called by WaveManager when a pooled enemy is reused. Restores the state HandleDeath tore down.
    /// </summary>
    public void ResetForSpawn(Vector3 position, Quaternion rotation)
    {
        isDead = false;
        loggedFirstUpdate = false;
        lastAttackTime = -999f;
        nextRepathTime = 0f;
        lastRequestedTargetPos = new Vector3(float.MaxValue, 0f, 0f);
        attackWindowCoroutine = null;

        if (colliders != null)
        {
            foreach (Collider col in colliders)
            {
                if (col != null) col.enabled = true;
            }
        }

        if (enemyWeapon != null)
        {
            enemyWeapon.DisableDamage();
        }
        if (weaponTransform != null)
        {
            weaponTransform.localRotation = weaponRestRotation;
        }

        transform.SetPositionAndRotation(position, rotation);
        if (agent != null)
        {
            agent.enabled = true;
            if (agent.isOnNavMesh)
            {
                agent.Warp(position);
                agent.isStopped = false;
                agent.ResetPath();
            }
            EnsureOnNavMesh();
        }

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.Rebind();
            animator.Update(0f);
        }

        ResolveTarget();
    }

    void Update()
    {
        if (isDead) return;
        if (target == null)
        {
            ResolveTarget();
            if (target == null) return;
        }

        if (!loggedFirstUpdate)
        {
            loggedFirstUpdate = true;
            VVRDiag.Log($"enemy first Update name={name} target={target.name} t={Time.realtimeSinceStartup:F2}");
        }

        // Calculate horizontal (XZ plane) distance so VR headset height offset doesn't skew melee range
        Vector3 targetPos = TargetPosition;
        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(targetPos.x, 0f, targetPos.z);
        float distanceToTarget = Vector3.Distance(flatSelf, flatTarget);

        bool canUseNavMesh = agent != null && agent.enabled && agent.isOnNavMesh;
        bool hasController = animator != null && animator.runtimeAnimatorController != null;

        if (distanceToTarget <= attackRange)
        {
            if (canUseNavMesh)
            {
                agent.isStopped = true;
            }
            if (hasController)
            {
                animator.SetFloat(SpeedHash, 0f);
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
                if (agent.isStopped)
                {
                    agent.isStopped = false;
                    nextRepathTime = 0f;
                }
                UpdateDestination(targetPos);
                if (hasController)
                {
                    animator.SetFloat(SpeedHash, agent.velocity.magnitude);
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
                if (hasController)
                {
                    animator.SetFloat(SpeedHash, moveSpeed);
                }
            }
        }
    }

    /// <summary>
    /// Requests a path at most every repathInterval seconds, and only when the player has moved or the agent has no path.
    /// The player's position is snapped onto the NavMesh first: the player normally stands inside the hole the weapon
    /// rack cuts into the NavMesh, and an off-mesh destination can be rejected outright.
    /// </summary>
    private void UpdateDestination(Vector3 targetPos)
    {
        if (Time.time < nextRepathTime) return;
        nextRepathTime = Time.time + repathInterval;

        bool targetMoved = (targetPos - lastRequestedTargetPos).sqrMagnitude >= repathMinTargetMove * repathMinTargetMove;
        if (!targetMoved && agent.hasPath && !agent.pathPending) return;

        Vector3 destination = targetPos;
        if (NavMesh.SamplePosition(targetPos, out NavMeshHit hit, navMeshSampleRadius, agent.areaMask))
        {
            destination = hit.position;
        }

        lastRequestedTargetPos = targetPos;
        agent.SetDestination(destination);
    }

    public void Attack()
    {
        if (isDead) return;

        if (animator != null && animator.runtimeAnimatorController != null)
        {
            animator.SetTrigger(AttackHash);
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
        // Wind up (telegraphs the attack so the player can raise the shield)
        yield return SwingWeapon(windUpAngle, 0.15f);
        if (!isDead && enemyWeapon != null)
        {
            enemyWeapon.EnableDamage();
        }
        // Strike: sweeping the trigger through the player also produces fresh trigger contacts
        yield return SwingWeapon(strikeAngle, 0.45f);
        if (enemyWeapon != null)
        {
            enemyWeapon.DisableDamage();
        }
        yield return SwingWeapon(0f, 0.3f);
    }

    private IEnumerator SwingWeapon(float angleFromRest, float duration)
    {
        if (weaponTransform == null)
        {
            yield return new WaitForSeconds(duration);
            yield break;
        }

        Quaternion from = weaponTransform.localRotation;
        Quaternion to = weaponRestRotation * Quaternion.Euler(angleFromRest, 0f, 0f);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            weaponTransform.localRotation = Quaternion.Slerp(from, to, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
    }

    void HandleDeath()
    {
        if (isDead) return;
        isDead = true;

        if (attackWindowCoroutine != null)
        {
            StopCoroutine(attackWindowCoroutine);
            attackWindowCoroutine = null;
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
            animator.SetTrigger(DieHash);
        }

        // Disable collisions
        if (colliders != null)
        {
            foreach (var col in colliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        VVRDiag.Log($"enemy died name={name} t={Time.realtimeSinceStartup:F2}");

        // Notify WaveManager
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.EnemyDefeated();
        }

        StartCoroutine(DespawnAfterDelay());
    }

    private IEnumerator DespawnAfterDelay()
    {
        yield return new WaitForSeconds(despawnDelay);
        if (WaveManager.Instance != null)
        {
            WaveManager.Instance.ReleaseEnemy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }
}
