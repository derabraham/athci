using System;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using Convai.Infrastructure.Networking;
public class StudyLogger : MonoBehaviour
{
    public static StudyLogger Instance { get; private set; }

    public static string ParticipantID { get; private set; } = "0";

    private float taskStartTime;
    private bool taskRunning;

    private string LogFolder => Path.Combine(Application.persistentDataPath, "StudyLogs");
    private string LogFilePath => Path.Combine(LogFolder, "study_log.csv");

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public static void SetParticipantID(string participantID)
    {
        ParticipantID = string.IsNullOrWhiteSpace(participantID) ? "0" : participantID.Trim();
    }

    public void StartTask()
    {
        StudyCounters.Reset();

        taskStartTime = Time.time;
        taskRunning = true;

        Debug.Log($"[StudyLogger] Task started. PID={ParticipantID}, Level={GetLevelID()}, Condition={GetConditionID()}");
    }

    public void FinishTask()
    {
        if (!taskRunning)
        {
            Debug.LogWarning("[StudyLogger] FinishTask called, but no task is running.");
            return;
        }

        float duration = Time.time - taskStartTime;
        taskRunning = false;

        WriteCsvLine(duration);

        Debug.Log($"[StudyLogger] Task finished. Duration={duration:F2}s");
    }

    private void WriteCsvLine(float durationSeconds)
    {
        Directory.CreateDirectory(LogFolder);

        bool fileExists = File.Exists(LogFilePath);

        using StreamWriter writer = new StreamWriter(LogFilePath, append: true);

        if (!fileExists)
        {
            writer.WriteLine("timestamp,pid,level_id,condition_id,task_time_seconds,num_questions,num_interactions");
        }

        string line =
            Csv(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")) + "," +
            Csv(ParticipantID) + "," +
            Csv(GetLevelID()) + "," +
            Csv(GetConditionID()) + "," +
            durationSeconds.ToString("F3", CultureInfo.InvariantCulture) + "," +
            StudyCounters.NumQuestions + "," +
            StudyCounters.NumInteractions;

        writer.WriteLine(line);

        Debug.Log($"[StudyLogger] CSV written to: {LogFilePath}");
    }

    private string GetLevelID()
    {
        return SceneManager.GetActiveScene().name;
    }

    private string GetConditionID()
    {
        return EvaSystemEventSender.IsCollab ? "A_CollabPartner" : "B_Tool";
    }

    private string Csv(string value)
    {
        if (value == null) return "";

        value = value.Replace("\"", "\"\"");

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            return $"\"{value}\"";

        return value;
    }
}