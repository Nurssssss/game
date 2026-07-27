using System;
using NUnit.Framework;
using QonaevLife.Core;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Внутриигровое время и фазы суток (FR-021, FR-023).</summary>
    [TestFixture]
    public sealed class GameClockTests
    {
        private static GameClockSettings Settings => new()
        {
            minutesPerRealSecond = 60f, // одна реальная секунда = один игровой час
            morningStartHour = 6,
            dayStartHour = 11,
            eveningStartHour = 18,
            nightStartHour = 23,
            startHour = 8
        };

        [Test]
        public void NewClock_StartsAtConfiguredHour()
        {
            var clock = new GameClock(Settings);

            Assert.That(clock.Day, Is.EqualTo(1));
            Assert.That(clock.TimeOfDay.TotalHours, Is.EqualTo(8).Within(0.001));
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Morning));
        }

        [Test]
        public void InvalidSettings_AreRejected()
        {
            var invalid = Settings;
            invalid.eveningStartHour = 5; // раньше dayStartHour

            Assert.Throws<ArgumentException>(() => new GameClock(invalid));
        }

        [TestCase(8, DayPhase.Morning)]
        [TestCase(12, DayPhase.Day)]
        [TestCase(19, DayPhase.Evening)]
        [TestCase(23, DayPhase.Night)]
        [TestCase(3, DayPhase.Night)]
        public void Phase_MatchesHour(int hour, DayPhase expected)
        {
            var settings = Settings;
            settings.startHour = hour;

            var clock = new GameClock(settings);

            Assert.That(clock.Phase, Is.EqualTo(expected));
        }

        [Test]
        public void Tick_AdvancesTimeByConfiguredScale()
        {
            var clock = new GameClock(Settings);

            clock.Tick(2f); // два часа

            Assert.That(clock.TimeOfDay.TotalHours, Is.EqualTo(10).Within(0.001));
        }

        [Test]
        public void PhaseChanged_FiresOnceOnTransition()
        {
            var clock = new GameClock(Settings);
            var phases = new System.Collections.Generic.List<DayPhase>();
            clock.PhaseChanged += phases.Add;

            clock.Tick(3f); // 8:00 → 11:00, наступает день

            Assert.That(phases, Is.EqualTo(new[] { DayPhase.Day }));
        }

        [Test]
        public void CrossingMidnight_IncrementsDay()
        {
            var clock = new GameClock(Settings);
            var days = new System.Collections.Generic.List<int>();
            clock.DayChanged += days.Add;

            clock.Tick(16f); // 8:00 + 16ч = 0:00 следующего дня

            Assert.That(clock.Day, Is.EqualTo(2));
            Assert.That(days, Is.EqualTo(new[] { 2 }));
            Assert.That(clock.TimeOfDay.TotalHours, Is.EqualTo(0).Within(0.001));
        }

        [Test]
        public void Pause_StopsTime()
        {
            var clock = new GameClock(Settings);
            clock.Pause();

            clock.Tick(5f);

            Assert.That(clock.TimeOfDay.TotalHours, Is.EqualTo(8).Within(0.001));

            clock.Resume();
            clock.Tick(1f);

            Assert.That(clock.TimeOfDay.TotalHours, Is.EqualTo(9).Within(0.001));
        }

        /// <summary>Сон и такси перематывают время вперёд (FR-081).</summary>
        [Test]
        public void SkipMinutes_AdvancesTimeAndPhase()
        {
            var clock = new GameClock(Settings);

            clock.SkipMinutes(minutes: 10 * 60); // 8:00 → 18:00

            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Evening));
            Assert.That(clock.TimeOfDay.TotalHours, Is.EqualTo(18).Within(0.001));
        }

        /// <summary>FR-023: после загрузки время восстанавливается без скачка.</summary>
        [Test]
        public void RestoreState_RestoresDayPhaseAndTime()
        {
            var clock = new GameClock(Settings);

            clock.RestoreState(day: 4, minutesOfDay: 20 * 60);

            Assert.That(clock.Day, Is.EqualTo(4));
            Assert.That(clock.TimeOfDay.TotalHours, Is.EqualTo(20).Within(0.001));
            Assert.That(clock.Phase, Is.EqualTo(DayPhase.Evening));
        }

        [Test]
        public void RestoreState_RejectsOutOfRangeValues()
        {
            var clock = new GameClock(Settings);

            Assert.Throws<ArgumentOutOfRangeException>(
                () => clock.RestoreState(day: 0, minutesOfDay: 100));

            Assert.Throws<ArgumentOutOfRangeException>(
                () => clock.RestoreState(day: 1, minutesOfDay: 1440));
        }

        [Test]
        public void RestoreThenTick_ContinuesFromRestoredPoint()
        {
            var clock = new GameClock(Settings);
            clock.RestoreState(day: 2, minutesOfDay: 23 * 60 + 30); // 23:30

            clock.Tick(1f); // +1 час → 0:30 третьего дня

            Assert.That(clock.Day, Is.EqualTo(3));
            Assert.That(clock.TimeOfDay.TotalHours, Is.EqualTo(0.5).Within(0.001));
        }
    }
}
