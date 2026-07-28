using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.UI
{
    /// <summary>
    /// Отрисовка карты (FR-092): открытые точки, игрок, цель и линия маршрута.
    /// Метки размещаются по нормализованным координатам из <see cref="MapModel"/>,
    /// поэтому карта корректна при любом размере панели.
    /// </summary>
    public sealed class MapView : MonoBehaviour
    {
        [Header("Контейнеры")]
        [SerializeField] [Tooltip("Область карты — задаёт систему координат.")]
        private RectTransform mapArea;

        [SerializeField] [Tooltip("Шаблон метки. Отключён на сцене.")]
        private RectTransform markerTemplate;

        [SerializeField] [Tooltip("Линия маршрута — прямая от игрока к цели.")]
        private RectTransform routeLine;

        [Header("Цвета")]
        [SerializeField] private Color playerColor = new(0.35f, 0.75f, 1f);
        [SerializeField] private Color objectiveColor = new(1f, 0.8f, 0.3f);
        [SerializeField] private Color openColor = new(0.5f, 0.85f, 0.5f);
        [SerializeField] private Color closedColor = new(0.6f, 0.6f, 0.65f);

        private readonly List<RectTransform> _markers = new();

        private MapModel _model;
        private ILocalizedText _text;
        private System.Func<Vector3> _playerPositionProvider;
        private System.Func<string> _objectiveProvider;

        public void Bind(MapModel model, ILocalizedText text,
            System.Func<Vector3> playerPositionProvider,
            System.Func<string> objectiveProvider)
        {
            _model = model;
            _text = text;
            _playerPositionProvider = playerPositionProvider;
            _objectiveProvider = objectiveProvider;
        }

        /// <summary>Пересобирает карту. Вызывается при открытии раздела.</summary>
        public void Refresh()
        {
            if (_model == null || mapArea == null || markerTemplate == null)
                return;

            var playerPosition = _playerPositionProvider?.Invoke() ?? Vector3.zero;
            var objectiveId = _objectiveProvider?.Invoke();

            _model.Refresh(playerPosition, objectiveId);

            EnsureMarkerCount(_model.Markers.Count);

            var size = mapArea.rect.size;

            for (var i = 0; i < _markers.Count; i++)
            {
                var marker = _markers[i];

                if (i >= _model.Markers.Count)
                {
                    marker.gameObject.SetActive(false);
                    continue;
                }

                var data = _model.Markers[i];
                marker.gameObject.SetActive(true);

                // Нормализованные координаты в пиксели области карты.
                marker.anchoredPosition = new Vector2(
                    (data.NormalizedPosition.x - 0.5f) * size.x,
                    (data.NormalizedPosition.y - 0.5f) * size.y);

                ApplyMarkerStyle(marker, data);
            }

            UpdateRoute(size);
        }

        /// <summary>
        /// Вид метки. Тип обозначается символом, а не только цветом: игрок
        /// должен различать метки без цветовой дифференциации (FR-095).
        /// </summary>
        private void ApplyMarkerStyle(RectTransform marker, MapMarker data)
        {
            var image = marker.GetComponent<Image>();
            var label = marker.GetComponentInChildren<TMP_Text>();

            var color = data.Kind switch
            {
                MapMarkerKind.Player => playerColor,
                MapMarkerKind.Objective => objectiveColor,
                _ => data.IsOpen ? openColor : closedColor
            };

            if (image != null)
                image.color = color;

            if (label == null)
                return;

            var symbol = data.Kind switch
            {
                MapMarkerKind.Player => "●",
                MapMarkerKind.Objective => "★",
                _ => data.IsOpen ? "▪" : "×"
            };

            var name = _text != null ? _text.Resolve(data.LabelKey) : data.LabelKey;

            // У игрока подпись не нужна — его метка и так узнаваема.
            label.text = data.Kind == MapMarkerKind.Player ? symbol : $"{symbol} {name}";
            label.color = color;
        }

        /// <summary>Линия маршрута: поворачивается и растягивается между точками.</summary>
        private void UpdateRoute(Vector2 size)
        {
            if (routeLine == null)
                return;

            if (!_model.TryGetRoute(out var from, out var to))
            {
                routeLine.gameObject.SetActive(false);
                return;
            }

            var start = new Vector2((from.x - 0.5f) * size.x, (from.y - 0.5f) * size.y);
            var end = new Vector2((to.x - 0.5f) * size.x, (to.y - 0.5f) * size.y);
            var delta = end - start;

            if (delta.sqrMagnitude < 0.01f)
            {
                // Игрок уже на цели — линия нулевой длины выглядела бы артефактом.
                routeLine.gameObject.SetActive(false);
                return;
            }

            routeLine.gameObject.SetActive(true);
            routeLine.anchoredPosition = start + delta * 0.5f;
            routeLine.sizeDelta = new Vector2(delta.magnitude, routeLine.sizeDelta.y);
            routeLine.localRotation = Quaternion.Euler(
                0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
        }

        private void EnsureMarkerCount(int required)
        {
            while (_markers.Count < required)
            {
                var instance = Instantiate(markerTemplate, mapArea);
                instance.gameObject.SetActive(true);
                _markers.Add(instance);
            }
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(RectTransform area, RectTransform template, RectTransform route)
        {
            mapArea = area;
            markerTemplate = template;
            routeLine = route;
        }
    }
}
