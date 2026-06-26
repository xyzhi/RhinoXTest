using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

public class Door : MonoBehaviour
{
    private const string LeftHandTag = "LeftHand";
    private const string RightHandTag = "RightHand";
    private const float RepeatKnockCooldownSeconds = 2f;

    public GameObject loading;
    public GameObject kaimen;
    public GameObject qiaomen;
    public float openAngle = 45f;
    public float openSpeed = 1f;

    // 开门前的语音门禁：用户敲门后必须连续说够一段时间，才继续执行开门流程。
    private const bool RequireSpeechBeforeOpen = true;
    // 判定“说话有效”的最短时长，达到这个时长后直接通过。
    private const float RequiredSpeechSeconds = 3f;
    // 单轮最长侦听时间。超时还没说够，会结束这一轮并重新开始侦听。
    private const float SpeechListenMaxSeconds = 12f;
    // 已确认说话后，如果连续低于静音阈值这么久，就认为这句话结束。
    private const float SpeechEndSilenceSeconds = 1.2f;
    // 已经开始说话后的静音判断阈值，低于它会累计静音时间。
    private const float SilenceThreshold = 0.008f;
    // 从未检测到说话时，用这个较高阈值判断“可能开始说话了”。
    private const float SpeechStartThreshold = 0.01f;
    // 刚开始录音时给麦克风一点稳定时间，避免启动瞬间杂音被当成说话。
    private const float SpeechStartGraceSeconds = 0.5f;
    // 声音超过开始阈值后，必须持续这么久才确认是真的说话。
    private const float SpeechConfirmSeconds = 0.06f;
    // 候选说话阶段如果短暂掉到阈值以下，超过这个时间就取消候选。
    private const float SpeechCandidateDropoutSeconds = 0.35f;
    private const int SpeechSampleRate = 16000;

    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpening;
    private bool hasStartedOpeningFlow;
    private bool hasCompletedOpenFlow;
    private float lastKnockTime = float.NegativeInfinity;
    private AudioClip speechGateClip;
    private float[] speechSampleBuffer;
    private bool wasPrimaryButtonPressed;
    private readonly List<InputDevice> rightHandDevices = new List<InputDevice>();
    //public GameObject[] people;

    private void Awake()
    {
        EnsureTriggerCollider();
        EnsureKinematicRigidbody();
        //for (int i = 0; i < people.Length; i++)
        //{
        //    people[i].SetActive((int)SceneController.curSceneType - 1 == i);
        //}
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
        //if (other == null || !IsHandCollider(other.transform))
        //{
        //    return;
        //}

        if (hasStartedOpeningFlow)
        {
            TryPlayRepeatKnock();
            return;
        }

        Open();
    }

    private IEnumerator DelayedOpenAction()
    {
        if (RequireSpeechBeforeOpen)
        {
            // 敲门音效已经播放，这里阻塞等待用户说话达标；不达标会在内部反复重听。
            yield return WaitForValidSpeechBeforeOpen();
        }

        yield return new WaitForSeconds(5f);

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

    private IEnumerator WaitForValidSpeechBeforeOpen()
    {
        while (enabled && !hasCompletedOpenFlow)
        {
            // 没有麦克风时不放行，等待设备恢复后再重新尝试。
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[Door] No microphone device found. Waiting before retry.");
                yield return new WaitForSeconds(1f);
                continue;
            }

            // ListenForSpeechAttempt 只负责侦听一轮，并把本轮有效说话时长回传。
            float spokenSeconds = 0f;
            yield return ListenForSpeechAttempt(value => spokenSeconds = value);

            if (spokenSeconds >= RequiredSpeechSeconds)
            {
                Debug.Log("[Door] Speech gate passed. spokenSeconds=" + spokenSeconds.ToString("F2"));
                yield break;
            }

            // 说话太短或本轮超时，都不会开门；稍等一下后重新开始下一轮侦听。
            Debug.Log("[Door] Speech too short. spokenSeconds=" + spokenSeconds.ToString("F2") + ", required=" + RequiredSpeechSeconds.ToString("F2") + ". Restart listening.");
            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator ListenForSpeechAttempt(System.Action<float> onComplete)
    {
        // 开新一轮前先停掉旧录音，避免上一次 Microphone clip 残留影响本轮判断。
        StopSpeechGateMicrophone();

        int recordSeconds = Mathf.Max(1, Mathf.CeilToInt(SpeechListenMaxSeconds + 1f));
        speechGateClip = Microphone.Start(null, true, recordSeconds, SpeechSampleRate);
        float listenStartTime = Time.realtimeSinceStartup;
        float speechStartTime = -1f;
        float lastSpeechTime = -1f;
        float speechCandidateStartTime = -1f;
        float speechCandidateLastLoudTime = -1f;
        float spokenSeconds = 0f;
        bool hasDetectedSpeech = false;

        Debug.Log("[Door] Start listening before opening.");

        while (speechGateClip != null && Time.realtimeSinceStartup - listenStartTime < SpeechListenMaxSeconds)
        {
            int position = Microphone.GetPosition(null);
            if (position > 0)
            {
                float now = Time.realtimeSinceStartup;
                float elapsed = now - listenStartTime;
                float level = GetRecentLevel(speechGateClip, position, 4096);

                // 录音刚启动时可能有瞬时噪声，宽限期内不做“开始说话”判断。
                if (!hasDetectedSpeech && elapsed < SpeechStartGraceSeconds)
                {
                    yield return null;
                    continue;
                }

                if (!hasDetectedSpeech && level >= SpeechStartThreshold)
                {
                    // 第一次超过开始阈值时，只标记为“候选说话”，暂时不算正式开始。
                    if (speechCandidateStartTime < 0f)
                    {
                        speechCandidateStartTime = now;
                        Debug.Log("[Door] Speech candidate started. level=" + level.ToString("F4") + ", threshold=" + SpeechStartThreshold.ToString("F4"));
                    }

                    speechCandidateLastLoudTime = now;
                    if (now - speechCandidateStartTime >= SpeechConfirmSeconds)
                    {
                        // 声音持续超过阈值一小段时间后，才确认用户真的开始说话。
                        hasDetectedSpeech = true;
                        speechStartTime = speechCandidateStartTime;
                        lastSpeechTime = now;
                        Debug.Log("[Door] Speech confirmed. level=" + level.ToString("F4"));
                    }
                }
                else if (!hasDetectedSpeech)
                {
                    // 候选阶段如果声音断掉太久，说明刚才可能只是噪声，重置候选状态。
                    if (speechCandidateStartTime >= 0f && now - speechCandidateLastLoudTime > SpeechCandidateDropoutSeconds)
                    {
                        speechCandidateStartTime = -1f;
                        speechCandidateLastLoudTime = -1f;
                    }
                }
                else
                {
                    // 已确认说话后，只要音量还高于静音阈值，就刷新最后一次有声时间。
                    if (level >= SilenceThreshold)
                    {
                        lastSpeechTime = now;
                    }

                    spokenSeconds = now - speechStartTime;
                    if (spokenSeconds >= RequiredSpeechSeconds || now - lastSpeechTime >= SpeechEndSilenceSeconds)
                    {
                        // 说够时长直接通过；或者用户停顿太久，则结束本轮并交给外层判断是否重听。
                        break;
                    }
                }
            }

            yield return null;
        }

        StopSpeechGateMicrophone();
        onComplete?.Invoke(spokenSeconds);
    }

    IEnumerator LoadSceneAsync()
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

    private float GetRecentLevel(AudioClip clip, int position, int sampleCount)
    {
        if (clip == null || position <= 0)
        {
            return 0f;
        }

        int count = Mathf.Min(sampleCount, position, clip.samples);
        if (count <= 0)
        {
            return 0f;
        }

        if (speechSampleBuffer == null || speechSampleBuffer.Length != count)
        {
            speechSampleBuffer = new float[count];
        }

        int start = Mathf.Max(0, position - count);
        clip.GetData(speechSampleBuffer, start);

        // 用最近一小段采样的平均绝对值估算音量，数值越大代表当前越“有声”。
        float sum = 0f;
        for (int i = 0; i < count; i++)
        {
            sum += Mathf.Abs(speechSampleBuffer[i]);
        }

        return sum / count;
    }

    private void StopSpeechGateMicrophone()
    {
        if (Microphone.IsRecording(null))
        {
            Microphone.End(null);
        }

        speechGateClip = null;
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

    private bool IsHandCollider(Transform target)
    {
        Transform current = target;
        while (current != null)
        {
            if (current.CompareTag(LeftHandTag) || current.CompareTag(RightHandTag))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
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

    private void OnDisable()
    {
        StopSpeechGateMicrophone();
    }
}
