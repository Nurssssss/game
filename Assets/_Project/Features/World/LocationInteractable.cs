using QonaevLife.Core;
using QonaevLife.Player;
using UnityEngine;

namespace QonaevLife.World
{
    /// <summary>Игрок взаимодействовал с точкой интереса.</summary>
    public readonly struct LocationInteractedEvent : IGameEvent
    {
        public LocationInteractedEvent(string locationId, InteractionKind kind)
        {
            LocationId = locationId;
            Kind = kind;
        }

        public string LocationId { get; }
        public InteractionKind Kind { get; }
    }

    /// <summary>
    /// Интерактивный объект точки интереса на сцене (FR-012). Сам ничего не
    /// решает: публикует событие, а реакцию выбирают модули работы, магазина
    /// или диалога. Доступность берётся из часов работы локации.
    /// </summary>
    [RequireComponent(typeof(Collider))]
    public sealed class LocationInteractable : MonoBehaviour, IInteractable
    {
        [Header("Привязка к контенту")]
        [SerializeField] [Tooltip("ID локации из ContentDatabase.")]
        private string locationId = string.Empty;

        [SerializeField] private InteractionKind kind = InteractionKind.Terminal;

        [SerializeField] [Tooltip("Ключ локализации подсказки.")]
        private string promptKey = "prompt.interact";

        private LocationRegistry _locations;

        public string InteractableId => $"interactable_{locationId}";

        public InteractionKind Kind => kind;

        public string PromptKey => promptKey;

        public string LocationId => locationId;

        /// <summary>
        /// Доступно, если локация работает сейчас. Без реестра объект считается
        /// доступным: сцену можно открыть и проверить без запущенной сессии.
        /// </summary>
        public bool IsAvailable
            => _locations == null || _locations.IsOpenNow(locationId);

        public string UnavailableReasonKey
            => IsAvailable ? string.Empty : "prompt.closed";

        /// <summary>Передаёт реестр локаций, чтобы объект знал часы работы.</summary>
        public void Bind(LocationRegistry locations) => _locations = locations;

        public void Interact(IInteractionContext context)
        {
            if (context?.EventBus == null || !IsAvailable)
                return;

            // Посещение точки открывает её на карте (FR-092).
            _locations?.Discover(locationId);

            context.EventBus.Publish(new LocationInteractedEvent(locationId, kind));
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(string id, InteractionKind interactionKind, string prompt)
        {
            locationId = id;
            kind = interactionKind;
            promptKey = prompt;
        }
    }
}
