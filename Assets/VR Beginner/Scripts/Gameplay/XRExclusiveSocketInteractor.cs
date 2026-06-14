using UnityEngine;

/// <summary>
/// Subclass of the classic Socket Interactor from the Interaction toolkit that will only accept object with the right
/// SocketTarget 
/// </summary>
public class XRExclusiveSocketInteractor : UnityEngine.XR.Interaction.Toolkit.Interactors.XRSocketInteractor
{
    public string AcceptedType;

    public override bool CanSelect(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRSelectInteractable interactable)
    {
        SocketTarget socketTarget = (interactable as Component)?.GetComponent<SocketTarget>();

        if (socketTarget == null)
            return false;

        return base.CanSelect(interactable) && (socketTarget.SocketType == AcceptedType);
    }

    public override bool CanHover(UnityEngine.XR.Interaction.Toolkit.Interactables.IXRHoverInteractable interactable)
    {
        SocketTarget socketTarget = (interactable as Component)?.GetComponent<SocketTarget>();

        if (socketTarget == null)
            return false;

        return base.CanHover(interactable) && (socketTarget.SocketType == AcceptedType);
    }
}
