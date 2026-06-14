using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.VFX;

/// <summary>
/// Handle destroying object when you throw it in the fire.
/// </summary>
public class FireDestruction : MonoBehaviour
{
    [FormerlySerializedAs("destroyEffect")]
    public VisualEffect DestroyEffect;

    public AudioSource DestroyAudioSource;

    void Start()
    {
        DestroyEffect.transform.position = new Vector3(0, 0, 0);
    }


    private void OnTriggerEnter(Collider other)
    {
        Potion potion = other.GetComponentInParent<Potion>();

        if (potion != null)
        {
            potion.PlugOff();

            // Wenn Potion gerade gehalten wird: nur entkorken, nicht zerstören
            if (other.GetComponentInParent<XROffsetGrabbable>() is XROffsetGrabbable potionGrab && potionGrab.isSelected)
            {
                return;
            }

            // Wenn Potion nicht gehalten wird, darf sie danach normal zerstört werden
        }

        if (other.gameObject.TryGetComponent(out XROffsetGrabbable scriptX) && !scriptX.isSelected)
        {
            if (!other.gameObject.TryGetComponent(out IndestructableObj scriptY))
            {
                var respawnable = other.GetComponent<RespawnableObject>();

                DestroyEffect.gameObject.transform.position = other.gameObject.transform.position;
                DestroyEffect.SendEvent("Explode");

                DestroyAudioSource.Play();

                if (respawnable == null)
                {
                    Destroy(other.gameObject, 0.2f);
                }
                else
                {
                    respawnable.Respawn();
                }
            }
        }
    }
}