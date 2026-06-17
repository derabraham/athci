using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;
using UnityEngine.UI;

public class StudyMenuUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TMP_Dropdown participantDropdown;

    [Header("Start Behaviour")]
    [SerializeField] private GameObject menuRoot;
    [SerializeField] private bool loadSceneOnStart = false;
    [SerializeField] private string sceneToLoad;
    public Button startButton;
    public Button stopButton;

    [Header("VR Menu Toggle")]
    [SerializeField] private bool allowVrToggle = true;
    [SerializeField] private XRNode toggleControllerNode = XRNode.RightHand;

    private InputDevice toggleController;
    private bool wasPrimaryButtonPressed;

    private void Awake()
    {
        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonPressed);
        if (stopButton != null)
            stopButton.onClick.AddListener(OnStopButtonPressed);
    }

    private void Start()
    {
        TryInitializeController();
        GameObject targetMenu = menuRoot != null ? menuRoot : gameObject;
        resetButtons();
        targetMenu.SetActive(false);
    }

    private void Update()
    {
        HandleVRMenuToggle();
    }


    public void OnStopButtonPressed()
    {
        resetButtons();

        if (StudyLogger.Instance != null)
            StudyLogger.Instance.FinishTask(false);
    }

    public void resetButtons()
    {
        startButton.interactable = true;
        stopButton.interactable = false;
    }

    public void OnStartButtonPressed()
    {
        startButton.interactable = false;
        stopButton.interactable = true;
        string pid = GetParticipantID();

        StudyLogger.SetParticipantID(pid);

        if (loadSceneOnStart && !string.IsNullOrWhiteSpace(sceneToLoad))
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(sceneToLoad);
        }
        else
        {
            StudyLogger.Instance.StartTask();

            if (menuRoot != null)
                menuRoot.SetActive(false);
            else
                gameObject.SetActive(false);
        }
        
    }

    private void HandleVRMenuToggle()
    {
        if (!allowVrToggle)
            return;

        if (!toggleController.isValid)
            TryInitializeController();

        if (!toggleController.isValid)
            return;

        if (toggleController.TryGetFeatureValue(CommonUsages.secondaryButton, out bool isPressed))
        {
            if (isPressed && !wasPrimaryButtonPressed)
            {
                ToggleMenu();
            }

            wasPrimaryButtonPressed = isPressed;
        }
    }

    private void ToggleMenu()
    {
        GameObject targetMenu = menuRoot != null ? menuRoot : gameObject;
        targetMenu.SetActive(!targetMenu.activeSelf);
    }

    private void TryInitializeController()
    {
        toggleController = InputDevices.GetDeviceAtXRNode(toggleControllerNode);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (StudyLogger.Instance != null)
            StudyLogger.Instance.StartTask();
        else
            Debug.LogWarning("[StudyMenuUI] No StudyLogger found after scene load.");
    }

    private string GetParticipantID()
    {
        if (participantDropdown != null)
            return participantDropdown.options[participantDropdown.value].text;

        Debug.LogWarning("[StudyMenuUI] No participant input/dropdown assigned.");
        return "0";
    }
}