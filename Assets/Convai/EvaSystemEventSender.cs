using Convai.Infrastructure.Networking;
using Convai.Modules.Narrative;
using UnityEngine;

public class EvaSystemEventSender : MonoBehaviour
{
    public static EvaSystemEventSender Instance { get; private set; }

    [Header("Convai")]
    [SerializeField] private ConvaiNarrativeDesignTrigger narrativeTrigger;

    [Header("Condition")]
    [Tooltip("Set this per scene. If enabled, EVA can send proactive collaboration hints. If disabled, EVA behaves as tool mode.")]
    [SerializeField] private bool isCollab = false;

    [Header("Manual Speaking Guard - Recommended")]
    [SerializeField] private bool blockWhileUserIsSpeaking = false;

    [SerializeField] private bool blockWhileEvaIsSpeaking = false;

    [SerializeField] private float requiredSilentSecondsBeforeSend = 5f;

    [Header("Optional AudioSource Guard - OFF by default")]
    [SerializeField] private bool useAudioSourceSpeakingGuard = false;

    [SerializeField] private AudioSource[] evaVoiceAudioSources;

    [Header("Startup / Spam Protection")]
    [SerializeField] private float collabStartGracePeriod = 15f;

    [SerializeField] private float minimumGapBetweenProactiveHints = 35f;

    [SerializeField] private float minimumGapAfterEvaTrigger = 12f;

    [Header("Queue Behaviour")]
    [SerializeField] private bool queueLatestMessageWhileBusy = false;

    [Header("Debug")]
    [SerializeField] private bool logMessages = true;

    private static bool isUserSpeaking = false;
    private static bool isEvaSpeakingManual = false;
    private static float collabEnabledAt = -999f;
    private static float nextAllowedProactiveHintTime = 0f;
    private static float lastUserSpeechTime = -999f;
    private static float lastEvaTriggerTime = -999f;
    private static float lastEvaSpeechTime = -999f;
    private static float lastAnySpeechOrTriggerTime = -999f;

    private string queuedMessage = "";
    private bool queuedAllowInToolMode = false;
    private bool queuedBypassCooldown = false;

    public static bool IsCollab
    {
        get
        {
            return Instance != null && Instance.isCollab;
        }
    }

    public static bool IsToolMode
    {
        get
        {
            return !IsCollab;
        }
    }

    public static bool IsUserSpeaking { get { return isUserSpeaking; } }

    public static bool IsEvaSpeaking
    {
        get
        {
            if (Instance == null)
            {
                return false;
            }

            return isEvaSpeakingManual || Instance.IsEvaVoicePlayingByAudioSourceOnlyIfEnabled();
        }
    }

    public static bool IsAnyoneSpeaking { get { return IsUserSpeaking || IsEvaSpeaking; } }

    public static bool IsInCollabGracePeriod
    {
        get
        {
            return IsCollab && Time.time < collabEnabledAt + (Instance != null ? Instance.collabStartGracePeriod : 10f);
        }
    }

    public static bool HasEnoughSilenceForProactiveHint
    {
        get
        {
            if (Instance == null)
            {
                return false;
            }

            if (Instance.blockWhileUserIsSpeaking && isUserSpeaking)
            {
                return false;
            }

            if (Instance.blockWhileEvaIsSpeaking && IsEvaSpeaking)
            {
                return false;
            }

            return Time.time >= lastAnySpeechOrTriggerTime + Instance.requiredSilentSecondsBeforeSend;
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
        ApplyInspectorCondition();
        MarkActivityNow();
    }

    private void Start()
    {
        ApplyInspectorCondition();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying && Instance == this)
        {
            ApplyInspectorCondition();
        }
    }
#endif

    private void Update()
    {
        // C/T keyboard switching removed.
        TrySendQueuedMessageWhenSafe();
    }

    private void ApplyInspectorCondition()
    {
        MarkActivityNow();

        if (isCollab)
        {
            collabEnabledAt = Time.time;
            nextAllowedProactiveHintTime = Time.time + collabStartGracePeriod;

            if (logMessages)
            {
                Debug.Log("[EVA] Collaboration mode enabled from Inspector.");
            }
        }
        else
        {
            ClearQueuedMessage();

            if (logMessages)
            {
                Debug.Log("[EVA] Tool mode enabled from Inspector.");
            }
        }
    }

    public void SetCollaborationModeFromInspector(bool enabled)
    {
        isCollab = enabled;
        ApplyInspectorCondition();
    }

    // Falls andere Scripts schon EvaSystemEventSender.SetCollaborationMode(...)
    // aufrufen, kannst du diese Methode behalten.
    public static void SetCollaborationMode(bool enabled)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[EVA] No EvaSystemEventSender found in scene.");
            return;
        }

        Instance.SetCollaborationModeFromInspector(enabled);
    }

    public static void NotifyUserSpeechStarted()
    {
        isUserSpeaking = true;
        lastUserSpeechTime = Time.time;
        MarkActivityNow();

        if (Instance != null && Instance.logMessages)
        {
            Debug.Log("[EVA] User speech started. Proactive hints blocked.");
        }
    }

    public static void NotifyUserSpeechEnded()
    {
        isUserSpeaking = false;
        lastUserSpeechTime = Time.time;
        MarkActivityNow();

        if (Instance != null && Instance.logMessages)
        {
            Debug.Log("[EVA] User speech ended. Silence timer restarted.");
        }
    }

    public static void NotifyUserMessageSubmitted()
    {
        isUserSpeaking = false;
        lastUserSpeechTime = Time.time;
        MarkActivityNow();

        if (Instance != null && Instance.logMessages)
        {
            Debug.Log("[EVA] User message submitted. Silence timer restarted.");
        }
    }

    public static void NotifyEvaSpeechStarted()
    {
        isEvaSpeakingManual = true;
        lastEvaSpeechTime = Time.time;
        MarkActivityNow();

        if (Instance != null && Instance.logMessages)
        {
            Debug.Log("[EVA] EVA speech started. Proactive hints blocked.");
        }
    }

    public static void NotifyEvaSpeechEnded()
    {
        isEvaSpeakingManual = false;
        lastEvaSpeechTime = Time.time;
        MarkActivityNow();

        if (Instance != null && Instance.logMessages)
        {
            Debug.Log("[EVA] EVA speech ended. Silence timer restarted.");
        }
    }

    public static void Send(string message)
    {
        Send(message, false, false);
    }

    public static void Send(string message, bool allowInToolMode)
    {
        Send(message, allowInToolMode, false);
    }

    public static void Send(string message, bool allowInToolMode, bool bypassCooldown)
    {
        if (!IsCollab && !allowInToolMode)
        {
            return;
        }

        if (Instance == null)
        {
            Debug.LogWarning("[EVA] No EvaSystemEventSender found in scene.");
            return;
        }

        Instance.SendOrQueueInternal(message, allowInToolMode, bypassCooldown);
    }

    public static void SendProactiveHint(string userFacingGermanMessage)
    {
        Send(userFacingGermanMessage, false, false);
    }

    public static void SendContextUpdate(string contextMessage)
    {
        if (Instance != null && Instance.logMessages)
        {
            Debug.Log("[EVA] Private context update suppressed. Do not send private/system text through NarrativeTrigger.");
        }
    }

    public static void SendForcedCongratulations(string userFacingGermanMessage)
    {
        Send(userFacingGermanMessage, true, true);
    }

    private void SendOrQueueInternal(string message, bool allowInToolMode, bool bypassCooldown)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (!bypassCooldown && Time.time < nextAllowedProactiveHintTime)
        {
            LogSkip("cooldown/grace period is active");
            return;
        }

        if (!bypassCooldown && Time.time < lastEvaTriggerTime + minimumGapAfterEvaTrigger)
        {
            LogSkip("EVA was triggered recently");
            return;
        }

        if (!bypassCooldown && !CanSendNow())
        {
            if (queueLatestMessageWhileBusy)
            {
                QueueLatestMessage(message, allowInToolMode, bypassCooldown);
            }
            else
            {
                LogSkip("user/EVA is speaking or silence period is too short");
            }

            return;
        }

        SendInternal(message);

        if (!bypassCooldown)
        {
            nextAllowedProactiveHintTime = Time.time + minimumGapBetweenProactiveHints;
        }
    }

    private bool CanSendNow()
    {
        if (blockWhileUserIsSpeaking && isUserSpeaking)
        {
            return false;
        }

        if (blockWhileEvaIsSpeaking && IsEvaSpeaking)
        {
            return false;
        }

        return Time.time >= lastAnySpeechOrTriggerTime + requiredSilentSecondsBeforeSend;
    }

    private void QueueLatestMessage(string message, bool allowInToolMode, bool bypassCooldown)
    {
        queuedMessage = message;
        queuedAllowInToolMode = allowInToolMode;
        queuedBypassCooldown = bypassCooldown;

        if (logMessages)
        {
            Debug.Log("[EVA] Proactive hint queued until manual silence guard allows it.");
        }
    }

    private void ClearQueuedMessage()
    {
        queuedMessage = "";
        queuedAllowInToolMode = false;
        queuedBypassCooldown = false;
    }

    private void TrySendQueuedMessageWhenSafe()
    {
        if (string.IsNullOrEmpty(queuedMessage))
        {
            return;
        }

        if (!IsCollab && !queuedAllowInToolMode)
        {
            ClearQueuedMessage();
            return;
        }

        if (!queuedBypassCooldown && Time.time < nextAllowedProactiveHintTime)
        {
            return;
        }

        if (!queuedBypassCooldown && Time.time < lastEvaTriggerTime + minimumGapAfterEvaTrigger)
        {
            return;
        }

        if (!queuedBypassCooldown && !CanSendNow())
        {
            return;
        }

        string messageToSend = queuedMessage;
        bool bypassCooldown = queuedBypassCooldown;
        ClearQueuedMessage();

        SendInternal(messageToSend);

        if (!bypassCooldown)
        {
            nextAllowedProactiveHintTime = Time.time + minimumGapBetweenProactiveHints;
        }
    }

    private bool IsEvaVoicePlayingByAudioSourceOnlyIfEnabled()
    {
        if (!useAudioSourceSpeakingGuard)
        {
            return false;
        }

        if (evaVoiceAudioSources == null || evaVoiceAudioSources.Length == 0)
        {
            return false;
        }

        foreach (AudioSource source in evaVoiceAudioSources)
        {
            if (source != null && source.isPlaying)
            {
                return true;
            }
        }

        return false;
    }

    private void SendInternal(string message)
    {
        if (narrativeTrigger == null)
        {
            Debug.LogWarning("[EVA] ConvaiNarrativeDesignTrigger is not assigned.");
            return;
        }

        string finalMessage = message.Trim();

        if (logMessages)
        {
            Debug.Log("[EVA] Sending safe EVA trigger text: " + finalMessage);
        }

        narrativeTrigger.ResetTrigger();
        narrativeTrigger.SetTriggerMessage(finalMessage);

        bool success = narrativeTrigger.InvokeTrigger();

        lastEvaTriggerTime = Time.time;
        MarkActivityNow();

        if (!success)
        {
            Debug.LogWarning("[EVA] Failed to invoke Convai narrative trigger.");
        }
        else
        {
            StudyCounters.AddInteraction();
        }
    }

    private void LogSkip(string reason)
    {
        if (logMessages)
        {
            Debug.Log("[EVA] Proactive hint skipped because " + reason + ".");
        }
    }

    private static void MarkActivityNow()
    {
        lastAnySpeechOrTriggerTime = Time.time;
    }
}