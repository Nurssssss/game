using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Dialogue;
using QonaevLife.Language;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Двуязычные диалоги (FR-033, FR-040 — FR-042, AT-003).</summary>
    [TestFixture]
    public sealed class DialogueServiceTests
    {
        private const string NpcId = "npc_aidana";
        private const string RootNodeId = "dlg_greeting";
        private const string SecondNodeId = "dlg_followup";
        private const string WordId = "word_salem";

        private readonly List<ScriptableObject> _created = new();

        private EventBus _eventBus;
        private LanguageProgressService _language;
        private ContentDatabase _content;
        private DialogueService _service;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _language = new LanguageProgressService(_eventBus, new LanguageProgressSettings
            {
                experiencePerLevel = 100f,
                maxLevel = 5,
                correctAnswersPerStage = 2,
                hideTranslationFromStage = MasteryStage.Familiar
            });

            _content = BuildContent();
            _service = new DialogueService(_content, _eventBus, _language);
        }

        [TearDown]
        public void TearDown()
        {
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

        private ContentDatabase BuildContent()
        {
            var word = Create<WordDefinition>();
            SetString(word, "id", WordId);
            SetString(word, "kazakh", "Сәлем");
            SetString(word, "russian", "Привет");

            var npc = Create<NpcDefinition>();
            SetString(npc, "id", NpcId);
            SetString(npc, "displayNameKey", "npc.aidana");
            SetString(npc, "homeLocationId", "loc_apartment_01");
            SetString(npc, "rootDialogueId", RootNodeId);

            var second = Create<DialogueNodeDefinition>();
            SetString(second, "id", SecondNodeId);
            SetString(second, "speakerNpcId", NpcId);
            SetLine(second, "line", "Хорошего дня!", "Күнің жақсы өтсін!", null);

            var root = Create<DialogueNodeDefinition>();
            SetString(root, "id", RootNodeId);
            SetString(root, "speakerNpcId", NpcId);
            SetLine(root, "line", "Привет!", "Сәлем!", new[] { WordId });
            SetFloat(root, "languageExperience", 25f);
            SetChoices(root, new[]
            {
                new ChoiceSpec
                {
                    ChoiceId = "choice_simple",
                    Russian = "И тебе привет",
                    Kazakh = "Сәлем",
                    NextNodeId = SecondNodeId,
                    RequiredLanguageLevel = 0,
                    RequiredTrust = 0f,
                    TrustDelta = 0.1f
                },
                new ChoiceSpec
                {
                    ChoiceId = "choice_fluent",
                    Russian = "Как ваши дела сегодня?",
                    Kazakh = "Бүгін қалыңыз қалай?",
                    NextNodeId = SecondNodeId,
                    RequiredLanguageLevel = 3,
                    RequiredTrust = 0f
                },
                new ChoiceSpec
                {
                    ChoiceId = "choice_trusted",
                    Russian = "Можно попросить об услуге?",
                    Kazakh = "Өтініш білдіруге бола ма?",
                    NextNodeId = string.Empty,
                    RequiredLanguageLevel = 0,
                    RequiredTrust = 0.8f
                }
            });

            var database = Create<ContentDatabase>();
            SetObjectList(database, "words", new ScriptableObject[] { word });
            SetObjectList(database, "npcs", new ScriptableObject[] { npc });
            SetObjectList(database, "dialogueNodes", new ScriptableObject[] { root, second });

            return database;
        }

        private struct ChoiceSpec
        {
            public string ChoiceId;
            public string Russian;
            public string Kazakh;
            public string NextNodeId;
            public int RequiredLanguageLevel;
            public float RequiredTrust;
            public float TrustDelta;
        }

        private static void SetString(ScriptableObject target, string field, string value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetFloat(ScriptableObject target, string field, float value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).floatValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetBool(ScriptableObject target, string field, bool value)
        {
            var so = new SerializedObject(target);
            so.FindProperty(field).boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetLine(ScriptableObject target, string field, string russian,
            string kazakh, IReadOnlyList<string> wordIds)
        {
            var so = new SerializedObject(target);
            var line = so.FindProperty(field);
            line.FindPropertyRelative("russian").stringValue = russian;
            line.FindPropertyRelative("kazakh").stringValue = kazakh;

            var words = line.FindPropertyRelative("wordIds");
            words.arraySize = wordIds?.Count ?? 0;
            for (var i = 0; i < (wordIds?.Count ?? 0); i++)
                words.GetArrayElementAtIndex(i).stringValue = wordIds[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetChoices(ScriptableObject target, IReadOnlyList<ChoiceSpec> specs)
        {
            var so = new SerializedObject(target);
            var choices = so.FindProperty("choices");
            choices.arraySize = specs.Count;

            for (var i = 0; i < specs.Count; i++)
            {
                var spec = specs[i];
                var element = choices.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("choiceId").stringValue = spec.ChoiceId;
                element.FindPropertyRelative("nextNodeId").stringValue = spec.NextNodeId;
                element.FindPropertyRelative("requiredLanguageLevel").intValue =
                    spec.RequiredLanguageLevel;
                element.FindPropertyRelative("requiredTrust").floatValue = spec.RequiredTrust;
                element.FindPropertyRelative("requiredFlag").stringValue = string.Empty;

                var line = element.FindPropertyRelative("line");
                line.FindPropertyRelative("russian").stringValue = spec.Russian;
                line.FindPropertyRelative("kazakh").stringValue = spec.Kazakh;
                line.FindPropertyRelative("wordIds").arraySize = 0;

                var effects = element.FindPropertyRelative("effects");
                if (spec.TrustDelta != 0f)
                {
                    effects.arraySize = 1;
                    var effect = effects.GetArrayElementAtIndex(0);
                    effect.FindPropertyRelative("effectType").stringValue = "trust";
                    effect.FindPropertyRelative("targetId").stringValue = NpcId;
                    effect.FindPropertyRelative("value").floatValue = spec.TrustDelta;
                }
                else
                {
                    effects.arraySize = 0;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectList(ScriptableObject target, string field,
            IReadOnlyList<ScriptableObject> values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Count;

            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        [Test]
        public void Start_ActivatesRootNode()
        {
            var started = _service.TryStart(NpcId, trust: 0.5f, flags: null);

            Assert.That(started, Is.True);
            Assert.That(_service.IsActive, Is.True);
            Assert.That(_service.CurrentNodeId, Is.EqualTo(RootNodeId));
            Assert.That(_service.CurrentNpcId, Is.EqualTo(NpcId));
        }

        [Test]
        public void Start_UnknownNpc_IsRejected()
        {
            Assert.That(_service.TryStart("npc_missing", 0.5f, null), Is.False);
            Assert.That(_service.IsActive, Is.False);
        }

        [Test]
        public void Start_PublishesEvent()
        {
            DialogueStartedEvent captured = default;
            _eventBus.Subscribe<DialogueStartedEvent>(e => captured = e);

            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(captured.NpcId, Is.EqualTo(NpcId));
            Assert.That(captured.NodeId, Is.EqualTo(RootNodeId));
        }

        /// <summary>FR-041: русский с казахским переводом.</summary>
        [Test]
        public void RussianWithKazakhMode_ShowsRussianPrimary()
        {
            _language.SetMode(TranslationMode.RussianWithKazakh);
            _service.TryStart(NpcId, 0.5f, null);

            var line = _service.GetCurrentLine();

            Assert.That(line.Primary, Is.EqualTo("Привет!"));
            Assert.That(line.Translation, Is.EqualTo("Сәлем!"));
        }

        /// <summary>FR-041: казахский с русским переводом.</summary>
        [Test]
        public void KazakhWithRussianMode_SwapsPrimaryAndTranslation()
        {
            _language.SetMode(TranslationMode.KazakhWithRussian);
            _service.TryStart(NpcId, 0.5f, null);

            var line = _service.GetCurrentLine();

            Assert.That(line.Primary, Is.EqualTo("Сәлем!"));
            Assert.That(line.Translation, Is.EqualTo("Привет!"));
        }

        /// <summary>FR-041: режим погружения без перевода.</summary>
        [Test]
        public void KazakhOnlyMode_HidesTranslation()
        {
            _language.SetMode(TranslationMode.KazakhOnly);
            _service.TryStart(NpcId, 0.5f, null);

            var line = _service.GetCurrentLine();

            Assert.That(line.Primary, Is.EqualTo("Сәлем!"));
            Assert.That(line.HasTranslation, Is.False);
        }

        /// <summary>FR-041: переключение режима не сбрасывает прогресс диалога.</summary>
        [Test]
        public void ChangingMode_KeepsCurrentNode()
        {
            _service.TryStart(NpcId, 0.5f, null);
            _service.TrySelectChoice(0);

            var nodeBefore = _service.CurrentNodeId;
            _language.SetMode(TranslationMode.KazakhOnly);

            Assert.That(_service.CurrentNodeId, Is.EqualTo(nodeBefore));
            Assert.That(_service.IsActive, Is.True);
        }

        /// <summary>FR-044: у освоенного слова перевод скрывается.</summary>
        [Test]
        public void MasteredWord_HidesTranslationInDialogue()
        {
            _service.TryStart(NpcId, 0.5f, null);
            Assert.That(_service.GetCurrentLine().HasTranslation, Is.True);

            for (var i = 0; i < 8; i++)
                _language.RegisterAnswer(WordId, isCorrect: true);

            _service.End();
            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(_service.GetCurrentLine().HasTranslation, Is.False);
        }

        [Test]
        public void ForceFullTranslation_RestoresTranslation()
        {
            for (var i = 0; i < 8; i++)
                _language.RegisterAnswer(WordId, isCorrect: true);

            _language.ForceFullTranslation = true;
            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(_service.GetCurrentLine().HasTranslation, Is.True);
        }

        /// <summary>П. 5.5 ТЗ: в упражнении перевод скрыт независимо от режима.</summary>
        [Test]
        public void TranslationExercise_HidesTranslation()
        {
            SetBool(_content.DialogueNodes[0], "isTranslationExercise", true);
            _language.SetMode(TranslationMode.RussianWithKazakh);

            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(_service.GetCurrentLine().HasTranslation, Is.False);
        }

        [Test]
        public void TranslationExercise_YieldsToAccessibilitySetting()
        {
            SetBool(_content.DialogueNodes[0], "isTranslationExercise", true);
            _language.ForceFullTranslation = true;

            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(_service.GetCurrentLine().HasTranslation, Is.True);
        }

        /// <summary>FR-046: уровень языка открывает варианты ответа.</summary>
        [Test]
        public void LowLanguageLevel_LocksFluentChoice()
        {
            _service.TryStart(NpcId, 0.5f, null);

            var choices = _service.Choices;

            Assert.That(choices, Has.Count.EqualTo(3));
            Assert.That(choices[0].IsAvailable, Is.True);
            Assert.That(choices[1].IsAvailable, Is.False);
            Assert.That(choices[1].LockedReasonKey,
                Is.EqualTo(DialogueService.LockedByLanguageKey));
        }

        [Test]
        public void HigherLanguageLevel_UnlocksFluentChoice()
        {
            _language.AddExperience(250f); // уровень 3
            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(_service.Choices[1].IsAvailable, Is.True);
        }

        [Test]
        public void LowTrust_LocksTrustedChoice()
        {
            _service.TryStart(NpcId, trust: 0.2f, flags: null);

            Assert.That(_service.Choices[2].IsAvailable, Is.False);
            Assert.That(_service.Choices[2].LockedReasonKey,
                Is.EqualTo(DialogueService.LockedByTrustKey));
        }

        [Test]
        public void HighTrust_UnlocksTrustedChoice()
        {
            _service.TryStart(NpcId, trust: 0.9f, flags: null);

            Assert.That(_service.Choices[2].IsAvailable, Is.True);
        }

        [Test]
        public void SelectingLockedChoice_IsRejected()
        {
            _service.TryStart(NpcId, 0.5f, null);

            var selected = _service.TrySelectChoice(1);

            Assert.That(selected, Is.False);
            Assert.That(_service.CurrentNodeId, Is.EqualTo(RootNodeId));
        }

        [Test]
        public void SelectingChoice_AdvancesToNextNode()
        {
            _service.TryStart(NpcId, 0.5f, null);

            var selected = _service.TrySelectChoice(0);

            Assert.That(selected, Is.True);
            Assert.That(_service.CurrentNodeId, Is.EqualTo(SecondNodeId));
        }

        [Test]
        public void TerminalNode_HasNoChoices()
        {
            _service.TryStart(NpcId, 0.5f, null);
            _service.TrySelectChoice(0);

            Assert.That(_service.Choices, Is.Empty);
        }

        [Test]
        public void EmptyNextNode_EndsDialogue()
        {
            _service.TryStart(NpcId, trust: 0.9f, flags: null);

            DialogueEndedEvent ended = default;
            var endedCount = 0;
            _eventBus.Subscribe<DialogueEndedEvent>(e => { ended = e; endedCount++; });

            _service.TrySelectChoice(2); // nextNodeId пуст

            Assert.That(endedCount, Is.EqualTo(1));
            Assert.That(ended.NpcId, Is.EqualTo(NpcId));
            Assert.That(_service.IsActive, Is.False);
        }

        /// <summary>FR-034: доверие меняется только через явный эффект выбора.</summary>
        [Test]
        public void ChoiceEffect_IsPublished()
        {
            _service.TryStart(NpcId, 0.5f, null);

            DialogueEffectRequestedEvent captured = default;
            var count = 0;
            _eventBus.Subscribe<DialogueEffectRequestedEvent>(e => { captured = e; count++; });

            _service.TrySelectChoice(0);

            Assert.That(count, Is.EqualTo(1));
            Assert.That(captured.EffectType, Is.EqualTo("trust"));
            Assert.That(captured.TargetId, Is.EqualTo(NpcId));
            Assert.That(captured.Value, Is.EqualTo(0.1f).Within(0.001f));
        }

        [Test]
        public void ChoiceWithoutEffects_PublishesNothing()
        {
            _language.AddExperience(250f);
            _service.TryStart(NpcId, 0.5f, null);

            var count = 0;
            _eventBus.Subscribe<DialogueEffectRequestedEvent>(_ => count++);

            _service.TrySelectChoice(1); // без эффектов

            Assert.That(count, Is.Zero);
        }

        /// <summary>FR-046: узел выдаёт опыт языка.</summary>
        [Test]
        public void SelectingChoice_AwardsLanguageExperience()
        {
            _service.TryStart(NpcId, 0.5f, null);
            var experienceBefore = _language.Experience;

            _service.TrySelectChoice(0);

            Assert.That(_language.Experience, Is.GreaterThan(experienceBefore));
        }

        /// <summary>FR-042: слово реплики добавляется в личный словарь.</summary>
        [Test]
        public void AddWordToDictionary_StoresWord()
        {
            _service.TryStart(NpcId, 0.5f, null);

            var added = _service.TryAddWordToDictionary(WordId);

            Assert.That(added, Is.True);
            Assert.That(_language.TryGetWord(WordId, out _), Is.True);
        }

        [Test]
        public void AddUnknownWord_IsRejected()
        {
            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(_service.TryAddWordToDictionary("word_missing"), Is.False);
        }

        [Test]
        public void CurrentLineExposesWordIds()
        {
            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(_service.GetCurrentLine().WordIds, Contains.Item(WordId));
        }

        [Test]
        public void End_ClearsState()
        {
            _service.TryStart(NpcId, 0.5f, null);

            _service.End();

            Assert.That(_service.IsActive, Is.False);
            Assert.That(_service.CurrentNodeId, Is.Empty);
            Assert.That(_service.Choices, Is.Empty);
        }

        [Test]
        public void End_WhenInactive_PublishesNothing()
        {
            var count = 0;
            _eventBus.Subscribe<DialogueEndedEvent>(_ => count++);

            _service.End();

            Assert.That(count, Is.Zero);
        }

        [Test]
        public void SelectingChoice_WhenInactive_IsRejected()
        {
            Assert.That(_service.TrySelectChoice(0), Is.False);
        }

        [Test]
        public void OutOfRangeChoice_IsRejected()
        {
            _service.TryStart(NpcId, 0.5f, null);

            Assert.That(_service.TrySelectChoice(99), Is.False);
            Assert.That(_service.TrySelectChoice(-1), Is.False);
            Assert.That(_service.IsActive, Is.True);
        }
    }
}
