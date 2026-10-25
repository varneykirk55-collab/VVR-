using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(Rigidbody))]
public class Weapon : MonoBehaviour
{
    public float baseDamage = 10f;
    public float minimumVelocityForDamage = 1.5f;
    public float damageMultiplier = 2f;
    public AudioSource impactAudioSource;
    public AudioClip[] impactSounds;

    protected Rigidbody rb;
    protected XRBaseInteractable interactable;
    protected bool isHeld = false;
    protected Vector3 previousPosition;
    protected Vector3 currentVelocity;

    public bool IsHeld => isHeld;
    public Vector3 CurrentVelocity => currentVelocity;

    /// <summary>
    /// Returns the point on the weapon used to measure strike velocity (e.g., blade tip or axe head).
    /// Defaults to the weapon's root transform if no specific strike point is assigned.
    /// </summary>
    protected virtual Transform GetVelocityReferenceTransform()
    {
        return transform;
    }

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        }

        interactable = GetComponent<XRBaseInteractable>();
        if (impactAudioSource == null)
        {
            impactAudioSource = GetComponent<AudioSource>();
        }

        Transform refTransform = GetVelocityReferenceTransform();
        previousPosition = refTransform != null ? refTransform.position : transform.position;
    }

    protected virtual void OnEnable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.AddListener(OnGrabbed);
            interactable.selectExited.AddListener(OnReleased);
        }
    }

    protected virtual void OnDisable()
    {
        if (interactable != null)
        {
            interactable.selectEntered.RemoveListener(OnGrabbed);
            interactable.selectExited.RemoveListener(OnReleased);
        }
    }

    protected virtual void FixedUpdate()
    {
        Transform refTransform = GetVelocityReferenceTransform();
        Vector3 trackedPos = refTransform != null ? refTransform.position : transform.position;

        if (Time.fixedDeltaTime > 0f)
        {
            currentVelocity = (trackedPos - previousPosition) / Time.fixedDeltaTime;
        }
        previousPosition = trackedPos;
    }

    protected virtual void OnGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;
        Transform refTransform = GetVelocityReferenceTransform();
        previousPosition = refTransform != null ? refTransform.position : transform.position;
        currentVelocity = Vector3.zero;
    }

    protected virtual void OnReleased(SelectExitEventArgs args)
    {
        isHeld = false;
    }

    /// <summary>
    /// Allows programmatic or test-driven state changes when an XRI interactor is not actively driving selection.
    /// </summary>
    public void SetHeldState(bool held)
    {
        isHeld = held;
        Transform refTransform = GetVelocityReferenceTransform();
        previousPosition = refTransform != null ? refTransform.position : transform.position;
        currentVelocity = Vector3.zero;
    }

    /// <summary>
    /// Evaluates whether the weapon's current strike point velocity exceeds minimumVelocityForDamage
    /// and applies velocity-scaled damage to the target EnemyHealth.
    /// </summary>
    public virtual bool TryDealDamage(EnemyHealth enemy)
    {
        if (!isHeld || enemy == null) return false;

        float velocityMagnitude = currentVelocity.magnitude;
        if (velocityMagnitude <= minimumVelocityForDamage) return false;

        float damage = baseDamage + (velocityMagnitude * damageMultiplier);
        Transform refTransform = GetVelocityReferenceTransform();
        Vector3 hitPoint = refTransform != null ? refTransform.position : transform.position;
        enemy.TakeDamage(damage, hitPoint);
        return true;
    }

    protected virtual void PlayImpactSound(float intensity = 1f)
    {
        if (impactAudioSource != null && impactSounds != null && impactSounds.Length > 0)
        {
            AudioClip clip = impactSounds[Random.Range(0, impactSounds.Length)];
            if (clip != null)
            {
                impactAudioSource.volume = Mathf.Clamp01(intensity);
                impactAudioSource.PlayOneShot(clip);
            }
        }
    }

    protected virtual void TriggerHaptics(float intensity, float duration)
    {
        if (interactable != null && interactable.interactorsSelecting.Count > 0)
        {
            var interactor = interactable.interactorsSelecting[0];
            if (interactor is XRBaseInputInteractor inputInteractor)
            {
                inputInteractor.SendHapticImpulse(Mathf.Clamp01(intensity), duration);
            }
        }
    }
}
