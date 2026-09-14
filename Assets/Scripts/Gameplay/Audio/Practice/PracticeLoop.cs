using System;

namespace ArcCreate.Gameplay.Audio.Practice
{
    public enum LeadInMode
    {
        None,
        OneBar,
        TwoBars,
        TwoSeconds,
    }

    /// <summary>
    /// State and decisions for the practice A-B loop. Timings are audio timings in ms.
    /// Holds no references to Unity or services so it can be tested directly.
    /// </summary>
    public class PracticeLoop
    {
        public const int MinLengthMs = 1000;

        /// <summary>
        /// Slack applied below the lead-in start before a restart is triggered,
        /// so rounding in the audio timing never causes a restart storm.
        /// </summary>
        public const int RestartToleranceMs = 10;

        private int prerollChartMs = 0;

        public event Action OnRangeChange;

        public int AudioLength { get; private set; }

        public int From { get; private set; }

        public int To { get; private set; }

        public bool Enabled { get; set; }

        public LeadInMode LeadIn { get; set; } = LeadInMode.OneBar;

        /// <summary>
        /// Gets the number of restarts since the range was last set.
        /// </summary>
        public int LoopCount { get; private set; }

        public int LengthMs => To - From;

        /// <summary>
        /// Set the audio length. Resets the range to the whole audio.
        /// </summary>
        public void SetAudioLength(int lengthMs)
        {
            AudioLength = Math.Max(0, lengthMs);
            SetRange(0, AudioLength);
        }

        public void SetFrom(int timing) => SetRange(timing, To);

        public void SetTo(int timing) => SetRange(From, timing);

        /// <summary>
        /// Set the loop range. Endpoints are ordered and clamped to the audio,
        /// and the range is kept at least <see cref="MinLengthMs"/> long where the audio allows.
        /// </summary>
        public void SetRange(int from, int to)
        {
            if (from > to)
            {
                (from, to) = (to, from);
            }

            int minLength = Math.Min(MinLengthMs, AudioLength);
            from = Clamp(from, 0, AudioLength - minLength);
            to = Clamp(to, from + minLength, AudioLength);

            From = from;
            To = to;
            LoopCount = 0;
            prerollChartMs = 0;
            OnRangeChange?.Invoke();
        }

        public bool Contains(int timing) => timing >= From && timing <= To;

        /// <summary>
        /// Real-time delay in ms to pass to PlayWithDelay when restarting, so that the
        /// configured lead-in scrolls past before the audio reaches <see cref="From"/>.
        /// </summary>
        /// <param name="barLengthMs">Length of one bar at <see cref="From"/> in chart ms, or 0 if unknown.</param>
        /// <param name="playbackSpeed">Current playback speed.</param>
        public int RestartDelayMs(double barLengthMs, float playbackSpeed)
        {
            playbackSpeed = playbackSpeed <= 0 ? 1 : playbackSpeed;
            double bars;
            switch (LeadIn)
            {
                case LeadInMode.None:
                    return Values.DelayBeforeAudioResume;
                case LeadInMode.OneBar:
                    bars = 1;
                    break;
                case LeadInMode.TwoBars:
                    bars = 2;
                    break;
                default:
                    return 2000;
            }

            if (barLengthMs <= 0)
            {
                return 2000;
            }

            int delay = (int)Math.Ceiling(bars * barLengthMs / playbackSpeed);
            return Math.Max(delay, Values.DelayBeforeAudioResume);
        }

        /// <summary>
        /// Record that a restart was issued with the given real-time delay and speed.
        /// </summary>
        public void MarkRestarted(int delayMs, float playbackSpeed)
        {
            prerollChartMs = (int)Math.Round(delayMs * playbackSpeed);
            LoopCount++;
        }

        /// <summary>
        /// Whether playback should jump back to <see cref="From"/>.
        /// True when the loop is on, audio is playing, and the timing has left the range
        /// (allowing for the lead-in of the most recent restart).
        /// </summary>
        public bool ShouldRestart(int timing, bool isPlaying)
        {
            if (!Enabled || !isPlaying)
            {
                return false;
            }

            return timing > To || timing < From - prerollChartMs - RestartToleranceMs;
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
