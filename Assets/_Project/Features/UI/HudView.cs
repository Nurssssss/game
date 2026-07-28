using QonaevLife.Core;
using QonaevLife.Economy;
using QonaevLife.Jobs;
using QonaevLife.World;
using TMPro;
using UnityEngine;

namespace QonaevLife.UI
{
    /// <summary>
    /// Постоянный HUD (FR-090): время, деньги, активная цель и предупреждения
    /// о критических потребностях. Не перекрывает диалог и обновляет строки
    /// только при изменении данных, а не каждый кадр.
    /// </summary>
    public sealed class HudView : MonoBehaviour
    {
        [SerializeField] private TMP_Text clockLabel;
        [SerializeField] private TMP_Text moneyLabel;
        [SerializeField] private TMP_Text objectiveLabel;
        [SerializeField] private TMP_Text notificationLabel;

        [SerializeField] [Tooltip("Сколько секунд держится уведомление.")]
        [Min(0.5f)]
        private float notificationDuration = 4f;

        private IEventBus _eventBus;
        private IGameClock _clock;
        private IWalletService _wallet;
        private JobShiftService _jobs;
        private LocationRegistry _locations;
        private ILocalizedText _text;

        private int _lastShownMinute = -1;
        private long _lastShownBalance = long.MinValue;
        private float _notificationHideTime;

        public void Bind(IEventBus eventBus, IGameClock clock, IWalletService wallet,
            JobShiftService jobs, LocationRegistry locations, ILocalizedText text)
        {
            Unbind();

            _eventBus = eventBus;
            _clock = clock;
            _wallet = wallet;
            _jobs = jobs;
            _locations = locations;
            _text = text;

            _eventBus.Subscribe<ShiftStartedEvent>(OnShiftStarted);
            _eventBus.Subscribe<ShiftStageChangedEvent>(OnShiftStageChanged);
            _eventBus.Subscribe<ShiftFinishedEvent>(OnShiftFinished);
            _eventBus.Subscribe<WeatherChangedEvent>(OnWeatherChanged);
            _eventBus.Subscribe<LocationInteractedEvent>(OnLocationInteracted);

            RefreshObjective();
            HideNotification();
        }

        public void Unbind()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<ShiftStartedEvent>(OnShiftStarted);
            _eventBus.Unsubscribe<ShiftStageChangedEvent>(OnShiftStageChanged);
            _eventBus.Unsubscribe<ShiftFinishedEvent>(OnShiftFinished);
            _eventBus.Unsubscribe<WeatherChangedEvent>(OnWeatherChanged);
            _eventBus.Unsubscribe<LocationInteractedEvent>(OnLocationInteracted);
            _eventBus = null;
        }

        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (_clock == null)
                return;

            UpdateClock();
            UpdateMoney();
            UpdateNotificationTimer();

            // Остаток времени на этап тикает, поэтому цель обновляется вместе
            // с игровой минутой — этого достаточно для читаемого отсчёта.
            if (_jobs is { State: ShiftState.InProgress })
                RefreshObjective();
        }

        private void UpdateClock()
        {
            var timeOfDay = _clock.TimeOfDay;
            var minute = (int)timeOfDay.TotalMinutes;

            // Строка перерисовывается раз в игровую минуту, а не каждый кадр.
            if (minute == _lastShownMinute)
                return;

            _lastShownMinute = minute;

            if (clockLabel == null)
                return;

            var phase = _text.Resolve($"phase.{_clock.Phase}");
            clockLabel.text =
                $"{_text.Resolve("hud.day")} {_clock.Day}   " +
                $"{timeOfDay.Hours:D2}:{timeOfDay.Minutes:D2}   {phase}";
        }

        private void UpdateMoney()
        {
            if (_wallet == null || moneyLabel == null || _wallet.Balance == _lastShownBalance)
                return;

            _lastShownBalance = _wallet.Balance;
            moneyLabel.text = $"{_text.Resolve("hud.money")}: {_wallet.Balance} {_text.Resolve("common.currency")}";
        }

        private void RefreshObjective()
        {
            if (objectiveLabel == null)
                return;

            var stage = _jobs?.CurrentStage;

            if (stage == null)
            {
                objectiveLabel.text = _text.Resolve("hud.no_objective");
                return;
            }

            var objective = _text.Resolve(stage.Value.objectiveKey);
            var remaining = _jobs.MinutesRemaining;

            objectiveLabel.text = remaining.HasValue
                ? $"{_text.Resolve("hud.objective")}: {objective} ({(int)remaining.Value} мин)"
                : $"{_text.Resolve("hud.objective")}: {objective}";
        }

        private void OnShiftStarted(ShiftStartedEvent started)
        {
            ShowNotification(_text.Resolve("job.shift_started"));
            RefreshObjective();
        }

        private void OnShiftStageChanged(ShiftStageChangedEvent changed) => RefreshObjective();

        private void OnShiftFinished(ShiftFinishedEvent finished)
        {
            var message = finished.State == ShiftState.Completed
                ? $"{_text.Resolve("job.shift_completed")} {finished.Payout} {_text.Resolve("common.currency")}"
                : _text.Resolve("job.shift_failed");

            ShowNotification(message);
            RefreshObjective();
        }

        private void OnWeatherChanged(WeatherChangedEvent changed)
            => ShowNotification(_text.Resolve($"weather.{changed.Current}"));

        /// <summary>
        /// Название локации берётся из её определения: ID и ключ локализации
        /// различаются, поэтому склеивать ключ из ID нельзя.
        /// </summary>
        private void OnLocationInteracted(LocationInteractedEvent interacted)
        {
            if (_locations != null
                && _locations.TryGet(interacted.LocationId, out var definition))
            {
                ShowNotification(_text.Resolve(definition.DisplayNameKey));
                return;
            }

            ShowNotification(interacted.LocationId);
        }

        private void ShowNotification(string message)
        {
            if (notificationLabel == null)
                return;

            notificationLabel.text = message;
            notificationLabel.gameObject.SetActive(true);
            _notificationHideTime = Time.time + notificationDuration;
        }

        private void UpdateNotificationTimer()
        {
            if (notificationLabel == null
                || !notificationLabel.gameObject.activeSelf
                || Time.time < _notificationHideTime)
            {
                return;
            }

            HideNotification();
        }

        private void HideNotification()
        {
            if (notificationLabel != null)
                notificationLabel.gameObject.SetActive(false);
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(TMP_Text clock, TMP_Text money, TMP_Text objective,
            TMP_Text notification)
        {
            clockLabel = clock;
            moneyLabel = money;
            objectiveLabel = objective;
            notificationLabel = notification;
        }
    }
}
