using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Language;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Мини-уроки казахского (FR-043, AT-003).</summary>
    [TestFixture]
    public sealed class LessonServiceTests
    {
        private readonly List<ScriptableObject> _created = new();

        private EventBus _eventBus;
        private LanguageProgressService _language;
        private ContentDatabase _content;
        private LessonService _service;

        private static LessonSettings Settings => new()
        {
            tasksPerLesson = 4,
            experiencePerCorrectAnswer = 10f,
            perfectLessonBonus = 20f
        };

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _language = new LanguageProgressService(
                _eventBus, LanguageProgressSettings.Default);

            _content = BuildContent();
            _service = new LessonService(_content, _eventBus, _language, Settings, seed: 42);
            _service.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _service.Shutdown();

            foreach (var asset in _created)
                Object.DestroyImmediate(asset);

            _created.Clear();
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        /// <summary>Восемь слов: хватает и на задания, и на неверные варианты.</summary>
        private ContentDatabase BuildContent()
        {
            var rows = new[]
            {
                ("word_su", "Су", "Вода", "Су ішемін", WordCategory.Food),
                ("word_nan", "Нан", "Хлеб", "Нан жеймін", WordCategory.Food),
                ("word_kofe", "Кофе", "Кофе", "Кофе ішемін", WordCategory.Food),
                ("word_shai", "Шай", "Чай", "Шай ішемін", WordCategory.Food),
                ("word_salem", "Сәлем", "Привет", "Сәлем айтамын", WordCategory.Greeting),
                ("word_rahmet", "Рақмет", "Спасибо", "Рақмет айтамын", WordCategory.Courtesy),
                ("word_zhumys", "Жұмыс", "Работа", "Жұмыс істеймін", WordCategory.Work),
                ("word_kala", "Қала", "Город", "Қалада тұрамын", WordCategory.City)
            };

            var words = new List<ScriptableObject>();

            foreach (var row in rows)
            {
                var word = Create<WordDefinition>();
                var so = new SerializedObject(word);
                so.FindProperty("id").stringValue = row.Item1;
                so.FindProperty("kazakh").stringValue = row.Item2;
                so.FindProperty("russian").stringValue = row.Item3;
                so.FindProperty("exampleKazakh").stringValue = row.Item4;
                so.FindProperty("exampleRussian").stringValue = "пример";
                so.FindProperty("category").enumValueIndex = (int)row.Item5;
                so.FindProperty("minLanguageLevel").intValue = 1;
                so.ApplyModifiedPropertiesWithoutUndo();
                words.Add(word);
            }

            var database = Create<ContentDatabase>();
            var dbObject = new SerializedObject(database);
            var list = dbObject.FindProperty("words");
            list.arraySize = words.Count;
            for (var i = 0; i < words.Count; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = words[i];
            dbObject.ApplyModifiedPropertiesWithoutUndo();

            return database;
        }

        private void LearnWords(params string[] wordIds)
        {
            foreach (var id in wordIds)
                _language.AddWord(id);
        }

        [Test]
        public void WithoutLearnedWords_CannotStart()
        {
            Assert.That(_service.CanStart(), Is.False);
            Assert.That(_service.TryStart(), Is.False);
            Assert.That(_service.IsActive, Is.False);
        }

        [Test]
        public void WithLearnedWords_CanStart()
        {
            LearnWords("word_su", "word_nan");

            Assert.That(_service.CanStart(), Is.True);
            Assert.That(_service.TryStart(), Is.True);
            Assert.That(_service.IsActive, Is.True);
        }

        [Test]
        public void Start_PublishesEventWithTaskCount()
        {
            LearnWords("word_su", "word_nan", "word_kofe");

            LessonStartedEvent captured = default;
            _eventBus.Subscribe<LessonStartedEvent>(e => captured = e);

            _service.TryStart();

            Assert.That(captured.TaskCount, Is.EqualTo(_service.TaskCount));
            Assert.That(captured.TaskCount, Is.GreaterThan(0));
        }

        /// <summary>Число заданий не превышает ни настройку, ни размер словаря.</summary>
        [Test]
        public void TaskCount_IsLimitedByDictionarySize()
        {
            LearnWords("word_su", "word_nan");
            _service.TryStart();

            Assert.That(_service.TaskCount, Is.EqualTo(2));
        }

        [Test]
        public void TaskCount_IsLimitedBySettings()
        {
            LearnWords("word_su", "word_nan", "word_kofe", "word_shai",
                "word_salem", "word_rahmet");

            _service.TryStart();

            Assert.That(_service.TaskCount, Is.EqualTo(Settings.tasksPerLesson));
        }

        [Test]
        public void SecondStart_WhileActive_IsRejected()
        {
            LearnWords("word_su", "word_nan");
            _service.TryStart();

            Assert.That(_service.TryStart(), Is.False);
        }

        /// <summary>FR-043: каждое задание имеет варианты и один верный ответ.</summary>
        [Test]
        public void EachTask_HasOptionsAndValidCorrectIndex()
        {
            LearnWords("word_su", "word_nan", "word_kofe", "word_shai");
            _service.TryStart();

            for (var i = 0; i < _service.TaskCount; i++)
            {
                var task = _service.CurrentTask;

                Assert.That(task, Is.Not.Null);
                Assert.That(task.Value.Options, Has.Count.EqualTo(LessonService.OptionsPerTask));
                Assert.That(task.Value.CorrectIndex, Is.InRange(0, task.Value.Options.Count - 1));
                Assert.That(task.Value.Prompt, Is.Not.Empty);
                Assert.That(task.Value.WordId, Is.Not.Empty);

                _service.Answer(task.Value.CorrectIndex);
            }
        }

        [Test]
        public void Options_ContainNoDuplicates()
        {
            LearnWords("word_su", "word_nan", "word_kofe", "word_shai");
            _service.TryStart();

            var task = _service.CurrentTask;
            Assert.That(task, Is.Not.Null);

            var unique = new HashSet<string>(task.Value.Options);
            Assert.That(unique, Has.Count.EqualTo(task.Value.Options.Count),
                "Одинаковые варианты сделали бы задание неразрешимым.");
        }

        /// <summary>FR-043: минимум два типа задания.</summary>
        [Test]
        public void Lesson_UsesBothTaskKinds()
        {
            LearnWords("word_su", "word_nan", "word_kofe", "word_shai");
            _service.TryStart();

            var kinds = new HashSet<LessonTaskKind>();

            while (_service.IsActive)
            {
                var task = _service.CurrentTask;
                if (task == null)
                    break;

                kinds.Add(task.Value.Kind);
                _service.Answer(task.Value.CorrectIndex);
            }

            Assert.That(kinds, Has.Count.EqualTo(2),
                "ТЗ требует не менее двух типов задания.");
        }

        [Test]
        public void CorrectAnswer_IsCounted()
        {
            LearnWords("word_su", "word_nan");
            _service.TryStart();

            var task = _service.CurrentTask;
            var result = _service.Answer(task.Value.CorrectIndex);

            Assert.That(result, Is.True);
            Assert.That(_service.CorrectAnswers, Is.EqualTo(1));
        }

        [Test]
        public void WrongAnswer_IsNotCounted()
        {
            LearnWords("word_su", "word_nan");
            _service.TryStart();

            var task = _service.CurrentTask;
            var wrong = (task.Value.CorrectIndex + 1) % task.Value.Options.Count;

            Assert.That(_service.Answer(wrong), Is.False);
            Assert.That(_service.CorrectAnswers, Is.Zero);
        }

        /// <summary>FR-043: после ошибки игрок узнаёт правильный ответ.</summary>
        [Test]
        public void AnswerEvent_ReportsCorrectIndex()
        {
            LearnWords("word_su", "word_nan");
            _service.TryStart();

            var task = _service.CurrentTask;
            var expectedIndex = task.Value.CorrectIndex;
            var wrong = (expectedIndex + 1) % task.Value.Options.Count;

            LessonAnsweredEvent captured = default;
            _eventBus.Subscribe<LessonAnsweredEvent>(e => captured = e);

            _service.Answer(wrong);

            Assert.That(captured.IsCorrect, Is.False);
            Assert.That(captured.CorrectIndex, Is.EqualTo(expectedIndex));
            Assert.That(captured.WordId, Is.EqualTo(task.Value.WordId));
        }

        [Test]
        public void Answer_AdvancesToNextTask()
        {
            LearnWords("word_su", "word_nan", "word_kofe");
            _service.TryStart();

            var first = _service.CurrentTask;
            _service.Answer(first.Value.CorrectIndex);

            Assert.That(_service.CurrentIndex, Is.EqualTo(1));
            Assert.That(_service.CurrentTask.Value.WordId, Is.Not.EqualTo(first.Value.WordId));
        }

        [Test]
        public void LastAnswer_FinishesLesson()
        {
            LearnWords("word_su", "word_nan");
            _service.TryStart();

            LessonFinishedEvent finished = default;
            var count = 0;
            _eventBus.Subscribe<LessonFinishedEvent>(e => { finished = e; count++; });

            while (_service.IsActive)
                _service.Answer(_service.CurrentTask.Value.CorrectIndex);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(finished.Result.TotalTasks, Is.EqualTo(2));
            Assert.That(finished.Result.CorrectAnswers, Is.EqualTo(2));
            Assert.That(finished.Result.IsPerfect, Is.True);
        }

        /// <summary>FR-043: урок начисляет опыт языка.</summary>
        [Test]
        public void PerfectLesson_AwardsExperienceWithBonus()
        {
            LearnWords("word_su", "word_nan");

            var experienceBefore = _language.Experience;
            _service.TryStart();

            while (_service.IsActive)
                _service.Answer(_service.CurrentTask.Value.CorrectIndex);

            // Два верных ответа по 10 плюс бонус 20 за урок без ошибок.
            Assert.That(_language.Experience, Is.GreaterThan(experienceBefore));
        }

        [Test]
        public void WrongAnswers_ReduceExperience()
        {
            LearnWords("word_su", "word_nan");
            _service.TryStart();

            LessonFinishedEvent finished = default;
            _eventBus.Subscribe<LessonFinishedEvent>(e => finished = e);

            while (_service.IsActive)
            {
                var task = _service.CurrentTask;
                var wrong = (task.Value.CorrectIndex + 1) % task.Value.Options.Count;
                _service.Answer(wrong);
            }

            Assert.That(finished.Result.CorrectAnswers, Is.Zero);
            Assert.That(finished.Result.ExperienceGained, Is.Zero);
            Assert.That(finished.Result.IsPerfect, Is.False);
        }

        /// <summary>Ответы урока влияют на освоение слова (FR-042).</summary>
        [Test]
        public void Answers_AdvanceWordMastery()
        {
            LearnWords("word_su", "word_nan");
            _service.TryStart();

            var task = _service.CurrentTask;
            var wordId = task.Value.WordId;

            _language.TryGetWord(wordId, out var before);
            _service.Answer(task.Value.CorrectIndex);
            _language.TryGetWord(wordId, out var after);

            Assert.That(after.CorrectAnswers, Is.EqualTo(before.CorrectAnswers + 1));
        }

        [Test]
        public void Abandon_FinishesWithPartialProgress()
        {
            LearnWords("word_su", "word_nan", "word_kofe");
            _service.TryStart();

            _service.Answer(_service.CurrentTask.Value.CorrectIndex);

            LessonFinishedEvent finished = default;
            _eventBus.Subscribe<LessonFinishedEvent>(e => finished = e);

            _service.Abandon();

            Assert.That(_service.IsActive, Is.False);
            Assert.That(finished.Result.CorrectAnswers, Is.EqualTo(1),
                "Опыт за отвеченные задания сохраняется.");
        }

        [Test]
        public void AnswerWithoutActiveLesson_IsRejected()
        {
            Assert.That(_service.Answer(0), Is.False);
        }

        [Test]
        public void CurrentTask_IsNullWhenInactive()
        {
            Assert.That(_service.CurrentTask, Is.Null);
        }

        [Test]
        public void InvalidSettings_AreRejected()
        {
            var invalid = Settings;
            invalid.tasksPerLesson = 0;

            Assert.Throws<System.ArgumentException>(
                () => new LessonService(_content, _eventBus, _language, invalid));
        }

        /// <summary>Одинаковый seed даёт одинаковый урок — QA воспроизводит прогон.</summary>
        [Test]
        public void SameSeed_ProducesSameLesson()
        {
            LearnWords("word_su", "word_nan", "word_kofe", "word_shai");

            var first = new LessonService(_content, _eventBus, _language, Settings, seed: 7);
            first.TryStart();
            var firstPrompt = first.CurrentTask.Value.Prompt;

            var second = new LessonService(_content, _eventBus, _language, Settings, seed: 7);
            second.TryStart();

            Assert.That(second.CurrentTask.Value.Prompt, Is.EqualTo(firstPrompt));
        }
    }
}
