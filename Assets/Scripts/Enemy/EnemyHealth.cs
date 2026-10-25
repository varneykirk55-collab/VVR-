using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class EnemyHealth : MonoBehaviour
{
    public float maxHealth = 100f;
    public float currentHealth;

    public UnityEvent OnDeath = new UnityEvent();
    public UnityEvent OnTakeDamage = new UnityEvent();

    public ParticleSystem bloodEffect;
    public AudioSource audioSource;
    public AudioClip hitSound;
    public AudioClip deathSound;

    private Animator animator;
    private bool isDead = false;

    void Awake()
    {
        currentHealth = maxHealth;
        animator = GetComponent<Animator>();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    void Start()
    {
        if (currentHealth <= 0f) currentHealth = maxHealth;
    }

    public void TakeDamage(float amount, Vector3 hitPoint)
    {
        if (isDead || currentHealth <= 0f) return;

        currentHealth -= amount;
        currentHealth = Mathf.Max(0f, currentHealth);

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

        if (currentHealth > 0f)
        {
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger("Hit");
            }
        }
        else
        {
            Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (audioSource != null && deathSound != null)
        {
            audioSource.PlayOneShot(deathSound);
        }
        OnDeath?.Invoke();
    }
}
