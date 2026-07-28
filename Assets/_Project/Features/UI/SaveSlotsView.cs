using System.Collections.Generic;
using QonaevLife.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.UI
{
    /// <summary>
    /// Экран слотов сохранения (FR-002, FR-003, AT-005). Показывает профиль,
    /// игровой день и время записи. Повреждённый слот отображается с понятной
    /// причиной и не даёт загрузиться (FR-004).
    /// </summary>
    public sealed class SaveSlotsView : ScreenView
    {
        [Header("Список слотов")]
        [SerializeField] private Transform slotContainer;

        [SerializeField] [Tooltip("Шаблон строки слота. Отключён на сцене.")]
        private Button slotTemplate;

        [Header("Прочее")]
        [SerializeField] private TMP_Text titleLabel;
        [SerializeField] private Button backButton;

        [SerializeField]
        [Tooltip("Режим: сохранение вместо загрузки. Переключается при открытии.")]
        private bool saveMode;

        private readonly List<Button> _slotButtons = new();

        private IEventBus _bus;
        private MainMenuModel _model;

        public override UiScreen Screen => UiScreen.SaveSlots;

        /// <summary>В режиме сохранения выбор слота записывает игру, а не грузит.</summary>
        public bool SaveMode
        {
            get => saveMode;
            set
            {
                saveMode = value;
                if (IsVisible)
                    Refresh();
            }
        }

        public void BindSlots(IEventBus eventBus, MainMenuModel model)
        {
            _bus = eventBus;
            _model = model;

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(CloseSelf);
            }
        }

        protected override void OnShown() => Refresh();

        public void Refresh()
        {
            if (_model == null || Text == null || slotTemplate == null || slotContainer == null)
                return;

            _model.Refresh();

            if (titleLabel != null)
            {
                titleLabel.text = Text.Resolve(saveMode ? "slots.title_save" : "slots.title_load");
            }

            if (backButton != null)
            {
                var backLabel = backButton.GetComponentInChildren<TMP_Text>();
                if (backLabel != null)
                    backLabel.text = Text.Resolve("common.back");
            }

            EnsureButtonCount(_model.Slots.Count);

            for (var i = 0; i < _slotButtons.Count; i++)
            {
                var button = _slotButtons[i];

                if (i >= _model.Slots.Count)
                {
                    button.gameObject.SetActive(false);
                    continue;
                }

                var slot = _model.Slots[i];
                button.gameObject.SetActive(true);

                // В режиме загрузки пустой и повреждённый слот недоступны;
                // в режиме сохранения записать можно в любой.
                button.interactable = saveMode || slot.CanLoad;

                var label = button.GetComponentInChildren<TMP_Text>();
                if (label != null)
                {
                    label.text = FormatSlot(slot);
                    label.color = slot.Status switch
                    {
                        SaveSlotStatus.Corrupted => new Color(1f, 0.55f, 0.45f),
                        SaveSlotStatus.UnsupportedVersion => new Color(1f, 0.75f, 0.4f),
                        SaveSlotStatus.Empty => new Color(0.65f, 0.65f, 0.7f),
                        _ => Color.white
                    };
                }

                var index = slot.Index;
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => OnSlotSelected(index));
            }
        }

        /// <summary>
        /// Строка слота. Состояние дублируется текстом, а не только цветом:
        /// критическая информация не должна передаваться одним цветом (п. 9 ТЗ).
        /// </summary>
        private string FormatSlot(SlotView slot)
        {
            var number = slot.Index + 1;

            if (slot.IsEmpty)
                return $"{number}. {Text.Resolve("slot.empty")}";

            if (!slot.CanLoad)
                return $"{number}. {Text.Resolve(slot.StatusKey)}";

            var saved = slot.SavedAtUtc.ToLocalTime().ToString("dd.MM.yyyy HH:mm");
            var name = string.IsNullOrWhiteSpace(slot.ProfileName)
                ? Text.Resolve("slot.no_name")
                : slot.ProfileName;

            return $"{number}. {name}   " +
                   $"{Text.Resolve("hud.day")} {slot.GameDay}   {saved}";
        }

        private void OnSlotSelected(int slotIndex)
        {
            var action = saveMode ? MainMenuAction.NewGame : MainMenuAction.Load;

            // Режим сохранения обрабатывается тем же событием: композиционный
            // корень различает их по флагу экрана.
            _bus?.Publish(new SaveSlotSelectedEvent(slotIndex, saveMode));

            if (!saveMode)
                _bus?.Publish(new MainMenuActionEvent(action, slotIndex));
        }

        private void EnsureButtonCount(int required)
        {
            while (_slotButtons.Count < required)
            {
                var instance = Instantiate(slotTemplate, slotContainer);
                instance.gameObject.SetActive(true);
                _slotButtons.Add(instance);
            }
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(Transform container, Button template, TMP_Text title, Button back)
        {
            slotContainer = container;
            slotTemplate = template;
            titleLabel = title;
            backButton = back;
        }
    }

    /// <summary>Игрок выбрал слот — сохранить или загрузить решает корень.</summary>
    public readonly struct SaveSlotSelectedEvent : IGameEvent
    {
        public SaveSlotSelectedEvent(int slotIndex, bool isSaveRequest)
        {
            SlotIndex = slotIndex;
            IsSaveRequest = isSaveRequest;
        }

        public int SlotIndex { get; }
        public bool IsSaveRequest { get; }
    }
}
