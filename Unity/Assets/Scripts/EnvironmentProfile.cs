using System;
using System.Collections.Generic;
using UnityEngine;

public enum ExperienceEnvironment
{
    Forest,
    Mountain,
    Ski,
    Pool,
    Ocean,
    Beach,
    Desert,
    Rain,
    Custom
}

[CreateAssetMenu(
    fileName = "EnvironmentProfile",
    menuName = "HackGT/Environment Profile"
)]
public class EnvironmentProfile : ScriptableObject
{
    [Header("Identity")]
    public string id = "forest";
    public string displayName = "Forest";
    public ExperienceEnvironment environmentType =
        ExperienceEnvironment.Forest;

    [Header("Environment Prefab")]
    [Tooltip(
        "Complete environment package. It should contain walls/scenery/FX, "
        + "but NOT a camera, XR rig, or AudioListener."
    )]
    public GameObject environmentPrefab;

    [Header("Environmental Conditions")]

    [Tooltip("Demonstration temperature, in degrees C.")]
    public float temperatureC = 20f;

    [Range(0f, 1f)]
    [Tooltip("0 = dry, 1 = saturated/humid.")]
    public float humidity = 0.5f;

    [Tooltip("Approximate wind speed used by the experience model.")]
    public float windSpeed = 2f;

    [Range(0f, 1f)]
    public float windGustiness = 0.2f;

    [Range(0f, 1f)]
    public float precipitation = 0f;

    [Range(0f, 1f)]
    [Tooltip("How immersed the user appears to be in water.")]
    public float waterLevel = 0f;

    [Tooltip("Used as an experiential altitude variable.")]
    public float altitudeMeters = 0f;

    [Range(0f, 1f)]
    [Tooltip("Mud / unstable ground intensity.")]
    public float mudLevel = 0f;

    [Range(0f, 1f)]
    public float snowLevel = 0f;

    [Header("Wind")]
    public Vector3 windDirection = Vector3.forward;

    [Header("Visual Character")]

    [Tooltip("Color cast used for deliberate environmental color interaction.")]
    public Color environmentalTint = Color.white;

    [Tooltip(
        "Approximate dominant scenery color used for camouflage/contrast feedback."
    )]
    public Color sceneryColor = Color.green;

    [Range(0f, 1f)]
    public float sceneryColorInfluence = 0.12f;

    [Header("Audio")]

    public AudioClip ambientLoop;
    public AudioClip windLoop;
    public AudioClip humidityLoop;
    public AudioClip waterLoop;

    [Range(0f, 1f)]
    public float ambientVolume = 0.8f;

    [Range(0f, 1f)]
    public float windVolume = 0.8f;

    [Range(0f, 1f)]
    public float humidityVolume = 0.25f;

    [Range(0f, 1f)]
    public float waterVolume = 0.9f;

    [Header("Timed Environmental Events")]
    public List<EnvironmentEvent> events =
        new List<EnvironmentEvent>();

    [Serializable]
    public class EnvironmentEvent
    {
        public string eventName = "Bird";

        [Tooltip("Time after entering environment.")]
        [Min(0f)]
        public float firstTime = 4f;

        [Tooltip(
            "When true, event will recur at a randomized interval."
        )]
        public bool repeat = false;

        [Min(0.1f)]
        public float repeatMinimum = 4f;

        [Min(0.1f)]
        public float repeatMaximum = 8f;

        public AudioClip audioClip;

        [Range(0f, 1f)]
        public float audioVolume = 1f;

        [Tooltip("Optional visual event prefab.")]
        public GameObject visualPrefab;

        [Tooltip("Relative position inside the active environment.")]
        public Vector3 localPosition;

        public Vector3 localEulerAngles;

        public Vector3 localScale = Vector3.one;

        [Tooltip("Destroy visual event after this many seconds.")]
        [Min(0f)]
        public float lifetime = 5f;

        [Header("Optional Haptic Response")]
        public bool hapticPulse = false;

        [Range(0f, 1f)]
        public float hapticAmplitude = 0.25f;

        [Min(0f)]
        public float hapticDuration = 0.1f;
    }
}