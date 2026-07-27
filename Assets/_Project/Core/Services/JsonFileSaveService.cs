using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace QonaevLife.Core
{
    /// <summary>
    /// Сохранения в версионированный JSON внутри указанной папки (FR-003 — FR-005).
    /// Запись атомарна: данные пишутся во временный файл, затем заменяют исходный,
    /// поэтому сбой во время записи не уничтожает предыдущий валидный слот.
    /// Ни один метод не бросает исключения наружу — ошибки возвращаются в результате.
    /// </summary>
    public sealed class JsonFileSaveService : ISaveService
    {
        private const string SlotFilePrefix = "slot_";
        private const string SlotFileExtension = ".json";
        private const string TempFileExtension = ".tmp";
        private const string BackupFileExtension = ".bak";

        private readonly string _rootDirectory;

        public JsonFileSaveService(string rootDirectory, int slotCount = 3)
        {
            if (string.IsNullOrWhiteSpace(rootDirectory))
                throw new ArgumentException("Папка сохранений не задана.", nameof(rootDirectory));

            if (slotCount < 3)
                throw new ArgumentOutOfRangeException(
                    nameof(slotCount), "ТЗ требует не менее трёх слотов (FR-002).");

            _rootDirectory = rootDirectory;
            SlotCount = slotCount;
        }

        public int SlotCount { get; }

        public IReadOnlyList<SaveSlotInfo> EnumerateSlots()
        {
            var slots = new List<SaveSlotInfo>(SlotCount);
            for (var i = 0; i < SlotCount; i++)
                slots.Add(GetSlotInfo(i));

            return slots;
        }

        public SaveSlotInfo GetSlotInfo(int slotIndex)
        {
            if (!IsSlotIndexValid(slotIndex))
                return new SaveSlotInfo(slotIndex, SaveSlotStatus.Empty, string.Empty, 0, 0, default);

            var path = GetSlotPath(slotIndex);
            if (!File.Exists(path))
                return new SaveSlotInfo(slotIndex, SaveSlotStatus.Empty, string.Empty, 0, 0, default);

            var parsed = TryReadSlot(path, out var data, out var status);
            if (!parsed)
                return new SaveSlotInfo(slotIndex, status, string.Empty, 0, 0, default);

            return new SaveSlotInfo(
                slotIndex,
                status,
                data.ProfileName,
                data.SchemaVersion,
                data.world?.day ?? 0,
                data.SavedAtUtc);
        }

        public bool Save(int slotIndex, SaveData data)
        {
            if (!IsSlotIndexValid(slotIndex))
            {
                Debug.LogError($"[Save] Некорректный индекс слота: {slotIndex}.");
                return false;
            }

            if (data == null)
            {
                Debug.LogError("[Save] Попытка сохранить пустые данные.");
                return false;
            }

            data.SchemaVersion = SaveData.CurrentSchemaVersion;
            data.SavedAtUtc = DateTime.UtcNow;

            var path = GetSlotPath(slotIndex);
            var tempPath = path + TempFileExtension;
            var backupPath = path + BackupFileExtension;

            try
            {
                Directory.CreateDirectory(_rootDirectory);

                var json = JsonUtility.ToJson(data, prettyPrint: true);
                File.WriteAllText(tempPath, json);

                if (File.Exists(path))
                {
                    // Replace атомарно заменяет целевой файл и оставляет копию прежнего.
                    File.Replace(tempPath, path, backupPath);
                    SafeDelete(backupPath);
                }
                else
                {
                    File.Move(tempPath, path);
                }

                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save] Не удалось сохранить слот {slotIndex}: {exception.Message}");
                SafeDelete(tempPath);
                return false;
            }
        }

        public LoadResult Load(int slotIndex)
        {
            if (!IsSlotIndexValid(slotIndex))
                return LoadResult.Fail(SaveSlotStatus.Corrupted, "Некорректный номер слота.");

            var path = GetSlotPath(slotIndex);
            if (!File.Exists(path))
                return LoadResult.Fail(SaveSlotStatus.Empty, "Слот сохранения пуст.");

            if (!TryReadSlot(path, out var data, out var status))
            {
                return status switch
                {
                    SaveSlotStatus.UnsupportedVersion => LoadResult.Fail(
                        status,
                        "Это сохранение создано более новой версией игры и не может быть загружено."),
                    _ => LoadResult.Fail(
                        status,
                        "Файл сохранения повреждён и не может быть прочитан.")
                };
            }

            return LoadResult.Ok(data);
        }

        public bool DeleteSlot(int slotIndex)
        {
            if (!IsSlotIndexValid(slotIndex))
                return false;

            try
            {
                var path = GetSlotPath(slotIndex);
                if (!File.Exists(path))
                    return false;

                File.Delete(path);
                return true;
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save] Не удалось удалить слот {slotIndex}: {exception.Message}");
                return false;
            }
        }

        private bool TryReadSlot(string path, out SaveData data, out SaveSlotStatus status)
        {
            data = null;

            string json;
            try
            {
                json = File.ReadAllText(path);
            }
            catch (Exception exception)
            {
                Debug.LogError($"[Save] Ошибка чтения {path}: {exception.Message}");
                status = SaveSlotStatus.Corrupted;
                return false;
            }

            if (string.IsNullOrWhiteSpace(json))
            {
                status = SaveSlotStatus.Corrupted;
                return false;
            }

            SaveData parsed;
            try
            {
                parsed = JsonUtility.FromJson<SaveData>(json);
            }
            catch (Exception)
            {
                // Повреждённый JSON — это ожидаемый сценарий, а не сбой приложения (FR-004).
                status = SaveSlotStatus.Corrupted;
                return false;
            }

            if (parsed == null || parsed.SchemaVersion <= 0)
            {
                status = SaveSlotStatus.Corrupted;
                return false;
            }

            if (parsed.SchemaVersion > SaveData.CurrentSchemaVersion)
            {
                status = SaveSlotStatus.UnsupportedVersion;
                return false;
            }

            if (parsed.SchemaVersion < SaveData.CurrentSchemaVersion
                && !SaveMigrations.TryMigrate(parsed, out var migrationError))
            {
                Debug.LogError($"[Save] Миграция не удалась: {migrationError}");
                status = SaveSlotStatus.Corrupted;
                return false;
            }

            if (parsed.world == null)
            {
                status = SaveSlotStatus.Corrupted;
                return false;
            }

            data = parsed;
            status = SaveSlotStatus.Valid;
            return true;
        }

        private bool IsSlotIndexValid(int slotIndex) => slotIndex >= 0 && slotIndex < SlotCount;

        private string GetSlotPath(int slotIndex)
            => Path.Combine(_rootDirectory, $"{SlotFilePrefix}{slotIndex}{SlotFileExtension}");

        private static void SafeDelete(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[Save] Не удалось удалить временный файл {path}: {exception.Message}");
            }
        }
    }
}
