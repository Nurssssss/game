using System;
using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.UI
{
    /// <summary>Действия главного меню (FR-001).</summary>
    public enum MainMenuAction
    {
        NewGame = 0,
        Continue = 1,
        Load = 2,
        Settings = 3,
        Credits = 4,
        Quit = 5
    }

    /// <summary>Игрок выбрал действие в меню — обрабатывает композиционный корень.</summary>
    public readonly struct MainMenuActionEvent : IGameEvent
    {
        public MainMenuActionEvent(MainMenuAction action, int slotIndex)
        {
            Action = action;
            SlotIndex = slotIndex;
        }

        public MainMenuAction Action { get; }

        /// <summary>Слот для «Продолжить», «Загрузить» и «Новая игра». -1 — не задан.</summary>
        public int SlotIndex { get; }
    }

    /// <summary>Слот сохранения, подготовленный к показу.</summary>
    public readonly struct SlotView
    {
        public SlotView(int index, SaveSlotStatus status, string profileName, int gameDay,
            DateTime savedAtUtc)
        {
            Index = index;
            Status = status;
            ProfileName = profileName;
            GameDay = gameDay;
            SavedAtUtc = savedAtUtc;
        }

        public int Index { get; }
        public SaveSlotStatus Status { get; }
        public string ProfileName { get; }
        public int GameDay { get; }
        public DateTime SavedAtUtc { get; }

        public bool CanLoad => Status == SaveSlotStatus.Valid;
        public bool IsEmpty => Status == SaveSlotStatus.Empty;

        /// <summary>Ключ локализации состояния слота — для проблемных слотов.</summary>
        public string StatusKey => Status switch
        {
            SaveSlotStatus.Empty => "slot.empty",
            SaveSlotStatus.Valid => "slot.valid",
            SaveSlotStatus.Corrupted => "slot.corrupted",
            SaveSlotStatus.UnsupportedVersion => "slot.unsupported",
            _ => "slot.unknown"
        };
    }

    /// <summary>
    /// Данные главного меню и экрана слотов (FR-001 — FR-004). Определяет,
    /// доступна ли кнопка «Продолжить», и какой слот она загрузит.
    /// </summary>
    public sealed class MainMenuModel
    {
        private readonly ISaveService _saveService;
        private readonly List<SlotView> _slots = new();

        public MainMenuModel(ISaveService saveService)
        {
            _saveService = saveService ?? throw new ArgumentNullException(nameof(saveService));
        }

        public IReadOnlyList<SlotView> Slots => _slots;

        /// <summary>
        /// Слот для «Продолжить» — последний сохранённый из валидных.
        /// Возвращает -1, если продолжать нечего.
        /// </summary>
        public int MostRecentSlotIndex { get; private set; } = -1;

        public bool CanContinue => MostRecentSlotIndex >= 0;

        /// <summary>Есть ли хотя бы один валидный слот для экрана загрузки.</summary>
        public bool HasAnySave => CanContinue;

        /// <summary>Перечитывает слоты с диска.</summary>
        public void Refresh()
        {
            _slots.Clear();
            MostRecentSlotIndex = -1;

            var latest = DateTime.MinValue;

            foreach (var info in _saveService.EnumerateSlots())
            {
                _slots.Add(new SlotView(
                    info.SlotIndex, info.Status, info.ProfileName, info.GameDay, info.SavedAtUtc));

                // Повреждённый слот не может быть «последним»: кнопка
                // «Продолжить» обязана вести в работающую игру (FR-004).
                if (!info.CanLoad || info.SavedAtUtc <= latest)
                    continue;

                latest = info.SavedAtUtc;
                MostRecentSlotIndex = info.SlotIndex;
            }
        }

        /// <summary>Первый свободный слот для новой игры, или -1 если все заняты.</summary>
        public int FindFirstEmptySlot()
        {
            foreach (var slot in _slots)
            {
                if (slot.IsEmpty)
                    return slot.Index;
            }

            return -1;
        }

        public bool TryGetSlot(int index, out SlotView slot)
        {
            foreach (var candidate in _slots)
            {
                if (candidate.Index != index)
                    continue;

                slot = candidate;
                return true;
            }

            slot = default;
            return false;
        }
    }
}
