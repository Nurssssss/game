using System;
using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.Core;

namespace QonaevLife.World
{
    public readonly struct LocationDiscoveredEvent : IGameEvent
    {
        public LocationDiscoveredEvent(string locationId) => LocationId = locationId;
        public string LocationId { get; }
    }

    /// <summary>
    /// Реестр точек интереса района. Отвечает за то, какие локации игрок открыл
    /// и какие доступны сейчас: карта, цели заданий и пункты такси спрашивают
    /// именно его, а не сцену (FR-092, FR-081).
    /// </summary>
    public sealed class LocationRegistry : IGameService
    {
        private readonly ContentDatabase _content;
        private readonly IEventBus _eventBus;
        private readonly IGameClock _clock;
        private readonly HashSet<string> _discovered = new();

        public LocationRegistry(ContentDatabase content, IEventBus eventBus, IGameClock clock)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        }

        public IReadOnlyCollection<string> DiscoveredLocationIds => _discovered;

        public void Initialize()
        {
            // Локации, открытые с начала игры, доступны до первой прогулки.
            foreach (var location in _content.Locations)
            {
                if (location != null && location.DiscoveredFromStart)
                    _discovered.Add(location.Id);
            }
        }

        public void Shutdown() => _discovered.Clear();

        public bool IsDiscovered(string locationId)
            => !string.IsNullOrWhiteSpace(locationId) && _discovered.Contains(locationId);

        /// <summary>Открывает локацию. Повторный вызов не публикует событие снова.</summary>
        public bool Discover(string locationId)
        {
            if (string.IsNullOrWhiteSpace(locationId))
                return false;

            if (!_content.TryGetLocation(locationId, out _))
                return false;

            if (!_discovered.Add(locationId))
                return false;

            _eventBus.Publish(new LocationDiscoveredEvent(locationId));
            return true;
        }

        public bool TryGet(string locationId, out LocationDefinition definition)
            => _content.TryGetLocation(locationId, out definition);

        /// <summary>Открыта ли локация в текущий внутриигровой час.</summary>
        public bool IsOpenNow(string locationId)
            => _content.TryGetLocation(locationId, out var definition)
               && definition.IsOpenAtHour((int)_clock.TimeOfDay.TotalHours);

        /// <summary>
        /// Можно ли выдать локацию как цель задания: она существует, открыта
        /// игроком и работает сейчас (FR-073).
        /// </summary>
        public bool IsValidObjectiveTarget(string locationId)
            => IsDiscovered(locationId) && IsOpenNow(locationId);

        /// <summary>Пункты, доступные для поездки на такси (FR-081).</summary>
        public void CollectTaxiDestinations(List<LocationDefinition> buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException(nameof(buffer));

            buffer.Clear();

            foreach (var location in _content.Locations)
            {
                if (location == null || !location.IsTaxiDestination)
                    continue;

                if (IsDiscovered(location.Id))
                    buffer.Add(location);
            }
        }

        public void RestoreState(WorldSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _discovered.Clear();
            Initialize();

            if (data.discoveredLocationIds == null)
                return;

            foreach (var id in data.discoveredLocationIds)
            {
                // Локация могла исчезнуть из контента — молча пропускаем,
                // чтобы старое сохранение не считалось повреждённым.
                if (!string.IsNullOrWhiteSpace(id) && _content.TryGetLocation(id, out _))
                    _discovered.Add(id);
            }
        }

        public void CaptureState(WorldSaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.discoveredLocationIds.Clear();
            data.discoveredLocationIds.AddRange(_discovered);
        }
    }
}
