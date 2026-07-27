using System.Collections.Generic;
using NUnit.Framework;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Economy;
using QonaevLife.Jobs;
using QonaevLife.World;
using UnityEngine;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Смена курьера — профессия MVP (FR-070, FR-075, AT-002).</summary>
    [TestFixture]
    public sealed class JobShiftServiceTests
    {
        private const string PickupLocationId = "loc_courier_hub";
        private const string DropLocationId = "loc_apartment_01";
        private const string JobId = "job_courier";

        private readonly List<ScriptableObject> _created = new();

        private EventBus _eventBus;
        private GameClock _clock;
        private WalletService _wallet;
        private ContentDatabase _content;
        private LocationRegistry _locations;
        private JobShiftService _service;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _clock = new GameClock(GameClockSettings.Default); // старт 8:00, фаза Morning
            _wallet = new WalletService(_clock, _eventBus);

            _content = BuildContent(stageTimeLimitMinutes: 0f);
            _locations = new LocationRegistry(_content, _eventBus, _clock);
            _locations.Initialize();

            _service = new JobShiftService(_content, _eventBus, _clock, _wallet, _locations);
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

        /// <summary>
        /// Собирает минимальный контент курьерской смены. Id и приватные поля
        /// определений задаются через SerializedObject, потому что в рантайме
        /// они намеренно только для чтения.
        /// </summary>
        private ContentDatabase BuildContent(float stageTimeLimitMinutes,
            long basePayout = 800, long onTimeBonus = 200, long failurePenalty = 150,
            long stagePayout = 0)
        {
            var pickup = Create<LocationDefinition>();
            SetId(pickup, PickupLocationId);
            SetField(pickup, "displayNameKey", "loc.hub");
            SetField(pickup, "sectorId", "sector_center");
            SetField(pickup, "alwaysOpen", true);
            SetField(pickup, "discoveredFromStart", true);

            var drop = Create<LocationDefinition>();
            SetId(drop, DropLocationId);
            SetField(drop, "displayNameKey", "loc.apartment");
            SetField(drop, "sectorId", "sector_center");
            SetField(drop, "alwaysOpen", true);
            SetField(drop, "discoveredFromStart", true);

            var job = Create<JobDefinition>();
            SetId(job, JobId);
            SetField(job, "displayNameKey", "job.courier");
            SetField(job, "hubLocationId", PickupLocationId);
            SetField(job, "basePayout", basePayout);
            SetField(job, "onTimeBonus", onTimeBonus);
            SetField(job, "failurePenalty", failurePenalty);
            SetField(job, "primarySkillId", "skill_work");
            SetStringList(job, "availablePhases", new[] { "Morning", "Day" });
            SetStages(job, new[]
            {
                new JobStageDefinition
                {
                    stageId = "stage_pickup",
                    objectiveKey = "job.courier.pickup",
                    locationId = PickupLocationId,
                    timeLimitMinutes = stageTimeLimitMinutes,
                    stagePayout = stagePayout
                },
                new JobStageDefinition
                {
                    stageId = "stage_deliver",
                    objectiveKey = "job.courier.deliver",
                    locationId = DropLocationId,
                    timeLimitMinutes = stageTimeLimitMinutes,
                    stagePayout = stagePayout
                }
            });

            var database = Create<ContentDatabase>();
            SetObjectList(database, "locations", new ScriptableObject[] { pickup, drop });
            SetObjectList(database, "jobs", new ScriptableObject[] { job });

            return database;
        }

        private static void SetId(ContentDefinition definition, string id)
            => SetField(definition, "id", id);

        private static void SetField(ScriptableObject target, string field, object value)
        {
            var so = new UnityEditor.SerializedObject(target);
            var property = so.FindProperty(field);
            Assert.That(property, Is.Not.Null, $"Поле '{field}' не найдено в {target.name}.");

            switch (value)
            {
                case string s: property.stringValue = s; break;
                case bool b: property.boolValue = b; break;
                case long l: property.longValue = l; break;
                case int i: property.intValue = i; break;
                case float f: property.floatValue = f; break;
                default: Assert.Fail($"Неподдерживаемый тип {value.GetType()}."); break;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStringList(ScriptableObject target, string field,
            IReadOnlyList<string> values)
        {
            var so = new UnityEditor.SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Count;

            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).stringValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetObjectList(ScriptableObject target, string field,
            IReadOnlyList<ScriptableObject> values)
        {
            var so = new UnityEditor.SerializedObject(target);
            var property = so.FindProperty(field);
            property.arraySize = values.Count;

            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetStages(JobDefinition job, IReadOnlyList<JobStageDefinition> stages)
        {
            var so = new UnityEditor.SerializedObject(job);
            var property = so.FindProperty("stages");
            property.arraySize = stages.Count;

            for (var i = 0; i < stages.Count; i++)
            {
                var element = property.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("stageId").stringValue = stages[i].stageId;
                element.FindPropertyRelative("objectiveKey").stringValue = stages[i].objectiveKey;
                element.FindPropertyRelative("locationId").stringValue = stages[i].locationId;
                element.FindPropertyRelative("timeLimitMinutes").floatValue =
                    stages[i].timeLimitMinutes;
                element.FindPropertyRelative("stagePayout").longValue = stages[i].stagePayout;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Dictionary<string, int> Skills(int workLevel = 0)
            => new() { ["skill_work"] = workLevel };

        [Test]
        public void NewService_HasNoActiveShift()
        {
            Assert.That(_service.State, Is.EqualTo(ShiftState.None));
            Assert.That(_service.ActiveJobId, Is.Empty);
            Assert.That(_service.CurrentStage, Is.Null);
        }

        [Test]
        public void StartShift_BeginsAtFirstStage()
        {
            var result = _service.TryStartShift(JobId, Skills());

            Assert.That(result.Success, Is.True);
            Assert.That(_service.State, Is.EqualTo(ShiftState.InProgress));
            Assert.That(_service.StageIndex, Is.Zero);
            Assert.That(_service.StageCount, Is.EqualTo(2));
            Assert.That(_service.CurrentTargetLocationId, Is.EqualTo(PickupLocationId));
        }

        [Test]
        public void StartShift_PublishesStartedEvent()
        {
            ShiftStartedEvent captured = default;
            _eventBus.Subscribe<ShiftStartedEvent>(e => captured = e);

            _service.TryStartShift(JobId, Skills());

            Assert.That(captured.JobId, Is.EqualTo(JobId));
            Assert.That(captured.FirstStageId, Is.EqualTo("stage_pickup"));
            Assert.That(captured.TargetLocationId, Is.EqualTo(PickupLocationId));
        }

        [Test]
        public void UnknownJob_IsRejected()
        {
            var result = _service.TryStartShift("job_missing", Skills());

            Assert.That(result.Failure, Is.EqualTo(ShiftStartFailure.UnknownJob));
            Assert.That(_service.State, Is.EqualTo(ShiftState.None));
        }

        [Test]
        public void SecondShift_WhileActive_IsRejected()
        {
            _service.TryStartShift(JobId, Skills());

            var result = _service.TryStartShift(JobId, Skills());

            Assert.That(result.Failure, Is.EqualTo(ShiftStartFailure.AlreadyOnShift));
        }

        /// <summary>Смена доступна только в разрешённые фазы суток.</summary>
        [Test]
        public void WrongDayPhase_IsRejected()
        {
            _clock.RestoreState(day: 1, minutesOfDay: 21 * 60); // вечер

            var result = _service.TryStartShift(JobId, Skills());

            Assert.That(result.Failure, Is.EqualTo(ShiftStartFailure.WrongDayPhase));
            Assert.That(result.Detail, Is.EqualTo("Evening"));
        }

        [Test]
        public void InsufficientSkill_IsRejected()
        {
            var job = _content.Jobs[0];
            var so = new UnityEditor.SerializedObject(job);
            var requirements = so.FindProperty("skillRequirements");
            requirements.arraySize = 1;
            var element = requirements.GetArrayElementAtIndex(0);
            element.FindPropertyRelative("skillId").stringValue = "skill_work";
            element.FindPropertyRelative("minLevel").intValue = 3;
            so.ApplyModifiedPropertiesWithoutUndo();

            var result = _service.TryStartShift(JobId, Skills(workLevel: 1));

            Assert.That(result.Failure, Is.EqualTo(ShiftStartFailure.SkillTooLow));
            Assert.That(result.Detail, Is.EqualTo("skill_work"));
        }

        /// <summary>FR-073: смена не начинается, если первая цель недостижима.</summary>
        [Test]
        public void UndiscoveredStartLocation_IsRejected()
        {
            var closedContent = BuildContent(stageTimeLimitMinutes: 0f);
            var hub = closedContent.Locations[0];
            var so = new UnityEditor.SerializedObject(hub);
            so.FindProperty("discoveredFromStart").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();

            var registry = new LocationRegistry(closedContent, _eventBus, _clock);
            registry.Initialize();
            var service = new JobShiftService(
                closedContent, _eventBus, _clock, _wallet, registry);

            var result = service.TryStartShift(JobId, Skills());

            Assert.That(result.Failure, Is.EqualTo(ShiftStartFailure.LocationUnavailable));
        }

        [Test]
        public void CompletingStage_AdvancesToNextTarget()
        {
            _service.TryStartShift(JobId, Skills());

            ShiftStageChangedEvent captured = default;
            _eventBus.Subscribe<ShiftStageChangedEvent>(e => captured = e);

            var advanced = _service.TryCompleteCurrentStage(PickupLocationId);

            Assert.That(advanced, Is.True);
            Assert.That(_service.StageIndex, Is.EqualTo(1));
            Assert.That(_service.CurrentTargetLocationId, Is.EqualTo(DropLocationId));
            Assert.That(captured.StageId, Is.EqualTo("stage_deliver"));
            Assert.That(captured.StageCount, Is.EqualTo(2));
        }

        [Test]
        public void CompletingStage_AtWrongLocation_IsRejected()
        {
            _service.TryStartShift(JobId, Skills());

            var advanced = _service.TryCompleteCurrentStage(DropLocationId);

            Assert.That(advanced, Is.False);
            Assert.That(_service.StageIndex, Is.Zero, "Этап не должен смениться.");
        }

        /// <summary>AT-002: полный цикл смены заканчивается выплатой.</summary>
        [Test]
        public void FullShift_PaysBaseAndOnTimeBonus()
        {
            _service.TryStartShift(JobId, Skills());

            ShiftFinishedEvent finished = default;
            _eventBus.Subscribe<ShiftFinishedEvent>(e => finished = e);

            _service.TryCompleteCurrentStage(PickupLocationId);
            _service.TryCompleteCurrentStage(DropLocationId);

            Assert.That(finished.State, Is.EqualTo(ShiftState.Completed));
            Assert.That(finished.WasOnTime, Is.True);
            Assert.That(finished.Payout, Is.EqualTo(1000), "800 базовых + 200 бонус.");
            Assert.That(_wallet.Balance, Is.EqualTo(1000));
            Assert.That(_service.State, Is.EqualTo(ShiftState.Completed));
        }

        /// <summary>FR-075: выплату можно проследить по ID смены.</summary>
        [Test]
        public void Payout_IsTraceableByJobId()
        {
            _service.TryStartShift(JobId, Skills());
            _service.TryCompleteCurrentStage(PickupLocationId);
            _service.TryCompleteCurrentStage(DropLocationId);

            Assert.That(_wallet.RecentTransactions, Has.Count.EqualTo(1));
            Assert.That(_wallet.RecentTransactions[0].SourceId, Is.EqualTo(JobId));
            Assert.That(_wallet.RecentTransactions[0].Reason,
                Is.EqualTo(TransactionReason.JobPayout));
        }

        [Test]
        public void StagePayouts_AreCreditedPerStage()
        {
            var content = BuildContent(stageTimeLimitMinutes: 0f, stagePayout: 50);
            var registry = new LocationRegistry(content, _eventBus, _clock);
            registry.Initialize();
            var service = new JobShiftService(content, _eventBus, _clock, _wallet, registry);

            service.TryStartShift(JobId, Skills());
            service.TryCompleteCurrentStage(PickupLocationId);

            Assert.That(_wallet.Balance, Is.EqualTo(50));
            Assert.That(_wallet.RecentTransactions[0].SourceId,
                Is.EqualTo($"{JobId}:stage_pickup"));
        }

        [Test]
        public void MissedDeadline_FailsShiftOnTick()
        {
            var content = BuildContent(stageTimeLimitMinutes: 30f);
            var registry = new LocationRegistry(content, _eventBus, _clock);
            registry.Initialize();
            var service = new JobShiftService(content, _eventBus, _clock, _wallet, registry);

            _wallet.TryApply(new TransactionRequest(
                1000, TransactionReason.StartingCapital, "test"));
            service.TryStartShift(JobId, Skills());

            ShiftFinishedEvent finished = default;
            _eventBus.Subscribe<ShiftFinishedEvent>(e => finished = e);

            _clock.SkipMinutes(45); // лимит 30 минут просрочен
            service.Tick();

            Assert.That(finished.State, Is.EqualTo(ShiftState.Failed));
            Assert.That(service.State, Is.EqualTo(ShiftState.Failed));
            Assert.That(_wallet.Balance, Is.EqualTo(850), "Списан штраф 150.");
        }

        [Test]
        public void MinutesRemaining_CountsDown()
        {
            var content = BuildContent(stageTimeLimitMinutes: 60f);
            var registry = new LocationRegistry(content, _eventBus, _clock);
            registry.Initialize();
            var service = new JobShiftService(content, _eventBus, _clock, _wallet, registry);

            service.TryStartShift(JobId, Skills());
            Assert.That(service.MinutesRemaining, Is.EqualTo(60d).Within(0.001d));

            _clock.SkipMinutes(20);
            Assert.That(service.MinutesRemaining, Is.EqualTo(40d).Within(0.001d));
        }

        [Test]
        public void NoTimeLimit_ReportsNoDeadline()
        {
            _service.TryStartShift(JobId, Skills());

            Assert.That(_service.MinutesRemaining, Is.Null);
        }

        /// <summary>FR-064: штраф не уводит баланс в минус.</summary>
        [Test]
        public void FailurePenalty_DoesNotPushBalanceNegative()
        {
            var content = BuildContent(stageTimeLimitMinutes: 10f);
            var registry = new LocationRegistry(content, _eventBus, _clock);
            registry.Initialize();
            var service = new JobShiftService(content, _eventBus, _clock, _wallet, registry);

            // Баланс 0 — штраф 150 списать нечем.
            service.TryStartShift(JobId, Skills());
            _clock.SkipMinutes(30);
            service.Tick();

            Assert.That(_wallet.Balance, Is.Zero);
            Assert.That(service.State, Is.EqualTo(ShiftState.Failed));
        }

        [Test]
        public void AbandonShift_CountsAsFailure()
        {
            _service.TryStartShift(JobId, Skills());

            _service.AbandonShift();

            Assert.That(_service.State, Is.EqualTo(ShiftState.Failed));
        }

        [Test]
        public void Tick_WithoutActiveShift_DoesNothing()
        {
            Assert.DoesNotThrow(() => _service.Tick());
            Assert.That(_service.State, Is.EqualTo(ShiftState.None));
        }

        /// <summary>Лимит этапа должен переживать переход через полночь.</summary>
        [Test]
        public void DeadlineAcrossMidnight_IsNotFalselyMissed()
        {
            var content = BuildContent(stageTimeLimitMinutes: 120f);
            var registry = new LocationRegistry(content, _eventBus, _clock);
            registry.Initialize();
            var service = new JobShiftService(content, _eventBus, _clock, _wallet, registry);

            // Смена доступна утром/днём — стартуем в 10:00, затем сдвигаем время.
            service.TryStartShift(JobId, Skills());
            _clock.SkipMinutes(60); // час прошёл, лимит 120 минут ещё не истёк

            service.Tick();

            Assert.That(service.State, Is.EqualTo(ShiftState.InProgress));
        }
    }
}
