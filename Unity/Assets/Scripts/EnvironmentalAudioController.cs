using UnityEngine;

public class EnvironmentalAudioController
    : MonoBehaviour
{
    [SerializeField]
    private ExperienceRoomController room;

    [Header("Loop Channels")]

    [SerializeField]
    private LoopCrossfader ambient;

    [SerializeField]
    private LoopCrossfader wind;

    [SerializeField]
    private LoopCrossfader humidity;

    [SerializeField]
    private LoopCrossfader water;

    [SerializeField]
    private float crossfadeSeconds = 0.75f;

    public void ApplyProfile(
        EnvironmentProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        if (ambient != null)
        {
            ambient.Play(
                profile.ambientLoop,
                profile.ambientVolume,
                crossfadeSeconds
            );
        }

        if (wind != null)
        {
            wind.Play(
                profile.windLoop,
                profile.windVolume,
                crossfadeSeconds
            );
        }

        if (humidity != null)
        {
            humidity.Play(
                profile.humidityLoop,
                profile.humidityVolume,
                crossfadeSeconds
            );
        }

        if (water != null)
        {
            water.Play(
                profile.waterLoop,
                profile.waterVolume,
                crossfadeSeconds
            );
        }
    }

    private void Update()
    {
        if (room == null)
        {
            room =
                FindFirstObjectByType<
                    ExperienceRoomController>();
        }

        if (room == null)
        {
            return;
        }

        EnvironmentRuntimeState e =
            room.CurrentState;

        float windFactor =
            Mathf.Clamp01(
                e.windSpeed / 15f
            );

        float windNoise =
            Mathf.Lerp(
                0.65f,
                1.15f,
                Mathf.PerlinNoise(
                    Time.time * 0.55f,
                    17.2f
                )
            );

        if (wind != null)
        {
            wind.SetMultiplier(
                windFactor *
                windNoise *
                (
                    0.7f +
                    0.3f *
                    e.windGustiness
                )
            );
        }

        if (humidity != null)
        {
            humidity.SetMultiplier(
                e.humidity
            );
        }

        if (water != null)
        {
            water.SetMultiplier(
                e.waterLevel
            );
        }

        if (ambient != null)
        {
            float underwaterAttenuation =
                Mathf.Lerp(
                    1f,
                    0.45f,
                    e.waterLevel
                );

            ambient.SetMultiplier(
                underwaterAttenuation
            );
        }
    }
}