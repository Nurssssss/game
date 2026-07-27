using System;
using System.Collections.Generic;

namespace QonaevLife.Core
{
    /// <summary>
    /// Реестр сервисов сессии. Это не GameManager: он не хранит игровое
    /// состояние и не содержит игровой логики — только разрешение контрактов,
    /// заполняемое композиционным корнем.
    /// </summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, object> _services = new();
        private readonly List<IGameService> _initOrder = new();
        private bool _initialized;

        /// <summary>Регистрирует реализацию под её контрактом.</summary>
        public void Register<TContract>(TContract implementation) where TContract : class
        {
            if (implementation == null)
                throw new ArgumentNullException(nameof(implementation));

            var contract = typeof(TContract);
            if (_services.ContainsKey(contract))
                throw new InvalidOperationException($"Сервис {contract.Name} уже зарегистрирован.");

            if (_initialized)
                throw new InvalidOperationException(
                    $"Нельзя регистрировать {contract.Name} после InitializeAll.");

            _services.Add(contract, implementation);

            if (implementation is IGameService service)
                _initOrder.Add(service);
        }

        /// <summary>Возвращает сервис или бросает исключение, если он не зарегистрирован.</summary>
        public TContract Resolve<TContract>() where TContract : class
        {
            if (_services.TryGetValue(typeof(TContract), out var service))
                return (TContract)service;

            throw new InvalidOperationException(
                $"Сервис {typeof(TContract).Name} не зарегистрирован. " +
                "Проверьте композиционный корень.");
        }

        public bool TryResolve<TContract>(out TContract service) where TContract : class
        {
            if (_services.TryGetValue(typeof(TContract), out var found))
            {
                service = (TContract)found;
                return true;
            }

            service = null;
            return false;
        }

        /// <summary>Инициализирует сервисы в порядке регистрации.</summary>
        public void InitializeAll()
        {
            if (_initialized)
                return;

            _initialized = true;

            for (var i = 0; i < _initOrder.Count; i++)
                _initOrder[i].Initialize();
        }

        /// <summary>Останавливает сервисы в обратном порядке и очищает реестр.</summary>
        public void ShutdownAll()
        {
            for (var i = _initOrder.Count - 1; i >= 0; i--)
                _initOrder[i].Shutdown();

            _initOrder.Clear();
            _services.Clear();
            _initialized = false;
        }
    }
}
