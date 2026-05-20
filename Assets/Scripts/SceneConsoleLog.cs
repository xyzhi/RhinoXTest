using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SceneConsoleLog : MonoBehaviour
{
    [SerializeField] private Text consoleText;
    [SerializeField] private int maxLines = 40;

    private readonly Queue<string> lines = new Queue<string>();

    private void Awake()
    {
        Clear();
    }

    private void OnEnable()
    {
        Application.logMessageReceived += HandleLog;
    }

    private void OnDisable()
    {
        Application.logMessageReceived -= HandleLog;
    }

    public void Clear()
    {
        lines.Clear();
        if (consoleText != null)
        {
            consoleText.text = string.Empty;
        }
    }

    private void HandleLog(string condition, string stackTrace, LogType type)
    {
        if (consoleText == null || string.IsNullOrEmpty(condition))
        {
            return;
        }

        lines.Enqueue($"[{type}] {condition}");
        while (lines.Count > maxLines)
        {
            lines.Dequeue();
        }

        consoleText.text = string.Join("\n", lines);
    }
}
