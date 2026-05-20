using UnityEngine;
using UnityEngine.UI;

public class PlayerSpeechCaption : MonoBehaviour
{
    [SerializeField] private Text captionText;
    [SerializeField] private float visibleSeconds = 3f;

    private float hideAtTime;

    private void Awake()
    {
        Hide();
    }

    private void LateUpdate()
    {
        if (captionText != null && captionText.gameObject.activeSelf && hideAtTime > 0f && Time.time >= hideAtTime)
        {
            Hide();
        }
    }

    public void Show(string text)
    {
        if (captionText == null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        captionText.text = text;
        hideAtTime = Time.time + visibleSeconds;
        captionText.gameObject.SetActive(true);
    }

    public void Hide()
    {
        hideAtTime = 0f;
        if (captionText != null)
        {
            captionText.gameObject.SetActive(false);
        }
    }
}
