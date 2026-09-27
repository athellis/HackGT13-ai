using UnityEngine;

public class VirtualMirrorController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera mirrorCamera;

    [SerializeField]
    private Transform avatarRoot;


    [Header("Camera Framing")]
    [SerializeField]
    private float distanceFromAvatar = 2.5f;

    [SerializeField]
    private float fieldOfView = 40f;


    private Renderer[] avatarRenderers;


    private void Start()
    {
        if (mirrorCamera == null)
        {
            Debug.LogError(
                "VirtualMirrorController: Mirror Camera is not assigned."
            );

            return;
        }


        if (avatarRoot == null)
        {
            Debug.LogError(
                "VirtualMirrorController: Avatar Root is not assigned."
            );

            return;
        }


        avatarRenderers =
            avatarRoot.GetComponentsInChildren<Renderer>();


        if (
            avatarRenderers == null ||
            avatarRenderers.Length == 0
        )
        {
            Debug.LogError(
                "VirtualMirrorController: No renderers found under AvatarRoot."
            );

            return;
        }


        mirrorCamera.fieldOfView =
            fieldOfView;


        UpdateMirrorCamera();
    }


    private void LateUpdate()
    {
        if (
            mirrorCamera == null ||
            avatarRoot == null ||
            avatarRenderers == null ||
            avatarRenderers.Length == 0
        )
        {
            return;
        }


        UpdateMirrorCamera();
    }


    private void UpdateMirrorCamera()
    {
        Bounds bounds =
            avatarRenderers[0].bounds;


        for (
            int i = 1;
            i < avatarRenderers.Length;
            i++
        )
        {
            if (avatarRenderers[i].enabled)
            {
                bounds.Encapsulate(
                    avatarRenderers[i].bounds
                );
            }
        }


        // Actual visible center of the avatar.
        Vector3 avatarCenter =
            bounds.center;


        // Put camera in front of avatar.
        Vector3 cameraPosition =
            avatarCenter
            + avatarRoot.forward
            * distanceFromAvatar;


        mirrorCamera.transform.position =
            cameraPosition;


        // Aim directly at actual body center.
        Vector3 direction =
            avatarCenter
            - mirrorCamera.transform.position;


        if (direction.sqrMagnitude > 0.001f)
        {
            mirrorCamera.transform.rotation =
                Quaternion.LookRotation(
                    direction,
                    Vector3.up
                );
        }


        mirrorCamera.fieldOfView =
            fieldOfView;
    }
}