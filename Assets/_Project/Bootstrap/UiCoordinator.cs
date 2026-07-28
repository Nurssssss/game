using QonaevLife.Core;
using QonaevLife.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace QonaevLife.Bootstrap
{
    /// <summary>
    /// Связывает экраны интерфейса с сессией и обрабатывает действия меню
    /// (FR-001 — FR-005). Живёт в сцене, потому что владеет ссылками на
    /// представления; игровых правил не содержит.
    /// </summary>
    public sealed class UiCoordinator : MonoBehaviour
    {
        [SerializeField] [Tooltip("Клавиша открытия телефона (FR-091).")]
        private Key phoneKey = Key.Tab;

        [SerializeField] [Tooltip("Клавиша паузы и выхода из экранов.")]
        private Key pauseKey = Key.Escape;

        private GameSession _session;
        private UiRouter _router;
        private MainMenuModel _menuModel;
        private ISettingsService _settings;

        private MainMenuView _mainMenu;
        private SaveSlotsView _saveSlots;
        private SettingsView _settingsView;
        private PhoneView _phone;
        private Player.PlayerInputBridge _playerInput;

        /// <summary>
        /// Подключает интерфейс к сессии. Вызывается из
        /// <see cref="GameBootstrap"/> после сборки сервисов.
        /// </summary>
        public void Bind(GameSession session, UiRouter router, ISettingsService settings,
            ILocalizedText text)
        {
            _session = session;
            _router = router;
            _settings = settings;
            _menuModel = new MainMenuModel(session.SaveService);

            _playerInput = FindFirstObjectByType<Player.PlayerInputBridge>();

            BindScreens(text);

            session.EventBus.Subscribe<MainMenuActionEvent>(OnMenuAction);
            session.EventBus.Subscribe<SaveSlotSelectedEvent>(OnSlotSelected);
            session.EventBus.Subscribe<ScreenChangedEvent>(OnScreenChanged);
        }

        private void BindScreens(ILocalizedText text)
        {
            var bus = _session.EventBus;

            _mainMenu = FindFirstObjectByType<MainMenuView>();
            if (_mainMenu != null)
            {
                _mainMenu.BindScreen(bus, _router, text);
                _mainMenu.BindMenu(bus, _menuModel);
            }

            _saveSlots = FindFirstObjectByType<SaveSlotsView>();
            if (_saveSlots != null)
            {
                _saveSlots.BindScreen(bus, _router, text);
                _saveSlots.BindSlots(bus, _menuModel);
            }

            _settingsView = FindFirstObjectByType<SettingsView>();
            if (_settingsView != null)
            {
                _settingsView.BindScreen(bus, _router, text);
                _settingsView.BindSettings(_settings);
            }

            _phone = FindFirstObjectByType<PhoneView>();
            if (_phone != null)
            {
                _phone.BindScreen(bus, _router, text);

                var phoneModel = new PhoneModel(
                    _session.Content, _session.Wallet, _session.Jobs, _session.Language);

                _phone.BindPhone(phoneModel);
            }

            var mapView = FindFirstObjectByType<MapView>();
            if (mapView != null)
            {
                var mapModel = new MapModel(_session.Locations);

                // Позиция игрока и цель запрашиваются в момент отрисовки:
                // карта всегда показывает актуальное состояние.
                mapView.Bind(mapModel, text,
                    playerPositionProvider: GetPlayerPosition,
                    objectiveProvider: () => _session.Jobs.CurrentTargetLocationId);
            }

            var applier = FindFirstObjectByType<SettingsApplier>();
            if (applier != null)
                applier.Bind(bus, _settings, _session.Language);
        }

        private Vector3 GetPlayerPosition()
            => _playerInput != null ? _playerInput.transform.position : Vector3.zero;

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || _router == null)
                return;

            if (keyboard[pauseKey].wasPressedThisFrame)
                HandlePause();
            else if (keyboard[phoneKey].wasPressedThisFrame)
                HandlePhone();
        }

        /// <summary>
        /// Escape закрывает верхний экран, а на игровом экране открывает меню:
        /// одна клавиша и для «назад», и для паузы — как ожидает игрок.
        /// </summary>
        private void HandlePause()
        {
            if (_router.Current == UiScreen.None)
            {
                _router.Push(UiScreen.MainMenu);
                return;
            }

            // Диалог закрывает себя сам через DialogueInputGate.
            if (_router.Current != UiScreen.Dialogue)
                _router.Pop();
        }

        private void HandlePhone()
        {
            if (_router.Current == UiScreen.Phone)
                _router.Pop();
            else if (_router.Current == UiScreen.None)
                _router.Push(UiScreen.Phone);
        }

        /// <summary>
        /// Открытый экран блокирует управление персонажем, а меню и настройки
        /// ещё и останавливают время (п. 9 ТЗ).
        /// </summary>
        private void OnScreenChanged(ScreenChangedEvent changed)
        {
            if (_playerInput != null)
                _playerInput.IsInputBlocked = _router.IsGameplayBlocked;

            if (_router.ShouldPauseTime)
                _session.Clock.Pause();
            else
                _session.Clock.Resume();
        }

        private void OnMenuAction(MainMenuActionEvent action)
        {
            switch (action.Action)
            {
                case MainMenuAction.NewGame:
                    StartNewGame(action.SlotIndex);
                    break;

                case MainMenuAction.Continue:
                case MainMenuAction.Load:
                    LoadSlot(action.SlotIndex);
                    break;

                case MainMenuAction.Quit:
                    QuitGame();
                    break;
            }
        }

        private void OnSlotSelected(SaveSlotSelectedEvent selected)
        {
            if (!selected.IsSaveRequest)
                return;

            SaveToSlot(selected.SlotIndex);
        }

        private void StartNewGame(int slotIndex)
        {
            // Слот может быть занят полностью: тогда пишем в первый,
            // перезаписывая старую партию по явному выбору игрока.
            var target = slotIndex >= 0 ? slotIndex : 0;

            _router.CloseAll();
            SaveToSlot(target);

            Debug.Log($"[UI] Новая игра в слоте {target + 1}.");
        }

        private void LoadSlot(int slotIndex)
        {
            if (slotIndex < 0)
                return;

            var result = _session.SaveService.Load(slotIndex);

            if (!result.Success)
            {
                // Понятное сообщение вместо тихого отказа (FR-004, NFR-006).
                Debug.LogError($"[UI] Загрузка слота {slotIndex + 1}: {result.Message}");
                return;
            }

            _session.RestoreSave(result.Data);
            _router.CloseAll();

            Debug.Log($"[UI] Слот {slotIndex + 1} загружен: день {_session.Clock.Day}.");
        }

        private void SaveToSlot(int slotIndex)
        {
            if (slotIndex < 0)
                return;

            var profile = _settings.Current.interfaceLanguage == "kk" ? "Ойыншы" : "Игрок";
            var data = _session.CaptureSave(profile);

            if (_session.SaveService.Save(slotIndex, data))
                Debug.Log($"[UI] Сохранено в слот {slotIndex + 1}.");
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void OnDestroy()
        {
            if (_session == null)
                return;

            _session.EventBus.Unsubscribe<MainMenuActionEvent>(OnMenuAction);
            _session.EventBus.Unsubscribe<SaveSlotSelectedEvent>(OnSlotSelected);
            _session.EventBus.Unsubscribe<ScreenChangedEvent>(OnScreenChanged);
        }
    }
}
