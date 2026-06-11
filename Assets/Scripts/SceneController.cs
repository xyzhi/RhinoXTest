using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public enum SceneType
{
    None,
    OldMan,
    Boy,
    Girl
}
public class SceneController : MonoBehaviour
{
    public GameObject missionPanel;
    public GameObject mission1Text;
    public GameObject mission2Text;
    public GameObject mission3Text;
    static public SceneType curSceneType;
    static public string SceneName
    {
        get
        {
            switch (curSceneType)
            {
                case SceneType.None:
                    break;
                case SceneType.OldMan:
                    return "投资理财";
                    break;
                case SceneType.Boy:
                    return "情感诈骗";
                    break;
                case SceneType.Girl:
                    return "刷单返利";
                    break;
                default:
                    break;
            }
            return "";
        }
    }
    private void Start()
    {
        missionPanel.SetActive(false);
        mission1Text.SetActive(false);
        mission2Text.SetActive(false);
        mission3Text.SetActive(false);
    }
    public void ChangeScene()
    {        
        StartCoroutine(LoadSceneAsync());
    }
    public void ShowMission(int type)
    {
        curSceneType = (SceneType)type;
        Debug.Log(curSceneType.ToString());
        missionPanel.SetActive(true);
        switch (type)
        {
            case 1:
                mission1Text.SetActive(true);
                break;
            case 2:
                mission2Text.SetActive(true);
                break;
            case 3:
                mission3Text.SetActive(true);
                break;
            default:
                break;
        }
    }
    IEnumerator LoadSceneAsync()
    {
        VoiceServerDiscoveryUI.ApplySelectedServerToVoiceChatManager();
        int goScene = 1;
        switch (curSceneType)
        {
            case SceneType.OldMan:
                goScene = 1;
                break;
            case SceneType.Boy:
                goScene = 5;
                break;
            case SceneType.Girl:
                goScene = 3;
                break;
            default:
                break;
        }
        AsyncOperation operation = SceneManager.LoadSceneAsync(goScene);
        operation.allowSceneActivation = false;

        float progress = 0f;

        while (progress < 0.9f)
        {
            progress = Mathf.Clamp01(operation.progress / 0.9f);
            Debug.Log($"加载进度：{progress * 100}%");
            yield return null;
        }

        // 这里可以做：显示“加载完成，点击进入”按钮
        Debug.Log("加载完成，准备切换场景");

        operation.allowSceneActivation = true;
    }
}
