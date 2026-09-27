using UnityEngine;

public class EnvironmentEffectsController : MonoBehaviour
{
    private WindZone windZone;

    private ParticleSystem rainParticles;
    private ParticleSystem snowParticles;

    private Material rainMaterial;
    private Material snowMaterial;


    // =====================================================
    // MAIN ENTRY POINT
    // =====================================================

    public void ApplyEffects(EnvironmentData data)
    {
        if (data == null)
        {
            Debug.LogWarning(
                "EnvironmentEffectsController received no data."
            );

            return;
        }


        SetupWind(data);

        SetupRain(data);

        SetupSnow(data);


        Debug.Log(
            "ENVIRONMENT EFFECTS APPLIED\n"
            + "Wind: " + data.wind
            + "\nGustiness: " + data.gustiness
            + "\nPrecipitation: " + data.precipitation
            + "\nSnow: " + data.snow
        );
    }


    // =====================================================
    // WIND
    // =====================================================

    private void SetupWind(EnvironmentData data)
    {
        if (windZone == null)
        {
            GameObject windObject =
                new GameObject("RuntimeWind");

            windObject.transform.SetParent(
                transform
            );

            windObject.transform.localPosition =
                Vector3.zero;

            windZone =
                windObject.AddComponent<WindZone>();

            windZone.mode =
                WindZoneMode.Directional;
        }


        // Open-Meteo wind defaults to km/h.
        float windMetersPerSecond =
            data.wind / 3.6f;

        float gustMetersPerSecond =
            data.gustiness / 3.6f;


        // Unity WindZone is not measured directly
        // in physical m/s, so scale the real value.
        windZone.windMain =
            Mathf.Clamp(
                windMetersPerSecond * 0.25f,
                0f,
                5f
            );


        windZone.windTurbulence =
            Mathf.Clamp(
                gustMetersPerSecond * 0.15f,
                0f,
                5f
            );


        windZone.windPulseMagnitude =
            Mathf.Clamp(
                gustMetersPerSecond * 0.1f,
                0f,
                2f
            );


        windZone.windPulseFrequency =
            0.5f;


        if (
            data.windDirection
            != Vector3.zero
        )
        {
            windZone.transform.rotation =
                Quaternion.LookRotation(
                    data.windDirection,
                    Vector3.up
                );
        }
    }


    // =====================================================
    // RAIN
    // =====================================================

    private void SetupRain(EnvironmentData data)
    {
        if (rainParticles == null)
        {
            rainParticles =
                CreateRainSystem();
        }


        float intensity =
            Mathf.Clamp01(
                data.precipitation / 10f
            );


        ParticleSystem.EmissionModule emission =
            rainParticles.emission;


        if (data.precipitation > 0.01f)
        {
            emission.enabled = true;

            emission.rateOverTime =
                500f
                + (2500f * intensity);


            if (!rainParticles.isPlaying)
            {
                rainParticles.Play();
            }
        }
        else
        {
            emission.enabled = false;

            rainParticles.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmittingAndClear
            );
        }
    }


    private ParticleSystem CreateRainSystem()
    {
        GameObject rainObject =
            new GameObject("RuntimeRain");

        rainObject.transform.SetParent(
            transform
        );

        rainObject.transform.localPosition =
            new Vector3(
                0f,
                10f,
                0f
            );


        ParticleSystem system =
            rainObject.AddComponent<
                ParticleSystem
            >();


        ParticleSystem.MainModule main =
            system.main;

        main.loop = true;

        main.startLifetime = 1.5f;

        main.startSpeed = 18f;

        main.startSize =
            new ParticleSystem.MinMaxCurve(
                0.02f,
                0.05f
            );

        main.maxParticles = 6000;

        main.simulationSpace =
            ParticleSystemSimulationSpace.World;


        ParticleSystem.ShapeModule shape =
            system.shape;

        shape.shapeType =
            ParticleSystemShapeType.Box;

        shape.scale =
            new Vector3(
                20f,
                1f,
                20f
            );


        ParticleSystemRenderer renderer =
            system.GetComponent<
                ParticleSystemRenderer
            >();

        renderer.renderMode = ParticleSystemRenderMode.Stretch;

        renderer.lengthScale = 4f;

        renderer.velocityScale = 0.25f;


        rainMaterial =
            CreateParticleMaterial(
                "RainParticleMaterial",
                new Color(
                    0.65f,
                    0.8f,
                    1f,
                    0.7f
                )
            );

        renderer.material =
            rainMaterial;


        system.Stop(
            true,
            ParticleSystemStopBehavior
                .StopEmittingAndClear
        );


        return system;
    }


    // =====================================================
    // SNOW
    // =====================================================

    private void SetupSnow(EnvironmentData data)
    {
        if (snowParticles == null)
        {
            snowParticles =
                CreateSnowSystem();
        }


        float intensity =
            Mathf.Clamp01(
                data.snow
            );


        ParticleSystem.EmissionModule emission =
            snowParticles.emission;


        if (data.snow > 0.01f)
        {
            emission.enabled = true;

            emission.rateOverTime =
                100f
                + (900f * intensity);


            if (!snowParticles.isPlaying)
            {
                snowParticles.Play();
            }
        }
        else
        {
            emission.enabled = false;

            snowParticles.Stop(
                true,
                ParticleSystemStopBehavior
                    .StopEmittingAndClear
            );
        }
    }


    private ParticleSystem CreateSnowSystem()
    {
        GameObject snowObject =
            new GameObject("RuntimeSnow");

        snowObject.transform.SetParent(
            transform
        );

        snowObject.transform.localPosition =
            new Vector3(
                0f,
                10f,
                0f
            );


        ParticleSystem system =
            snowObject.AddComponent<
                ParticleSystem
            >();


        ParticleSystem.MainModule main =
            system.main;

        main.loop = true;

        main.startLifetime =
            new ParticleSystem.MinMaxCurve(
                6f,
                10f
            );

        main.startSpeed =
            new ParticleSystem.MinMaxCurve(
                0.8f,
                2f
            );

        main.startSize =
            new ParticleSystem.MinMaxCurve(
                0.03f,
                0.12f
            );

        main.maxParticles = 4000;

        main.simulationSpace =
            ParticleSystemSimulationSpace.World;


        ParticleSystem.ShapeModule shape =
            system.shape;

        shape.shapeType =
            ParticleSystemShapeType.Box;

        shape.scale =
            new Vector3(
                20f,
                1f,
                20f
            );


        ParticleSystemRenderer renderer =
            system.GetComponent<
                ParticleSystemRenderer
            >();

        renderer.renderMode =
            ParticleSystemRenderMode.Billboard;


        snowMaterial =
            CreateParticleMaterial(
                "SnowParticleMaterial",
                new Color(
                    1f,
                    1f,
                    1f,
                    0.9f
                )
            );

        renderer.material =
            snowMaterial;


        system.Stop(
            true,
            ParticleSystemStopBehavior
                .StopEmittingAndClear
        );


        return system;
    }


    // =====================================================
    // MATERIAL
    // =====================================================

    private Material CreateParticleMaterial(
        string materialName,
        Color color
    )
    {
        Shader shader =
            Shader.Find(
                "Universal Render Pipeline/Particles/Unlit"
            );


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Particles/Standard Unlit"
                );
        }


        if (shader == null)
        {
            shader =
                Shader.Find(
                    "Sprites/Default"
                );
        }


        Material material =
            new Material(shader);

        material.name =
            materialName;

        material.color =
            color;


        return material;
    }
}