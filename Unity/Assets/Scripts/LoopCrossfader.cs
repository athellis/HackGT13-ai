using System.Collections;
using UnityEngine;

public class LoopCrossfader : MonoBehaviour
{
    [SerializeField]
    private AudioSource sourceA;

    [SerializeField]
    private AudioSource sourceB;

    private AudioSource active;

    private AudioSource inactive;

    private Coroutine fadeRoutine;

    private float baseVolume;

    private float multiplier = 1f;

    private void Awake()
    {
        Configure(sourceA);
        Configure(sourceB);

        active = null;
        inactive = sourceA;
    }

    private void Configure(
        AudioSource source)
    {
        if (source == null)
        {
            return;
        }

        source.loop = true;
        source.playOnAwake = false;
    }

    public void Play(
        AudioClip clip,
        float volume,
        float fadeSeconds)
    {
        if (clip == null)
        {
            Stop(fadeSeconds);
            return;
        }

        baseVolume =
            Mathf.Clamp01(volume);

        multiplier = 1f;

        if (active != null &&
            active.clip == clip)
        {
            active.volume =
                baseVolume * multiplier;

            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(
                fadeRoutine
            );
        }

        fadeRoutine =
            StartCoroutine(
                CrossFade(
                    clip,
                    fadeSeconds
                )
            );
    }

    public void SetMultiplier(
        float value)
    {
        multiplier =
            Mathf.Clamp01(value);

        if (active != null)
        {
            active.volume =
                baseVolume *
                multiplier;
        }
    }

    public void Stop(
        float fadeSeconds)
    {
        if (active == null)
        {
            return;
        }

        if (fadeRoutine != null)
        {
            StopCoroutine(
                fadeRoutine
            );
        }

        fadeRoutine =
            StartCoroutine(
                FadeOut(fadeSeconds)
            );
    }

    private IEnumerator CrossFade(
        AudioClip clip,
        float seconds)
    {
        AudioSource incoming =
            inactive;

        if (incoming == null)
        {
            yield break;
        }

        incoming.clip = clip;
        incoming.volume = 0f;
        incoming.loop = true;
        incoming.Play();

        AudioSource old =
            active;

        if (old == null)
        {
            active =
                incoming;

            inactive =
                sourceA == incoming
                ? sourceB
                : sourceA;

            float elapsed = 0f;

            while (elapsed < seconds)
            {
                elapsed += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed /
                        seconds
                    );

                incoming.volume =
                    baseVolume *
                    multiplier *
                    t;

                yield return null;
            }

            incoming.volume =
                baseVolume *
                multiplier;

            yield break;
        }

        float timer = 0f;

        while (timer < seconds)
        {
            timer +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    timer /
                    seconds
                );

            incoming.volume =
                baseVolume *
                multiplier *
                t;

            old.volume =
                Mathf.Lerp(
                    old.volume,
                    0f,
                    t
                );

            yield return null;
        }

        old.Stop();
        old.clip = null;

        incoming.volume =
            baseVolume *
            multiplier;

        active =
            incoming;

        inactive =
            old;
    }

    private IEnumerator FadeOut(
        float seconds)
    {
        AudioSource old =
            active;

        if (old == null)
        {
            yield break;
        }

        float startingVolume =
            old.volume;

        float elapsed = 0f;

        while (elapsed < seconds)
        {
            elapsed +=
                Time.deltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed /
                    seconds
                );

            old.volume =
                Mathf.Lerp(
                    startingVolume,
                    0f,
                    t
                );

            yield return null;
        }

        old.Stop();
        old.clip = null;

        active = null;
        inactive = old;
    }
}