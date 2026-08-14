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
    static public int curPersonalityType = 1;
    static public string PersonalityName
    {
        get
        {
            switch (curPersonalityType)
            {
                case 1:
                    return "执迷不悟型";
                case 2:
                    return "半信半疑型";
                case 3:
                    return "固执暴躁型";
                case 4:
                    return "胆小怕事型";
                case 5:
                    return "贪小便宜型";
                case 6:
                    return "自以为是型";
                case 7:
                    return "亲情绑架型";
                default:
                    return "执迷不悟型";
            }
        }
    }
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
        if (missionPanel != null)
        {
            missionPanel.SetActive(false);
        }

        if (mission1Text != null)
        {
            mission1Text.SetActive(false);
        }

        if (mission2Text != null)
        {
            mission2Text.SetActive(false);
        }

        if (mission3Text != null)
        {
            mission3Text.SetActive(false);
        }
    }
    public void ChangeScene()
    {
        ChangeScene(curPersonalityType);
    }
    public void ChangeScene(int personalityType)
    {
        curPersonalityType = Mathf.Clamp(personalityType, 1, 7);
        StartCoroutine(LoadSceneAsync());
    }
    public void ShowMission(int type)
    {
        curSceneType = (SceneType)type;
        Debug.Log(curSceneType.ToString());
        if (missionPanel != null)
        {
            missionPanel.SetActive(true);
        }

        switch (type)
        {
            case 1:
                if (mission1Text != null)
                {
                    mission1Text.SetActive(true);
                }
                break;
            case 2:
                if (mission2Text != null)
                {
                    mission2Text.SetActive(true);
                }
                break;
            case 3:
                if (mission3Text != null)
                {
                    mission3Text.SetActive(true);
                }
                break;
            default:
                break;
        }
    }
    IEnumerator LoadSceneAsync()
    {
        VoiceServerDiscoveryUI.ApplySelectedServerToVoiceChatManager();
        //int goScene = 1;
        //switch (curSceneType)
        //{
        //    case SceneType.OldMan:
        //        goScene = 1;
        //        break;
        //    case SceneType.Boy:
        //        goScene = 5;
        //        break;
        //    case SceneType.Girl:
        //        goScene = 3;
        //        break;
        //    default:
        //        break;
        //}
        //2026/8/12改成直接到老头场景
        AsyncOperation operation = SceneManager.LoadSceneAsync(1);
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
