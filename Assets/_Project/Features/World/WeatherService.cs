using System;
using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.World
{
    /// <summary>Минимальный набор погодных состояний MVP (FR-022).</summary>
    public enum WeatherState
    {
        Clear = 0,
        Cloudy = 1,
        Rain = 2
    }

    public readonly struct WeatherChangedEvent : IGameEvent
    {
        public WeatherChangedEvent(WeatherState previous, WeatherState current)
        {
            Previous = previous;
            Current = current;
        }

        public WeatherState Previous { get; }
        public WeatherState Current { get; }
    }

    [Serializable]
    public struct WeatherSettings
    {
        [UnityEngine.Tooltip("Минимальная длительность одного состояния во внутриигровых минутах.")]
        public float minDurationMinutes;

        [UnityEngine.Tooltip("Максимальная длительность одного состояния.")]
        public float maxDurationMinutes;

        public static WeatherSettings Default => new()
        {
            minDurationMinutes = 120f,
            maxDurationMinutes = 480f
        };

        public bool IsValid()
            => minDurationMinutes > 0f && maxDurationMinutes >= minDurationMinutes;
    }

    /// <summary>
    /// Погода вертикального среза (FR-022, FR-023). Использует детерминированный
    /// генератор с сохраняемым seed, поэтому после загрузки не происходит
    /// необъяснимого скачка состояния.
    /// </summary>
    public sealed class WeatherService : IGameService
    {
        private readonly IEventBus _eventBus;
        private readonly WeatherSettings _settings;
        private readonly List<WeatherState> _states = new()
        {
            WeatherState.Clear,
            WeatherState.Cloudy,
            WeatherState.Rain
        };

        private Random _random;
        private int _seed;
        private double _minutesUntilChange;

        public WeatherService(IEventBus eventBus, WeatherSettings settings, int seed)
        {
            if (!settings.IsValid())
                throw new ArgumentException("Некорректные настройки погоды.", nameof(settings));

            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _settings = settings;
            _seed = seed;
            _random = new Random(seed);

            Current = WeatherState.Clear;
            _minutesUntilChange = RollDuration();
        }

        public WeatherState Current { get; private set; }

        public void Initialize()
        {
        }

        public void Shutdown()
        {
        }

        public void AdvanceMinutes(double gameMinutes)
        {
            if (gameMinutes <= 0d)
                return;

            _minutesUntilChange -= gameMinutes;

            // Длинная перемотка (сон) может перекрыть несколько интервалов.
            while (_minutesUntilChange <= 0d)
            {
                SwitchToNextState();
                _minutesUntilChange += RollDuration();
            }
        }

        public void RestoreState(WorldSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            var previous = Current;

            Current = Enum.TryParse<WeatherState>(data.weatherId, out var parsed)
                ? parsed
                : WeatherState.Clear;

            // Свежий генератор от сохранённого состояния: следующая смена погоды
            // предсказуема и не зависит от того, сколько шагов прошло до сохранения.
            _random = new Random(_seed);
            _minutesUntilChange = RollDuration();

            if (Current != previous)
                _eventBus.Publish(new WeatherChangedEvent(previous, Current));
        }

        public void CaptureState(WorldSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.weatherId = Current.ToString();
        }

        private void SwitchToNextState()
        {
            var previous = Current;

            // Выбираем среди состояний, отличных от текущего, чтобы смена была заметна.
            var candidateIndex = _random.Next(_states.Count - 1);
            foreach (var state in _states)
            {
                if (state == previous)
                    continue;

                if (candidateIndex == 0)
                {
                    Current = state;
                    break;
                }

                candidateIndex--;
            }

            if (Current != previous)
                _eventBus.Publish(new WeatherChangedEvent(previous, Current));
        }

        private double RollDuration()
            => _settings.minDurationMinutes
               + _random.NextDouble() * (_settings.maxDurationMinutes - _settings.minDurationMinutes);
    }
}
