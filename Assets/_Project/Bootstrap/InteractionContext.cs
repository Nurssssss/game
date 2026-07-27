using QonaevLife.Core;
using QonaevLife.Player;

namespace QonaevLife.Bootstrap
{
    /// <summary>
    /// Что доступно интерактивному объекту в момент взаимодействия.
    /// Создаётся композиционным корнем: объекты сцены не ищут сервисы сами.
    /// </summary>
    public sealed class InteractionContext : IInteractionContext
    {
        public InteractionContext(IEventBus eventBus, IGameClock clock)
        {
            EventBus = eventBus;
            Clock = clock;
        }

        public IEventBus EventBus { get; }

        public IGameClock Clock { get; }
    }
}
