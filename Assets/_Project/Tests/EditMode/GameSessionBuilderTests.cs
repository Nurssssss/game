using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using QonaevLife.Bootstrap;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Economy;
using QonaevLife.Language;
using QonaevLife.Player;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>
    /// Композиционный корень целиком (п. 4.2 ТЗ) и round-trip сохранения (AT-005).
    /// </summary>
    [TestFixture]
    public sealed class GameSessionBuilderTests
    {
        private readonly List<ScriptableObject> _created = new();

        private string _directory;
        private GameSessionConfig _config;
        private ContentDatabase _content;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(
                Path.GetTempPath(), "QonaevLifeSession", Path.GetRandomFileName());
            Directory.CreateDirectory(_directory);

            _config = Create<GameSessionConfig>();
            _content = Create<ContentDatabase>();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var asset in _created)
                Object.DestroyImmediate(asset);

            _created.Clear();

            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
        }

        private T Create<T>() where T : ScriptableObject
        {
            var asset = ScriptableObject.CreateInstance<T>();
            _created.Add(asset);
            return asset;
        }

        private GameSession BuildSession() =>
            GameSessionBuilder.Build(_config, _content, _directory);

        [Test]
        public void Build_CreatesAllServices()
        {
            var session = BuildSession();

            Assert.That(session.Clock, Is.Not.Null);
            Assert.That(session.Weather, Is.Not.Null);
            Assert.That(session.Wallet, Is.Not.Null);
            Assert.That(session.Needs, Is.Not.Null);
            Assert.That(session.Language, Is.Not.Null);
            Assert.That(session.SaveService, Is.Not.Null);
            Assert.That(session.Locations, Is.Not.Null);
            Assert.That(session.Dialogue, Is.Not.Null);
            Assert.That(session.Jobs, Is.Not.Null);

            session.Shutdown();
        }

        [Test]
        public void Build_RegistersContractsInRegistry()
        {
            var session = BuildSession();

            Assert.That(session.Registry.Resolve<IEventBus>(), Is.SameAs(session.EventBus));
            Assert.That(session.Registry.Resolve<IGameClock>(), Is.SameAs(session.Clock));
            Assert.That(session.Registry.Resolve<IWalletService>(), Is.SameAs(session.Wallet));
            Assert.That(session.Registry.Resolve<ISaveService>(), Is.SameAs(session.SaveService));
            Assert.That(session.Registry.Resolve<ILanguageProgressService>(),
                Is.SameAs(session.Language));

            session.Shutdown();
        }

        [Test]
        public void Build_WithoutConfig_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => GameSessionBuilder.Build(null, _content, _directory));
        }

        [Test]
        public void Build_WithoutContent_Throws()
        {
            Assert.Throws<System.ArgumentNullException>(
                () => GameSessionBuilder.Build(_config, null, _directory));
        }

        [Test]
        public void Build_WithInvalidConfig_Throws()
        {
            // Ломаем границы фаз: вечер раньше дня.
            var so = new SerializedObject(_config);
            so.FindProperty("clock").FindPropertyRelative("eveningStartHour").intValue = 2;
            so.ApplyModifiedPropertiesWithoutUndo();

            Assert.Throws<System.InvalidOperationException>(() => BuildSession());
        }

        /// <summary>FR-002, FR-050: стартовый капитал начисляется транзакцией.</summary>
        [Test]
        public void ApplyNewGameState_CreditsStartingCapitalWithReason()
        {
            var session = BuildSession();

            GameSessionBuilder.ApplyNewGameState(session, _config);

            Assert.That(session.Wallet.Balance, Is.GreaterThan(0));
            Assert.That(session.Wallet.RecentTransactions, Has.Count.EqualTo(1));

            var record = session.Wallet.RecentTransactions[0];
            Assert.That(record.Reason, Is.EqualTo(TransactionReason.StartingCapital));
            Assert.That(record.SourceId, Is.EqualTo("new_game"));

            session.Shutdown();
        }

        [Test]
        public void Tick_AdvancesClockAndDecaysNeeds()
        {
            var session = BuildSession();
            var hungerBefore = session.Needs.GetValue(NeedIds.Hunger);
            var timeBefore = session.Clock.TimeOfDay;

            // Скорость по умолчанию — 1 игровая минута в реальную секунду.
            for (var i = 0; i < 60; i++)
                session.Tick(1f, Vector3.zero);

            Assert.That(session.Clock.TimeOfDay, Is.GreaterThan(timeBefore));
            Assert.That(session.Needs.GetValue(NeedIds.Hunger), Is.LessThan(hungerBefore));

            session.Shutdown();
        }

        [Test]
        public void Tick_WhilePaused_DoesNothing()
        {
            var session = BuildSession();
            session.Clock.Pause();
            var timeBefore = session.Clock.TimeOfDay;

            session.Tick(10f, Vector3.zero);

            Assert.That(session.Clock.TimeOfDay, Is.EqualTo(timeBefore));

            session.Shutdown();
        }

        /// <summary>AT-005: после сохранения и загрузки восстанавливаются деньги, время и словарь.</summary>
        [Test]
        public void SaveAndRestore_RoundTripsSessionState()
        {
            var session = BuildSession();
            GameSessionBuilder.ApplyNewGameState(session, _config);

            session.Clock.SkipMinutes(5 * 60);
            session.Language.SetMode(TranslationMode.KazakhWithRussian);
            session.Language.AddWord("word_custom");
            session.Language.AddExperience(120f);
            session.Wallet.TryApply(new TransactionRequest(
                -500, TransactionReason.Purchase, "shop_test"));

            var expectedBalance = session.Wallet.Balance;
            var expectedDay = session.Clock.Day;
            var expectedHour = session.Clock.TimeOfDay.TotalHours;
            var expectedLevel = session.Language.Level;

            var data = session.CaptureSave("Тестовый профиль");
            Assert.That(session.SaveService.Save(0, data), Is.True);

            session.Shutdown();

            // Новая сессия читает слот с нуля.
            var reloaded = BuildSession();
            var result = reloaded.SaveService.Load(0);

            Assert.That(result.Success, Is.True);
            reloaded.RestoreSave(result.Data);

            Assert.That(reloaded.Wallet.Balance, Is.EqualTo(expectedBalance));
            Assert.That(reloaded.Clock.Day, Is.EqualTo(expectedDay));
            Assert.That(reloaded.Clock.TimeOfDay.TotalHours,
                Is.EqualTo(expectedHour).Within(0.001));
            Assert.That(reloaded.Language.Level, Is.EqualTo(expectedLevel));
            Assert.That(reloaded.Language.Mode, Is.EqualTo(TranslationMode.KazakhWithRussian));
            Assert.That(reloaded.Language.TryGetWord("word_custom", out _), Is.True);
            Assert.That(result.Data.ProfileName, Is.EqualTo("Тестовый профиль"));

            reloaded.Shutdown();
        }

        [Test]
        public void CaptureSave_StampsCurrentSchemaVersion()
        {
            var session = BuildSession();

            var data = session.CaptureSave("Профиль");

            Assert.That(data.world.day, Is.EqualTo(session.Clock.Day));
            Assert.That(data.player.needs, Is.Not.Empty);

            session.Shutdown();
        }

        [Test]
        public void Shutdown_ClearsRegistry()
        {
            var session = BuildSession();

            session.Shutdown();

            Assert.That(session.Registry.TryResolve<IGameClock>(out _), Is.False);
        }

        [Test]
        public void SaveDirectory_IsCreatedUnderConfiguredFolder()
        {
            var session = BuildSession();

            session.SaveService.Save(0, session.CaptureSave("Профиль"));

            var expected = Path.Combine(_directory, _config.SaveFolderName);
            Assert.That(Directory.Exists(expected), Is.True);

            session.Shutdown();
        }
    }
}
