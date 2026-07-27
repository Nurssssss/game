using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Content
{
    public enum AgeCategory
    {
        Teen = 0,
        Young = 1,
        Adult = 2,
        Senior = 3
    }

    /// <summary>Одна запись расписания: где NPC находится в заданную фазу суток (п. 6 ТЗ).</summary>
    [System.Serializable]
    public struct ScheduleEntry
    {
        [Tooltip("Стабильный ключ записи расписания.")]
        public string entryId;

        [Tooltip("Фаза суток: Morning, Day, Evening, Night.")]
        public string dayPhase;

        [Tooltip("ID точки назначения в эту фазу.")]
        public string locationId;

        [Tooltip("Поведение на месте: idle, work, walk, sleep.")]
        public string behaviour;

        [Tooltip("Приоритет при конфликте записей. Больше — важнее.")]
        public int priority;
    }

    /// <summary>
    /// Профиль именного NPC (FR-030). Все поля доступны контент-менеджеру
    /// без правки кода.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Npc_",
        menuName = "Qonaev Life/NPC/Профиль",
        order = 40)]
    public sealed class NpcDefinition : ContentDefinition
    {
        [Header("Личность")]
        [SerializeField] [Tooltip("Ключ локализации имени.")]
        private string displayNameKey = string.Empty;

        [SerializeField] private AgeCategory ageCategory = AgeCategory.Adult;

        [SerializeField] [Tooltip("Ключ локализации профессии.")]
        private string professionKey = string.Empty;

        [Header("Места")]
        [SerializeField] [Tooltip("ID локации дома.")]
        private string homeLocationId = string.Empty;

        [SerializeField] [Tooltip("ID локации работы. Может быть пустым.")]
        private string workLocationId = string.Empty;

        [Header("Поведение")]
        [SerializeField] private List<ScheduleEntry> schedule = new();

        [SerializeField] [Tooltip("Стартовое настроение: -1 враждебное, 0 нейтральное, 1 доброе.")]
        [Range(-1f, 1f)]
        private float baseMood;

        [SerializeField] [Tooltip("Стартовое доверие к игроку (FR-034).")]
        [Range(0f, 1f)]
        private float initialTrust = 0.5f;

        [Header("Диалог и язык")]
        [SerializeField] [Tooltip("ID корневого узла диалога.")]
        private string rootDialogueId = string.Empty;

        [SerializeField] [Tooltip("Говорит ли NPC преимущественно на казахском.")]
        private bool prefersKazakh;

        [Header("Визуал")]
        [SerializeField] private Sprite portrait;
        [SerializeField] [Tooltip("Адресуемый ключ префаба NPC, а не прямая ссылка.")]
        private string addressablePrefabKey = string.Empty;

        public string DisplayNameKey => displayNameKey;
        public AgeCategory AgeCategory => ageCategory;
        public string ProfessionKey => professionKey;
        public string HomeLocationId => homeLocationId;
        public string WorkLocationId => workLocationId;
        public IReadOnlyList<ScheduleEntry> Schedule => schedule;
        public float BaseMood => baseMood;
        public float InitialTrust => initialTrust;
        public string RootDialogueId => rootDialogueId;
        public bool PrefersKazakh => prefersKazakh;
        public Sprite Portrait => portrait;
        public string AddressablePrefabKey => addressablePrefabKey;

        private static readonly string[] ValidPhases = { "Morning", "Day", "Evening", "Night" };

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);

            if (string.IsNullOrWhiteSpace(displayNameKey))
                errors.Add($"{name}: не заполнен ключ имени.");

            if (string.IsNullOrWhiteSpace(homeLocationId))
                errors.Add($"{name}: не указан дом NPC (FR-030).");

            if (schedule.Count == 0)
                errors.Add($"{name}: у NPC нет расписания (FR-031).");

            var seenEntryIds = new HashSet<string>();
            var coveredPhases = new HashSet<string>();

            foreach (var entry in schedule)
            {
                if (string.IsNullOrWhiteSpace(entry.entryId))
                {
                    errors.Add($"{name}: запись расписания без entryId.");
                    continue;
                }

                if (!seenEntryIds.Add(entry.entryId))
                    errors.Add($"{name}: дублирующийся entryId '{entry.entryId}'.");

                if (System.Array.IndexOf(ValidPhases, entry.dayPhase) < 0)
                {
                    errors.Add($"{name}: запись '{entry.entryId}' содержит " +
                               $"неизвестную фазу '{entry.dayPhase}'.");
                }
                else
                {
                    coveredPhases.Add(entry.dayPhase);
                }

                if (string.IsNullOrWhiteSpace(entry.locationId))
                    errors.Add($"{name}: запись '{entry.entryId}' без locationId.");
            }

            // AT-004 требует проверяемого поведения в рабочее и нерабочее время.
            foreach (var phase in ValidPhases)
            {
                if (!coveredPhases.Contains(phase))
                    errors.Add($"{name}: в расписании не покрыта фаза '{phase}'.");
            }
        }
    }
}
