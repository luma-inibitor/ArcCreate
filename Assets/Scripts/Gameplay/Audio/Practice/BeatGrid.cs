using System;
using System.Collections.Generic;
using System.Linq;
using ArcCreate.Gameplay.Data;
using ArcCreate.Utility.Extension;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Bar and beat positions derived from a timing group's timing events.
    /// All values are in chart timing (ms). The rules are:
    /// <list type="bullet">
    /// <item>Every timing event starts a new bar, and no line is drawn past the next event.</item>
    /// <item>Chart timings and drawn lines are whole milliseconds, so a timing within 0.5 ms of a line
    /// counts as on it. The exact boundary belongs to the later line.</item>
    /// <item>A segment with an unusable tempo (bpm 0, |bpm| above <see cref="Values.MaxBeatlineBpm"/>, or a
    /// non-finite value) has no grid at all, like the game's beatlines. A divisor below 1 only drops its bar
    /// lines, its beat lines are still drawn.</item>
    /// <item>The first segment's grid extends backwards before the first timing event, as the game draws
    /// beatlines before timing 0, so bar indices there are negative.</item>
    /// <item>Two events sharing a timing: the later one in chart order wins.</item>
    /// </list>
    /// </summary>
    public class BeatGrid
    {
        /// <summary>
        /// Drawn timings are whole milliseconds, so a timing this close to a line counts as on it.
        /// </summary>
        private const double Tol = 0.5;

        private readonly List<TimingEvent> timings;

        public BeatGrid(IEnumerable<TimingEvent> timings)
        {
            // OrderBy is stable, so events sharing a timing keep chart order and the later one wins.
            this.timings = timings.OrderBy(t => t.Timing).ToList();
        }

        public enum Rounding
        {
            Nearest,
            Down,
            Up,
        }

        private bool IsEmpty => timings.Count == 0;

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
        /// Snap a timing to a bar line: the nearest one, the one at or before it, or the one at or after it.
        /// Returns the timing unchanged if the grid has no usable tempo at that point. Every timing event
        /// starts a new bar, so the line above a timing is capped at the next event.
        /// </summary>
        public int SnapToBar(int timing, Rounding rounding = Rounding.Nearest)
        {
            if (IsEmpty)
            {
                return timing;
            }

            int index = SegmentIndexAt(timing);
            TimingEvent ev = timings[index];
            double bar = BarLengthOf(ev);
            if (bar <= 0)
            {
                return timing;
            }

            double segmentEnd = index + 1 < timings.Count ? timings[index + 1].Timing : double.PositiveInfinity;
            double offset = timing - (double)ev.Timing;
            double lower = ev.Timing + (LineIndexAtOrBefore(offset, bar) * bar);
            double candidate;
            switch (rounding)
            {
                case Rounding.Down:
                    candidate = lower;
                    break;
                case Rounding.Up:
                    // At or after, so a timing already on a line stays put.
                    candidate = Math.Min(ev.Timing + (LineIndexAtOrAfter(offset, bar) * bar), segmentEnd);
                    break;
                default:
                    double above = Math.Min(lower + bar, segmentEnd);
                    candidate = timing - lower < above - timing ? lower : above;
                    break;
            }

            return RoundToWholeMs(candidate);
        }

        /// <summary>
        /// Zero-based index of the bar containing the timing, counted from the first timing event.
        /// Negative before the first event, whose grid extends backwards.
        /// </summary>
        public int BarIndexAt(int timing)
        {
            if (IsEmpty)
            {
                return 0;
            }

            double bars = 0;
            for (int i = 0; i < timings.Count; i++)
            {
                TimingEvent ev = timings[i];
                double bar = BarLengthOf(ev);
                bool last = i + 1 >= timings.Count;
                double segmentEnd = last ? double.PositiveInfinity : timings[i + 1].Timing;

                // The first segment also owns everything before it, since its grid extends backwards.
                if (timing < segmentEnd)
                {
                    if (bar > 0)
                    {
                        bars += LineIndexAtOrBefore(timing - (double)ev.Timing, bar);
                    }

                    break;
                }

                if (bar > 0)
                {
                    // Only the lines strictly before the next event: one landing on it is that event's own bar.
                    bars += LineIndexAtOrAfter(segmentEnd - ev.Timing, bar);
                }
            }

            return ClampToInt(bars);
        }

        /// <summary>
        /// Beat lines in [from, to], in order, flagged when they start a bar.
        /// Every timing event starts a new bar, and a line landing on the next event is left to that event.
        /// Segments without a usable tempo produce no lines; the first segment's lines extend backwards.
        /// </summary>
        public IEnumerable<(int timing, bool isBar)> LinesBetween(int from, int to)
        {
            if (IsEmpty || to < from)
            {
                yield break;
            }

            int startIndex = SegmentIndexAt(from);
            for (int index = startIndex; index < timings.Count; index++)
            {
                TimingEvent ev = timings[index];

                // The window's own segment always gets a look, since the first one extends backwards.
                if (index > startIndex && ev.Timing > to)
                {
                    yield break;
                }

                double beat = BeatLengthOf(ev);
                if (beat <= 0)
                {
                    continue;
                }

                double bar = BarLengthOf(ev);
                double segmentEnd = index + 1 < timings.Count ? timings[index + 1].Timing : double.PositiveInfinity;
                double startAt = index == 0 ? from : Math.Max(from, ev.Timing);
                double k = LineIndexAtOrAfter(startAt - (double)ev.Timing, beat);

                while (true)
                {
                    double offset = k * beat;
                    double t = ev.Timing + offset;
                    if (t > to || t >= segmentEnd - Tol)
                    {
                        break;
                    }

                    yield return (RoundToWholeMs(t), IsBarOffset(offset, bar));
                    k++;
                }
            }
        }

        /// <summary>
        /// Index of the last line at or before an offset from the segment start, in steps of
        /// <paramref name="step"/> ms. A line up to 0.5 ms above the offset still counts as at it, but a
        /// line exactly 0.5 ms above does not: it is drawn at the next whole millisecond.
        /// </summary>
        private static double LineIndexAtOrBefore(double offset, double step)
            => Math.Ceiling((offset + Tol) / step) - 1;

        /// <summary>
        /// Index of the first line at or after an offset from the segment start. The mirror of
        /// <see cref="LineIndexAtOrBefore"/>: a line exactly 0.5 ms below the offset is still at it.
        /// </summary>
        private static double LineIndexAtOrAfter(double offset, double step)
            => Math.Ceiling((offset - Tol) / step);

        private static bool IsBarOffset(double offset, double bar)
        {
            if (bar <= 0)
            {
                return false;
            }

            // The divisor can be fractional, so bar lines need not fall on a whole number of beats.
            double m = Math.Round(offset / bar);
            return Math.Abs(offset - (m * bar)) < Tol;
        }

        private static int RoundToWholeMs(double t)
        {
            // Half up, matching the tolerance rule: a line 0.5 ms above a whole ms is drawn at the next one.
            return ClampToInt(Math.Floor(t + Tol));
        }

        private static int ClampToInt(double value)
        {
            if (value >= int.MaxValue)
            {
                return int.MaxValue;
            }

            if (value <= int.MinValue)
            {
                return int.MinValue;
            }

            return (int)value;
        }

        private static double BeatLengthOf(TimingEvent ev)
        {
            float bpm = ev.Bpm;
            if (bpm == 0 || float.IsNaN(bpm) || float.IsInfinity(bpm) || Math.Abs(bpm) > Values.MaxBeatlineBpm)
            {
                return 0;
            }

            return 60000.0 / Math.Abs(bpm);
        }

        private static double BarLengthOf(TimingEvent ev)
        {
            double beat = BeatLengthOf(ev);
            float divisor = ev.Divisor;
            if (beat <= 0 || float.IsNaN(divisor) || float.IsInfinity(divisor) || divisor < 1)
            {
                return 0;
            }

            return beat * divisor;
        }

        private int SegmentIndexAt(int timing)
        {
            int index = timings.BisectRight(timing, ev => ev.Timing) - 1;
            return Math.Max(index, 0);
        }
    }
}
