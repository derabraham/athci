using UnityEngine;
using Convai.Modules.Narrative;
using Convai.Infrastructure.Networking;

public class EvaSystemEventSender : MonoBehaviour
{
    public static EvaSystemEventSender Instance { get; private set; }

    [Header("Convai")]
    [SerializeField] private ConvaiNarrativeDesignTrigger narrativeTrigger;

    [Header("Condition")]
    [SerializeField] private bool isCollab = false;

    [Header("Message Prefix")]
    [SerializeField] private string systemPrefix = "[System] ";

    [Header("Debug")]
    [SerializeField] private bool logMessages = true;

    public static bool IsCollab { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        IsCollab = isCollab;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        IsCollab = isCollab;
    }
#endif

    public void SetCollabMode(bool value)
    {
        isCollab = value;
        IsCollab = value;
    }

    public static void Send(string message)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[EVA] No EvaSystemEventSender found in scene.");
            return;
        }

        if (!IsCollab)
        {
            return;
        }

        Instance.SendInternal(message);
    }

    private void SendInternal(string message)
    {
        if (narrativeTrigger == null)
        {
            Debug.LogWarning("[EVA] ConvaiNarrativeDesignTrigger is not assigned.");
            return;
        }

        string finalMessage = systemPrefix + message;

        if (logMessages)
        {
            Debug.Log("[EVA] Sending system event: " + finalMessage);
        }

        narrativeTrigger.ResetTrigger();
        narrativeTrigger.SetTriggerMessage(finalMessage);

        bool success = narrativeTrigger.InvokeTrigger();

        if (!success)
        {
            Debug.LogWarning("[EVA] Failed to invoke Convai narrative trigger.");
        }
        else
        {
            StudyCounters.AddInteraction();
        }
    }
}