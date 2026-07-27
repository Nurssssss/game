namespace QonaevLife.Core
{
    /// <summary>
    /// Базовый контракт сервиса игровой сессии. Сервисы регистрируются
    /// композиционным корнем (Bootstrap) и никогда не ищут друг друга сами.
    /// </summary>
    public interface IGameService
    {
        /// <summary>Вызывается один раз после регистрации всех сервисов.</summary>
        void Initialize();

        /// <summary>Освобождение ресурсов при завершении сессии.</summary>
        void Shutdown();
    }
}
