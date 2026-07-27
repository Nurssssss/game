using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.World;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Точки интереса района (FR-073, FR-081, FR-092).</summary>
    [TestFixture]
    public sealed class LocationRegistryTests
    {
        private const string StartLocationId = "loc_apartment_01";
        private const string HiddenLocationId = "loc_landmark_01";
        private const string NightShopId = "loc_shop_night";

        private readonly List<ScriptableObject> _created = new();

        private EventBus _eventBus;
        private GameClock _clock;
        private ContentDatabase _content;
        private LocationRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _clock = new GameClock(GameClockSettings.Default); // 8:00
            _content = BuildContent();
            _registry = new LocationRegistry(_content, _eventBus, _clock);
            _registry.Initialize();
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
            var apartment = CreateLocation(StartLocationId, discovered: true, alwaysOpen: true);
            var landmark = CreateLocation(HiddenLocationId, discovered: false, alwaysOpen: true);

            // Магазин работает с 22:00 до 4:00 — интервал через полночь.
            var nightShop = CreateLocation(NightShopId, discovered: true, alwaysOpen: false,
                openHour: 22, closeHour: 4);

            var database = Create<ContentDatabase>();
            SetObjectList(database, "locations",
                new ScriptableObject[] { apartment, landmark, nightShop });

            return database;
        }

        private LocationDefinition CreateLocation(string id, bool discovered, bool alwaysOpen,
            int openHour = 0, int closeHour = 23, bool taxiDestination = true)
        {
            var location = Create<LocationDefinition>();
            var so = new SerializedObject(location);

            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayNameKey").stringValue = $"loc.{id}";
            so.FindProperty("sectorId").stringValue = "sector_center";
            so.FindProperty("discoveredFromStart").boolValue = discovered;
            so.FindProperty("alwaysOpen").boolValue = alwaysOpen;
            so.FindProperty("openHour").intValue = openHour;
            so.FindProperty("closeHour").intValue = closeHour;
            so.FindProperty("isTaxiDestination").boolValue = taxiDestination;
            so.ApplyModifiedPropertiesWithoutUndo();

            return location;
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
        public void Initialize_DiscoversStartLocations()
        {
            Assert.That(_registry.IsDiscovered(StartLocationId), Is.True);
            Assert.That(_registry.IsDiscovered(HiddenLocationId), Is.False);
        }

        [Test]
        public void Discover_OpensLocationAndPublishesEventOnce()
        {
            LocationDiscoveredEvent captured = default;
            var count = 0;
            _eventBus.Subscribe<LocationDiscoveredEvent>(e => { captured = e; count++; });

            Assert.That(_registry.Discover(HiddenLocationId), Is.True);
            Assert.That(_registry.Discover(HiddenLocationId), Is.False, "Повторное открытие.");

            Assert.That(count, Is.EqualTo(1));
            Assert.That(captured.LocationId, Is.EqualTo(HiddenLocationId));
            Assert.That(_registry.IsDiscovered(HiddenLocationId), Is.True);
        }

        [Test]
        public void DiscoverUnknownLocation_IsRejected()
        {
            Assert.That(_registry.Discover("loc_missing"), Is.False);
            Assert.That(_registry.Discover(null), Is.False);
        }

        [Test]
        public void AlwaysOpenLocation_IsOpenAtAnyHour()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 3 * 60);

            Assert.That(_registry.IsOpenNow(StartLocationId), Is.True);
        }

        /// <summary>Интервал работы через полночь считается верно.</summary>
        [TestCase(23, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, false)]
        [TestCase(12, false)]
        [TestCase(21, false)]
        public void OvernightHours_AreEvaluatedCorrectly(int hour, bool expectedOpen)
        {
            _clock.RestoreState(day: 1, minutesOfDay: hour * 60);

            Assert.That(_registry.IsOpenNow(NightShopId), Is.EqualTo(expectedOpen));
        }

        /// <summary>FR-073: цель задания должна быть открыта и работать.</summary>
        [Test]
        public void ClosedLocation_IsNotValidObjective()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 12 * 60); // магазин закрыт

            Assert.That(_registry.IsValidObjectiveTarget(NightShopId), Is.False);

            _clock.RestoreState(day: 1, minutesOfDay: 23 * 60); // магазин открыт

            Assert.That(_registry.IsValidObjectiveTarget(NightShopId), Is.True);
        }

        [Test]
        public void UndiscoveredLocation_IsNotValidObjective()
        {
            Assert.That(_registry.IsValidObjectiveTarget(HiddenLocationId), Is.False);

            _registry.Discover(HiddenLocationId);

            Assert.That(_registry.IsValidObjectiveTarget(HiddenLocationId), Is.True);
        }

        /// <summary>FR-081: такси предлагает только открытые точки.</summary>
        [Test]
        public void TaxiDestinations_IncludeOnlyDiscoveredLocations()
        {
            var buffer = new List<LocationDefinition>();

            _registry.CollectTaxiDestinations(buffer);
            var idsBefore = buffer.ConvertAll(l => l.Id);

            Assert.That(idsBefore, Contains.Item(StartLocationId));
            Assert.That(idsBefore, Does.Not.Contain(HiddenLocationId));

            _registry.Discover(HiddenLocationId);
            _registry.CollectTaxiDestinations(buffer);

            Assert.That(buffer.ConvertAll(l => l.Id), Contains.Item(HiddenLocationId));
        }

        [Test]
        public void CaptureAndRestore_PreservesDiscoveries()
        {
            _registry.Discover(HiddenLocationId);

            var data = new WorldSaveData();
            _registry.CaptureState(data);

            var restored = new LocationRegistry(_content, _eventBus, _clock);
            restored.Initialize();
            restored.RestoreState(data);

            Assert.That(restored.IsDiscovered(HiddenLocationId), Is.True);
            Assert.That(restored.IsDiscovered(StartLocationId), Is.True);
        }

        /// <summary>Исчезнувшая из контента локация не делает сохранение битым.</summary>
        [Test]
        public void RestoreState_IgnoresUnknownLocationIds()
        {
            var data = new WorldSaveData();
            data.discoveredLocationIds.Add("loc_removed_in_patch");

            Assert.DoesNotThrow(() => _registry.RestoreState(data));
            Assert.That(_registry.IsDiscovered("loc_removed_in_patch"), Is.False);
            Assert.That(_registry.IsDiscovered(StartLocationId), Is.True,
                "Стартовые локации остаются открытыми.");
        }

        [Test]
        public void TryGet_ReturnsDefinition()
        {
            Assert.That(_registry.TryGet(StartLocationId, out var definition), Is.True);
            Assert.That(definition.Id, Is.EqualTo(StartLocationId));
            Assert.That(_registry.TryGet("loc_missing", out _), Is.False);
        }
    }
}
