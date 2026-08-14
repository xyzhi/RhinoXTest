using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class LipSyncBackendAudioPlayer : MonoBehaviour
{
    public AudioSource audioSource;
    public Transform expressionRoot;
    public float expressionWeight = 100f;
    public bool resetExpressionWhenStopped = true;

    public string CurrentEmotion { get; private set; }

    private static readonly string[] ExpressionKeys = { "xi", "nu", "ai", "le" };

    private readonly Dictionary<string, List<ExpressionBlendShape>> expressionTargets =
        new Dictionary<string, List<ExpressionBlendShape>>();
    private readonly List<ExpressionBlendShape> allExpressionTargets = new List<ExpressionBlendShape>();

    private bool stoppedManually;
    private bool expressionTargetsCached;
    private bool hasActiveExpression;

    private struct ExpressionBlendShape
    {
        public SkinnedMeshRenderer Renderer;
        public int Index;

        public ExpressionBlendShape(SkinnedMeshRenderer renderer, int index)
        {
            Renderer = renderer;
            Index = index;
        }
    }

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
        }

        CacheExpressionTargets();
    }

    private void Update()
    {
        if (stoppedManually || audioSource == null || audioSource.isPlaying)
        {
            return;
        }

        if (resetExpressionWhenStopped && hasActiveExpression)
        {
            ResetExpressionWeights();
        }
    }

    public void PlayBackendAudio(AudioClip clip, string emotion)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        stoppedManually = false;
        CurrentEmotion = emotion;
        audioSource.clip = clip;
        ApplyEmotion(emotion);
        audioSource.Play();
    }

    public void ApplyEmotion(string emotion)
    {
        CurrentEmotion = emotion;
        ApplyExpression(GetExpressionKeyFromEmotion(emotion));
    }

    public void StopPlayback()
    {
        stoppedManually = true;

        if (audioSource != null)
        {
            audioSource.Stop();
        }

        if (resetExpressionWhenStopped)
        {
            ResetExpressionWeights();
        }
    }

    private void OnDisable()
    {
        if (resetExpressionWhenStopped)
        {
            ResetExpressionWeights();
        }
    }

    private void ApplyExpression(string expressionKey)
    {
        if (!expressionTargetsCached)
        {
            CacheExpressionTargets();
        }

        ResetExpressionWeights();

        expressionKey = NormalizeExpressionKey(expressionKey);
        if (string.IsNullOrEmpty(expressionKey))
        {
            return;
        }

        List<ExpressionBlendShape> targets;
        if (!expressionTargets.TryGetValue(expressionKey, out targets))
        {
            return;
        }

        float weight = Mathf.Clamp(expressionWeight, 0f, 100f);
        for (int i = 0; i < targets.Count; i++)
        {
            ExpressionBlendShape target = targets[i];
            if (target.Renderer != null)
            {
                target.Renderer.SetBlendShapeWeight(target.Index, weight);
            }
        }

        hasActiveExpression = targets.Count > 0;
    }

    private void ResetExpressionWeights()
    {
        if (!expressionTargetsCached)
        {
            CacheExpressionTargets();
        }

        for (int i = 0; i < allExpressionTargets.Count; i++)
        {
            ExpressionBlendShape target = allExpressionTargets[i];
            if (target.Renderer != null)
            {
                target.Renderer.SetBlendShapeWeight(target.Index, 0f);
            }
        }

        hasActiveExpression = false;
    }

    private void CacheExpressionTargets()
    {
        expressionTargets.Clear();
        allExpressionTargets.Clear();

        for (int i = 0; i < ExpressionKeys.Length; i++)
        {
            expressionTargets[ExpressionKeys[i]] = new List<ExpressionBlendShape>();
        }

        Transform root = expressionRoot;
        if (root == null)
        {
            root = FindChildRecursive(transform, "mode");
        }

        if (root == null)
        {
            root = transform;
        }

        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SkinnedMeshRenderer renderer = renderers[i];
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null)
            {
                continue;
            }

            for (int blendShapeIndex = 0; blendShapeIndex < mesh.blendShapeCount; blendShapeIndex++)
            {
                string blendShapeName = mesh.GetBlendShapeName(blendShapeIndex);
                string expressionKey = GetExpressionKeyFromBlendShapeName(blendShapeName);
                if (string.IsNullOrEmpty(expressionKey))
                {
                    continue;
                }

                ExpressionBlendShape target = new ExpressionBlendShape(renderer, blendShapeIndex);
                expressionTargets[expressionKey].Add(target);
                allExpressionTargets.Add(target);
            }
        }

        expressionTargetsCached = true;
    }

    private Transform FindChildRecursive(Transform root, string childName)
    {
        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }

            Transform result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }

        return null;
    }

    private string GetExpressionKeyFromBlendShapeName(string blendShapeName)
    {
        if (string.IsNullOrEmpty(blendShapeName))
        {
            return null;
        }

        for (int i = 0; i < ExpressionKeys.Length; i++)
        {
            string key = ExpressionKeys[i];
            if (blendShapeName.IndexOf("F_yd_" + key + "_max", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return key;
            }
        }

        return null;
    }

    private string GetExpressionKeyFromEmotion(string emotion)
    {
        if (string.IsNullOrWhiteSpace(emotion))
        {
            return null;
        }

        switch (emotion.Trim())
        {
            case "\u9ed8\u8ba4":
                return null;
            case "\u60b2\u4f24":
                return "ai";
            case "\u7126\u6025":
                return "xi";
            case "\u751f\u6c14":
                return "nu";
            case "\u5feb\u4e50":
                return "le";
            default:
                return NormalizeExpressionKey(emotion);
        }
    }

    private string NormalizeExpressionKey(string expressionKey)
    {
        if (string.IsNullOrEmpty(expressionKey))
        {
            return null;
        }

        expressionKey = expressionKey.Trim().ToLowerInvariant();
        switch (expressionKey)
        {
            case "\u559c":
            case "happy":
            case "joy":
            case "anxious":
                return "xi";
            case "\u6012":
            case "angry":
                return "nu";
            case "\u54c0":
            case "sad":
                return "ai";
            case "\u4e50":
                return "le";
            default:
                return expressionKey;
        }
    }
}
