using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Shield : Weapon
{
    public float damageReductionMultiplier = 0.2f; // Takes 20% damage when blocking
    public float blockAngleTolerance = 90f; // Cone angle for successful block

    protected override void Awake()
    {
        base.Awake();
        baseDamage = 5f; // Shield bash base damage
        damageMultiplier = 1f;
    }

    // Called by PlayerHealth or EnemyWeapon when taking damage
    public bool TryBlock(Vector3 attackDirection, out float reducedDamage, float incomingDamage)
    {
        reducedDamage = incomingDamage;

        if (!isHeld) return false;

        // Check if the shield is facing the attack
        Vector3 shieldForward = transform.forward; // Assuming Z+ is the front of the shield
        float angle = Vector3.Angle(shieldForward, -attackDirection.normalized);

        if (angle <= blockAngleTolerance / 2f)
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
                enemy.TakeDamage(damage, collision.GetContact(0).point);

                PlayImpactSound(velocityMagnitude / 5f);
                TriggerHaptics(0.4f, 0.1f);
            }
        }
    }

    private void TriggerHaptics(float intensity, float duration)
    {
        if (interactable != null && interactable.interactorsSelecting.Count > 0)
        {
            var interactor = interactable.interactorsSelecting[0];
            if (interactor is XRBaseControllerInteractor controllerInteractor)
            {
                controllerInteractor.xrController.SendHapticImpulse(intensity, duration);
            }
        }
    }
}
