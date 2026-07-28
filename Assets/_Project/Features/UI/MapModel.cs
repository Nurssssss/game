using System;
using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.World;
using UnityEngine;

namespace QonaevLife.UI
{
    /// <summary>Что означает метка на карте — определяет иконку и подпись.</summary>
    public enum MapMarkerKind
    {
        Player = 0,
        Objective = 1,
        Location = 2
    }

    /// <summary>Метка карты в нормализованных координатах [0, 1].</summary>
    public readonly struct MapMarker
    {
        public MapMarker(string id, MapMarkerKind kind, Vector2 normalizedPosition,
            string labelKey, bool isOpen)
        {
            Id = id;
            Kind = kind;
            NormalizedPosition = normalizedPosition;
            LabelKey = labelKey;
            IsOpen = isOpen;
        }

        public string Id { get; }
        public MapMarkerKind Kind { get; }

        /// <summary>Позиция на карте: (0,0) — левый нижний угол, (1,1) — правый верхний.</summary>
        public Vector2 NormalizedPosition { get; }

        public string LabelKey { get; }

        /// <summary>Работает ли локация сейчас — на карте показывается иконкой.</summary>
        public bool IsOpen { get; }
    }

    /// <summary>
    /// Данные карты (FR-092): открытые точки интереса, позиция игрока, текущая
    /// цель и линия маршрута. Не рисует ничего сам — отдаёт метки в
    /// нормализованных координатах, чтобы отрисовка не зависела от размера
    /// панели. Логика отделена от Unity и покрывается тестами.
    /// </summary>
    public sealed class MapModel
    {
        private readonly LocationRegistry _locations;
        private readonly List<MapMarker> _markers = new();

        /// <summary>Половина стороны области, отображаемой на карте, в метрах.</summary>
        private readonly float _worldExtent;

        public MapModel(LocationRegistry locations, float worldExtent = 60f)
        {
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));

            if (worldExtent <= 0f)
                throw new ArgumentOutOfRangeException(nameof(worldExtent));

            _worldExtent = worldExtent;
        }

        public IReadOnlyList<MapMarker> Markers => _markers;

        /// <summary>Позиция игрока на карте.</summary>
        public Vector2 PlayerPosition { get; private set; }

        /// <summary>Позиция текущей цели или null, если цели нет.</summary>
        public Vector2? ObjectivePosition { get; private set; }

        /// <summary>
        /// Пересобирает метки. Маркер цели ставится в фактическую позицию
        /// интерактивного объекта, а не приблизительно (FR-092).
        /// </summary>
        public void Refresh(Vector3 playerWorldPosition, string objectiveLocationId)
        {
            _markers.Clear();
            ObjectivePosition = null;

            PlayerPosition = ToNormalized(playerWorldPosition);
            _markers.Add(new MapMarker(
                "player", MapMarkerKind.Player, PlayerPosition, "map.you", isOpen: true));

            // Карта показывает все открытые точки, а не только пункты такси,
            // поэтому обходим список открытых напрямую.
            foreach (var locationId in _locations.DiscoveredLocationIds)
            {
                if (!_locations.TryGet(locationId, out var definition))
                    continue;

                var isObjective = string.Equals(
                    locationId, objectiveLocationId, StringComparison.Ordinal);

                var normalized = ToNormalized(definition.MarkerPosition);

                if (isObjective)
                    ObjectivePosition = normalized;

                _markers.Add(new MapMarker(
                    locationId,
                    isObjective ? MapMarkerKind.Objective : MapMarkerKind.Location,
                    normalized,
                    definition.DisplayNameKey,
                    _locations.IsOpenNow(locationId)));
            }
        }

        /// <summary>
        /// Точки линии маршрута — упрощённый вид по ТЗ: прямая от игрока к цели.
        /// Возвращает пустой список, если цели нет.
        /// </summary>
        public bool TryGetRoute(out Vector2 from, out Vector2 to)
        {
            if (!ObjectivePosition.HasValue)
            {
                from = default;
                to = default;
                return false;
            }

            from = PlayerPosition;
            to = ObjectivePosition.Value;
            return true;
        }

        /// <summary>
        /// Мировые координаты в нормализованные [0, 1]. Значения за границей
        /// области зажимаются к краю: метка остаётся видимой у рамки, а не
        /// исчезает за пределами панели.
        /// </summary>
        public Vector2 ToNormalized(Vector3 worldPosition)
        {
            var x = Mathf.InverseLerp(-_worldExtent, _worldExtent, worldPosition.x);
            var y = Mathf.InverseLerp(-_worldExtent, _worldExtent, worldPosition.z);

            return new Vector2(Mathf.Clamp01(x), Mathf.Clamp01(y));
        }
    }
}
