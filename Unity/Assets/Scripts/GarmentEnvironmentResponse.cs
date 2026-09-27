using UnityEngine;

public class GarmentEnvironmentResponse : MonoBehaviour
{
    [Header("System")]
    [SerializeField]
    private ExperienceRoomController room;

    [Header("Garment Properties")]

    [Range(0f, 1f)]
    public float insulation = 0.5f;

    [Range(0f, 1f)]
    public float windResistance = 0.5f;

    [Range(0f, 1f)]
    public float waterproofing = 0.5f;

    [Range(0f, 1f)]
    public float breathability = 0.5f;

    [Range(0f, 1f)]
    public float tightness = 0.5f;

    [Range(0f, 1f)]
    public float mudResistance = 0.2f;

    public Color garmentColor = Color.white;

    [Header("Renderers")]
    [SerializeField]
    private Renderer[] renderers;

    [Header("Optional Wind Bones")]
    [SerializeField]
    private Transform[] windBones;

    [SerializeField]
    private float windRotationDegrees = 10f;

    [Header("Shader Properties")]
    [SerializeField]
    private string baseColorProperty =
        "_BaseColor";

    [SerializeField]
    private string smoothnessProperty =
        "_Smoothness";

    [SerializeField]
    private string wetnessProperty =
        "_Wetness";

    [SerializeField]
    private string dirtProperty =
        "_DirtAmount";

    [SerializeField]
    private string windStrengthProperty =
        "_WindStrength";

    private MaterialPropertyBlock block;

    private Color[] originalColors;
    private float[] originalSmoothness;

    private Quaternion[] originalBoneRotations;

    private int baseColorID;
    private int smoothnessID;
    private int wetnessID;
    private int dirtID;
    private int windStrengthID;

    private float wetness;

    private void Awake()
    {
        if (room == null)
        {
            room =
                FindFirstObjectByType<
                    ExperienceRoomController>();
        }

        if (renderers == null ||
            renderers.Length == 0)
        {
            renderers =
                GetComponentsInChildren<
                    Renderer
                >(true);
        }

        block =
            new MaterialPropertyBlock();

        baseColorID =
            Shader.PropertyToID(
                baseColorProperty
            );

        smoothnessID =
            Shader.PropertyToID(
                smoothnessProperty
            );

        wetnessID =
            Shader.PropertyToID(
                wetnessProperty
            );

        dirtID =
            Shader.PropertyToID(
                dirtProperty
            );

        windStrengthID =
            Shader.PropertyToID(
                windStrengthProperty
            );

        CacheOriginalMaterialValues();

        if (windBones != null)
        {
            originalBoneRotations =
                new Quaternion[
                    windBones.Length
                ];

            for (int i = 0;
                 i < windBones.Length;
                 i++)
            {
                if (windBones[i] != null)
                {
                    originalBoneRotations[i] =
                        windBones[i]
                            .localRotation;
                }
            }
        }
    }

    private void CacheOriginalMaterialValues()
    {
        originalColors =
            new Color[renderers.Length];

        originalSmoothness =
            new float[renderers.Length];

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer r =
                renderers[i];

            originalColors[i] =
                garmentColor;

            originalSmoothness[i] =
                0.5f;

            if (r == null ||
                r.sharedMaterial == null)
            {
                continue;
            }

            Material material =
                r.sharedMaterial;

            if (material.HasProperty(
                baseColorID))
            {
                originalColors[i] =
                    material.GetColor(
                        baseColorID
                    );
            }

            if (material.HasProperty(
                smoothnessID))
            {
                originalSmoothness[i] =
                    material.GetFloat(
                        smoothnessID
                    );
            }
        }
    }

    private void LateUpdate()
    {
        if (room == null)
        {
            return;
        }

        EnvironmentRuntimeState env =
            room.CurrentState;

        float targetWetness =
            ExperienceModel.Wetness(
                env,
                CreateTemporaryOutfit()
            );

        float wetRate =
            0.5f + env.waterLevel;

        wetness =
            Mathf.MoveTowards(
                wetness,
                targetWetness,
                wetRate *
                Time.deltaTime
            );

        ApplyVisualProperties(
            env
        );

        ApplyWind(
            env
        );
    }

    private OutfitMetrics CreateTemporaryOutfit()
    {
        OutfitMetrics o =
            new OutfitMetrics();

        o.insulation =
            insulation;

        o.windResistance =
            windResistance;

        o.waterproofing =
            waterproofing;

        o.breathability =
            breathability;

        o.tightness =
            tightness;

        o.dominantColor =
            garmentColor;

        return o;
    }

    private void ApplyVisualProperties(
        EnvironmentRuntimeState env)
    {
        Color environmentalColor =
            Color.Lerp(
                Color.white,
                env.environmentalTint,
                env.sceneryColorInfluence
            );

        Color targetColor =
            MultiplyColors(
                originalColors[0],
                environmentalColor
            );

        // Wet clothes appear darker.
        targetColor =
            Color.Lerp(
                targetColor,
                targetColor * 0.72f,
                wetness * 0.5f
            );

        float dirt =
            Mathf.Clamp01(
                env.mudLevel *
                (1f - mudResistance)
            );

        for (int i = 0;
             i < renderers.Length;
             i++)
        {
            Renderer r =
                renderers[i];

            if (r == null)
            {
                continue;
            }

            r.GetPropertyBlock(
                block
            );

            if (r.sharedMaterial != null &&
                r.sharedMaterial.HasProperty(
                    baseColorID))
            {
                Color color =
                    Color.Lerp(
                        originalColors[i],
                        MultiplyColors(
                            originalColors[i],
                            environmentalColor
                        ),
                        env.sceneryColorInfluence
                    );

                color =
                    Color.Lerp(
                        color,
                        color * 0.72f,
                        wetness * 0.5f
                    );

                block.SetColor(
                    baseColorID,
                    color
                );
            }

            if (r.sharedMaterial != null &&
                r.sharedMaterial.HasProperty(
                    smoothnessID))
            {
                float smoothness =
                    Mathf.Lerp(
                        originalSmoothness[i],
                        0.92f,
                        wetness
                    );

                block.SetFloat(
                    smoothnessID,
                    smoothness
                );
            }

            if (r.sharedMaterial != null &&
                r.sharedMaterial.HasProperty(
                    wetnessID))
            {
                block.SetFloat(
                    wetnessID,
                    wetness
                );
            }

            if (r.sharedMaterial != null &&
                r.sharedMaterial.HasProperty(
                    dirtID))
            {
                block.SetFloat(
                    dirtID,
                    dirt
                );
            }

            if (r.sharedMaterial != null &&
                r.sharedMaterial.HasProperty(
                    windStrengthID))
            {
                float wind =
                    Mathf.Clamp01(
                        env.windSpeed / 15f
                    );

                wind *=
                    0.7f +
                    env.windGustiness * 0.3f;

                block.SetFloat(
                    windStrengthID,
                    wind
                );
            }

            r.SetPropertyBlock(
                block
            );
        }
    }

    private void ApplyWind(
        EnvironmentRuntimeState env)
    {
        if (windBones == null ||
            windBones.Length == 0)
        {
            return;
        }

        float wind =
            Mathf.Clamp01(
                env.windSpeed / 15f
            );

        float time =
            Time.time *
            (1f + wind * 2f);

        for (int i = 0;
             i < windBones.Length;
             i++)
        {
            if (windBones[i] == null)
            {
                continue;
            }

            float noiseA =
                Mathf.PerlinNoise(
                    time,
                    i * 11.7f
                ) - 0.5f;

            float noiseB =
                Mathf.PerlinNoise(
                    time * 1.41f,
                    i * 17.3f
                ) - 0.5f;

            float magnitude =
                wind *
                windRotationDegrees;

            Quaternion rotation =
                originalBoneRotations[i] *
                Quaternion.Euler(
                    noiseA * magnitude,
                    0f,
                    noiseB * magnitude
                );

            windBones[i].localRotation =
                rotation;
        }
    }

    private Color MultiplyColors(
        Color a,
        Color b)
    {
        return new Color(
            a.r * b.r,
            a.g * b.g,
            a.b * b.b,
            a.a
        );
    }
}