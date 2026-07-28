using System;
using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.Core;

namespace QonaevLife.Language
{
    /// <summary>Тип задания мини-урока (FR-043: минимум два типа).</summary>
    public enum LessonTaskKind
    {
        /// <summary>Выбрать правильный перевод слова из вариантов.</summary>
        ChooseTranslation = 0,

        /// <summary>Сопоставить слово с контекстом — примером употребления.</summary>
        MatchContext = 1
    }

    /// <summary>Один вопрос урока с вариантами ответа.</summary>
    public readonly struct LessonTask
    {
        public LessonTask(LessonTaskKind kind, string wordId, string prompt,
            IReadOnlyList<string> options, int correctIndex)
        {
            Kind = kind;
            WordId = wordId;
            Prompt = prompt;
            Options = options;
            CorrectIndex = correctIndex;
        }

        public LessonTaskKind Kind { get; }
        public string WordId { get; }

        /// <summary>Текст вопроса: слово или пример употребления.</summary>
        public string Prompt { get; }

        public IReadOnlyList<string> Options { get; }

        /// <summary>Индекс правильного варианта в <see cref="Options"/>.</summary>
        public int CorrectIndex { get; }
    }

    /// <summary>Итог урока.</summary>
    public readonly struct LessonResult
    {
        public LessonResult(int correctAnswers, int totalTasks, float experienceGained)
        {
            CorrectAnswers = correctAnswers;
            TotalTasks = totalTasks;
            ExperienceGained = experienceGained;
        }

        public int CorrectAnswers { get; }
        public int TotalTasks { get; }
        public float ExperienceGained { get; }

        public bool IsPerfect => TotalTasks > 0 && CorrectAnswers == TotalTasks;
    }

    public readonly struct LessonStartedEvent : IGameEvent
    {
        public LessonStartedEvent(int taskCount) => TaskCount = taskCount;
        public int TaskCount { get; }
    }

    public readonly struct LessonAnsweredEvent : IGameEvent
    {
        public LessonAnsweredEvent(string wordId, bool isCorrect, int correctIndex)
        {
            WordId = wordId;
            IsCorrect = isCorrect;
            CorrectIndex = correctIndex;
        }

        public string WordId { get; }
        public bool IsCorrect { get; }

        /// <summary>Правильный ответ — показывается игроку после ошибки (FR-043).</summary>
        public int CorrectIndex { get; }
    }

    public readonly struct LessonFinishedEvent : IGameEvent
    {
        public LessonFinishedEvent(LessonResult result) => Result = result;
        public LessonResult Result { get; }
    }

    /// <summary>
    /// Мини-уроки казахского (FR-043). Урок собирается из слов личного словаря
    /// игрока, поэтому проверяет именно то, что он встречал в диалогах.
    /// По завершении показывает результат и начисляет опыт языка.
    /// Не зависит от Unity, поэтому покрывается модульными тестами.
    /// </summary>
    public sealed class LessonService : IGameService
    {
        /// <summary>Сколько вариантов ответа в задании, включая правильный.</summary>
        public const int OptionsPerTask = 4;

        private readonly ContentDatabase _content;
        private readonly IEventBus _eventBus;
        private readonly ILanguageProgressService _language;
        private readonly LessonSettings _settings;

        private readonly List<LessonTask> _tasks = new();
        private readonly List<string> _optionBuffer = new();
        private readonly List<WordDefinition> _poolBuffer = new();

        private Random _random;
        private int _currentIndex;
        private int _correctAnswers;

        public LessonService(ContentDatabase content, IEventBus eventBus,
            ILanguageProgressService language, LessonSettings settings, int seed = 20260728)
        {
            if (!settings.IsValid())
                throw new ArgumentException("Некорректные настройки урока.", nameof(settings));

            _content = content ?? throw new ArgumentNullException(nameof(content));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _language = language ?? throw new ArgumentNullException(nameof(language));
            _settings = settings;
            _random = new Random(seed);
        }

        public bool IsActive { get; private set; }

        public int TaskCount => _tasks.Count;

        public int CurrentIndex => _currentIndex;

        /// <summary>Текущее задание или null, если урок не идёт.</summary>
        public LessonTask? CurrentTask
            => IsActive && _currentIndex < _tasks.Count ? _tasks[_currentIndex] : null;

        public int CorrectAnswers => _correctAnswers;

        public void Initialize()
        {
        }

        public void Shutdown() => Reset();

        /// <summary>
        /// Достаточно ли материала для урока. Урок строится из слов словаря,
        /// а вариантов ответа нужно не меньше <see cref="OptionsPerTask"/>.
        /// </summary>
        public bool CanStart()
        {
            if (_language.LearnedWords.Count < 1)
                return false;

            CollectPool();
            return _poolBuffer.Count >= OptionsPerTask;
        }

        /// <summary>
        /// Собирает урок из слов личного словаря. Возвращает false, если
        /// материала не хватает — тогда игрок сначала должен собрать слова
        /// в диалогах.
        /// </summary>
        public bool TryStart()
        {
            if (IsActive || !CanStart())
                return false;

            BuildTasks();

            if (_tasks.Count == 0)
                return false;

            IsActive = true;
            _currentIndex = 0;
            _correctAnswers = 0;

            _eventBus.Publish(new LessonStartedEvent(_tasks.Count));
            return true;
        }

        /// <summary>
        /// Принимает ответ. Возвращает true, если ответ верный. Урок
        /// завершается сам после последнего задания.
        /// </summary>
        public bool Answer(int optionIndex)
        {
            if (!IsActive || _currentIndex >= _tasks.Count)
                return false;

            var task = _tasks[_currentIndex];
            var isCorrect = optionIndex == task.CorrectIndex;

            if (isCorrect)
                _correctAnswers++;

            // Ответ учитывается в прогрессе освоения слова: и верный, и
            // ошибочный (FR-042, FR-043).
            _language.RegisterAnswer(task.WordId, isCorrect);

            _eventBus.Publish(new LessonAnsweredEvent(
                task.WordId, isCorrect, task.CorrectIndex));

            _currentIndex++;

            if (_currentIndex >= _tasks.Count)
                Finish();

            return isCorrect;
        }

        /// <summary>Прерывает урок. Опыт за отвеченные задания сохраняется.</summary>
        public void Abandon()
        {
            if (IsActive)
                Finish();
        }

        private void Finish()
        {
            var experience = _correctAnswers * _settings.experiencePerCorrectAnswer;

            if (_correctAnswers == _tasks.Count && _tasks.Count > 0)
                experience += _settings.perfectLessonBonus;

            if (experience > 0f)
                _language.AddExperience(experience);

            var result = new LessonResult(_correctAnswers, _tasks.Count, experience);

            IsActive = false;
            _eventBus.Publish(new LessonFinishedEvent(result));
        }

        /// <summary>
        /// Составляет задания. Оба типа чередуются, чтобы урок не сводился
        /// к одному упражнению (FR-043).
        /// </summary>
        private void BuildTasks()
        {
            _tasks.Clear();
            CollectPool();

            if (_poolBuffer.Count < OptionsPerTask)
                return;

            // Слова урока берутся из словаря: проверяем то, что игрок встречал.
            var learned = new List<WordDefinition>();

            foreach (var word in _language.LearnedWords)
            {
                if (_content.TryGetWord(word.WordId, out var definition))
                    learned.Add(definition);
            }

            if (learned.Count == 0)
                return;

            Shuffle(learned);

            var taskCount = Math.Min(_settings.tasksPerLesson, learned.Count);

            for (var i = 0; i < taskCount; i++)
            {
                var target = learned[i];

                // Задание с контекстом требует примера употребления; если его
                // нет, спрашиваем перевод.
                var wantsContext = i % 2 == 1
                                   && !string.IsNullOrWhiteSpace(target.ExampleKazakh);

                var task = wantsContext
                    ? BuildContextTask(target)
                    : BuildTranslationTask(target);

                if (task.HasValue)
                    _tasks.Add(task.Value);
            }
        }

        private LessonTask? BuildTranslationTask(WordDefinition target)
        {
            _optionBuffer.Clear();
            _optionBuffer.Add(target.Russian);

            FillDistractors(target, useRussian: true);

            if (_optionBuffer.Count < OptionsPerTask)
                return null;

            var correctIndex = ShuffleAndFindCorrect(target.Russian);

            return new LessonTask(
                LessonTaskKind.ChooseTranslation,
                target.Id,
                target.Kazakh,
                new List<string>(_optionBuffer),
                correctIndex);
        }

        /// <summary>
        /// Сопоставление слова с контекстом: показывается пример на казахском,
        /// игрок выбирает, какое слово в нём главное.
        /// </summary>
        private LessonTask? BuildContextTask(WordDefinition target)
        {
            _optionBuffer.Clear();
            _optionBuffer.Add(target.Kazakh);

            FillDistractors(target, useRussian: false);

            if (_optionBuffer.Count < OptionsPerTask)
                return null;

            var correctIndex = ShuffleAndFindCorrect(target.Kazakh);

            return new LessonTask(
                LessonTaskKind.MatchContext,
                target.Id,
                target.ExampleKazakh,
                new List<string>(_optionBuffer),
                correctIndex);
        }

        /// <summary>
        /// Добавляет неверные варианты. Берутся слова той же категории, если
        /// их хватает: выбор между «водой» и «хлебом» осмысленнее, чем между
        /// «водой» и «до свидания».
        /// </summary>
        private void FillDistractors(WordDefinition target, bool useRussian)
        {
            AddDistractorsFrom(target, useRussian, sameCategoryOnly: true);

            if (_optionBuffer.Count < OptionsPerTask)
                AddDistractorsFrom(target, useRussian, sameCategoryOnly: false);
        }

        private void AddDistractorsFrom(WordDefinition target, bool useRussian,
            bool sameCategoryOnly)
        {
            Shuffle(_poolBuffer);

            foreach (var candidate in _poolBuffer)
            {
                if (_optionBuffer.Count >= OptionsPerTask)
                    return;

                if (candidate.Id == target.Id)
                    continue;

                if (sameCategoryOnly && candidate.Category != target.Category)
                    continue;

                var option = useRussian ? candidate.Russian : candidate.Kazakh;

                // Одинаковые варианты сделали бы задание неразрешимым.
                if (string.IsNullOrWhiteSpace(option) || _optionBuffer.Contains(option))
                    continue;

                _optionBuffer.Add(option);
            }
        }

        private int ShuffleAndFindCorrect(string correctOption)
        {
            Shuffle(_optionBuffer);
            return _optionBuffer.IndexOf(correctOption);
        }

        private void CollectPool()
        {
            _poolBuffer.Clear();

            foreach (var word in _content.Words)
            {
                if (word != null
                    && !string.IsNullOrWhiteSpace(word.Kazakh)
                    && !string.IsNullOrWhiteSpace(word.Russian))
                {
                    _poolBuffer.Add(word);
                }
            }
        }

        private void Shuffle<T>(IList<T> items)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = _random.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
        }

        private void Reset()
        {
            IsActive = false;
            _tasks.Clear();
            _currentIndex = 0;
            _correctAnswers = 0;
        }
    }

    /// <summary>Настройки урока. Правятся данными, а не кодом (п. 10 ТЗ).</summary>
    [Serializable]
    public struct LessonSettings
    {
        [UnityEngine.Tooltip("Сколько заданий в одном уроке.")]
        public int tasksPerLesson;

        [UnityEngine.Tooltip("Опыт языка за верный ответ.")]
        public float experiencePerCorrectAnswer;

        [UnityEngine.Tooltip("Дополнительный опыт за урок без ошибок.")]
        public float perfectLessonBonus;

        public static LessonSettings Default => new()
        {
            tasksPerLesson = 5,
            experiencePerCorrectAnswer = 12f,
            perfectLessonBonus = 20f
        };

        public bool IsValid()
            => tasksPerLesson > 0
               && experiencePerCorrectAnswer >= 0f
               && perfectLessonBonus >= 0f;
    }
}
