using UnityEngine;

public class MirrorCameraController : MonoBehaviour
{
    [Header("References")]
    [SerializeField]
    private Camera mirrorCamera;

    [SerializeField]
    private Transform avatarTarget;

    [Header("Camera Position")]
    [SerializeField]
    private Vector3 cameraOffset =
        new Vector3(0f, 1.35f, 2.75f);

    [SerializeField]
    private Vector3 lookOffset =
        new Vector3(0f, 1.05f, 0f);

    [Header("Camera Settings")]
    [SerializeField]
    private float fieldOfView = 38f;

    [SerializeField]
    private float nearClip = 0.05f;

    [SerializeField]
    private float farClip = 20f;

    private void Awake()
    {
        if (mirrorCamera == null)
        {
            mirrorCamera =
                GetComponent<Camera>();
        }

        ConfigureCamera();
    }

    private void LateUpdate()
    {
        if (avatarTarget == null ||
            mirrorCamera == null)
        {
            return;
        }

        Vector3 targetPosition =
            avatarTarget.position +
            cameraOffset;

        transform.position =
            targetPosition;

        Vector3 lookPosition =
            avatarTarget.position +
            lookOffset;

        Vector3 direction =
            lookPosition -
            transform.position;

        if (direction.sqrMagnitude >
            0.0001f)
        {
            transform.rotation =
                Quaternion.LookRotation(
                    direction.normalized,
                    Vector3.up
                );
        }
    }

    private void ConfigureCamera()
    {
        mirrorCamera.fieldOfView =
            fieldOfView;

        mirrorCamera.nearClipPlane =
            nearClip;

        mirrorCamera.farClipPlane =
            farClip;

        mirrorCamera.allowHDR =
            false;

        mirrorCamera.allowMSAA =
            false;

        mirrorCamera.useOcclusionCulling =
            true;
    }

    public void SetAvatar(
        Transform avatar)
    {
        avatarTarget = avatar;
    }
}