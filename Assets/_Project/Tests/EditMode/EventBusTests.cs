using NUnit.Framework;
using QonaevLife.Core;
using UnityEngine.TestTools;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Типизированная шина событий (п. 4.2 ТЗ).</summary>
    [TestFixture]
    public sealed class EventBusTests
    {
        private readonly struct SampleEvent : IGameEvent
        {
            public SampleEvent(int value) => Value = value;
            public int Value { get; }
        }

        private readonly struct OtherEvent : IGameEvent
        {
            public OtherEvent(string tag) => Tag = tag;
            public string Tag { get; }
        }

        private EventBus _bus;

        [SetUp]
        public void SetUp() => _bus = new EventBus();

        [Test]
        public void Publish_ReachesSubscriber()
        {
            var received = 0;
            _bus.Subscribe<SampleEvent>(e => received = e.Value);

            _bus.Publish(new SampleEvent(42));

            Assert.That(received, Is.EqualTo(42));
        }

        [Test]
        public void Publish_WithNoSubscribers_DoesNothing()
        {
            Assert.DoesNotThrow(() => _bus.Publish(new SampleEvent(1)));
        }

        [Test]
        public void EventTypes_AreIsolated()
        {
            var sampleCount = 0;
            var otherCount = 0;
            _bus.Subscribe<SampleEvent>(_ => sampleCount++);
            _bus.Subscribe<OtherEvent>(_ => otherCount++);

            _bus.Publish(new SampleEvent(1));

            Assert.That(sampleCount, Is.EqualTo(1));
            Assert.That(otherCount, Is.Zero);
        }

        [Test]
        public void DuplicateSubscription_IsIgnored()
        {
            var count = 0;
            void Handler(SampleEvent _) => count++;

            _bus.Subscribe<SampleEvent>(Handler);
            _bus.Subscribe<SampleEvent>(Handler);

            _bus.Publish(new SampleEvent(1));

            Assert.That(count, Is.EqualTo(1));
        }

        [Test]
        public void Unsubscribe_StopsDelivery()
        {
            var count = 0;
            void Handler(SampleEvent _) => count++;

            _bus.Subscribe<SampleEvent>(Handler);
            _bus.Publish(new SampleEvent(1));
            _bus.Unsubscribe<SampleEvent>(Handler);
            _bus.Publish(new SampleEvent(2));

            Assert.That(count, Is.EqualTo(1));
        }

        /// <summary>Отписка внутри обработчика не должна ломать текущую рассылку.</summary>
        [Test]
        public void UnsubscribeDuringDispatch_IsSafe()
        {
            var firstCalls = 0;
            var secondCalls = 0;

            void Second(SampleEvent _) => secondCalls++;
            void First(SampleEvent _)
            {
                firstCalls++;
                _bus.Unsubscribe<SampleEvent>(Second);
            }

            _bus.Subscribe<SampleEvent>(First);
            _bus.Subscribe<SampleEvent>(Second);

            Assert.DoesNotThrow(() => _bus.Publish(new SampleEvent(1)));

            Assert.That(firstCalls, Is.EqualTo(1));
            Assert.That(secondCalls, Is.EqualTo(1), "Снимок списка уже включал второго подписчика.");

            _bus.Publish(new SampleEvent(2));
            Assert.That(secondCalls, Is.EqualTo(1), "После отписки доставка прекращается.");
        }

        /// <summary>Вложенная публикация не должна портить внешнюю рассылку.</summary>
        [Test]
        public void NestedPublish_DeliversBothEvents()
        {
            var sampleCalls = 0;
            var otherCalls = 0;

            _bus.Subscribe<SampleEvent>(_ =>
            {
                sampleCalls++;
                _bus.Publish(new OtherEvent("nested"));
            });
            _bus.Subscribe<SampleEvent>(_ => sampleCalls++);
            _bus.Subscribe<OtherEvent>(_ => otherCalls++);

            _bus.Publish(new SampleEvent(1));

            Assert.That(sampleCalls, Is.EqualTo(2), "Оба подписчика внешнего события получили его.");
            Assert.That(otherCalls, Is.EqualTo(1));
        }

        /// <summary>NFR-006: исключение в одном обработчике не прерывает остальных.</summary>
        [Test]
        public void ThrowingHandler_DoesNotBlockOthers()
        {
            var secondCalled = false;

            _bus.Subscribe<SampleEvent>(_ => throw new System.InvalidOperationException("сбой"));
            _bus.Subscribe<SampleEvent>(_ => secondCalled = true);

            LogAssert.ignoreFailingMessages = true;
            Assert.DoesNotThrow(() => _bus.Publish(new SampleEvent(1)));
            LogAssert.ignoreFailingMessages = false;

            Assert.That(secondCalled, Is.True);
        }

        [Test]
        public void NullHandler_IsRejected()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => _bus.Subscribe<SampleEvent>(null));
        }

        [Test]
        public void Clear_RemovesAllSubscribers()
        {
            var count = 0;
            _bus.Subscribe<SampleEvent>(_ => count++);

            _bus.Clear();
            _bus.Publish(new SampleEvent(1));

            Assert.That(count, Is.Zero);
        }
    }
}
