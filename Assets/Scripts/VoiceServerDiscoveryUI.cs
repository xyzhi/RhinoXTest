using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class VoiceServerDiscoveryUI : MonoBehaviour
{
    public static VoiceServerDiscoveryUI Instance { get; private set; }

    private const string LastServerIpKey = "VoiceServer.LastIp";

    [Header("Scan")]
    [SerializeField] private int serverPort = 8001;
    [SerializeField] private string apiPath = "/v1/audio/transcriptions";
    [SerializeField] private string probePath = "/health";
    [SerializeField] private float scanDelaySeconds = 0.2f;
    [SerializeField] private int connectTimeoutMilliseconds = 1000;

    [Header("UI")]
    [SerializeField] private Canvas uiCanvas;
    [SerializeField] private Text statusText;
    [SerializeField] private InputField fallbackIpInput;
    [SerializeField] private Button rescanButton;

    private readonly List<string> serverIps = new List<string>();
    private readonly List<string> diagnosticLines = new List<string>();
    private string lastServerIp;

    private void Awake()
    {
        Instance = this;
        lastServerIp = PlayerPrefs.GetString(LastServerIpKey, "");
        EnsureUi();
        if (fallbackIpInput != null && string.IsNullOrWhiteSpace(fallbackIpInput.text))
        {
            fallbackIpInput.text = string.IsNullOrWhiteSpace(lastServerIp) ? "等待自动查找" : lastServerIp;
        }

        RefreshDropdown("正在自动查找语音服务，请稍候...", true);
    }

    private IEnumerator Start()
    {
        if (scanDelaySeconds > 0f)
        {
            yield return new WaitForSeconds(scanDelaySeconds);
        }

        yield return ScanServers();
    }

    private void OnDestroy()
    {
        if (rescanButton != null)
        {
            rescanButton.onClick.RemoveListener(Rescan);
        }

        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static void ApplySelectedServerToVoiceChatManager()
    {
        if (Instance != null)
        {
            Instance.ApplySelection();
            return;
        }

        string savedIp = PlayerPrefs.GetString(LastServerIpKey, "");
        if (!string.IsNullOrWhiteSpace(savedIp))
        {
            VoiceChatManager.apiUrl = BuildApiUrl(savedIp, 8001, "/v1/audio/transcriptions");
            Debug.Log("[VoiceServerDiscoveryUI] Applied saved voice server: " + VoiceChatManager.apiUrl);
        }
    }

    public void Rescan()
    {
        StopAllCoroutines();
        StartCoroutine(ScanServers());
    }

    public void ApplyManualInput()
    {
        ApplySelection();
    }

    public void ApplySelection()
    {
        string selectedIp = GetSelectedIp();
        if (string.IsNullOrWhiteSpace(selectedIp))
        {
            Debug.LogWarning("[VoiceServerDiscoveryUI] No voice server selected. VoiceChatManager.apiUrl remains: " + VoiceChatManager.apiUrl);
            SetStatus("还没有找到语音服务，请确认电脑服务已启动并点击刷新");
            return;
        }

        ApplyIp(selectedIp);
    }

    private void ApplyIp(string selectedIp)
    {
        VoiceChatManager.apiUrl = BuildApiUrl(selectedIp, serverPort, apiPath);
        PlayerPrefs.SetString(LastServerIpKey, selectedIp);
        PlayerPrefs.Save();
        lastServerIp = selectedIp;
        SetStatus("已连接语音服务: " + selectedIp + "\n请求地址: " + VoiceChatManager.apiUrl);
        Debug.Log("[VoiceServerDiscoveryUI] Applied voice server: " + VoiceChatManager.apiUrl);
    }

    private IEnumerator ScanServers()
    {
        EnsureUi();
        RefreshDropdown("正在自动查找语音服务，请稍候...", true);
        diagnosticLines.Clear();

        List<string> candidates = BuildCandidateIps();
        AddDiagnostic("扫描开始: " + candidates.Count + " 个地址, 端口 " + serverPort + ", 探测 " + GetNormalizedApiPath(probePath));
        if (candidates.Count == 0)
        {
            RefreshDropdown("没有找到本机局域网 IP，请检查网络后点击刷新", false);
            yield break;
        }

        Task<List<string>> scanTask = ScanReachableIpsAsync(candidates);
        while (!scanTask.IsCompleted)
        {
            yield return null;
        }

        if (scanTask.IsFaulted)
        {
            Debug.LogError("[VoiceServerDiscoveryUI] Scan failed: " + scanTask.Exception);
            RefreshDropdown("扫描失败，请检查网络后点击刷新", false);
            yield break;
        }

        serverIps.Clear();
        serverIps.AddRange(scanTask.Result);
        if (serverIps.Count > 0 && fallbackIpInput != null)
        {
            fallbackIpInput.text = serverIps[0];
        }

        AddDiagnostic("扫描完成: " + (serverIps.Count == 0 ? "未找到服务" : string.Join(", ", serverIps)));
        RefreshDropdown(serverIps.Count == 0 ? "没有找到开着语音服务的机器，请检查电脑服务和网络后点击刷新" : "找到 " + serverIps.Count + " 台语音服务，已自动选择第一台", false);
    }

    private async Task<List<string>> ScanReachableIpsAsync(List<string> candidates)
    {
        Task<string>[] tasks = candidates.Select(CheckServerAsync).ToArray();
        string[] results = await Task.WhenAll(tasks);
        return results
            .Where(ip => !string.IsNullOrWhiteSpace(ip))
            .Distinct()
            .OrderByDescending(ip => ip == lastServerIp)
            .ThenBy(ip => ip, StringComparer.Ordinal)
            .ToList();
    }

    private async Task<string> CheckServerAsync(string ip)
    {
        using (var client = new TcpClient())
        {
            try
            {
                Task connectTask = client.ConnectAsync(ip, serverPort);
                Task timeoutTask = Task.Delay(connectTimeoutMilliseconds);
                Task finishedTask = await Task.WhenAny(connectTask, timeoutTask);
                if (finishedTask != connectTask || !client.Connected)
                {
                    return null;
                }

                bool hasEndpoint = await ProbeHttpEndpointAsync(client, ip);
                if (hasEndpoint)
                {
                    AddDiagnostic("找到服务: " + ip + ":" + serverPort);
                }
                return hasEndpoint ? ip : null;
            }
            catch
            {
                return null;
            }
        }
    }

    private async Task<bool> ProbeHttpEndpointAsync(TcpClient client, string ip)
    {
        NetworkStream stream = client.GetStream();
        string requestText = "GET " + GetNormalizedApiPath(probePath) + " HTTP/1.1\r\nHost: " + ip + "\r\nConnection: close\r\n\r\n";
        byte[] requestBytes = Encoding.ASCII.GetBytes(requestText);
        await stream.WriteAsync(requestBytes, 0, requestBytes.Length);

        byte[] buffer = new byte[256];
        Task<int> readTask = stream.ReadAsync(buffer, 0, buffer.Length);
        Task timeoutTask = Task.Delay(connectTimeoutMilliseconds);
        Task finishedTask = await Task.WhenAny(readTask, timeoutTask);
        if (finishedTask != readTask || readTask.Result <= 0)
        {
            return false;
        }

        string response = Encoding.ASCII.GetString(buffer, 0, readTask.Result);
        return response.StartsWith("HTTP/", StringComparison.OrdinalIgnoreCase)
            && !response.Contains(" 404 ")
            && !response.Contains(" 503 ");
    }

    private List<string> BuildCandidateIps()
    {
        var ips = new List<string>();

        AddCandidate(ips, lastServerIp);
        if (fallbackIpInput != null)
        {
            AddCandidate(ips, fallbackIpInput.text);
        }

        foreach (IPAddress localAddress in GetLocalIPv4Addresses())
        {
            string localIp = localAddress.ToString();
            string prefix = GetClassCPrefix(localIp);
            if (string.IsNullOrEmpty(prefix))
            {
                AddCandidate(ips, localIp);
                continue;
            }

            AddDiagnostic("扫描网段: " + prefix + "1-254");
            for (int i = 1; i <= 254; i++)
            {
                AddCandidate(ips, prefix + i);
            }
        }

        return ips.Distinct().ToList();
    }

    private IEnumerable<IPAddress> GetLocalIPv4Addresses()
    {
        var addresses = new List<IPAddress>();

        try
        {
            foreach (NetworkInterface networkInterface in NetworkInterface.GetAllNetworkInterfaces())
            {
                if (networkInterface.OperationalStatus != OperationalStatus.Up)
                {
                    continue;
                }

                IPInterfaceProperties properties = networkInterface.GetIPProperties();
                foreach (UnicastIPAddressInformation address in properties.UnicastAddresses)
                {
                    if (address.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address.Address))
                    {
                        addresses.Add(address.Address);
                        AddDiagnostic("本机网卡: " + address.Address + " (" + networkInterface.Name + ")");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning("[VoiceServerDiscoveryUI] NetworkInterface scan failed: " + ex.Message);
            AddDiagnostic("读取网卡失败: " + ex.Message);
        }

        return addresses;
    }

    private static string GetClassCPrefix(string ip)
    {
        string[] parts = ip.Split('.');
        if (parts.Length != 4)
        {
            return "";
        }

        return parts[0] + "." + parts[1] + "." + parts[2] + ".";
    }

    private static void AddCandidate(List<string> ips, string ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return;
        }

        ip = ip.Trim();
        IPAddress parsed;
        if (IPAddress.TryParse(ip, out parsed) && parsed.AddressFamily == AddressFamily.InterNetwork)
        {
            ips.Add(ip);
        }
    }

    private void RefreshDropdown(string status, bool keepLastOnly)
    {
        EnsureUi();

        if (!keepLastOnly && serverIps.Count > 0 && fallbackIpInput != null)
        {
            fallbackIpInput.text = serverIps[0];
        }
        else if (!keepLastOnly && serverIps.Count == 0 && fallbackIpInput != null && string.IsNullOrWhiteSpace(lastServerIp))
        {
            fallbackIpInput.text = "未找到语音服务";
        }

        SetStatus(status);
    }

    private string GetSelectedIp()
    {
        IPAddress fallbackAddress;
        if (fallbackIpInput != null && IPAddress.TryParse(fallbackIpInput.text, out fallbackAddress))
        {
            return fallbackIpInput.text.Trim();
        }

        return lastServerIp;
    }

    private void EnsureUi()
    {
        if (uiCanvas == null)
        {
            uiCanvas = FindObjectOfType<Canvas>();
        }

        if (uiCanvas == null)
        {
            return;
        }

        if (fallbackIpInput == null)
        {
            fallbackIpInput = uiCanvas.GetComponentInChildren<InputField>(true);
        }

        if (rescanButton == null)
        {
            rescanButton = FindButton(uiCanvas.transform, "VoiceServerRescanButton");
        }

        if (statusText == null)
        {
            statusText = FindStatusText(uiCanvas.transform);
        }

        ConfigureInputField(fallbackIpInput);
        HideDropdowns(uiCanvas.transform);
        HookButtons();
    }

    private Text FindStatusText(Transform parent)
    {
        foreach (Text text in parent.GetComponentsInChildren<Text>(true))
        {
            if (text.name.IndexOf("status", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.name.IndexOf("voice", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return text;
            }
        }

        return null;
    }

    private Button FindButton(Transform parent, string buttonName)
    {
        foreach (Button button in parent.GetComponentsInChildren<Button>(true))
        {
            if (string.Equals(button.name, buttonName, StringComparison.OrdinalIgnoreCase))
            {
                return button;
            }
        }

        return null;
    }

    private void HideDropdowns(Transform parent)
    {
        foreach (Dropdown dropdown in parent.GetComponentsInChildren<Dropdown>(true))
        {
            dropdown.gameObject.SetActive(false);
        }
    }

    private void HookButtons()
    {
        if (rescanButton != null)
        {
            rescanButton.onClick.RemoveListener(Rescan);
            rescanButton.onClick.AddListener(Rescan);
        }
    }

    private void ConfigureInputField(InputField input)
    {
        if (input == null)
        {
            return;
        }

        input.contentType = InputField.ContentType.Standard;
        input.characterLimit = 32;
        input.interactable = false;

        Text placeholder = input.placeholder as Text;
        if (placeholder != null)
        {
            placeholder.text = "自动查找语音服务";
            placeholder.fontSize = 20;
            placeholder.color = new Color(0.45f, 0.45f, 0.45f, 0.75f);
        }

        Text text = input.textComponent;
        if (text != null)
        {
            text.fontSize = 22;
            text.color = Color.black;
            text.alignment = TextAnchor.MiddleCenter;
        }
    }

    private void AddDiagnostic(string line)
    {
        Debug.Log("[VoiceServerDiscoveryUI] " + line);
        diagnosticLines.Add(line);
        while (diagnosticLines.Count > 6)
        {
            diagnosticLines.RemoveAt(0);
        }

        UpdateStatusText();
    }

    private void SetStatus(string text)
    {
        if (diagnosticLines.Count == 0)
        {
            diagnosticLines.Add(text);
        }
        else
        {
            diagnosticLines[0] = text;
        }

        UpdateStatusText();
    }

    private void UpdateStatusText()
    {
        if (statusText != null)
        {
            statusText.text = string.Join("\n", diagnosticLines.ToArray());
        }
    }

    private static string BuildApiUrl(string ip, int port, string path)
    {
        return "http://" + ip.Trim() + ":" + port + GetNormalizedApiPath(path);
    }

    private static string GetNormalizedApiPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        return path.StartsWith("/", StringComparison.Ordinal) ? path : "/" + path;
    }
}
