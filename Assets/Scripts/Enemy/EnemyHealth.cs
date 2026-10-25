using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    public UnityEvent OnDeath;
    public UnityEvent OnTakeDamage;

    public ParticleSystem bloodEffect;
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip deathSound;

    private Animator animator;

    void Start()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (currentHealth <= 0) return;

        currentHealth -= amount;

        OnTakeDamage?.Invoke();

        if (bloodEffect != null)
        {
            bloodEffect.transform.position = hitPoint;
            bloodEffect.Play();
        }

        if (audioSource != null && hitSound != null)
        {
            audioSource.PlayOneShot(hitSound);
        }

        if (currentHealth > 0)
        {
            if (animator != null) animator.SetTrigger("Hit");
        }
        else
        {
            Die();
        }
    }

    private void Die()
    {
        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }
        OnDeath?.Invoke();
    }
}
