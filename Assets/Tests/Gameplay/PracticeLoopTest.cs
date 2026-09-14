using ArcCreate.Gameplay;
using ArcCreate.Gameplay.Audio.Practice;
using NUnit.Framework;

namespace Tests.Unit
{
    public class PracticeLoopTest
    {
        private PracticeLoop loop;

        [SetUp]
        public void Setup()
        {
            loop = new PracticeLoop();
            loop.SetAudioLength(10000);
        }

        [Test]
        public void AudioLengthResetsRangeToWholeAudio()
        {
            Assert.AreEqual(0, loop.From);
            Assert.AreEqual(10000, loop.To);
        }

        [Test]
        public void RangeIsOrdered()
        {
            loop.SetRange(5000, 2000);
            Assert.AreEqual(2000, loop.From);
            Assert.AreEqual(5000, loop.To);
        }

        [Test]
        public void RangeKeepsMinimumLengthAndStaysInsideAudio()
        {
            loop.SetRange(9500, 9800);
            Assert.AreEqual(9000, loop.From);
            Assert.AreEqual(10000, loop.To);

            loop.SetRange(-500, 200);
            Assert.AreEqual(0, loop.From);
            Assert.AreEqual(1000, loop.To);
        }

        [Test]
        public void ShortAudioDoesNotProduceNegativeBounds()
        {
            loop.SetAudioLength(500);
            loop.SetRange(100, 400);
            Assert.AreEqual(0, loop.From);
            Assert.AreEqual(500, loop.To);

            loop.SetAudioLength(0);
            loop.SetRange(100, 400);
            Assert.AreEqual(0, loop.From);
            Assert.AreEqual(0, loop.To);
        }

        [Test]
        public void SetFromAndSetToKeepTheOtherEnd()
        {
            loop.SetRange(2000, 5000);
            loop.SetFrom(3000);
            Assert.AreEqual(3000, loop.From);
            Assert.AreEqual(5000, loop.To);
            loop.SetTo(4500);
            Assert.AreEqual(3000, loop.From);
            Assert.AreEqual(4500, loop.To);
        }

        [Test]
        public void RangeChangeRaisesEventAndResetsLoopCount()
        {
            int raised = 0;
            loop.OnRangeChange += () => raised++;
            loop.MarkRestarted(200, 1);
            Assert.AreEqual(1, loop.LoopCount);
            loop.SetRange(2000, 5000);
            Assert.AreEqual(1, raised);
            Assert.AreEqual(0, loop.LoopCount);
        }

        [Test]
        public void RestartOnlyWhenEnabledAndPlaying()
        {
            loop.SetRange(2000, 5000);
            Assert.IsFalse(loop.ShouldRestart(6000, isPlaying: true));
            loop.Enabled = true;
            Assert.IsFalse(loop.ShouldRestart(6000, isPlaying: false));
            Assert.IsTrue(loop.ShouldRestart(6000, isPlaying: true));
        }

        [Test]
        public void RestartPastEndOfRange()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Assert.IsFalse(loop.ShouldRestart(5000, true));
            Assert.IsTrue(loop.ShouldRestart(5001, true));
        }

        [Test]
        public void RestartBeforeStartAllowsToleranceAndLeadIn()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Assert.IsFalse(loop.ShouldRestart(2000 - PracticeLoop.RestartToleranceMs, true));
            Assert.IsTrue(loop.ShouldRestart(2000 - PracticeLoop.RestartToleranceMs - 1, true));

            // 500 ms real delay at 0.5x is 250 ms of chart time before A.
            loop.MarkRestarted(500, 0.5f);
            Assert.IsFalse(loop.ShouldRestart(1750 - PracticeLoop.RestartToleranceMs, true));
            Assert.IsTrue(loop.ShouldRestart(1750 - PracticeLoop.RestartToleranceMs - 1, true));
        }

        [Test]
        public void RestartDelayFollowsLeadInMode()
        {
            loop.LeadIn = LeadInMode.None;
            Assert.AreEqual(Values.DelayBeforeAudioResume, loop.RestartDelayMs(1000, 1f));

            loop.LeadIn = LeadInMode.OneBar;
            Assert.AreEqual(1000, loop.RestartDelayMs(1000, 1f));
            Assert.AreEqual(2000, loop.RestartDelayMs(1000, 0.5f));

            loop.LeadIn = LeadInMode.TwoBars;
            Assert.AreEqual(4000, loop.RestartDelayMs(1000, 0.5f));

            loop.LeadIn = LeadInMode.TwoSeconds;
            Assert.AreEqual(2000, loop.RestartDelayMs(1000, 0.5f));
        }

        [Test]
        public void RestartDelayFallsBackWithoutABar()
        {
            loop.LeadIn = LeadInMode.OneBar;
            Assert.AreEqual(2000, loop.RestartDelayMs(0, 1f));
            Assert.GreaterOrEqual(loop.RestartDelayMs(50, 1f), Values.DelayBeforeAudioResume);
        }

        [Test]
        public void Contains()
        {
            loop.SetRange(2000, 5000);
            Assert.IsTrue(loop.Contains(2000));
            Assert.IsTrue(loop.Contains(5000));
            Assert.IsFalse(loop.Contains(1999));
            Assert.IsFalse(loop.Contains(5001));
        }

        [Test]
        public void MoveFromNeverCrossesTo()
        {
            loop.SetRange(2000, 5000);
            loop.MoveFrom(3000);
            Assert.AreEqual(3000, loop.From);
            loop.MoveFrom(7000);
            Assert.AreEqual(5000 - PracticeLoop.MinLengthMs, loop.From);
            Assert.AreEqual(5000, loop.To);
            loop.MoveFrom(-100);
            Assert.AreEqual(0, loop.From);
        }

        [Test]
        public void MoveToNeverCrossesFrom()
        {
            loop.SetRange(2000, 5000);
            loop.MoveTo(1000);
            Assert.AreEqual(2000, loop.From);
            Assert.AreEqual(2000 + PracticeLoop.MinLengthMs, loop.To);
            loop.MoveTo(20000);
            Assert.AreEqual(10000, loop.To);
        }
    }
}
