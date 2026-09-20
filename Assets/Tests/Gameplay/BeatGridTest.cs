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

        [TestCase(1100, BeatGrid.Rounding.Down, 0)]
        [TestCase(1100, BeatGrid.Rounding.Up, 2000)]
        [TestCase(2000, BeatGrid.Rounding.Down, 2000)]
        [TestCase(2000, BeatGrid.Rounding.Up, 2000)]
        [TestCase(1999, BeatGrid.Rounding.Down, 0)]
        [TestCase(2001, BeatGrid.Rounding.Up, 4000)]
        [TestCase(7900, BeatGrid.Rounding.Up, 8000)]
        [TestCase(7999, BeatGrid.Rounding.Down, 6000)]
        [TestCase(8500, BeatGrid.Rounding.Down, 8000)]
        [TestCase(-100, BeatGrid.Rounding.Up, 0)]
        public void SnapToBarDirected(int timing, BeatGrid.Rounding rounding, int expected)
        {
            Assert.AreEqual(expected, TwoTempos().SnapToBar(timing, rounding));
        }

        [Test]
        public void SnapUpStopsAtNextTimingEvent()
        {
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 4), Ev(1500, 120, 4) });
            Assert.AreEqual(1500, grid.SnapToBar(100, BeatGrid.Rounding.Up));
            Assert.AreEqual(0, grid.SnapToBar(1400, BeatGrid.Rounding.Down));
        }

        [Test]
        public void FractionalBarLinesRoundTrip()
        {
            // 90 bpm 4/4: bar lines at 0, 2666.67, 5333.33, 8000. Snapping stores whole ms,
            // so a line can land just below its true position and must still count as on it.
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 90, 4) });
            int bar2 = grid.SnapToBar(5300);
            Assert.AreEqual(5333, bar2);
            Assert.AreEqual(2, grid.BarIndexAt(bar2));
            Assert.AreEqual(bar2, grid.SnapToBar(bar2, BeatGrid.Rounding.Down));
            Assert.AreEqual(bar2, grid.SnapToBar(bar2, BeatGrid.Rounding.Up));
            Assert.AreEqual(1, grid.BarIndexAt(grid.SnapToBar(2700)));
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
        public void TypicalBarLengthIsTheTempoCoveringMostTime()
        {
            // 2000 ms bars for 0..8000, then 1000 ms bars to the end.
            Assert.AreEqual(2000, TwoTempos().TypicalBarLength(0, 12000), 1e-6);
            Assert.AreEqual(1000, TwoTempos().TypicalBarLength(0, 20000), 1e-6);

            // A short half-speed section does not change the typical bar.
            BeatGrid gimmick = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 4), Ev(4000, 60, 4), Ev(6000, 120, 4) });
            Assert.AreEqual(2000, gimmick.TypicalBarLength(0, 30000), 1e-6);
        }

        [Test]
        public void TypicalBarLengthIgnoresUnusableTempos()
        {
            BeatGrid stops = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 4), Ev(1000, 0, 4), Ev(9000, 120, 4) });
            Assert.AreEqual(2000, stops.TypicalBarLength(0, 10000), 1e-6);
            Assert.AreEqual(0, new BeatGrid(new List<TimingEvent>()).TypicalBarLength(0, 10000), 1e-6);
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

        [Test]
        public void LinesWithinOneTempo()
        {
            var lines = new List<(int timing, bool isBar)>(TwoTempos().LinesBetween(0, 2000));
            CollectionAssert.AreEqual(
                new List<(int, bool)> { (0, true), (500, false), (1000, false), (1500, false), (2000, true) },
                lines);
        }

        [Test]
        public void LinesAcrossTempoChangeRestartTheBar()
        {
            var lines = new List<(int timing, bool isBar)>(TwoTempos().LinesBetween(7000, 8500));
            CollectionAssert.AreEqual(
                new List<(int, bool)> { (7000, false), (7500, false), (8000, true), (8250, false), (8500, false) },
                lines);
        }

        [Test]
        public void LinesStartMidWindowWithoutPrecedingLine()
        {
            var lines = new List<(int timing, bool isBar)>(TwoTempos().LinesBetween(1001, 1600));
            CollectionAssert.AreEqual(new List<(int, bool)> { (1500, false) }, lines);
        }

        [Test]
        public void NoLinesForUnusableTempoOrEmptyRange()
        {
            BeatGrid zeroBpm = new BeatGrid(new List<TimingEvent> { Ev(0, 0, 4) });
            CollectionAssert.IsEmpty(new List<(int, bool)>(zeroBpm.LinesBetween(0, 5000)));
            CollectionAssert.IsEmpty(new List<(int, bool)>(TwoTempos().LinesBetween(500, 100)));
        }
    }
}
