using UnityEngine;
using UnityEngine.UI;

public class StartPanel : MonoBehaviour
{
    public SceneController sceneController;
    public Button[] personalityButtons;
    public int defaultPersonalityType = 1;
    public AudioSource uiAudioSource;
    public AudioClip slideTriggerClip;
    public AudioClip startTrainingClickClip;

    private int selectedPersonalityType;
    private bool isStartingTraining;

    private void Start()
    {
        SelectPersonality(defaultPersonalityType, false);
    }

    public void SelectPersonality(int personalityType)
    {
        SelectPersonality(personalityType, true);
    }

    private void SelectPersonality(int personalityType, bool playSound)
    {
        selectedPersonalityType = Mathf.Clamp(personalityType, 1, 7);
        SceneController.curPersonalityType = selectedPersonalityType;

        if (personalityButtons == null || selectedPersonalityType > personalityButtons.Length)
        {
            return;
        }

        Button selectedButton = personalityButtons[selectedPersonalityType - 1];
        if (selectedButton != null)
        {
            selectedButton.Select();
        }

        if (playSound)
        {
            PlayUiSound(slideTriggerClip);
        }
    }

    public void StartTraining()
    {
        if (isStartingTraining)
        {
            return;
        }

        if (selectedPersonalityType < 1 || selectedPersonalityType > 7)
        {
            SelectPersonality(defaultPersonalityType, false);
        }

        StartCoroutine(StartTrainingAfterClickSound());
    }

    private System.Collections.IEnumerator StartTrainingAfterClickSound()
    {
        isStartingTraining = true;
        PlayUiSound(startTrainingClickClip);

        if (startTrainingClickClip != null)
        {
            yield return new WaitForSeconds(startTrainingClickClip.length);
        }

        if (sceneController == null)
        {
            sceneController = FindObjectOfType<SceneController>();
        }

        if (sceneController != null)
        {
            sceneController.ChangeScene(selectedPersonalityType);
        }
    }

    private void PlayUiSound(AudioClip clip)
    {
        if (clip == null)
        {
            return;
        }

        if (uiAudioSource == null)
        {
            uiAudioSource = GetComponent<AudioSource>();
        }

        if (uiAudioSource == null)
        {
            uiAudioSource = gameObject.AddComponent<AudioSource>();
            uiAudioSource.playOnAwake = false;
            uiAudioSource.spatialBlend = 0f;
        }

        uiAudioSource.PlayOneShot(clip);
    }
}
