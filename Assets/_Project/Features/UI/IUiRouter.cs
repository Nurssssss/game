using System;
using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.UI
{
    /// <summary>Экраны интерфейса (п. 9 ТЗ).</summary>
    public enum UiScreen
    {
        /// <summary>Игра без открытых окон — виден только HUD.</summary>
        None = 0,

        MainMenu = 1,
        SaveSlots = 2,
        Settings = 3,
        Phone = 4,
        Map = 5,
        Dialogue = 6,
        Shop = 7,
        Lesson = 8,
        Credits = 9
    }

    public readonly struct ScreenChangedEvent : IGameEvent
    {
        public ScreenChangedEvent(UiScreen previous, UiScreen current)
        {
            Previous = previous;
            Current = current;
        }

        public UiScreen Previous { get; }
        public UiScreen Current { get; }
    }

    /// <summary>
    /// Маршрутизация экранов (контракт IUiRouter из п. 4.2 ТЗ). Держит стек
    /// открытых окон, чтобы «назад» возвращал на предыдущий экран, а не
    /// закрывал всё сразу. Игровой ввод блокируется, пока открыт любой экран,
    /// кроме <see cref="UiScreen.None"/>.
    /// </summary>
    public interface IUiRouter
    {
        UiScreen Current { get; }

        /// <summary>Нужно ли блокировать управление персонажем.</summary>
        bool IsGameplayBlocked { get; }

        /// <summary>Нужно ли останавливать игровое время (FR-021, пауза).</summary>
        bool ShouldPauseTime { get; }

        IReadOnlyList<UiScreen> Stack { get; }

        /// <summary>Открывает экран поверх текущего.</summary>
        void Push(UiScreen screen);

        /// <summary>Закрывает верхний экран и возвращается к предыдущему.</summary>
        void Pop();

        /// <summary>Закрывает все экраны и возвращает управление игроку.</summary>
        void CloseAll();

        /// <summary>Заменяет весь стек одним экраном — для перехода в меню.</summary>
        void Replace(UiScreen screen);
    }

    /// <summary>
    /// Стековый маршрутизатор. Не зависит от Unity, поэтому покрывается
    /// модульными тестами (NFR-011).
    /// </summary>
    public sealed class UiRouter : IUiRouter, IGameService
    {
        private readonly IEventBus _eventBus;
        private readonly List<UiScreen> _stack = new();

        /// <summary>
        /// Экраны, при которых время останавливается. Диалог время не
        /// останавливает: разговор — часть игрового процесса, и смена
        /// с лимитом не должна замирать (FR-070).
        /// </summary>
        private static readonly HashSet<UiScreen> TimePausingScreens = new()
        {
            UiScreen.MainMenu,
            UiScreen.SaveSlots,
            UiScreen.Settings,
            UiScreen.Credits
        };

        public UiRouter(IEventBus eventBus)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public UiScreen Current => _stack.Count > 0 ? _stack[^1] : UiScreen.None;

        public bool IsGameplayBlocked => Current != UiScreen.None;

        public bool ShouldPauseTime => TimePausingScreens.Contains(Current);

        public IReadOnlyList<UiScreen> Stack => _stack;

        public void Initialize()
        {
        }

        public void Shutdown() => _stack.Clear();

        public void Push(UiScreen screen)
        {
            if (screen == UiScreen.None)
            {
                CloseAll();
                return;
            }

            // Повторное открытие того же экрана не должно плодить стек:
            // иначе одна лишняя нажатая клавиша потребует двух «назад».
            if (Current == screen)
                return;

            var previous = Current;
            _stack.Add(screen);
            _eventBus.Publish(new ScreenChangedEvent(previous, screen));
        }

        public void Pop()
        {
            if (_stack.Count == 0)
                return;

            var previous = Current;
            _stack.RemoveAt(_stack.Count - 1);
            _eventBus.Publish(new ScreenChangedEvent(previous, Current));
        }

        public void CloseAll()
        {
            if (_stack.Count == 0)
                return;

            var previous = Current;
            _stack.Clear();
            _eventBus.Publish(new ScreenChangedEvent(previous, UiScreen.None));
        }

        public void Replace(UiScreen screen)
        {
            var previous = Current;
            _stack.Clear();

            if (screen != UiScreen.None)
                _stack.Add(screen);

            if (previous != screen)
                _eventBus.Publish(new ScreenChangedEvent(previous, screen));
        }
    }
}
