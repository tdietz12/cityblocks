using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Scales a RectTransform so that the visible center of a TMP text (its textBounds) stays fixed on screen,
    /// regardless of the rect's pivot or the text's alignment. Assumes no local rotation on the target.
    /// </summary>
    internal static class TextCenteredPulse
    {
        private static readonly Dictionary<RectTransform, Vector2> AppliedOffsets = new Dictionary<RectTransform, Vector2>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearOffsets() { AppliedOffsets.Clear(); }

        /// <summary>
        /// Offset (in the target's parent space) that keeps the text center fixed when the target is scaled
        /// from <paramref name="baseScale"/> to <paramref name="baseScale"/> * <paramref name="factor"/>.
        /// </summary>
        public static Vector2 ComputeOffset(RectTransform target, TMP_Text text, Vector3 baseScale, float factor)
        {
            if (target == null || text == null || Mathf.Approximately(factor, 1f)) return Vector2.zero;
            if (text.havePropertiesChanged) text.ForceMeshUpdate();
            if (text.textInfo == null || text.textInfo.characterCount == 0) return Vector2.zero;
            Vector3 center = target.InverseTransformPoint(text.transform.TransformPoint(text.textBounds.center));
            return new Vector2((1f - factor) * baseScale.x * center.x, (1f - factor) * baseScale.y * center.y);
        }

        /// <summary>
        /// Sets the target's scale and moves it by the change in compensation offset since the last call,
        /// so position changes made by layout or other code in between are preserved.
        /// </summary>
        public static void Apply(RectTransform target, TMP_Text text, Vector3 baseScale, float factor)
        {
            if (target == null) return;
            Vector2 offset = ComputeOffset(target, text, baseScale, factor);
            Vector2 previous;
            AppliedOffsets.TryGetValue(target, out previous);
            target.localScale = baseScale * factor;
            target.anchoredPosition += offset - previous;
            if (offset == Vector2.zero) AppliedOffsets.Remove(target);
            else AppliedOffsets[target] = offset;
        }

        /// <summary>Restores the rest scale and removes any compensation offset still applied.</summary>
        public static void Reset(RectTransform target, Vector3 baseScale)
        {
            if (target == null) return;
            Vector2 previous;
            if (AppliedOffsets.TryGetValue(target, out previous))
            {
                target.anchoredPosition -= previous;
                AppliedOffsets.Remove(target);
            }
            target.localScale = baseScale;
        }
    }
}
