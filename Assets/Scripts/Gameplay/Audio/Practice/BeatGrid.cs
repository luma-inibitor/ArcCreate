using System;
using System.Collections.Generic;
using ArcCreate.Gameplay.Data;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Bar and beat positions derived from a timing group's timing events.
    /// All values are in chart timing (ms).
    /// </summary>
    public class BeatGrid
    {
        private readonly List<TimingEvent> timings;

        public BeatGrid(IEnumerable<TimingEvent> timings)
        {
            this.timings = new List<TimingEvent>(timings);
            this.timings.Sort((a, b) => a.Timing.CompareTo(b.Timing));
        }

        public enum Unit
        {
            Bar,
            Beat,
        }

        public bool IsEmpty => timings.Count == 0;

        /// <summary>
        /// Length in ms of one bar at the given timing, or 0 if the tempo or divisor is not usable.
        /// </summary>
        public double BarLengthAt(int timing)
        {
            if (IsEmpty)
            {
                return 0;
            }

            return BarLengthOf(timings[SegmentIndexAt(timing)]);
        }

        /// <summary>
        /// Length in ms of one beat at the given timing, or 0 if the tempo is not usable.
        /// </summary>
        public double BeatLengthAt(int timing)
        {
            if (IsEmpty)
            {
                return 0;
            }

            return BeatLengthOf(timings[SegmentIndexAt(timing)]);
        }

        /// <summary>
        /// Snap a timing to the nearest bar line.
        /// </summary>
        public int SnapToBar(int timing) => Snap(timing, Unit.Bar);

        /// <summary>
        /// Snap a timing to the nearest beat.
        /// </summary>
        public int SnapToBeat(int timing) => Snap(timing, Unit.Beat);

        /// <summary>
        /// Snap a timing to the nearest bar line or beat. Returns the timing unchanged if the grid
        /// has no usable tempo at that point. Every timing event starts a new bar.
        /// </summary>
        public int Snap(int timing, Unit unit)
        {
            if (IsEmpty)
            {
                return timing;
            }

            int index = SegmentIndexAt(timing);
            TimingEvent ev = timings[index];
            double step = unit == Unit.Bar ? BarLengthOf(ev) : BeatLengthOf(ev);
            if (step <= 0)
            {
                return timing;
            }

            double segmentStart = ev.Timing;
            double segmentEnd = index + 1 < timings.Count ? timings[index + 1].Timing : double.PositiveInfinity;

            // Nearest of the line at or below the timing and the line above it. The next timing
            // event always starts a new bar, so the line above is capped there.
            double lower = segmentStart + (Math.Floor((timing - segmentStart) / step) * step);
            double upper = Math.Min(lower + step, segmentEnd);
            double candidate = timing - lower < upper - timing ? lower : upper;
            return (int)Math.Round(candidate);
        }

        /// <summary>
        /// Zero-based index of the bar containing the timing, counted from the first timing event.
        /// </summary>
        public int BarIndexAt(int timing)
        {
            if (IsEmpty)
            {
                return 0;
            }

            int bars = 0;
            for (int i = 0; i < timings.Count; i++)
            {
                TimingEvent ev = timings[i];
                double bar = BarLengthOf(ev);
                bool last = i + 1 >= timings.Count;
                int segmentEnd = last ? int.MaxValue : timings[i + 1].Timing;

                if (timing < segmentEnd || last)
                {
                    if (bar > 0 && timing >= ev.Timing)
                    {
                        bars += (int)Math.Floor((timing - ev.Timing) / bar);
                    }

                    return bars;
                }

                if (bar > 0)
                {
                    bars += (int)Math.Ceiling((segmentEnd - ev.Timing) / bar);
                }
            }

            return bars;
        }

        /// <summary>
        /// Beat lines in [from, to], in order, flagged when they start a bar.
        /// Every timing event starts a new bar. Segments without a usable tempo produce no lines.
        /// </summary>
        public IEnumerable<(int timing, bool isBar)> LinesBetween(int from, int to)
        {
            if (IsEmpty || to < from)
            {
                yield break;
            }

            for (int index = SegmentIndexAt(from); index < timings.Count; index++)
            {
                TimingEvent ev = timings[index];
                if (ev.Timing > to)
                {
                    yield break;
                }

                double beat = BeatLengthOf(ev);
                if (beat <= 0)
                {
                    continue;
                }

                double segmentEnd = index + 1 < timings.Count ? timings[index + 1].Timing : double.PositiveInfinity;
                int beatsPerBar = ev.Divisor >= 1 ? (int)Math.Round(ev.Divisor) : 0;
                double startAt = Math.Max(from, ev.Timing);
                long k = (long)Math.Ceiling(((startAt - ev.Timing) / beat) - 1e-9);

                while (true)
                {
                    double t = ev.Timing + (k * beat);
                    if (t > to || t >= segmentEnd)
                    {
                        break;
                    }

                    yield return ((int)Math.Round(t), beatsPerBar > 0 && k % beatsPerBar == 0);
                    k++;
                }
            }
        }

        private static double BeatLengthOf(TimingEvent ev)
        {
            return ev.Bpm == 0 ? 0 : 60000.0 / Math.Abs(ev.Bpm);
        }

        private static double BarLengthOf(TimingEvent ev)
        {
            double beat = BeatLengthOf(ev);
            return ev.Divisor <= 0 ? 0 : beat * ev.Divisor;
        }

        private int SegmentIndexAt(int timing)
        {
            int index = 0;
            while (index + 1 < timings.Count && timings[index + 1].Timing <= timing)
            {
                index++;
            }

            return index;
        }
    }
}
