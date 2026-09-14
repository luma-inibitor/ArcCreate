using System.Collections.Generic;
using ArcCreate.Gameplay.Audio.Practice;
using ArcCreate.Gameplay.Data;
using NUnit.Framework;

namespace Tests.Unit
{
    public class BeatGridTest
    {
        private static TimingEvent Ev(int timing, float bpm, float divisor)
            => new TimingEvent { Timing = timing, Bpm = bpm, Divisor = divisor };

        // 120 bpm 4/4 (bar 2000 ms) until 8000, then 240 bpm 4/4 (bar 1000 ms).
        private static BeatGrid TwoTempos()
            => new BeatGrid(new List<TimingEvent> { Ev(8000, 240, 4), Ev(0, 120, 4) });

        [Test]
        public void BarLength()
        {
            BeatGrid grid = TwoTempos();
            Assert.AreEqual(2000, grid.BarLengthAt(0), 1e-6);
            Assert.AreEqual(2000, grid.BarLengthAt(7999), 1e-6);
            Assert.AreEqual(1000, grid.BarLengthAt(8000), 1e-6);
            Assert.AreEqual(500, grid.BeatLengthAt(0), 1e-6);
        }

        [TestCase(0, 0)]
        [TestCase(900, 0)]
        [TestCase(1100, 2000)]
        [TestCase(2999, 2000)]
        [TestCase(3001, 4000)]
        [TestCase(7900, 8000)]
        [TestCase(8400, 8000)]
        [TestCase(8600, 9000)]
        [TestCase(-400, 0)]
        [TestCase(-1100, -2000)]
        public void SnapToBar(int timing, int expected)
        {
            Assert.AreEqual(expected, TwoTempos().SnapToBar(timing));
        }

        [TestCase(600, 500)]
        [TestCase(8120, 8000)]
        [TestCase(8130, 8250)]
        public void SnapToBeat(int timing, int expected)
        {
            Assert.AreEqual(expected, TwoTempos().SnapToBeat(timing));
        }

        [Test]
        public void NextTimingEventStartsABar()
        {
            // Segment 0..1500 at 120 bpm is shorter than one bar (2000), so the bar line
            // above anything in it is the next timing event at 1500, not 2000.
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 4), Ev(1500, 120, 4) });
            Assert.AreEqual(0, grid.SnapToBar(700));
            Assert.AreEqual(1500, grid.SnapToBar(800));
            Assert.AreEqual(1500, grid.SnapToBar(1400));
        }

        [TestCase(0, 0)]
        [TestCase(2000, 1)]
        [TestCase(7999, 3)]
        [TestCase(8000, 4)]
        [TestCase(9500, 5)]
        public void BarIndex(int timing, int expected)
        {
            Assert.AreEqual(expected, TwoTempos().BarIndexAt(timing));
        }

        [Test]
        public void UnusableTempoLeavesTimingAlone()
        {
            BeatGrid zeroBpm = new BeatGrid(new List<TimingEvent> { Ev(0, 0, 4) });
            Assert.AreEqual(1234, zeroBpm.SnapToBar(1234));
            Assert.AreEqual(0, zeroBpm.BarLengthAt(1234), 1e-6);

            BeatGrid zeroDivisor = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 0) });
            Assert.AreEqual(1234, zeroDivisor.SnapToBar(1234));
            Assert.AreEqual(500, zeroDivisor.SnapToBeat(600));

            BeatGrid empty = new BeatGrid(new List<TimingEvent>());
            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(1234, empty.SnapToBar(1234));
        }
    }
}
