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

    private void Update()
    {
        if (handAnimator != null)
        {
            float triggerValue = pinchAnimationAction.action.ReadValue<float>();
            float gripValue = gripAnimationAction.action.ReadValue<float>();

            handAnimator.SetFloat("Trigger", triggerValue);
            handAnimator.SetFloat("Grip", gripValue);
        }
    }
}
