using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Content
{
    /// <summary>
    /// Реестр контента вертикального срезa. Собирает определения в один ассет,
    /// чтобы сервисы получали их по стабильному ID, а редакторная проверка
    /// могла пройти по всем ссылкам сразу (п. 6 ТЗ).
    /// </summary>
    [CreateAssetMenu(
        fileName = "ContentDatabase",
        menuName = "Qonaev Life/База контента",
        order = 0)]
    public sealed class ContentDatabase : ScriptableObject
    {
        [SerializeField] private List<WordDefinition> words = new();
        [SerializeField] private List<ItemDefinition> items = new();
        [SerializeField] private List<JobDefinition> jobs = new();
        [SerializeField] private List<NpcDefinition> npcs = new();
        [SerializeField] private List<DialogueNodeDefinition> dialogueNodes = new();
        [SerializeField] private List<LocationDefinition> locations = new();

        private readonly Dictionary<string, WordDefinition> _wordsById = new();
        private readonly Dictionary<string, ItemDefinition> _itemsById = new();
        private readonly Dictionary<string, JobDefinition> _jobsById = new();
        private readonly Dictionary<string, NpcDefinition> _npcsById = new();
        private readonly Dictionary<string, DialogueNodeDefinition> _dialogueById = new();
        private readonly Dictionary<string, LocationDefinition> _locationsById = new();

        private bool _indexed;

        public IReadOnlyList<WordDefinition> Words => words;
        public IReadOnlyList<ItemDefinition> Items => items;
        public IReadOnlyList<JobDefinition> Jobs => jobs;
        public IReadOnlyList<NpcDefinition> Npcs => npcs;
        public IReadOnlyList<DialogueNodeDefinition> DialogueNodes => dialogueNodes;
        public IReadOnlyList<LocationDefinition> Locations => locations;

        /// <summary>Строит индексы по ID. Вызывается лениво и повторно безопасно.</summary>
        public void BuildIndex()
        {
            if (_indexed)
                return;

            Index(words, _wordsById);
            Index(items, _itemsById);
            Index(jobs, _jobsById);
            Index(npcs, _npcsById);
            Index(dialogueNodes, _dialogueById);
            Index(locations, _locationsById);

            _indexed = true;
        }

        public bool TryGetWord(string id, out WordDefinition definition)
            => TryGet(_wordsById, id, out definition);

        public bool TryGetItem(string id, out ItemDefinition definition)
            => TryGet(_itemsById, id, out definition);

        public bool TryGetJob(string id, out JobDefinition definition)
            => TryGet(_jobsById, id, out definition);

        public bool TryGetNpc(string id, out NpcDefinition definition)
            => TryGet(_npcsById, id, out definition);

        public bool TryGetDialogueNode(string id, out DialogueNodeDefinition definition)
            => TryGet(_dialogueById, id, out definition);

        public bool TryGetLocation(string id, out LocationDefinition definition)
            => TryGet(_locationsById, id, out definition);

        public bool HasLocation(string id) => TryGetLocation(id, out _);

        private bool TryGet<T>(Dictionary<string, T> map, string id, out T definition)
            where T : class
        {
            BuildIndex();

            if (!string.IsNullOrWhiteSpace(id) && map.TryGetValue(id, out definition))
                return true;

            definition = null;
            return false;
        }

        private static void Index<T>(List<T> source, Dictionary<string, T> target)
            where T : ContentDefinition
        {
            target.Clear();

            foreach (var definition in source)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                // Дубликаты ID ловит редакторная проверка; в рантайме берём первый.
                target.TryAdd(definition.Id, definition);
            }
        }

        /// <summary>
        /// Полная проверка базы: пустые ID, дубликаты, несуществующие ссылки
        /// и минимальный объём контента вертикального среза (п. 6 ТЗ, FR-045).
        /// </summary>
        public void ValidateAll(List<string> errors)
        {
            _indexed = false;
            BuildIndex();

            ValidateCollection(words, errors);
            ValidateCollection(items, errors);
            ValidateCollection(jobs, errors);
            ValidateCollection(npcs, errors);
            ValidateCollection(dialogueNodes, errors);
            ValidateCollection(locations, errors);

            ValidateDuplicateIds(words, errors);
            ValidateDuplicateIds(items, errors);
            ValidateDuplicateIds(jobs, errors);
            ValidateDuplicateIds(npcs, errors);
            ValidateDuplicateIds(dialogueNodes, errors);
            ValidateDuplicateIds(locations, errors);

            ValidateCrossReferences(errors);
            ValidateVerticalSliceVolume(errors);
        }

        private static void ValidateCollection<T>(List<T> source, List<string> errors)
            where T : ContentDefinition
        {
            for (var i = 0; i < source.Count; i++)
            {
                if (source[i] == null)
                {
                    errors.Add($"{typeof(T).Name}: пустая ссылка в списке, индекс {i}.");
                    continue;
                }

                source[i].Validate(errors);
            }
        }

        private static void ValidateDuplicateIds<T>(List<T> source, List<string> errors)
            where T : ContentDefinition
        {
            var seen = new HashSet<string>();

            foreach (var definition in source)
            {
                if (definition == null || string.IsNullOrWhiteSpace(definition.Id))
                    continue;

                if (!seen.Add(definition.Id))
                    errors.Add($"{typeof(T).Name}: дублирующийся Id '{definition.Id}'.");
            }
        }

        private void ValidateCrossReferences(List<string> errors)
        {
            // NPC: дом, работа и корневой диалог должны существовать.
            foreach (var npc in npcs)
            {
                if (npc == null)
                    continue;

                RequireLocation(npc.HomeLocationId, $"NPC '{npc.Id}' (дом)", errors);

                if (!string.IsNullOrWhiteSpace(npc.WorkLocationId))
                    RequireLocation(npc.WorkLocationId, $"NPC '{npc.Id}' (работа)", errors);

                if (!string.IsNullOrWhiteSpace(npc.RootDialogueId)
                    && !TryGetDialogueNode(npc.RootDialogueId, out _))
                {
                    errors.Add($"NPC '{npc.Id}': корневой диалог " +
                               $"'{npc.RootDialogueId}' не найден.");
                }

                foreach (var entry in npc.Schedule)
                    RequireLocation(entry.locationId,
                        $"NPC '{npc.Id}', расписание '{entry.entryId}'", errors);
            }

            // Работа: локация выдачи и локации этапов.
            foreach (var job in jobs)
            {
                if (job == null)
                    continue;

                RequireLocation(job.HubLocationId, $"Работа '{job.Id}' (выдача смены)", errors);

                foreach (var stage in job.Stages)
                    RequireLocation(stage.locationId,
                        $"Работа '{job.Id}', этап '{stage.stageId}'", errors);
            }

            // Диалоги: говорящий, переходы и словарные теги.
            foreach (var node in dialogueNodes)
            {
                if (node == null)
                    continue;

                if (!string.IsNullOrWhiteSpace(node.SpeakerNpcId)
                    && !TryGetNpc(node.SpeakerNpcId, out _))
                {
                    errors.Add($"Диалог '{node.Id}': говорящий " +
                               $"'{node.SpeakerNpcId}' не найден.");
                }

                foreach (var wordId in node.Line.wordIds ?? new List<string>())
                    RequireWord(wordId, $"Диалог '{node.Id}'", errors);

                foreach (var choice in node.Choices)
                {
                    if (!string.IsNullOrWhiteSpace(choice.nextNodeId)
                        && !TryGetDialogueNode(choice.nextNodeId, out _))
                    {
                        errors.Add($"Диалог '{node.Id}', вариант '{choice.choiceId}': " +
                                   $"переход на несуществующий узел '{choice.nextNodeId}'.");
                    }

                    foreach (var wordId in choice.line.wordIds ?? new List<string>())
                        RequireWord(wordId,
                            $"Диалог '{node.Id}', вариант '{choice.choiceId}'", errors);
                }
            }
        }

        /// <summary>Минимальный объём контента вертикального среза (FR-045).</summary>
        private void ValidateVerticalSliceVolume(List<string> errors)
        {
            const int requiredWords = 100;
            const int minNpcs = 20;

            if (words.Count < requiredWords)
            {
                errors.Add($"Объём контента: требуется не менее {requiredWords} слов " +
                           $"(FR-045), сейчас {words.Count}.");
            }

            if (npcs.Count < minNpcs)
            {
                errors.Add($"Объём контента: требуется не менее {minNpcs} именных NPC " +
                           $"(п. 2.1 ТЗ), сейчас {npcs.Count}.");
            }

            if (jobs.Count < 1)
                errors.Add("Объём контента: нужна хотя бы одна профессия (FR-070).");
        }

        private void RequireLocation(string locationId, string context, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(locationId))
                return;

            if (!TryGetLocation(locationId, out _))
                errors.Add($"{context}: локация '{locationId}' не найдена.");
        }

        private void RequireWord(string wordId, string context, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(wordId))
                return;

            if (!TryGetWord(wordId, out _))
                errors.Add($"{context}: слово '{wordId}' не найдено в словаре.");
        }
    }
}
