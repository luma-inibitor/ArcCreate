using System;
using System.Collections.Generic;

namespace ArcCreate.Gameplay
{
    /// <summary>
    /// A single judgement point of a hold or an arc.
    /// </summary>
    public struct JudgePoint
    {
        /// <summary>
        /// The chart timing of the point, in milliseconds.
        /// </summary>
        public int Timing;

        /// <summary>
        /// The number of combos this point gives. It is 2 only for the point that absorbed the removed last point of an arc.
        /// </summary>
        public int Weight;
    }

    /// <summary>
    /// Calculates the judgement points of holds and arcs in the same order of single precision operations as the official client
    /// (checked against the native code of Arcaea 7.0.255: interval at 0x17cb97c, points at 0xd92558, arc merge at 0xba81e8).
    /// Every operation is stored in a <see cref="float"/> so that the result never depends on the intermediate precision of the runtime.
    /// </summary>
    public static class JudgePointCalculator
    {
        /// <summary>
        /// Calculate the interval between two judgement points, in milliseconds.
        /// A BPM of 0 gives an infinite interval, like the official client.
        /// </summary>
        /// <param name="bpm">The BPM at the start of the note. The sign is ignored.</param>
        /// <param name="density">The TimingPointDensityFactor of the chart.</param>
        /// <returns>The interval.</returns>
        public static float CalculateInterval(float bpm, float density)
        {
            float absBpm = Math.Abs(bpm);
            float factor = absBpm < 255f ? 2f : 1f;
            float perBeat = (float)(60000f / absBpm);
            float perPoint = (float)(perBeat / factor);
            return (float)(perPoint / density);
        }

        /// <summary>
        /// Calculate the judgement points of a hold or of an arc that is not a trace.
        /// </summary>
        /// <param name="startTiming">The start timing of the note.</param>
        /// <param name="endTiming">The end timing of the note.</param>
        /// <param name="interval">The interval from <see cref="CalculateInterval"/>.</param>
        /// <param name="includeStart">True for an arc that follows a connected arc. Its first point is at its start.</param>
        /// <param name="hasNextArc">True for an arc that has a connected arc after it. Enables the merge of the last two points.</param>
        /// <returns>The points, ordered by timing. Empty if the note has no length.</returns>
        public static List<JudgePoint> Calculate(int startTiming, int endTiming, float interval, bool includeStart, bool hasNextArc)
        {
            List<JudgePoint> points = new List<JudgePoint>();
            int duration = endTiming - startTiming;
            if (duration == 0)
            {
                return points;
            }

            int total = (int)((float)((float)duration / interval));
            int firstIndex = includeStart ? 0 : 1;
            for (int index = firstIndex; index < total; index++)
            {
                float offset = (float)(interval * (float)index);
                int timing = (int)(float)(offset + (float)startTiming);
                if (timing < endTiming)
                {
                    points.Add(new JudgePoint() { Timing = timing, Weight = 1 });
                }
            }

            if (points.Count == 0)
            {
                // A note too short to hold a regular point has a single point in the middle.
                float half = (float)((float)duration * 0.5f);
                int timing = (int)(float)(half + (float)startTiming);
                points.Add(new JudgePoint() { Timing = timing, Weight = 1 });
            }

            if (hasNextArc && points.Count > 1)
            {
                int shorterTotal = (int)((float)((float)(duration - 2) / interval));
                if (total - 1 == shorterTotal)
                {
                    // The last point is dropped and the point before it counts twice, so the total combo does not change.
                    points.RemoveAt(points.Count - 1);
                    JudgePoint last = points[points.Count - 1];
                    last.Weight = 2;
                    points[points.Count - 1] = last;
                }
            }

            return points;
        }

        /// <summary>
        /// Sum of the weights of the points.
        /// </summary>
        /// <param name="points">The points.</param>
        /// <returns>The total combo.</returns>
        public static int TotalCombo(List<JudgePoint> points)
        {
            int combo = 0;
            for (int i = 0; i < points.Count; i++)
            {
                combo += points[i].Weight;
            }

            return combo;
        }
    }
}
