using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DroneModeController : MonoBehaviour
{
    public Camera droneCamera;
    public GameObject playerController;
    public GameObject droneUI;
    public GameObject SimpleSampleCharacterControl;
    public GameObject Drone;

    private CameraController cameraController;
    private bool slowModeActive = false; // Hinzugefügt

    private bool droneModeActive = false;

    void Start()
    {
        // Deaktiviere die Drohnenkamera und Steuerung zu Beginn
        droneCamera.enabled = false;
        playerController.SetActive(false);
        droneUI.SetActive(false);
        Drone.SetActive(false);
        cameraController = droneCamera.GetComponent<CameraController>();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C)) // Ändere die Taste nach Bedarf
        {
            ToggleDroneMode();
        }

        if (Input.GetKeyDown(KeyCode.X)) // Hinzugefügt
        {
            ToggleSlowMode();
        }
    }

    void ToggleDroneMode()
    {
        droneModeActive = !droneModeActive;

        if (droneModeActive)
        {
            droneCamera.enabled = true;
            playerController.SetActive(true);
            droneUI.SetActive(true);
            Drone.SetActive(true);
        }
        else
        {
            droneCamera.enabled = false;
            playerController.SetActive(false);
            droneUI.SetActive(false);
            Drone.SetActive(false);
        }
    }
    void ToggleSlowMode() // Hinzugefügt
    {
        slowModeActive = !slowModeActive;

        cameraController.SetSlowMode(slowModeActive);
    }
}