using System;
using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.Core;
using QonaevLife.Economy;
using QonaevLife.World;

namespace QonaevLife.Jobs
{
    /// <summary>Состояние смены (FR-070).</summary>
    public enum ShiftState
    {
        None = 0,
        InProgress = 1,
        Completed = 2,
        Failed = 3
    }

    /// <summary>Почему смену нельзя взять.</summary>
    public enum ShiftStartFailure
    {
        None = 0,
        UnknownJob = 1,
        AlreadyOnShift = 2,
        WrongDayPhase = 3,
        SkillTooLow = 4,
        LocationUnavailable = 5
    }

    public readonly struct ShiftStartResult
    {
        private ShiftStartResult(bool success, ShiftStartFailure failure, string detail)
        {
            Success = success;
            Failure = failure;
            Detail = detail;
        }

        public bool Success { get; }
        public ShiftStartFailure Failure { get; }

        /// <summary>Уточнение для UI: какой навык или какая локация помешали.</summary>
        public string Detail { get; }

        public static ShiftStartResult Ok() => new(true, ShiftStartFailure.None, string.Empty);

        public static ShiftStartResult Fail(ShiftStartFailure failure, string detail = "")
            => new(false, failure, detail);
    }

    public readonly struct ShiftStartedEvent : IGameEvent
    {
        public ShiftStartedEvent(string jobId, string firstStageId, string targetLocationId)
        {
            JobId = jobId;
            FirstStageId = firstStageId;
            TargetLocationId = targetLocationId;
        }

        public string JobId { get; }
        public string FirstStageId { get; }
        public string TargetLocationId { get; }
    }

    public readonly struct ShiftStageChangedEvent : IGameEvent
    {
        public ShiftStageChangedEvent(string jobId, string stageId, string targetLocationId,
            int stageIndex, int stageCount)
        {
            JobId = jobId;
            StageId = stageId;
            TargetLocationId = targetLocationId;
            StageIndex = stageIndex;
            StageCount = stageCount;
        }

        public string JobId { get; }
        public string StageId { get; }
        public string TargetLocationId { get; }
        public int StageIndex { get; }
        public int StageCount { get; }
    }

    public readonly struct ShiftFinishedEvent : IGameEvent
    {
        public ShiftFinishedEvent(string jobId, ShiftState state, long payout, bool wasOnTime)
        {
            JobId = jobId;
            State = state;
            Payout = payout;
            WasOnTime = wasOnTime;
        }

        public string JobId { get; }
        public ShiftState State { get; }
        public long Payout { get; }
        public bool WasOnTime { get; }
    }

    /// <summary>
    /// Одна смена профессии MVP — курьер (FR-070). Ведёт цикл «получить смену →
    /// выполнить цели → завершить → получить оплату»: у смены есть начало,
    /// условия успеха и провала, понятный прогресс и денежный результат.
    /// Деньги начисляются только через транзакцию с ID смены (FR-075).
    /// </summary>
    public sealed class JobShiftService : IGameService
    {
        private readonly ContentDatabase _content;
        private readonly IEventBus _eventBus;
        private readonly IGameClock _clock;
        private readonly IWalletService _wallet;
        private readonly LocationRegistry _locations;

        private JobDefinition _job;
        private int _stageIndex;
        private double _stageDeadlineMinutes;
        private bool _missedAnyDeadline;
        private long _accruedStagePayouts;

        public JobShiftService(ContentDatabase content, IEventBus eventBus, IGameClock clock,
            IWalletService wallet, LocationRegistry locations)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _locations = locations ?? throw new ArgumentNullException(nameof(locations));
        }

        public ShiftState State { get; private set; } = ShiftState.None;

        public string ActiveJobId => _job != null ? _job.Id : string.Empty;

        public int StageIndex => _stageIndex;

        public int StageCount => _job?.Stages.Count ?? 0;

        /// <summary>Текущий этап или null, если смена не идёт.</summary>
        public JobStageDefinition? CurrentStage
            => _job != null && _stageIndex >= 0 && _stageIndex < _job.Stages.Count
                ? _job.Stages[_stageIndex]
                : null;

        /// <summary>ID локации текущей цели — маркер карты ведёт именно туда (FR-092).</summary>
        public string CurrentTargetLocationId => CurrentStage?.locationId ?? string.Empty;

        /// <summary>Сколько внутриигровых минут осталось на этап. null — без лимита.</summary>
        public double? MinutesRemaining
        {
            get
            {
                if (State != ShiftState.InProgress || _stageDeadlineMinutes <= 0d)
                    return null;

                return Math.Max(0d, _stageDeadlineMinutes - TotalMinutesNow());
            }
        }

        public void Initialize()
        {
        }

        public void Shutdown() => ResetShift();

        /// <summary>
        /// Берёт смену. Проверяет фазу суток, навыки и доступность локаций
        /// до любых изменений состояния, поэтому отказ ничего не ломает.
        /// </summary>
        public ShiftStartResult TryStartShift(string jobId,
            IReadOnlyDictionary<string, int> skillLevels)
        {
            if (State == ShiftState.InProgress)
                return ShiftStartResult.Fail(ShiftStartFailure.AlreadyOnShift);

            if (!_content.TryGetJob(jobId, out var job))
                return ShiftStartResult.Fail(ShiftStartFailure.UnknownJob, jobId);

            var phase = _clock.Phase.ToString();
            if (job.AvailablePhases.Count > 0 && !ContainsPhase(job.AvailablePhases, phase))
                return ShiftStartResult.Fail(ShiftStartFailure.WrongDayPhase, phase);

            foreach (var requirement in job.SkillRequirements)
            {
                if (string.IsNullOrWhiteSpace(requirement.skillId))
                    continue;

                var level = skillLevels != null
                            && skillLevels.TryGetValue(requirement.skillId, out var value)
                    ? value
                    : 0;

                if (level < requirement.minLevel)
                    return ShiftStartResult.Fail(ShiftStartFailure.SkillTooLow, requirement.skillId);
            }

            if (job.Stages.Count == 0)
                return ShiftStartResult.Fail(ShiftStartFailure.UnknownJob, jobId);

            // Первая цель должна быть достижима, иначе смена начнётся тупиком (FR-073).
            var firstStage = job.Stages[0];
            if (!_locations.IsValidObjectiveTarget(firstStage.locationId))
            {
                return ShiftStartResult.Fail(
                    ShiftStartFailure.LocationUnavailable, firstStage.locationId);
            }

            _job = job;
            _stageIndex = 0;
            _missedAnyDeadline = false;
            _accruedStagePayouts = 0;
            State = ShiftState.InProgress;
            _stageDeadlineMinutes = ResolveDeadline(firstStage);

            _eventBus.Publish(new ShiftStartedEvent(job.Id, firstStage.stageId, firstStage.locationId));

            return ShiftStartResult.Ok();
        }

        /// <summary>
        /// Отмечает текущий этап выполненным. Возвращает false, если смена не идёт
        /// или игрок не на нужной локации.
        /// </summary>
        public bool TryCompleteCurrentStage(string atLocationId)
        {
            if (State != ShiftState.InProgress || _job == null)
                return false;

            var stage = _job.Stages[_stageIndex];
            if (!string.Equals(stage.locationId, atLocationId, StringComparison.Ordinal))
                return false;

            if (IsDeadlineMissed())
                _missedAnyDeadline = true;

            // Промежуточная выплата начисляется сразу, чтобы прогресс был ощутим,
            // и тоже проходит через журнал транзакций (FR-075).
            if (stage.stagePayout > 0)
            {
                var result = _wallet.TryApply(new TransactionRequest(
                    stage.stagePayout,
                    TransactionReason.JobPayout,
                    sourceId: $"{_job.Id}:{stage.stageId}"));

                if (result.Applied)
                    _accruedStagePayouts += stage.stagePayout;
            }

            _stageIndex++;

            if (_stageIndex >= _job.Stages.Count)
            {
                CompleteShift();
                return true;
            }

            var next = _job.Stages[_stageIndex];
            _stageDeadlineMinutes = ResolveDeadline(next);

            _eventBus.Publish(new ShiftStageChangedEvent(
                _job.Id, next.stageId, next.locationId, _stageIndex, _job.Stages.Count));

            return true;
        }

        /// <summary>
        /// Проверяет просроченные лимиты. Вызывается из тика сессии, чтобы смена
        /// провалилась сама, если игрок не успел.
        /// </summary>
        public void Tick()
        {
            if (State != ShiftState.InProgress || !IsDeadlineMissed())
                return;

            FailShift();
        }

        /// <summary>Игрок отказался от смены — засчитывается как провал.</summary>
        public void AbandonShift()
        {
            if (State == ShiftState.InProgress)
                FailShift();
        }

        private void CompleteShift()
        {
            var wasOnTime = !_missedAnyDeadline;
            var payout = _job.BasePayout + (wasOnTime ? _job.OnTimeBonus : 0);

            if (payout > 0)
            {
                _wallet.TryApply(new TransactionRequest(
                    payout, TransactionReason.JobPayout, sourceId: _job.Id));
            }

            var jobId = _job.Id;
            var total = payout + _accruedStagePayouts;

            State = ShiftState.Completed;
            _eventBus.Publish(new ShiftFinishedEvent(jobId, ShiftState.Completed, total, wasOnTime));

            ResetShift();
        }

        private void FailShift()
        {
            var jobId = _job.Id;
            var penalty = _job.FailurePenalty;

            // Штраф не может уронить игрока в долг: списываем только то,
            // что есть на балансе (FR-064 — необратимого проигрыша нет).
            var chargeable = Math.Min(penalty, Math.Max(0, _wallet.Balance));
            if (chargeable > 0)
            {
                _wallet.TryApply(new TransactionRequest(
                    -chargeable, TransactionReason.Penalty, sourceId: jobId));
            }

            State = ShiftState.Failed;
            _eventBus.Publish(new ShiftFinishedEvent(
                jobId, ShiftState.Failed, -chargeable, wasOnTime: false));

            ResetShift();
        }

        private bool IsDeadlineMissed()
            => _stageDeadlineMinutes > 0d && TotalMinutesNow() > _stageDeadlineMinutes;

        private double ResolveDeadline(JobStageDefinition stage)
            => stage.timeLimitMinutes > 0f
                ? TotalMinutesNow() + stage.timeLimitMinutes
                : 0d;

        /// <summary>
        /// Сквозное время партии в минутах. Использует номер дня, поэтому лимит
        /// этапа корректно переживает переход через полночь.
        /// </summary>
        private double TotalMinutesNow()
            => (_clock.Day - 1) * 1440d + _clock.TimeOfDay.TotalMinutes;

        private static bool ContainsPhase(IReadOnlyList<string> phases, string phase)
        {
            for (var i = 0; i < phases.Count; i++)
            {
                if (string.Equals(phases[i], phase, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        private void ResetShift()
        {
            _job = null;
            _stageIndex = 0;
            _stageDeadlineMinutes = 0d;
            _missedAnyDeadline = false;
            _accruedStagePayouts = 0;
        }
    }
}
