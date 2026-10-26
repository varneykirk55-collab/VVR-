using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    public static PlayerHealth Instance { get; private set; }

    public float maxHealth = 100f;
    public float currentHealth;
    public Shield activeShield;

    [Tooltip("Keep the body CapsuleCollider under the headset. The XR Origin root does not move when the player walks in room-scale, so without this enemies would hit an empty spot at the origin.")]
    public bool bodyColliderFollowsHead = true;

    public UnityEvent OnDeath = new UnityEvent();
    public UnityEvent<float> OnHealthChanged = new UnityEvent<float>();
    public UnityEvent OnTakeDamage = new UnityEvent();

    private CapsuleCollider bodyCollider;
    private Transform head;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        currentHealth = maxHealth;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth / Mathf.Max(1f, maxHealth));

        bodyCollider = GetComponent<CapsuleCollider>();
        Camera cam = Camera.main;
        if (cam != null && cam.transform.IsChildOf(transform))
        {
            head = cam.transform;
        }
    }

    private void LateUpdate()
    {
        if (!bodyColliderFollowsHead || bodyCollider == null || head == null) return;

        Vector3 headLocal = transform.InverseTransformPoint(head.position);
        Vector3 center = bodyCollider.center;
        float dx = headLocal.x - center.x;
        float dz = headLocal.z - center.z;
        // Only touch the collider when the head has moved noticeably, to avoid needless physics shape updates.
        if (dx * dx + dz * dz > 0.0004f)
        {
            bodyCollider.center = new Vector3(headLocal.x, center.y, headLocal.z);
        }
    }

    public Shield GetEquippedShield()
    {
        if (activeShield != null && activeShield.IsHeld)
        {
            return activeShield;
        }
        return Shield.GetHeldShield();
    }

    public void TakeDamage(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(currentHealth, 0f);
        VVRDiag.Log($"player hit damage={amount:F1} hp={currentHealth:F0}");

        OnTakeDamage?.Invoke();
        OnHealthChanged?.Invoke(currentHealth / Mathf.Max(1f, maxHealth));

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
        VVRDiag.Log("player died");
        OnDeath?.Invoke();
    }

    public void Heal(float amount)
    {
        if (currentHealth <= 0f) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);

        OnHealthChanged?.Invoke(currentHealth / Mathf.Max(1f, maxHealth));
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(1f);
    }
}
