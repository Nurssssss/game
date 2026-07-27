using System;
using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.Player
{
    /// <summary>Идентификаторы потребностей MVP (FR-060).</summary>
    public static class NeedIds
    {
        public const string Hunger = "hunger";
        public const string Energy = "energy";
        public const string Fatigue = "fatigue";
        public const string Mood = "mood";

        public static readonly string[] All = { Hunger, Energy, Fatigue, Mood };
    }

    /// <summary>Как быстро потребность падает и когда считается критической.</summary>
    [Serializable]
    public struct NeedSettings
    {
        public string needId;

        [UnityEngine.Tooltip("Падение за одну внутриигровую минуту.")]
        public float decayPerGameMinute;

        [UnityEngine.Tooltip("Порог, ниже которого игрок получает предупреждение (FR-064).")]
        public float criticalThreshold;

        [UnityEngine.Tooltip("Стартовое значение в новой игре.")]
        public float startValue;
    }

    public readonly struct NeedChangedEvent : IGameEvent
    {
        public NeedChangedEvent(string needId, float previousValue, float newValue, bool isCritical)
        {
            NeedId = needId;
            PreviousValue = previousValue;
            NewValue = newValue;
            IsCritical = isCritical;
        }

        public string NeedId { get; }
        public float PreviousValue { get; }
        public float NewValue { get; }
        public bool IsCritical { get; }
    }

    /// <summary>
    /// Потребности игрока (FR-060, FR-061). Значение зажимается в [0, 100]:
    /// критически низкая потребность даёт предупреждение, но никогда не приводит
    /// к необратимому проигрышу и не отнимает сохранение (FR-064).
    /// </summary>
    public sealed class NeedsService : IGameService
    {
        public const float MinValue = 0f;
        public const float MaxValue = 100f;

        private readonly IEventBus _eventBus;
        private readonly Dictionary<string, NeedSettings> _settings = new();
        private readonly Dictionary<string, float> _values = new();
        private readonly List<NeedSettings> _decayBuffer = new();

        public NeedsService(IEventBus eventBus, IEnumerable<NeedSettings> settings)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));

            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            foreach (var entry in settings)
            {
                if (string.IsNullOrWhiteSpace(entry.needId))
                    throw new ArgumentException("Найдена настройка потребности без needId.");

                _settings[entry.needId] = entry;
                _values[entry.needId] = Clamp(entry.startValue);
            }

            if (_settings.Count == 0)
                throw new ArgumentException("Не задана ни одна потребность.", nameof(settings));
        }

        public IReadOnlyDictionary<string, float> Values => _values;

        public void Initialize()
        {
        }

        public void Shutdown() => _values.Clear();

        public float GetValue(string needId)
            => _values.TryGetValue(needId, out var value) ? value : MaxValue;

        public bool IsCritical(string needId)
            => _settings.TryGetValue(needId, out var settings)
               && GetValue(needId) <= settings.criticalThreshold;

        /// <summary>Естественное падение потребностей за прошедшее внутриигровое время.</summary>
        public void AdvanceMinutes(double gameMinutes)
        {
            if (gameMinutes <= 0d)
                return;

            // Копия ключей: Modify публикует событие, а обработчик вправе
            // изменить другую потребность, что тронет коллекцию во время обхода.
            _decayBuffer.Clear();
            foreach (var settings in _settings.Values)
            {
                if (settings.decayPerGameMinute != 0f)
                    _decayBuffer.Add(settings);
            }

            foreach (var settings in _decayBuffer)
                Modify(settings.needId, (float)(-settings.decayPerGameMinute * gameMinutes));
        }

        /// <summary>Изменяет потребность и публикует событие, если значение изменилось.</summary>
        public void Modify(string needId, float delta)
        {
            if (string.IsNullOrWhiteSpace(needId) || delta == 0f)
                return;

            if (!_values.TryGetValue(needId, out var previous))
                return;

            var updated = Clamp(previous + delta);
            if (Math.Abs(updated - previous) < float.Epsilon)
                return;

            _values[needId] = updated;
            _eventBus.Publish(new NeedChangedEvent(needId, previous, updated, IsCritical(needId)));
        }

        public void RestoreState(PlayerSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            if (data.needs == null)
                return;

            foreach (var entry in data.needs)
            {
                if (entry == null || !_values.ContainsKey(entry.needId))
                    continue;

                _values[entry.needId] = Clamp(entry.value);
            }
        }

        public void CaptureState(PlayerSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.needs.Clear();

            foreach (var pair in _values)
                data.needs.Add(new NeedValueData { needId = pair.Key, value = pair.Value });
        }

        private static float Clamp(float value) => Math.Clamp(value, MinValue, MaxValue);
    }
}
