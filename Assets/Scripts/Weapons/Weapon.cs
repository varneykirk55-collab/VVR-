using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

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

    protected virtual void Awake()
    {
        rb = GetComponent<Rigidbody>();
        interactable = GetComponent<XRBaseInteractable>();
        if (impactAudioSource == null)
            impactAudioSource = GetComponent<AudioSource>();

        previousPosition = transform.position;
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
        currentVelocity = (transform.position - previousPosition) / Time.fixedDeltaTime;
        previousPosition = transform.position;
    }

    protected virtual void OnGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;
    }

    protected virtual void OnReleased(SelectExitEventArgs args)
    {
        isHeld = false;
    }

    protected virtual void PlayImpactSound(float intensity = 1f)
    {
        if (impactAudioSource != null && impactSounds != null && impactSounds.Length > 0)
        {
            AudioClip clip = impactSounds[Random.Range(0, impactSounds.Length)];
            impactAudioSource.volume = Mathf.Clamp01(intensity);
            impactAudioSource.PlayOneShot(clip);
        }
    }
}
