using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LightingController : MonoBehaviour
{
    public GameObject directionalLightTag;
    public GameObject directionalLightEvening;
    public GameObject Laterne1;
    public GameObject Laterne2;
    public GameObject Laterne3;
    public GameObject Laterne4;
    public GameObject Laterne5;
    public GameObject Laterne6;
    public GameObject Laterne7;
    public GameObject Laterne8;
    public GameObject Laterne9;
    public GameObject Laterne10;
    public GameObject Laterne11;
    public GameObject Laterne12;
    public GameObject Laterne13;
    public GameObject Laterne14;
    public GameObject Laterne15;
    public GameObject Laterne16;
    public GameObject Laterne17;
    public GameObject Laterne18;
    public GameObject Laterne19;
    public GameObject Laterne20;
    public GameObject Laterne21;
    public GameObject Laterne22;
    public GameObject Laterne23;
    public GameObject Laterne24;
    public GameObject Laterne25;
    public GameObject Laterne26;
    public GameObject Laterne27;
    public GameObject Laterne28;
    public Material daySkybox;
    public Material eveningSkybox;
    public Material nightSkybox;
    private Dropdown lightingDropdown;

    private void Start()
    {
        lightingDropdown = GetComponent<Dropdown>();
        lightingDropdown.onValueChanged.AddListener(ChangeLightingState);
        directionalLightTag.SetActive(true);
        directionalLightEvening.SetActive(false);
        Laterne1.SetActive(false);
        Laterne2.SetActive(false);
        Laterne3.SetActive(false);
        Laterne4.SetActive(false);
        Laterne5.SetActive(false);
        Laterne6.SetActive(false);
        Laterne7.SetActive(false);
        Laterne8.SetActive(false);
        Laterne9.SetActive(false);
        Laterne10.SetActive(false);
        Laterne11.SetActive(false);
        Laterne12.SetActive(false);
        Laterne13.SetActive(false);
        Laterne14.SetActive(false);
        Laterne15.SetActive(false);
        Laterne16.SetActive(false);
        Laterne17.SetActive(false);
        Laterne18.SetActive(false);
        Laterne19.SetActive(false);
        Laterne20.SetActive(false);
        Laterne21.SetActive(false);
        Laterne22.SetActive(false);
        Laterne23.SetActive(false);
        Laterne24.SetActive(false);
        Laterne25.SetActive(false);
        Laterne26.SetActive(false);
        Laterne27.SetActive(false);
        Laterne28.SetActive(false);
        RenderSettings.skybox = daySkybox;
    }

    private void ChangeLightingState(int index)
    {
        switch (index)
        {
            case 0: // Tag ausgewählt
                directionalLightTag.SetActive(true);
                directionalLightEvening.SetActive(false);
                RenderSettings.skybox = index == 0 ? daySkybox : daySkybox;
                RenderSettings.ambientIntensity = 1f; // will make it light again
                RenderSettings.reflectionIntensity = 1f; // will make it light again
                Laterne1.SetActive(false);
                Laterne2.SetActive(false);
                Laterne3.SetActive(false);
                Laterne4.SetActive(false);
                Laterne5.SetActive(false);
                Laterne6.SetActive(false);
                Laterne7.SetActive(false);
                Laterne8.SetActive(false);
                Laterne9.SetActive(false);
                Laterne10.SetActive(false);
                Laterne11.SetActive(false);
                Laterne12.SetActive(false);
                Laterne13.SetActive(false);
                Laterne14.SetActive(false);
                Laterne15.SetActive(false);
                Laterne16.SetActive(false);
                Laterne17.SetActive(false);
                Laterne18.SetActive(false);
                Laterne19.SetActive(false);
                Laterne20.SetActive(false);
                Laterne21.SetActive(false);
                Laterne22.SetActive(false);
                Laterne23.SetActive(false);
                Laterne24.SetActive(false);
                Laterne25.SetActive(false);
                Laterne26.SetActive(false);
                Laterne27.SetActive(false);
                Laterne28.SetActive(false);
                break;
            case 1: // Abend ausgewählt
                directionalLightTag.SetActive(false);
                directionalLightEvening.SetActive(true);
                RenderSettings.skybox = index == 0 ? daySkybox : eveningSkybox;
                RenderSettings.ambientIntensity = 0.75f; // Will make it dark
                RenderSettings.reflectionIntensity = 0.75f; // will make it dark
                Laterne1.SetActive(false);
                Laterne2.SetActive(false);
                Laterne3.SetActive(false);
                Laterne4.SetActive(false);
                Laterne5.SetActive(false);
                Laterne6.SetActive(false);
                Laterne7.SetActive(false);
                Laterne8.SetActive(false);
                Laterne9.SetActive(false);
                Laterne10.SetActive(false);
                Laterne11.SetActive(false);
                Laterne12.SetActive(false);
                Laterne13.SetActive(false);
                Laterne14.SetActive(false);
                Laterne15.SetActive(false);
                Laterne16.SetActive(false);
                Laterne17.SetActive(false);
                Laterne18.SetActive(false);
                Laterne19.SetActive(false);
                Laterne20.SetActive(false);
                Laterne21.SetActive(false);
                Laterne22.SetActive(false);
                Laterne23.SetActive(false);
                Laterne24.SetActive(false);
                Laterne25.SetActive(false);
                Laterne26.SetActive(false);
                Laterne27.SetActive(false);
                Laterne28.SetActive(false);
                break;
            case 2: // Nacht ausgewählt
                directionalLightTag.SetActive(false);
                directionalLightEvening.SetActive(false);
                RenderSettings.skybox = index == 0 ? daySkybox : nightSkybox;
                RenderSettings.ambientIntensity = 0.175f; // Will make it dark
                RenderSettings.reflectionIntensity = 0.175f; // will make it dark

                Laterne1.SetActive(true);
                Laterne2.SetActive(true);
                Laterne3.SetActive(true);
                Laterne4.SetActive(true);
                Laterne5.SetActive(true);
                Laterne6.SetActive(true);
                Laterne7.SetActive(true);
                Laterne8.SetActive(true);
                Laterne9.SetActive(true);
                Laterne10.SetActive(true);
                Laterne11.SetActive(true);
                Laterne12.SetActive(true);
                Laterne13.SetActive(true);
                Laterne14.SetActive(true);
                Laterne15.SetActive(true);
                Laterne16.SetActive(true);
                Laterne17.SetActive(true);
                Laterne18.SetActive(true);
                Laterne19.SetActive(true);
                Laterne20.SetActive(true);
                Laterne21.SetActive(true);
                Laterne22.SetActive(true);
                Laterne23.SetActive(true);
                Laterne24.SetActive(true);
                Laterne25.SetActive(true);
                Laterne26.SetActive(true);
                Laterne27.SetActive(true);
                Laterne28.SetActive(true);
                break;
        }
    }
}