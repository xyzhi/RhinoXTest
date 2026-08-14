using System.Collections;
using UnityEngine;

public class NpcBlinkController : MonoBehaviour
{
    [SerializeField] private SkinnedMeshRenderer faceRenderer;
    [SerializeField] private SkinnedMeshRenderer faceRenderer2;
    [SerializeField] private int[] blinkBlendShapeIndices = { 8 };
    public string[] targetRendererNameKeywords = { "R_laoyeye_rig_st", "R_laoyeye_rig_Eyelashes1" };
    public string[] blinkBlendShapeNameKeywords = { "F_yd_BY_L_max", "F_yd_BY_R_max" };
    [SerializeField] private float minBlinkInterval = 3f;
    [SerializeField] private float maxBlinkInterval = 6f;
    [SerializeField] private float closeSeconds = 0.06f;
    [SerializeField] private float closedSeconds = 0.04f;
    [SerializeField] private float openSeconds = 0.08f;
    [SerializeField] private float blinkWeight = 100f;

    private BlinkTarget[] blinkTargets;
    private Coroutine blinkRoutine;

    private class BlinkTarget
    {
        public SkinnedMeshRenderer renderer;
        public int[] indices;
        public float[] originalWeights;
    }

    private void Awake()
    {
        CacheBlinkTargets();
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
        if (blinkTargets == null || blinkTargets.Length == 0)
        {
            return;
        }

        for (int targetIndex = 0; targetIndex < blinkTargets.Length; targetIndex++)
        {
            BlinkTarget target = blinkTargets[targetIndex];
            if (target.renderer == null || target.renderer.sharedMesh == null || target.indices == null)
            {
                continue;
            }

            for (int i = 0; i < target.indices.Length; i++)
            {
                int index = target.indices[i];
                if (index < 0 || index >= target.renderer.sharedMesh.blendShapeCount)
                {
                    continue;
                }

                float baseWeight = target.originalWeights != null && index < target.originalWeights.Length ? target.originalWeights[index] : 0f;
                target.renderer.SetBlendShapeWeight(index, Mathf.Clamp(baseWeight + weight, 0f, 100f));
            }
        }
    }

    private void CacheBlinkTargets()
    {
        System.Collections.Generic.List<BlinkTarget> targets = new System.Collections.Generic.List<BlinkTarget>();

        AddBlinkTarget(targets, faceRenderer);
        AddBlinkTarget(targets, faceRenderer2);

        SkinnedMeshRenderer[] renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            if (!MatchesTargetRendererName(renderer))
            {
                continue;
            }

            AddBlinkTarget(targets, renderer);
        }

        blinkTargets = targets.ToArray();
    }

    private void AddBlinkTarget(System.Collections.Generic.List<BlinkTarget> targets, SkinnedMeshRenderer renderer)
    {
        if (renderer == null || renderer.sharedMesh == null || ContainsRenderer(targets, renderer))
        {
            return;
        }

        int[] indices = FindBlinkBlendShapeIndices(renderer);
        if (indices.Length == 0 && targets.Count == 0)
        {
            indices = blinkBlendShapeIndices == null ? new int[0] : blinkBlendShapeIndices;
        }

        if (indices.Length == 0)
        {
            return;
        }

        BlinkTarget target = new BlinkTarget();
        target.renderer = renderer;
        target.indices = indices;
        target.originalWeights = new float[renderer.sharedMesh.blendShapeCount];
        for (int i = 0; i < target.originalWeights.Length; i++)
        {
            target.originalWeights[i] = renderer.GetBlendShapeWeight(i);
        }

        targets.Add(target);
    }

    private bool ContainsRenderer(System.Collections.Generic.List<BlinkTarget> targets, SkinnedMeshRenderer renderer)
    {
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].renderer == renderer)
            {
                return true;
            }
        }

        return false;
    }

    private void RestoreOriginalWeights()
    {
        if (blinkTargets == null)
        {
            return;
        }

        for (int targetIndex = 0; targetIndex < blinkTargets.Length; targetIndex++)
        {
            BlinkTarget target = blinkTargets[targetIndex];
            if (target.renderer == null || target.originalWeights == null)
            {
                continue;
            }

            for (int i = 0; i < target.originalWeights.Length; i++)
            {
                target.renderer.SetBlendShapeWeight(i, target.originalWeights[i]);
            }
        }
    }

    private int[] FindBlinkBlendShapeIndices(SkinnedMeshRenderer renderer)
    {
        if (renderer == null || renderer.sharedMesh == null || blinkBlendShapeNameKeywords == null)
        {
            return new int[0];
        }

        System.Collections.Generic.List<int> indices = new System.Collections.Generic.List<int>();
        Mesh mesh = renderer.sharedMesh;
        for (int i = 0; i < mesh.blendShapeCount; i++)
        {
            string blendShapeName = mesh.GetBlendShapeName(i);
            if (MatchesBlinkBlendShapeName(blendShapeName))
            {
                indices.Add(i);
            }
        }

        return indices.ToArray();
    }

    private bool MatchesTargetRendererName(SkinnedMeshRenderer renderer)
    {
        if (renderer == null)
        {
            return false;
        }

        if (targetRendererNameKeywords == null || targetRendererNameKeywords.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < targetRendererNameKeywords.Length; i++)
        {
            string keyword = targetRendererNameKeywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) && renderer.name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }

    private bool MatchesBlinkBlendShapeName(string blendShapeName)
    {
        if (string.IsNullOrEmpty(blendShapeName))
        {
            return false;
        }

        for (int i = 0; i < blinkBlendShapeNameKeywords.Length; i++)
        {
            string keyword = blinkBlendShapeNameKeywords[i];
            if (!string.IsNullOrWhiteSpace(keyword) && blendShapeName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }
        }

        return false;
    }
}
