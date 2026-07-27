using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Core;
using QonaevLife.Player;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Потребности игрока (FR-060, FR-061, FR-064).</summary>
    [TestFixture]
    public sealed class NeedsServiceTests
    {
        private EventBus _eventBus;
        private NeedsService _service;

        private static List<NeedSettings> Settings => new()
        {
            new NeedSettings
            {
                needId = NeedIds.Hunger,
                decayPerGameMinute = 1f,
                criticalThreshold = 20f,
                startValue = 80f
            },
            new NeedSettings
            {
                needId = NeedIds.Energy,
                decayPerGameMinute = 0f,
                criticalThreshold = 10f,
                startValue = 50f
            }
        };

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _service = new NeedsService(_eventBus, Settings);
        }

        [Test]
        public void NewService_UsesStartValues()
        {
            Assert.That(_service.GetValue(NeedIds.Hunger), Is.EqualTo(80f));
            Assert.That(_service.GetValue(NeedIds.Energy), Is.EqualTo(50f));
        }

        [Test]
        public void EmptySettings_AreRejected()
        {
            Assert.Throws<System.ArgumentException>(
                () => new NeedsService(_eventBus, new List<NeedSettings>()));
        }

        [Test]
        public void AdvanceMinutes_DecaysOnlyNeedsWithDecayRate()
        {
            _service.AdvanceMinutes(10d);

            Assert.That(_service.GetValue(NeedIds.Hunger), Is.EqualTo(70f).Within(0.001f));
            Assert.That(_service.GetValue(NeedIds.Energy), Is.EqualTo(50f),
                "Потребность без скорости падения не меняется.");
        }

        [Test]
        public void Modify_RestoresNeedAndClampsAtMax()
        {
            _service.Modify(NeedIds.Hunger, 50f);

            Assert.That(_service.GetValue(NeedIds.Hunger), Is.EqualTo(NeedsService.MaxValue));
        }

        /// <summary>FR-064: значение не уходит ниже нуля и не даёт необратимого проигрыша.</summary>
        [Test]
        public void Decay_ClampsAtZero()
        {
            _service.AdvanceMinutes(10_000d);

            Assert.That(_service.GetValue(NeedIds.Hunger), Is.EqualTo(NeedsService.MinValue));

            // После восстановления игрок продолжает играть.
            _service.Modify(NeedIds.Hunger, 40f);
            Assert.That(_service.GetValue(NeedIds.Hunger), Is.EqualTo(40f));
        }

        [Test]
        public void IsCritical_ReflectsThreshold()
        {
            Assert.That(_service.IsCritical(NeedIds.Hunger), Is.False);

            _service.AdvanceMinutes(65d); // 80 → 15, порог 20

            Assert.That(_service.IsCritical(NeedIds.Hunger), Is.True);
        }

        [Test]
        public void NeedChangedEvent_ReportsCriticalState()
        {
            var events = new List<NeedChangedEvent>();
            _eventBus.Subscribe<NeedChangedEvent>(events.Add);

            _service.AdvanceMinutes(65d);

            Assert.That(events, Has.Count.EqualTo(1));
            Assert.That(events[0].NeedId, Is.EqualTo(NeedIds.Hunger));
            Assert.That(events[0].PreviousValue, Is.EqualTo(80f));
            Assert.That(events[0].NewValue, Is.EqualTo(15f).Within(0.001f));
            Assert.That(events[0].IsCritical, Is.True);
        }

        [Test]
        public void UnchangedValue_DoesNotPublishEvent()
        {
            var events = 0;
            _eventBus.Subscribe<NeedChangedEvent>(_ => events++);

            _service.Modify(NeedIds.Hunger, 0f);
            _service.AdvanceMinutes(0d);

            Assert.That(events, Is.Zero);
        }

        [Test]
        public void ModifyingNeedFromHandler_IsSafe()
        {
            // Обработчик события трогает другую потребность — обход не должен падать.
            _eventBus.Subscribe<NeedChangedEvent>(e =>
            {
                if (e.NeedId == NeedIds.Hunger)
                    _service.Modify(NeedIds.Energy, -5f);
            });

            Assert.DoesNotThrow(() => _service.AdvanceMinutes(10d));

            Assert.That(_service.GetValue(NeedIds.Energy), Is.EqualTo(45f).Within(0.001f));
        }

        [Test]
        public void UnknownNeed_IsIgnored()
        {
            Assert.DoesNotThrow(() => _service.Modify("unknown_need", -10f));
            Assert.That(_service.GetValue("unknown_need"), Is.EqualTo(NeedsService.MaxValue));
        }

        [Test]
        public void CaptureAndRestore_PreservesValues()
        {
            _service.AdvanceMinutes(30d);

            var data = new PlayerSaveData();
            _service.CaptureState(data);

            var restored = new NeedsService(_eventBus, Settings);
            restored.RestoreState(data);

            Assert.That(restored.GetValue(NeedIds.Hunger),
                Is.EqualTo(_service.GetValue(NeedIds.Hunger)).Within(0.001f));
        }

        [Test]
        public void RestoreState_ClampsOutOfRangeValues()
        {
            var data = new PlayerSaveData();
            data.needs.Add(new NeedValueData { needId = NeedIds.Hunger, value = 500f });

            _service.RestoreState(data);

            Assert.That(_service.GetValue(NeedIds.Hunger), Is.EqualTo(NeedsService.MaxValue));
        }
    }
}
