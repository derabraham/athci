using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// PotionBrewed will be hooked to the OnBrew event of the CauldronContent in the scene, and this script will take care
/// of dispatching the right prefab to the ObjectSpawner to make the potion spawn
/// </summary>
public class PotionSpawner : MonoBehaviour
{
    public GameObject PotionPrefab;
    public GameObject BadPotionPrefab;
    public ObjectSpawner SpawnerCorrect;
    public ObjectSpawner SpawnerIncorrect;

    public void PotionBrewed(CauldronContent.Recipe recipe)
    {
        if (recipe != null)
        {
            SpawnerCorrect.Prefab = PotionPrefab;
            SpawnerCorrect.enabled = true;
            SpawnerCorrect.Spawn();
            EvaSystemEventSender.Send("The player successfully brewed the right potion.");

        }
        else
        {
            SpawnerIncorrect.Prefab = BadPotionPrefab;
            SpawnerIncorrect.enabled = true;
            SpawnerIncorrect.Spawn();
            //EvaSystemEventSender.Send("The player failed to brew the correct potion, since they added the wrong ingredients.");
        }
    }
}
