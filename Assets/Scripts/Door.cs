using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class Door : MonoBehaviour
{
    private const float RepeatKnockCooldownSeconds = 2f;

    public GameObject loading;
    public GameObject kaimen;
    public GameObject qiaomen;
    public float openAngle = 45f;
    public float openSpeed = 1f;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpening;
    private bool hasStartedOpeningFlow;
    private bool hasCompletedOpenFlow;
    private float lastKnockTime = float.NegativeInfinity;
    private bool wasPrimaryButtonPressed;
    private readonly List<InputDevice> rightHandDevices = new List<InputDevice>();

    private void Awake()
    {
        EnsureTriggerCollider();
        EnsureKinematicRigidbody();
    }

    private void Start()
    {
        closedRotation = transform.rotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
    }

    public void Open()
    {
        if (hasStartedOpeningFlow)
        {
            return;
        }

        hasStartedOpeningFlow = true;
        PlayKnock();

        StartCoroutine(DelayedOpenAction());
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasStartedOpeningFlow)
        {
            TryPlayRepeatKnock();
            return;
        }

        Open();
    }

    private IEnumerator DelayedOpenAction()
    {
        yield return new WaitForSeconds(2f);

        if (kaimen != null)
        {
            kaimen.SetActive(true);
        }

        isOpening = true;
    }

    private void Update()
    {
        HandleEditorKnockKey();
        HandlePrimaryButtonKnock();

        if (!isOpening || hasCompletedOpenFlow)
        {
            return;
        }

        transform.rotation = Quaternion.Lerp(
            transform.rotation,
            openRotation,
            Time.deltaTime * openSpeed
        );

        if (Quaternion.Angle(transform.rotation, openRotation) < 1f)
        {
            hasCompletedOpenFlow = true;
            isOpening = false;
            StartCoroutine(DelayedAction());
        }
    }

    private IEnumerator DelayedAction()
    {
        yield return new WaitForSeconds(2f);
        ChangeScene();
    }

    private void ChangeScene()
    {
        if (loading != null)
        {
            loading.SetActive(true);
        }

        StartCoroutine(LoadSceneAsync());
    }

    private IEnumerator LoadSceneAsync()
    {
        VoiceServerDiscoveryUI.ApplySelectedServerToVoiceChatManager();
        int goScene = 1;
        switch (SceneController.curSceneType)
        {
            case SceneType.OldMan:
                goScene = 2;
                break;
            case SceneType.Boy:
                goScene = 6;
                break;
            case SceneType.Girl:
                goScene = 4;
                break;
            default:
                break;
        }

        AsyncOperation operation = SceneManager.LoadSceneAsync(goScene);
        operation.allowSceneActivation = true;

        while (!operation.isDone)
        {
            yield return null;
        }
    }

    private void HandlePrimaryButtonKnock()
    {
        bool isPressed = IsRightPrimaryButtonPressed();
        if (isPressed && !wasPrimaryButtonPressed)
        {
            TriggerKnock();
        }

        wasPrimaryButtonPressed = isPressed;
    }

    private void HandleEditorKnockKey()
    {
#if UNITY_EDITOR || UNITY_STANDALONE_WIN
        if (Input.GetKeyDown(KeyCode.A))
        {
            TriggerKnock();
        }
#endif
    }

    private void TriggerKnock()
    {
        if (hasStartedOpeningFlow)
        {
            TryPlayRepeatKnock();
        }
        else
        {
            Open();
        }
    }

    private bool IsRightPrimaryButtonPressed()
    {
        rightHandDevices.Clear();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, rightHandDevices);

        for (int i = 0; i < rightHandDevices.Count; i++)
        {
            bool isPressed;
            if (rightHandDevices[i].TryGetFeatureValue(CommonUsages.primaryButton, out isPressed) && isPressed)
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureTriggerCollider()
    {
        Collider triggerCollider = GetComponentInChildren<BoxCollider>(true);
        if (triggerCollider == null)
        {
            triggerCollider = GetComponentInChildren<Collider>(true);
        }

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void EnsureKinematicRigidbody()
    {
        Rigidbody body = GetComponent<Rigidbody>();
        if (body == null)
        {
            body = gameObject.AddComponent<Rigidbody>();
        }

        body.isKinematic = true;
        body.useGravity = false;
    }

    private void PlayKnock()
    {
        lastKnockTime = Time.time;
        if (qiaomen != null)
        {
            qiaomen.SetActive(false);
            qiaomen.SetActive(true);
        }
    }

    private void TryPlayRepeatKnock()
    {
        if (Time.time - lastKnockTime < RepeatKnockCooldownSeconds)
        {
            return;
        }

        PlayKnock();
    }
}
