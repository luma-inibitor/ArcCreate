using ArcCreate.Gameplay.Chart;
using ArcCreate.Gameplay.Data;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Flat, sideways chart preview. Time runs left to right. The sky band on top shows arcs,
    /// traces and arctaps; the floor lanes below show taps and holds, lane 1 at the top.
    /// Notes in timing groups with no input are skipped.
    /// </summary>
    public class ChartStripGraphic : MaskableGraphic
    {
        private const int MaxGridLines = 512;
        private const int MaxArcSegments = 64;

        [Header("Layout")]
        [SerializeField] [Range(0.1f, 0.9f)] private float skyFraction = 0.45f;
        [SerializeField] private float bandGap = 4f;
        [SerializeField] private float tapWidth = 4f;
        [SerializeField] private float arcThickness = 3f;
        [SerializeField] private float traceThickness = 1.5f;
        [SerializeField] private float arcTapSize = 5f;
        [SerializeField] private float arcSegmentLength = 6f;

        [Header("Colors")]
        [SerializeField] private Color skyColor = new Color(1, 1, 1, 0.06f);
        [SerializeField] private Color laneColorA = new Color(1, 1, 1, 0.10f);
        [SerializeField] private Color laneColorB = new Color(1, 1, 1, 0.05f);
        [SerializeField] private Color barLineColor = new Color(1, 1, 1, 0.35f);
        [SerializeField] private Color beatLineColor = new Color(1, 1, 1, 0.12f);
        [SerializeField] private Color tapColor = Color.white;
        [SerializeField] private Color holdColor = new Color(1, 1, 1, 0.55f);
        [SerializeField] private Color traceColor = new Color(0.75f, 0.75f, 0.75f, 0.8f);
        [SerializeField] private Color arcTapColor = Color.white;

        private readonly UIVertex[] quad = new UIVertex[4];
        private int fromTiming;
        private int toTiming;
        private BeatGrid grid;

        private Rect drawRect;
        private float skyTopY;
        private float skyBottomY;
        private float floorTopY;
        private float laneHeight;
        private float laneFrom;
        private int laneCount;

        /// <summary>
        /// Set the chart timing window to draw. Rebuilds only when something changed.
        /// </summary>
        public void SetWindow(int fromChartTiming, int toChartTiming, BeatGrid beatGrid)
        {
            if (fromChartTiming == fromTiming && toChartTiming == toTiming && ReferenceEquals(beatGrid, grid))
            {
                return;
            }

            fromTiming = fromChartTiming;
            toTiming = toChartTiming;
            grid = beatGrid;
            SetVerticesDirty();
        }

        /// <summary>
        /// Force a rebuild, for example after the chart was edited.
        /// </summary>
        public void Refresh()
        {
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            drawRect = rectTransform.rect;
            if (toTiming <= fromTiming || drawRect.width <= 0 || drawRect.height <= 0)
            {
                return;
            }

            LayOut();
            DrawBackground(vh);
            DrawGrid(vh);

            if (Services.Chart == null)
            {
                return;
            }

            foreach (Hold hold in Services.Chart.FindEventsWithinRange<Hold>(fromTiming, toTiming, false))
            {
                if (!Skip(hold))
                {
                    DrawHold(vh, hold);
                }
            }

            foreach (Tap tap in Services.Chart.FindEventsWithinRange<Tap>(fromTiming, toTiming, false))
            {
                if (!Skip(tap))
                {
                    DrawTap(vh, tap);
                }
            }

            foreach (Arc arc in Services.Chart.FindEventsWithinRange<Arc>(fromTiming, toTiming, false))
            {
                if (!Skip(arc))
                {
                    DrawArc(vh, arc);
                }
            }

            foreach (ArcTap arcTap in Services.Chart.FindEventsWithinRange<ArcTap>(fromTiming, toTiming, false))
            {
                if (!Skip(arcTap))
                {
                    DrawArcTap(vh, arcTap);
                }
            }
        }

        private static bool Skip(Note note)
        {
            TimingGroup group = Services.Chart.GetTimingGroup(note.TimingGroup);
            return group == null || group.GroupProperties.NoInput;
        }

        private static Color ArcColor(int colorId)
        {
            if (Services.Skin == null)
            {
                return Color.white;
            }

            Color color = Services.Skin.GetArcColor(colorId).high;
            color.a = 1;
            return color;
        }

        private void LayOut()
        {
            skyTopY = drawRect.yMax;
            skyBottomY = drawRect.yMax - (drawRect.height * skyFraction);
            floorTopY = skyBottomY - bandGap;

            laneFrom = Mathf.Min(Values.LaneFrom, 1);
            float laneTo = Mathf.Max(Values.LaneTo, 4);
            laneCount = Mathf.Max(1, Mathf.RoundToInt(laneTo - laneFrom) + 1);
            laneHeight = Mathf.Max(0, floorTopY - drawRect.yMin) / laneCount;
        }

        private void DrawBackground(VertexHelper vh)
        {
            AddRect(vh, drawRect.xMin, skyBottomY, drawRect.xMax, skyTopY, skyColor);
            for (int i = 0; i < laneCount; i++)
            {
                float top = floorTopY - (i * laneHeight);
                AddRect(vh, drawRect.xMin, top - laneHeight, drawRect.xMax, top, i % 2 == 0 ? laneColorA : laneColorB);
            }
        }

        private void DrawGrid(VertexHelper vh)
        {
            if (grid == null)
            {
                return;
            }

            int count = 0;
            foreach ((int timing, bool isBar) in grid.LinesBetween(fromTiming, toTiming))
            {
                if (++count > MaxGridLines)
                {
                    break;
                }

                float x = XAt(timing);
                float half = isBar ? 0.75f : 0.5f;
                AddRect(vh, x - half, drawRect.yMin, x + half, drawRect.yMax, isBar ? barLineColor : beatLineColor);
            }
        }

        private void DrawTap(VertexHelper vh, Tap tap)
        {
            float x = XAt(tap.Timing);
            float y = LaneY(tap.Lane);
            float halfHeight = laneHeight * 0.35f;
            AddRect(vh, x - (tapWidth / 2), y - halfHeight, x + (tapWidth / 2), y + halfHeight, tapColor);
        }

        private void DrawHold(VertexHelper vh, Hold hold)
        {
            float x0 = Mathf.Max(XAt(hold.Timing), drawRect.xMin);
            float x1 = Mathf.Min(XAt(hold.EndTiming), drawRect.xMax);
            if (x1 <= x0)
            {
                return;
            }

            float y = LaneY(hold.Lane);
            float halfHeight = laneHeight * 0.25f;
            AddRect(vh, x0, y - halfHeight, x1, y + halfHeight, holdColor);
        }

        private void DrawArc(VertexHelper vh, Arc arc)
        {
            int duration = arc.EndTiming - arc.Timing;
            int start = Mathf.Max(arc.Timing, fromTiming);
            int end = Mathf.Min(arc.EndTiming, toTiming);
            if (duration <= 0 || end <= start)
            {
                return;
            }

            Color color = arc.IsTrace ? traceColor : ArcColor(arc.Color);
            float thickness = arc.IsTrace ? traceThickness : arcThickness;
            float pixelLength = XAt(end) - XAt(start);
            int segments = Mathf.Clamp(Mathf.CeilToInt(pixelLength / Mathf.Max(1, arcSegmentLength)), 1, MaxArcSegments);

            Vector2 previous = ArcPoint(arc, start, duration);
            for (int i = 1; i <= segments; i++)
            {
                int timing = start + (int)((long)(end - start) * i / segments);
                Vector2 next = ArcPoint(arc, timing, duration);
                AddSegment(vh, previous, next, thickness, color);
                previous = next;
            }
        }

        private void DrawArcTap(VertexHelper vh, ArcTap arcTap)
        {
            Arc arc = arcTap.Arc;
            if (arc == null)
            {
                return;
            }

            int duration = arc.EndTiming - arc.Timing;
            float arcX = duration > 0
                ? ArcFormula.X(arc.XStart, arc.XEnd, (arcTap.Timing - arc.Timing) / (float)duration, arc.LineType)
                : arc.XStart;
            AddDiamond(vh, new Vector2(XAt(arcTap.Timing), SkyY(arcX)), arcTapSize, arcTapColor);
        }

        private Vector2 ArcPoint(Arc arc, int timing, int duration)
        {
            float t = (timing - arc.Timing) / (float)duration;
            return new Vector2(XAt(timing), SkyY(ArcFormula.X(arc.XStart, arc.XEnd, t, arc.LineType)));
        }

        private float XAt(int timing)
        {
            return drawRect.xMin + ((timing - fromTiming) / (float)(toTiming - fromTiming) * drawRect.width);
        }

        private float LaneY(float lane)
        {
            float y = floorTopY - (((lane - laneFrom) + 0.5f) * laneHeight);
            return Mathf.Clamp(y, drawRect.yMin + (laneHeight / 2), floorTopY - (laneHeight / 2));
        }

        /// <summary>
        /// Arc x of -0.5 is the far left of the track and 1.5 the far right, drawn top to bottom.
        /// </summary>
        private float SkyY(float arcX)
        {
            float n = Mathf.Clamp01((arcX + 0.5f) / 2f);
            return Mathf.Lerp(skyTopY - arcThickness, skyBottomY + arcThickness, n);
        }

        private void AddRect(VertexHelper vh, float x0, float y0, float x1, float y1, Color c)
        {
            AddQuad(vh, new Vector2(x0, y0), new Vector2(x0, y1), new Vector2(x1, y1), new Vector2(x1, y0), c);
        }

        private void AddSegment(VertexHelper vh, Vector2 a, Vector2 b, float thickness, Color c)
        {
            Vector2 direction = b - a;
            if (direction.sqrMagnitude < 1e-6f)
            {
                return;
            }

            Vector2 normal = new Vector2(-direction.y, direction.x).normalized * (thickness / 2);
            AddQuad(vh, a - normal, a + normal, b + normal, b - normal, c);
        }

        private void AddDiamond(VertexHelper vh, Vector2 center, float size, Color c)
        {
            AddQuad(
                vh,
                center + new Vector2(0, size),
                center + new Vector2(size, 0),
                center + new Vector2(0, -size),
                center + new Vector2(-size, 0),
                c);
        }

        private void AddQuad(VertexHelper vh, Vector2 a, Vector2 b, Vector2 c, Vector2 d, Color tint)
        {
            Color32 c32 = tint * color;
            SetVertex(0, a, c32);
            SetVertex(1, b, c32);
            SetVertex(2, c, c32);
            SetVertex(3, d, c32);
            vh.AddUIVertexQuad(quad);
        }

        private void SetVertex(int index, Vector2 position, Color32 c)
        {
            UIVertex vertex = UIVertex.simpleVert;
            vertex.position = position;
            vertex.color = c;
            quad[index] = vertex;
        }
    }
}
