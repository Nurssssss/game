using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Content
{
    public enum LocationKind
    {
        Apartment = 0,
        Shop = 1,
        Cafe = 2,
        GasStation = 3,
        Park = 4,
        BusStop = 5,
        WorkHub = 6,
        Street = 7,
        Landmark = 8
    }

    /// <summary>
    /// Точка интереса района (п. 6 ТЗ). Координата хранится как маркер сектора,
    /// а не как ссылка на объект сцены (п. 7 ТЗ).
    /// </summary>
    [CreateAssetMenu(
        fileName = "Location_",
        menuName = "Qonaev Life/Мир/Локация",
        order = 60)]
    public sealed class LocationDefinition : ContentDefinition
    {
        [Header("Отображение")]
        [SerializeField] private string displayNameKey = string.Empty;
        [SerializeField] private LocationKind kind = LocationKind.Street;

        [Header("Размещение")]
        [SerializeField] [Tooltip("ID сектора, которому принадлежит локация.")]
        private string sectorId = string.Empty;

        [SerializeField] [Tooltip("Позиция маркера в координатах сектора.")]
        private Vector3 markerPosition;

        [SerializeField] [Tooltip("Является ли локация интерьером с отдельной загрузкой (FR-024).")]
        private bool isInterior;

        [SerializeField] [Tooltip("Адресуемый ключ сцены/префаба интерьера.")]
        private string interiorAddressableKey = string.Empty;

        [Header("Часы работы")]
        [SerializeField] [Tooltip("Работает круглосуточно.")]
        private bool alwaysOpen = true;

        [SerializeField] [Tooltip("Час открытия при отключённом alwaysOpen.")] [Range(0, 23)]
        private int openHour;

        [SerializeField] [Tooltip("Час закрытия при отключённом alwaysOpen.")] [Range(0, 23)]
        private int closeHour = 23;

        [Header("Доступность")]
        [SerializeField] [Tooltip("Открыта ли локация с начала игры.")]
        private bool discoveredFromStart;

        [SerializeField] [Tooltip("Можно ли использовать как пункт такси (FR-081).")]
        private bool isTaxiDestination = true;

        public string DisplayNameKey => displayNameKey;
        public LocationKind Kind => kind;
        public string SectorId => sectorId;
        public Vector3 MarkerPosition => markerPosition;
        public bool IsInterior => isInterior;
        public string InteriorAddressableKey => interiorAddressableKey;
        public bool AlwaysOpen => alwaysOpen;
        public int OpenHour => openHour;
        public int CloseHour => closeHour;
        public bool DiscoveredFromStart => discoveredFromStart;
        public bool IsTaxiDestination => isTaxiDestination;

        /// <summary>Открыта ли локация в указанный час (FR-073 — не выдавать цель в закрытое время).</summary>
        public bool IsOpenAtHour(int hour)
        {
            if (alwaysOpen)
                return true;

            // Интервал может переходить через полночь: например, 22 → 4.
            return openHour <= closeHour
                ? hour >= openHour && hour < closeHour
                : hour >= openHour || hour < closeHour;
        }

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);

            if (string.IsNullOrWhiteSpace(displayNameKey))
                errors.Add($"{name}: не заполнен ключ названия.");

            if (string.IsNullOrWhiteSpace(sectorId))
                errors.Add($"{name}: не указан сектор (FR-020).");

            if (isInterior && string.IsNullOrWhiteSpace(interiorAddressableKey))
                errors.Add($"{name}: интерьер без адресуемого ключа загрузки (FR-024).");

            if (!alwaysOpen && openHour == closeHour)
                errors.Add($"{name}: часы открытия и закрытия совпадают — локация никогда не работает.");
        }
    }
}
