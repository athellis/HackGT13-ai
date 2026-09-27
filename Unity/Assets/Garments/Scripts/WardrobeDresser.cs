using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns at most one garment per slot on the SMPL-X avatar.
/// Instantiating a few static meshes is the Quest 2-safe swap path
/// (no cloth sim, no extra skinned copies of the body).
/// </summary>
public class WardrobeDresser : MonoBehaviour
{
    [Tooltip("SMPL-X root — the object that has the SMPLX component")]
    public Transform avatarRoot;

    [Tooltip("Optional. Pushes combined outfit metrics + a local fit score")]
    public ExperienceRoomController room;

    [Tooltip("Drag garment PREFABS from Project (must have GarmentInfo)")]
    public List<GarmentInfo> garmentPrefabs = new List<GarmentInfo>();

    private readonly Dictionary<GarmentSlot, GameObject> equipped =
        new Dictionary<GarmentSlot, GameObject>();

    private void Awake()
    {
        if (room == null)
        {
            room = FindFirstObjectByType<ExperienceRoomController>();
        }
    }

    public bool EquipByCategory(string category)
    {
        GarmentInfo match = FindPrefab(category, byId: false);
        if (match == null)
        {
            Debug.LogWarning("No prefab for category: " + category);
            return false;
        }

        return Equip(match);
    }

    public bool EquipById(string garmentId)
    {
        GarmentInfo match = FindPrefab(garmentId, byId: true);
        if (match == null)
        {
            Debug.LogWarning("No prefab for id: " + garmentId);
            return false;
        }

        return Equip(match);
    }

    public void ClearSlot(GarmentSlot slot)
    {
        if (equipped.TryGetValue(slot, out GameObject instance) && instance != null)
        {
            Destroy(instance);
        }

        equipped.Remove(slot);
        PushOutfitToRoom();
    }

    public bool Equip(GarmentInfo prefabInfo)
    {
        if (prefabInfo == null || avatarRoot == null)
        {
            Debug.LogError("WardrobeDresser needs an avatarRoot and a garment prefab.");
            return false;
        }

        Transform bone = FindBone(avatarRoot, prefabInfo.attachBoneName);
        if (bone == null)
        {
            Debug.LogError("Missing SMPL-X bone: " + prefabInfo.attachBoneName);
            return false;
        }

        GarmentSlot slot = prefabInfo.slot;
        if (slot == GarmentSlot.FullBody)
        {
            ClearSlot(GarmentSlot.Upper);
            ClearSlot(GarmentSlot.Lower);
        }
        else if (slot == GarmentSlot.Upper || slot == GarmentSlot.Lower)
        {
            ClearSlot(GarmentSlot.FullBody);
        }

        if (equipped.TryGetValue(slot, out GameObject old) && old != null)
        {
            Destroy(old);
        }

        GameObject instance = Instantiate(prefabInfo.gameObject, bone);
        instance.name = prefabInfo.garmentId;
        instance.transform.localPosition = prefabInfo.fittedLocalPosition;
        instance.transform.localRotation = Quaternion.Euler(prefabInfo.fittedLocalEuler);
        instance.transform.localScale = prefabInfo.fittedLocalScale;

        GarmentInfo live = instance.GetComponent<GarmentInfo>();
        if (live != null)
        {
            live.ApplyQuestRendererSettings();
        }

        EnsureEnvironmentResponse(instance, prefabInfo);
        equipped[slot] = instance;

        Debug.Log("Equipped " + prefabInfo.garmentId + " (" + prefabInfo.category + ")");
        PushOutfitToRoom();
        return true;
    }

    private GarmentInfo FindPrefab(string key, bool byId)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return null;
        }

        for (int i = 0; i < garmentPrefabs.Count; i++)
        {
            GarmentInfo g = garmentPrefabs[i];
            if (g == null)
            {
                continue;
            }

            if (byId)
            {
                if (string.Equals(g.garmentId, key, System.StringComparison.OrdinalIgnoreCase))
                {
                    return g;
                }
            }
            else if (g.MatchesCategory(key))
            {
                return g;
            }
        }

        return null;
    }

    private void EnsureEnvironmentResponse(GameObject instance, GarmentInfo source)
    {
        GarmentEnvironmentResponse response =
            instance.GetComponent<GarmentEnvironmentResponse>();

        if (response == null)
        {
            response = instance.AddComponent<GarmentEnvironmentResponse>();
        }

        response.insulation = source.insulation;
        response.windResistance = source.windResistance;
        response.waterproofing = source.waterproofing;
        response.breathability = source.breathability;
        response.tightness = source.tightness;
        response.garmentColor = source.garmentColor;
    }

    private void PushOutfitToRoom()
    {
        if (room == null)
        {
            return;
        }

        OutfitMetrics combined = CombineEquippedMetrics();
        room.SetOutfitMetrics(combined);

        float cold = ExperienceModel.ColdExposure(room.CurrentState, combined);
        float heat = ExperienceModel.HeatHumidityDiscomfort(room.CurrentState, combined);
        float wet = ExperienceModel.Wetness(room.CurrentState, combined);
        float slip = ExperienceModel.SlipRisk(room.CurrentState, combined);
        float worst = Mathf.Max(cold, Mathf.Max(heat, Mathf.Max(wet, slip)));
        room.SetExternalFitScore((1f - worst) * 100f);
    }

    private OutfitMetrics CombineEquippedMetrics()
    {
        OutfitMetrics o = new OutfitMetrics();
        int count = 0;
        Color colorSum = Color.black;

        foreach (KeyValuePair<GarmentSlot, GameObject> pair in equipped)
        {
            if (pair.Value == null)
            {
                continue;
            }

            GarmentInfo info = pair.Value.GetComponent<GarmentInfo>();
            if (info == null)
            {
                continue;
            }

            o.insulation = Mathf.Max(o.insulation, info.insulation);
            o.windResistance = Mathf.Max(o.windResistance, info.windResistance);
            o.waterproofing = Mathf.Max(o.waterproofing, info.waterproofing);
            o.traction = Mathf.Max(o.traction, info.traction);
            o.breathability = count == 0
                ? info.breathability
                : Mathf.Min(o.breathability, info.breathability);
            o.tightness = Mathf.Max(o.tightness, info.tightness);
            colorSum += info.garmentColor;
            count++;
        }

        if (count > 0)
        {
            o.dominantColor = colorSum / count;
        }

        return o;
    }

    private static Transform FindBone(Transform root, string boneName)
    {
        if (root.name == boneName)
        {
            return root;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform found = FindBone(root.GetChild(i), boneName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
    }
}
