using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Npc;
using QonaevLife.World;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>NPC, расписания и симуляция удалённых (FR-030 — FR-032, AT-004).</summary>
    [TestFixture]
    public sealed class NpcServiceTests
    {
        private const string HomeId = "loc_home";
        private const string WorkId = "loc_work";
        private const string CafeId = "loc_cafe";
        private const string DispatcherId = "npc_dispatcher";
        private const string BaristaId = "npc_barista";

        private readonly List<ScriptableObject> _created = new();

        private EventBus _eventBus;
        private GameClock _clock;
        private ContentDatabase _content;
        private LocationRegistry _locations;
        private NpcService _service;

        private static NpcSimulationSettings Settings => new()
        {
            activeRadius = 20f,
            deactivationMargin = 5f,
            maxActiveNpcs = 10
        };

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _clock = new GameClock(GameClockSettings.Default); // 08:00, Morning
            _content = BuildContent();

            _locations = new LocationRegistry(_content, _eventBus, _clock);
            _locations.Initialize();

            _service = new NpcService(_content, _eventBus, _clock, _locations, Settings);
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

        private ContentDatabase BuildContent()
        {
            var home = CreateLocation(HomeId, new Vector3(0f, 0f, 0f));
            var work = CreateLocation(WorkId, new Vector3(10f, 0f, 0f));
            var cafe = CreateLocation(CafeId, new Vector3(100f, 0f, 0f));

            // Диспетчер: утром и днём на работе, вечером в кафе, ночью дома.
            var dispatcher = CreateNpc(DispatcherId, new[]
            {
                ("e1", "Morning", WorkId, 0),
                ("e2", "Day", WorkId, 0),
                ("e3", "Evening", CafeId, 0),
                ("e4", "Night", HomeId, 0)
            });

            // Бариста всегда в кафе — проверяет, что далёкий NPC не активен.
            var barista = CreateNpc(BaristaId, new[]
            {
                ("b1", "Morning", CafeId, 0),
                ("b2", "Day", CafeId, 0),
                ("b3", "Evening", CafeId, 0),
                ("b4", "Night", CafeId, 0)
            });

            var database = Create<ContentDatabase>();
            SetList(database, "locations", new ScriptableObject[] { home, work, cafe });
            SetList(database, "npcs", new ScriptableObject[] { dispatcher, barista });

            return database;
        }

        private LocationDefinition CreateLocation(string id, Vector3 position)
        {
            var location = Create<LocationDefinition>();
            var so = new SerializedObject(location);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayNameKey").stringValue = $"loc.{id}";
            so.FindProperty("sectorId").stringValue = "sector_center";
            so.FindProperty("markerPosition").vector3Value = position;
            so.FindProperty("alwaysOpen").boolValue = true;
            so.FindProperty("discoveredFromStart").boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
            return location;
        }

        private NpcDefinition CreateNpc(string id,
            IReadOnlyList<(string Entry, string Phase, string Location, int Priority)> schedule)
        {
            var npc = Create<NpcDefinition>();
            var so = new SerializedObject(npc);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayNameKey").stringValue = $"npc.{id}";
            so.FindProperty("homeLocationId").stringValue = HomeId;
            so.FindProperty("rootDialogueId").stringValue = "dlg_root";
            so.FindProperty("initialTrust").floatValue = 0.5f;

            var array = so.FindProperty("schedule");
            array.arraySize = schedule.Count;

            for (var i = 0; i < schedule.Count; i++)
            {
                var element = array.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("entryId").stringValue = schedule[i].Entry;
                element.FindPropertyRelative("dayPhase").stringValue = schedule[i].Phase;
                element.FindPropertyRelative("locationId").stringValue = schedule[i].Location;
                element.FindPropertyRelative("behaviour").stringValue = "idle";
                element.FindPropertyRelative("priority").intValue = schedule[i].Priority;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            return npc;
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

        [Test]
        public void Initialize_PlacesNpcsByCurrentPhase()
        {
            Assert.That(_service.TryGetState(DispatcherId, out var dispatcher), Is.True);
            Assert.That(dispatcher.CurrentLocationId, Is.EqualTo(WorkId),
                "Утром диспетчер на работе.");
            Assert.That(dispatcher.ScheduleEntryId, Is.EqualTo("e1"));

            Assert.That(_service.TryGetState(BaristaId, out var barista), Is.True);
            Assert.That(barista.CurrentLocationId, Is.EqualTo(CafeId));
        }

        [Test]
        public void GetNpcsAt_ReturnsOccupants()
        {
            Assert.That(_service.GetNpcsAt(WorkId), Contains.Item(DispatcherId));
            Assert.That(_service.GetNpcsAt(CafeId), Contains.Item(BaristaId));
            Assert.That(_service.GetNpcsAt(HomeId), Is.Empty);
        }

        /// <summary>AT-004: при смене фазы NPC переходит на другую точку.</summary>
        [Test]
        public void PhaseChange_MovesNpcToScheduledLocation()
        {
            NpcScheduleChangedEvent captured = default;
            var count = 0;
            _eventBus.Subscribe<NpcScheduleChangedEvent>(e => { captured = e; count++; });

            _clock.RestoreState(day: 1, minutesOfDay: 20 * 60); // Evening
            _service.Update(Vector3.zero);

            Assert.That(count, Is.GreaterThanOrEqualTo(1));
            Assert.That(captured.NpcId, Is.EqualTo(DispatcherId));
            Assert.That(captured.PreviousLocationId, Is.EqualTo(WorkId));
            Assert.That(captured.CurrentLocationId, Is.EqualTo(CafeId));

            _service.TryGetState(DispatcherId, out var state);
            Assert.That(state.CurrentLocationId, Is.EqualTo(CafeId));
        }

        [Test]
        public void PhaseChange_UpdatesLocationIndex()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 20 * 60);
            _service.Update(Vector3.zero);

            Assert.That(_service.GetNpcsAt(WorkId), Is.Empty);
            Assert.That(_service.GetNpcsAt(CafeId), Contains.Item(DispatcherId));
        }

        [Test]
        public void NightPhase_SendsNpcHome()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 23 * 60 + 30);
            _service.Update(Vector3.zero);

            _service.TryGetState(DispatcherId, out var state);
            Assert.That(state.CurrentLocationId, Is.EqualTo(HomeId));
        }

        [Test]
        public void SamePhase_DoesNotRepublishEvents()
        {
            var count = 0;
            _eventBus.Subscribe<NpcScheduleChangedEvent>(_ => count++);

            _service.Update(Vector3.zero);
            _service.Update(Vector3.zero);

            Assert.That(count, Is.Zero, "Без смены фазы расписание не пересчитывается.");
        }

        /// <summary>FR-032: рядом с игроком NPC симулируется полностью.</summary>
        [Test]
        public void NearbyNpc_BecomesActive()
        {
            // Работа в 10 м от начала координат, радиус 20 м.
            _service.Update(Vector3.zero);

            _service.TryGetState(DispatcherId, out var dispatcher);
            Assert.That(dispatcher.Level, Is.EqualTo(NpcSimulationLevel.Active));

            // Кафе в 100 м — далеко.
            _service.TryGetState(BaristaId, out var barista);
            Assert.That(barista.Level, Is.EqualTo(NpcSimulationLevel.Distant));
        }

        [Test]
        public void SimulationLevelChange_PublishesEvent()
        {
            var events = new List<NpcSimulationLevelChangedEvent>();
            _eventBus.Subscribe<NpcSimulationLevelChangedEvent>(events.Add);

            _service.Update(Vector3.zero);

            Assert.That(events, Is.Not.Empty);
            Assert.That(events.Exists(e => e.NpcId == DispatcherId
                                           && e.Level == NpcSimulationLevel.Active), Is.True);
        }

        [Test]
        public void WalkingAway_DeactivatesNpc()
        {
            _service.Update(Vector3.zero);
            _service.TryGetState(DispatcherId, out var near);
            Assert.That(near.Level, Is.EqualTo(NpcSimulationLevel.Active));

            _service.Update(new Vector3(200f, 0f, 0f));

            _service.TryGetState(DispatcherId, out var far);
            Assert.That(far.Level, Is.EqualTo(NpcSimulationLevel.Distant));
        }

        /// <summary>
        /// Гистерезис: на границе радиуса NPC не должен мерцать между
        /// состояниями от кадра к кадру.
        /// </summary>
        [Test]
        public void Hysteresis_PreventsFlickerAtBoundary()
        {
            // Активируем: работа в 10 м, игрок в начале координат.
            _service.Update(Vector3.zero);
            _service.TryGetState(DispatcherId, out var active);
            Assert.That(active.Level, Is.EqualTo(NpcSimulationLevel.Active));

            // Отходим так, что расстояние 22 м: больше радиуса 20,
            // но меньше радиуса с запасом 25 — NPC остаётся активным.
            _service.Update(new Vector3(-12f, 0f, 0f));

            _service.TryGetState(DispatcherId, out var stillActive);
            Assert.That(stillActive.Level, Is.EqualTo(NpcSimulationLevel.Active),
                "В зоне гистерезиса состояние не меняется.");
        }

        /// <summary>FR-032: число активных NPC ограничено бюджетом.</summary>
        [Test]
        public void ActiveCount_RespectsBudget()
        {
            var tightSettings = Settings;
            tightSettings.maxActiveNpcs = 1;
            tightSettings.activeRadius = 500f; // все в радиусе

            var service = new NpcService(
                _content, _eventBus, _clock, _locations, tightSettings);
            service.Initialize();

            service.Update(Vector3.zero);

            Assert.That(service.ActiveCount, Is.EqualTo(1));

            service.Shutdown();
        }

        [Test]
        public void ClosestNpc_GetsPriorityWithinBudget()
        {
            var tightSettings = Settings;
            tightSettings.maxActiveNpcs = 1;
            tightSettings.activeRadius = 500f;

            var service = new NpcService(
                _content, _eventBus, _clock, _locations, tightSettings);
            service.Initialize();

            // Игрок у начала координат: диспетчер в 10 м, бариста в 100 м.
            service.Update(Vector3.zero);

            service.TryGetState(DispatcherId, out var dispatcher);
            service.TryGetState(BaristaId, out var barista);

            Assert.That(dispatcher.Level, Is.EqualTo(NpcSimulationLevel.Active));
            Assert.That(barista.Level, Is.EqualTo(NpcSimulationLevel.Distant));

            service.Shutdown();
        }

        [Test]
        public void InvalidSettings_AreRejected()
        {
            var invalid = Settings;
            invalid.activeRadius = 0f;

            Assert.Throws<System.ArgumentException>(
                () => new NpcService(_content, _eventBus, _clock, _locations, invalid));
        }

        [Test]
        public void UnknownNpc_IsNotFound()
        {
            Assert.That(_service.TryGetState("npc_missing", out _), Is.False);
            Assert.That(_service.TryGetState(null, out _), Is.False);
        }

        [Test]
        public void UnknownLocation_HasNoOccupants()
        {
            Assert.That(_service.GetNpcsAt("loc_missing"), Is.Empty);
            Assert.That(_service.GetNpcsAt(null), Is.Empty);
        }

        /// <summary>П. 7 ТЗ: место и этап расписания сохраняются.</summary>
        [Test]
        public void CaptureAndRestore_PreservesScheduleState()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 20 * 60);
            _service.Update(Vector3.zero);

            // Записи создаёт владелец доверия; сервис дописывает место.
            var saved = new List<NpcSaveData>
            {
                new() { npcId = DispatcherId, trust = 0.5f },
                new() { npcId = BaristaId, trust = 0.5f }
            };

            _service.CaptureState(saved);

            Assert.That(saved[0].currentLocationId, Is.EqualTo(CafeId));
            Assert.That(saved[0].scheduleEntryId, Is.EqualTo("e3"));

            var restored = new NpcService(
                _content, _eventBus, _clock, _locations, Settings);
            restored.Initialize();
            restored.RestoreState(saved);

            restored.TryGetState(DispatcherId, out var state);
            Assert.That(state.CurrentLocationId, Is.EqualTo(CafeId));
            Assert.That(restored.GetNpcsAt(CafeId), Contains.Item(DispatcherId));

            restored.Shutdown();
        }

        [Test]
        public void RestoreState_IgnoresUnknownNpc()
        {
            var saved = new List<NpcSaveData>
            {
                new() { npcId = "npc_removed", currentLocationId = CafeId }
            };

            Assert.DoesNotThrow(() => _service.RestoreState(saved));
            Assert.That(_service.TryGetState("npc_removed", out _), Is.False);
        }

        [Test]
        public void UpdateBeforeInitialize_IsSafe()
        {
            var service = new NpcService(_content, _eventBus, _clock, _locations, Settings);

            Assert.DoesNotThrow(() => service.Update(Vector3.zero));
        }
    }
}
