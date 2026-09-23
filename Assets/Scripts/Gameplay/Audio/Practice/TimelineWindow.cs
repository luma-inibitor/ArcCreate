using System;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// A visible span of time inside fixed bounds, mapped to a 0..1 horizontal axis.
    /// </summary>
    public class TimelineWindow
    {
        public const int MinLengthMs = 100;

        /// <summary>
        /// Gets the lower bound the window stays inside.
        /// </summary>
        public int Min { get; private set; }

        /// <summary>
        /// Gets the upper bound the window stays inside.
        /// </summary>
        public int Max { get; private set; }

        /// <summary>
        /// Gets the timing at the left edge of the window.
        /// </summary>
        public int From { get; private set; }

        /// <summary>
        /// Gets the length of the window in ms.
        /// </summary>
        public int Length { get; private set; }

        /// <summary>
        /// Gets the timing at the right edge of the window.
        /// </summary>
        public int To => From + Length;

        /// <summary>
        /// Set the bounds. They are ordered, and the window is refitted inside them.
        /// </summary>
        public void SetBounds(int min, int max)
        {
            if (max < min)
            {
                (min, max) = (max, min);
            }

            Min = min;
            Max = max;
            Set(From, Length);
        }

        /// <summary>
        /// Set the window. Length is clamped to the bounds, then the window is shifted to fit inside them.
        /// </summary>
        public void Set(int from, int length)
        {
            length = ClampLength(length);
            From = Clamp(from, Min, Max - length);
            Length = length;
        }

        /// <summary>
        /// Centre the window on a timing, keeping its length.
        /// </summary>
        public void CenterOn(int timing) => CenterOn(timing, Length);

        /// <summary>
        /// Centre a window of the given length on a timing, shifted to fit inside the bounds.
        /// </summary>
        public void CenterOn(int timing, int length)
        {
            length = ClampLength(length);
            Set(timing - (length / 2), length);
        }

        /// <summary>
        /// Position of a timing on the axis. Values outside 0..1 are outside the window.
        /// </summary>
        public float Normalize(int timing)
        {
            return Length <= 0 ? 0 : (timing - From) / (float)Length;
        }

        /// <summary>
        /// Timing at a position on the axis, the inverse of <see cref="Normalize"/>.
        /// </summary>
        public int Denormalize(float t)
        {
            return From + (int)Math.Round(t * Length);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                max = min;
            }

            return value < min ? min : (value > max ? max : value);
        }

        private int ClampLength(int length)
        {
            int span = Max - Min;
            return Clamp(length, Math.Min(MinLengthMs, span), span);
        }
    }
}
