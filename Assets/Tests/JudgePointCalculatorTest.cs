using System.Collections.Generic;
using ArcCreate.Gameplay;
using NUnit.Framework;

namespace Tests.Unit
{
    /// <summary>
    /// Expected values are from the native code of Arcaea 7.0.255 (interval 0x17cb97c, points 0xd92558, arc merge 0xba81e8).
    /// </summary>
    public class JudgePointCalculatorTest
    {
        [TestCase(181f, 1f, 165.74586f)]
        [TestCase(150f, 1f, 200f)]
        [TestCase(150f, 2f, 100f)]
        [TestCase(300f, 1f, 200f)]
        [TestCase(-150f, 1f, 200f)]
        public void CalculateInterval_HalfBeatBelow255AndOneBeatAbove(float bpm, float density, float expected)
        {
            Assert.That(JudgePointCalculator.CalculateInterval(bpm, density), Is.EqualTo(expected).Within(0.0001f));
        }

        [Test]
        public void CalculateInterval_ZeroBpmIsInfinite()
        {
            Assert.That(float.IsPositiveInfinity(JudgePointCalculator.CalculateInterval(0f, 1f)), Is.True);
        }

        [Test]
        public void Calculate_NoteShorterThanTwoIntervalsHasOnePointInTheMiddle()
        {
            float interval = JudgePointCalculator.CalculateInterval(181f, 1f);
            List<JudgePoint> points = JudgePointCalculator.Calculate(0, 83, interval, false, false);

            Assert.That(points.Count, Is.EqualTo(1));
            Assert.That(points[0].Timing, Is.EqualTo(41));
        }

        [Test]
        public void Calculate_ZeroLengthHasNoPoint()
        {
            float interval = JudgePointCalculator.CalculateInterval(181f, 1f);

            Assert.That(JudgePointCalculator.Calculate(0, 0, interval, false, false), Is.Empty);
        }

        [TestCase(497, 1)]
        [TestCase(498, 2)]
        [TestCase(500, 2)]
        public void Calculate_SecondPointNeedsThreeIntervals(int duration, int expectedCount)
        {
            float interval = JudgePointCalculator.CalculateInterval(181f, 1f);

            Assert.That(JudgePointCalculator.Calculate(0, duration, interval, false, false).Count, Is.EqualTo(expectedCount));
        }

        [Test]
        public void Calculate_ArcConnectedAfterAnotherArcHasAPointAtItsStart()
        {
            float interval = JudgePointCalculator.CalculateInterval(181f, 1f);
            List<JudgePoint> points = JudgePointCalculator.Calculate(0, 500, interval, true, false);

            Assert.That(points.Count, Is.EqualTo(3));
            Assert.That(points[0].Timing, Is.EqualTo(0));
        }

        [Test]
        public void Calculate_ArcEndingOnTheGridMergesItsLastTwoPointsWhenAnArcFollows()
        {
            float interval = JudgePointCalculator.CalculateInterval(181f, 1f);
            List<JudgePoint> merged = JudgePointCalculator.Calculate(0, 498, interval, false, true);
            List<JudgePoint> notMerged = JudgePointCalculator.Calculate(0, 498, interval, false, false);

            Assert.That(merged.Count, Is.EqualTo(1));
            Assert.That(merged[0].Timing, Is.EqualTo(165));
            Assert.That(merged[0].Weight, Is.EqualTo(2));
            Assert.That(JudgePointCalculator.TotalCombo(merged), Is.EqualTo(JudgePointCalculator.TotalCombo(notMerged)));
        }

        [Test]
        public void Calculate_MergeIsNotUsedWhenTheEndIsMoreThanTwoMillisecondsAfterTheGrid()
        {
            float interval = JudgePointCalculator.CalculateInterval(181f, 1f);

            Assert.That(JudgePointCalculator.Calculate(0, 500, interval, false, true).Count, Is.EqualTo(2));
        }

        [Test]
        public void Calculate_ZeroBpmHasOnePointInTheMiddle()
        {
            float interval = JudgePointCalculator.CalculateInterval(0f, 1f);
            List<JudgePoint> points = JudgePointCalculator.Calculate(0, 400, interval, false, false);

            Assert.That(points.Count, Is.EqualTo(1));
            Assert.That(points[0].Timing, Is.EqualTo(200));
        }

        [Test]
        public void Calculate_UsesSinglePrecision()
        {
            // 1250 ms at 168 BPM is exactly 7 intervals. Double precision gives 6.999999999999999 and loses a point.
            float interval = JudgePointCalculator.CalculateInterval(168f, 1f);

            Assert.That(JudgePointCalculator.Calculate(60000, 61250, interval, false, false).Count, Is.EqualTo(6));
        }
    }
}
