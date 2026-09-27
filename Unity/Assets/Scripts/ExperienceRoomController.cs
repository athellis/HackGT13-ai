using System;
using System.Collections.Generic;
using UnityEngine;


public class ExperienceRoomController : MonoBehaviour
{
    // =====================================================
    // ENVIRONMENT SETUP
    // =====================================================

    [Header("Environment Setup")]

    [SerializeField]
    private Transform environmentRoot;


    [SerializeField]
    private EnvironmentProfile startingEnvironment;


    [SerializeField]
    private EnvironmentProfile[] availableEnvironments;


    [SerializeField]
    private float defaultTransitionDuration = 1.0f;


    [SerializeField]
    private bool warmupAllEnvironments = false;


    // =====================================================
    // SYSTEMS
    // =====================================================

    [Header("Systems")]

    [SerializeField]
    private EnvironmentEventRunner eventRunner;


    [SerializeField]
    private EnvironmentalAudioController audioController;


    // =====================================================
    // CURRENT STATE
    // =====================================================

    public EnvironmentRuntimeState CurrentState
    {
        get;
        private set;
    } = new EnvironmentRuntimeState();


    public OutfitMetrics CurrentOutfit
    {
        get;
        private set;
    } = new OutfitMetrics();


    public EnvironmentProfile CurrentProfile
    {
        get;
        private set;
    }


    public float ExternalFitScore
    {
        get;
        private set;
    }


    // =====================================================
    // EVENTS
    // =====================================================

    public event Action<EnvironmentProfile>
        OnEnvironmentChanged;


    public event Action<OutfitMetrics>
        OnOutfitChanged;


    public event Action<float>
        OnFitScoreChanged;


    // =====================================================
    // TRANSITION STATE
    // =====================================================

    private EnvironmentRuntimeState
        transitionStart;


    private EnvironmentRuntimeState
        transitionTarget;


    private float transitionTime;

    private float transitionDuration;


    // =====================================================
    // ENVIRONMENT INSTANCES
    // =====================================================

    private readonly Dictionary<
        EnvironmentProfile,
        GameObject
    > environmentInstances =
        new Dictionary<
            EnvironmentProfile,
            GameObject
        >();


    private GameObject activeEnvironment;


    // =====================================================
    // UNITY
    // =====================================================

    private void Awake()
    {
        if (eventRunner == null)
        {
            eventRunner =
                GetComponent<EnvironmentEventRunner>();
        }


        if (audioController == null)
        {
            audioController =
                GetComponent<EnvironmentalAudioController>();
        }


        transitionStart =
            new EnvironmentRuntimeState();


        transitionTarget =
            new EnvironmentRuntimeState();
    }


    private void Start()
    {
        if (warmupAllEnvironments)
        {
            WarmupEnvironments();
        }


        if (startingEnvironment != null)
        {
            SetEnvironment(
                startingEnvironment,
                0f
            );
        }
    }


    private void Update()
    {
        if (CurrentProfile == null)
        {
            return;
        }


        if (transitionDuration <= 0f)
        {
            CurrentState.CopyFrom(
                transitionTarget
            );

            return;
        }


        transitionTime +=
            Time.deltaTime;


        float rawT =
            Mathf.Clamp01(
                transitionTime /
                transitionDuration
            );


        float smoothT =
            rawT *
            rawT *
            (3f - 2f * rawT);


        CurrentState.Lerp(
            transitionStart,
            transitionTarget,
            smoothT
        );


        if (rawT >= 1f)
        {
            CurrentState.CopyFrom(
                transitionTarget
            );
        }
    }


    // =====================================================
    // ENVIRONMENT CONTROL
    // =====================================================

    public void SetEnvironment(
        EnvironmentProfile profile,
        float duration = -1f)
    {
        if (profile == null)
        {
            Debug.LogWarning(
                "SetEnvironment received a null profile."
            );

            return;
        }


        if (duration < 0f)
        {
            duration =
                defaultTransitionDuration;
        }


        transitionStart.CopyFrom(
            CurrentState
        );


        transitionTarget =
            EnvironmentRuntimeState
                .FromProfile(profile);


        transitionTime = 0f;


        transitionDuration =
            duration;


        CurrentProfile =
            profile;


        GameObject nextEnvironment =
            GetOrCreateEnvironment(
                profile
            );


        if (activeEnvironment != null)
        {
            activeEnvironment.SetActive(
                false
            );
        }


        activeEnvironment =
            nextEnvironment;


        activeEnvironment.SetActive(
            true
        );


        if (eventRunner != null)
        {
            eventRunner.Begin(
                profile,
                activeEnvironment.transform
            );
        }


        if (audioController != null)
        {
            audioController.ApplyProfile(
                profile
            );
        }


        OnEnvironmentChanged?.Invoke(
            profile
        );
    }


    public void SetEnvironmentById(
        string id,
        float duration = -1f)
    {
        if (
            string.IsNullOrWhiteSpace(
                id
            )
        )
        {
            return;
        }


        if (availableEnvironments == null)
        {
            Debug.LogWarning(
                "No available environments are assigned."
            );

            return;
        }


        for (
            int i = 0;
            i < availableEnvironments.Length;
            i++
        )
        {
            EnvironmentProfile profile =
                availableEnvironments[i];


            if (
                profile != null &&
                string.Equals(
                    profile.id,
                    id,
                    StringComparison
                        .OrdinalIgnoreCase
                )
            )
            {
                SetEnvironment(
                    profile,
                    duration
                );

                return;
            }
        }


        Debug.LogWarning(
            "No environment found with ID: "
            + id
        );
    }


    // =====================================================
    // OUTFIT DATA
    // =====================================================

    public void SetOutfitMetrics(
        OutfitMetrics metrics)
    {
        if (metrics == null)
        {
            return;
        }


        CurrentOutfit.CopyFrom(
            metrics
        );


        OnOutfitChanged?.Invoke(
            CurrentOutfit
        );
    }


    public void SetOutfitMetrics(
        float insulation,
        float windResistance,
        float waterproofing,
        float breathability,
        float traction,
        float tightness,
        Color dominantColor)
    {
        CurrentOutfit.insulation =
            Mathf.Clamp01(
                insulation
            );


        CurrentOutfit.windResistance =
            Mathf.Clamp01(
                windResistance
            );


        CurrentOutfit.waterproofing =
            Mathf.Clamp01(
                waterproofing
            );


        CurrentOutfit.breathability =
            Mathf.Clamp01(
                breathability
            );


        CurrentOutfit.traction =
            Mathf.Clamp01(
                traction
            );


        CurrentOutfit.tightness =
            Mathf.Clamp01(
                tightness
            );


        CurrentOutfit.dominantColor =
            dominantColor;


        OnOutfitChanged?.Invoke(
            CurrentOutfit
        );
    }


    public void SetExternalFitScore(
        float score)
    {
        ExternalFitScore =
            Mathf.Clamp(
                score,
                0f,
                100f
            );


        OnFitScoreChanged?.Invoke(
            ExternalFitScore
        );
    }


    // =====================================================
    // ENVIRONMENT WARMUP
    // =====================================================

    public void WarmupEnvironments()
    {
        if (availableEnvironments == null)
        {
            return;
        }


        for (
            int i = 0;
            i < availableEnvironments.Length;
            i++
        )
        {
            if (
                availableEnvironments[i] == null
            )
            {
                continue;
            }


            GetOrCreateEnvironment(
                availableEnvironments[i]
            );
        }
    }


    // =====================================================
    // CREATE / CACHE ENVIRONMENT
    // =====================================================

    private GameObject GetOrCreateEnvironment(
        EnvironmentProfile profile)
    {
        GameObject instance;


        if (
            environmentInstances.TryGetValue(
                profile,
                out instance
            )
        )
        {
            return instance;
        }


        if (
            profile.environmentPrefab == null
        )
        {
            throw new InvalidOperationException(
                "Environment profile '"
                + profile.displayName
                + "' has no environment prefab."
            );
        }


        instance =
            Instantiate(
                profile.environmentPrefab,
                environmentRoot
            );


        instance.name =
            profile.displayName
            + "_Runtime";


        instance.transform.localPosition =
            Vector3.zero;


        instance.transform.localRotation =
            Quaternion.identity;


        instance.transform.localScale =
            Vector3.one;


        instance.SetActive(
            false
        );


        environmentInstances.Add(
            profile,
            instance
        );


        return instance;
    }
}