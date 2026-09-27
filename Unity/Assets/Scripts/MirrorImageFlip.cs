using UnityEngine;
using UnityEngine.UI;

public class MirrorImageFlip : MonoBehaviour
{
    [SerializeField]
    private RawImage rawImage;

    private void Awake()
    {
        if (rawImage == null)
        {
            rawImage =
                GetComponent<RawImage>();
        }

        rawImage.uvRect =
            new Rect(
                1f,
                0f,
                -1f,
                1f
            );
    }
}