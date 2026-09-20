using System;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// State and decisions for the practice A-B loop. Timings are audio timings in ms.
    /// Holds no references to Unity or services so it can be tested directly.
    /// </summary>
    public class PracticeLoop
    {
        public const int MinLengthMs = 1000;

        /// <summary>
        /// Lead-in used when the bar length at <see cref="From"/> is unknown.
        /// </summary>
        public const int FallbackLeadInMs = 2000;

        private int? lastTiming;

        public int AudioLength { get; private set; }

        public int From { get; private set; }

        public int To { get; private set; }

        public bool Enabled { get; set; }

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
            ResetTracking();
        }

        /// <summary>
        /// Move the start without ever crossing the end, for dragging a handle.
        /// </summary>
        public void MoveFrom(int timing)
        {
            int minLength = Math.Min(MinLengthMs, AudioLength);
            SetRange(Clamp(timing, 0, To - minLength), To);
        }

        /// <summary>
        /// Move the end without ever crossing the start, for dragging a handle.
        /// </summary>
        public void MoveTo(int timing)
        {
            int minLength = Math.Min(MinLengthMs, AudioLength);
            SetRange(From, Clamp(timing, From + minLength, AudioLength));
        }

        /// <summary>
        /// Real-time delay in ms to pass to PlayWithDelay when restarting, so that one bar of
        /// lead-in scrolls past before the audio reaches <see cref="From"/>.
        /// </summary>
        /// <param name="barLengthMs">Length of one bar at <see cref="From"/> in chart ms, or 0 if unknown.</param>
        /// <param name="playbackSpeed">Current playback speed.</param>
        public int RestartDelayMs(double barLengthMs, float playbackSpeed)
        {
            playbackSpeed = playbackSpeed <= 0 ? 1 : playbackSpeed;

            if (barLengthMs <= 0)
            {
                return FallbackLeadInMs;
            }

            int delay = (int)Math.Ceiling(barLengthMs / playbackSpeed);
            return Math.Max(delay, Values.DelayBeforeAudioResume);
        }

        /// <summary>
        /// Forget the last observed timing, so the next <see cref="ShouldRestart"/> call only observes.
        /// </summary>
        public void ResetTracking()
        {
            lastTiming = null;
        }

        /// <summary>
        /// Whether playback should jump back to <see cref="From"/>. Call once per frame, paused or not.
        /// True only when the loop is on, audio is playing, and the timing moved from at or before
        /// <see cref="To"/> to past it since the last call. Seeking anywhere never restarts by itself,
        /// and playback that starts before <see cref="From"/> plays into the range and loops at the end.
        /// </summary>
        public bool ShouldRestart(int timing, bool isPlaying)
        {
            bool crossedEnd = Enabled && isPlaying && lastTiming.HasValue
                && lastTiming.Value <= To && timing > To;
            lastTiming = timing;
            return crossedEnd;
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
