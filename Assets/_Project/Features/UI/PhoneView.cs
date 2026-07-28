using System.Collections.Generic;
using QonaevLife.Language;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace QonaevLife.UI
{
    /// <summary>
    /// Внутриигровой телефон (FR-091). Каждый обязательный раздел доступен
    /// вкладкой, без выхода во внешние меню. Раздел «Карта» рисуется
    /// отдельным компонентом (FR-092).
    /// </summary>
    public sealed class PhoneView : ScreenView
    {
        [Header("Вкладки")]
        [SerializeField] private Transform tabContainer;
        [SerializeField] [Tooltip("Шаблон кнопки вкладки. Отключён на сцене.")]
        private Button tabTemplate;

        [Header("Содержимое")]
        [SerializeField] private TMP_Text headerLabel;
        [SerializeField] private TMP_Text bodyLabel;
        [SerializeField] private Button backButton;

        [Header("Карта")]
        [SerializeField] [Tooltip("Панель карты — активна только на вкладке «Карта».")]
        private GameObject mapPanel;

        [SerializeField] private MapView mapView;

        private readonly List<Button> _tabButtons = new();

        private static readonly PhoneTab[] Tabs =
        {
            PhoneTab.Map, PhoneTab.Tasks, PhoneTab.Dictionary,
            PhoneTab.Finance, PhoneTab.Contacts, PhoneTab.Transport
        };

        private PhoneModel _model;

        public override UiScreen Screen => UiScreen.Phone;

        public void BindPhone(PhoneModel model)
        {
            _model = model;

            if (backButton != null)
            {
                backButton.onClick.RemoveAllListeners();
                backButton.onClick.AddListener(CloseSelf);
            }

            BuildTabs();
            Refresh();
        }

        protected override void OnShown() => Refresh();

        private void BuildTabs()
        {
            if (tabTemplate == null || tabContainer == null)
                return;

            while (_tabButtons.Count < Tabs.Length)
            {
                var instance = Instantiate(tabTemplate, tabContainer);
                instance.gameObject.SetActive(true);
                _tabButtons.Add(instance);
            }

            for (var i = 0; i < _tabButtons.Count; i++)
            {
                var tab = Tabs[i];
                var button = _tabButtons[i];

                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    _model.SetTab(tab);
                    Refresh();
                });
            }
        }

        public void Refresh()
        {
            if (_model == null || Text == null)
                return;

            var active = _model.ActiveTab;

            for (var i = 0; i < _tabButtons.Count && i < Tabs.Length; i++)
            {
                var label = _tabButtons[i].GetComponentInChildren<TMP_Text>();
                if (label == null)
                    continue;

                label.text = Text.Resolve($"phone.tab.{Tabs[i]}");

                // Активная вкладка помечается символом, а не только цветом:
                // информация не должна передаваться одним цветом (п. 9 ТЗ).
                if (Tabs[i] == active)
                {
                    label.text = $"▸ {label.text}";
                    label.color = new Color(0.6f, 0.85f, 1f);
                }
                else
                {
                    label.color = new Color(0.8f, 0.8f, 0.85f);
                }
            }

            if (headerLabel != null)
                headerLabel.text = Text.Resolve($"phone.tab.{active}");

            var isMap = active == PhoneTab.Map;

            if (mapPanel != null)
                mapPanel.SetActive(isMap);

            if (bodyLabel != null)
            {
                bodyLabel.gameObject.SetActive(!isMap);
                bodyLabel.text = isMap ? string.Empty : BuildBody(active);
            }

            if (isMap && mapView != null)
                mapView.Refresh();
        }

        private string BuildBody(PhoneTab tab) => tab switch
        {
            PhoneTab.Tasks => BuildTasks(),
            PhoneTab.Dictionary => BuildDictionary(),
            PhoneTab.Finance => BuildFinance(),
            PhoneTab.Contacts => BuildContacts(),
            PhoneTab.Transport => Text.Resolve("phone.transport_empty"),
            _ => string.Empty
        };

        private string BuildTasks()
        {
            var tasks = _model.GetTasks();
            if (tasks.Count == 0)
                return Text.Resolve("phone.tasks_empty");

            var builder = new System.Text.StringBuilder();
            foreach (var task in tasks)
            {
                builder.AppendLine($"<b>{Text.Resolve(task.TitleKey)}</b>");
                builder.AppendLine(Text.Resolve(task.ObjectiveKey));

                if (task.MinutesRemaining.HasValue)
                {
                    builder.AppendLine(
                        $"{Text.Resolve("phone.time_left")}: " +
                        $"{(int)task.MinutesRemaining.Value} {Text.Resolve("common.minutes")}");
                }

                builder.AppendLine();
            }

            return builder.ToString();
        }

        private string BuildDictionary()
        {
            var entries = _model.GetDictionary();
            if (entries.Count == 0)
                return Text.Resolve("phone.dictionary_empty");

            var builder = new System.Text.StringBuilder();
            builder.AppendLine(
                $"{Text.Resolve("phone.language_level")}: {_model.LanguageLevel}");
            builder.AppendLine();

            foreach (var entry in entries)
            {
                var stage = Text.Resolve($"mastery.{entry.Stage}");
                builder.Append($"<b>{entry.Kazakh}</b> — {entry.Russian}");

                if (entry.HasTranscription)
                    builder.Append($"  [{entry.Transcription}]");

                builder.AppendLine($"   ({stage})");
            }

            return builder.ToString();
        }

        private string BuildFinance()
        {
            var builder = new System.Text.StringBuilder();
            builder.AppendLine($"<b>{Text.Resolve("hud.money")}: {_model.Balance} ₸</b>");
            builder.AppendLine();

            var entries = _model.GetFinance();
            if (entries.Count == 0)
            {
                builder.AppendLine(Text.Resolve("phone.finance_empty"));
                return builder.ToString();
            }

            foreach (var entry in entries)
            {
                // Знак дублирует смысл операции: доход и расход различимы
                // без цвета.
                var sign = entry.IsIncome ? "+" : "−";
                var amount = System.Math.Abs(entry.Amount);

                builder.AppendLine(
                    $"{Text.Resolve("hud.day")} {entry.GameDay}   " +
                    $"{sign}{amount} ₸   {Text.Resolve(entry.ReasonKey)}");
            }

            return builder.ToString();
        }

        private string BuildContacts()
        {
            var builder = new System.Text.StringBuilder();
            var any = false;

            foreach (var npc in _model.GetContacts())
            {
                any = true;
                builder.AppendLine(
                    $"<b>{Text.Resolve(npc.DisplayNameKey)}</b> — " +
                    $"{Text.Resolve(npc.ProfessionKey)}");
            }

            return any ? builder.ToString() : Text.Resolve("phone.contacts_empty");
        }

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void Configure(Transform tabs, Button tabButtonTemplate, TMP_Text header,
            TMP_Text body, Button back, GameObject map, MapView view)
        {
            tabContainer = tabs;
            tabTemplate = tabButtonTemplate;
            headerLabel = header;
            bodyLabel = body;
            backButton = back;
            mapPanel = map;
            mapView = view;
        }
    }
}
