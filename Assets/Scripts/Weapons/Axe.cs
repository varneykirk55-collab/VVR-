using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Axe : Weapon
{
    public Transform axeHead; // Point where damage is calculated from
    public LayerMask enemyLayer;
    public float swingCooldown = 0.6f;

    private Dictionary<Collider, float> hitCooldowns = new Dictionary<Collider, float>();

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
            if (((1 << collision.gameObject.layer) & enemyLayer) != 0)
            {
                if (CanHit(collision.collider))
                {
                    EnemyHealth enemy = collision.gameObject.GetComponentInParent<EnemyHealth>();
                    if (enemy != null)
                    {
                        float damage = baseDamage + (velocityMagnitude * damageMultiplier);
                        enemy.TakeDamage(damage, collision.GetContact(0).point);

                        PlayImpactSound(velocityMagnitude / 5f);
                        TriggerHaptics(0.7f, 0.15f); // Heavier haptics for axe

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
        if (hitCooldowns.TryGetValue(col, out float time))
        {
            return Time.time > time;
        }
        return true;
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
