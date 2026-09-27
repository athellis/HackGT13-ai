using UnityEngine;
using UnityEngine.Rendering;

public enum GarmentSlot
{
    Upper,
    Lower,
    Outer,
    FullBody
}

/// <summary>
/// Metadata on a clothing prefab. Quest 2 path: one static mesh per slot,
/// parented to an SMPL-X bone. DeepFashion2 category names are preferred;
/// "tees" is kept as an alias for short_sleeved_shirt.
/// </summary>
public class GarmentInfo : MonoBehaviour
{
    [Header("Identity")]
    [Tooltip("API / wardrobe key. Example: tees or short_sleeved_shirt")]
    public string category = "tees";

    [Tooltip("Unique id for this specific item, e.g. white_tee")]
    public string garmentId = "white_tee";

    [Tooltip("DeepFashion2 class when the backend uses the dataset names")]
    public string deepFashionCategory = "short_sleeved_shirt";

    public GarmentSlot slot = GarmentSlot.Upper;

    [Header("SMPL-X attach")]
    [Tooltip("Bone this mesh follows. Tees: spine2. Pants: pelvis. Outer: spine3")]
    public string attachBoneName = "spine2";

    [Tooltip("Local pose after you fit the shirt on the avatar in the editor")]
    public Vector3 fittedLocalPosition;

    public Vector3 fittedLocalEuler;

    public Vector3 fittedLocalScale = Vector3.one;

    [Header("Outfit metrics (environment fit)")]
    [Range(0f, 1f)] public float insulation = 0.35f;
    [Range(0f, 1f)] public float windResistance = 0.25f;
    [Range(0f, 1f)] public float waterproofing = 0.15f;
    [Range(0f, 1f)] public float breathability = 0.7f;
    [Range(0f, 1f)] public float traction = 0.5f;
    [Range(0f, 1f)] public float tightness = 0.4f;
    public Color garmentColor = Color.white;

    [Header("Quest 2")]
    public bool disableShadows = true;

    public bool MatchesCategory(string requested)
    {
        if (string.IsNullOrWhiteSpace(requested))
        {
            return false;
        }

        string key = NormalizeCategory(requested);
        return key == NormalizeCategory(category)
            || key == NormalizeCategory(deepFashionCategory)
            || key == NormalizeCategory(garmentId);
    }

    public static string NormalizeCategory(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        string key = raw.Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');

        switch (key)
        {
            case "tee":
            case "tees":
            case "tshirt":
            case "t_shirt":
            case "short_sleeve_shirt":
            case "short_sleeved_shirt":
                return "short_sleeved_shirt";
            case "jacket":
            case "jackets":
            case "coat":
            case "long_sleeved_outwear":
                return "long_sleeved_outwear";
            case "pants":
            case "pant":
            case "trousers":
                return "trousers";
            default:
                return key;
        }
    }

    public OutfitMetrics ToOutfitMetrics()
    {
        return new OutfitMetrics
        {
            insulation = insulation,
            windResistance = windResistance,
            waterproofing = waterproofing,
            breathability = breathability,
            traction = traction,
            tightness = tightness,
            dominantColor = garmentColor
        };
    }

    public void ApplyQuestRendererSettings()
    {
        if (!disableShadows)
        {
            return;
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            renderers[i].shadowCastingMode = ShadowCastingMode.Off;
            renderers[i].receiveShadows = false;
        }
    }

    [ContextMenu("Capture Fitted Transform From This Object")]
    public void CaptureFittedTransform()
    {
        fittedLocalPosition = transform.localPosition;
        fittedLocalEuler = transform.localEulerAngles;
        fittedLocalScale = transform.localScale;
    }
}
