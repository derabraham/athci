using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor))]
public class TriggerAnimationEvent : MonoBehaviour
{
    public string TriggerName;

    int m_TriggerID;
    
    void Start()
    {
        m_TriggerID = Animator.StringToHash(TriggerName);
        var interactor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor>();
        interactor.selectEntered.AddListener(TriggerAnim);
    }

    public void TriggerAnim(SelectEnterEventArgs args)
    {
        var interactable = args.interactableObject as Component;
        var animator = interactable.GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.SetTrigger(TriggerName);
        }

        if (args.interactableObject is UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable baseInteractable)
            baseInteractable.interactionLayers = (InteractionLayerMask)((int)baseInteractable.interactionLayers & ~(1 << LayerMask.NameToLayer("Hands")));
    }
}
