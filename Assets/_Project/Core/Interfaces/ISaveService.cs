using System;
using System.Collections.Generic;

namespace QonaevLife.Core
{
    /// <summary>Почему слот нельзя загрузить (FR-004).</summary>
    public enum SaveSlotStatus
    {
        /// <summary>Слот пуст — доступен для новой игры.</summary>
        Empty = 0,

        /// <summary>Слот читается и версия поддерживается.</summary>
        Valid = 1,

        /// <summary>Файл есть, но не разбирается или не проходит проверку.</summary>
        Corrupted = 2,

        /// <summary>Версия схемы новее поддерживаемой этой сборкой.</summary>
        UnsupportedVersion = 3
    }

    /// <summary>Краткая сводка по слоту для главного меню (FR-001, FR-002).</summary>
    public readonly struct SaveSlotInfo
    {
        public SaveSlotInfo(int slotIndex, SaveSlotStatus status, string profileName,
            int schemaVersion, int gameDay, DateTime savedAtUtc)
        {
            SlotIndex = slotIndex;
            Status = status;
            ProfileName = profileName;
            SchemaVersion = schemaVersion;
            GameDay = gameDay;
            SavedAtUtc = savedAtUtc;
        }

        public int SlotIndex { get; }
        public SaveSlotStatus Status { get; }
        public string ProfileName { get; }
        public int SchemaVersion { get; }
        public int GameDay { get; }
        public DateTime SavedAtUtc { get; }

        public bool CanLoad => Status == SaveSlotStatus.Valid;
    }

    /// <summary>Результат операции загрузки, не бросающий исключения наружу.</summary>
    public readonly struct LoadResult
    {
        private LoadResult(bool success, SaveData data, SaveSlotStatus status, string message)
        {
            Success = success;
            Data = data;
            Status = status;
            Message = message;
        }

        public bool Success { get; }
        public SaveData Data { get; }
        public SaveSlotStatus Status { get; }

        /// <summary>Понятное игроку сообщение об ошибке (FR-004).</summary>
        public string Message { get; }

        public static LoadResult Ok(SaveData data)
            => new(true, data, SaveSlotStatus.Valid, string.Empty);

        public static LoadResult Fail(SaveSlotStatus status, string message)
            => new(false, null, status, message);
    }

    /// <summary>
    /// Локальные слоты сохранения (FR-003 — FR-005). Реализация обязана
    /// не падать на повреждённом файле и не терять последний валидный слот.
    /// </summary>
    public interface ISaveService
    {
        /// <summary>Минимальное число слотов по ТЗ (FR-002).</summary>
        int SlotCount { get; }

        IReadOnlyList<SaveSlotInfo> EnumerateSlots();

        SaveSlotInfo GetSlotInfo(int slotIndex);

        /// <summary>Записывает слот атомарно: сначала во временный файл, потом замена.</summary>
        bool Save(int slotIndex, SaveData data);

        LoadResult Load(int slotIndex);

        bool DeleteSlot(int slotIndex);
    }
}
