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
        private readonly Func<bool> _dialogueGuard;

        public CourierShiftCoordinator(IEventBus eventBus, JobShiftService jobs,
            string jobId, string hubLocationId,
            Func<IReadOnlyDictionary<string, int>> skillProvider = null,
            Func<bool> dialogueGuard = null)
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
            _dialogueGuard = dialogueGuard;
        }

        /// <summary>Почему последняя попытка взять смену не удалась. Для UI.</summary>
        public ShiftStartFailure LastStartFailure { get; private set; }

        public void Initialize()
        {
            _eventBus.Subscribe<LocationInteractedEvent>(OnLocationInteracted);
            _eventBus.Subscribe<Dialogue.DialogueEffectRequestedEvent>(OnDialogueEffect);
        }

        public void Shutdown()
        {
            _eventBus.Unsubscribe<LocationInteractedEvent>(OnLocationInteracted);
            _eventBus.Unsubscribe<Dialogue.DialogueEffectRequestedEvent>(OnDialogueEffect);
        }

        /// <summary>
        /// Диалог выдаёт смену эффектом «job» с ID работы — так игрок получает
        /// работу через разговор с диспетчером, а не молча по нажатию клавиши.
        /// </summary>
        private void OnDialogueEffect(Dialogue.DialogueEffectRequestedEvent effect)
        {
            if (!string.Equals(effect.EffectType, "job", StringComparison.Ordinal))
                return;

            if (!string.Equals(effect.TargetId, _jobId, StringComparison.Ordinal))
                return;

            if (_jobs.State != ShiftState.InProgress)
                TryStart();
        }

        private void OnLocationInteracted(LocationInteractedEvent interacted)
        {
            // Пока смена идёт, любая точка может оказаться целью текущего этапа —
            // это проверяется первым, иначе игрок не сможет сдать заказ в хабе.
            if (_jobs.State == ShiftState.InProgress)
            {
                _jobs.TryCompleteCurrentStage(interacted.LocationId);
                return;
            }

            // Смену вне диалога выдаём только там, где нет говорящего NPC:
            // иначе игрок получил бы работу молча, минуя разговор с диспетчером.
            if (_dialogueGuard != null && _dialogueGuard())
                return;

            if (!string.Equals(interacted.LocationId, _hubLocationId, StringComparison.Ordinal))
                return;

            TryStart();
        }

        /// <summary>
        /// Начинает смену по требованию — например, из эффекта выбора реплики
        /// «Беру смену» (FR-070).
        /// </summary>
        public bool TryStart()
        {
            var skills = _skillProvider?.Invoke();
            var result = _jobs.TryStartShift(_jobId, skills);
            LastStartFailure = result.Failure;

            return result.Success;
        }
    }
}
