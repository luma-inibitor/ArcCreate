using System;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// A visible span of time inside fixed bounds, mapped to a 0..1 horizontal axis.
    /// </summary>
    public class TimelineWindow
    {
        public const int MinLengthMs = 100;

        public int Min { get; private set; }

        public int Max { get; private set; }

        public int From { get; private set; }

        public int Length { get; private set; }

        public int To => From + Length;

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

        public void CenterOn(int timing) => CenterOn(timing, Length);

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

        public int Denormalize(float t)
        {
            return From + (int)Math.Round(t * Length);
        }

        private int ClampLength(int length)
        {
            int span = Max - Min;
            return Clamp(length, Math.Min(MinLengthMs, span), span);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (max < min)
            {
                max = min;
            }

            return value < min ? min : (value > max ? max : value);
        }
    }
}
