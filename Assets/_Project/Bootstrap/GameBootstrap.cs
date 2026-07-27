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

            BindSceneObjects();

            Debug.Log($"[Bootstrap] Сессия запущена. День {_session.Clock.Day}, " +
                      $"фаза {_session.Clock.Phase}, баланс {_session.Wallet.Balance}.");
        }

        /// <summary>
        /// Связывает объекты сцены с сессией: детектор взаимодействия и точки
        /// интереса без этого молчат, что позволяет открыть сцену без запуска.
        /// </summary>
        private void BindSceneObjects()
        {
            var context = new InteractionContext(_session.EventBus, _session.Clock);

            foreach (var binder in FindObjectsByType<Player.PlayerSessionBinder>(
                         FindObjectsSortMode.None))
            {
                binder.Detector?.Bind(_session.EventBus, context);
            }

            foreach (var interactable in FindObjectsByType<World.LocationInteractable>(
                         FindObjectsSortMode.None))
            {
                interactable.Bind(_session.Locations);
            }

            // Текст интерфейса прототипа. Полноценный пакет локализации
            // подключается отдельно (FR-094).
            var localizedText = UI.DictionaryLocalizedText.CreateRussianPrototype();

            foreach (var prompt in FindObjectsByType<UI.InteractionPromptView>(
                         FindObjectsSortMode.None))
            {
                prompt.Bind(_session.EventBus, localizedText);
            }

            foreach (var hud in FindObjectsByType<UI.HudView>(FindObjectsSortMode.None))
            {
                hud.Bind(_session.EventBus, _session.Clock, _session.Wallet,
                    _session.Jobs, localizedText);
            }
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
