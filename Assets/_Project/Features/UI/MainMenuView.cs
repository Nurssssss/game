using QonaevLife.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.UI
{
    /// <summary>
    /// Главное меню (FR-001): «Новая игра», «Продолжить», «Загрузить»,
    /// «Настройки», «Титры», «Выход». Кнопка «Продолжить» недоступна, когда
    /// продолжать нечего, — и остаётся видимой, чтобы игрок понимал, что
    /// такая возможность существует.
    /// </summary>
    public sealed class MainMenuView : ScreenView
    {
        [Header("Кнопки")]
        [SerializeField] private Button newGameButton;
        [SerializeField] private Button continueButton;
        [SerializeField] private Button loadButton;
        [SerializeField] private Button settingsButton;
        [SerializeField] private Button creditsButton;
        [SerializeField] private Button quitButton;

        [Header("Подписи")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private TMP_Text versionLabel;
        [SerializeField] private TMP_Text hintLabel;

        private IEventBus _bus;
        private MainMenuModel _model;

        public override UiScreen Screen => UiScreen.MainMenu;

        /// <summary>Подключает модель слотов и шину для действий меню.</summary>
        public void BindMenu(IEventBus eventBus, MainMenuModel model)
        {
            _bus = eventBus;
            _model = model;

            WireButton(newGameButton, () => Raise(MainMenuAction.NewGame,
                _model.FindFirstEmptySlot()));

            WireButton(continueButton, () => Raise(MainMenuAction.Continue,
                _model.MostRecentSlotIndex));

            WireButton(loadButton, () => Router.Push(UiScreen.SaveSlots));
            WireButton(settingsButton, () => Router.Push(UiScreen.Settings));
            WireButton(creditsButton, () => Router.Push(UiScreen.Credits));
            WireButton(quitButton, () => Raise(MainMenuAction.Quit, -1));

            Refresh();
        }

        protected override void OnShown() => Refresh();

        /// <summary>Перечитывает слоты: состав мог измениться после игры.</summary>
        public void Refresh()
        {
            if (_model == null || Text == null)
                return;

            _model.Refresh();

            if (titleLabel != null)
                titleLabel.text = Text.Resolve("menu.title");

            SetLabel(newGameButton, "menu.new_game");
            SetLabel(continueButton, "menu.continue");
            SetLabel(loadButton, "menu.load");
            SetLabel(settingsButton, "menu.settings");
            SetLabel(creditsButton, "menu.credits");
            SetLabel(quitButton, "menu.quit");

            // Кнопки остаются видимыми, но недоступными: так игрок понимает,
            // что возможность есть, просто сейчас нечего продолжать (п. 9 ТЗ).
            if (continueButton != null)
                continueButton.interactable = _model.CanContinue;

            if (loadButton != null)
                loadButton.interactable = _model.HasAnySave;

            if (hintLabel != null)
            {
                hintLabel.text = _model.CanContinue
                    ? Text.Resolve("menu.hint_continue")
                    : Text.Resolve("menu.hint_new");
            }

            if (versionLabel != null)
                versionLabel.text = $"{Text.Resolve("menu.version")} {Application.version}";
        }

        private void Raise(MainMenuAction action, int slotIndex)
            => _bus?.Publish(new MainMenuActionEvent(action, slotIndex));

        private void SetLabel(Button button, string key)
        {
            if (button == null)
                return;

            var label = button.GetComponentInChildren<TMP_Text>();
            if (label != null)
                label.text = Text.Resolve(key);
        }

        private static void WireButton(Button button, UnityEngine.Events.UnityAction action)
        {
            if (button == null)
                return;

            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(action);
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(Button newGame, Button @continue, Button load, Button settings,
            Button credits, Button quit, TMP_Text title, TMP_Text version, TMP_Text hint)
        {
            newGameButton = newGame;
            continueButton = @continue;
            loadButton = load;
            settingsButton = settings;
            creditsButton = credits;
            quitButton = quit;
            titleLabel = title;
            versionLabel = version;
            hintLabel = hint;
        }
    }
}
