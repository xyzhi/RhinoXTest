using System;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public sealed class SteamVROpenXRBootstrap : MonoBehaviour
{
    const string SteamVrUri = "steam://rungameid/250820";
    const float RayLength = 20f;
    const float RayWidth = 0.01f;

    static bool launchAttempted;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
    static void LaunchBeforeXRStarts()
    {
        LaunchSteamVROnce();
    }

    void Awake()
    {
        Application.runInBackground = true;
        LaunchSteamVROnce();
        EnsureControllerRays();
    }

    static void LaunchSteamVROnce()
    {
        if (launchAttempted)
            return;

        launchAttempted = true;
        LaunchSteamVRIfNeeded();
    }

    static void LaunchSteamVRIfNeeded()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        if (IsSteamVRRunning())
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SteamVrUri,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            UnityEngine.Debug.LogWarning("Failed to launch SteamVR: " + exception.Message);
        }
#endif
    }

    static bool IsSteamVRRunning()
    {
        return Process.GetProcessesByName("vrmonitor").Length > 0
            || Process.GetProcessesByName("vrserver").Length > 0
            || Process.GetProcessesByName("vrcompositor").Length > 0;
    }

    void EnsureControllerRays()
    {
        if (FindObjectOfType<XRRayInteractor>() != null)
            return;

        CreateControllerRay("Left Controller Ray", true, new Vector3(-0.25f, 1.2f, 0.45f));
        CreateControllerRay("Right Controller Ray", false, new Vector3(0.25f, 1.2f, 0.45f));
    }

    void CreateControllerRay(string objectName, bool leftHand, Vector3 fallbackLocalPosition)
    {
        var rayObject = new GameObject(objectName);
        rayObject.transform.SetParent(transform, false);
        rayObject.transform.localPosition = fallbackLocalPosition;
        rayObject.transform.localRotation = Quaternion.identity;

        var controller = rayObject.AddComponent<ActionBasedController>();
        ConfigureControllerInput(controller, leftHand);

        var interactor = rayObject.AddComponent<XRRayInteractor>();
        interactor.xrController = controller;
        interactor.lineType = XRRayInteractor.LineType.StraightLine;
        interactor.maxRaycastDistance = RayLength;
        interactor.enableUIInteraction = true;
        interactor.selectActionTrigger = XRBaseControllerInteractor.InputTriggerType.State;

        var lineRenderer = rayObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = true;
        lineRenderer.widthMultiplier = RayWidth;
        lineRenderer.numCapVertices = 4;
        lineRenderer.material = CreateRayMaterial(leftHand ? Color.cyan : Color.yellow);

        var lineVisual = rayObject.AddComponent<XRInteractorLineVisual>();
        lineVisual.overrideInteractorLineLength = true;
        lineVisual.lineLength = RayLength;
        lineVisual.lineWidth = RayWidth;
        lineVisual.validColorGradient = CreateGradient(leftHand ? Color.cyan : Color.yellow);
        lineVisual.invalidColorGradient = CreateGradient(new Color(1f, 0.25f, 0.2f));

        var visibility = rayObject.AddComponent<ControllerRayVisibility>();
        visibility.Initialize(controller, interactor, lineRenderer, lineVisual);
    }

    static void ConfigureControllerInput(ActionBasedController controller, bool leftHand)
    {
        var hand = leftHand ? "LeftHand" : "RightHand";
        controller.positionAction = ActionProperty("Position", "Vector3",
            "<XRController>{" + hand + "}/pointerPosition",
            "<XRController>{" + hand + "}/devicePosition");
        controller.rotationAction = ActionProperty("Rotation", "Quaternion",
            "<XRController>{" + hand + "}/pointerRotation",
            "<XRController>{" + hand + "}/deviceRotation");
        controller.trackingStateAction = ActionProperty("Tracking State", "Integer",
            "<XRController>{" + hand + "}/trackingState");
        controller.selectAction = ActionProperty("Select", "Button",
            "<XRController>{" + hand + "}/gripPressed",
            "<XRController>{" + hand + "}/triggerPressed");
        controller.selectActionValue = ActionProperty("Select Value", "Axis",
            "<XRController>{" + hand + "}/grip",
            "<XRController>{" + hand + "}/trigger");
        controller.activateAction = ActionProperty("Activate", "Button",
            "<XRController>{" + hand + "}/triggerPressed");
        controller.activateActionValue = ActionProperty("Activate Value", "Axis",
            "<XRController>{" + hand + "}/trigger");
        controller.uiPressAction = ActionProperty("UI Press", "Button",
            "<XRController>{" + hand + "}/triggerPressed");
        controller.uiPressActionValue = ActionProperty("UI Press Value", "Axis",
            "<XRController>{" + hand + "}/trigger");
        controller.hapticDeviceAction = ActionProperty("Haptic Device", string.Empty,
            "<XRController>{" + hand + "}/*");
    }

    static InputActionProperty ActionProperty(string actionName, string expectedControlType, params string[] bindings)
    {
        var actionType = expectedControlType == "Button" ? InputActionType.Button : InputActionType.Value;
        var action = new InputAction(actionName, actionType, expectedControlType: expectedControlType);
        for (var index = 0; index < bindings.Length; index++)
            action.AddBinding(bindings[index]);

        return new InputActionProperty(action);
    }

    static Material CreateRayMaterial(Color color)
    {
        var shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");

        var material = new Material(shader);
        material.color = color;
        return material;
    }

    static Gradient CreateGradient(Color color)
    {
        return new Gradient
        {
            colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
        };
    }

    sealed class ControllerRayVisibility : MonoBehaviour
    {
        ActionBasedController controller;
        XRRayInteractor interactor;
        LineRenderer lineRenderer;
        XRInteractorLineVisual lineVisual;
        bool visible = true;

        public void Initialize(
            ActionBasedController actionController,
            XRRayInteractor rayInteractor,
            LineRenderer renderer,
            XRInteractorLineVisual visual)
        {
            controller = actionController;
            interactor = rayInteractor;
            lineRenderer = renderer;
            lineVisual = visual;
            SetVisible(false);
        }

        void LateUpdate()
        {
            if (controller == null || controller.currentControllerState == null)
            {
                SetVisible(false);
                return;
            }

            var trackingState = controller.currentControllerState.inputTrackingState;
            var hasPose = (trackingState & InputTrackingState.Position) != 0
                && (trackingState & InputTrackingState.Rotation) != 0;
            SetVisible(hasPose);
        }

        void SetVisible(bool value)
        {
            if (visible == value)
                return;

            visible = value;
            if (interactor != null)
                interactor.enabled = value;
            if (lineRenderer != null)
                lineRenderer.enabled = value;
            if (lineVisual != null)
                lineVisual.enabled = value;
        }
    }
}
