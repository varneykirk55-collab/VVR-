using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Axe : Weapon
{
    public Transform axeHead; // Point where damage and velocity are calculated from
    public LayerMask enemyLayer = ~0;
    public float swingCooldown = 0.6f;

    private Dictionary<Collider, float> hitCooldowns = new Dictionary<Collider, float>();

    protected override Transform GetVelocityReferenceTransform()
    {
        return axeHead != null ? axeHead : transform;
    }

    protected override void Awake()
    {
        base.Awake();
        baseDamage = 15f; // Axe hits harder base
        damageMultiplier = 2.5f;
        minimumVelocityForDamage = 2.0f; // Needs slightly more force
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isHeld) return;

        float velocityMagnitude = currentVelocity.magnitude;

        if (velocityMagnitude > minimumVelocityForDamage)
        {
            EnemyHealth enemy = collision.gameObject.GetComponentInParent<EnemyHealth>();
            bool isEnemyLayer = ((1 << collision.gameObject.layer) & enemyLayer) != 0;

            if (enemy != null && isEnemyLayer)
            {
                if (CanHit(collision.collider))
                {
                    float damage = baseDamage + (velocityMagnitude * damageMultiplier);
                    Vector3 hitPoint = collision.contactCount > 0
                        ? collision.GetContact(0).point
                        : GetVelocityReferenceTransform().position;

                    enemy.TakeDamage(damage, hitPoint);

                    PlayImpactSound(velocityMagnitude / 5f);
                    TriggerHaptics(0.7f, 0.15f); // Heavier haptics for axe

                    if (collision.collider != null)
                    {
                        hitCooldowns[collision.collider] = Time.time + swingCooldown;
                    }
                }
            }
            else
            {
                PlayImpactSound(0.3f);
                TriggerHaptics(0.3f, 0.1f);
            }
        }
    }

    private bool CanHit(Collider col)
    {
        if (col != null && hitCooldowns.TryGetValue(col, out float time))
        {
            return Time.time > time;
        }
        return true;
    }
}
