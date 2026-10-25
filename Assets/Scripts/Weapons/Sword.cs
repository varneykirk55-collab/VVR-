using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class Sword : Weapon
{
    public Transform damagePoint;
    public LayerMask enemyLayer;
    public float swingCooldown = 0.5f;
    private float lastSwingTime;

    // To prevent rapid hit exploits
    private Dictionary<Collider, float> hitCooldowns = new Dictionary<Collider, float>();

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
                        TriggerHaptics(0.5f, 0.1f);

                        hitCooldowns[collision.collider] = Time.time + swingCooldown;
                    }
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
