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

        [SerializeField]
        [Tooltip("Открывать главное меню при запуске (FR-001). Снимите для отладки мира.")]
        private bool startInMainMenu = true;

        private GameSession _session;
        private Transform _playerTransform;

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

            foreach (var binder in FindObjectsByType<Player.PlayerSessionBinder>())
            {
                binder.Detector?.Bind(_session.EventBus, context);
            }

            foreach (var interactable in FindObjectsByType<World.LocationInteractable>())
            {
                interactable.Bind(_session.Locations);
            }

            // Текст интерфейса прототипа. Полноценный пакет локализации
            // подключается отдельно (FR-094).
            var localizedText = UI.DictionaryLocalizedText.CreateRussianPrototype();

            foreach (var lighting in FindObjectsByType<World.DayNightLighting>())
            {
                lighting.Bind(_session.Clock);
            }

            foreach (var prompt in FindObjectsByType<UI.InteractionPromptView>())
            {
                prompt.Bind(_session.EventBus, localizedText);
            }

            foreach (var hud in FindObjectsByType<UI.HudView>())
            {
                hud.Bind(_session.EventBus, _session.Clock, _session.Wallet,
                    _session.Jobs, _session.Locations, localizedText);
            }

            var dialogueView = FindAnyObjectByType<UI.DialogueView>();
            if (dialogueView != null)
            {
                dialogueView.Bind(_session.EventBus, _session.Dialogue,
                    _session.Language, localizedText);
            }

            var gate = FindAnyObjectByType<UI.DialogueInputGate>();
            var playerInput = FindAnyObjectByType<Player.PlayerInputBridge>();

            if (playerInput != null)
                _playerTransform = playerInput.transform;

            // Фигуры NPC на сцене. Спавнер создаётся, если его нет: сцена
            // должна собираться и без него.
            var spawner = FindAnyObjectByType<Npc.NpcSpawner>();
            if (spawner == null)
            {
                var spawnerObject = new GameObject("NpcSpawner");
                spawner = spawnerObject.AddComponent<Npc.NpcSpawner>();
            }

            spawner.Bind(_session.EventBus, _session.Npcs, content);
            if (gate != null && dialogueView != null)
                gate.Bind(_session.EventBus, playerInput, dialogueView);

            // Экраны меню, слотов, настроек и телефона. Координатор создаётся
            // здесь же, если его нет на сцене: сцену можно собрать без него.
            var uiCoordinator = FindAnyObjectByType<UiCoordinator>()
                                ?? gameObject.AddComponent<UiCoordinator>();

            uiCoordinator.Bind(_session, _session.Router, _session.Settings, localizedText);

            // Игра начинается с главного меню (FR-001): игрок выбирает новую
            // игру или продолжение, а не оказывается сразу в мире.
            if (startInMainMenu)
                _session.Router.Replace(UI.UiScreen.MainMenu);
        }

        private void Update()
        {
            if (_session == null)
                return;

            var playerPosition = _playerTransform != null
                ? _playerTransform.position
                : Vector3.zero;

            _session.Tick(Time.deltaTime, playerPosition);
        }

        private void OnDestroy()
        {
            _session?.Shutdown();
            _session = null;
        }
    }
}
