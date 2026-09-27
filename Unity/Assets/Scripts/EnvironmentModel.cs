using UnityEngine;

public static class ExperienceModel
{
    public static float ColdExposure(
        EnvironmentRuntimeState environment,
        OutfitMetrics outfit)
    {
        float coldFromTemperature =
            Mathf.InverseLerp(
                16f,
                -15f,
                environment.temperatureC
            );

        float windFactor =
            Mathf.Clamp01(
                environment.windSpeed / 15f
            );

        float protection =
            Mathf.Clamp01(
                0.55f * outfit.insulation +
                0.45f * outfit.windResistance
            );

        float exposure =
            coldFromTemperature * 0.7f +
            coldFromTemperature *
            windFactor * 0.3f;

        return Mathf.Clamp01(
            exposure *
            (1f - protection)
        );
    }

    public static float HeatHumidityDiscomfort(
        EnvironmentRuntimeState environment,
        OutfitMetrics outfit)
    {
        float heat =
            Mathf.InverseLerp(
                22f,
                40f,
                environment.temperatureC
            );

        float restrictedBreathability =
            outfit.tightness *
            (1f - outfit.breathability);

        float humidityFactor =
            environment.humidity;

        return Mathf.Clamp01(
            heat *
            humidityFactor *
            (
                0.6f +
                0.4f * restrictedBreathability
            )
        );
    }

    public static float Wetness(
        EnvironmentRuntimeState environment,
        OutfitMetrics outfit)
    {
        float exposure =
            environment.waterLevel * 0.9f +
            environment.precipitation * 0.5f +
            environment.humidity * 0.1f;

        return Mathf.Clamp01(
            exposure *
            (1f - outfit.waterproofing)
        );
    }

    public static float SlipRisk(
        EnvironmentRuntimeState environment,
        OutfitMetrics outfit)
    {
        float groundHazard =
            Mathf.Clamp01(
                environment.mudLevel * 0.7f +
                environment.snowLevel * 0.3f
            );

        return Mathf.Clamp01(
            groundHazard *
            (1f - outfit.traction)
        );
    }

    public static float CamouflageSimilarity(
        Color outfitColor,
        Color environmentColor)
    {
        float h1, s1, v1;
        float h2, s2, v2;

        Color.RGBToHSV(
            outfitColor,
            out h1,
            out s1,
            out v1
        );

        Color.RGBToHSV(
            environmentColor,
            out h2,
            out s2,
            out v2
        );

        float hueDifference =
            Mathf.Abs(h1 - h2);

        hueDifference =
            Mathf.Min(
                hueDifference,
                1f - hueDifference
            );

        float hueSimilarity =
            1f -
            Mathf.Clamp01(
                hueDifference * 2f
            );

        float saturationSimilarity =
            1f -
            Mathf.Abs(
                s1 - s2
            );

        float brightnessSimilarity =
            1f -
            Mathf.Abs(
                v1 - v2
            );

        return Mathf.Clamp01(
            0.5f * hueSimilarity +
            0.25f * saturationSimilarity +
            0.25f * brightnessSimilarity
        );
    }
}