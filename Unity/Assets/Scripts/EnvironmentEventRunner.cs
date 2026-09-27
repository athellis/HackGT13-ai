using UnityEngine;

public class EnvironmentEventRunner : MonoBehaviour
{
    [SerializeField]
    private AudioSource eventAudioSource;

    private EnvironmentProfile currentProfile;

    private Transform activeEnvironmentRoot;

    private float elapsedTime;

    private float[] nextEventTimes;

    private bool[] eventArmed;


    public void Begin(
        EnvironmentProfile profile,
        Transform environmentRoot)
    {
        currentProfile = profile;

        activeEnvironmentRoot =
            environmentRoot;

        elapsedTime = 0f;


        if (currentProfile == null)
        {
            nextEventTimes = null;

            eventArmed = null;

            return;
        }


        int count =
            currentProfile.events.Count;


        nextEventTimes =
            new float[count];

        eventArmed =
            new bool[count];


        for (int i = 0; i < count; i++)
        {
            EnvironmentProfile.EnvironmentEvent e =
                currentProfile.events[i];


            nextEventTimes[i] =
                Mathf.Max(
                    0f,
                    e.firstTime
                );


            eventArmed[i] = true;
        }
    }


    private void Update()
    {
        if (
            currentProfile == null ||
            nextEventTimes == null
        )
        {
            return;
        }


        elapsedTime += Time.deltaTime;


        for (
            int i = 0;
            i < nextEventTimes.Length;
            i++
        )
        {
            if (!eventArmed[i])
            {
                continue;
            }


            if (
                elapsedTime <
                nextEventTimes[i]
            )
            {
                continue;
            }


            TriggerEvent(i);


            EnvironmentProfile.EnvironmentEvent e =
                currentProfile.events[i];


            if (e.repeat)
            {
                float min =
                    Mathf.Max(
                        0.1f,
                        e.repeatMinimum
                    );


                float max =
                    Mathf.Max(
                        min,
                        e.repeatMaximum
                    );


                nextEventTimes[i] =
                    elapsedTime +
                    Random.Range(
                        min,
                        max
                    );
            }
            else
            {
                eventArmed[i] = false;
            }
        }
    }


    private void TriggerEvent(int index)
    {
        EnvironmentProfile.EnvironmentEvent e =
            currentProfile.events[index];


        // AUDIO EVENT
        if (
            e.audioClip != null &&
            eventAudioSource != null
        )
        {
            eventAudioSource.PlayOneShot(
                e.audioClip,
                e.audioVolume
            );
        }


        // VISUAL EVENT
        if (
            e.visualPrefab != null &&
            activeEnvironmentRoot != null
        )
        {
            GameObject instance =
                Instantiate(
                    e.visualPrefab,
                    activeEnvironmentRoot
                );


            instance.transform.localPosition =
                e.localPosition;


            instance.transform.localEulerAngles =
                e.localEulerAngles;


            instance.transform.localScale =
                e.localScale;


            if (e.lifetime > 0f)
            {
                Destroy(
                    instance,
                    e.lifetime
                );
            }
        }


        // Haptics were intentionally removed.
        // The original project referenced an
        // ExperienceHaptics class that does not exist.
    }
}