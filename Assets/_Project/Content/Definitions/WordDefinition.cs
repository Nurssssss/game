using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Content
{
    /// <summary>Категория словарной статьи для фильтров и уроков.</summary>
    public enum WordCategory
    {
        Greeting = 0,
        Food = 1,
        City = 2,
        Work = 3,
        Transport = 4,
        Numbers = 5,
        Everyday = 6,
        Courtesy = 7
    }

    /// <summary>
    /// Словарная статья (п. 6 ТЗ): казахское слово, перевод, транскрипция и пример.
    /// Правки текста делаются в данных, а не в коде (п. 11 ТЗ).
    /// </summary>
    [CreateAssetMenu(
        fileName = "Word_",
        menuName = "Qonaev Life/Язык/Слово",
        order = 10)]
    public sealed class WordDefinition : ContentDefinition
    {
        [Header("Текст")]
        [SerializeField] [Tooltip("Слово или выражение на казахском.")]
        private string kazakh = string.Empty;

        [SerializeField] [Tooltip("Перевод на русский.")]
        private string russian = string.Empty;

        [SerializeField] [Tooltip("Транскрипция. Необязательна.")]
        private string transcription = string.Empty;

        [Header("Пример употребления")]
        [SerializeField] [TextArea(2, 4)] private string exampleKazakh = string.Empty;
        [SerializeField] [TextArea(2, 4)] private string exampleRussian = string.Empty;

        [Header("Классификация")]
        [SerializeField] private WordCategory category = WordCategory.Everyday;

        [SerializeField]
        [Tooltip("С какого уровня языка слово встречается в уроках.")]
        [Min(1)]
        private int minLanguageLevel = 1;

        public string Kazakh => kazakh;
        public string Russian => russian;
        public string Transcription => transcription;
        public string ExampleKazakh => exampleKazakh;
        public string ExampleRussian => exampleRussian;
        public WordCategory Category => category;
        public int MinLanguageLevel => minLanguageLevel;

        public bool HasTranscription => !string.IsNullOrWhiteSpace(transcription);

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);

            // Пустой перевод — блокирующая ошибка проверки контента (FR-045).
            if (string.IsNullOrWhiteSpace(kazakh))
                errors.Add($"{name}: не заполнен казахский текст.");

            if (string.IsNullOrWhiteSpace(russian))
                errors.Add($"{name}: не заполнен русский перевод.");

            if (minLanguageLevel < 1)
                errors.Add($"{name}: minLanguageLevel должен быть не меньше 1.");

            // Пример нужен целиком либо не нужен вовсе.
            var hasKazakhExample = !string.IsNullOrWhiteSpace(exampleKazakh);
            var hasRussianExample = !string.IsNullOrWhiteSpace(exampleRussian);
            if (hasKazakhExample != hasRussianExample)
                errors.Add($"{name}: пример заполнен только на одном языке.");
        }
    }
}
