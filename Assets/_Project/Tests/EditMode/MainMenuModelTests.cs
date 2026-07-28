using System;
using System.IO;
using NUnit.Framework;
using QonaevLife.Core;
using QonaevLife.UI;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Главное меню и слоты сохранений (FR-001 — FR-004, AT-001).</summary>
    [TestFixture]
    public sealed class MainMenuModelTests
    {
        private string _directory;
        private JsonFileSaveService _saveService;
        private MainMenuModel _model;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "QonaevMenu", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);

            _saveService = new JsonFileSaveService(_directory);
            _model = new MainMenuModel(_saveService);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private SaveData CreateSave(string profile, int day)
        {
            var data = SaveData.CreateNew(profile, GameClockSettings.Default);
            data.world.day = day;
            return data;
        }

        [Test]
        public void EmptySlots_CannotContinue()
        {
            _model.Refresh();

            Assert.That(_model.Slots, Has.Count.EqualTo(_saveService.SlotCount));
            Assert.That(_model.CanContinue, Is.False);
            Assert.That(_model.MostRecentSlotIndex, Is.EqualTo(-1));
            Assert.That(_model.HasAnySave, Is.False);
        }

        [Test]
        public void SavedSlot_EnablesContinue()
        {
            _saveService.Save(1, CreateSave("Айдана", day: 4));

            _model.Refresh();

            Assert.That(_model.CanContinue, Is.True);
            Assert.That(_model.MostRecentSlotIndex, Is.EqualTo(1));
        }

        /// <summary>«Продолжить» ведёт в самый свежий слот, а не в первый.</summary>
        [Test]
        public void Continue_PicksMostRecentSave()
        {
            _saveService.Save(0, CreateSave("Старый", day: 1));
            System.Threading.Thread.Sleep(1100); // отметка времени в секундах
            _saveService.Save(2, CreateSave("Новый", day: 9));

            _model.Refresh();

            Assert.That(_model.MostRecentSlotIndex, Is.EqualTo(2));
        }

        /// <summary>FR-004: повреждённый слот не может быть целью «Продолжить».</summary>
        [Test]
        public void CorruptedSlot_IsNotEligibleForContinue()
        {
            File.WriteAllText(Path.Combine(_directory, "slot_0.json"), "мусор");

            _model.Refresh();

            Assert.That(_model.CanContinue, Is.False);
            Assert.That(_model.TryGetSlot(0, out var slot), Is.True);
            Assert.That(slot.Status, Is.EqualTo(SaveSlotStatus.Corrupted));
            Assert.That(slot.CanLoad, Is.False);
            Assert.That(slot.StatusKey, Is.EqualTo("slot.corrupted"));
        }

        [Test]
        public void CorruptedSlot_DoesNotHideValidOne()
        {
            File.WriteAllText(Path.Combine(_directory, "slot_0.json"), "мусор");
            _saveService.Save(1, CreateSave("Целый", day: 3));

            _model.Refresh();

            Assert.That(_model.MostRecentSlotIndex, Is.EqualTo(1));
        }

        [Test]
        public void SlotView_ExposesProfileAndDay()
        {
            _saveService.Save(0, CreateSave("Нурсултан", day: 7));

            _model.Refresh();
            _model.TryGetSlot(0, out var slot);

            Assert.That(slot.ProfileName, Is.EqualTo("Нурсултан"));
            Assert.That(slot.GameDay, Is.EqualTo(7));
            Assert.That(slot.SavedAtUtc, Is.Not.EqualTo(default(DateTime)));
            Assert.That(slot.StatusKey, Is.EqualTo("slot.valid"));
        }

        /// <summary>FR-002: новая игра занимает первый свободный слот.</summary>
        [Test]
        public void FindFirstEmptySlot_SkipsOccupied()
        {
            _saveService.Save(0, CreateSave("Занят", day: 1));

            _model.Refresh();

            Assert.That(_model.FindFirstEmptySlot(), Is.EqualTo(1));
        }

        [Test]
        public void FindFirstEmptySlot_ReturnsMinusOneWhenFull()
        {
            for (var i = 0; i < _saveService.SlotCount; i++)
                _saveService.Save(i, CreateSave($"Слот {i}", day: i + 1));

            _model.Refresh();

            Assert.That(_model.FindFirstEmptySlot(), Is.EqualTo(-1));
        }

        [Test]
        public void UnsupportedVersion_IsReportedInStatus()
        {
            var data = CreateSave("Из будущего", day: 2);
            data.SchemaVersion = SaveData.CurrentSchemaVersion + 3;
            File.WriteAllText(Path.Combine(_directory, "slot_0.json"),
                JsonUtility.ToJson(data, prettyPrint: true));

            _model.Refresh();
            _model.TryGetSlot(0, out var slot);

            Assert.That(slot.Status, Is.EqualTo(SaveSlotStatus.UnsupportedVersion));
            Assert.That(slot.StatusKey, Is.EqualTo("slot.unsupported"));
            Assert.That(_model.CanContinue, Is.False);
        }

        [Test]
        public void TryGetSlot_ReturnsFalseForUnknownIndex()
        {
            _model.Refresh();

            Assert.That(_model.TryGetSlot(99, out _), Is.False);
        }

        [Test]
        public void Refresh_PicksUpChanges()
        {
            _model.Refresh();
            Assert.That(_model.CanContinue, Is.False);

            _saveService.Save(0, CreateSave("Новая", day: 1));
            _model.Refresh();

            Assert.That(_model.CanContinue, Is.True);
        }
    }
}
