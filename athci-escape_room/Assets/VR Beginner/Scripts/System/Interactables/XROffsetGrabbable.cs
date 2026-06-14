using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// Small modification of the classic XRGrabInteractable that will keep the position and rotation offset between the
/// grabbed object and the controller instead of snapping the object to the controller. Better for UX and the illusion
/// of holding the thing (see Tomato Presence : https://owlchemylabs.com/tomatopresence/)
/// </summary>
public class XROffsetGrabbable : UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable
{
    class SavedTransform
    {
        public Vector3 OriginalPosition;
        public Quaternion OriginalRotation;
    }
    
    
    Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor, SavedTransform> m_SavedTransforms = new Dictionary<UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor, SavedTransform>();

    Rigidbody m_Rb;

    protected override void Awake()
    {
        base.Awake();

        //the base class already grab it but don't expose it so have to grab it again
        m_Rb = GetComponent<Rigidbody>();
    }
    
    protected override void OnSelectEntered(SelectEnterEventArgs evt)
    {
        var interactor = evt.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
        if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor)
        {
            Transform interactorAttachTransform = interactor.GetAttachTransform(this);
            SavedTransform savedTransform = new SavedTransform();
            
            savedTransform.OriginalPosition = interactorAttachTransform.localPosition;
            savedTransform.OriginalRotation = interactorAttachTransform.localRotation;

            m_SavedTransforms[interactor] = savedTransform;
            
            
            bool haveAttach = attachTransform != null;

            interactorAttachTransform.position = haveAttach ? attachTransform.position : m_Rb.worldCenterOfMass;
            interactorAttachTransform.rotation = haveAttach ? attachTransform.rotation : m_Rb.rotation;
        }

        base.OnSelectEntered(evt);
    }

    protected override void OnSelectExited(SelectExitEventArgs evt)
    {
        var interactor = evt.interactorObject as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
        if (interactor is UnityEngine.XR.Interaction.Toolkit.Interactors.XRDirectInteractor)
        {
            SavedTransform savedTransform = null;
            if (m_SavedTransforms.TryGetValue(interactor, out savedTransform))
            {
                Transform interactorAttachTransform = interactor.GetAttachTransform(this);
                interactorAttachTransform.localPosition = savedTransform.OriginalPosition;
                interactorAttachTransform.localRotation = savedTransform.OriginalRotation;

                m_SavedTransforms.Remove(interactor);
            }
        }
        
        base.OnSelectExited(evt);
    }

    public override bool IsSelectableBy(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRSelectInteractor interactor)
    {
        var baseInteractor = interactor as UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor;
        if (baseInteractor == null)
            return base.IsSelectableBy(interactor);

        int interactorLayerMask = 1 << baseInteractor.gameObject.layer;
        return base.IsSelectableBy(interactor) && (((int)interactionLayers & interactorLayerMask) != 0);
    }
}
