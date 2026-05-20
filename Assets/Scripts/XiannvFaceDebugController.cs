using System;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class XiannvFaceDebugController : MonoBehaviour
{
    public enum ExpressionPreset
    {
        Neutral,
        Smile,
        Sad,
        Angry,
        Surprise,
        Relaxed
    }

    public enum VowelPreset
    {
        Neutral,
        A,
        O,
        E,
        I,
        U
    }

    public enum BlinkPreset
    {
        Open,
        Half,
        Closed
    }

    public ExpressionPreset expression;
    public VowelPreset vowel;
    public BlinkPreset blink;

    private SkinnedMeshRenderer bodyRenderer;
    private readonly Dictionary<string, int> blendShapeIndexMap = new Dictionary<string, int>();
    private readonly HashSet<int> controlledIndices = new HashSet<int>();
    private ExpressionPreset lastExpression = (ExpressionPreset)(-1);
    private VowelPreset lastVowel = (VowelPreset)(-1);
    private BlinkPreset lastBlink = (BlinkPreset)(-1);

    private void OnEnable()
    {
        RefreshRenderer();
        ApplyIfNeeded(true);
    }

    private void OnValidate()
    {
        RefreshRenderer();
        ApplyIfNeeded(true);
    }

    private void LateUpdate()
    {
        ApplyIfNeeded(false);
    }

    private void RefreshRenderer()
    {
        if (bodyRenderer != null && bodyRenderer.sharedMesh != null)
        {
            return;
        }

        bodyRenderer = FindBodyRenderer(transform);
        if (bodyRenderer == null)
        {
            var xiannv = GameObject.Find("xiannv");
            if (xiannv != null)
            {
                bodyRenderer = FindBodyRenderer(xiannv.transform);
            }
        }

        RebuildBlendShapeMap();
    }

    private SkinnedMeshRenderer FindBodyRenderer(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer == null || renderer.sharedMesh == null)
            {
                continue;
            }

            if (renderer.name == "CC_Base_Body")
            {
                return renderer;
            }
        }

        foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (renderer != null && renderer.sharedMesh != null && renderer.sharedMesh.blendShapeCount > 100)
            {
                return renderer;
            }
        }

        return null;
    }

    private void RebuildBlendShapeMap()
    {
        blendShapeIndexMap.Clear();

        if (bodyRenderer == null || bodyRenderer.sharedMesh == null)
        {
            return;
        }

        var mesh = bodyRenderer.sharedMesh;
        for (var i = 0; i < mesh.blendShapeCount; i++)
        {
            var normalized = NormalizeName(mesh.GetBlendShapeName(i));
            if (!blendShapeIndexMap.ContainsKey(normalized))
            {
                blendShapeIndexMap.Add(normalized, i);
            }
        }
    }

    private void ApplyIfNeeded(bool force)
    {
        if (!force && lastExpression == expression && lastVowel == vowel && lastBlink == blink)
        {
            return;
        }

        RefreshRenderer();
        if (bodyRenderer == null || bodyRenderer.sharedMesh == null)
        {
            return;
        }

        ClearControlledWeights();
        ApplyExpression();
        ApplyVowel();
        ApplyBlink();

        lastExpression = expression;
        lastVowel = vowel;
        lastBlink = blink;
    }

    private void ClearControlledWeights()
    {
        foreach (var index in controlledIndices)
        {
            bodyRenderer.SetBlendShapeWeight(index, 0f);
        }

        controlledIndices.Clear();
    }

    private void ApplyExpression()
    {
        switch (expression)
        {
            case ExpressionPreset.Smile:
                // FACS AU12 + AU6: lip corner puller + cheek raiser
                AddWeight(72f, "Mouth Smile L", "Mouth_Smile_L", "mouthSmileLeft");
                AddWeight(72f, "Mouth Smile R", "Mouth_Smile_R", "mouthSmileRight");
                AddWeight(28f, "Cheek Raise L", "Cheek_Raise_L", "cheekSquintLeft");
                AddWeight(28f, "Cheek Raise R", "Cheek_Raise_R", "cheekSquintRight");
                AddWeight(12f, "Eye Squint L", "Eye_Squint_L", "eyeSquintLeft");
                AddWeight(12f, "Eye Squint R", "Eye_Squint_R", "eyeSquintRight");
                break;
            case ExpressionPreset.Sad:
                // FACS AU1 + AU4 + AU15: inner brow raiser + brow lowerer + lip corner depressor
                AddWeight(36f, "Brow Raise Inner L", "Brow_Raise_Inner_L", "browInnerUp");
                AddWeight(36f, "Brow Raise Inner R", "Brow_Raise_Inner_R", "browInnerUp");
                AddWeight(18f, "Brow Drop L", "Brow_Drop_L", "Brow Down L", "Brow_Down_L", "browDownLeft");
                AddWeight(18f, "Brow Drop R", "Brow_Drop_R", "Brow Down R", "Brow_Down_R", "browDownRight");
                AddWeight(52f, "Mouth Frown L", "Mouth_Frown_L", "mouthFrownLeft");
                AddWeight(52f, "Mouth Frown R", "Mouth_Frown_R", "mouthFrownRight");
                break;
            case ExpressionPreset.Angry:
                // FACS AU4 + AU7 + AU23
                AddWeight(48f, "Brow Drop L", "Brow_Drop_L", "Brow Down L", "Brow_Down_L", "browDownLeft");
                AddWeight(48f, "Brow Drop R", "Brow_Drop_R", "Brow Down R", "Brow_Down_R", "browDownRight");
                AddWeight(34f, "Brow Compress L", "Brow_Compress_L", "Brow Lateral L", "Brow_Lateral_L");
                AddWeight(34f, "Brow Compress R", "Brow_Compress_R", "Brow Lateral R", "Brow_Lateral_R");
                AddWeight(22f, "Eye Squint L", "Eye_Squint_L", "eyeSquintLeft");
                AddWeight(22f, "Eye Squint R", "Eye_Squint_R", "eyeSquintRight");
                AddWeight(30f, "Mouth Press L", "Mouth_Press_L", "mouthPressLeft");
                AddWeight(30f, "Mouth Press R", "Mouth_Press_R", "mouthPressRight");
                break;
            case ExpressionPreset.Surprise:
                // FACS AU1 + AU2 + AU5 + AU26
                AddWeight(36f, "Brow Raise Inner L", "Brow_Raise_Inner_L", "browInnerUp");
                AddWeight(36f, "Brow Raise Inner R", "Brow_Raise_Inner_R", "browInnerUp");
                AddWeight(24f, "Brow Raise Outer L", "Brow_Raise_Outer_L", "browOuterUpLeft");
                AddWeight(24f, "Brow Raise Outer R", "Brow_Raise_Outer_R", "browOuterUpRight");
                AddWeight(42f, "Eye Wide L", "Eye_Wide_L", "Eye Widen L", "Eye_Widen_L", "eyeWideLeft");
                AddWeight(42f, "Eye Wide R", "Eye_Wide_R", "Eye Widen R", "Eye_Widen_R", "eyeWideRight");
                AddWeight(45f, "Jaw Open", "Jaw_Open", "jawOpen");
                break;
            case ExpressionPreset.Relaxed:
                // VRM relaxed/fun style: mild smile without cheek squeeze
                AddWeight(24f, "Mouth Smile L", "Mouth_Smile_L", "mouthSmileLeft");
                AddWeight(24f, "Mouth Smile R", "Mouth_Smile_R", "mouthSmileRight");
                AddWeight(10f, "Mouth Stretch L", "Mouth_Stretch_L", "mouthStretchLeft");
                AddWeight(10f, "Mouth Stretch R", "Mouth_Stretch_R", "mouthStretchRight");
                break;
        }
    }

    private void ApplyVowel()
    {
        switch (vowel)
        {
            case VowelPreset.A:
                AddWeight(78f, "Ah");
                AddWeight(22f, "AE");
                AddWeight(18f, "Jaw Open", "Jaw_Open", "jawOpen");
                break;
            case VowelPreset.O:
                AddWeight(68f, "Oh", "oh");
                AddWeight(24f, "W_OO", "W OO", "ou");
                AddWeight(10f, "Mouth Funnel", "Mouth_Funnel", "mouthFunnel");
                break;
            case VowelPreset.E:
                AddWeight(50f, "Er");
                AddWeight(24f, "EE", "ee");
                AddWeight(14f, "Mouth Stretch L", "Mouth_Stretch_L", "mouthStretchLeft");
                AddWeight(14f, "Mouth Stretch R", "Mouth_Stretch_R", "mouthStretchRight");
                break;
            case VowelPreset.I:
                AddWeight(76f, "EE", "ee");
                AddWeight(18f, "Ih", "ih");
                AddWeight(12f, "Mouth Stretch L", "Mouth_Stretch_L", "mouthStretchLeft");
                AddWeight(12f, "Mouth Stretch R", "Mouth_Stretch_R", "mouthStretchRight");
                break;
            case VowelPreset.U:
                AddWeight(70f, "W_OO", "W OO", "ou");
                AddWeight(18f, "Oh", "oh");
                AddWeight(18f, "Mouth Pucker", "Mouth_Pucker", "mouthPucker");
                break;
        }
    }

    private void ApplyBlink()
    {
        float weight;
        switch (blink)
        {
            case BlinkPreset.Half:
                weight = 50f;
                break;
            case BlinkPreset.Closed:
                weight = 100f;
                break;
            default:
                weight = 0f;
                break;
        }

        if (weight <= 0f)
        {
            return;
        }

        AddWeight(weight, "Eye Blink L", "Eye_Blink_L", "eyeBlinkLeft");
        AddWeight(weight, "Eye Blink R", "Eye_Blink_R", "eyeBlinkRight");
    }

    private void AddWeight(float weight, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (!blendShapeIndexMap.TryGetValue(NormalizeName(alias), out var index))
            {
                continue;
            }

            controlledIndices.Add(index);
            var current = bodyRenderer.GetBlendShapeWeight(index);
            bodyRenderer.SetBlendShapeWeight(index, Mathf.Clamp(current + weight, 0f, 100f));
            return;
        }
    }

    private static string NormalizeName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
        {
            return string.Empty;
        }

        var chars = new List<char>(rawName.Length);
        foreach (var c in rawName)
        {
            if (char.IsLetterOrDigit(c))
            {
                chars.Add(char.ToLowerInvariant(c));
            }
        }

        return new string(chars.ToArray());
    }
}
