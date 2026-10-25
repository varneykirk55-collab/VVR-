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

    public UnityEvent OnDeath = new UnityEvent();
    public UnityEvent<float> OnHealthChanged = new UnityEvent<float>();
    public UnityEvent OnTakeDamage = new UnityEvent();

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

        OnTakeDamage?.Invoke();
        OnHealthChanged?.Invoke(currentHealth / Mathf.Max(1f, maxHealth));

        if (currentHealth <= 0f)
        {
            Die();
        }
    }

    private void Die()
    {
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
