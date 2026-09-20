using System;
using ArcCreate.Data;
using ArcCreate.Gameplay.Judgement;
using ArcCreate.Gameplay.Score;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Judgement statistics collected while the player drills one practice loop section.
    /// Holds no references to Unity or services so it can be tested directly.
    /// </summary>
    public class PracticeStats
    {
        /// <summary>
        /// Width of one early/late histogram bin in ms.
        /// </summary>
        public const int BinWidthMs = 10;

        /// <summary>
        /// Number of histogram bins. Bins cover -<see cref="Values.GoodJudgeWindow"/> to
        /// +<see cref="Values.GoodJudgeWindow"/>, with offsets outside that range clamped
        /// into the edge bins.
        /// </summary>
        public const int BinCount = 2 * Values.GoodJudgeWindow / BinWidthMs;

        private readonly int[] histogram = new int[BinCount];
        private readonly StatisticCalculator offsetStatistics = new StatisticCalculator();
        private int offsetCount;

        /// <summary>
        /// Gets the number of times the loop restarted since the last <see cref="Reset"/>.
        /// </summary>
        public int LoopCount { get; private set; }

        /// <summary>
        /// Gets the number of <see cref="JudgementResult.Max"/> judgements.
        /// </summary>
        public int MaxCount { get; private set; }

        /// <summary>
        /// Gets the number of perfect judgements that are not <see cref="JudgementResult.Max"/>.
        /// </summary>
        public int PerfectCount { get; private set; }

        /// <summary>
        /// Gets the number of good judgements.
        /// </summary>
        public int GoodCount { get; private set; }

        /// <summary>
        /// Gets the number of miss judgements.
        /// </summary>
        public int MissCount { get; private set; }

        /// <summary>
        /// Gets the total number of judged notes.
        /// </summary>
        public int JudgedCount => MaxCount + PerfectCount + GoodCount + MissCount;

        /// <summary>
        /// Gets the number of judgements that landed early.
        /// </summary>
        public int EarlyCount { get; private set; }

        /// <summary>
        /// Gets the number of judgements that landed late.
        /// </summary>
        public int LateCount { get; private set; }

        /// <summary>
        /// Gets the score fraction of the judged notes, in 0..1. Max and Perfect count as 1,
        /// Good counts as <see cref="Constants.GoodPenaltyMultipler"/>, Miss counts as 0.
        /// 0 when nothing has been judged.
        /// </summary>
        public double Accuracy
        {
            get
            {
                int judged = JudgedCount;
                if (judged == 0)
                {
                    return 0;
                }

                double weighted = MaxCount + PerfectCount + (GoodCount * Constants.GoodPenaltyMultipler);
                return weighted / judged;
            }
        }

        /// <summary>
        /// Gets the mean of the recorded early/late offsets in ms, 0 when none have been recorded.
        /// </summary>
        public double OffsetMean => offsetStatistics.Mean;

        /// <summary>
        /// Gets the number of offsets recorded into <see cref="OffsetMean"/> and the histogram.
        /// </summary>
        public int OffsetCount => offsetCount;

        /// <summary>
        /// Gets the count of recorded offsets in the given histogram bin.
        /// </summary>
        public int HistogramBin(int index) => histogram[index];

        /// <summary>
        /// Histogram bin index for an offset in ms.
        /// </summary>
        public static int BinFor(int offsetMs)
        {
            int bin = (int)Math.Floor((offsetMs + Values.GoodJudgeWindow) / (double)BinWidthMs);
            if (bin < 0)
            {
                bin = 0;
            }
            else if (bin >= BinCount)
            {
                bin = BinCount - 1;
            }

            return bin;
        }

        /// <summary>
        /// The offset in ms at the start of the given histogram bin.
        /// </summary>
        public static int BinStartMs(int bin) => -Values.GoodJudgeWindow + (bin * BinWidthMs);

        /// <summary>
        /// Record a judgement. <paramref name="offsetMs"/> is present only for timed judgements,
        /// and only that path updates the offset mean and histogram.
        /// </summary>
        public void Record(JudgementResult result, int? offsetMs)
        {
            if (result.IsMax())
            {
                MaxCount++;
            }
            else if (result.IsPerfect())
            {
                PerfectCount++;
            }
            else if (result.IsGood())
            {
                GoodCount++;
            }
            else if (result.IsMiss())
            {
                MissCount++;
            }

            // IsEarly and IsLate leave misses out; here a miss on either side counts by its direction too.
            if (result.IsEarly() || result == JudgementResult.MissEarly)
            {
                EarlyCount++;
            }
            else if (result.IsLate() || result == JudgementResult.MissLate)
            {
                LateCount++;
            }

            if (offsetMs.HasValue)
            {
                offsetStatistics.UpdateStatistics(offsetMs.Value);
                offsetCount++;
                histogram[BinFor(offsetMs.Value)]++;
            }
        }

        /// <summary>
        /// Count one loop restart.
        /// </summary>
        public void CountLoop()
        {
            LoopCount++;
        }

        /// <summary>
        /// Reset all statistics back to zero.
        /// </summary>
        public void Reset()
        {
            LoopCount = 0;
            MaxCount = 0;
            PerfectCount = 0;
            GoodCount = 0;
            MissCount = 0;
            EarlyCount = 0;
            LateCount = 0;
            offsetCount = 0;
            offsetStatistics.Reset();
            Array.Clear(histogram, 0, histogram.Length);
        }
    }
}
