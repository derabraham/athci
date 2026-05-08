using UnityEngine;
using Convai.Modules.Narrative;
using Convai.Infrastructure.Networking;

public class EvaSystemEventSender : MonoBehaviour
{
    public static EvaSystemEventSender Instance { get; private set; }

    [Header("Convai")]
    [SerializeField] private ConvaiNarrativeDesignTrigger narrativeTrigger;

    [Header("Message Prefix")]
    [SerializeField] private string systemPrefix = "[System] ";

    [Header("Debug")]
    [SerializeField] private bool logMessages = true;

    private static bool isCollab = false;

    public static bool IsCollab => isCollab;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C)) {
            isCollab = true;
            UnityEngine.Debug.Log("[EVA] Collaboration mode enabled. System events will now be sent to Convai.");
        }
        if (Input.GetKeyDown(KeyCode.T)){
            isCollab = false;
            UnityEngine.Debug.Log("[EVA] Collaboration mode disabled. System events will no longer be sent to Convai.");
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public static void Send(string message)
    {
        if (!isCollab) return;

        if (Instance == null)
        {
            Debug.LogWarning("[EVA] No EvaSystemEventSender found in scene.");
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
            Debug.Log("[EVA] Sending system event: " + finalMessage);

        // Wichtig, falls der Trigger auf "Trigger Once" steht.
        narrativeTrigger.ResetTrigger();

        // Message setzen.
        narrativeTrigger.SetTriggerMessage(finalMessage);

        // Trigger auslösen.
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