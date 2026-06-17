using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Potion will check if it have a PotionReceiver under it when it's poured, and OnPotionPoured event will be called if
/// it does.
/// </summary>
public class PotionReceiver : MonoBehaviour
{
    private bool correctPoured = false;

    [System.Serializable]
    public class PotionPouredEvent : UnityEvent<string> { }

    public string[] AcceptedPotionType;
    
    public PotionPouredEvent OnPotionPoured;

    public void ReceivePotion(string PotionType)
    {
        if (AcceptedPotionType.Contains(PotionType) && !correctPoured)
        {
            OnPotionPoured.Invoke(PotionType);
            correctPoured = true;

            EvaSystemEventSender.Send("The player poured the correct potion on the basket, the door will open.");
            StudyLogger.Instance.FinishTask(true);
        }
        else if (!correctPoured)
        {
            EvaSystemEventSender.Send("The player poured the wrong potion on the basket. Ask if they have the right potion.");
        }
    }
}
