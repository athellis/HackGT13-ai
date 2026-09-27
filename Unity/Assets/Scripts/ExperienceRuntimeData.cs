using System;
using UnityEngine;

[Serializable]
public class EnvironmentRuntimeState
{
    public ExperienceEnvironment environmentType;

    public float temperatureC;
    public float humidity;
    public float windSpeed;
    public float windGustiness;
    public float precipitation;
    public float waterLevel;
    public float altitudeMeters;
    public float mudLevel;
    public float snowLevel;

    public Vector3 windDirection;

    public Color environmentalTint;
    public Color sceneryColor;
    public float sceneryColorInfluence;

    public static EnvironmentRuntimeState FromProfile(
        EnvironmentProfile profile)
    {
        EnvironmentRuntimeState state =
            new EnvironmentRuntimeState();

        state.environmentType =
            profile.environmentType;

        state.temperatureC =
            profile.temperatureC;

        state.humidity =
            profile.humidity;

        state.windSpeed =
            profile.windSpeed;

        state.windGustiness =
            profile.windGustiness;

        state.precipitation =
            profile.precipitation;

        state.waterLevel =
            profile.waterLevel;

        state.altitudeMeters =
            profile.altitudeMeters;

        state.mudLevel =
            profile.mudLevel;

        state.snowLevel =
            profile.snowLevel;

        state.windDirection =
            profile.windDirection.normalized;

        state.environmentalTint =
            profile.environmentalTint;

        state.sceneryColor =
            profile.sceneryColor;

        state.sceneryColorInfluence =
            profile.sceneryColorInfluence;

        return state;
    }

    public void CopyFrom(EnvironmentRuntimeState other)
    {
        environmentType = other.environmentType;
        temperatureC = other.temperatureC;
        humidity = other.humidity;
        windSpeed = other.windSpeed;
        windGustiness = other.windGustiness;
        precipitation = other.precipitation;
        waterLevel = other.waterLevel;
        altitudeMeters = other.altitudeMeters;
        mudLevel = other.mudLevel;
        snowLevel = other.snowLevel;
        windDirection = other.windDirection;
        environmentalTint = other.environmentalTint;
        sceneryColor = other.sceneryColor;
        sceneryColorInfluence =
            other.sceneryColorInfluence;
    }

    public void Lerp(
        EnvironmentRuntimeState a,
        EnvironmentRuntimeState b,
        float t)
    {
        t = Mathf.Clamp01(t);

        temperatureC =
            Mathf.Lerp(a.temperatureC, b.temperatureC, t);

        humidity =
            Mathf.Lerp(a.humidity, b.humidity, t);

        windSpeed =
            Mathf.Lerp(a.windSpeed, b.windSpeed, t);

        windGustiness =
            Mathf.Lerp(a.windGustiness, b.windGustiness, t);

        precipitation =
            Mathf.Lerp(a.precipitation, b.precipitation, t);

        waterLevel =
            Mathf.Lerp(a.waterLevel, b.waterLevel, t);

        altitudeMeters =
            Mathf.Lerp(a.altitudeMeters, b.altitudeMeters, t);

        mudLevel =
            Mathf.Lerp(a.mudLevel, b.mudLevel, t);

        snowLevel =
            Mathf.Lerp(a.snowLevel, b.snowLevel, t);

        windDirection =
            Vector3.Slerp(
                a.windDirection,
                b.windDirection,
                t
            ).normalized;

        environmentalTint =
            Color.Lerp(
                a.environmentalTint,
                b.environmentalTint,
                t
            );

        sceneryColor =
            Color.Lerp(
                a.sceneryColor,
                b.sceneryColor,
                t
            );

        sceneryColorInfluence =
            Mathf.Lerp(
                a.sceneryColorInfluence,
                b.sceneryColorInfluence,
                t
            );
    }
}

[Serializable]
public class OutfitMetrics
{
    [Range(0f, 1f)]
    public float insulation = 0.5f;

    [Range(0f, 1f)]
    public float windResistance = 0.5f;

    [Range(0f, 1f)]
    public float waterproofing = 0.5f;

    [Range(0f, 1f)]
    public float breathability = 0.5f;

    [Range(0f, 1f)]
    public float traction = 0.5f;

    [Range(0f, 1f)]
    public float tightness = 0.5f;

    public Color dominantColor = Color.white;

    public void CopyFrom(OutfitMetrics other)
    {
        insulation = other.insulation;
        windResistance = other.windResistance;
        waterproofing = other.waterproofing;
        breathability = other.breathability;
        traction = other.traction;
        tightness = other.tightness;
        dominantColor = other.dominantColor;
    }
}