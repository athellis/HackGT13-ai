using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using Newtonsoft.Json;

public class WhisperRecorder : MonoBehaviour
{
    [Header("FastAPI")]
    public string transcribeUrl =
        "http://127.0.0.1:8000/transcribe";

    [Header("Recommendation System")]
    public EnvironmentApiController recommendationController;

    [Header("Optional Status Text")]
    public TMP_Text statusText;

    [Header("Recording")]
    public int maxRecordingSeconds = 15;
    public int sampleRate = 16000;

    [Header("Demo")]
    public float autoStopAfterSeconds = 7f;

    [Header("Mac Testing Backup")]
    public bool useSpacebar = true;

    private AudioClip recordingClip;
    private bool isRecording = false;
    private Coroutine autoStopCoroutine;


    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR

        if (!UnityEngine.Android.Permission
            .HasUserAuthorizedPermission(
                UnityEngine.Android.Permission.Microphone
            ))
        {
            UnityEngine.Android.Permission
                .RequestUserPermission(
                    UnityEngine.Android.Permission.Microphone
                );
        }

#endif

        if (statusText != null)
        {
            statusText.text =
                "Press A to speak";
        }
    }


    private void Update()
    {
        // ============================================
        // QUEST:
        // Right controller A button
        // ============================================

        if (
            OVRInput.GetDown(
                OVRInput.Button.One,
                OVRInput.Controller.RTouch
            )
        )
        {
            StartVoiceRequest();
        }


        // ============================================
        // MAC / UNITY EDITOR BACKUP:
        // Spacebar
        // ============================================

        if (
            useSpacebar &&
            Input.GetKeyDown(
                KeyCode.Space
            )
        )
        {
            StartVoiceRequest();
        }
    }


    // =================================================
    // ONE PRESS STARTS THE ENTIRE VOICE REQUEST
    // =================================================

    public void StartVoiceRequest()
    {
        if (isRecording)
        {
            return;
        }


        StartRecording();


        if (isRecording)
        {
            autoStopCoroutine =
                StartCoroutine(
                    AutoStopRecording()
                );
        }
    }


    private IEnumerator AutoStopRecording()
    {
        yield return new WaitForSeconds(
            autoStopAfterSeconds
        );


        if (isRecording)
        {
            StopRecording();
        }
    }


    private void StartRecording()
    {
        if (isRecording)
        {
            return;
        }


        if (
            Microphone.devices.Length == 0
        )
        {
            Debug.LogError(
                "NO MICROPHONE FOUND"
            );


            if (statusText != null)
            {
                statusText.text =
                    "No microphone found";
            }


            return;
        }


        Debug.Log(
            "Using microphone: "
            + Microphone.devices[0]
        );


        recordingClip =
            Microphone.Start(
                null,
                false,
                maxRecordingSeconds,
                sampleRate
            );


        isRecording = true;


        if (statusText != null)
        {
            statusText.text =
                "Listening...";
        }


        Debug.Log(
            "WHISPER RECORDING STARTED"
        );
    }


    private void StopRecording()
    {
        if (!isRecording)
        {
            return;
        }


        int recordedFrames =
            Microphone.GetPosition(
                null
            );


        Microphone.End(
            null
        );


        isRecording = false;
        autoStopCoroutine = null;


        if (
            recordedFrames <= 0 ||
            recordingClip == null
        )
        {
            Debug.LogError(
                "NO AUDIO WAS RECORDED"
            );


            if (statusText != null)
            {
                statusText.text =
                    "No audio recorded";
            }


            return;
        }


        Debug.Log(
            "Recorded frames: "
            + recordedFrames
        );


        if (statusText != null)
        {
            statusText.text =
                "Transcribing...";
        }


        byte[] wav =
            ConvertClipToWav(
                recordingClip,
                recordedFrames
            );


        StartCoroutine(
            SendToWhisper(
                wav
            )
        );
    }


    private IEnumerator SendToWhisper(
        byte[] wavData
    )
    {
        WWWForm form =
            new WWWForm();


        form.AddBinaryData(
            "file",
            wavData,
            "speech.wav",
            "audio/wav"
        );


        using UnityWebRequest request =
            UnityWebRequest.Post(
                transcribeUrl,
                form
            );


        request.timeout = 180;


        Debug.Log(
            "Sending audio to Whisper..."
        );


        yield return
            request.SendWebRequest();


        if (
            request.result !=
            UnityWebRequest.Result.Success
        )
        {
            Debug.LogError(
                "WHISPER FAILED\n"
                + request.error
                + "\n"
                + request.downloadHandler.text
            );


            if (statusText != null)
            {
                statusText.text =
                    "Whisper failed";
            }


            yield break;
        }


        Debug.Log(
            "WHISPER RESPONSE:\n"
            + request.downloadHandler.text
        );


        WhisperResponse response =
            JsonConvert
                .DeserializeObject
                <WhisperResponse>(
                    request.downloadHandler.text
                );


        if (
            response == null ||
            string.IsNullOrWhiteSpace(
                response.text
            )
        )
        {
            Debug.LogError(
                "Whisper returned no transcript"
            );


            if (statusText != null)
            {
                statusText.text =
                    "No speech detected";
            }


            yield break;
        }


        string transcript =
            response.text.Trim();


        Debug.Log(
            "TRANSCRIPT:\n"
            + transcript
        );


        if (statusText != null)
        {
            statusText.text =
                "You said:\n"
                + transcript
                + "\n\nFinding outfits...";
        }


        // SEND TRANSCRIPT TO YOUR EXISTING RECOMMENDER

        if (
            recommendationController != null
        )
        {
            recommendationController
                .AnalyzeRequest(
                    transcript
                );
        }
        else
        {
            Debug.LogError(
                "EnvironmentApiController "
                + "is not assigned!"
            );
        }
    }


    private byte[] ConvertClipToWav(
        AudioClip clip,
        int recordedFrames
    )
    {
        recordedFrames =
            Mathf.Clamp(
                recordedFrames,
                0,
                clip.samples
            );


        int channels =
            clip.channels;


        int totalSamples =
            recordedFrames
            * channels;


        float[] samples =
            new float[
                totalSamples
            ];


        clip.GetData(
            samples,
            0
        );


        using MemoryStream stream =
            new MemoryStream();


        using BinaryWriter writer =
            new BinaryWriter(
                stream
            );


        int bytesPerSample = 2;


        int dataSize =
            totalSamples
            * bytesPerSample;


        // RIFF

        writer.Write(
            System.Text.Encoding
                .ASCII
                .GetBytes(
                    "RIFF"
                )
        );


        writer.Write(
            36 + dataSize
        );


        writer.Write(
            System.Text.Encoding
                .ASCII
                .GetBytes(
                    "WAVE"
                )
        );


        // FORMAT

        writer.Write(
            System.Text.Encoding
                .ASCII
                .GetBytes(
                    "fmt "
                )
        );


        writer.Write(16);

        writer.Write(
            (short)1
        );

        writer.Write(
            (short)channels
        );

        writer.Write(
            clip.frequency
        );


        writer.Write(
            clip.frequency
            * channels
            * bytesPerSample
        );


        writer.Write(
            (short)(
                channels
                * bytesPerSample
            )
        );


        writer.Write(
            (short)16
        );


        // DATA

        writer.Write(
            System.Text.Encoding
                .ASCII
                .GetBytes(
                    "data"
                )
        );


        writer.Write(
            dataSize
        );


        foreach (
            float sample
            in samples
        )
        {
            float clamped =
                Mathf.Clamp(
                    sample,
                    -1f,
                    1f
                );


            short pcm =
                (short)(
                    clamped
                    * short.MaxValue
                );


            writer.Write(
                pcm
            );
        }


        writer.Flush();


        return stream.ToArray();
    }
}


public class WhisperResponse
{
    public string text
    {
        get;
        set;
    }
}