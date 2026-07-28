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

            foreach (var lighting in FindObjectsByType<World.DayNightLighting>(
                         FindObjectsSortMode.None))
            {
                lighting.Bind(_session.Clock);
            }

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

            var dialogueView = FindFirstObjectByType<UI.DialogueView>();
            if (dialogueView != null)
            {
                dialogueView.Bind(_session.EventBus, _session.Dialogue,
                    _session.Language, localizedText);
            }

            var gate = FindFirstObjectByType<UI.DialogueInputGate>();
            var playerInput = FindFirstObjectByType<Player.PlayerInputBridge>();
            if (gate != null && dialogueView != null)
                gate.Bind(_session.EventBus, playerInput, dialogueView);

            // Экраны меню, слотов, настроек и телефона. Координатор создаётся
            // здесь же, если его нет на сцене: сцену можно собрать без него.
            var uiCoordinator = FindFirstObjectByType<UiCoordinator>()
                                ?? gameObject.AddComponent<UiCoordinator>();

            uiCoordinator.Bind(_session, _session.Router, _session.Settings, localizedText);

            // Игра начинается с главного меню (FR-001): игрок выбирает новую
            // игру или продолжение, а не оказывается сразу в мире.
            if (startInMainMenu)
                _session.Router.Replace(UI.UiScreen.MainMenu);
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
