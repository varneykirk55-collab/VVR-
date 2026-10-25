using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Shield : Weapon
{
    public static readonly List<Shield> ActiveShields = new List<Shield>();

    public float damageReductionMultiplier = 0.2f; // Takes 20% damage when blocking (or 0 when physical intercept)
    public float blockAngleTolerance = 100f; // Cone angle for successful block

    protected override void Awake()
    {
        base.Awake();
        baseDamage = 5f; // Shield bash base damage
        damageMultiplier = 1f;
    }

    protected override void OnEnable()
    {
        base.OnEnable();
        if (!ActiveShields.Contains(this))
        {
            ActiveShields.Add(this);
        }
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        ActiveShields.Remove(this);
    }

    /// <summary>
    /// Finds the currently held shield in the scene (if any).
    /// </summary>
    public static Shield GetHeldShield()
    {
        for (int i = 0; i < ActiveShields.Count; i++)
        {
            Shield shield = ActiveShields[i];
            if (shield != null && shield.isHeld)
            {
                return shield;
            }
        }
        return null;
    }

    // Called by PlayerHealth or EnemyWeapon when taking damage
    public bool TryBlock(Vector3 attackDirection, out float reducedDamage, float incomingDamage)
    {
        reducedDamage = incomingDamage;

        if (!isHeld) return false;
        if (attackDirection.sqrMagnitude < 0.0001f) return false;

        // Check if the shield is facing the attack (attackDirection points from player/shield toward the attacker, or from attacker to player)
        Vector3 shieldForward = transform.forward;
        float angleAgainstIncoming = Vector3.Angle(shieldForward, -attackDirection.normalized);
        float angleTowardAttacker = Vector3.Angle(shieldForward, attackDirection.normalized);
        float effectiveAngle = Mathf.Min(angleAgainstIncoming, angleTowardAttacker);

        if (effectiveAngle <= blockAngleTolerance / 2f)
        {
            // Successful block
            reducedDamage = incomingDamage * damageReductionMultiplier;
            PlayImpactSound(1f);
            TriggerHaptics(0.8f, 0.2f);
            return true;
        }

        return false;
    }

    void OnCollisionEnter(Collision collision)
    {
        // Allow shield bashing
        if (!isHeld) return;

        float velocityMagnitude = currentVelocity.magnitude;

        if (velocityMagnitude > minimumVelocityForDamage)
        {
            EnemyHealth enemy = collision.gameObject.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                float damage = baseDamage + (velocityMagnitude * damageMultiplier);
                Vector3 hitPoint = collision.contactCount > 0
                    ? collision.GetContact(0).point
                    : transform.position;
                enemy.TakeDamage(damage, hitPoint);

                PlayImpactSound(velocityMagnitude / 5f);
                TriggerHaptics(0.4f, 0.1f);
            }
        }
    }
}
