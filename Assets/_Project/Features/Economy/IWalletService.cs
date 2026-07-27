using System;
using System.Collections.Generic;
using QonaevLife.Core;

namespace QonaevLife.Economy
{
    /// <summary>Причина денежного движения. Каждая транзакция обязана её указывать (п. 5.6 ТЗ).</summary>
    public enum TransactionReason
    {
        Unknown = 0,
        StartingCapital,
        JobPayout,
        QuestReward,
        Purchase,
        Sale,
        TaxiFare,
        Fuel,
        Rent,
        PropertyPurchase,
        Penalty,
        Refund,
        Discount
    }

    /// <summary>Итог попытки провести транзакцию.</summary>
    public enum TransactionStatus
    {
        Applied = 0,

        /// <summary>Недостаточно средств — баланс не изменён (AT-006).</summary>
        InsufficientFunds = 1,

        /// <summary>Нулевая сумма или некорректные параметры.</summary>
        InvalidAmount = 2,

        /// <summary>Знак суммы не соответствует причине: расход с плюсом или доход с минусом (п. 10 ТЗ).</summary>
        SignMismatchesReason = 3
    }

    /// <summary>Запрос на изменение баланса. Единственный путь к деньгам игрока.</summary>
    public readonly struct TransactionRequest
    {
        public TransactionRequest(long amount, TransactionReason reason, string sourceId)
        {
            Amount = amount;
            Reason = reason;
            SourceId = sourceId;
        }

        /// <summary>Положительная сумма — доход, отрицательная — расход.</summary>
        public long Amount { get; }

        public TransactionReason Reason { get; }

        /// <summary>ID инициатора: смены, задания, магазина или предмета (FR-075).</summary>
        public string SourceId { get; }
    }

    /// <summary>Проведённая транзакция в журнале.</summary>
    public readonly struct TransactionRecord
    {
        public TransactionRecord(string transactionId, long amount, long balanceAfter,
            TransactionReason reason, string sourceId, int gameDay, double gameMinutesOfDay)
        {
            TransactionId = transactionId;
            Amount = amount;
            BalanceAfter = balanceAfter;
            Reason = reason;
            SourceId = sourceId;
            GameDay = gameDay;
            GameMinutesOfDay = gameMinutesOfDay;
        }

        public string TransactionId { get; }
        public long Amount { get; }
        public long BalanceAfter { get; }
        public TransactionReason Reason { get; }
        public string SourceId { get; }
        public int GameDay { get; }
        public double GameMinutesOfDay { get; }
    }

    /// <summary>Результат транзакции.</summary>
    public readonly struct TransactionResult
    {
        private TransactionResult(TransactionStatus status, long balanceAfter, TransactionRecord record)
        {
            Status = status;
            BalanceAfter = balanceAfter;
            Record = record;
        }

        public TransactionStatus Status { get; }
        public long BalanceAfter { get; }
        public TransactionRecord Record { get; }

        public bool Applied => Status == TransactionStatus.Applied;

        public static TransactionResult Ok(long balanceAfter, TransactionRecord record)
            => new(TransactionStatus.Applied, balanceAfter, record);

        public static TransactionResult Rejected(TransactionStatus status, long balanceUnchanged)
            => new(status, balanceUnchanged, default);
    }

    /// <summary>Событие изменения баланса для UI и квестов.</summary>
    public readonly struct BalanceChangedEvent : IGameEvent
    {
        public BalanceChangedEvent(long previousBalance, long newBalance, TransactionRecord record)
        {
            PreviousBalance = previousBalance;
            NewBalance = newBalance;
            Record = record;
        }

        public long PreviousBalance { get; }
        public long NewBalance { get; }
        public TransactionRecord Record { get; }
    }

    /// <summary>
    /// Единый кошелёк игрока (FR-050, FR-052). Прямая правка баланса из UI,
    /// квестов или мини-игр невозможна — только через <see cref="TryApply"/>.
    /// </summary>
    public interface IWalletService
    {
        long Balance { get; }

        IReadOnlyList<TransactionRecord> RecentTransactions { get; }

        /// <summary>Хватает ли средств на расход указанной величины.</summary>
        bool CanAfford(long cost);

        /// <summary>
        /// Атомарно проводит транзакцию. При отказе баланс и журнал не меняются.
        /// </summary>
        TransactionResult TryApply(TransactionRequest request);
    }
}
