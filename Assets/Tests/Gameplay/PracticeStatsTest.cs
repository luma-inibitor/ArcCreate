using ArcCreate.Gameplay.Audio.Practice;
using ArcCreate.Gameplay.Judgement;
using NUnit.Framework;

namespace Tests.Unit
{
    public class PracticeStatsTest
    {
        private PracticeStats stats;

        [SetUp]
        public void Setup()
        {
            stats = new PracticeStats();
        }

        [Test]
        public void FreshStatsAreAllZero()
        {
            Assert.AreEqual(0, stats.LoopCount);
            Assert.AreEqual(0, stats.MaxCount);
            Assert.AreEqual(0, stats.PerfectCount);
            Assert.AreEqual(0, stats.GoodCount);
            Assert.AreEqual(0, stats.MissCount);
            Assert.AreEqual(0, stats.JudgedCount);
            Assert.AreEqual(0, stats.EarlyCount);
            Assert.AreEqual(0, stats.LateCount);
            Assert.AreEqual(0, stats.OffsetCount);
            Assert.AreEqual(0d, stats.OffsetMean);
            Assert.AreEqual(0d, stats.Accuracy, 1e-9);

            for (int bin = 0; bin < PracticeStats.BinCount; bin++)
            {
                Assert.AreEqual(0, stats.HistogramBin(bin));
            }
        }

        [Test]
        public void MaxCountsSeparatelyFromPerfect()
        {
            stats.Record(JudgementResult.Max, 0);
            stats.Record(JudgementResult.PerfectEarly, -10);
            stats.Record(JudgementResult.PerfectLate, 10);
            stats.Record(JudgementResult.PerfectMapped, null);

            Assert.AreEqual(1, stats.MaxCount);
            Assert.AreEqual(3, stats.PerfectCount);
            Assert.AreEqual(4, stats.JudgedCount);
        }

        [Test]
        public void GoodAndMissCountByClassIncludingMapped()
        {
            stats.Record(JudgementResult.GoodEarly, -60);
            stats.Record(JudgementResult.GoodLate, 60);
            stats.Record(JudgementResult.GoodMapped, null);
            stats.Record(JudgementResult.MissEarly, -110);
            stats.Record(JudgementResult.MissLate, 110);
            stats.Record(JudgementResult.MissMapped, null);

            Assert.AreEqual(3, stats.GoodCount);
            Assert.AreEqual(3, stats.MissCount);
            Assert.AreEqual(6, stats.JudgedCount);
        }

        [Test]
        public void MaxIsNeitherEarlyNorLate()
        {
            stats.Record(JudgementResult.Max, 0);
            Assert.AreEqual(0, stats.EarlyCount);
            Assert.AreEqual(0, stats.LateCount);
        }

        [Test]
        public void MappedResultsAreNeitherEarlyNorLate()
        {
            stats.Record(JudgementResult.PerfectMapped, null);
            stats.Record(JudgementResult.GoodMapped, null);
            stats.Record(JudgementResult.MissMapped, null);

            Assert.AreEqual(0, stats.EarlyCount);
            Assert.AreEqual(0, stats.LateCount);
        }

        [Test]
        public void EarlyAndLateResultsAreCountedByDirection()
        {
            stats.Record(JudgementResult.PerfectEarly, -10);
            stats.Record(JudgementResult.GoodEarly, -60);
            stats.Record(JudgementResult.MissEarly, -110);
            stats.Record(JudgementResult.PerfectLate, 10);
            stats.Record(JudgementResult.GoodLate, 60);
            stats.Record(JudgementResult.MissLate, 110);

            Assert.AreEqual(3, stats.EarlyCount);
            Assert.AreEqual(3, stats.LateCount);
        }

        [Test]
        public void AccuracyWeighsMaxPerfectGoodAndMiss()
        {
            stats.Record(JudgementResult.Max, 0);
            stats.Record(JudgementResult.Max, 0);
            stats.Record(JudgementResult.GoodLate, 60);
            stats.Record(JudgementResult.MissLate, 110);

            Assert.AreEqual(0.625d, stats.Accuracy, 1e-9);
        }

        [Test]
        public void AccuracyIsZeroWhenNothingJudged()
        {
            Assert.AreEqual(0d, stats.Accuracy, 1e-9);
        }

        [Test]
        public void OffsetMeanAveragesRecordedOffsets()
        {
            stats.Record(JudgementResult.PerfectEarly, -10);
            stats.Record(JudgementResult.PerfectLate, 10);
            stats.Record(JudgementResult.PerfectLate, 20);

            Assert.AreEqual(3, stats.OffsetCount);
            Assert.AreEqual(20d / 3d, stats.OffsetMean, 1e-9);
        }

        [Test]
        public void RecordWithoutOffsetDoesNotChangeMeanOrOffsetCount()
        {
            stats.Record(JudgementResult.PerfectEarly, -10);
            double meanBefore = stats.OffsetMean;
            int countBefore = stats.OffsetCount;

            stats.Record(JudgementResult.PerfectMapped, null);

            Assert.AreEqual(meanBefore, stats.OffsetMean, 1e-9);
            Assert.AreEqual(countBefore, stats.OffsetCount);
        }

        [TestCase(-100, 0)]
        [TestCase(-1, 9)]
        [TestCase(0, 10)]
        [TestCase(9, 10)]
        [TestCase(10, 11)]
        [TestCase(99, 19)]
        [TestCase(100, 19)]
        [TestCase(-101, 0)]
        [TestCase(250, 19)]
        public void BinForClampsAndBucketsOffsets(int offsetMs, int expectedBin)
        {
            Assert.AreEqual(expectedBin, PracticeStats.BinFor(offsetMs));
        }

        [TestCase(0, -100)]
        [TestCase(9, -10)]
        [TestCase(10, 0)]
        [TestCase(19, 90)]
        public void BinStartMsReturnsTheBinsLowerEdge(int bin, int expectedStartMs)
        {
            Assert.AreEqual(expectedStartMs, PracticeStats.BinStartMs(bin));
        }

        [Test]
        public void HistogramCountsRecordsByBin()
        {
            stats.Record(JudgementResult.PerfectEarly, -10);
            stats.Record(JudgementResult.PerfectEarly, -10);
            stats.Record(JudgementResult.PerfectLate, 10);

            Assert.AreEqual(2, stats.HistogramBin(PracticeStats.BinFor(-10)));
            Assert.AreEqual(1, stats.HistogramBin(PracticeStats.BinFor(10)));
            Assert.AreEqual(0, stats.HistogramBin(PracticeStats.BinFor(50)));
        }

        [Test]
        public void CountLoopIncrementsLoopCount()
        {
            stats.CountLoop();
            stats.CountLoop();
            Assert.AreEqual(2, stats.LoopCount);
        }

        [Test]
        public void ResetClearsEverything()
        {
            stats.Record(JudgementResult.Max, 0);
            stats.Record(JudgementResult.GoodLate, 60);
            stats.Record(JudgementResult.MissEarly, -110);
            stats.CountLoop();
            stats.CountLoop();

            stats.Reset();

            Assert.AreEqual(0, stats.LoopCount);
            Assert.AreEqual(0, stats.MaxCount);
            Assert.AreEqual(0, stats.PerfectCount);
            Assert.AreEqual(0, stats.GoodCount);
            Assert.AreEqual(0, stats.MissCount);
            Assert.AreEqual(0, stats.JudgedCount);
            Assert.AreEqual(0, stats.EarlyCount);
            Assert.AreEqual(0, stats.LateCount);
            Assert.AreEqual(0, stats.OffsetCount);
            Assert.AreEqual(0d, stats.OffsetMean);
            Assert.AreEqual(0d, stats.Accuracy, 1e-9);

            for (int bin = 0; bin < PracticeStats.BinCount; bin++)
            {
                Assert.AreEqual(0, stats.HistogramBin(bin));
            }
        }
    }
}
