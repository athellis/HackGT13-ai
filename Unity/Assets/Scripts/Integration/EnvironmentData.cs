using UnityEngine;

public class EnvironmentData : MonoBehaviour
{
    [Header("Environment Identity")]
    public string environmentId;

    [Header("Visual Asset")]
    public Material skyboxMaterial;

    [Header("Climate")]
    public float temperature;
    public float humidity;
    public float wind;
    public float gustiness;
    public float precipitation;
    public float water;
    public float altitude;
    public float mud;
    public float snow;

    [Header("Wind")]
    public Vector3 windDirection;

    [Header("Visual")]
    public Color environmentColor = Color.white;
    public Color sceneryColor = Color.white;
}