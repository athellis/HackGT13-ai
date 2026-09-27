using UnityEngine;

public class HumanShiverController : MonoBehaviour
{
    [SerializeField]
    private ExperienceRoomController room;

    [Header("Optional Blend Shape")]
    [SerializeField]
    private SkinnedMeshRenderer[] bodyRenderers;

    [SerializeField]
    private string shiverBlendShapeName =
        "Shiver";

    [Header("Bone Fallback")]
    [SerializeField]
    private Transform[] shiverBones;

    [SerializeField]
    private float boneRotationDegrees = 1.5f;

    [SerializeField]
    private float noiseSpeed = 6f;

    [SerializeField]
    private int randomSeed = 21;

    private Quaternion[] originalRotations;

    private int[] blendShapeIndices;

    private void Awake()
    {
        if (room == null)
        {
            room =
                FindFirstObjectByType<
                    ExperienceRoomController>();
        }

        if (shiverBones != null)
        {
            originalRotations =
                new Quaternion[
                    shiverBones.Length
                ];

            for (int i = 0;
                 i < shiverBones.Length;
                 i++)
            {
                if (shiverBones[i] != null)
                {
                    originalRotations[i] =
                        shiverBones[i]
                            .localRotation;
                }
            }
        }

        if (bodyRenderers != null)
        {
            blendShapeIndices =
                new int[
                    bodyRenderers.Length
                ];

            for (int i = 0;
                 i < bodyRenderers.Length;
                 i++)
            {
                blendShapeIndices[i] = -1;

                if (bodyRenderers[i] == null ||
                    bodyRenderers[i].sharedMesh == null)
                {
                    continue;
                }

                blendShapeIndices[i] =
                    bodyRenderers[i]
                        .sharedMesh
                        .GetBlendShapeIndex(
                            shiverBlendShapeName
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

        float shiver =
            ExperienceModel
                .ColdExposure(
                    room.CurrentState,
                    room.CurrentOutfit
                );

        float noiseTime =
            Time.time * noiseSpeed;

        if (bodyRenderers != null)
        {
            for (int i = 0;
                 i < bodyRenderers.Length;
                 i++)
            {
                if (bodyRenderers[i] == null ||
                    blendShapeIndices[i] < 0)
                {
                    continue;
                }

                float noise =
                    Mathf.PerlinNoise(
                        noiseTime,
                        randomSeed + i * 13.7f
                    );

                bodyRenderers[i]
                    .SetBlendShapeWeight(
                        blendShapeIndices[i],
                        shiver * noise * 100f
                    );
            }
        }

        if (shiverBones != null)
        {
            for (int i = 0;
                 i < shiverBones.Length;
                 i++)
            {
                if (shiverBones[i] == null)
                {
                    continue;
                }

                float x =
                    Mathf.PerlinNoise(
                        noiseTime,
                        randomSeed + i
                    ) - 0.5f;

                float z =
                    Mathf.PerlinNoise(
                        noiseTime * 1.37f,
                        randomSeed + i * 4
                    ) - 0.5f;

                float magnitude =
                    shiver *
                    boneRotationDegrees;

                Quaternion target =
                    originalRotations[i] *
                    Quaternion.Euler(
                        x * magnitude,
                        0f,
                        z * magnitude
                    );

                shiverBones[i].localRotation =
                    target;
            }
        }
    }
}