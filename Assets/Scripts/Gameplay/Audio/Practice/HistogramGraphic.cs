using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Draws a row of bars scaled to the tallest one, for the early/late histogram of the pause screen.
    /// One graphic instead of one image per bin.
    /// </summary>
    public class HistogramGraphic : MaskableGraphic
    {
        [SerializeField] private float gap = 1;

        private int[] counts = new int[0];

        /// <summary>
        /// Show these bin counts. Bars are drawn left to right, each scaled to the tallest bin.
        /// </summary>
        public void SetCounts(int[] binCounts)
        {
            counts = binCounts ?? new int[0];
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            int tallest = 0;
            foreach (int count in counts)
            {
                tallest = Mathf.Max(tallest, count);
            }

            if (tallest == 0)
            {
                return;
            }

            Rect rect = GetPixelAdjustedRect();
            float binWidth = rect.width / counts.Length;
            for (int i = 0; i < counts.Length; i++)
            {
                if (counts[i] == 0)
                {
                    continue;
                }

                float x0 = rect.xMin + (i * binWidth) + gap;
                float x1 = rect.xMin + ((i + 1) * binWidth) - gap;
                float y1 = rect.yMin + (rect.height * counts[i] / tallest);
                int v = vh.currentVertCount;
                vh.AddVert(new Vector3(x0, rect.yMin), color, Vector2.zero);
                vh.AddVert(new Vector3(x0, y1), color, Vector2.zero);
                vh.AddVert(new Vector3(x1, y1), color, Vector2.zero);
                vh.AddVert(new Vector3(x1, rect.yMin), color, Vector2.zero);
                vh.AddTriangle(v, v + 1, v + 2);
                vh.AddTriangle(v + 2, v + 3, v);
            }
        }
    }
}
