using System.Collections.Generic;
using UnityEngine;

public class EnvironmentManager : MonoBehaviour
{
    [System.Serializable]
    public class EnvironmentEntry
    {
        public string environmentId;
        public GameObject prefab;
    }

    [Header("Available Environments")]
    public List<EnvironmentEntry> environments =
        new List<EnvironmentEntry>();

    [Header("Testing")]
    public string startEnvironmentId = "";

    private GameObject currentEnvironment;


    private void Start()
    {
        if (!string.IsNullOrEmpty(startEnvironmentId))
        {
            LoadEnvironment(startEnvironmentId);
        }
    }


    public GameObject LoadEnvironment(
        string environmentId
    )
    {
        // Remove current environment
        if (currentEnvironment != null)
        {
            Destroy(currentEnvironment);

            currentEnvironment = null;
        }


        // Find requested environment
        foreach (EnvironmentEntry entry in environments)
        {
            if (
                entry.environmentId
                == environmentId
            )
            {
                if (entry.prefab == null)
                {
                    Debug.LogError(
                        "Prefab is missing for environment: "
                        + environmentId
                    );

                    return null;
                }


                // Spawn environment
                currentEnvironment =
                    Instantiate(
                        entry.prefab,
                        Vector3.zero,
                        Quaternion.identity
                    );


                // Get its metadata
                EnvironmentData data =
                    currentEnvironment
                        .GetComponent<EnvironmentData>();


                // Apply matching HDRI / skybox
                if (
                    data != null
                    && data.skyboxMaterial != null
                )
                {
                    RenderSettings.skybox =
                        data.skyboxMaterial;

                    DynamicGI.UpdateEnvironment();
                }


                Debug.Log(
                    "Loaded environment: "
                    + environmentId
                );


                // THIS is what EnvironmentApiController needs
                return currentEnvironment;
            }
        }


        Debug.LogError(
            "Environment not found: "
            + environmentId
        );


        return null;
    }


    public GameObject GetCurrentEnvironment()
    {
        return currentEnvironment;
    }
}