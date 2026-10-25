using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EnemyWeapon : MonoBehaviour
{
    public float damage = 15f;

    // To only deal damage once per attack animation
    private bool canDealDamage = false;
    private Collider weaponCollider;

    void Start()
    {
        weaponCollider = GetComponent<Collider>();
        if (weaponCollider != null)
        {
            weaponCollider.isTrigger = true;
        }
    }

    // Called via Animation Events
    public void EnableDamage()
    {
        canDealDamage = true;
    }

    // Called via Animation Events
    public void DisableDamage()
    {
        canDealDamage = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!canDealDamage) return;

        if (other.CompareTag("Player"))
        {
            // First check if hit a shield
            Shield shield = other.GetComponentInParent<Shield>();
            float damageToDeal = damage;

            if (shield != null)
            {
                // Vector from player to enemy
                Vector3 attackDir = transform.position - other.transform.position;
                if (shield.TryBlock(attackDir, out float reduced, damage))
                {
                    damageToDeal = reduced;
                }
            }

            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player != null)
            {
                player.TakeDamage(damageToDeal);
                canDealDamage = false; // Only hit once per swing
            }
        }
    }
}
