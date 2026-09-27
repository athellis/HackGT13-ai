using UnityEngine;

/// <summary>
/// Editor / PC Play Mode keys. On Quest 2 use FashionAPIClient.RequestOutfit from UI/voice instead.
/// </summary>
public class WardrobeTestHotkeys : MonoBehaviour
{
    public WardrobeDresser dresser;

    [Tooltip("Pressed with 1")]
    public string idKey1 = "white_tee";

    [Tooltip("Pressed with 2")]
    public string idKey2 = "black_tee";

    private void Awake()
    {
        if (dresser == null)
        {
            dresser = GetComponent<WardrobeDresser>();
        }
    }

    private void Update()
    {
        if (dresser == null)
        {
            return;
        }

        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            dresser.EquipById(idKey1);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            dresser.EquipById(idKey2);
        }

        if (Input.GetKeyDown(KeyCode.Alpha0))
        {
            dresser.ClearSlot(GarmentSlot.Upper);
        }
    }
}
