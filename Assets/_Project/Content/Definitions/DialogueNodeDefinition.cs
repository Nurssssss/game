using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Content
{
    /// <summary>
    /// Двуязычная реплика. Одна и та же реплика показывается в выбранном режиме
    /// без дублирования логики квеста (FR-040).
    /// </summary>
    [System.Serializable]
    public struct BilingualLine
    {
        [TextArea(2, 5)] [Tooltip("Текст на русском.")]
        public string russian;

        [TextArea(2, 5)] [Tooltip("Текст на казахском.")]
        public string kazakh;

        [Tooltip("ID слов из словаря, встречающихся в реплике. Их можно добавить в словарь.")]
        public List<string> wordIds;

        public bool IsEmpty
            => string.IsNullOrWhiteSpace(russian) && string.IsNullOrWhiteSpace(kazakh);
    }

    /// <summary>Эффект выбора реплики. Доверие меняется только явно (FR-034).</summary>
    [System.Serializable]
    public struct DialogueEffect
    {
        [Tooltip("Тип эффекта: trust, quest, flag, money, languageXp.")]
        public string effectType;

        [Tooltip("Цель эффекта: ID квеста, флага или NPC.")]
        public string targetId;

        [Tooltip("Величина эффекта.")]
        public float value;
    }

    /// <summary>Вариант ответа игрока.</summary>
    [System.Serializable]
    public struct DialogueChoice
    {
        [Tooltip("Стабильный ключ варианта.")]
        public string choiceId;

        public BilingualLine line;

        [Tooltip("ID следующего узла. Пусто — диалог завершается.")]
        public string nextNodeId;

        [Tooltip("Минимальный уровень языка для этого варианта (FR-046).")]
        [Min(0)]
        public int requiredLanguageLevel;

        [Tooltip("Минимальное доверие NPC для этого варианта.")]
        [Range(0f, 1f)]
        public float requiredTrust;

        [Tooltip("Требуемый флаг диалога. Пусто — без условия.")]
        public string requiredFlag;

        public List<DialogueEffect> effects;
    }

    /// <summary>
    /// Узел диалога (п. 6 ТЗ): говорящий, локализации, варианты ответа,
    /// условия, эффекты и словарные теги.
    /// </summary>
    [CreateAssetMenu(
        fileName = "Dialogue_",
        menuName = "Qonaev Life/Диалоги/Узел",
        order = 50)]
    public sealed class DialogueNodeDefinition : ContentDefinition
    {
        [Header("Говорящий")]
        [SerializeField] [Tooltip("ID NPC, произносящего реплику.")]
        private string speakerNpcId = string.Empty;

        [Header("Реплика")]
        [SerializeField] private BilingualLine line;

        [Header("Варианты ответа")]
        [SerializeField] private List<DialogueChoice> choices = new();

        [Header("Учебный режим")]
        [SerializeField]
        [Tooltip("Упражнение: в этом узле перевод скрывается принудительно (п. 5.5 ТЗ).")]
        private bool isTranslationExercise;

        [SerializeField] [Tooltip("Опыт языка за прохождение узла.")] [Min(0)]
        private float languageExperience;

        public string SpeakerNpcId => speakerNpcId;
        public BilingualLine Line => line;
        public IReadOnlyList<DialogueChoice> Choices => choices;
        public bool IsTranslationExercise => isTranslationExercise;
        public float LanguageExperience => languageExperience;

        /// <summary>Терминальный узел — без вариантов ответа, завершает диалог.</summary>
        public bool IsTerminal => choices.Count == 0;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);

            if (string.IsNullOrWhiteSpace(speakerNpcId))
                errors.Add($"{name}: не указан говорящий NPC.");

            // Пустая локализация — блокирующая ошибка (FR-045, NFR-022).
            if (string.IsNullOrWhiteSpace(line.russian))
                errors.Add($"{name}: пустая русская реплика.");

            if (string.IsNullOrWhiteSpace(line.kazakh))
                errors.Add($"{name}: пустая казахская реплика (FR-040).");

            var seenChoiceIds = new HashSet<string>();
            for (var i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];

                if (string.IsNullOrWhiteSpace(choice.choiceId))
                {
                    errors.Add($"{name}: вариант #{i} без choiceId.");
                    continue;
                }

                if (!seenChoiceIds.Add(choice.choiceId))
                    errors.Add($"{name}: дублирующийся choiceId '{choice.choiceId}'.");

                if (choice.line.IsEmpty)
                    errors.Add($"{name}: вариант '{choice.choiceId}' без текста.");
                else if (string.IsNullOrWhiteSpace(choice.line.kazakh))
                    errors.Add($"{name}: вариант '{choice.choiceId}' без казахского текста.");

                // Ссылка на себя — гарантированный вечный цикл в диалоге.
                if (choice.nextNodeId == Id && !string.IsNullOrWhiteSpace(Id))
                    errors.Add($"{name}: вариант '{choice.choiceId}' ссылается на свой же узел.");
            }

            if (isTranslationExercise && choices.Count == 0)
                errors.Add($"{name}: упражнение без вариантов ответа не проверяет перевод.");
        }
    }
}
