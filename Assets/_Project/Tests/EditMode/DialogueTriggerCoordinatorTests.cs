using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Dialogue;
using QonaevLife.Language;
using QonaevLife.World;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>
    /// Запуск диалога по расписанию NPC и применение эффектов (FR-031, FR-034).
    /// </summary>
    [TestFixture]
    public sealed class DialogueTriggerCoordinatorTests
    {
        private const string HubId = "loc_hub";
        private const string CafeId = "loc_cafe";
        private const string NpcId = "npc_dispatcher";
        private const string RootNodeId = "dlg_root";

        private readonly List<ScriptableObject> _created = new();

        private EventBus _eventBus;
        private GameClock _clock;
        private ContentDatabase _content;
        private DialogueService _dialogue;
        private DialogueTriggerCoordinator _coordinator;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _clock = new GameClock(GameClockSettings.Default); // 08:00, Morning
            _content = BuildContent();

            var language = new LanguageProgressService(
                _eventBus, LanguageProgressSettings.Default);

            _dialogue = new DialogueService(_content, _eventBus, language);
            _coordinator = new DialogueTriggerCoordinator(
                _eventBus, _dialogue, _content, _clock);
            _coordinator.Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            _coordinator.Shutdown();

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
            var hub = CreateLocation(HubId);
            var cafe = CreateLocation(CafeId);

            var node = Create<DialogueNodeDefinition>();
            var nodeObject = new SerializedObject(node);
            nodeObject.FindProperty("id").stringValue = RootNodeId;
            nodeObject.FindProperty("speakerNpcId").stringValue = NpcId;
            var line = nodeObject.FindProperty("line");
            line.FindPropertyRelative("russian").stringValue = "Привет";
            line.FindPropertyRelative("kazakh").stringValue = "Сәлем";

            var choices = nodeObject.FindProperty("choices");
            choices.arraySize = 1;
            var choice = choices.GetArrayElementAtIndex(0);
            choice.FindPropertyRelative("choiceId").stringValue = "choice_trust";
            choice.FindPropertyRelative("nextNodeId").stringValue = string.Empty;
            var choiceLine = choice.FindPropertyRelative("line");
            choiceLine.FindPropertyRelative("russian").stringValue = "Помогу";
            choiceLine.FindPropertyRelative("kazakh").stringValue = "Көмектесемін";

            var effects = choice.FindPropertyRelative("effects");
            effects.arraySize = 1;
            var effect = effects.GetArrayElementAtIndex(0);
            effect.FindPropertyRelative("effectType").stringValue = "trust";
            effect.FindPropertyRelative("targetId").stringValue = NpcId;
            effect.FindPropertyRelative("value").floatValue = 0.2f;
            nodeObject.ApplyModifiedPropertiesWithoutUndo();

            // Диспетчер: утром и днём в хабе, вечером в кафе, ночью дома.
            var npc = Create<NpcDefinition>();
            var npcObject = new SerializedObject(npc);
            npcObject.FindProperty("id").stringValue = NpcId;
            npcObject.FindProperty("displayNameKey").stringValue = "npc.dispatcher";
            npcObject.FindProperty("homeLocationId").stringValue = HubId;
            npcObject.FindProperty("rootDialogueId").stringValue = RootNodeId;
            npcObject.FindProperty("initialTrust").floatValue = 0.5f;

            var schedule = npcObject.FindProperty("schedule");
            var entries = new[]
            {
                ("e_morning", "Morning", HubId),
                ("e_day", "Day", HubId),
                ("e_evening", "Evening", CafeId),
                ("e_night", "Night", HubId)
            };
            schedule.arraySize = entries.Length;
            for (var i = 0; i < entries.Length; i++)
            {
                var entry = schedule.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("entryId").stringValue = entries[i].Item1;
                entry.FindPropertyRelative("dayPhase").stringValue = entries[i].Item2;
                entry.FindPropertyRelative("locationId").stringValue = entries[i].Item3;
                entry.FindPropertyRelative("behaviour").stringValue = "work";
            }
            npcObject.ApplyModifiedPropertiesWithoutUndo();

            var database = Create<ContentDatabase>();
            SetList(database, "locations", new ScriptableObject[] { hub, cafe });
            SetList(database, "npcs", new ScriptableObject[] { npc });
            SetList(database, "dialogueNodes", new ScriptableObject[] { node });

            return database;
        }

        private LocationDefinition CreateLocation(string id)
        {
            var location = Create<LocationDefinition>();
            var so = new SerializedObject(location);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayNameKey").stringValue = $"loc.{id}";
            so.FindProperty("sectorId").stringValue = "sector_center";
            so.FindProperty("alwaysOpen").boolValue = true;
            so.FindProperty("discoveredFromStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return location;
        }

        private static void SetList(ScriptableObject target, string field,
            IReadOnlyList<ScriptableObject> values)
        {
            var so = new SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Count;
            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>Утром диспетчер в хабе — диалог начинается.</summary>
        [Test]
        public void InteractingWhereNpcScheduled_StartsDialogue()
        {
            _eventBus.Publish(new LocationInteractedEvent(HubId, Player.InteractionKind.Npc));

            Assert.That(_dialogue.IsActive, Is.True);
            Assert.That(_dialogue.CurrentNpcId, Is.EqualTo(NpcId));
        }

        /// <summary>Утром в кафе никого нет — диалога не будет.</summary>
        [Test]
        public void InteractingWhereNpcAbsent_DoesNotStartDialogue()
        {
            _eventBus.Publish(new LocationInteractedEvent(CafeId, Player.InteractionKind.Npc));

            Assert.That(_dialogue.IsActive, Is.False);
        }

        /// <summary>Вечером тот же NPC уже в кафе — расписание работает (AT-004).</summary>
        [Test]
        public void ScheduleFollowsDayPhase()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 20 * 60); // Evening

            _eventBus.Publish(new LocationInteractedEvent(CafeId, Player.InteractionKind.Npc));

            Assert.That(_dialogue.IsActive, Is.True);
            Assert.That(_dialogue.CurrentNpcId, Is.EqualTo(NpcId));
        }

        [Test]
        public void EveningInterationAtHub_FindsNobody()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 20 * 60);

            _eventBus.Publish(new LocationInteractedEvent(HubId, Player.InteractionKind.Npc));

            Assert.That(_dialogue.IsActive, Is.False);
        }

        [Test]
        public void SecondInteraction_WhileDialogueActive_IsIgnored()
        {
            _eventBus.Publish(new LocationInteractedEvent(HubId, Player.InteractionKind.Npc));
            var nodeBefore = _dialogue.CurrentNodeId;

            _eventBus.Publish(new LocationInteractedEvent(HubId, Player.InteractionKind.Npc));

            Assert.That(_dialogue.CurrentNodeId, Is.EqualTo(nodeBefore));
        }

        /// <summary>FR-034: доверие меняется только по явному эффекту выбора.</summary>
        [Test]
        public void ChoiceEffect_IncreasesTrust()
        {
            Assert.That(_coordinator.GetTrust(NpcId), Is.EqualTo(0.5f).Within(0.001f));

            _eventBus.Publish(new LocationInteractedEvent(HubId, Player.InteractionKind.Npc));
            _dialogue.TrySelectChoice(0);

            Assert.That(_coordinator.GetTrust(NpcId), Is.EqualTo(0.7f).Within(0.001f));
        }

        [Test]
        public void Trust_IsClampedToOne()
        {
            for (var i = 0; i < 10; i++)
            {
                _eventBus.Publish(new LocationInteractedEvent(HubId, Player.InteractionKind.Npc));
                _dialogue.TrySelectChoice(0);
            }

            Assert.That(_coordinator.GetTrust(NpcId), Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void CaptureAndRestore_PreservesTrust()
        {
            _eventBus.Publish(new LocationInteractedEvent(HubId, Player.InteractionKind.Npc));
            _dialogue.TrySelectChoice(0);

            var saved = new List<NpcSaveData>();
            _coordinator.CaptureState(saved);

            Assert.That(saved, Has.Count.EqualTo(1));
            Assert.That(saved[0].npcId, Is.EqualTo(NpcId));

            var restored = new DialogueTriggerCoordinator(
                _eventBus, _dialogue, _content, _clock);
            restored.Initialize();
            restored.RestoreState(saved);

            Assert.That(restored.GetTrust(NpcId), Is.EqualTo(0.7f).Within(0.001f));

            restored.Shutdown();
        }

        /// <summary>Исчезнувший из контента NPC не ломает загрузку сохранения.</summary>
        [Test]
        public void RestoreState_IgnoresUnknownNpc()
        {
            var saved = new List<NpcSaveData>
            {
                new() { npcId = "npc_removed_in_patch", trust = 0.9f }
            };

            Assert.DoesNotThrow(() => _coordinator.RestoreState(saved));
            Assert.That(_coordinator.GetTrust("npc_removed_in_patch"), Is.Zero);
        }

        [Test]
        public void UnknownLocation_IsIgnored()
        {
            Assert.DoesNotThrow(() => _eventBus.Publish(
                new LocationInteractedEvent("loc_missing", Player.InteractionKind.Npc)));

            Assert.That(_dialogue.IsActive, Is.False);
        }
    }
}
