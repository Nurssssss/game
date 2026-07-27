using System;
using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Core
{
    /// <summary>
    /// Корень пользовательского сохранения. Содержит только сериализуемые данные
    /// и стабильные строковые ID — никаких ссылок на сцены, префабы или GameObject (п. 7 ТЗ).
    /// </summary>
    [Serializable]
    public sealed class SaveData
    {
        /// <summary>Текущая версия схемы. Повышается при несовместимом изменении формата.</summary>
        public const int CurrentSchemaVersion = 1;

        [SerializeField] private int schemaVersion = CurrentSchemaVersion;
        [SerializeField] private string profileName = string.Empty;
        [SerializeField] private string savedAtUtc = string.Empty;

        public WorldSaveData world = new();
        public PlayerSaveData player = new();
        public EconomySaveData economy = new();
        public LanguageSaveData language = new();
        public List<NpcSaveData> npcs = new();
        public List<QuestSaveData> quests = new();
        public List<PropertySaveData> properties = new();

        public int SchemaVersion
        {
            get => schemaVersion;
            set => schemaVersion = value;
        }

        public string ProfileName
        {
            get => profileName;
            set => profileName = value ?? string.Empty;
        }

        /// <summary>Момент записи в UTC, ISO-8601 (round-trip формат).</summary>
        public DateTime SavedAtUtc
        {
            get => DateTime.TryParse(savedAtUtc, null,
                System.Globalization.DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : default;
            set => savedAtUtc = value.ToUniversalTime().ToString("O");
        }

        public static SaveData CreateNew(string profile, GameClockSettings clockSettings)
            => new()
            {
                schemaVersion = CurrentSchemaVersion,
                ProfileName = profile,
                world = WorldSaveData.CreateNew(clockSettings)
            };
    }

    /// <summary>Дата/время, погода, открытые точки и состояние интерактивных объектов.</summary>
    [Serializable]
    public sealed class WorldSaveData
    {
        public int day = 1;
        public double minutesOfDay;
        public string weatherId = string.Empty;
        public string activeSectorId = string.Empty;
        public List<string> discoveredLocationIds = new();
        public List<InteractableStateData> interactableStates = new();

        public static WorldSaveData CreateNew(GameClockSettings clockSettings)
            => new()
            {
                day = 1,
                minutesOfDay = clockSettings.startHour * 60d
            };
    }

    [Serializable]
    public sealed class InteractableStateData
    {
        public string interactableId = string.Empty;
        public bool isUnlocked;
        public bool isConsumed;
    }

    /// <summary>Позиция в безопасной точке, потребности, навыки, инвентарь (п. 7 ТЗ).</summary>
    [Serializable]
    public sealed class PlayerSaveData
    {
        /// <summary>ID безопасной точки восстановления, а не сырые координаты сцены.</summary>
        public string safeSpawnLocationId = string.Empty;

        public Vector3 localPosition;
        public float yaw;

        public List<NeedValueData> needs = new();
        public List<SkillValueData> skills = new();
        public List<InventoryEntryData> inventory = new();
        public float reputation;
    }

    [Serializable]
    public sealed class NeedValueData
    {
        public string needId = string.Empty;
        public float value;
    }

    [Serializable]
    public sealed class SkillValueData
    {
        public string skillId = string.Empty;
        public int level;
        public float experience;
    }

    [Serializable]
    public sealed class InventoryEntryData
    {
        public string itemId = string.Empty;
        public int quantity;
    }

    /// <summary>Баланс и ограниченная по размеру история транзакций (п. 7 ТЗ).</summary>
    [Serializable]
    public sealed class EconomySaveData
    {
        public long balance;
        public List<TransactionRecordData> recentTransactions = new();
    }

    [Serializable]
    public sealed class TransactionRecordData
    {
        public string transactionId = string.Empty;
        public long amount;
        public string reasonId = string.Empty;
        public string sourceId = string.Empty;
        public int gameDay;
        public double gameMinutesOfDay;
    }

    /// <summary>Выученные слова, результаты уроков, уровень языка, режим подсказок.</summary>
    [Serializable]
    public sealed class LanguageSaveData
    {
        public int level;
        public float experience;
        public string translationMode = string.Empty;
        public List<LearnedWordData> learnedWords = new();
        public List<string> completedLessonIds = new();
    }

    [Serializable]
    public sealed class LearnedWordData
    {
        public string wordId = string.Empty;
        public int masteryStage;
        public int correctAnswers;
        public int wrongAnswers;
    }

    /// <summary>Доверие, флаги диалогов, логическое место и этап расписания (п. 7 ТЗ).</summary>
    [Serializable]
    public sealed class NpcSaveData
    {
        public string npcId = string.Empty;
        public float trust;
        public string currentLocationId = string.Empty;
        public string scheduleEntryId = string.Empty;
        public List<string> dialogueFlags = new();
    }

    [Serializable]
    public sealed class QuestSaveData
    {
        public string questId = string.Empty;
        public string state = string.Empty;
        public int objectiveIndex;
        public List<QuestCounterData> counters = new();
    }

    [Serializable]
    public sealed class QuestCounterData
    {
        public string counterId = string.Empty;
        public int value;
    }

    [Serializable]
    public sealed class PropertySaveData
    {
        public string propertyId = string.Empty;
        public bool isOwned;
        public bool isRented;
        public List<PlacedFurnitureData> placedFurniture = new();
    }

    [Serializable]
    public sealed class PlacedFurnitureData
    {
        public string itemId = string.Empty;
        public string slotId = string.Empty;
    }
}
