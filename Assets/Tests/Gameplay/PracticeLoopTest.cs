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

        /// <summary>
        /// An arrange-step call to <see cref="PracticeLoop.ShouldRestart"/> whose result is not asserted,
        /// kept separate from the asserted steps for readability.
        /// </summary>
        private static void Observe(PracticeLoop loop, int timing, bool playing = true)
        {
            loop.ShouldRestart(timing, playing);
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
        public void RestartOnlyWhenEnabledAndPlaying()
        {
            loop.SetRange(2000, 5000);
            Observe(loop, 4900);
            Assert.IsFalse(loop.ShouldRestart(6000, isPlaying: true));

            loop.Enabled = true;
            Observe(loop, 4900);
            Assert.IsFalse(loop.ShouldRestart(6000, isPlaying: false));

            Observe(loop, 4900);
            Assert.IsTrue(loop.ShouldRestart(6000, isPlaying: true));
        }

        [Test]
        public void RestartPastEndOfRange()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Assert.IsFalse(loop.ShouldRestart(4990, true));
            Assert.IsFalse(loop.ShouldRestart(5000, true));
            Assert.IsTrue(loop.ShouldRestart(5001, true));
        }

        [Test]
        public void NoRestartBeforeStart()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Assert.IsFalse(loop.ShouldRestart(1000, true));
            Assert.IsFalse(loop.ShouldRestart(1500, true));
            Assert.IsFalse(loop.ShouldRestart(2500, true));
        }

        [Test]
        public void SeekPastEndDoesNotRestart()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Observe(loop, 3000, playing: false);

            // Seek while paused, then resume and keep playing past B.
            Assert.IsFalse(loop.ShouldRestart(8000, false));
            Assert.IsFalse(loop.ShouldRestart(8000, true));
            Assert.IsFalse(loop.ShouldRestart(8100, true));
        }

        [Test]
        public void SeekBeforeStartPlaysIntoRangeAndLoopsAtEnd()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Observe(loop, 3000, playing: false);

            Assert.IsFalse(loop.ShouldRestart(500, false));
            Assert.IsFalse(loop.ShouldRestart(500, true));
            Assert.IsFalse(loop.ShouldRestart(2500, true));
            Assert.IsFalse(loop.ShouldRestart(4990, true));
            Assert.IsTrue(loop.ShouldRestart(5010, true));
        }

        [Test]
        public void RestartJumpBackDoesNotRestartAgain()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Observe(loop, 4990);
            Assert.IsTrue(loop.ShouldRestart(5010, true));

            // Lead-in starts before A.
            Assert.IsFalse(loop.ShouldRestart(1000, true));
            Assert.IsFalse(loop.ShouldRestart(1016, true));
        }

        [Test]
        public void FrameSkipAcrossEndStillRestarts()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Observe(loop, 4800);
            Assert.IsTrue(loop.ShouldRestart(5400, true));
        }

        [Test]
        public void NoRestartUntilObservedAfterRangeChangeOrReset()
        {
            loop.SetRange(2000, 5000);
            loop.Enabled = true;
            Observe(loop, 4000);

            loop.SetTo(3000);
            Assert.IsFalse(loop.ShouldRestart(4000, true));

            Observe(loop, 2900);
            loop.ResetTracking();
            Assert.IsFalse(loop.ShouldRestart(3100, true));
        }

        [Test]
        public void RestartDelayUsesOneBarOfLeadIn()
        {
            Assert.AreEqual(1000, loop.RestartDelayMs(1000, 1f));
            Assert.AreEqual(2000, loop.RestartDelayMs(1000, 0.5f));
            Assert.AreEqual(3334, loop.RestartDelayMs(1000, 0.3f));
        }

        [Test]
        public void RestartDelayFallsBackWithoutABar()
        {
            Assert.AreEqual(PracticeLoop.FallbackLeadInMs, loop.RestartDelayMs(0, 1f));
            Assert.AreEqual(Values.DelayBeforeAudioResume, loop.RestartDelayMs(50, 1f));
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
