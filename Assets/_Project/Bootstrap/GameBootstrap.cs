using UnityEngine;

namespace QonaevLife.Bootstrap
{
    /// <summary>
    /// Точка входа постоянной сцены Bootstrap (п. 7 ТЗ). Запускает сервисы,
    /// продвигает время сессии и корректно останавливает её при выходе.
    /// Игровой логики не содержит — только жизненный цикл.
    /// </summary>
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] [Tooltip("Балансовые настройки сессии.")]
        private GameSessionConfig config;

        [SerializeField] [Tooltip("База контента: локации, NPC, диалоги, работы, слова.")]
        private Content.ContentDatabase content;

        [SerializeField] [Tooltip("Начислять стартовый капитал сразу при запуске (для отладки).")]
        private bool startNewGameOnAwake = true;

        private GameSession _session;

        /// <summary>Текущая сессия или null, если запуск не удался.</summary>
        public GameSession Session => _session;

        private void Awake()
        {
            if (config == null)
            {
                Debug.LogError("[Bootstrap] Не назначен GameSessionConfig — сессия не запущена.");
                return;
            }

            if (content == null)
            {
                Debug.LogError("[Bootstrap] Не назначена ContentDatabase — сессия не запущена.");
                return;
            }

            DontDestroyOnLoad(gameObject);

            try
            {
                _session = GameSessionBuilder.Build(
                    config, content, Application.persistentDataPath);
            }
            catch (System.Exception exception)
            {
                // Игрок должен увидеть понятную ошибку, а не молчаливый чёрный экран (NFR-006).
                Debug.LogError($"[Bootstrap] Не удалось запустить сессию: {exception.Message}");
                return;
            }

            if (startNewGameOnAwake)
                GameSessionBuilder.ApplyNewGameState(_session, config);

            Debug.Log($"[Bootstrap] Сессия запущена. День {_session.Clock.Day}, " +
                      $"фаза {_session.Clock.Phase}, баланс {_session.Wallet.Balance}.");
        }

        private void Update()
        {
            _session?.Tick(Time.deltaTime);
        }

        private void OnDestroy()
        {
            _session?.Shutdown();
            _session = null;
        }
    }
}
