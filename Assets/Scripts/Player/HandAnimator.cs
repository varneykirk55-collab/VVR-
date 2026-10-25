using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class HandAnimator : MonoBehaviour
{
    public InputActionProperty pinchAnimationAction;
    public InputActionProperty gripAnimationAction;

    public Animator handAnimator;

    private void Start()
    {
        if (handAnimator == null)
        {
            handAnimator = GetComponent<Animator>();
        }
    }

    private void OnEnable()
    {
        if (pinchAnimationAction.action != null)
        {
            pinchAnimationAction.action.Enable();
        }
        if (gripAnimationAction.action != null)
        {
            gripAnimationAction.action.Enable();
        }
    }

    private void Update()
    {
        if (handAnimator != null && handAnimator.runtimeAnimatorController != null)
        {
            float triggerValue = pinchAnimationAction.action != null
                ? pinchAnimationAction.action.ReadValue<float>()
                : 0f;
            float gripValue = gripAnimationAction.action != null
                ? gripAnimationAction.action.ReadValue<float>()
                : 0f;

            handAnimator.SetFloat("Trigger", triggerValue);
            handAnimator.SetFloat("Grip", gripValue);
        }
    }
}
