using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Sword : Weapon
{
    public Transform damagePoint;
    public LayerMask enemyLayer = ~0;
    public float swingCooldown = 0.5f;
    private float lastSwingTime = -999f;

    // To prevent rapid hit exploits per collider
    private Dictionary<Collider, float> hitCooldowns = new Dictionary<Collider, float>();

    protected override Transform GetVelocityReferenceTransform()
    {
        return damagePoint != null ? damagePoint : transform;
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
                    TriggerHaptics(0.5f, 0.1f);

                    lastSwingTime = Time.time;
                    hitCooldowns[collision.collider] = Time.time + swingCooldown;
                }
            }
            else
            {
                // Hit something else (e.g. environment, shield)
                PlayImpactSound(0.2f);
                TriggerHaptics(0.2f, 0.05f);
            }
        }
    }

    private bool CanHit(Collider col)
    {
        if (Time.time < lastSwingTime + (swingCooldown * 0.25f))
        {
            return false;
        }
        if (col != null && hitCooldowns.TryGetValue(col, out float time))
        {
            return Time.time > time;
        }
        return true;
    }
}
