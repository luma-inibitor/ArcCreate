using ArcCreate.Gameplay.Audio.Practice;
using NUnit.Framework;

namespace Tests.Unit
{
    public class TimelineWindowTest
    {
        private TimelineWindow window;

        [SetUp]
        public void Setup()
        {
            window = new TimelineWindow();
            window.SetBounds(0, 10000);
        }

        [Test]
        public void CenterInTheMiddle()
        {
            window.CenterOn(5000, 2000);
            Assert.AreEqual(4000, window.From);
            Assert.AreEqual(6000, window.To);
        }

        [Test]
        public void CenterNearTheEdgesShiftsInsideBounds()
        {
            window.CenterOn(300, 2000);
            Assert.AreEqual(0, window.From);
            Assert.AreEqual(2000, window.To);

            window.CenterOn(9900, 2000);
            Assert.AreEqual(8000, window.From);
            Assert.AreEqual(10000, window.To);
        }

        [Test]
        public void LengthIsClampedToBoundsAndMinimum()
        {
            window.CenterOn(5000, 50000);
            Assert.AreEqual(0, window.From);
            Assert.AreEqual(10000, window.Length);

            window.CenterOn(5000, 10);
            Assert.AreEqual(TimelineWindow.MinLengthMs, window.Length);
        }

        [Test]
        public void TinyBoundsDoNotProduceNegativeLength()
        {
            window.SetBounds(0, 40);
            window.CenterOn(20, 2000);
            Assert.AreEqual(0, window.From);
            Assert.AreEqual(40, window.Length);

            window.SetBounds(0, 0);
            window.CenterOn(20, 2000);
            Assert.AreEqual(0, window.Length);
            Assert.AreEqual(0f, window.Normalize(20));
        }

        [Test]
        public void NormalizeAndDenormalize()
        {
            window.CenterOn(5000, 2000);
            Assert.AreEqual(0f, window.Normalize(4000), 1e-6);
            Assert.AreEqual(0.5f, window.Normalize(5000), 1e-6);
            Assert.AreEqual(1f, window.Normalize(6000), 1e-6);
            Assert.Less(window.Normalize(3000), 0f);
            Assert.AreEqual(4500, window.Denormalize(0.25f));
        }

        [Test]
        public void ReversedBoundsAreOrdered()
        {
            window.SetBounds(10000, 0);
            Assert.AreEqual(0, window.Min);
            Assert.AreEqual(10000, window.Max);
        }
    }
}
