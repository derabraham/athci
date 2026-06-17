using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

public class MeetingPointCsvLogger : MonoBehaviour
{
    [Header("Tracking")]
    [Tooltip("Am besten Main Camera eintragen. Rotation/Kopfdrehen zählt nicht, weil nur X/Z-Position geprüft wird.")]
    public Transform trackingTransform;

    [Tooltip("Ab welcher Positionsänderung das Experiment als gestartet gilt.")]
    public float movementStartThreshold = 0.15f;

    [Tooltip("Wie oft geloggt werden soll. 1 = jede Sekunde.")]
    public float logIntervalSeconds = 1f;

    [Header("Campus References")]
    public Transform parkhausPoint;

    [Header("Distance Rules")]
    public float minDistanceToParkhaus = 25f;
    public float maxDistanceToParkhaus = 45f;
    public float extraDistanceLimit = 500f;

    [Header("Visibility Objects")]
    public GameObject mensaObject;
    public GameObject building2Object;

    [Header("Visibility Settings")]
    public LayerMask visibilityBlockers;
    public bool drawDebugLines = false;

    [Header("Terrain Ground Check")]
    public Terrain terrain;
    public string asphaltTerrainLayerName = "Pebbles_C_TerrainLayer";

    [Range(0f, 1f)]
    public float asphaltThreshold = 0.5f;

    [Header("CSV")]
    public string filePrefix = "MeetingPointExperiment";

    [Tooltip("Wenn leer, wird Application.persistentDataPath benutzt.")]
    public string customSaveFolder = @"";

    private Vector3 initialPosition;
    private bool experimentStarted = false;
    private float experimentStartTime;
    private float nextLogTime;
    private string currentCsvPath = "";

    private readonly List<string> csvLines = new List<string>();

    private void Start()
    {
        if (trackingTransform != null)
        {
            initialPosition = trackingTransform.position;
        }
        else
        {
            Debug.LogWarning("MeetingPointCsvLogger: Kein Tracking Transform gesetzt.");
        }

        csvLines.Add(
            "elapsedSeconds," +
            "posX,posY,posZ," +
            "distanceToParkhaus," +
            "moreThanMinParkhausDistance," +
            "lessThanMaxParkhausDistance," +
            "inCorrectParkhausRange," +
            "lessThan500mFromParkhaus," +
            "moreThan500mFromParkhaus," +
            "terrainLayer," +
            "asphaltStrength," +
            "onAsphalt," +
            "mensaVisible," +
            "building2Visible," +
            "searchZonePlausible," +
            "failedConditions"
        );

        Debug.Log("CSV Zielordner: " + GetFolderPath());
    }

    private void Update()
    {
        if (trackingTransform == null || parkhausPoint == null)
        {
            return;
        }

        if (!experimentStarted)
        {
            TryStartExperimentAfterMovement();
            return;
        }

        if (Time.time >= nextLogTime)
        {
            LogCurrentState();
            SaveCsv();
            nextLogTime = Time.time + logIntervalSeconds;
        }
    }

    private void TryStartExperimentAfterMovement()
    {
        Vector3 currentPosition = trackingTransform.position;

        Vector2 startXZ = new Vector2(initialPosition.x, initialPosition.z);
        Vector2 currentXZ = new Vector2(currentPosition.x, currentPosition.z);

        float movedDistance = Vector2.Distance(startXZ, currentXZ);

        if (movedDistance >= movementStartThreshold)
        {
            experimentStarted = true;
            experimentStartTime = Time.time;
            nextLogTime = Time.time + logIntervalSeconds;

            Debug.Log("MeetingPoint Experiment gestartet durch erste Bewegung. Bewegung: " + movedDistance.ToString("F3") + " m");

            LogCurrentState();
            SaveCsv();
        }
    }

    private void LogCurrentState()
    {
        Vector3 pos = trackingTransform.position;

        float elapsedSeconds = Time.time - experimentStartTime;
        float distanceToParkhaus = Vector3.Distance(pos, parkhausPoint.position);

        bool moreThanMinDistance = distanceToParkhaus > minDistanceToParkhaus;
        bool lessThanMaxDistance = distanceToParkhaus < maxDistanceToParkhaus;
        bool inCorrectParkhausRange = moreThanMinDistance && lessThanMaxDistance;

        bool lessThan500m = distanceToParkhaus < extraDistanceLimit;
        bool moreThan500m = distanceToParkhaus > extraDistanceLimit;

        string terrainLayer = GetDominantTerrainLayerName(pos, out float dominantStrength);
        bool onAsphalt = IsStandingOnAsphalt(pos, out float asphaltStrength);

        bool mensaVisible = IsObjectVisibleFromPosition(pos, mensaObject);
        bool building2Visible = IsObjectVisibleFromPosition(pos, building2Object);

        bool searchZonePlausible =
            inCorrectParkhausRange &&
            onAsphalt &&
            !mensaVisible &&
            building2Visible;

        string failedConditions = GetFailedConditions(
            moreThanMinDistance,
            lessThanMaxDistance,
            onAsphalt,
            mensaVisible,
            building2Visible
        );

        string line =
            FormatFloat(elapsedSeconds) + "," +
            FormatFloat(pos.x) + "," +
            FormatFloat(pos.y) + "," +
            FormatFloat(pos.z) + "," +
            FormatFloat(distanceToParkhaus) + "," +
            Bool(moreThanMinDistance) + "," +
            Bool(lessThanMaxDistance) + "," +
            Bool(inCorrectParkhausRange) + "," +
            Bool(lessThan500m) + "," +
            Bool(moreThan500m) + "," +
            EscapeCsv(terrainLayer) + "," +
            FormatFloat(asphaltStrength) + "," +
            Bool(onAsphalt) + "," +
            Bool(mensaVisible) + "," +
            Bool(building2Visible) + "," +
            Bool(searchZonePlausible) + "," +
            EscapeCsv(failedConditions);

        csvLines.Add(line);
    }

    private string GetFailedConditions(
        bool moreThanMinDistance,
        bool lessThanMaxDistance,
        bool onAsphalt,
        bool mensaVisible,
        bool building2Visible
    )
    {
        List<string> failed = new List<string>();

        if (!moreThanMinDistance)
        {
            failed.Add("TooCloseToParkhaus");
        }

        if (!lessThanMaxDistance)
        {
            failed.Add("TooFarFromParkhaus");
        }

        if (!onAsphalt)
        {
            failed.Add("NotOnAsphalt");
        }

        if (mensaVisible)
        {
            failed.Add("MensaVisible");
        }

        if (!building2Visible)
        {
            failed.Add("Building2NotVisible");
        }

        return string.Join("|", failed);
    }

    private bool IsObjectVisibleFromPosition(Vector3 observerPosition, GameObject targetObject)
    {
        if (targetObject == null)
        {
            return false;
        }

        Collider[] colliders = targetObject.GetComponentsInChildren<Collider>();

        if (colliders == null || colliders.Length == 0)
        {
            return false;
        }

        Vector3 origin = observerPosition + Vector3.up * 1.6f;

        foreach (Collider collider in colliders)
        {
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            Vector3[] checkPoints = GetColliderCheckPoints(collider);

            foreach (Vector3 point in checkPoints)
            {
                if (HasLineOfSight(origin, targetObject, point))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private Vector3[] GetColliderCheckPoints(Collider collider)
    {
        Bounds bounds = collider.bounds;

        Vector3 center = bounds.center;
        Vector3 ext = bounds.extents;

        return new Vector3[]
        {
            center,
            center + new Vector3(0f, ext.y * 0.8f, 0f),
            center + new Vector3(ext.x * 0.8f, 0f, 0f),
            center + new Vector3(-ext.x * 0.8f, 0f, 0f),
            center + new Vector3(0f, 0f, ext.z * 0.8f),
            center + new Vector3(0f, 0f, -ext.z * 0.8f),
            center + new Vector3(ext.x * 0.6f, ext.y * 0.6f, ext.z * 0.6f),
            center + new Vector3(-ext.x * 0.6f, ext.y * 0.6f, -ext.z * 0.6f)
        };
    }

    private bool HasLineOfSight(Vector3 origin, GameObject targetObject, Vector3 targetPoint)
    {
        Vector3 direction = targetPoint - origin;
        float distance = direction.magnitude;

        if (distance <= 0.01f)
        {
            return false;
        }

        if (drawDebugLines)
        {
            Debug.DrawLine(origin, targetPoint, Color.yellow);
        }

        if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, visibilityBlockers, QueryTriggerInteraction.Ignore))
        {
            bool hitTarget =
                hit.transform.gameObject == targetObject ||
                hit.transform.IsChildOf(targetObject.transform);

            if (drawDebugLines)
            {
                Debug.DrawLine(origin, hit.point, hitTarget ? Color.green : Color.red);
            }

            return hitTarget;
        }

        return false;
    }

    private bool IsStandingOnAsphalt(Vector3 worldPosition, out float asphaltStrength)
    {
        asphaltStrength = 0f;

        if (terrain == null || terrain.terrainData == null)
        {
            return false;
        }

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPosition = terrain.transform.position;

        int mapX = Mathf.RoundToInt(
            ((worldPosition.x - terrainPosition.x) / terrainData.size.x) * (terrainData.alphamapWidth - 1)
        );

        int mapZ = Mathf.RoundToInt(
            ((worldPosition.z - terrainPosition.z) / terrainData.size.z) * (terrainData.alphamapHeight - 1)
        );

        mapX = Mathf.Clamp(mapX, 0, terrainData.alphamapWidth - 1);
        mapZ = Mathf.Clamp(mapZ, 0, terrainData.alphamapHeight - 1);

        float[,,] splatmapData = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
        TerrainLayer[] terrainLayers = terrainData.terrainLayers;

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            string layerName = terrainLayers[i] != null ? terrainLayers[i].name : "";

            if (layerName.Contains(asphaltTerrainLayerName))
            {
                asphaltStrength = splatmapData[0, 0, i];
                return asphaltStrength >= asphaltThreshold;
            }
        }

        return false;
    }

    private string GetDominantTerrainLayerName(Vector3 worldPosition, out float dominantStrength)
    {
        dominantStrength = 0f;

        if (terrain == null || terrain.terrainData == null)
        {
            return "Kein Terrain gesetzt";
        }

        TerrainData terrainData = terrain.terrainData;
        Vector3 terrainPosition = terrain.transform.position;

        int mapX = Mathf.RoundToInt(
            ((worldPosition.x - terrainPosition.x) / terrainData.size.x) * (terrainData.alphamapWidth - 1)
        );

        int mapZ = Mathf.RoundToInt(
            ((worldPosition.z - terrainPosition.z) / terrainData.size.z) * (terrainData.alphamapHeight - 1)
        );

        mapX = Mathf.Clamp(mapX, 0, terrainData.alphamapWidth - 1);
        mapZ = Mathf.Clamp(mapZ, 0, terrainData.alphamapHeight - 1);

        float[,,] splatmapData = terrainData.GetAlphamaps(mapX, mapZ, 1, 1);
        TerrainLayer[] terrainLayers = terrainData.terrainLayers;

        int dominantIndex = 0;
        float strongestValue = 0f;

        for (int i = 0; i < terrainLayers.Length; i++)
        {
            float value = splatmapData[0, 0, i];

            if (value > strongestValue)
            {
                strongestValue = value;
                dominantIndex = i;
            }
        }

        dominantStrength = strongestValue;

        if (terrainLayers.Length == 0 || terrainLayers[dominantIndex] == null)
        {
            return "Unbekannt";
        }

        return terrainLayers[dominantIndex].name;
    }

    private void OnApplicationQuit()
    {
        SaveCsv();
    }

    private void OnDisable()
    {
        if (experimentStarted)
        {
            SaveCsv();
        }
    }

    private void SaveCsv()
    {
        if (csvLines.Count <= 1)
        {
            Debug.Log("CSV nicht gespeichert: Noch keine Datenzeilen vorhanden.");
            return;
        }

        string folderPath = GetFolderPath();

        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        if (string.IsNullOrEmpty(currentCsvPath))
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string fileName = $"{filePrefix}_{timestamp}.csv";
            currentCsvPath = Path.Combine(folderPath, fileName);
        }

        File.WriteAllLines(currentCsvPath, csvLines, Encoding.UTF8);
    }

    private string GetFolderPath()
    {
        if (string.IsNullOrWhiteSpace(customSaveFolder))
        {
            return Application.persistentDataPath;
        }

        return customSaveFolder;
    }

    private string FormatFloat(float value)
    {
        return value.ToString("F3", CultureInfo.InvariantCulture);
    }

    private string Bool(bool value)
    {
        return value ? "true" : "false";
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        value = value.Replace("\"", "\"\"");

        if (value.Contains(",") || value.Contains(";") || value.Contains("\"") || value.Contains("\n"))
        {
            return "\"" + value + "\"";
        }

        return value;
    }
}