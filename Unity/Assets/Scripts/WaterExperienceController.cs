using UnityEngine;

public class WaterExperienceController : MonoBehaviour
{
    [SerializeField]
    private ExperienceRoomController room;


    [Header("Visuals")]

    [SerializeField]
    private GameObject underwaterOverlay;

    [SerializeField]
    private ParticleSystem bubbles;

    [SerializeField]
    private ParticleSystem surfaceParticles;


    [Header("Optional Audio")]

    [SerializeField]
    private AudioSource underwaterAudio;


    [SerializeField]
    private float activationThreshold = 0.65f;


    private bool wasUnderwater;


    private void Awake()
    {
        if (room == null)
        {
            room =
                FindAnyObjectByType<
                    ExperienceRoomController
                >();
        }
    }


    private void Update()
    {
        if (room == null)
        {
            return;
        }


        float water =
            room.CurrentState.waterLevel;


        bool underwater =
            water >= activationThreshold;


        // ---------------------------------------------
        // UNDERWATER OVERLAY
        // ---------------------------------------------

        if (underwaterOverlay != null)
        {
            underwaterOverlay.SetActive(
                underwater
            );
        }


        // ---------------------------------------------
        // BUBBLES
        // ---------------------------------------------

        if (bubbles != null)
        {
            ParticleSystem.EmissionModule emission =
                bubbles.emission;


            emission.rateOverTime =
                Mathf.Lerp(
                    0f,
                    18f,
                    water
                );
        }


        // ---------------------------------------------
        // SURFACE PARTICLES
        // ---------------------------------------------

        if (surfaceParticles != null)
        {
            ParticleSystem.EmissionModule emission =
                surfaceParticles.emission;


            emission.rateOverTime =
                Mathf.Lerp(
                    0f,
                    8f,
                    water
                );
        }


        // ---------------------------------------------
        // UNDERWATER AUDIO
        // ---------------------------------------------

        if (underwaterAudio != null)
        {
            underwaterAudio.volume =
                Mathf.Lerp(
                    0f,
                    0.7f,
                    water
                );
        }


        // Haptics were intentionally removed.
        //
        // The original project attempted to call:
        //
        // ExperienceHaptics.PulseBothHands(...)
        //
        // but ExperienceHaptics does not exist in
        // the shared project.


        // Optional debug so we can still detect
        // when the user transitions underwater.

        if (
            underwater &&
            !wasUnderwater
        )
        {
            Debug.Log(
                "Entered underwater environment."
            );
        }


        if (
            !underwater &&
            wasUnderwater
        )
        {
            Debug.Log(
                "Exited underwater environment."
            );
        }


        wasUnderwater =
            underwater;
    }
}