using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Fullscreen UI overlay that blocks raycasts everywhere EXCEPT inside defined cutout rectangles.
    /// Allows targeted interaction with 3D board columns or specific UI buttons while blocking the rest of the screen.
    /// </summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public class TutorialMaskCutout : Graphic, ICanvasRaycastFilter
    {
        [Tooltip("Color of the dimming mask outside the cutouts.")]
        [SerializeField] private Color maskColor = new Color(0f, 0f, 0f, 0.5f);

        private readonly List<Rect> cutoutScreenRects = new List<Rect>();

        protected override void Awake()
        {
            base.Awake();
            color = maskColor;
            raycastTarget = true;
        }

        public void SetMaskColor(Color newColor)
        {
            maskColor = newColor;
            color = newColor;
            SetVerticesDirty();
        }

        public void ClearCutouts()
        {
            cutoutScreenRects.Clear();
            SetVerticesDirty();
        }

        public void SetCutout(Rect screenRect)
        {
            cutoutScreenRects.Clear();
            if (screenRect.width > 0 && screenRect.height > 0)
            {
                cutoutScreenRects.Add(screenRect);
            }
            SetVerticesDirty();
        }

        public void AddCutout(Rect screenRect)
        {
            if (screenRect.width > 0 && screenRect.height > 0)
            {
                cutoutScreenRects.Add(screenRect);
                SetVerticesDirty();
            }
        }

        /// <summary>
        /// Generates mesh geometry with rectangular holes cut out for interactive areas.
        /// </summary>
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();

            RectTransform rt = rectTransform;
            Rect screenBounds = rt.rect;

            if (cutoutScreenRects.Count == 0 || maskColor.a <= 0.001f)
            {
                // Full rectangle or transparent
                if (maskColor.a > 0.001f)
                {
                    AddQuad(vh, screenBounds.xMin, screenBounds.yMin, screenBounds.xMax, screenBounds.yMax, maskColor);
                }
                return;
            }

            // Convert first cutout to local Rect coordinates inside this RectTransform
            Rect cutoutScreen = cutoutScreenRects[0];
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, new Vector2(cutoutScreen.xMin, cutoutScreen.yMin), cam, out Vector2 localMin);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rt, new Vector2(cutoutScreen.xMax, cutoutScreen.yMax), cam, out Vector2 localMax);

            float holeXMin = Mathf.Max(screenBounds.xMin, Mathf.Min(localMin.x, localMax.x));
            float holeXMax = Mathf.Min(screenBounds.xMax, Mathf.Max(localMin.x, localMax.x));
            float holeYMin = Mathf.Max(screenBounds.yMin, Mathf.Min(localMin.y, localMax.y));
            float holeYMax = Mathf.Min(screenBounds.yMax, Mathf.Max(localMin.y, localMax.y));

            // 4 surrounding quads around the hole (Top, Bottom, Left, Right)
            // Top quad
            if (screenBounds.yMax > holeYMax)
                AddQuad(vh, screenBounds.xMin, holeYMax, screenBounds.xMax, screenBounds.yMax, maskColor);

            // Bottom quad
            if (holeYMin > screenBounds.yMin)
                AddQuad(vh, screenBounds.xMin, screenBounds.yMin, screenBounds.xMax, holeYMin, maskColor);

            // Left quad
            if (holeXMin > screenBounds.xMin)
                AddQuad(vh, screenBounds.xMin, holeYMin, holeXMin, holeYMax, maskColor);

            // Right quad
            if (screenBounds.xMax > holeXMax)
                AddQuad(vh, holeXMax, holeYMin, screenBounds.xMax, holeYMax, maskColor);
        }

        private static void AddQuad(VertexHelper vh, float xMin, float yMin, float xMax, float yMax, Color c)
        {
            int startIndex = vh.currentVertCount;

            UIVertex vert = UIVertex.simpleVert;
            vert.color = c;

            vert.position = new Vector3(xMin, yMin);
            vh.AddVert(vert);

            vert.position = new Vector3(xMin, yMax);
            vh.AddVert(vert);

            vert.position = new Vector3(xMax, yMax);
            vh.AddVert(vert);

            vert.position = new Vector3(xMax, yMin);
            vh.AddVert(vert);

            vh.AddTriangle(startIndex, startIndex + 1, startIndex + 2);
            vh.AddTriangle(startIndex + 2, startIndex + 3, startIndex);
        }

        /// <summary>
        /// Checks if a screen raycast should pass through. Returns FALSE inside cutouts to allow clicking behind the mask.
        /// </summary>
        public bool IsRaycastLocationValid(Vector2 sp, Camera eventCamera)
        {
            // If inside any cutout rect, let raycast pass through to the game/UI underneath
            foreach (Rect cutout in cutoutScreenRects)
            {
                if (cutout.Contains(sp))
                {
                    return false; // Hole: does not block
                }
            }

            // Outside cutouts: block the raycast
            return true;
        }
    }
}
