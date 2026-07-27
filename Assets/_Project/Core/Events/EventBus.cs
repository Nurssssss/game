using System;
using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Core
{
    /// <summary>
    /// Синхронная шина событий. Рассылка идёт по снимку списка подписчиков,
    /// поэтому подписка/отписка внутри обработчика безопасна. Исключение в одном
    /// обработчике логируется и не прерывает рассылку остальным (NFR-006).
    /// Буферы берутся из пула, поэтому вложенная публикация не портит внешнюю.
    /// </summary>
    public sealed class EventBus : IEventBus
    {
        private readonly Dictionary<Type, List<Delegate>> _handlers = new();
        private readonly Stack<List<Delegate>> _bufferPool = new();

        public void Subscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null)
                throw new ArgumentNullException(nameof(handler));

            var key = typeof(TEvent);
            if (!_handlers.TryGetValue(key, out var list))
            {
                list = new List<Delegate>();
                _handlers.Add(key, list);
            }

            if (!list.Contains(handler))
                list.Add(handler);
        }

        public void Unsubscribe<TEvent>(Action<TEvent> handler) where TEvent : IGameEvent
        {
            if (handler == null)
                return;

            if (_handlers.TryGetValue(typeof(TEvent), out var list))
                list.Remove(handler);
        }

        public void Publish<TEvent>(TEvent gameEvent) where TEvent : IGameEvent
        {
            if (!_handlers.TryGetValue(typeof(TEvent), out var list) || list.Count == 0)
                return;

            // Снимок: обработчик вправе подписаться, отписаться или опубликовать
            // другое событие во время рассылки.
            var buffer = RentBuffer();
            buffer.AddRange(list);

            try
            {
                for (var i = 0; i < buffer.Count; i++)
                {
                    try
                    {
                        ((Action<TEvent>)buffer[i]).Invoke(gameEvent);
                    }
                    catch (Exception exception)
                    {
                        Debug.LogException(exception);
                    }
                }
            }
            finally
            {
                ReturnBuffer(buffer);
            }
        }

        public void Clear()
        {
            _handlers.Clear();
            _bufferPool.Clear();
        }

        private List<Delegate> RentBuffer()
            => _bufferPool.Count > 0 ? _bufferPool.Pop() : new List<Delegate>();

        private void ReturnBuffer(List<Delegate> buffer)
        {
            buffer.Clear();
            _bufferPool.Push(buffer);
        }
    }
}
