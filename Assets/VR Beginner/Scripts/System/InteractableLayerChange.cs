using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;


public class InteractableLayerChange : MonoBehaviour
{
    public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable TargetInteractable;
    public InteractionLayerMask NewLayerMask;

    public void ChangeLayerDynamic(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable)
    {
        interactable.interactionLayers = NewLayerMask;
    }

    public void ChangeLayer()
    {
        TargetInteractable.interactionLayers = NewLayerMask;
    }
}
