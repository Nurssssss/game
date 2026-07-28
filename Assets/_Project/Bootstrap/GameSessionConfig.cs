using QonaevLife.Core;
using QonaevLife.Language;
using QonaevLife.Player;
using QonaevLife.World;
using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Bootstrap
{
    /// <summary>
    /// Балансовые настройки сессии в одном ассете (п. 10 ТЗ): стартовый капитал,
    /// скорость времени, потребности и прогресс языка правятся без перекомпиляции.
    /// </summary>
    [CreateAssetMenu(
        fileName = "GameSessionConfig",
        menuName = "Qonaev Life/Настройки сессии",
        order = 1)]
    public sealed class GameSessionConfig : ScriptableObject
    {
        [Header("Время")]
        [SerializeField] private GameClockSettings clock = GameClockSettings.Default;

        [Header("Погода")]
        [SerializeField] private WeatherSettings weather = WeatherSettings.Default;

        [SerializeField] [Tooltip("Seed погоды. Фиксирован, чтобы прогоны QA были воспроизводимы.")]
        private int weatherSeed = 12345;

        [Header("Экономика")]
        [SerializeField] [Tooltip("Стартовый капитал новой игры (FR-002).")] [Min(0)]
        private long startingCapital = 5000;

        [Header("Профессия MVP")]
        [SerializeField] [Tooltip("ID работы, доступной в вертикальном срезе (FR-070).")]
        private string primaryJobId = "job_courier";

        [SerializeField] [Tooltip("ID локации, где выдаётся смена.")]
        private string primaryJobHubLocationId = "loc_courier_hub";

        [Header("Язык")]
        [SerializeField] private LanguageProgressSettings language = LanguageProgressSettings.Default;

        [Header("Мини-уроки")]
        [SerializeField] [Tooltip("Число заданий и опыт за урок (FR-043).")]
        private LessonSettings lessons = LessonSettings.Default;

        [Header("NPC")]
        [SerializeField] [Tooltip("Радиус и бюджет полной симуляции NPC (FR-032).")]
        private Npc.NpcSimulationSettings npcSimulation = Npc.NpcSimulationSettings.Default;

        [Header("Потребности")]
        [SerializeField]
        private List<NeedSettings> needs = new()
        {
            new NeedSettings
            {
                needId = NeedIds.Hunger,
                decayPerGameMinute = 0.05f,
                criticalThreshold = 15f,
                startValue = 80f
            },
            new NeedSettings
            {
                needId = NeedIds.Energy,
                decayPerGameMinute = 0.04f,
                criticalThreshold = 15f,
                startValue = 90f
            },
            new NeedSettings
            {
                needId = NeedIds.Fatigue,
                decayPerGameMinute = 0.03f,
                criticalThreshold = 10f,
                startValue = 100f
            },
            new NeedSettings
            {
                needId = NeedIds.Mood,
                decayPerGameMinute = 0.02f,
                criticalThreshold = 20f,
                startValue = 70f
            }
        };

        [Header("Сохранения")]
        [SerializeField] [Tooltip("Число локальных слотов. ТЗ требует не менее трёх (FR-002).")]
        [Min(3)]
        private int saveSlotCount = 3;

        [SerializeField] [Tooltip("Папка внутри persistentDataPath.")]
        private string saveFolderName = "Saves";

        public GameClockSettings Clock => clock;
        public WeatherSettings Weather => weather;
        public int WeatherSeed => weatherSeed;
        public long StartingCapital => startingCapital;
        public string PrimaryJobId => primaryJobId;
        public string PrimaryJobHubLocationId => primaryJobHubLocationId;
        public Npc.NpcSimulationSettings NpcSimulation => npcSimulation;
        public LanguageProgressSettings Language => language;
        public LessonSettings Lessons => lessons;
        public IReadOnlyList<NeedSettings> Needs => needs;
        public int SaveSlotCount => saveSlotCount;
        public string SaveFolderName => saveFolderName;

        /// <summary>Проверка настроек, чтобы битый конфиг не ронял запуск игры.</summary>
        public bool TryValidate(out string error)
        {
            if (!clock.IsValid())
            {
                error = "Некорректные настройки времени: границы фаз должны возрастать.";
                return false;
            }

            if (!weather.IsValid())
            {
                error = "Некорректные настройки погоды.";
                return false;
            }

            if (!language.IsValid())
            {
                error = "Некорректные настройки прогресса языка.";
                return false;
            }

            if (needs == null || needs.Count == 0)
            {
                error = "Не задана ни одна потребность (FR-060).";
                return false;
            }

            var seen = new HashSet<string>();
            foreach (var need in needs)
            {
                if (string.IsNullOrWhiteSpace(need.needId))
                {
                    error = "Найдена потребность без needId.";
                    return false;
                }

                if (!seen.Add(need.needId))
                {
                    error = $"Дублирующаяся потребность '{need.needId}'.";
                    return false;
                }
            }

            if (saveSlotCount < 3)
            {
                error = "ТЗ требует не менее трёх слотов сохранения (FR-002).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(saveFolderName))
            {
                error = "Не задана папка сохранений.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(primaryJobId))
            {
                error = "Не задан ID профессии MVP (FR-070).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(primaryJobHubLocationId))
            {
                error = "Не задана локация выдачи смены.";
                return false;
            }

            if (!lessons.IsValid())
            {
                error = "Некорректные настройки мини-уроков (FR-043).";
                return false;
            }

            if (!npcSimulation.IsValid())
            {
                error = "Некорректные настройки симуляции NPC (FR-032).";
                return false;
            }

            error = string.Empty;
            return true;
        }
    }
}
