using UnityEngine;
using Convai.Modules.Narrative;

public class EvaSystemEventSender : MonoBehaviour
{
    public static EvaSystemEventSender Instance { get; private set; }

    [Header("Convai")]
    [SerializeField] private ConvaiNarrativeDesignTrigger narrativeTrigger;

    [Header("Manual Speaking Guard - Recommended")]
    [Tooltip("If true, proactive hints are blocked while NotifyUserSpeechStarted() has been called and NotifyUserSpeechEnded() has not been called yet.")]
    [SerializeField] private bool blockWhileUserIsSpeaking = false;

    [Tooltip("If true, proactive hints are blocked while NotifyEvaSpeechStarted() has been called and NotifyEvaSpeechEnded() has not been called yet.")]
    [SerializeField] private bool blockWhileEvaIsSpeaking = false;

    [Tooltip("How many seconds of silence are required after the last user speech, EVA trigger, or manual EVA speech event before a proactive hint may be sent.")]
    [SerializeField] private float requiredSilentSecondsBeforeSend = 5f;

    [Header("Optional AudioSource Guard - OFF by default")]
    [Tooltip("Keep this OFF unless you have a reliable EVA voice AudioSource that isPlaying is false when EVA is silent. ConvAI AudioSources can stay active forever and block all hints.")]
    [SerializeField] private bool useAudioSourceSpeakingGuard = false;

    [Tooltip("Only used when Use Audio Source Speaking Guard is enabled.")]
    [SerializeField] private AudioSource[] evaVoiceAudioSources;

    [Header("Startup / Spam Protection")]
    [Tooltip("Prevents immediate hint spam after enabling collaboration.")]
    [SerializeField] private float collabStartGracePeriod = 15f;

    [Tooltip("Minimum gap between two automatic proactive hints.")]
    [SerializeField] private float minimumGapBetweenProactiveHints = 35f;

    [Tooltip("Minimum gap after any EVA trigger. This prevents EVA from interrupting herself even if no voice callbacks are wired.")]
    [SerializeField] private float minimumGapAfterEvaTrigger = 12f;

    [Header("Queue Behaviour")]
    [Tooltip("If true, the latest blocked hint is sent later once all guards allow it. If false, blocked hints are discarded. Recommended: false for testing and predictable behaviour.")]
    [SerializeField] private bool queueLatestMessageWhileBusy = false;

    [Header("Debug")]
    [SerializeField] private bool logMessages = true;

    private static bool isCollab = false;
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

    public static bool IsCollab { get { return isCollab; } }
    public static bool IsToolMode { get { return !isCollab; } }
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
            return isCollab && Time.time < collabEnabledAt + (Instance != null ? Instance.collabStartGracePeriod : 10f);
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
        MarkActivityNow();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            SetCollaborationMode(true);
        }

        if (Input.GetKeyDown(KeyCode.T))
        {
            SetCollaborationMode(false);
        }

        TrySendQueuedMessageWhenSafe();
    }

    public static void SetCollaborationMode(bool enabled)
    {
        isCollab = enabled;
        MarkActivityNow();

        if (enabled)
        {
            collabEnabledAt = Time.time;
            nextAllowedProactiveHintTime = Time.time + (Instance != null ? Instance.collabStartGracePeriod : 10f);
            Debug.Log("[EVA] Collaboration mode enabled. Proactive hints are rare; private campus context should be appended silently to real user messages.");
        }
        else
        {
            if (Instance != null)
            {
                Instance.ClearQueuedMessage();
            }

            Debug.Log("[EVA] Tool mode enabled. EVA will not send proactive hints.");
        }
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
        if (!isCollab && !allowInToolMode)
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

        if (!isCollab && !queuedAllowInToolMode)
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
