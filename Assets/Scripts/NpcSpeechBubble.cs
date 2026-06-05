using UnityEngine;

public class NpcSpeechBubble : MonoBehaviour
{
    [SerializeField] private TextMesh textMesh;
    private bool isShowing;

    private void Awake()
    {
        if (!isShowing)
        {
            Hide();
        }
    }

    private void LateUpdate()
    {
        if (textMesh == null)
        {
            return;
        }

        Camera mainCamera = Camera.main;
        if (mainCamera == null)
        {
            return;
        }

        Vector3 direction = textMesh.transform.position - mainCamera.transform.position;
        if (direction.sqrMagnitude > 0.0001f)
        {
            textMesh.transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }
    }

    public void Show(string text)
    {
        if (textMesh == null)
        {
            return;
        }

        isShowing = true;
        textMesh.text = string.IsNullOrWhiteSpace(text) ? "..." : text;
        textMesh.gameObject.SetActive(true);
    }

    public void Hide()
    {
        isShowing = false;
        if (textMesh != null)
        {
            textMesh.gameObject.SetActive(false);
        }
    }
}
