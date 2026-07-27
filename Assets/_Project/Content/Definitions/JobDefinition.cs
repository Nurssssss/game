using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Content
{
    /// <summary>Один шаг смены. Для курьера: получить посылку, доехать, вручить (FR-070).</summary>
    [System.Serializable]
    public struct JobStageDefinition
    {
        [Tooltip("Стабильный ключ этапа внутри смены.")]
        public string stageId;

        [Tooltip("Ключ локализации описания цели.")]
        public string objectiveKey;

        [Tooltip("ID точки интереса, где выполняется этап.")]
        public string locationId;

        [Tooltip("Лимит внутриигровых минут на этап. 0 — без лимита.")]
        [Min(0)]
        public float timeLimitMinutes;

        [Tooltip("Промежуточная выплата за этап. Может быть 0.")]
        [Min(0)]
        public long stagePayout;
    }

    /// <summary>
    /// Определение профессии/смены (п. 6 ТЗ, FR-070). Оплата, требования по навыкам
    /// и длительность конфигурируются без перекомпиляции (п. 10 ТЗ).
    /// </summary>
    [CreateAssetMenu(
        fileName = "Job_",
        menuName = "Qonaev Life/Работа/Профессия",
        order = 30)]
    public sealed class JobDefinition : ContentDefinition
    {
        [Header("Отображение")]
        [SerializeField] private string displayNameKey = string.Empty;
        [SerializeField] private string descriptionKey = string.Empty;

        [Header("Доступность")]
        [SerializeField] [Tooltip("ID локации, где выдаётся смена.")]
        private string hubLocationId = string.Empty;

        [SerializeField] [Tooltip("Фазы суток, в которые смену можно взять.")]
        private List<string> availablePhases = new();

        [SerializeField] [Tooltip("Минимальные уровни навыков: skillId -> level.")]
        private List<SkillRequirement> skillRequirements = new();

        [Header("Оплата")]
        [SerializeField] [Tooltip("Базовая оплата за успешно завершённую смену.")] [Min(0)]
        private long basePayout = 500;

        [SerializeField] [Tooltip("Бонус за выполнение всех этапов в срок.")] [Min(0)]
        private long onTimeBonus;

        [SerializeField] [Tooltip("Штраф за провал смены. Вычитается из оплаты.")] [Min(0)]
        private long failurePenalty;

        [Header("Этапы смены")]
        [SerializeField] private List<JobStageDefinition> stages = new();

        [Header("Награда за навыки")]
        [SerializeField] [Tooltip("ID навыка, растущего от этой работы.")]
        private string primarySkillId = string.Empty;

        [SerializeField] [Min(0)] private float skillExperiencePerShift = 10f;

        [SerializeField] [Tooltip("Опыт языка за смену, если в ней есть диалоги (FR-046).")] [Min(0)]
        private float languageExperiencePerShift;

        public string DisplayNameKey => displayNameKey;
        public string DescriptionKey => descriptionKey;
        public string HubLocationId => hubLocationId;
        public IReadOnlyList<string> AvailablePhases => availablePhases;
        public IReadOnlyList<SkillRequirement> SkillRequirements => skillRequirements;
        public long BasePayout => basePayout;
        public long OnTimeBonus => onTimeBonus;
        public long FailurePenalty => failurePenalty;
        public IReadOnlyList<JobStageDefinition> Stages => stages;
        public string PrimarySkillId => primarySkillId;
        public float SkillExperiencePerShift => skillExperiencePerShift;
        public float LanguageExperiencePerShift => languageExperiencePerShift;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);

            if (string.IsNullOrWhiteSpace(displayNameKey))
                errors.Add($"{name}: не заполнен ключ названия.");

            if (string.IsNullOrWhiteSpace(hubLocationId))
                errors.Add($"{name}: не указана локация выдачи смены.");

            if (stages.Count == 0)
                errors.Add($"{name}: смена без этапов не может быть выполнена (FR-070).");

            if (availablePhases.Count == 0)
                errors.Add($"{name}: не указана ни одна фаза суток — смену нельзя взять.");

            var seenStageIds = new HashSet<string>();
            for (var i = 0; i < stages.Count; i++)
            {
                var stage = stages[i];

                if (string.IsNullOrWhiteSpace(stage.stageId))
                {
                    errors.Add($"{name}: этап #{i} без stageId.");
                    continue;
                }

                if (!seenStageIds.Add(stage.stageId))
                    errors.Add($"{name}: дублирующийся stageId '{stage.stageId}'.");

                if (string.IsNullOrWhiteSpace(stage.locationId))
                    errors.Add($"{name}: этап '{stage.stageId}' без locationId (FR-092).");

                if (string.IsNullOrWhiteSpace(stage.objectiveKey))
                    errors.Add($"{name}: этап '{stage.stageId}' без ключа описания цели.");
            }

            foreach (var requirement in skillRequirements)
            {
                if (string.IsNullOrWhiteSpace(requirement.skillId))
                    errors.Add($"{name}: требование по навыку без skillId.");
            }

            if (string.IsNullOrWhiteSpace(primarySkillId) && skillExperiencePerShift > 0f)
                errors.Add($"{name}: задан опыт навыка, но не указан primarySkillId.");
        }
    }

    [System.Serializable]
    public struct SkillRequirement
    {
        public string skillId;

        [Min(0)]
        public int minLevel;
    }
}
