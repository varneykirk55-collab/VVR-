using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyWeapon : MonoBehaviour
{
    public float damage = 15f;

    // To only deal damage once per attack animation
    private bool canDealDamage = false;
    private Collider weaponCollider;

    public bool CanDealDamage => canDealDamage;

    void Start()
    {
        weaponCollider = GetComponent<Collider>();
        if (weaponCollider != null)
        {
            weaponCollider.isTrigger = true;
        }
    }

    // Called via Animation Events or EnemyAI attack coroutine
    public void EnableDamage()
    {
        canDealDamage = true;
    }

    // Called via Animation Events or EnemyAI attack coroutine
    public void DisableDamage()
    {
        canDealDamage = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canDealDamage || other == null) return;

        // 1. Check if the enemy weapon physically struck a held Shield first
        Shield hitShield = other.GetComponentInParent<Shield>();
        if (hitShield != null && hitShield.IsHeld)
        {
            Vector3 attackDir = transform.position - hitShield.transform.position;
            if (hitShield.TryBlock(attackDir, out float reducedFromShieldHit, damage))
            {
                canDealDamage = false; // Attack was intercepted by the shield
                PlayerHealth ownerPlayer = FindPlayerHealth(other);
                if (ownerPlayer != null && reducedFromShieldHit > 0f)
                {
                    ownerPlayer.TakeDamage(reducedFromShieldHit);
                }
                return;
            }
        }

        // 2. Check if the enemy weapon hit the Player
        PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
        if (player == null && other.CompareTag("Player"))
        {
            player = FindPlayerHealth(other);
        }

        if (player != null)
        {
            float damageToDeal = damage;

            // Check if the player is holding a shield oriented toward this attack
            Shield shield = player.GetEquippedShield();
            if (shield == null)
            {
                shield = other.GetComponentInParent<Shield>();
            }

            if (shield != null && shield.IsHeld)
            {
                Vector3 attackDir = transform.position - other.transform.position;
                if (shield.TryBlock(attackDir, out float reduced, damage))
                {
                    damageToDeal = reduced;
                }
            }

            player.TakeDamage(damageToDeal);
            canDealDamage = false; // Only hit once per swing
        }
    }

    private PlayerHealth FindPlayerHealth(Collider other)
    {
        if (other != null)
        {
            PlayerHealth inParent = other.GetComponentInParent<PlayerHealth>();
            if (inParent != null) return inParent;
        }
        if (PlayerHealth.Instance != null) return PlayerHealth.Instance;
        return Object.FindAnyObjectByType<PlayerHealth>();
    }
}
