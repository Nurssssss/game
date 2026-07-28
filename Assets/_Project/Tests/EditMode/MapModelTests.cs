using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.UI;
using QonaevLife.World;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Карта: точки, цель и маршрут (FR-092).</summary>
    [TestFixture]
    public sealed class MapModelTests
    {
        private const string HomeId = "loc_home";
        private const string CafeId = "loc_cafe";
        private const string HiddenId = "loc_hidden";
        private const string NightShopId = "loc_night";

        private readonly List<ScriptableObject> _created = new();

        private EventBus _eventBus;
        private GameClock _clock;
        private LocationRegistry _locations;
        private MapModel _map;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _clock = new GameClock(GameClockSettings.Default); // 08:00

            var content = BuildContent();
            _locations = new LocationRegistry(content, _eventBus, _clock);
            _locations.Initialize();

            _map = new MapModel(_locations, worldExtent: 50f);
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
            var home = CreateLocation(HomeId, new Vector3(-25f, 0f, -25f), true, true);
            var cafe = CreateLocation(CafeId, new Vector3(25f, 0f, 25f), true, true);
            var hidden = CreateLocation(HiddenId, new Vector3(0f, 0f, 40f), false, true);
            var night = CreateLocation(NightShopId, new Vector3(10f, 0f, 0f), true, false);

            var database = Create<ContentDatabase>();
            var so = new SerializedObject(database);
            var list = so.FindProperty("locations");
            var all = new ScriptableObject[] { home, cafe, hidden, night };
            list.arraySize = all.Length;
            for (var i = 0; i < all.Length; i++)
                list.GetArrayElementAtIndex(i).objectReferenceValue = all[i];
            so.ApplyModifiedPropertiesWithoutUndo();

            return database;
        }

        private LocationDefinition CreateLocation(string id, Vector3 position, bool discovered,
            bool alwaysOpen)
        {
            var location = Create<LocationDefinition>();
            var so = new SerializedObject(location);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayNameKey").stringValue = $"loc.{id}";
            so.FindProperty("sectorId").stringValue = "sector_center";
            so.FindProperty("markerPosition").vector3Value = position;
            so.FindProperty("discoveredFromStart").boolValue = discovered;
            so.FindProperty("alwaysOpen").boolValue = alwaysOpen;
            so.FindProperty("openHour").intValue = 22;
            so.FindProperty("closeHour").intValue = 4;
            so.ApplyModifiedPropertiesWithoutUndo();
            return location;
        }

        [Test]
        public void Refresh_AlwaysIncludesPlayerMarker()
        {
            _map.Refresh(Vector3.zero, objectiveLocationId: null);

            Assert.That(_map.Markers, Is.Not.Empty);
            Assert.That(_map.Markers[0].Kind, Is.EqualTo(MapMarkerKind.Player));
        }

        [Test]
        public void PlayerAtOrigin_IsAtCenterOfMap()
        {
            _map.Refresh(Vector3.zero, null);

            Assert.That(_map.PlayerPosition.x, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(_map.PlayerPosition.y, Is.EqualTo(0.5f).Within(0.001f));
        }

        [Test]
        public void WorldCoordinates_MapToNormalizedRange()
        {
            // Область карты ±50 м: точка (25, 25) — три четверти по обеим осям.
            var normalized = _map.ToNormalized(new Vector3(25f, 0f, 25f));

            Assert.That(normalized.x, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(normalized.y, Is.EqualTo(0.75f).Within(0.001f));
        }

        /// <summary>Точка за границей области зажимается к краю, а не исчезает.</summary>
        [Test]
        public void PositionOutsideExtent_IsClampedToEdge()
        {
            var normalized = _map.ToNormalized(new Vector3(500f, 0f, -500f));

            Assert.That(normalized.x, Is.EqualTo(1f));
            Assert.That(normalized.y, Is.EqualTo(0f));
        }

        [Test]
        public void UndiscoveredLocation_IsNotOnMap()
        {
            _map.Refresh(Vector3.zero, null);

            Assert.That(FindMarker(HiddenId), Is.Null, "Неоткрытая точка не показывается.");
            Assert.That(FindMarker(HomeId), Is.Not.Null);
        }

        [Test]
        public void DiscoveringLocation_AddsItToMap()
        {
            _locations.Discover(HiddenId);
            _map.Refresh(Vector3.zero, null);

            Assert.That(FindMarker(HiddenId), Is.Not.Null);
        }

        /// <summary>FR-092: маркер цели совпадает с позицией объекта.</summary>
        [Test]
        public void ObjectiveMarker_MatchesLocationPosition()
        {
            _map.Refresh(Vector3.zero, CafeId);

            var marker = FindMarker(CafeId);

            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Value.Kind, Is.EqualTo(MapMarkerKind.Objective));
            Assert.That(marker.Value.NormalizedPosition.x, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(_map.ObjectivePosition, Is.Not.Null);
        }

        [Test]
        public void WithoutObjective_NoObjectiveMarker()
        {
            _map.Refresh(Vector3.zero, null);

            Assert.That(_map.ObjectivePosition, Is.Null);

            foreach (var marker in _map.Markers)
                Assert.That(marker.Kind, Is.Not.EqualTo(MapMarkerKind.Objective));
        }

        /// <summary>Маршрут строится от игрока к цели.</summary>
        [Test]
        public void Route_ConnectsPlayerToObjective()
        {
            _map.Refresh(new Vector3(-25f, 0f, -25f), CafeId);

            Assert.That(_map.TryGetRoute(out var from, out var to), Is.True);
            Assert.That(from, Is.EqualTo(_map.PlayerPosition));
            Assert.That(to.x, Is.EqualTo(0.75f).Within(0.001f));
        }

        [Test]
        public void Route_IsAbsentWithoutObjective()
        {
            _map.Refresh(Vector3.zero, null);

            Assert.That(_map.TryGetRoute(out _, out _), Is.False);
        }

        /// <summary>Закрытая локация видна, но помечена как закрытая.</summary>
        [Test]
        public void ClosedLocation_IsMarkedClosed()
        {
            _map.Refresh(Vector3.zero, null); // 08:00, магазин работает 22–4

            var marker = FindMarker(NightShopId);

            Assert.That(marker, Is.Not.Null);
            Assert.That(marker.Value.IsOpen, Is.False);
        }

        [Test]
        public void OpenLocation_IsMarkedOpen()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 23 * 60);
            _map.Refresh(Vector3.zero, null);

            Assert.That(FindMarker(NightShopId)!.Value.IsOpen, Is.True);
        }

        [Test]
        public void Refresh_ReplacesPreviousMarkers()
        {
            _map.Refresh(Vector3.zero, null);
            var first = _map.Markers.Count;

            _map.Refresh(Vector3.zero, null);

            Assert.That(_map.Markers, Has.Count.EqualTo(first),
                "Повторный вызов не должен накапливать метки.");
        }

        [Test]
        public void InvalidExtent_IsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new MapModel(_locations, worldExtent: 0f));
        }

        private MapMarker? FindMarker(string id)
        {
            foreach (var marker in _map.Markers)
            {
                if (marker.Id == id)
                    return marker;
            }

            return null;
        }
    }
}
