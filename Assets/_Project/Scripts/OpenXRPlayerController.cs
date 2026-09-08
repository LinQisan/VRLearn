using UnityEngine;

/// <summary>
/// Lightweight CharacterController locomotion driven by OpenXR controller input.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public sealed class OpenXRPlayerController : MonoBehaviour
{
    [Min(0f)] public float Acceleration = 0.1f;
    [Min(0f)] public float MoveSpeed = 2f;
    [Min(0f)] public float Gravity = 9.81f;

    CharacterController characterController;
    OVRPlayerController metaPlayerController;
    float verticalVelocity;
    bool gravityEnabled = true;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    public void BindMetaController(OVRPlayerController controller)
    {
        metaPlayerController = controller;
        SyncMetaSettings();
    }

    void OnEnable()
    {
        if (metaPlayerController != null)
            metaPlayerController.enabled = true;
    }

    void OnDisable()
    {
        if (metaPlayerController != null)
            metaPlayerController.enabled = false;
    }

    void Update()
    {
        if (metaPlayerController != null)
        {
            SyncMetaSettings();
            return;
        }

        if (!characterController.enabled)
            return;

        var cameraTransform = OpenXRScene.MainCameraTransform;
        UpdateCapsuleFromTrackedHead(cameraTransform);
        var forward = cameraTransform != null ? cameraTransform.forward : transform.forward;
        var right = cameraTransform != null ? cameraTransform.right : transform.right;
        forward.y = 0f;
        right.y = 0f;
        forward.Normalize();
        right.Normalize();

        var stick = OpenXRInput.LeftStick;
        // Keep the legacy OVRPlayerController Acceleration tuning meaningful. The original
        // scene uses 0.04 and gameplay raises it to 0.1 for faster sections.
        var horizontal = Vector3.ClampMagnitude(forward * stick.y + right * stick.x, 1f)
            * Acceleration * MoveSpeed * 10f;

        if (gravityEnabled)
        {
            if (characterController.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -1f;
            else
                verticalVelocity -= Gravity * Time.deltaTime;
        }
        else
        {
            verticalVelocity = 0f;
        }

        characterController.Move((horizontal + Vector3.up * verticalVelocity) * Time.deltaTime);
    }

    private void SyncMetaSettings()
    {
        if (metaPlayerController == null)
            return;
        metaPlayerController.Acceleration = Acceleration;
        metaPlayerController.GravityModifier = gravityEnabled ? 1f : 0f;
    }

    public void SetGravityEnabled(bool enabled)
    {
        gravityEnabled = enabled;
        SyncMetaSettings();
        if (!enabled)
            verticalVelocity = 0f;
    }

    private void UpdateCapsuleFromTrackedHead(Transform cameraTransform)
    {
        if (cameraTransform == null)
            return;

        // In Floor tracking mode the XR Origin is the floor reference. Keep the
        // capsule bottom at that reference and let its center follow room-scale
        // head movement instead of using the legacy negative OVR center.
        var localHead = transform.InverseTransformPoint(cameraTransform.position);
        var height = Mathf.Max(characterController.radius * 2f, localHead.y);
        characterController.height = height;
        characterController.center = new Vector3(
            localHead.x,
            height * 0.5f + characterController.skinWidth,
            localHead.z);
    }
}
