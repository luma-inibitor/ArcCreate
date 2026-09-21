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
        /// Lead-in used for <see cref="LeadInMode.TwoSeconds"/>, and for the bar modes when the bar
        /// length at <see cref="From"/> is unknown.
        /// </summary>
        public const int FallbackLeadInMs = 2000;

        /// <summary>
        /// How close to the end of the song counts as "at the end" for <see cref="ReachedEnd"/>.
        /// </summary>
        public const int EndToleranceMs = 100;

        private int? lastTiming;

        /// <summary>
        /// How much runs before <see cref="From"/> when the loop restarts, so notes scroll in.
        /// </summary>
        public enum LeadInMode
        {
            /// <summary>Restart right at <see cref="From"/>, with only the resume delay.</summary>
            None,

            /// <summary>One bar of the chart at <see cref="From"/>.</summary>
            OneBar,

            /// <summary>Two bars of the chart at <see cref="From"/>.</summary>
            TwoBars,

            /// <summary>Two seconds of real time, regardless of tempo.</summary>
            TwoSeconds,
        }

        public LeadInMode LeadIn { get; set; } = LeadInMode.OneBar;

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
        /// Real-time delay in ms to pass to PlayWithDelay when restarting, so that the
        /// <see cref="LeadIn"/> scrolls past before the audio reaches <see cref="From"/>.
        /// Never shorter than <see cref="Values.DelayBeforeAudioResume"/>.
        /// </summary>
        /// <param name="barLengthMs">Length of one bar at <see cref="From"/> in chart ms, or 0 if unknown.</param>
        /// <param name="playbackSpeed">Current playback speed.</param>
        public int RestartDelayMs(double barLengthMs, float playbackSpeed)
        {
            playbackSpeed = playbackSpeed <= 0 ? 1 : playbackSpeed;

            int bars;
            switch (LeadIn)
            {
                case LeadInMode.None:
                    return Values.DelayBeforeAudioResume;
                case LeadInMode.TwoSeconds:
                    return Math.Max(FallbackLeadInMs, Values.DelayBeforeAudioResume);
                case LeadInMode.TwoBars:
                    bars = 2;
                    break;
                default:
                    bars = 1;
                    break;
            }

            if (barLengthMs <= 0)
            {
                return FallbackLeadInMs;
            }

            int delay = (int)Math.Ceiling(bars * barLengthMs / playbackSpeed);
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

        /// <summary>
        /// Whether the loop should restart because it reached the end of the song. A loop whose
        /// <see cref="To"/> is the end of the song never crosses it through <see cref="ShouldRestart"/>,
        /// because audio stops there, so this covers that case instead. The caller decides whether
        /// playback is actually running (for example, whether the pause menu is hidden), since
        /// <c>IsPlaying</c> is already false once the clip ends.
        /// </summary>
        public bool ReachedEnd(int timing) =>
            Enabled && To >= AudioLength - EndToleranceMs && timing >= AudioLength - EndToleranceMs;

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
