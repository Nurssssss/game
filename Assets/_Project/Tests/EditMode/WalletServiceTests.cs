using NUnit.Framework;
using QonaevLife.Core;
using QonaevLife.Economy;

namespace QonaevLife.Tests.EditMode
{
    /// <summary>Экономика и атомарность транзакций (FR-050, FR-052, FR-075, AT-006).</summary>
    [TestFixture]
    public sealed class WalletServiceTests
    {
        private EventBus _eventBus;
        private GameClock _clock;
        private WalletService _wallet;

        [SetUp]
        public void SetUp()
        {
            _eventBus = new EventBus();
            _clock = new GameClock(GameClockSettings.Default);
            _wallet = new WalletService(_clock, _eventBus);
        }

        private TransactionResult Deposit(long amount, string source = "test_income")
            => _wallet.TryApply(new TransactionRequest(
                amount, TransactionReason.JobPayout, source));

        [Test]
        public void NewWallet_StartsAtZero()
        {
            Assert.That(_wallet.Balance, Is.Zero);
            Assert.That(_wallet.RecentTransactions, Is.Empty);
        }

        [Test]
        public void Payout_IncreasesBalanceAndLogsTransaction()
        {
            var result = Deposit(1500);

            Assert.That(result.Applied, Is.True);
            Assert.That(_wallet.Balance, Is.EqualTo(1500));
            Assert.That(_wallet.RecentTransactions, Has.Count.EqualTo(1));
            Assert.That(_wallet.RecentTransactions[0].Reason,
                Is.EqualTo(TransactionReason.JobPayout));
            Assert.That(_wallet.RecentTransactions[0].SourceId, Is.EqualTo("test_income"));
        }

        [Test]
        public void Purchase_WithSufficientFunds_DeductsBalance()
        {
            Deposit(1000);

            var result = _wallet.TryApply(new TransactionRequest(
                -400, TransactionReason.Purchase, "shop_bread"));

            Assert.That(result.Applied, Is.True);
            Assert.That(_wallet.Balance, Is.EqualTo(600));
        }

        /// <summary>AT-006: при недостатке денег баланс и журнал не меняются.</summary>
        [Test]
        public void Purchase_WithInsufficientFunds_LeavesStateUntouched()
        {
            Deposit(100);
            var journalCountBefore = _wallet.RecentTransactions.Count;

            var result = _wallet.TryApply(new TransactionRequest(
                -500, TransactionReason.Purchase, "shop_expensive"));

            Assert.That(result.Applied, Is.False);
            Assert.That(result.Status, Is.EqualTo(TransactionStatus.InsufficientFunds));
            Assert.That(_wallet.Balance, Is.EqualTo(100), "Баланс не должен измениться.");
            Assert.That(_wallet.RecentTransactions,
                Has.Count.EqualTo(journalCountBefore),
                "Отклонённая транзакция не должна попадать в журнал.");
        }

        [Test]
        public void ExactBalance_IsAffordable()
        {
            Deposit(250);

            var result = _wallet.TryApply(new TransactionRequest(
                -250, TransactionReason.Purchase, "shop_exact"));

            Assert.That(result.Applied, Is.True);
            Assert.That(_wallet.Balance, Is.Zero);
        }

        [Test]
        public void ZeroAmount_IsRejected()
        {
            var result = _wallet.TryApply(new TransactionRequest(
                0, TransactionReason.Purchase, "shop_free"));

            Assert.That(result.Status, Is.EqualTo(TransactionStatus.InvalidAmount));
        }

        /// <summary>FR-075: каждая транзакция обязана иметь источник.</summary>
        [Test]
        public void MissingSourceId_IsRejected()
        {
            var result = _wallet.TryApply(new TransactionRequest(
                500, TransactionReason.JobPayout, sourceId: "  "));

            Assert.That(result.Status, Is.EqualTo(TransactionStatus.InvalidAmount));
            Assert.That(_wallet.Balance, Is.Zero);
        }

        /// <summary>П. 10 ТЗ: расходная причина не может приносить деньги.</summary>
        [Test]
        public void PositiveAmount_WithExpenseReason_IsRejected()
        {
            var result = _wallet.TryApply(new TransactionRequest(
                500, TransactionReason.Purchase, "shop_bug"));

            Assert.That(result.Status, Is.EqualTo(TransactionStatus.SignMismatchesReason));
            Assert.That(_wallet.Balance, Is.Zero);
        }

        [Test]
        public void NegativeAmount_WithIncomeReason_IsRejected()
        {
            Deposit(1000);

            var result = _wallet.TryApply(new TransactionRequest(
                -300, TransactionReason.JobPayout, "job_bug"));

            Assert.That(result.Status, Is.EqualTo(TransactionStatus.SignMismatchesReason));
            Assert.That(_wallet.Balance, Is.EqualTo(1000));
        }

        [Test]
        public void Refund_ReturnsMoneyToPlayer()
        {
            Deposit(1000);
            _wallet.TryApply(new TransactionRequest(-400, TransactionReason.Purchase, "shop_item"));

            var result = _wallet.TryApply(new TransactionRequest(
                400, TransactionReason.Refund, "shop_item"));

            Assert.That(result.Applied, Is.True);
            Assert.That(_wallet.Balance, Is.EqualTo(1000));
        }

        [Test]
        public void UnknownReason_IsRejected()
        {
            var result = _wallet.TryApply(new TransactionRequest(
                100, TransactionReason.Unknown, "mystery"));

            Assert.That(result.Applied, Is.False);
        }

        [Test]
        public void BalanceChangedEvent_CarriesPreviousAndNewBalance()
        {
            Deposit(800);

            BalanceChangedEvent captured = default;
            var received = 0;
            _eventBus.Subscribe<BalanceChangedEvent>(e =>
            {
                captured = e;
                received++;
            });

            _wallet.TryApply(new TransactionRequest(-300, TransactionReason.TaxiFare, "taxi_1"));

            Assert.That(received, Is.EqualTo(1));
            Assert.That(captured.PreviousBalance, Is.EqualTo(800));
            Assert.That(captured.NewBalance, Is.EqualTo(500));
            Assert.That(captured.Record.Reason, Is.EqualTo(TransactionReason.TaxiFare));
        }

        [Test]
        public void RejectedTransaction_DoesNotPublishEvent()
        {
            var received = 0;
            _eventBus.Subscribe<BalanceChangedEvent>(_ => received++);

            _wallet.TryApply(new TransactionRequest(-999, TransactionReason.Purchase, "too_pricey"));

            Assert.That(received, Is.Zero);
        }

        /// <summary>П. 7 ТЗ: журнал в сохранении ограничен по размеру.</summary>
        [Test]
        public void Journal_IsCappedAtMaxEntries()
        {
            Deposit(1_000_000, "seed");

            for (var i = 0; i < WalletService.MaxJournalEntries + 20; i++)
                _wallet.TryApply(new TransactionRequest(-1, TransactionReason.Purchase, $"item_{i}"));

            Assert.That(_wallet.RecentTransactions,
                Has.Count.EqualTo(WalletService.MaxJournalEntries));
        }

        [Test]
        public void CaptureAndRestore_PreservesBalance()
        {
            Deposit(2500);
            _wallet.TryApply(new TransactionRequest(-500, TransactionReason.Fuel, "gas_station"));

            var data = new EconomySaveData();
            _wallet.CaptureState(data);

            var restored = new WalletService(_clock, _eventBus);
            restored.RestoreState(data);

            Assert.That(restored.Balance, Is.EqualTo(2000));
            Assert.That(restored.RecentTransactions, Has.Count.EqualTo(2));
        }

        [Test]
        public void CanAfford_ReflectsBalance()
        {
            Deposit(300);

            Assert.That(_wallet.CanAfford(300), Is.True);
            Assert.That(_wallet.CanAfford(301), Is.False);
        }
    }
}
