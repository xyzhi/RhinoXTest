using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[ExecuteAlways]
public class ReturnSceneButton : MonoBehaviour
{
    private const float MissionSeconds = 6f * 60f;
#if UNITY_EDITOR
    private const bool UseFakeAnalyzeData = false;
#endif
    private const float ResultPanelMinHeight = 700f;
    private const float ResultPanelVerticalPadding = 140f;

    [SerializeField] private int sceneIndex = 0;
    [SerializeField] private Transform PanelResult;
    [SerializeField] private Button ReturnButton;

    [Header("Result Texts")]
    [SerializeField] private Text scoreText;
    [SerializeField] private Text descText;
    [SerializeField] private Text detailsText;
    [SerializeField] private GameObject test;
    public Image Progress;
    public Text ProgressTxt;

    [Header("Result Pages")]
    [SerializeField] private Button resultUpButton;
    [SerializeField] private Button resultDownButton;

    private float missionStartTime;
    private bool missionTimerRunning;
    private bool missionPanelShown;
    private Coroutine waitNpcCoroutine;
    private int resultPageIndex;
    private readonly List<string> resultDetailPages = new List<string>();

    private void Start()
    {
        EnsureResultTexts();
        EnsureResultPageSetup();

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

        if (resultUpButton != null)
        {
            resultUpButton.onClick.RemoveListener(ShowPreviousResultPage);
        }

        if (resultDownButton != null)
        {
            resultDownButton.onClick.RemoveListener(ShowNextResultPage);
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

        EnsureResultPageSetup();
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
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
        //SceneManager.LoadSceneAsync(sceneIndex);
    }

    private void RequestAnalyzeDesc()
    {
        EnsureResultTexts();
        ShowLoading();

#if UNITY_EDITOR
        if (UseFakeAnalyzeData)
        {
            SetAnalyzeResult(CreateFakeAnalyzeResponse());
            return;
        }
#endif

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

#if UNITY_EDITOR
    private VoiceChatManager.ChatAnalyzeResponse CreateFakeAnalyzeResponse()
    {
        return new VoiceChatManager.ChatAnalyzeResponse
        {
            result = 1,
            score = 10,
            desc = "学员能及时追问受害人的具体情况，并要求查看手机，体现出一定的防备心理；但未继续核实身份、来意和涉诈信息，导致未能及时阻断诈骗风险。",
            details = new[]
            {
                new VoiceChatManager.ChatAnalyzeDetail { score = 10, text = "追问具体情况(要求查看手机)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "表明身份" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "核实身份" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "表明来意" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "核实涉诈信息(询问是否安装APP)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "核实涉诈信息(表达发现您可能下载诈骗软件)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "追问具体情况(询问是怎么下载APP)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "追问具体情况(询问投资金额)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "针对性劝阻(表示是投资理财类诈骗软件)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "针对性劝阻(解释投资理财类诈骗的套路)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "针对性劝阻(告知银行取现线下交付的套路)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "针对性劝阻(解读电信网络诈骗的追赃难度)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "人文关怀(询问家庭健康情况)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "人文关怀(询问子女情况)" },
                new VoiceChatManager.ChatAnalyzeDetail { score = 0, text = "人文关怀(建议联系子女协助)" }
            }
        };
    }
#endif

    private void SetAnalyzeResult(VoiceChatManager.ChatAnalyzeResponse response)
    {
        EnsureResultTexts();

        if (response == null)
        {
            SetAnalyzeError("分析失败：服务器返回的数据为空。");
            return;
        }

        SetText(scoreText, "本次劝阻得分：" + response.score);
        SetText(descText, BuildInstructorDesc(string.IsNullOrWhiteSpace(response.desc) ? "分析接口已返回，但 desc 字段为空，请检查服务端 analyze 返回内容。" : response.desc));
        BuildDetailsText(response.details);
        SetScoreColor(response.score);
        ShowResultPage(0);

        StopVoiceForReconnect();
    }

    private void SetAnalyzeError(string value)
    {
        EnsureResultTexts();
        SetText(scoreText, "");
        SetText(descText, BuildInstructorDesc(value));
        SetText(detailsText, "");
        SetScoreColor(0);
        resultDetailPages.Clear();
        ShowResultPage(0);

        StopVoiceForReconnect();
    }

    private void ShowLoading()
    {
        SetText(scoreText, "");
        SetText(descText, BuildInstructorDesc("正在生成分析，请稍候"));
        SetText(detailsText, "");
        SetScoreColor(0);
        resultDetailPages.Clear();
        ShowResultPage(0);

    }

    private string BuildDetailsTextLegacy(VoiceChatManager.ChatAnalyzeDetail[] details)
    {
        if (details == null || details.Length == 0)
        {
            return "明细\n----------------------------------------------------------------------------------------------n暂无明细";
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("明细");
        builder.AppendLine("----------------------------------------------------------------------------------------------");

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

    private void BuildDetailsText(VoiceChatManager.ChatAnalyzeDetail[] details)
    {
        resultDetailPages.Clear();

        if (details == null || details.Length == 0)
        {
            return;
        }

        StringBuilder scoreBuilder = new StringBuilder();
        StringBuilder lostBuilder = new StringBuilder();

        for (int i = 0; i < details.Length; i++)
        {
            VoiceChatManager.ChatAnalyzeDetail detail = details[i];
            if (detail == null)
            {
                continue;
            }

            string detailText = string.IsNullOrWhiteSpace(detail.text) ? "未命名项" : detail.text;
            if (detail.score <= 0)
            {
                AppendDetailLine(lostBuilder, detailText);
            }
            else
            {
                AppendDetailLine(scoreBuilder, detail.score + "    " + detailText);
            }
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine("得分明细");
        builder.AppendLine("----------------------------------------------------------------------------------------------");
        builder.Append(scoreBuilder.ToString());

        if (scoreBuilder.Length > 0)
        {
            resultDetailPages.Add(builder.ToString());
        }

        builder.Clear();
        builder.AppendLine("失分明细");
        builder.AppendLine("----------------------------------------------------------------------------------------------");
        builder.Append(lostBuilder.ToString());

        if (lostBuilder.Length > 0)
        {
            resultDetailPages.Add(builder.ToString());
        }
    }

    private void AppendDetailLine(StringBuilder builder, string value)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine("--------------------");
        }

        builder.AppendLine(value);
    }

    private void EnsureResultTexts()
    {
        if (PanelResult == null)
        {
            return;
        }

        scoreText = scoreText != null ? scoreText : FindResultText("AnalyzeScoreText");
        descText = descText != null ? descText : FindResultText("AnalyzeDescText");
        detailsText = detailsText != null ? detailsText : FindResultText("AnalyzeDetailsText");
    }

    private Text FindResultText(string objectName)
    {
        Transform textParent = GetResultTextParent();
        Transform existing = FindChildRecursive(textParent, objectName);
        Text text = existing != null ? existing.GetComponent<Text>() : null;
        if (text == null)
        {
            Debug.LogWarning("[ReturnSceneButton] Missing result text: " + objectName);
        }

        return text;
    }

    private Transform GetResultTextParent()
    {
        Transform layout = FindChildRecursive(PanelResult, "Layout");
        return layout != null ? layout : PanelResult;
    }

    private string BuildInstructorDesc(string value)
    {
        return "数字教官：" + (value ?? "");
    }

    private void SetText(Text target, string value)
    {
        if (target == null)
            return;

        if (value == null)
        {
            value = "";
        }

        // 不间断空格（Non-breaking space）
        const char nbsp = '\u00A0';

        // 把普通空格替换为不间断空格
        string processed = value.Replace(" ", nbsp.ToString());

        target.text = processed;
        target.gameObject.SetActive(true);
    }

    private void RefreshPanelResultLayout()
    {
        if (PanelResult == null)
        {
            return;
        }

        EnsureResultPageSetup();
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
        RefreshResultPanelHeight();
        Canvas.ForceUpdateCanvases();
        RefreshResultPageButtons();
    }

    private void RefreshResultPanelHeight()
    {
        RectTransform panelRectTransform = PanelResult as RectTransform;
        RectTransform layoutRectTransform = GetResultTextParent() as RectTransform;
        if (panelRectTransform == null || layoutRectTransform == null)
        {
            return;
        }

        float preferredHeight = LayoutUtility.GetPreferredHeight(layoutRectTransform);
        if (preferredHeight <= 0f)
        {
            preferredHeight = layoutRectTransform.rect.height;
        }

        float targetHeight = Mathf.Max(ResultPanelMinHeight, preferredHeight + ResultPanelVerticalPadding);
        panelRectTransform.sizeDelta = new Vector2(panelRectTransform.sizeDelta.x, targetHeight);
    }

    private void EnsureResultPageSetup()
    {
        if (!Application.isPlaying || PanelResult == null)
        {
            return;
        }

        if (resultUpButton == null)
        {
            Transform upButtonTransform = FindChildRecursive(PanelResult, "UpBtn");
            resultUpButton = upButtonTransform != null ? upButtonTransform.GetComponent<Button>() : null;
        }

        if (resultDownButton == null)
        {
            Transform downButtonTransform = FindChildRecursive(PanelResult, "DownBtn");
            resultDownButton = downButtonTransform != null ? downButtonTransform.GetComponent<Button>() : null;
        }

        if (resultUpButton != null)
        {
            resultUpButton.onClick.RemoveListener(ShowPreviousResultPage);
            resultUpButton.onClick.AddListener(ShowPreviousResultPage);
        }

        if (resultDownButton != null)
        {
            resultDownButton.onClick.RemoveListener(ShowNextResultPage);
            resultDownButton.onClick.AddListener(ShowNextResultPage);
        }

        RefreshResultPageButtons();
    }

    private void ShowResultPage(int pageIndex)
    {
        int pageCount = GetResultPageCount();
        resultPageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);

        bool showSummaryPage = resultPageIndex == 0;

        if (descText != null)
        {
            descText.gameObject.SetActive(showSummaryPage);
        }

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(showSummaryPage);
        }

        if (detailsText != null)
        {
            bool showDetailPage = !showSummaryPage;
            detailsText.gameObject.SetActive(showDetailPage);
            if (showDetailPage)
            {
                SetText(detailsText, resultDetailPages[resultPageIndex - 1]);
            }
        }

        RefreshPanelResultLayout();
        RefreshResultPageButtons();
    }

    private void ShowPreviousResultPage()
    {
        ShowResultPage(resultPageIndex - 1);
    }

    private void ShowNextResultPage()
    {
        ShowResultPage(resultPageIndex + 1);
    }

    private void RefreshResultPageButtons()
    {
        int pageCount = GetResultPageCount();
        if (resultUpButton != null)
        {
            resultUpButton.gameObject.SetActive(pageCount > 1 && resultPageIndex > 0);
        }

        if (resultDownButton != null)
        {
            resultDownButton.gameObject.SetActive(pageCount > 1 && resultPageIndex < pageCount - 1);
        }
    }

    private int GetResultPageCount()
    {
        return 1 + resultDetailPages.Count;
    }

    private Transform FindChildRecursive(Transform parent, string objectName)
    {
        if (parent == null)
        {
            return null;
        }

        Transform direct = parent.Find(objectName);
        if (direct != null)
        {
            return direct;
        }

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform found = FindChildRecursive(parent.GetChild(i), objectName);
            if (found != null)
            {
                return found;
            }
        }

        return null;
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
