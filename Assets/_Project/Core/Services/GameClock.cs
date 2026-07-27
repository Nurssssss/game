using System;

namespace QonaevLife.Core
{
    /// <summary>Настраиваемые границы фаз суток и скорость времени (п. 10 ТЗ).</summary>
    [Serializable]
    public struct GameClockSettings
    {
        /// <summary>Внутриигровых минут за реальную секунду.</summary>
        public float minutesPerRealSecond;

        /// <summary>Час начала утра.</summary>
        public int morningStartHour;

        /// <summary>Час начала дня.</summary>
        public int dayStartHour;

        /// <summary>Час начала вечера.</summary>
        public int eveningStartHour;

        /// <summary>Час начала ночи.</summary>
        public int nightStartHour;

        /// <summary>Час начала партии в новой игре.</summary>
        public int startHour;

        public static GameClockSettings Default => new()
        {
            minutesPerRealSecond = 1f,
            morningStartHour = 6,
            dayStartHour = 11,
            eveningStartHour = 18,
            nightStartHour = 23,
            startHour = 8
        };

        /// <summary>Проверяет, что границы фаз строго возрастают внутри суток.</summary>
        public bool IsValid()
            => minutesPerRealSecond > 0f
               && morningStartHour >= 0
               && morningStartHour < dayStartHour
               && dayStartHour < eveningStartHour
               && eveningStartHour < nightStartHour
               && nightStartHour < 24
               && startHour >= 0
               && startHour < 24;
    }

    /// <summary>
    /// Внутриигровые часы. Продвигаются извне через <see cref="Tick"/>, что делает
    /// их полностью тестируемыми без Unity-плеера (NFR-011).
    /// </summary>
    public sealed class GameClock : IGameClock, IGameService
    {
        private const double MinutesPerDay = 24d * 60d;

        private GameClockSettings _settings;
        private double _totalMinutes;
        private DayPhase _phase;
        private int _day;

        public GameClock(GameClockSettings settings)
        {
            if (!settings.IsValid())
                throw new ArgumentException(
                    "Некорректные настройки часов: границы фаз должны возрастать, " +
                    "а скорость времени быть положительной.", nameof(settings));

            _settings = settings;
            _totalMinutes = settings.startHour * 60d;
            _day = 1;
            _phase = ResolvePhase(settings.startHour);
        }

        public TimeSpan Elapsed => TimeSpan.FromMinutes(_totalMinutes);

        public int Day => _day;

        public TimeSpan TimeOfDay => TimeSpan.FromMinutes(_totalMinutes % MinutesPerDay);

        public DayPhase Phase => _phase;

        public float MinutesPerRealSecond => _settings.minutesPerRealSecond;

        public bool IsPaused { get; private set; }

        public event Action<DayPhase> PhaseChanged;
        public event Action<int> DayChanged;

        public void Initialize()
        {
        }

        public void Shutdown()
        {
            PhaseChanged = null;
            DayChanged = null;
        }

        public void Pause() => IsPaused = true;

        public void Resume() => IsPaused = false;

        /// <summary>Продвигает время на <paramref name="realDeltaSeconds"/> реальных секунд.</summary>
        public void Tick(float realDeltaSeconds)
        {
            if (IsPaused || realDeltaSeconds <= 0f)
                return;

            AdvanceMinutes(realDeltaSeconds * _settings.minutesPerRealSecond);
        }

        /// <summary>
        /// Перематывает время вперёд на заданное число внутриигровых минут.
        /// Используется сном, поездкой на такси и завершением смены.
        /// </summary>
        public void SkipMinutes(double minutes)
        {
            if (minutes <= 0d)
                return;

            AdvanceMinutes(minutes);
        }

        /// <summary>Восстанавливает состояние из сохранения (FR-023).</summary>
        public void RestoreState(int day, double minutesOfDay)
        {
            if (day < 1)
                throw new ArgumentOutOfRangeException(nameof(day), "День не может быть меньше 1.");

            if (minutesOfDay < 0d || minutesOfDay >= MinutesPerDay)
                throw new ArgumentOutOfRangeException(
                    nameof(minutesOfDay), "Время суток должно быть в диапазоне [0, 1440).");

            _day = day;
            _totalMinutes = (day - 1) * MinutesPerDay + minutesOfDay;
            _phase = ResolvePhase((int)(minutesOfDay / 60d));
        }

        private void AdvanceMinutes(double minutes)
        {
            var previousDay = _day;
            var previousPhase = _phase;

            _totalMinutes += minutes;
            _day = (int)(_totalMinutes / MinutesPerDay) + 1;
            _phase = ResolvePhase((int)(_totalMinutes % MinutesPerDay / 60d));

            if (_day != previousDay)
                DayChanged?.Invoke(_day);

            if (_phase != previousPhase)
                PhaseChanged?.Invoke(_phase);
        }

        private DayPhase ResolvePhase(int hour)
        {
            if (hour >= _settings.nightStartHour || hour < _settings.morningStartHour)
                return DayPhase.Night;

            if (hour < _settings.dayStartHour)
                return DayPhase.Morning;

            return hour < _settings.eveningStartHour ? DayPhase.Day : DayPhase.Evening;
        }
    }
}
