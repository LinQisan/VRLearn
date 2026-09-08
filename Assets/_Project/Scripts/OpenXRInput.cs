using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

/// <summary>
/// Device-agnostic controller input supplied by the active OpenXR loader.
/// </summary>
public static class OpenXRInput
{
    const float DirectionThreshold = 0.55f;

    static int cachedFrame = -1;
    static bool primaryButton;
    static bool secondaryButton;
    static bool previousPrimaryButton;
    static bool previousSecondaryButton;
    static Vector2 leftStick;
    static Vector2 rightStick;
    static Vector2 previousLeftStick;

    public static bool IsEditorSimulation => Application.isEditor;

    public static Vector2 LeftStick
    {
        get { Refresh(); return leftStick; }
    }

    public static Vector2 RightStick
    {
        get { Refresh(); return rightStick; }
    }

    public static bool PrimaryButtonDown
    {
        get { Refresh(); return primaryButton && !previousPrimaryButton; }
    }

    public static bool SecondaryButtonDown
    {
        get { Refresh(); return secondaryButton && !previousSecondaryButton; }
    }

    public static bool LeftStickUp => LeftStick.y > DirectionThreshold;
    public static bool LeftStickDown => LeftStick.y < -DirectionThreshold;
    public static bool LeftStickRight => LeftStick.x > DirectionThreshold;
    public static bool LeftStickLeft => LeftStick.x < -DirectionThreshold;

    public static bool LeftStickDirectionReleased
    {
        get
        {
            Refresh();
            return previousLeftStick.sqrMagnitude >= DirectionThreshold * DirectionThreshold &&
                   leftStick.sqrMagnitude < DirectionThreshold * DirectionThreshold;
        }
    }

    public static void SetControllerVibration(float amplitude, float duration = 0.1f)
    {
        SetControllerVibration(XRNode.LeftHand, amplitude, duration);
        SetControllerVibration(XRNode.RightHand, amplitude, duration);
    }

    public static void StopControllerVibration()
    {
        StopControllerVibration(XRNode.LeftHand);
        StopControllerVibration(XRNode.RightHand);
    }

    static void SetControllerVibration(XRNode node, float amplitude, float duration)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid && device.TryGetHapticCapabilities(out var capabilities) &&
            capabilities.supportsImpulse)
        {
            device.SendHapticImpulse(0, Mathf.Clamp01(amplitude), Mathf.Max(0.01f, duration));
        }
    }

    static void StopControllerVibration(XRNode node)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid)
            device.StopHaptics();
    }

    static void Refresh()
    {
        if (cachedFrame == Time.frameCount)
            return;

        cachedFrame = Time.frameCount;
        previousPrimaryButton = primaryButton;
        previousSecondaryButton = secondaryButton;
        previousLeftStick = leftStick;

        if (IsEditorSimulation)
        {
            ReadEditorKeyboard();
            return;
        }

        ReadController(XRNode.LeftHand, out leftStick, out var leftPrimary, out var leftSecondary);
        ReadController(XRNode.RightHand, out rightStick, out var rightPrimary, out var rightSecondary);

        // Treat the equivalent face buttons on either controller as the same action:
        // Primary is Left X / Right A, Secondary is Left Y / Right B.
        primaryButton = leftPrimary || rightPrimary;
        secondaryButton = leftSecondary || rightSecondary;
    }

    static void ReadEditorKeyboard()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null)
        {
            leftStick = Vector2.zero;
            rightStick = Vector2.zero;
            primaryButton = false;
            secondaryButton = false;
            return;
        }

        leftStick = new Vector2(
            (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f),
            (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f));
        leftStick = Vector2.ClampMagnitude(leftStick, 1f);
        rightStick = Vector2.zero;
        primaryButton = keyboard.enterKey.isPressed || keyboard.spaceKey.isPressed;
        secondaryButton = keyboard.tabKey.isPressed;
    }

    static void ReadController(XRNode node, out Vector2 stick, out bool primary, out bool secondary)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        stick = Vector2.zero;
        primary = false;
        secondary = false;

        if (!device.isValid)
            return;

        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primary2DAxis, out stick);
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out primary);
        device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out secondary);
    }
}
