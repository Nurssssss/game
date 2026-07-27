using System;

namespace QonaevLife.Core
{
    /// <summary>Фазы суток (FR-021). Влияют на освещение, расписания NPC и доступность смен.</summary>
    public enum DayPhase
    {
        Morning = 0,
        Day = 1,
        Evening = 2,
        Night = 3
    }

    /// <summary>
    /// Внутриигровое время. Единственный источник истины о времени для всех модулей:
    /// расписаний NPC, погоды, доступности работы и UI.
    /// </summary>
    public interface IGameClock
    {
        /// <summary>Прошедшее внутриигровое время с начала партии.</summary>
        TimeSpan Elapsed { get; }

        /// <summary>Номер игрового дня, начиная с 1.</summary>
        int Day { get; }

        /// <summary>Время внутри текущих суток.</summary>
        TimeSpan TimeOfDay { get; }

        DayPhase Phase { get; }

        /// <summary>Сколько внутриигровых минут проходит за одну реальную секунду.</summary>
        float MinutesPerRealSecond { get; }

        bool IsPaused { get; }

        event Action<DayPhase> PhaseChanged;
        event Action<int> DayChanged;

        void Pause();
        void Resume();
    }
}
