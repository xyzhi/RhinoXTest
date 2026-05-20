using System.Collections;
using UnityEngine;

public class NpcBlinkController : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer faceRenderer;
    [SerializeField] private int[] blinkBlendShapeIndices = { 8 };
    [SerializeField] private float minBlinkInterval = 3f;
    [SerializeField] private float maxBlinkInterval = 6f;
    [SerializeField] private float closeSeconds = 0.06f;
    [SerializeField] private float closedSeconds = 0.04f;
    [SerializeField] private float openSeconds = 0.08f;
    [SerializeField] private float blinkWeight = 100f;

    private float[] originalWeights;
    private Coroutine blinkRoutine;

    private void Awake()
    {
        if (faceRenderer == null)
        {
            faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }

        CacheOriginalWeights();
    }

    private void OnEnable()
    {
        if (blinkRoutine == null)
        {
            blinkRoutine = StartCoroutine(BlinkLoop());
        }
    }

    private void OnDisable()
    {
        if (blinkRoutine != null)
        {
            StopCoroutine(blinkRoutine);
            blinkRoutine = null;
        }

        RestoreOriginalWeights();
    }

    private IEnumerator BlinkLoop()
    {
        while (true)
        {
            float waitSeconds = Random.Range(minBlinkInterval, Mathf.Max(minBlinkInterval, maxBlinkInterval));
            yield return new WaitForSeconds(waitSeconds);
            yield return BlinkOnce();
        }
    }

    private IEnumerator BlinkOnce()
    {
        yield return SetBlinkWeightOverTime(0f, blinkWeight, closeSeconds);
        SetBlinkWeight(blinkWeight);

        if (closedSeconds > 0f)
        {
            yield return new WaitForSeconds(closedSeconds);
        }

        yield return SetBlinkWeightOverTime(blinkWeight, 0f, openSeconds);
        SetBlinkWeight(0f);
    }

    private IEnumerator SetBlinkWeightOverTime(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            SetBlinkWeight(to);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetBlinkWeight(Mathf.Lerp(from, to, t));
            yield return null;
        }
    }

    private void SetBlinkWeight(float weight)
    {
        if (faceRenderer == null || faceRenderer.sharedMesh == null)
        {
            return;
        }

        for (int i = 0; i < blinkBlendShapeIndices.Length; i++)
        {
            int index = blinkBlendShapeIndices[i];
            if (index < 0 || index >= faceRenderer.sharedMesh.blendShapeCount)
            {
                continue;
            }

            float baseWeight = originalWeights != null && index < originalWeights.Length ? originalWeights[index] : 0f;
            faceRenderer.SetBlendShapeWeight(index, Mathf.Clamp(baseWeight + weight, 0f, 100f));
        }
    }

    private void CacheOriginalWeights()
    {
        if (faceRenderer == null || faceRenderer.sharedMesh == null)
        {
            return;
        }

        int count = faceRenderer.sharedMesh.blendShapeCount;
        originalWeights = new float[count];
        for (int i = 0; i < count; i++)
        {
            originalWeights[i] = faceRenderer.GetBlendShapeWeight(i);
        }
    }

    private void RestoreOriginalWeights()
    {
        if (faceRenderer == null || originalWeights == null)
        {
            return;
        }

        for (int i = 0; i < originalWeights.Length; i++)
        {
            faceRenderer.SetBlendShapeWeight(i, originalWeights[i]);
        }
    }
}
