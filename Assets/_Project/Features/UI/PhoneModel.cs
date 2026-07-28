using System;
using System.Collections.Generic;
using QonaevLife.Content;
using QonaevLife.Economy;
using QonaevLife.Jobs;
using QonaevLife.Language;

namespace QonaevLife.UI
{
    /// <summary>
    /// Разделы внутриигрового телефона (FR-091). Каждый обязательный раздел
    /// доступен без выхода во внешние меню.
    /// </summary>
    public enum PhoneTab
    {
        Map = 0,
        Tasks = 1,
        Dictionary = 2,
        Finance = 3,
        Contacts = 4,
        Transport = 5
    }

    /// <summary>Строка словаря для показа в телефоне.</summary>
    public readonly struct DictionaryEntry
    {
        public DictionaryEntry(string wordId, string kazakh, string russian, string transcription,
            MasteryStage stage)
        {
            WordId = wordId;
            Kazakh = kazakh;
            Russian = russian;
            Transcription = transcription;
            Stage = stage;
        }

        public string WordId { get; }
        public string Kazakh { get; }
        public string Russian { get; }
        public string Transcription { get; }
        public MasteryStage Stage { get; }

        public bool HasTranscription => !string.IsNullOrWhiteSpace(Transcription);
    }

    /// <summary>Запись журнала финансов.</summary>
    public readonly struct FinanceEntry
    {
        public FinanceEntry(long amount, string reasonKey, string sourceId, int gameDay)
        {
            Amount = amount;
            ReasonKey = reasonKey;
            SourceId = sourceId;
            GameDay = gameDay;
        }

        public long Amount { get; }
        public string ReasonKey { get; }
        public string SourceId { get; }
        public int GameDay { get; }

        public bool IsIncome => Amount > 0;
    }

    /// <summary>Активная задача игрока.</summary>
    public readonly struct TaskEntry
    {
        public TaskEntry(string titleKey, string objectiveKey, string targetLocationId,
            double? minutesRemaining)
        {
            TitleKey = titleKey;
            ObjectiveKey = objectiveKey;
            TargetLocationId = targetLocationId;
            MinutesRemaining = minutesRemaining;
        }

        public string TitleKey { get; }
        public string ObjectiveKey { get; }
        public string TargetLocationId { get; }
        public double? MinutesRemaining { get; }
    }

    /// <summary>
    /// Данные телефона (FR-091). Собирает содержимое разделов из сервисов,
    /// не обращаясь к сцене, поэтому проверяется модульными тестами.
    /// </summary>
    public sealed class PhoneModel
    {
        private readonly ContentDatabase _content;
        private readonly IWalletService _wallet;
        private readonly JobShiftService _jobs;
        private readonly ILanguageProgressService _language;

        private readonly List<DictionaryEntry> _dictionary = new();
        private readonly List<FinanceEntry> _finance = new();
        private readonly List<TaskEntry> _tasks = new();

        public PhoneModel(ContentDatabase content, IWalletService wallet, JobShiftService jobs,
            ILanguageProgressService language)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _wallet = wallet ?? throw new ArgumentNullException(nameof(wallet));
            _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
            _language = language ?? throw new ArgumentNullException(nameof(language));
        }

        public PhoneTab ActiveTab { get; private set; } = PhoneTab.Map;

        public void SetTab(PhoneTab tab) => ActiveTab = tab;

        /// <summary>Личный словарь игрока, отсортированный по этапу освоения.</summary>
        public IReadOnlyList<DictionaryEntry> GetDictionary()
        {
            _dictionary.Clear();

            foreach (var learned in _language.LearnedWords)
            {
                if (!_content.TryGetWord(learned.WordId, out var definition))
                    continue;

                _dictionary.Add(new DictionaryEntry(
                    learned.WordId,
                    definition.Kazakh,
                    definition.Russian,
                    definition.Transcription,
                    learned.Stage));
            }

            // Неосвоенные слова выше: игрок открывает словарь, чтобы повторить
            // то, что ещё не выучил.
            _dictionary.Sort((a, b) => a.Stage.CompareTo(b.Stage));

            return _dictionary;
        }

        /// <summary>Журнал финансов, новые записи первыми (FR-050).</summary>
        public IReadOnlyList<FinanceEntry> GetFinance()
        {
            _finance.Clear();

            var records = _wallet.RecentTransactions;
            for (var i = records.Count - 1; i >= 0; i--)
            {
                var record = records[i];
                _finance.Add(new FinanceEntry(
                    record.Amount,
                    $"reason.{record.Reason}",
                    record.SourceId,
                    record.GameDay));
            }

            return _finance;
        }

        public long Balance => _wallet.Balance;

        public int LanguageLevel => _language.Level;

        /// <summary>Активные задачи. Пока это только текущая смена (FR-072).</summary>
        public IReadOnlyList<TaskEntry> GetTasks()
        {
            _tasks.Clear();

            var stage = _jobs.CurrentStage;
            if (stage == null)
                return _tasks;

            _tasks.Add(new TaskEntry(
                titleKey: $"job.{_jobs.ActiveJobId}",
                objectiveKey: stage.Value.objectiveKey,
                targetLocationId: stage.Value.locationId,
                minutesRemaining: _jobs.MinutesRemaining));

            return _tasks;
        }

        /// <summary>Именные NPC, с которыми игрок уже знаком.</summary>
        public IEnumerable<NpcDefinition> GetContacts()
        {
            foreach (var npc in _content.Npcs)
            {
                if (npc != null && !string.IsNullOrWhiteSpace(npc.RootDialogueId))
                    yield return npc;
            }
        }
    }
}
