using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.XR;

public class VoiceChatManager : MonoBehaviour
{
    public static VoiceChatManager Instance { get; private set; }

    [Header("Server")]
    static public string apiUrl = "http://172.19.0.1:8001/v1/audio/transcriptions";
    [SerializeField] private string model = "iic/speech_paraformer-large-vad-punc_asr_nat-zh-cn-16k-common-vocab8404-pytorch";
    [SerializeField] private int timeoutSeconds = 20;

    [Header("Chat")]
    [SerializeField] private bool chatMode = true;
     private bool previewAsrBeforeServerChat = false;
    [SerializeField] private bool keepSession = true;
    [SerializeField] private string sessionId = "";

    [Header("Recording")]
    [SerializeField] private int maxRecordSeconds = 12;
    [SerializeField] private int sampleRate = 16000;
    [SerializeField] private bool autoSendWhenStop = true;
    [SerializeField] private bool autoStopOnSilence = true;
    [SerializeField] private float minRecordSeconds = 0.8f;
    private float silenceSeconds = 1.5f;
    [SerializeField] private float silenceThreshold = 0.015f;
    [SerializeField] private float speechStartThreshold = 0.03f;
    [SerializeField] private float speechStartGraceSeconds = 0.5f;
    [SerializeField] private float speechConfirmSeconds = 0.35f;
    [SerializeField] private float speechCandidateDropoutSeconds = 0.35f;

    [Header("Continuous Listening")]
    [SerializeField] private bool restartWhenNoSpeech = true;
    [SerializeField] private bool restartAfterNpcVoice = true;
    [SerializeField] private float restartListenDelaySeconds = 0.2f;

    [Header("Playback")]
    [SerializeField] private bool playTtsAudio = true;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private Animator npcAnimator;
    [SerializeField] private string talkingParameterName = "talking";
    [SerializeField] private NpcSpeechBubble npcSpeechBubble;
    [SerializeField] private PlayerSpeechCaption playerSpeechCaption;

    [Header("Debug Panel")]
    [SerializeField] private bool showPanel = false;
    [SerializeField] private Rect panelRect = new Rect(20, 20, 540, 430);

    private AudioClip recordingClip;
    private bool isRecording;
    private bool isSending;
    private string status = "Ready";
    private string transcript = "";
    private string assistantText = "";
    private string rawResponse = "";
    private Vector2 scroll;
    private bool previousPrimaryButton;
    private float recordStartTime;
    private float lastLoudTime;
    private float speechCandidateStartTime = -1f;
    private float speechCandidateLastLoudTime = -1f;
    private bool hasDetectedSpeech;
    private bool continuousListeningActive;
    private bool restartAfterCurrentResponse;
    private float[] levelSampleBuffer;

    [Serializable]
    private class VoiceChatResponse
    {
        public string text;
        public string session_id;
        public string assistant_text;
        public string chat_model;
        public string tts_audio_base64;
        public float tts_audio_duration_seconds;
        public string tts_audio_content_type;
    }

    [Serializable]
    public class ChatAnalyzeResponse
    {
        public ChatAnalyzeDetail[] details;
        public string desc;
        public int result;
        public int score;
    }

    [Serializable]
    public class ChatAnalyzeDetail
    {
        public string text;
        public int score;
    }

    private void Awake()
    {
        Debug.Log("[VoiceChatDemoTester] Awake.");

        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[VoiceChatDemoTester] Duplicate VoiceChatManager found. Destroying duplicate on " + name);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            Debug.Log("[VoiceChatDemoTester] AudioSource not assigned, created one on this GameObject.");
        }

        audioSource.playOnAwake = false;
    }

    private void OnDestroy()
    {
        SetNpcTalking(false);
        HideNpcSpeech();
        HidePlayerSpeech();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnDisable()
    {
        SetNpcTalking(false);
        HideNpcSpeech();
        HidePlayerSpeech();
    }

    private IEnumerator Start()
    {
        Debug.Log("[VoiceChatDemoTester] Start. apiUrl=" + apiUrl + ", chatMode=" + chatMode + ", previewAsrBeforeServerChat=" + previewAsrBeforeServerChat + ", sampleRate=" + sampleRate);
        if (Application.platform == RuntimePlatform.Android && IsLoopbackUrl(apiUrl))
        {
            Debug.LogWarning("[VoiceChatDemoTester] apiUrl is loopback on Android/headset. 127.0.0.1 points to the headset, not the PC server. Use the PC LAN IP instead.");
        }

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            Debug.Log("[VoiceChatDemoTester] Requesting microphone permission.");
            yield return Application.RequestUserAuthorization(UserAuthorization.Microphone);
        }

        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            status = "Microphone permission denied.";
            Debug.LogError("[VoiceChatDemoTester] Microphone permission denied.");
        }
        else
        {
            Debug.Log("[VoiceChatDemoTester] Microphone permission ready. devices=" + string.Join(", ", Microphone.devices));
        }
    }

    private void Update()
    {
        //if (Input.GetKeyDown(KeyCode.Space))
        //{
        //    ToggleRecording();
        //}

        //if (Input.GetKeyDown(KeyCode.Return) && !isRecording && !isSending && recordingClip != null)
        //{
        //    StartCoroutine(SendRecording());
        //}

        //if (Input.GetKeyDown(KeyCode.Backspace))
        //{
        //    sessionId = "";
        //    status = "Session cleared.";
        //    Debug.Log("[VoiceChatDemoTester] Session cleared.");
        //}

        HandleRightPrimaryButton();

        if (isRecording && autoStopOnSilence)
        {
            UpdateSilenceAutoStop();
        }
    }

    private void OnGUI()
    {
        if (!showPanel)
        {
            return;
        }

        panelRect = GUI.Window(GetInstanceID(), panelRect, DrawPanel, "Voice Chat Demo Tester");
    }

    private void DrawPanel(int id)
    {
        GUILayout.Label("Space / Right Primary: record or stop");
        GUILayout.Label("Enter: resend last recording    Backspace: clear session");
        GUILayout.Space(6);
        GUILayout.Label("Status: " + status);
        GUILayout.Label("Session: " + (string.IsNullOrEmpty(sessionId) ? "(none)" : sessionId));

        GUI.enabled = !isSending;
        if (GUILayout.Button(isRecording ? "Stop And Send" : "Start Recording", GUILayout.Height(36)))
        {
            ToggleRecording();
        }

        GUI.enabled = !isSending && !isRecording && recordingClip != null;
        if (GUILayout.Button("Send Last Recording", GUILayout.Height(30)))
        {
            StartCoroutine(SendRecording());
        }

        GUI.enabled = true;
        if (GUILayout.Button("Clear Session", GUILayout.Height(26)))
        {
            sessionId = "";
            status = "Session cleared.";
            Debug.Log("[VoiceChatDemoTester] Session cleared.");
        }

        GUILayout.Space(8);
        scroll = GUILayout.BeginScrollView(scroll);
        GUILayout.Label("ASR:");
        GUILayout.TextArea(transcript, GUILayout.MinHeight(55));
        GUILayout.Label("Assistant:");
        GUILayout.TextArea(assistantText, GUILayout.MinHeight(80));
        GUILayout.Label("Raw:");
        GUILayout.TextArea(rawResponse, GUILayout.MinHeight(90));
        GUILayout.EndScrollView();

        GUI.DragWindow();
    }

    public void StartRecordingFromExternal()
    {
        continuousListeningActive = true;
        if (isRecording)
        {
            Debug.LogWarning("[VoiceChatDemoTester] External start ignored: already recording.");
            return;
        }

        StartRecording();
    }

    public void StopRecordingFromExternal()
    {
        continuousListeningActive = false;
        restartAfterCurrentResponse = false;
        if (!isRecording)
        {
            Debug.LogWarning("[VoiceChatDemoTester] External stop ignored: not recording.");
            return;
        }

        StopRecording();
        if (autoSendWhenStop && recordingClip != null)
        {
            StartCoroutine(SendRecording());
        }
    }

    public void ToggleRecordingFromExternal()
    {
        continuousListeningActive = !isRecording;
        ToggleRecording();
    }

    //public void SendLastRecordingFromExternal()
    //{
    //    if (!isRecording && !isSending && recordingClip != null)
    //    {
    //        StartCoroutine(SendRecording());
    //        return;
    //    }

    //    Debug.LogWarning("[VoiceChatDemoTester] External send ignored. isRecording=" + isRecording + ", isSending=" + isSending + ", hasClip=" + (recordingClip != null));
    //}

    public void ClearSessionFromExternal()
    {
        sessionId = "";
        status = "Session cleared.";
        Debug.Log("[VoiceChatDemoTester] Session cleared by external call.");
    }

    public void StopVoiceForReconnect()
    {
        Debug.Log("[VoiceChatDemoTester] Stop voice for reconnect.");

        continuousListeningActive = false;
        restartAfterCurrentResponse = false;

        StopAllCoroutines();

        if (isRecording)
        {
            Microphone.End(null);
            isRecording = false;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }

        recordingClip = null;
        isSending = false;
        hasDetectedSpeech = false;
        speechCandidateStartTime = -1f;
        speechCandidateLastLoudTime = -1f;
        sessionId = "";
        status = "Stopped for reconnect.";

        SetNpcTalking(false);
        HideNpcSpeech();
        HidePlayerSpeech();
    }

    public string GetCurrentSessionId()
    {
        return sessionId;
    }

    public string BuildAnalyzeUrl()
    {
        return BuildAnalyzeUrl(sessionId);
    }

    public static string BuildAnalyzeUrl(string targetSessionId)
    {
        string baseUrl = apiUrl;
        int transcriptionPathIndex = baseUrl.IndexOf("/v1/audio/transcriptions", StringComparison.OrdinalIgnoreCase);
        if (transcriptionPathIndex >= 0)
        {
            baseUrl = baseUrl.Substring(0, transcriptionPathIndex);
        }
        else
        {
            baseUrl = baseUrl.TrimEnd('/');
        }

        return baseUrl.TrimEnd('/') + "/v1/chat/sessions/" + UnityWebRequest.EscapeURL(targetSessionId ?? "") + "/analyze";
    }

    public IEnumerator AnalyzeCurrentSession(Action<ChatAnalyzeResponse> onComplete, Action<string> onError)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            string message = "No chat session id. Finish at least one server chat turn before analyze.";
            Debug.LogWarning("[VoiceChatDemoTester] " + message);
            onError?.Invoke(message);
            yield break;
        }

        string analyzeUrl = BuildAnalyzeUrl(sessionId);
        Debug.Log("[VoiceChatDemoTester] Analyze request started. url=" + analyzeUrl);

        using (UnityWebRequest request = new UnityWebRequest(analyzeUrl, "POST"))
        {
            request.downloadHandler = new DownloadHandlerBuffer();
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes("{}"));
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = timeoutSeconds;

            float startTime = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();

            string body = request.downloadHandler != null ? request.downloadHandler.text : "";
            if (request.result != UnityWebRequest.Result.Success)
            {
                string message = "Analyze failed. elapsed=" + FormatSeconds(Time.realtimeSinceStartup - startTime) + ", result=" + request.result + ", error=" + request.error + ", code=" + request.responseCode + ", body=" + body;
                Debug.LogError("[VoiceChatDemoTester] " + message);
                onError?.Invoke(message);
                yield break;
            }

            Debug.Log("[VoiceChatDemoTester] Analyze request succeeded. elapsed=" + FormatSeconds(Time.realtimeSinceStartup - startTime) + ", body=" + body);

            ChatAnalyzeResponse analyzeResponse = null;
            try
            {
                analyzeResponse = JsonUtility.FromJson<ChatAnalyzeResponse>(body);
            }
            catch (Exception ex)
            {
                string message = "Analyze json parse failed: " + ex.Message + "\nBody: " + body;
                Debug.LogError("[VoiceChatDemoTester] " + message);
                onError?.Invoke(message);
                yield break;
            }

            if (analyzeResponse == null)
            {
                string message = "Analyze response is empty.";
                Debug.LogError("[VoiceChatDemoTester] " + message);
                onError?.Invoke(message);
                yield break;
            }

            onComplete?.Invoke(analyzeResponse);
        }
    }

    private void ToggleRecording()
    {
        if (isSending)
        {
            Debug.LogWarning("[VoiceChatDemoTester] Toggle ignored because request is sending.");
            return;
        }

        if (isRecording)
        {
            Debug.Log("[VoiceChatDemoTester] Stop recording requested.");
            StopRecording();
            if (autoSendWhenStop && recordingClip != null)
            {
                Debug.Log("[VoiceChatDemoTester] Auto send enabled. Sending recorded clip.");
                StartCoroutine(SendRecording());
            }
        }
        else
        {
            Debug.Log("[VoiceChatDemoTester] Start recording requested.");
            StartRecording();
        }
    }

    private void StartRecording()
    {
        if (!Application.HasUserAuthorization(UserAuthorization.Microphone))
        {
            status = "No microphone permission.";
            Debug.LogError("[VoiceChatDemoTester] Cannot record: no microphone permission.");
            return;
        }

        if (Microphone.devices.Length == 0)
        {
            status = "No microphone found.";
            Debug.LogError("[VoiceChatDemoTester] Cannot record: Microphone.devices is empty.");
            return;
        }

        Debug.Log("[VoiceChatDemoTester] Recording started. maxRecordSeconds=" + maxRecordSeconds + ", sampleRate=" + sampleRate);
        HidePlayerSpeech();
        if (audioSource != null && audioSource.isPlaying)
        {
            audioSource.Stop();
            SetNpcTalking(false);
            HideNpcSpeech();
            Debug.Log("[VoiceChatDemoTester] Stopped AudioSource before recording to avoid microphone feedback.");
        }

        Debug.Log("[VoiceChatDemoTester] Starting microphone. device=(default)");
        recordingClip = Microphone.Start(null, false, Mathf.Max(1, maxRecordSeconds), sampleRate);
        Debug.Log("[VoiceChatDemoTester] Microphone.Start returned. clip=" + (recordingClip == null ? "null" : recordingClip.name));
        isRecording = true;
        recordStartTime = Time.realtimeSinceStartup;
        lastLoudTime = recordStartTime;
        speechCandidateStartTime = -1f;
        speechCandidateLastLoudTime = -1f;
        hasDetectedSpeech = false;
        status = "Recording...";
    }

    private void StopRecording()
    {
        Debug.Log("[VoiceChatDemoTester] StopRecording begin. device=(default)");
        int position = Microphone.GetPosition(null);
        Debug.Log("[VoiceChatDemoTester] Microphone position before end=" + position);
        Microphone.End(null);
        Debug.Log("[VoiceChatDemoTester] Microphone ended.");
        isRecording = false;

        if (recordingClip == null || position <= 0)
        {
            status = "Recording is empty.";
            Debug.LogWarning("[VoiceChatDemoTester] Recording stopped but no samples were captured. position=" + position);
            return;
        }

        recordingClip = TrimClip(recordingClip, position);
        status = "Recorded " + recordingClip.length.ToString("F1") + "s.";
        Debug.Log("[VoiceChatDemoTester] Recording stopped. samples=" + position + ", seconds=" + recordingClip.length.ToString("F2"));
    }

    private void UpdateSilenceAutoStop()
    {
        if (recordingClip == null)
        {
            return;
        }

        float elapsed = Time.realtimeSinceStartup - recordStartTime;
        int position = Microphone.GetPosition(null);

        if (elapsed >= maxRecordSeconds - 0.05f)
        {
            Debug.Log("[VoiceChatDemoTester] Auto stop: reached maxRecordSeconds. hasDetectedSpeech=" + hasDetectedSpeech);
            StopRecording();
            if (hasDetectedSpeech && autoSendWhenStop && recordingClip != null)
            {
                StartCoroutine(SendRecording());
            }
            else
            {
                status = "No speech detected.";
                recordingClip = null;
                Debug.LogWarning("[VoiceChatDemoTester] Recording discarded because no speech was detected.");
                if (continuousListeningActive && restartWhenNoSpeech)
                {
                    StartCoroutine(RestartListeningAfterDelay("no speech"));
                }
            }
            return;
        }

        if (position <= 0)
        {
            return;
        }

        float level = GetRecentLevel(recordingClip, position, 4096);
        if (!hasDetectedSpeech && elapsed < speechStartGraceSeconds)
        {
            return;
        }

        if (!hasDetectedSpeech && level >= speechStartThreshold)
        {
            if (speechCandidateStartTime < 0f)
            {
                speechCandidateStartTime = Time.realtimeSinceStartup;
                Debug.Log("[VoiceChatDemoTester] Speech candidate started. level=" + level.ToString("F4") + ", threshold=" + speechStartThreshold.ToString("F4"));
            }

            speechCandidateLastLoudTime = Time.realtimeSinceStartup;
            float candidateElapsed = Time.realtimeSinceStartup - speechCandidateStartTime;
            if (candidateElapsed >= speechConfirmSeconds)
            {
                hasDetectedSpeech = true;
                lastLoudTime = Time.realtimeSinceStartup;
                Debug.Log("[VoiceChatDemoTester] Speech confirmed. candidateElapsed=" + FormatSeconds(candidateElapsed) + ", level=" + level.ToString("F4"));
            }
        }
        else if (!hasDetectedSpeech)
        {
            if (speechCandidateStartTime >= 0f && Time.realtimeSinceStartup - speechCandidateLastLoudTime > speechCandidateDropoutSeconds)
            {
                Debug.Log("[VoiceChatDemoTester] Speech candidate reset. lastLevel=" + level.ToString("F4"));
                speechCandidateStartTime = -1f;
                speechCandidateLastLoudTime = -1f;
            }
        }
        else if (hasDetectedSpeech && level >= silenceThreshold)
        {
            lastLoudTime = Time.realtimeSinceStartup;
        }

        if (!hasDetectedSpeech || elapsed < minRecordSeconds)
        {
            return;
        }

        float silentElapsed = Time.realtimeSinceStartup - lastLoudTime;
        if (silentElapsed >= silenceSeconds)
        {
            Debug.Log("[VoiceChatDemoTester] Auto stop: silence detected. level=" + level.ToString("F4") + ", silentElapsed=" + FormatSeconds(silentElapsed));
            StopRecording();
            if (autoSendWhenStop && recordingClip != null)
            {
                Debug.Log("[VoiceChatDemoTester] Auto send enabled after silence. Sending recorded clip.");
                StartCoroutine(SendRecording());
            }
        }
    }

    private IEnumerator SendRecording()
    {
        if (isSending)
        {
            Debug.LogWarning("[VoiceChatDemoTester] Send ignored: request is already sending.");
            yield break;
        }

        if (recordingClip == null)
        {
            status = "No recording to send.";
            Debug.LogWarning("[VoiceChatDemoTester] Send ignored: recordingClip is null.");
            yield break;
        }

        AudioClip clipToSend = recordingClip;
        recordingClip = null;
        isSending = true;
        status = "Sending...";

        float requestStartTime = Time.realtimeSinceStartup;
        Debug.Log("[VoiceChatDemoTester] Request timer started at " + DateTime.Now.ToString("HH:mm:ss.fff"));

        float encodeStartTime = Time.realtimeSinceStartup;
        byte[] wavBytes = EncodeWav(clipToSend);
        Debug.Log("[VoiceChatDemoTester] WAV encoded. elapsed=" + FormatSeconds(Time.realtimeSinceStartup - encodeStartTime) + ", wavBytes=" + wavBytes.Length);
        if (chatMode && previewAsrBeforeServerChat)
        {
            yield return SendServerRequest(wavBytes, false, requestStartTime, "ASR preview");
            status = "Waiting server chat...";
        }

        yield return SendServerRequest(wavBytes, chatMode, requestStartTime, chatMode ? "Server chat" : "ASR");
        Debug.Log("[VoiceChatDemoTester] Request finished. totalElapsed=" + FormatSeconds(Time.realtimeSinceStartup - requestStartTime) + ", finishedAt=" + DateTime.Now.ToString("HH:mm:ss.fff"));
        isSending = false;
        if (restartAfterCurrentResponse)
        {
            restartAfterCurrentResponse = false;
            StartCoroutine(RestartListeningAfterDelay("npc voice finished"));
        }
    }

    private IEnumerator SendServerRequest(byte[] wavBytes, bool serverChatMode, float parentRequestStartTime, string label)
    {
        Debug.Log("[VoiceChatDemoTester] " + label + " request started. url=" + apiUrl + ", timeout=" + timeoutSeconds + "s, serverChatMode=" + serverChatMode + ", sessionId=" + (string.IsNullOrWhiteSpace(sessionId) ? "(none)" : sessionId));
        var form = new WWWForm();
        form.AddBinaryData("file", wavBytes, "unity_voice.wav", "audio/wav");
        form.AddField("model", model);
        form.AddField("response_format", "json");

        if (serverChatMode)
        {
            form.AddField("chat_mode", "true");
        }

        if (keepSession && !string.IsNullOrWhiteSpace(sessionId))
        {
            form.AddField("session_id", sessionId);
        }

        using (UnityWebRequest request = UnityWebRequest.Post(apiUrl, form))
        {
            request.timeout = timeoutSeconds;
            float httpStartTime = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();
            float httpElapsed = Time.realtimeSinceStartup - httpStartTime;

            if (request.result != UnityWebRequest.Result.Success)
            {
                status = label + " failed: " + request.error;
                rawResponse = request.downloadHandler != null ? request.downloadHandler.text : "";
                Debug.LogError("[VoiceChatDemoTester] " + label + " request failed. httpElapsed=" + FormatSeconds(httpElapsed) + ", totalElapsed=" + FormatSeconds(Time.realtimeSinceStartup - parentRequestStartTime) + ", result=" + request.result + ", error=" + request.error + ", code=" + request.responseCode + ", headers=" + FormatHeaders(request.GetResponseHeaders()) + ", body=" + rawResponse);
                yield break;
            }

            rawResponse = request.downloadHandler.text;
            Debug.Log("[VoiceChatDemoTester] " + label + " request succeeded. httpElapsed=" + FormatSeconds(httpElapsed) + ", totalElapsed=" + FormatSeconds(Time.realtimeSinceStartup - parentRequestStartTime) + ", code=" + request.responseCode + ", body=" + rawResponse);
        }

        float parseStartTime = Time.realtimeSinceStartup;
        VoiceChatResponse response = null;
        try
        {
            response = JsonUtility.FromJson<VoiceChatResponse>(rawResponse);
        }
        catch (Exception ex)
        {
            status = label + " json parse failed: " + ex.Message;
            Debug.LogError("[VoiceChatDemoTester] " + label + " json parse failed. " + ex.Message + "\nBody: " + rawResponse);
        }
        Debug.Log("[VoiceChatDemoTester] " + label + " json parse elapsed=" + FormatSeconds(Time.realtimeSinceStartup - parseStartTime));

        if (response == null)
        {
            yield break;
        }

        transcript = response.text ?? "";
        ShowPlayerSpeech(transcript);
        assistantText = string.IsNullOrWhiteSpace(response.assistant_text) ? "" : response.assistant_text;
        Debug.Log("[VoiceChatDemoTester] " + label + " parsed response. text=" + transcript + ", assistant=" + assistantText + ", session_id=" + response.session_id);

        if (keepSession && !string.IsNullOrWhiteSpace(response.session_id))
        {
            sessionId = response.session_id;
            Debug.Log("[VoiceChatDemoTester] Session updated: " + sessionId);
        }

        status = serverChatMode ? "Done (Server Chat)" : "Done (ASR)";

        if (playTtsAudio && !string.IsNullOrWhiteSpace(response.tts_audio_base64))
        {
            Debug.Log("[VoiceChatDemoTester] TTS audio found. contentType=" + response.tts_audio_content_type + ", duration=" + response.tts_audio_duration_seconds);
            yield return PlayTtsAudio(response.tts_audio_base64, response.tts_audio_content_type, assistantText);
        }
        else if (playTtsAudio && serverChatMode)
        {
            Debug.Log("[VoiceChatDemoTester] No TTS audio returned.");
        }
    }

    private IEnumerator PlayTtsAudio(string base64, string contentType, string speechText)
    {
        float ttsStartTime = Time.realtimeSinceStartup;
        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(base64);
        }
        catch (Exception ex)
        {
            status = "TTS base64 decode failed: " + ex.Message;
            Debug.LogError("[VoiceChatDemoTester] TTS base64 decode failed. " + ex.Message);
            yield break;
        }

        string extension = GetAudioExtension(contentType);
        string path = Path.Combine(Application.persistentDataPath, "voice_chat_tts" + extension);
        File.WriteAllBytes(path, bytes);
        Debug.Log("[VoiceChatDemoTester] TTS audio written: " + path + ", bytes=" + bytes.Length + ", elapsed=" + FormatSeconds(Time.realtimeSinceStartup - ttsStartTime));

        AudioType audioType = GetAudioType(contentType);
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip("file://" + path, audioType))
        {
            float loadStartTime = Time.realtimeSinceStartup;
            yield return request.SendWebRequest();
            float loadElapsed = Time.realtimeSinceStartup - loadStartTime;
            if (request.result != UnityWebRequest.Result.Success)
            {
                status = "TTS load failed: " + request.error;
                Debug.LogError("[VoiceChatDemoTester] TTS audio load failed. elapsed=" + FormatSeconds(loadElapsed) + ", error=" + request.error);
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
            audioSource.clip = clip;
            audioSource.Play();
            SetNpcTalking(true);
            ShowNpcSpeech(speechText);
            Debug.Log("[VoiceChatDemoTester] TTS playback started. loadElapsed=" + FormatSeconds(loadElapsed) + ", totalTtsClientElapsed=" + FormatSeconds(Time.realtimeSinceStartup - ttsStartTime) + ", clipLength=" + (clip == null ? 0f : clip.length));

            if (clip != null)
            {
                yield return new WaitForSeconds(clip.length);
            }

            SetNpcTalking(false);
            HideNpcSpeech();

            if (restartAfterNpcVoice && continuousListeningActive && clip != null)
            {
                restartAfterCurrentResponse = true;
                Debug.Log("[VoiceChatDemoTester] TTS playback finished. Listening will restart after response cleanup.");
            }
        }
    }

    private void SetNpcTalking(bool isTalking)
    {
        if (npcAnimator == null || string.IsNullOrWhiteSpace(talkingParameterName))
        {
            return;
        }

        npcAnimator.SetBool(talkingParameterName, isTalking);
    }

    private void ShowNpcSpeech(string text)
    {
        if (npcSpeechBubble != null)
        {
            npcSpeechBubble.Show(text);
        }
    }

    private void HideNpcSpeech()
    {
        if (npcSpeechBubble != null)
        {
            npcSpeechBubble.Hide();
        }
    }

    private void ShowPlayerSpeech(string text)
    {
        if (playerSpeechCaption != null)
        {
            playerSpeechCaption.Show(text);
        }
    }

    private void HidePlayerSpeech()
    {
        if (playerSpeechCaption != null)
        {
            playerSpeechCaption.Hide();
        }
    }

    private IEnumerator RestartListeningAfterDelay(string reason)
    {
        if (!continuousListeningActive)
        {
            yield break;
        }

        if (restartListenDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(restartListenDelaySeconds);
        }

        if (!continuousListeningActive || isRecording || isSending)
        {
            yield break;
        }

        Debug.Log("[VoiceChatDemoTester] Restart listening after " + reason + ".");
        StartRecording();
    }

    private void HandleRightPrimaryButton()
    {
        var devices = new List<InputDevice>();
        InputDevices.GetDevicesAtXRNode(XRNode.RightHand, devices);
        if (devices.Count == 0)
        {
            previousPrimaryButton = false;
            return;
        }

        bool pressed;
        if (devices[0].TryGetFeatureValue(CommonUsages.primaryButton, out pressed))
        {
            if (pressed && !previousPrimaryButton)
            {
                ToggleRecording();
            }

            previousPrimaryButton = pressed;
        }
    }

    private static AudioClip TrimClip(AudioClip source, int samples)
    {
        samples = Mathf.Clamp(samples, 0, source.samples);
        float[] data = new float[samples * source.channels];
        source.GetData(data, 0);

        AudioClip clip = AudioClip.Create(source.name + "_trimmed", samples, source.channels, source.frequency, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static byte[] EncodeWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[samples.Length * 2];
        const float rescaleFactor = 32767f;

        for (int i = 0; i < samples.Length; i++)
        {
            intData[i] = (short)Mathf.Clamp(samples[i] * rescaleFactor, short.MinValue, short.MaxValue);
            byte[] byteArr = BitConverter.GetBytes(intData[i]);
            byteArr.CopyTo(bytesData, i * 2);
        }

        byte[] header = BuildWavHeader(clip, bytesData.Length);
        byte[] wav = new byte[header.Length + bytesData.Length];
        Buffer.BlockCopy(header, 0, wav, 0, header.Length);
        Buffer.BlockCopy(bytesData, 0, wav, header.Length, bytesData.Length);
        return wav;
    }

    private float GetRecentLevel(AudioClip clip, int microphonePosition, int sampleWindow)
    {
        int channels = clip.channels;
        int samples = Mathf.Min(sampleWindow, microphonePosition);
        if (samples <= 0)
        {
            return 0f;
        }

        int offset = Mathf.Max(0, microphonePosition - samples);
        int sampleCount = samples * channels;
        if (levelSampleBuffer == null || levelSampleBuffer.Length < sampleCount)
        {
            levelSampleBuffer = new float[sampleWindow * channels];
        }

        clip.GetData(levelSampleBuffer, offset);

        float sum = 0f;
        for (int i = 0; i < sampleCount; i++)
        {
            sum += Mathf.Abs(levelSampleBuffer[i]);
        }

        return sampleCount == 0 ? 0f : sum / sampleCount;
    }

    private static byte[] BuildWavHeader(AudioClip clip, int dataLength)
    {
        using (var stream = new MemoryStream(44))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataLength);
            writer.Write(Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((ushort)1);
            writer.Write((ushort)clip.channels);
            writer.Write(clip.frequency);
            writer.Write(clip.frequency * clip.channels * 2);
            writer.Write((ushort)(clip.channels * 2));
            writer.Write((ushort)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(dataLength);
            return stream.ToArray();
        }
    }

    private static AudioType GetAudioType(string contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return AudioType.WAV;
        }

        string lower = contentType.ToLowerInvariant();
        if (lower.Contains("mpeg") || lower.Contains("mp3"))
        {
            return AudioType.MPEG;
        }

        if (lower.Contains("ogg"))
        {
            return AudioType.OGGVORBIS;
        }

        return AudioType.WAV;
    }

    private static string GetAudioExtension(string contentType)
    {
        AudioType audioType = GetAudioType(contentType);
        if (audioType == AudioType.MPEG)
        {
            return ".mp3";
        }

        if (audioType == AudioType.OGGVORBIS)
        {
            return ".ogg";
        }

        return ".wav";
    }

    private static bool IsLoopbackUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        return url.Contains("127.0.0.1") || url.Contains("localhost");
    }

    private static string FormatSeconds(float seconds)
    {
        return seconds.ToString("F2") + "s";
    }

    private static string FormatHeaders(Dictionary<string, string> headers)
    {
        if (headers == null || headers.Count == 0)
        {
            return "{}";
        }

        var builder = new StringBuilder();
        builder.Append("{");
        bool first = true;
        foreach (KeyValuePair<string, string> pair in headers)
        {
            if (!first)
            {
                builder.Append(", ");
            }

            builder.Append(pair.Key);
            builder.Append("=");
            builder.Append(pair.Value);
            first = false;
        }

        builder.Append("}");
        return builder.ToString();
    }
}
