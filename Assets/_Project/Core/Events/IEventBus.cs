using System;

namespace QonaevLife.Core
{
    /// <summary>Маркер игрового события. Реализации должны быть неизменяемыми структурами.</summary>
    public interface IGameEvent
    {
    }

    /// <summary>
    /// Типизированная шина событий. Позволяет модулям реагировать друг на друга
    /// без прямых ссылок на сборки.
    /// </summary>
    public interface IEventBus
    {
        void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;
        void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent;
        void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent;
    }
}
