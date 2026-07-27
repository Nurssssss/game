using System;
using System.Collections.Generic;
using QonaevLife.Core;
using QonaevLife.World;

namespace QonaevLife.Jobs
{
    /// <summary>
    /// Связывает взаимодействие с точками интереса и цикл курьерской смены
    /// (FR-070). Живёт отдельно от <see cref="JobShiftService"/>: сервис знает
    /// правила смены, а координатор — как игрок эти правила запускает.
    /// </summary>
    public sealed class CourierShiftCoordinator : IGameService
    {
        private readonly IEventBus _eventBus;
        private readonly JobShiftService _jobs;
        private readonly string _jobId;
        private readonly string _hubLocationId;
        private readonly Func<IReadOnlyDictionary<string, int>> _skillProvider;

        public CourierShiftCoordinator(IEventBus eventBus, JobShiftService jobs,
            string jobId, string hubLocationId,
            Func<IReadOnlyDictionary<string, int>> skillProvider = null)
        {
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
            _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));

            if (string.IsNullOrWhiteSpace(jobId))
                throw new ArgumentException("Не задан ID работы.", nameof(jobId));

            if (string.IsNullOrWhiteSpace(hubLocationId))
                throw new ArgumentException("Не задан ID пункта выдачи.", nameof(hubLocationId));

            _jobId = jobId;
            _hubLocationId = hubLocationId;
            _skillProvider = skillProvider;
        }

        /// <summary>Почему последняя попытка взять смену не удалась. Для UI.</summary>
        public ShiftStartFailure LastStartFailure { get; private set; }

        public void Initialize()
            => _eventBus.Subscribe<LocationInteractedEvent>(OnLocationInteracted);

        public void Shutdown()
            => _eventBus.Unsubscribe<LocationInteractedEvent>(OnLocationInteracted);

        private void OnLocationInteracted(LocationInteractedEvent interacted)
        {
            // Пока смена идёт, любая точка может оказаться целью текущего этапа —
            // это проверяется первым, иначе игрок не сможет сдать заказ в хабе.
            if (_jobs.State == ShiftState.InProgress)
            {
                _jobs.TryCompleteCurrentStage(interacted.LocationId);
                return;
            }

            if (!string.Equals(interacted.LocationId, _hubLocationId, StringComparison.Ordinal))
                return;

            var skills = _skillProvider?.Invoke();
            var result = _jobs.TryStartShift(_jobId, skills);
            LastStartFailure = result.Failure;
        }
    }
}
