using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using QonaevLife.Core;
using UnityEngine;
using UnityEngine.TestTools;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Сохранение, загрузка и обработка повреждённых файлов (FR-003 — FR-005).</summary>
    [TestFixture]
    public sealed class SaveServiceTests
    {
        private string _directory;
        private JsonFileSaveService _service;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "QonaevLifeTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);
            _service = new JsonFileSaveService(_directory);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private static SaveData CreateSample(string profile = "Айдана", int day = 3)
        {
            var data = SaveData.CreateNew(profile, GameClockSettings.Default);
            data.world.day = day;
            data.world.minutesOfDay = 9 * 60;
            data.world.weatherId = "Rain";
            data.economy.balance = 4200;
            data.language.level = 2;
            data.language.learnedWords.Add(new LearnedWordData
            {
                wordId = "word_salem",
                masteryStage = 2,
                correctAnswers = 3
            });
            return data;
        }

        /// <summary>FR-002: не менее трёх слотов.</summary>
        [Test]
        public void SlotCount_IsAtLeastThree()
        {
            Assert.That(_service.SlotCount, Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public void FewerThanThreeSlots_IsRejected()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(
                () => new JsonFileSaveService(_directory, slotCount: 2));
        }

        [Test]
        public void EmptySlot_ReportsEmpty()
        {
            var info = _service.GetSlotInfo(0);

            Assert.That(info.Status, Is.EqualTo(SaveSlotStatus.Empty));
            Assert.That(info.CanLoad, Is.False);
        }

        [Test]
        public void SaveThenLoad_RoundTripsData()
        {
            Assert.That(_service.Save(0, CreateSample()), Is.True);

            var result = _service.Load(0);

            Assert.That(result.Success, Is.True);
            Assert.That(result.Data.ProfileName, Is.EqualTo("Айдана"));
            Assert.That(result.Data.world.day, Is.EqualTo(3));
            Assert.That(result.Data.world.weatherId, Is.EqualTo("Rain"));
            Assert.That(result.Data.economy.balance, Is.EqualTo(4200));
            Assert.That(result.Data.language.learnedWords, Has.Count.EqualTo(1));
            Assert.That(result.Data.language.learnedWords[0].wordId, Is.EqualTo("word_salem"));
        }

        [Test]
        public void Save_StampsSchemaVersionAndTimestamp()
        {
            _service.Save(1, CreateSample());

            var info = _service.GetSlotInfo(1);

            Assert.That(info.Status, Is.EqualTo(SaveSlotStatus.Valid));
            Assert.That(info.SchemaVersion, Is.EqualTo(SaveData.CurrentSchemaVersion));
            Assert.That(info.SavedAtUtc, Is.Not.EqualTo(default(System.DateTime)));
        }

        [Test]
        public void Slots_AreIndependent()
        {
            _service.Save(0, CreateSample("Первый", day: 1));
            _service.Save(2, CreateSample("Третий", day: 9));

            Assert.That(_service.Load(0).Data.ProfileName, Is.EqualTo("Первый"));
            Assert.That(_service.Load(2).Data.world.day, Is.EqualTo(9));
            Assert.That(_service.GetSlotInfo(1).Status, Is.EqualTo(SaveSlotStatus.Empty));
        }

        [Test]
        public void Save_OverwritesExistingSlot()
        {
            _service.Save(0, CreateSample("Старый", day: 1));
            _service.Save(0, CreateSample("Новый", day: 7));

            var result = _service.Load(0);

            Assert.That(result.Data.ProfileName, Is.EqualTo("Новый"));
            Assert.That(result.Data.world.day, Is.EqualTo(7));
        }

        /// <summary>FR-004: повреждённый файл не роняет игру и даёт понятное сообщение.</summary>
        [Test]
        public void CorruptedFile_ReportsCorruptedWithoutThrowing()
        {
            var path = Path.Combine(_directory, "slot_0.json");
            File.WriteAllText(path, "{ это не json ");

            LoadResult result = default;
            Assert.DoesNotThrow(() => result = _service.Load(0));

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(SaveSlotStatus.Corrupted));
            Assert.That(result.Message, Is.Not.Empty);
        }

        [Test]
        public void EmptyFile_ReportsCorrupted()
        {
            File.WriteAllText(Path.Combine(_directory, "slot_0.json"), string.Empty);

            Assert.That(_service.Load(0).Status, Is.EqualTo(SaveSlotStatus.Corrupted));
        }

        /// <summary>FR-004: сохранение из более новой версии не грузится, но не ломает игру.</summary>
        [Test]
        public void NewerSchemaVersion_IsReportedAsUnsupported()
        {
            var data = CreateSample();
            data.SchemaVersion = SaveData.CurrentSchemaVersion + 5;
            File.WriteAllText(
                Path.Combine(_directory, "slot_0.json"),
                JsonUtility.ToJson(data, prettyPrint: true));

            var result = _service.Load(0);

            Assert.That(result.Success, Is.False);
            Assert.That(result.Status, Is.EqualTo(SaveSlotStatus.UnsupportedVersion));
            Assert.That(result.Message, Is.Not.Empty);
        }

        /// <summary>Повреждение одного слота не мешает загрузить другой валидный.</summary>
        [Test]
        public void CorruptedSlot_DoesNotAffectOtherSlots()
        {
            _service.Save(1, CreateSample("Целый", day: 5));
            File.WriteAllText(Path.Combine(_directory, "slot_0.json"), "мусор");

            Assert.That(_service.Load(0).Success, Is.False);
            Assert.That(_service.Load(1).Success, Is.True);
            Assert.That(_service.Load(1).Data.ProfileName, Is.EqualTo("Целый"));
        }

        [Test]
        public void EnumerateSlots_ReturnsEntryPerSlot()
        {
            _service.Save(0, CreateSample());

            var slots = _service.EnumerateSlots();

            Assert.That(slots, Has.Count.EqualTo(_service.SlotCount));
            Assert.That(slots[0].CanLoad, Is.True);
            Assert.That(slots[1].Status, Is.EqualTo(SaveSlotStatus.Empty));
        }

        [Test]
        public void DeleteSlot_RemovesData()
        {
            _service.Save(0, CreateSample());

            Assert.That(_service.DeleteSlot(0), Is.True);
            Assert.That(_service.GetSlotInfo(0).Status, Is.EqualTo(SaveSlotStatus.Empty));
            Assert.That(_service.DeleteSlot(0), Is.False, "Повторное удаление ничего не находит.");
        }

        [Test]
        public void OutOfRangeSlot_IsHandledGracefully()
        {
            // Сервис сообщает о некорректном индексе в лог — это ожидаемое поведение.
            LogAssert.Expect(LogType.Error, new Regex("Некорректный индекс слота"));

            Assert.That(_service.Save(99, CreateSample()), Is.False);
            Assert.That(_service.Load(99).Success, Is.False);
            Assert.That(_service.DeleteSlot(-1), Is.False);
        }

        [Test]
        public void Save_DoesNotLeaveTempFiles()
        {
            _service.Save(0, CreateSample());
            _service.Save(0, CreateSample("Второй"));

            Assert.That(Directory.GetFiles(_directory, "*.tmp"), Is.Empty);
            Assert.That(Directory.GetFiles(_directory, "*.bak"), Is.Empty);
        }

        [Test]
        public void SaveNullData_IsRejected()
        {
            LogAssert.Expect(LogType.Error, new Regex("Попытка сохранить пустые данные"));

            Assert.That(_service.Save(0, null), Is.False);
        }
    }
}
