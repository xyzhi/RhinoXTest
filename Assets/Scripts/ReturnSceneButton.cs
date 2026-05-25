using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class ReturnSceneButton : MonoBehaviour
{
    private const float MissionSeconds = 6f * 60f;

    [SerializeField] private int sceneIndex = 0;
    [SerializeField] private Transform PanelResult;
    [SerializeField] private Button ReturnButton;

    [Header("Result Texts")]
    [SerializeField] private Text resultTitleText;
    [SerializeField] private Text scoreText;
    [SerializeField] private Text descText;
    [SerializeField] private Text detailsText;
    [SerializeField] private GameObject test;
    public Image Progress;
    public Text ProgressTxt;

    private float missionStartTime;
    private bool missionTimerRunning;
    private bool missionPanelShown;
    private Coroutine waitNpcCoroutine;

    private void Start()
    {
        EnsureResultTexts();

        if (ReturnButton != null)
        {
            ReturnButton.onClick.RemoveListener(ReturnToScene);
            ReturnButton.onClick.AddListener(ReturnToScene);
        }

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(ShowPanel);
            button.onClick.AddListener(ShowPanel);
        }

        if (PanelResult != null)
        {
            PanelResult.gameObject.SetActive(false);
        }

        if (Application.isPlaying)
        {
            missionStartTime = Time.time;
            missionTimerRunning = true;
            missionPanelShown = false;
            UpdateMissionProgress(0f);
        }
    }

    private void Update()
    {
        if (!Application.isPlaying || !missionTimerRunning || missionPanelShown)
        {
            return;
        }

        float elapsed = Mathf.Max(0f, Time.time - missionStartTime);
        float normalized = Mathf.Clamp01(elapsed / MissionSeconds);
        UpdateMissionProgress(normalized);

        if (elapsed >= MissionSeconds)
        {
            missionTimerRunning = false;
            HandleMissionTimerFinished();
        }
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            EnsureResultTexts();
        }
    }

    private void OnDestroy()
    {
        if (ReturnButton != null)
        {
            ReturnButton.onClick.RemoveListener(ReturnToScene);
        }

        Button button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.RemoveListener(ShowPanel);
        }

        if (waitNpcCoroutine != null)
        {
            StopCoroutine(waitNpcCoroutine);
            waitNpcCoroutine = null;
        }
    }

    private void ShowPanel()
    {
        if (missionPanelShown)
        {
            return;
        }

        missionPanelShown = true;
        missionTimerRunning = false;
        UpdateMissionProgress(1f);

        if (PanelResult != null)
        {
            PanelResult.gameObject.SetActive(true);
        }

        RequestAnalyzeDesc();
    }

    private void HandleMissionTimerFinished()
    {
        VoiceChatManager voiceChatManager = VoiceChatManager.Instance;
        if (voiceChatManager == null || voiceChatManager.IsUserSpeaking)
        {
            ShowPanel();
            return;
        }

        if (waitNpcCoroutine == null)
        {
            waitNpcCoroutine = StartCoroutine(ShowPanelAfterNpcFinished());
        }
    }

    private IEnumerator ShowPanelAfterNpcFinished()
    {
        VoiceChatManager voiceChatManager = VoiceChatManager.Instance;
        while (voiceChatManager != null && voiceChatManager.IsNpcSpeaking)
        {
            yield return null;
            voiceChatManager = VoiceChatManager.Instance;
        }

        waitNpcCoroutine = null;
        ShowPanel();
    }

    private void UpdateMissionProgress(float normalized)
    {
        normalized = Mathf.Clamp01(normalized);

        if (Progress != null)
        {
            Progress.fillAmount = 1f - normalized;
        }

        if (ProgressTxt != null)
        {
            float remaining = Mathf.Max(0f, MissionSeconds * (1f - normalized));
            int remainingSeconds = Mathf.CeilToInt(remaining);
            int minutes = remainingSeconds / 60;
            int seconds = remainingSeconds % 60;
            ProgressTxt.text = minutes.ToString("00") + ":" + seconds.ToString("00");
        }
    }

    public void ReturnToScene()
    {
        SceneManager.LoadSceneAsync(sceneIndex);
    }

    private void RequestAnalyzeDesc()
    {
        EnsureResultTexts();
        ShowLoading();

        if (VoiceChatManager.Instance == null)
        {
            SetAnalyzeError("未找到语音对话管理器");
            return;
        }

        StartCoroutine(VoiceChatManager.Instance.AnalyzeCurrentSession(
            response =>
            {
                SetAnalyzeResult(response);
            },
            error =>
            {
                SetAnalyzeError(BuildChineseAnalyzeError(error));
            }));
    }

    private void SetAnalyzeResult(VoiceChatManager.ChatAnalyzeResponse response)
    {
        EnsureResultTexts();

        if (response == null)
        {
            SetAnalyzeError("分析失败：服务器返回的数据为空。");
            return;
        }

        SetText(resultTitleText, response.result == 0 ? "本次劝阻失败" : "本次劝阻成功");
        SetText(scoreText, "总分：" + response.score);
        SetText(descText, string.IsNullOrWhiteSpace(response.desc) ? "分析接口已返回，但 desc 字段为空，请检查服务端 analyze 返回内容。" : response.desc);
        SetText(detailsText, BuildDetailsText(response.details));
        SetScoreColor(response.score);
        RefreshPanelResultLayout();

        StopVoiceForReconnect();
    }

    private void SetAnalyzeError(string value)
    {
        EnsureResultTexts();
        SetText(resultTitleText, "分析失败");
        SetText(scoreText, "");
        SetText(descText, value);
        SetText(detailsText, "");
        SetScoreColor(0);
        RefreshPanelResultLayout();

        StopVoiceForReconnect();
    }

    private void ShowLoading()
    {
        SetText(resultTitleText, "正在生成分析...");
        SetText(scoreText, "");
        SetText(descText, "请稍候");
        SetText(detailsText, "");
        SetScoreColor(0);
        RefreshPanelResultLayout();

    }

    private string BuildDetailsText(VoiceChatManager.ChatAnalyzeDetail[] details)
    {
        if (details == null || details.Length == 0)
        {
            return "明细\n--------------------\n暂无明细";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("明细");
        builder.AppendLine("--------------------");

        for (int i = 0; i < details.Length; i++)
        {
            VoiceChatManager.ChatAnalyzeDetail detail = details[i];
            if (detail == null)
            {
                continue;
            }

            builder.Append(detail.score);
            builder.Append("    ");
            builder.AppendLine(string.IsNullOrWhiteSpace(detail.text) ? "未命名项" : detail.text);

            if (i < details.Length - 1)
            {
                builder.AppendLine("--------------------");
            }
        }

        return builder.ToString();
    }

    private void EnsureResultTexts()
    {
        if (PanelResult == null)
        {
            return;
        }

        resultTitleText = resultTitleText != null ? resultTitleText : FindOrCreateText("AnalyzeTitleText", new Vector2(0f, 145f), new Vector2(760f, 48f), 36, TextAnchor.MiddleCenter);
        scoreText = scoreText != null ? scoreText : FindOrCreateText("AnalyzeScoreText", new Vector2(0f, 92f), new Vector2(760f, 40f), 30, TextAnchor.MiddleCenter);
        descText = descText != null ? descText : FindOrCreateText("AnalyzeDescText", new Vector2(0f, 20f), new Vector2(760f, 86f), 24, TextAnchor.MiddleCenter);
        detailsText = detailsText != null ? detailsText : FindOrCreateText("AnalyzeDetailsText", new Vector2(0f, -135f), new Vector2(760f, 200f), 23, TextAnchor.UpperLeft);
    }

    private Text FindOrCreateText(string objectName, Vector2 anchoredPosition, Vector2 size, int fontSize, TextAnchor alignment)
    {
        Transform existing = PanelResult.Find(objectName);
        Text text = existing != null ? existing.GetComponent<Text>() : null;
        if (text == null)
        {
            GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            textObject.transform.SetParent(PanelResult, false);
            text = textObject.GetComponent<Text>();
        }

        RectTransform rectTransform = text.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.anchoredPosition = anchoredPosition;
        rectTransform.sizeDelta = size;

        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Truncate;
        return text;
    }

    private void SetText(Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
            target.gameObject.SetActive(true);
        }
    }

    private void RefreshPanelResultLayout()
    {
        if (PanelResult == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();

        LayoutGroup[] layoutGroups = PanelResult.GetComponentsInChildren<LayoutGroup>(true);
        for (int i = layoutGroups.Length - 1; i >= 0; i--)
        {
            RectTransform rectTransform = layoutGroups[i].GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }

        ContentSizeFitter[] sizeFitters = PanelResult.GetComponentsInChildren<ContentSizeFitter>(true);
        for (int i = sizeFitters.Length - 1; i >= 0; i--)
        {
            RectTransform rectTransform = sizeFitters[i].GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            }
        }

        RectTransform panelRectTransform = PanelResult.GetComponent<RectTransform>();
        if (panelRectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelRectTransform);
        }

        Canvas.ForceUpdateCanvases();
    }

    private void SetScoreColor(int score)
    {
        if (scoreText == null)
        {
            return;
        }

        if (score < 0)
        {
            scoreText.color = new Color(1f, 0.34f, 0.28f);
        }
        else if (score > 0)
        {
            scoreText.color = new Color(0.35f, 0.9f, 0.55f);
        }
        else
        {
            scoreText.color = Color.white;
        }
    }

    private void StopVoiceForReconnect()
    {
        if (VoiceChatManager.Instance != null)
        {
            VoiceChatManager.Instance.StopVoiceForReconnect();
        }
    }

    private string BuildChineseAnalyzeError(string error)
    {
        if (string.IsNullOrWhiteSpace(error))
        {
            return "分析失败：未知错误";
        }

        string lowerError = error.ToLowerInvariant();

        if (lowerError.Contains("no chat session id"))
        {
            return "分析失败：还没有有效对话记录，请先完成一次对话。";
        }

        if (lowerError.Contains("cannot connect") || lowerError.Contains("failed to connect") || lowerError.Contains("could not resolve host"))
        {
            return "分析失败：无法连接到分析服务，请检查服务是否已启动、网络是否正常。";
        }

        if (lowerError.Contains("timeout") || lowerError.Contains("timed out") || lowerError.Contains("request timeout"))
        {
            return "分析失败：请求超时，请稍后重试。";
        }

        if (lowerError.Contains("code=404") || lowerError.Contains("404"))
        {
            return "分析失败：服务器没有找到分析接口，请检查服务端接口地址。";
        }

        if (lowerError.Contains("code=500") || lowerError.Contains("500"))
        {
            return "分析失败：服务器处理分析时出错，请查看服务端日志。";
        }

        if (lowerError.Contains("code=502") || lowerError.Contains("502"))
        {
            return "分析失败：服务网关错误，请检查后端服务是否正常。";
        }

        if (lowerError.Contains("json parse"))
        {
            return "分析失败：服务器返回的数据格式不正确。";
        }

        return "分析失败：" + error;
    }
}
