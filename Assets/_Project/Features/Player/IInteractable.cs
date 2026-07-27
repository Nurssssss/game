using QonaevLife.Core;

namespace QonaevLife.Player
{
    /// <summary>Что за действие предлагает объект — определяет иконку подсказки (FR-012).</summary>
    public enum InteractionKind
    {
        Generic = 0,
        Door = 1,
        Npc = 2,
        Shop = 3,
        Vehicle = 4,
        Bed = 5,
        Terminal = 6,
        Pickup = 7
    }

    /// <summary>
    /// Единый интерфейс взаимодействия (FR-012). Все интерактивные объекты
    /// реализуют его, поэтому игрок видит одинаковую подсказку у двери, NPC,
    /// магазина, транспорта, кровати и терминала.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>Стабильный ID для сохранения состояния объекта (п. 7 ТЗ).</summary>
        string InteractableId { get; }

        InteractionKind Kind { get; }

        /// <summary>Ключ локализации подсказки, а не готовый текст (NFR-022).</summary>
        string PromptKey { get; }

        /// <summary>Доступно ли действие сейчас: часы работы, состояние, требования.</summary>
        bool IsAvailable { get; }

        /// <summary>Причина недоступности для подсказки. Пусто, если доступно.</summary>
        string UnavailableReasonKey { get; }

        void Interact(IInteractionContext context);
    }

    /// <summary>Что доступно объекту в момент взаимодействия.</summary>
    public interface IInteractionContext
    {
        IEventBus EventBus { get; }
        IGameClock Clock { get; }
    }

    /// <summary>Объект в фокусе изменился — UI обновляет подсказку.</summary>
    public readonly struct InteractionTargetChangedEvent : IGameEvent
    {
        public InteractionTargetChangedEvent(IInteractable target)
        {
            Target = target;
        }

        /// <summary>Новая цель или null, если игрок ни на что не смотрит.</summary>
        public IInteractable Target { get; }
    }
}
