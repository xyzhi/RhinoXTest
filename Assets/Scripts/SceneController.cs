using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;


public enum RoleType
{
    OldMan,
    Boy,
    Girl
}
public class SceneController : MonoBehaviour
{
    public GameObject missionPanel;
    static public RoleType curRole;
    public void ChangeScene(int scene)
    {
        StartCoroutine(LoadSceneAsync(scene));
    }
    public void ShowMission2() { 
    }
    public void ShowMission(int type)
    {
        curRole = (RoleType)type;
        Debug.Log(curRole.ToString());
        missionPanel.SetActive(true);
    }
    IEnumerator LoadSceneAsync(int scene)
    {
        VoiceServerDiscoveryUI.ApplySelectedServerToVoiceChatManager();
        AsyncOperation operation = SceneManager.LoadSceneAsync(scene);
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
