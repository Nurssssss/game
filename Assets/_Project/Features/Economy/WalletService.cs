using System;
using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.Economy
{
    /// <summary>
    /// Кошелёк с журналом транзакций (FR-050, FR-075, AT-006).
    /// Баланс изменяется только после успешной проверки, поэтому отказ
    /// не оставляет частичного эффекта. Журнал ограничен по размеру (п. 7 ТЗ).
    /// </summary>
    public sealed class WalletService : IWalletService, IGameService
    {
        /// <summary>Сколько последних транзакций попадает в сохранение.</summary>
        public const int MaxJournalEntries = 50;

        private readonly IGameClock _clock;
        private readonly IEventBus _eventBus;
        private readonly List<TransactionRecord> _journal = new();

        private long _balance;
        private long _nextTransactionOrdinal = 1;

        public WalletService(IGameClock clock, IEventBus eventBus)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _eventBus = eventBus ?? throw new ArgumentNullException(nameof(eventBus));
        }

        public long Balance => _balance;

        public IReadOnlyList<TransactionRecord> RecentTransactions => _journal;

        public void Initialize()
        {
        }

        public void Shutdown() => _journal.Clear();

        public bool CanAfford(long cost) => cost <= 0 || _balance >= cost;

        public TransactionResult TryApply(TransactionRequest request)
        {
            if (request.Amount == 0)
                return TransactionResult.Rejected(TransactionStatus.InvalidAmount, _balance);

            if (string.IsNullOrWhiteSpace(request.SourceId))
                return TransactionResult.Rejected(TransactionStatus.InvalidAmount, _balance);

            // Знак суммы обязан соответствовать причине: расходная причина не может
            // приносить деньги, а доходная — списывать их. Скидка и возврат — явно
            // разрешённое исключение, которое возвращает деньги игроку (п. 10 ТЗ).
            if (!IsSignConsistentWithReason(request.Amount, request.Reason))
            {
                return TransactionResult.Rejected(
                    TransactionStatus.SignMismatchesReason, _balance);
            }

            if (request.Amount < 0)
            {
                var cost = -request.Amount;
                if (_balance < cost)
                    return TransactionResult.Rejected(TransactionStatus.InsufficientFunds, _balance);
            }

            var previousBalance = _balance;
            _balance = checked(_balance + request.Amount);

            var record = new TransactionRecord(
                transactionId: $"tx_{_nextTransactionOrdinal++:D6}",
                amount: request.Amount,
                balanceAfter: _balance,
                reason: request.Reason,
                sourceId: request.SourceId,
                gameDay: _clock.Day,
                gameMinutesOfDay: _clock.TimeOfDay.TotalMinutes);

            AppendToJournal(record);
            _eventBus.Publish(new BalanceChangedEvent(previousBalance, _balance, record));

            return TransactionResult.Ok(_balance, record);
        }

        /// <summary>Восстанавливает состояние из сохранения.</summary>
        public void RestoreState(EconomySaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            _balance = data.balance;
            _journal.Clear();
            _nextTransactionOrdinal = 1;

            if (data.recentTransactions == null)
                return;

            foreach (var entry in data.recentTransactions)
            {
                if (entry == null)
                    continue;

                var reason = Enum.TryParse<TransactionReason>(entry.reasonId, out var parsed)
                    ? parsed
                    : TransactionReason.Unknown;

                _journal.Add(new TransactionRecord(
                    entry.transactionId,
                    entry.amount,
                    balanceAfter: 0,
                    reason,
                    entry.sourceId,
                    entry.gameDay,
                    entry.gameMinutesOfDay));
            }

            _nextTransactionOrdinal = _journal.Count + 1;
        }

        /// <summary>Записывает состояние в сохранение.</summary>
        public void CaptureState(EconomySaveData data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));

            data.balance = _balance;
            data.recentTransactions.Clear();

            foreach (var record in _journal)
            {
                data.recentTransactions.Add(new TransactionRecordData
                {
                    transactionId = record.TransactionId,
                    amount = record.Amount,
                    reasonId = record.Reason.ToString(),
                    sourceId = record.SourceId,
                    gameDay = record.GameDay,
                    gameMinutesOfDay = record.GameMinutesOfDay
                });
            }
        }

        /// <summary>
        /// Доходные причины начисляют деньги, расходные — списывают.
        /// Возврат и скидка всегда возвращают деньги игроку.
        /// </summary>
        private static bool IsSignConsistentWithReason(long amount, TransactionReason reason)
            => reason switch
            {
                TransactionReason.StartingCapital => amount > 0,
                TransactionReason.JobPayout => amount > 0,
                TransactionReason.QuestReward => amount > 0,
                TransactionReason.Sale => amount > 0,
                TransactionReason.Refund => amount > 0,
                TransactionReason.Discount => amount > 0,

                TransactionReason.Purchase => amount < 0,
                TransactionReason.TaxiFare => amount < 0,
                TransactionReason.Fuel => amount < 0,
                TransactionReason.Rent => amount < 0,
                TransactionReason.PropertyPurchase => amount < 0,
                TransactionReason.Penalty => amount < 0,

                // Unknown и любые новые причины обязаны быть описаны явно.
                _ => false
            };

        private void AppendToJournal(TransactionRecord record)
        {
            _journal.Add(record);

            if (_journal.Count > MaxJournalEntries)
                _journal.RemoveRange(0, _journal.Count - MaxJournalEntries);
        }
    }
}
