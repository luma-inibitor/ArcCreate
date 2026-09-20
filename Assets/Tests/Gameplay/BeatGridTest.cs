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

        private static List<(int timing, bool isBar)> Lines(BeatGrid grid, int from, int to)
            => new List<(int timing, bool isBar)>(grid.LinesBetween(from, to));

        [Test]
        public void BarLength()
        {
            BeatGrid grid = TwoTempos();
            Assert.AreEqual(2000, grid.BarLengthAt(0), 1e-6);
            Assert.AreEqual(2000, grid.BarLengthAt(7999), 1e-6);
            Assert.AreEqual(1000, grid.BarLengthAt(8000), 1e-6);
        }

        [Test]
        public void BeatLengthFollowsTheSegment()
        {
            BeatGrid grid = TwoTempos();
            Assert.AreEqual(500, grid.BeatLengthAt(0), 1e-6);
            Assert.AreEqual(250, grid.BeatLengthAt(8000), 1e-6);
            Assert.AreEqual(0, new BeatGrid(new List<TimingEvent>()).BeatLengthAt(0), 1e-6);
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
        public void NearestPicksTheLaterLineAtTheExactMidpoint()
        {
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 4) });
            Assert.AreEqual(2000, grid.SnapToBar(1000));
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
            Assert.AreEqual(1500, grid.SnapToBar(100, BeatGrid.Rounding.Up));
            Assert.AreEqual(0, grid.SnapToBar(1400, BeatGrid.Rounding.Down));

            Assert.AreEqual(0, grid.BarIndexAt(1499));
            Assert.AreEqual(1, grid.BarIndexAt(1500));
            Assert.AreEqual(1, grid.BarIndexAt(3499));
            Assert.AreEqual(2, grid.BarIndexAt(3500));
        }

        [Test]
        public void FractionalBarLinesRoundTrip()
        {
            // 90 bpm 4/4: bar lines at 0, 2666.67, 5333.33, 8000. Snapping stores whole ms,
            // so a line can land just below its true position and must still count as on it.
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 90, 4) });
            Assert.AreEqual(2667, grid.SnapToBar(2700));
            Assert.AreEqual(2667, grid.SnapToBar(2667, BeatGrid.Rounding.Up));
            Assert.AreEqual(2667, grid.SnapToBar(2667, BeatGrid.Rounding.Down));
            Assert.AreEqual(2667, grid.SnapToBar(2668, BeatGrid.Rounding.Down));
            Assert.AreEqual(2667, grid.SnapToBar(2666, BeatGrid.Rounding.Up));

            int bar2 = grid.SnapToBar(5300);
            Assert.AreEqual(5333, bar2);
            Assert.AreEqual(2, grid.BarIndexAt(bar2));
            Assert.AreEqual(1, grid.BarIndexAt(grid.SnapToBar(2700)));
        }

        [Test]
        public void HalfMillisecondBarLineCountsFromItsWholeMillisecond()
        {
            // 96 bpm with divisor 3.5: bar 2187.5 ms exactly, drawn at 2188.
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 96, 3.5f) });
            Assert.AreEqual(2187.5, grid.BarLengthAt(0), 1e-6);
            Assert.AreEqual(2188, grid.SnapToBar(2188, BeatGrid.Rounding.Up));
            Assert.AreEqual(2188, grid.SnapToBar(2188, BeatGrid.Rounding.Down));
            Assert.AreEqual(1, grid.BarIndexAt(2188));
            Assert.AreEqual(0, grid.BarIndexAt(2187));
        }

        [Test]
        public void NextTimingEventOnARoundedUpLineIsNotAnExtraBar()
        {
            // 90 bpm 4/4 restarted at 2667, exactly where the first segment's second bar line is drawn.
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 90, 4), Ev(2667, 90, 4) });
            Assert.AreEqual(0, grid.BarIndexAt(2666));
            Assert.AreEqual(1, grid.BarIndexAt(2667));
            Assert.AreEqual(1, grid.BarIndexAt(5333));
            Assert.AreEqual(2, grid.BarIndexAt(5334));

            // The shared line is drawn once, by the segment that starts there.
            CollectionAssert.AreEqual(new List<(int, bool)> { (2667, true) }, Lines(grid, 2600, 2700));
        }

        [Test]
        public void WindowStartingOnALineIncludesIt()
        {
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 90, 4) });
            Assert.AreEqual((2667, true), Lines(grid, 2667, 3000)[0]);
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
        public void BarIndexAcrossAStop()
        {
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 4), Ev(1000, 0, 4), Ev(9000, 120, 4) });
            Assert.AreEqual(1, grid.BarIndexAt(5000));
            Assert.AreEqual(1, grid.BarIndexAt(9000));
            Assert.AreEqual(2, grid.BarIndexAt(11000));
        }

        [Test]
        public void TempoAboveTheBeatlineCeilingHasNoGrid()
        {
            // The game skips beatlines above Values.MaxBeatlineBpm, so this grid does too.
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 4), Ev(4000, 100000, 4), Ev(6000, 120, 4) });
            Assert.AreEqual(0, grid.BarLengthAt(5000), 1e-6);
            Assert.AreEqual(5000, grid.SnapToBar(5000));
            Assert.AreEqual(2, grid.BarIndexAt(6000));
            CollectionAssert.IsEmpty(Lines(grid, 4000, 5999));
        }

        [Test]
        public void NegativeBpmIsUsedAsItsAbsoluteValue()
        {
            Assert.AreEqual(2000, new BeatGrid(new List<TimingEvent> { Ev(0, -120, 4) }).BarLengthAt(0), 1e-6);
        }

        [Test]
        public void FractionalDivisor()
        {
            // 120 bpm with divisor 3.5: beat 500, bar 1750. Only beat lines landing on a bar are flagged,
            // so the bar line at 1750 falls between beats and nothing is flagged there.
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 3.5f) });
            Assert.AreEqual(1750, grid.SnapToBar(1700));

            var bars = new List<int>();
            foreach ((int timing, bool isBar) in grid.LinesBetween(0, 3500))
            {
                if (isBar)
                {
                    bars.Add(timing);
                }
            }

            CollectionAssert.AreEqual(new List<int> { 0, 3500 }, bars);
        }

        [Test]
        public void GridExtendsBackwardsBeforeTheFirstEvent()
        {
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(1000, 120, 4) });
            Assert.AreEqual(-1, grid.BarIndexAt(999));
            Assert.AreEqual(-1, grid.BarIndexAt(-1000));
            Assert.AreEqual(-2, grid.BarIndexAt(-1001));
            Assert.AreEqual(-1000, grid.SnapToBar(-900));
            CollectionAssert.AreEqual(
                new List<(int, bool)> { (-1000, true), (-500, false), (0, false) },
                Lines(grid, -1000, 0));
        }

        [Test]
        public void EventsSharingATimingKeepChartOrder()
        {
            // The later event wins, and the segment it shadows draws nothing.
            BeatGrid grid = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 4), Ev(0, 240, 4) });
            Assert.AreEqual(1000, grid.BarLengthAt(0), 1e-6);

            List<(int timing, bool isBar)> lines = Lines(grid, 0, 3000);
            Assert.AreEqual(13, lines.Count);
            Assert.AreEqual((0, true), lines[0]);
            CollectionAssert.AllItemsAreUnique(lines);
        }

        [Test]
        public void UnusableTempoLeavesTimingAlone()
        {
            BeatGrid zeroBpm = new BeatGrid(new List<TimingEvent> { Ev(0, 0, 4) });
            Assert.AreEqual(1234, zeroBpm.SnapToBar(1234));
            Assert.AreEqual(0, zeroBpm.BarLengthAt(1234), 1e-6);

            // A divisor below 1 only removes the bar lines; the beats are still drawn.
            BeatGrid zeroDivisor = new BeatGrid(new List<TimingEvent> { Ev(0, 120, 0) });
            Assert.AreEqual(1234, zeroDivisor.SnapToBar(1234));
            Assert.AreEqual(0, zeroDivisor.BarLengthAt(0), 1e-6);
            CollectionAssert.AreEqual(
                new List<(int, bool)> { (0, false), (500, false), (1000, false) },
                Lines(zeroDivisor, 0, 1000));

            BeatGrid empty = new BeatGrid(new List<TimingEvent>());
            Assert.AreEqual(1234, empty.SnapToBar(1234));
            Assert.AreEqual(0, empty.BarIndexAt(1234));
            Assert.AreEqual(0, empty.BarLengthAt(1234), 1e-6);
        }

        [Test]
        public void LinesWithinOneTempo()
        {
            CollectionAssert.AreEqual(
                new List<(int, bool)> { (0, true), (500, false), (1000, false), (1500, false), (2000, true) },
                Lines(TwoTempos(), 0, 2000));
        }

        [Test]
        public void LinesAcrossTempoChangeRestartTheBar()
        {
            CollectionAssert.AreEqual(
                new List<(int, bool)> { (7000, false), (7500, false), (8000, true), (8250, false), (8500, false) },
                Lines(TwoTempos(), 7000, 8500));
        }

        [Test]
        public void LinesStartMidWindowWithoutPrecedingLine()
        {
            CollectionAssert.AreEqual(new List<(int, bool)> { (1500, false) }, Lines(TwoTempos(), 1001, 1600));
        }

        [Test]
        public void NoLinesForUnusableTempoOrEmptyRange()
        {
            CollectionAssert.IsEmpty(Lines(new BeatGrid(new List<TimingEvent> { Ev(0, 0, 4) }), 0, 5000));
            CollectionAssert.IsEmpty(Lines(TwoTempos(), 500, 100));
        }
    }
}
