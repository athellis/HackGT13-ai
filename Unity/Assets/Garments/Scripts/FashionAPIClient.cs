using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

/// <summary>
/// Asks the fashion backend for a DeepFashion2-style recommendation
/// and swaps the matching prefab onto the avatar.
/// Default URL uses port 8001 so it does not collide with EnvironmentApiController on 8000.
/// </summary>
public class FashionAPIClient : MonoBehaviour
{
    [Header("Backend")]
    [Tooltip("Point this at your teammate api.py. Typical: http://127.0.0.1:8000/recommend")]
    public string backendUrl = "http://127.0.0.1:8000/recommend";

    [Header("Systems")]
    public WardrobeDresser dresser;

    [Tooltip("Optional. If set, the same occasion text can also drive the room walls.")]
    public EnvironmentApiController environmentApi;

    [Tooltip("When true, a fashion request also forwards text to the environment API.")]
    public bool alsoRequestEnvironment = false;

    private void Awake()
    {
        if (dresser == null)
        {
            dresser = GetComponent<WardrobeDresser>();
        }
    }

    public void RequestOutfit(string userText)
    {
        if (string.IsNullOrWhiteSpace(userText))
        {
            Debug.LogWarning("No user text was supplied.");
            return;
        }

        StartCoroutine(Send(userText));

        if (alsoRequestEnvironment && environmentApi != null)
        {
            environmentApi.AnalyzeRequest(userText);
        }
    }

    [ContextMenu("Test: recommend a tee")]
    public void TestRecommendTee()
    {
        RequestOutfit("put on a t-shirt");
    }

    [ContextMenu("Test: Himalaya climb")]
    public void TestHimalaya()
    {
        RequestOutfit("I am climbing the Himalayas");
    }

    private IEnumerator Send(string userText)
    {
        FashionRequest requestBody = new FashionRequest
        {
            text = userText
        };

        string json = JsonConvert.SerializeObject(requestBody);

        using UnityWebRequest request = new UnityWebRequest(backendUrl, "POST");
        byte[] body = System.Text.Encoding.UTF8.GetBytes(json);
        request.uploadHandler = new UploadHandlerRaw(body);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "application/json");

        Debug.Log("Fashion request:\n" + userText);
        yield return request.SendWebRequest();

        if (request.result != UnityWebRequest.Result.Success)
        {
            Debug.LogError(
                "Fashion backend failed:\n"
                + request.error
                + "\n"
                + request.downloadHandler.text
            );
            yield break;
        }

        Debug.Log("Fashion response:\n" + request.downloadHandler.text);

        FashionResponse response =
            JsonConvert.DeserializeObject<FashionResponse>(
                request.downloadHandler.text
            );

        if (response == null)
        {
            Debug.LogError("Could not parse fashion response.");
            yield break;
        }

        ApplyResponse(response);
    }

    private void ApplyResponse(FashionResponse response)
    {
        if (dresser == null)
        {
            Debug.LogError("WardrobeDresser is not assigned.");
            return;
        }

        bool equipped = false;

        if (response.recommendations != null)
        {
            for (int i = 0; i < response.recommendations.Length; i++)
            {
                FashionRecommendation rec = response.recommendations[i];
                if (rec == null)
                {
                    continue;
                }

                string category = FirstNonEmpty(
                    rec.category,
                    rec.deepfashion2,
                    rec.deep_fashion_category
                );

                if (!string.IsNullOrEmpty(rec.id) && dresser.EquipById(rec.id))
                {
                    equipped = true;
                    continue;
                }

                if (!string.IsNullOrEmpty(category) && dresser.EquipByCategory(category))
                {
                    equipped = true;
                }
            }
        }

        if (!equipped && !string.IsNullOrEmpty(response.garment_id))
        {
            equipped = dresser.EquipById(response.garment_id);
        }

        string rootCategory = FirstNonEmpty(
            response.category,
            response.environment
        );

        if (!equipped && !string.IsNullOrEmpty(rootCategory))
        {
            equipped = dresser.EquipByCategory(rootCategory);
        }

        if (!equipped)
        {
            Debug.LogWarning(
                "Fashion API returned no garment that matches a prefab. "
                + "GarmentInfo.category / deepFashionCategory must match the JSON category."
            );
        }
    }

    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null)
        {
            return null;
        }

        for (int i = 0; i < values.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(values[i]))
            {
                return values[i];
            }
        }

        return null;
    }
}

public class FashionRequest
{
    public string text { get; set; }
}

public class FashionResponse
{
    public string category { get; set; }
    public string garment_id { get; set; }
    public string environment { get; set; }
    public FashionRecommendation[] recommendations { get; set; }
}

public class FashionRecommendation
{
    public string id { get; set; }
    public float score { get; set; }
    public string category { get; set; }
    public string deepfashion2 { get; set; }
    public string deep_fashion_category { get; set; }
    public string gender { get; set; }
    public string description { get; set; }
}
