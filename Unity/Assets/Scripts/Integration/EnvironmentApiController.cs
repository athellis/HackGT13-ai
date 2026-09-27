using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;
using Newtonsoft.Json;

public class EnvironmentApiController : MonoBehaviour
{
    [Header("Backend")]
    public string backendUrl =
        "http://127.0.0.1:8000/recommend";


    [Header("Environment")]
    public EnvironmentManager environmentManager;


    [Header("Recommendation Images")]
    public RawImage outfitImage1;
    public RawImage outfitImage2;
    public RawImage outfitImage3;


    [Header("Weather")]
    public TMP_Text weatherText;


    [Header("Testing")]
    public bool runTestOnStart = true;

    [TextArea(2, 4)]
    public string testPrompt =
        "I'm going to a beach party in Athens, Greece and I want something stylish.";


    private void Start()
    {
        if (runTestOnStart)
        {
            AnalyzeRequest(testPrompt);
        }
    }


    public void AnalyzeRequest(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            Debug.LogWarning(
                "No request text supplied."
            );

            return;
        }

        StartCoroutine(
            SendRecommendationRequest(userText)
        );
    }


    private IEnumerator SendRecommendationRequest(
        string userText
    )
    {
        RecommendationRequestBody body =
            new RecommendationRequestBody
            {
                text = userText
            };

        string json =
            JsonConvert.SerializeObject(body);


        using UnityWebRequest request =
            new UnityWebRequest(
                backendUrl,
                "POST"
            );


        byte[] bodyBytes =
            System.Text.Encoding.UTF8
                .GetBytes(json);


        request.uploadHandler =
            new UploadHandlerRaw(
                bodyBytes
            );

        request.downloadHandler =
            new DownloadHandlerBuffer();


        request.SetRequestHeader(
            "Content-Type",
            "application/json"
        );


        // Image generation may take a little while.
        request.timeout = 300;


        Debug.Log(
            "Sending recommendation request:\n"
            + userText
        );


        yield return request.SendWebRequest();


        if (
            request.result !=
            UnityWebRequest.Result.Success
        )
        {
            Debug.LogError(
                "RECOMMENDATION REQUEST FAILED\n"
                + request.error
                + "\n"
                + request.downloadHandler.text
            );

            yield break;
        }


        Debug.Log(
            "BACKEND RESPONSE:\n"
            + request.downloadHandler.text
        );


        RecommendationApiResponse response =
            JsonConvert.DeserializeObject
                <RecommendationApiResponse>(
                    request.downloadHandler.text
                );


        if (response == null)
        {
            Debug.LogError(
                "Could not parse backend response."
            );

            yield break;
        }


        // ============================================
        // CHANGE ENVIRONMENT
        // ============================================

        if (
            environmentManager != null &&
            !string.IsNullOrEmpty(
                response.environment
            )
        )
        {
            environmentManager.LoadEnvironment(
                response.environment
            );

            Debug.Log(
                "Loaded environment: "
                + response.environment
            );
        }


        // ============================================
        // WEATHER TEXT
        // ============================================

        ApplyWeather(
            response.environment_data
        );


        // ============================================
        // THREE OUTFIT IMAGES
        // ============================================

        yield return StartCoroutine(
            LoadOutfitImages(
                response.recommendations
            )
        );
    }


    private void ApplyWeather(
        RecommendationEnvironmentData data
    )
    {
        if (
            weatherText == null ||
            data == null
        )
        {
            return;
        }


        List<string> lines =
            new List<string>();


        string location = "";


        if (
            !string.IsNullOrEmpty(
                data.resolved_location
            )
        )
        {
            location =
                data.resolved_location;
        }


        if (
            !string.IsNullOrEmpty(
                data.resolved_country
            )
        )
        {
            if (location.Length > 0)
            {
                location += ", ";
            }

            location +=
                data.resolved_country;
        }


        if (location.Length > 0)
        {
            lines.Add(location);
        }


        lines.Add(
            data.temperature.ToString("0.#")
            + "°C"
        );


        lines.Add(
            "Humidity: "
            + data.humidity.ToString("0.#")
            + "%"
        );


        lines.Add(
            "Wind: "
            + data.wind.ToString("0.#")
            + " km/h"
        );


        if (data.gustiness > 0.1f)
        {
            lines.Add(
                "Gusts: "
                + data.gustiness.ToString("0.#")
                + " km/h"
            );
        }


        if (data.precipitation > 0.01f)
        {
            lines.Add(
                "Rain: "
                + data.precipitation.ToString("0.#")
                + " mm"
            );
        }


        if (data.snow > 0.01f)
        {
            lines.Add(
                "Snow: "
                + data.snow.ToString("0.#")
            );
        }


        weatherText.text =
            string.Join(
                "\n",
                lines
            );
    }


    private IEnumerator LoadOutfitImages(
        RecommendationItem[] recommendations
    )
    {
        RawImage[] slots =
        {
            outfitImage1,
            outfitImage2,
            outfitImage3
        };


        // Clear whatever placeholder white boxes are there.
        foreach (RawImage slot in slots)
        {
            if (slot == null)
            {
                continue;
            }

            slot.texture = null;
            slot.color = Color.clear;
        }


        if (recommendations == null)
        {
            Debug.LogError(
                "Backend returned no recommendations."
            );

            yield break;
        }


        int count =
            Mathf.Min(
                3,
                recommendations.Length
            );


        for (int i = 0; i < count; i++)
        {
            RawImage target =
                slots[i];

            RecommendationItem recommendation =
                recommendations[i];


            if (
                target == null ||
                recommendation == null ||
                string.IsNullOrEmpty(
                    recommendation.image_url
                )
            )
            {
                continue;
            }


            Debug.Log(
                "Downloading outfit "
                + (i + 1)
                + ": "
                + recommendation.image_url
            );


            using UnityWebRequest imageRequest =
                UnityWebRequestTexture.GetTexture(
                    recommendation.image_url
                );


            imageRequest.timeout = 180;


            yield return
                imageRequest.SendWebRequest();


            if (
                imageRequest.result !=
                UnityWebRequest.Result.Success
            )
            {
                Debug.LogError(
                    "OUTFIT IMAGE FAILED:\n"
                    + recommendation.image_url
                    + "\n"
                    + imageRequest.error
                );

                continue;
            }


            Texture2D texture =
                DownloadHandlerTexture.GetContent(
                    imageRequest
                );


            target.texture =
                texture;

            target.color =
                Color.white;
        }
    }
}


// ========================================================
// JSON DATA CLASSES
// ========================================================

public class RecommendationRequestBody
{
    public string text { get; set; }
}


public class RecommendationApiResponse
{
    public string input { get; set; }

    public string environment { get; set; }

    public RecommendationEnvironmentData
        environment_data
    {
        get;
        set;
    }

    public RecommendationItem[]
        recommendations
    {
        get;
        set;
    }
}


public class RecommendationEnvironmentData
{
    public float temperature { get; set; }

    public float humidity { get; set; }

    public float wind { get; set; }

    public float gustiness { get; set; }

    public float precipitation { get; set; }

    public float water { get; set; }

    public float altitude { get; set; }

    public float mud { get; set; }

    public float snow { get; set; }

    public float wind_direction { get; set; }

    public string resolved_location
    {
        get;
        set;
    }

    public string resolved_country
    {
        get;
        set;
    }
}


public class RecommendationItem
{
    public string id { get; set; }

    public float score { get; set; }

    public string category { get; set; }

    public string gender { get; set; }

    public string material { get; set; }

    public string pattern { get; set; }

    public string length { get; set; }

    public string description { get; set; }

    public string image_url { get; set; }
}