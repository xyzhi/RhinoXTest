using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private const bool RequireSpeechBeforeOpen = true;
    private const float RequiredSpeechSeconds = 3f;
    private const float SpeechListenMaxSeconds = 12f;
    private const float SpeechEndSilenceSeconds = 1.2f;
    private const float SilenceThreshold = 0.01f;
    private const float SpeechStartThreshold = 0.012f;
    private const float SpeechStartGraceSeconds = 0.5f;
    private const float SpeechConfirmSeconds = 0.06f;
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
    public GameObject[] people;

    private void Awake()
    {
        EnsureTriggerCollider();
        EnsureKinematicRigidbody();
        for (int i = 0; i < people.Length; i++)
        {
            people[i].SetActive((int)SceneController.curRole - 1 == i);
        }
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

        StartCoroutine(LoadSceneAsync(2));
    }

    private IEnumerator WaitForValidSpeechBeforeOpen()
    {
        while (enabled && !hasCompletedOpenFlow)
        {
            if (Microphone.devices == null || Microphone.devices.Length == 0)
            {
                Debug.LogWarning("[Door] No microphone device found. Waiting before retry.");
                yield return new WaitForSeconds(1f);
                continue;
            }

            float spokenSeconds = 0f;
            yield return ListenForSpeechAttempt(value => spokenSeconds = value);

            if (spokenSeconds >= RequiredSpeechSeconds)
            {
                Debug.Log("[Door] Speech gate passed. spokenSeconds=" + spokenSeconds.ToString("F2"));
                yield break;
            }

            Debug.Log("[Door] Speech too short. spokenSeconds=" + spokenSeconds.ToString("F2") + ", required=" + RequiredSpeechSeconds.ToString("F2") + ". Restart listening.");
            yield return new WaitForSeconds(0.2f);
        }
    }

    private IEnumerator ListenForSpeechAttempt(System.Action<float> onComplete)
    {
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

                if (!hasDetectedSpeech && elapsed < SpeechStartGraceSeconds)
                {
                    yield return null;
                    continue;
                }

                if (!hasDetectedSpeech && level >= SpeechStartThreshold)
                {
                    if (speechCandidateStartTime < 0f)
                    {
                        speechCandidateStartTime = now;
                        Debug.Log("[Door] Speech candidate started. level=" + level.ToString("F4") + ", threshold=" + SpeechStartThreshold.ToString("F4"));
                    }

                    speechCandidateLastLoudTime = now;
                    if (now - speechCandidateStartTime >= SpeechConfirmSeconds)
                    {
                        hasDetectedSpeech = true;
                        speechStartTime = speechCandidateStartTime;
                        lastSpeechTime = now;
                        Debug.Log("[Door] Speech confirmed. level=" + level.ToString("F4"));
                    }
                }
                else if (!hasDetectedSpeech)
                {
                    if (speechCandidateStartTime >= 0f && now - speechCandidateLastLoudTime > SpeechCandidateDropoutSeconds)
                    {
                        speechCandidateStartTime = -1f;
                        speechCandidateLastLoudTime = -1f;
                    }
                }
                else
                {
                    if (level >= SilenceThreshold)
                    {
                        lastSpeechTime = now;
                    }

                    spokenSeconds = now - speechStartTime;
                    if (spokenSeconds >= RequiredSpeechSeconds || now - lastSpeechTime >= SpeechEndSilenceSeconds)
                    {
                        break;
                    }
                }
            }

            yield return null;
        }

        StopSpeechGateMicrophone();
        onComplete?.Invoke(spokenSeconds);
    }

    private IEnumerator LoadSceneAsync(int sceneIndex)
    {
        AsyncOperation operation = SceneManager.LoadSceneAsync(sceneIndex);
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
