using TMPro;
using UnityEngine;

public class EnvironmentalFeedback
    : MonoBehaviour
{
    [SerializeField]
    private ExperienceRoomController room;

    [Header("UI")]
    [SerializeField]
    private TMP_Text temperatureText;

    [SerializeField]
    private TMP_Text humidityText;

    [SerializeField]
    private TMP_Text windText;

    [SerializeField]
    private TMP_Text conditionText;

    [SerializeField]
    private TMP_Text styleText;

    [SerializeField]
    private TMP_Text fitText;

    private void Update()
    {
        if (room == null)
        {
            room =
                FindFirstObjectByType<
                    ExperienceRoomController>();

            if (room == null)
            {
                return;
            }
        }

        EnvironmentRuntimeState e =
            room.CurrentState;

        OutfitMetrics outfit =
            room.CurrentOutfit;

        if (temperatureText != null)
        {
            temperatureText.text =
                "TEMPERATURE\n" +
                e.temperatureC.ToString("0") +
                " °C";
        }

        if (humidityText != null)
        {
            humidityText.text =
                "HUMIDITY\n" +
                (
                    e.humidity * 100f
                ).ToString("0") +
                "%";
        }

        if (windText != null)
        {
            windText.text =
                "WIND\n" +
                e.windSpeed.ToString("0.0") +
                " m/s";
        }

        float cold =
            ExperienceModel.ColdExposure(
                e,
                outfit
            );

        float heat =
            ExperienceModel
                .HeatHumidityDiscomfort(
                    e,
                    outfit
                );

        float wet =
            ExperienceModel.Wetness(
                e,
                outfit
            );

        float slip =
            ExperienceModel.SlipRisk(
                e,
                outfit
            );

        if (conditionText != null)
        {
            conditionText.text =
                BuildConditionText(
                    cold,
                    heat,
                    wet,
                    slip
                );
        }

        if (styleText != null)
        {
            float camouflage =
                ExperienceModel
                    .CamouflageSimilarity(
                        outfit.dominantColor,
                        e.sceneryColor
                    );

            if (
                e.environmentType ==
                    ExperienceEnvironment.Forest
                &&
                camouflage > 0.80f
            )
            {
                styleText.text =
                    "CAMOUFLAGE ALERT\n" +
                    "You are becoming " +
                    "suspiciously shrub-like.";
            }
            else if (camouflage < 0.20f)
            {
                styleText.text =
                    "HIGH VISUAL CONTRAST\n" +
                    "Your outfit strongly " +
                    "stands out here.";
            }
            else
            {
                styleText.text =
                    "ENVIRONMENTAL CONTRAST\n" +
                    "Moderate.";
            }
        }

        if (fitText != null)
        {
            fitText.text =
                "FIT COMPATIBILITY\n" +
                room.ExternalFitScore
                    .ToString("0") +
                "%";
        }
    }

    private string BuildConditionText(
        float cold,
        float heat,
        float wet,
        float slip)
    {
        if (wet > 0.75f)
        {
            return
                "HIGH WATER EXPOSURE";
        }

        if (slip > 0.75f)
        {
            return
                "HIGH TERRAIN / SLIP RISK";
        }

        if (cold > 0.70f)
        {
            return
                "HIGH COLD EXPOSURE";
        }

        if (heat > 0.70f)
        {
            return
                "HIGH HEAT / HUMIDITY DISCOMFORT";
        }

        if (cold > 0.40f)
        {
            return
                "MODERATE COLD EXPOSURE";
        }

        return
            "ENVIRONMENTAL CONDITIONS\n" +
            "GENERALLY COMPATIBLE";
    }
}