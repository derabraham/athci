using UnityEngine;

public class CampusEvaHintController : MonoBehaviour
{
    [Header("References")]
    public MeetingPointDebugTracker tracker;

    [Header("Silent Context Appending")]
    [Tooltip("Wenn true, wird jeder echten User-Nachricht ein privater Campus-Kontext angehängt, bevor sie an ConvAI geschickt wird.")]
    public bool appendPrivateContextToUserMessages = true;

    [Tooltip("Tool-EVA: Kontext wird nur bei echten User-Fragen angehängt. EVA spricht dadurch nicht von allein.")]
    public bool appendContextInToolMode = true;

    [Tooltip("Collaboration-EVA: Kontext wird ebenfalls bei User-Nachrichten angehängt; zusätzliche proaktive Hinweise bleiben selten.")]
    public bool appendContextInCollaborationMode = true;

    [Tooltip("Nur zum Debuggen. Nicht dauerhaft aktiv lassen, weil hier der gesamte Prompt geloggt wird.")]
    public bool logPreparedConvaiMessages = false;

    [Header("Automatic Proactive Hints")]
    public bool automaticProactiveHintsEnabled = true;

    [Tooltip("Private context is NOT sent through NarrativeTrigger, because that trigger may be spoken aloud.")]
    public bool sendRegularContextUpdates = false;

    [Header("Proactive Hint Timing")]
    public float hintDelay = 45f;
    public float hintCooldown = 40f;

    [Tooltip("Extra silence check before proactive hints. EvaSystemEventSender also has its own silence guard.")]
    public float localSilentSecondsBeforeHint = 6f;

    [Header("Progress Tracking")]
    public float improvementThreshold = 2f;
    public float movingAwayThreshold = 3f;

    [Header("Distance Milestones")]
    public float nearDistance = 15f;
    public float veryNearDistance = 8f;
    public float almostThereDistance = 4f;

    [Header("Debug Test")]
    public bool allowHKeyTest = true;

    private float bestDistanceToMeetingPoint = float.MaxValue;
    private float lastCheckedDistance = float.MaxValue;
    private float timeWithoutProgress = 0f;
    private float nextHintAllowedTime = 0f;
    private float lastLocalSpeechOrHintTime = -999f;

    private bool meetingPointReached = false;
    private bool plausibleZoneHintSent = false;
    private bool veryNearHintSent = false;
    private bool almostThereHintSent = false;

    private void Update()
    {
        if (tracker == null)
        {
            return;
        }

        if (allowHKeyTest && Input.GetKeyDown(KeyCode.H))
        {
            EvaSystemEventSender.Send(
                tracker.BuildSafeGermanHintForEva(),
                true,
                false
            );

            lastLocalSpeechOrHintTime = Time.time;
            return;
        }

        HandleMeetingPointReached();

        if (tracker.IsAtMeetingPoint)
        {
            return;
        }

        if (!EvaSystemEventSender.IsCollab)
        {
            lastCheckedDistance = tracker.CurrentDistanceToMeetingPoint;
            return;
        }

        if (EvaSystemEventSender.IsInCollabGracePeriod)
        {
            lastCheckedDistance = tracker.CurrentDistanceToMeetingPoint;
            return;
        }

        string movementAssessment = GetMovementAssessment();
        HandleProgressTracking();

        if (automaticProactiveHintsEnabled)
        {
            HandleSpecialEvents();
            HandleStuckHint(movementAssessment);
        }

        lastCheckedDistance = tracker.CurrentDistanceToMeetingPoint;
    }

    // Recommended: call this from your ConvAI text-submit / final-transcript code before sending the user's text to ConvAI.
    // It appends private campus context to REAL user messages only. This is the "silent append" path.
    public string BuildMessageForConvai(string rawUserMessage)
    {
        if (string.IsNullOrWhiteSpace(rawUserMessage))
        {
            return rawUserMessage;
        }

        if (tracker == null || !ShouldAppendPrivateContextNow())
        {
            return rawUserMessage;
        }

        string movementAssessment = GetMovementAssessment();
        string preparedMessage = rawUserMessage + tracker.BuildPrivateContextAppendixForUserMessage(rawUserMessage, movementAssessment);

        if (logPreparedConvaiMessages)
        {
            Debug.Log("[EVA] Prepared user message with silent private campus context:\n" + preparedMessage);
        }

        return preparedMessage;
    }

    // Convenience method if your hook is called exactly when the final user text is submitted.
    // This resets the speech/silence state once and returns the message with private context appended.
    public string PrepareUserMessageForConvai(string rawUserMessage)
    {
        EvaSystemEventSender.NotifyUserMessageSubmitted();
        return BuildMessageForConvai(rawUserMessage);
    }

    private bool ShouldAppendPrivateContextNow()
    {
        if (!appendPrivateContextToUserMessages)
        {
            return false;
        }

        if (EvaSystemEventSender.IsCollab)
        {
            return appendContextInCollaborationMode;
        }

        return appendContextInToolMode;
    }

    // Optional: call this when you receive the user's final speech transcript.
    // In collaboration mode, it can produce a safe spoken reaction to table-tennis mentions.
    public void NotifyUserMessageSubmitted(string rawUserMessage)
    {
        EvaSystemEventSender.NotifyUserMessageSubmitted();

        if (tracker == null || string.IsNullOrWhiteSpace(rawUserMessage))
        {
            return;
        }

        if (!EvaSystemEventSender.IsCollab)
        {
            return;
        }

        if (tracker.ContainsTableTennisKeyword(rawUserMessage) && CanSendLocalProactiveHint())
        {
            EvaSystemEventSender.SendProactiveHint(tracker.BuildSafeGermanTableTennisHintForEva());
            MarkHintSent();
        }
    }

    private void HandleMeetingPointReached()
    {
        if (!tracker.IsAtMeetingPoint || tracker.CurrentDistanceToMeetingPoint > tracker.meetingPointRadius)
        {
            meetingPointReached = false;
            return;
        }

        if (meetingPointReached)
        {
            return;
        }

        meetingPointReached = true;

        EvaSystemEventSender.SendForcedCongratulations(
            "Glückwunsch, jetzt bist du wirklich im Treffpunkt-Radius. Das ist der richtige Treffpunkt."
        );
    }

    private void HandleProgressTracking()
    {
        float currentDistance = tracker.CurrentDistanceToMeetingPoint;

        if (currentDistance < bestDistanceToMeetingPoint - improvementThreshold)
        {
            bestDistanceToMeetingPoint = currentDistance;
            timeWithoutProgress = 0f;
            return;
        }

        timeWithoutProgress += Time.deltaTime;
    }

    private void HandleSpecialEvents()
    {
        if (!CanSendLocalProactiveHint())
        {
            return;
        }

        float currentDistance = tracker.CurrentDistanceToMeetingPoint;

        if (tracker.IsSearchZonePlausible && !plausibleZoneHintSent)
        {
            plausibleZoneHintSent = true;
            EvaSystemEventSender.SendProactiveHint(
                "Diese Suchzone wirkt plausibel, aber der Treffpunkt ist noch nicht bestätigt. Schau dich hier weiter vorsichtig um."
            );
            MarkHintSent();
            return;
        }

        if (currentDistance <= veryNearDistance && !veryNearHintSent)
        {
            veryNearHintSent = true;
            EvaSystemEventSender.SendProactiveHint(
                "Du bist in einem interessanten Bereich, aber bestätigt ist der Treffpunkt noch nicht. Vergleich die Hinweise weiter."
            );
            MarkHintSent();
            return;
        }

        if (currentDistance <= almostThereDistance && !almostThereHintSent)
        {
            almostThereHintSent = true;
            EvaSystemEventSender.SendProactiveHint(
                "Die unmittelbare Umgebung ist interessant, aber ich würde den Treffpunkt noch nicht bestätigen. Schau weiter genau hin."
            );
            MarkHintSent();
        }
    }

    private void HandleStuckHint(string movementAssessment)
    {
        if (timeWithoutProgress < hintDelay || !CanSendLocalProactiveHint())
        {
            return;
        }

        EvaSystemEventSender.SendProactiveHint(
            tracker.BuildSafeGermanHintForEva()
        );

        MarkHintSent();
        timeWithoutProgress = 0f;
    }

    private bool CanSendLocalProactiveHint()
    {
        if (Time.time < nextHintAllowedTime)
        {
            return false;
        }

        if (!EvaSystemEventSender.HasEnoughSilenceForProactiveHint)
        {
            return false;
        }

        return Time.time >= lastLocalSpeechOrHintTime + localSilentSecondsBeforeHint;
    }

    private void MarkHintSent()
    {
        lastLocalSpeechOrHintTime = Time.time;
        nextHintAllowedTime = Time.time + hintCooldown;
    }

    private string GetMovementAssessment()
    {
        if (tracker == null)
        {
            return "There is no tracker available for movement assessment.";
        }

        // Prefer the rolling history from MeetingPointDebugTracker instead of frame-to-frame comparison.
        // Frame-to-frame distance changes are usually too small and caused the assistant to get weak/unclear context.
        string historyTrend = tracker.BuildMovementTrendForEva(tracker.shortTrendWindowSeconds);

        if (timeWithoutProgress >= hintDelay)
        {
            return historyTrend + " The user also seems unsure or has not made clear progress for a while.";
        }

        return historyTrend;
    }
}
