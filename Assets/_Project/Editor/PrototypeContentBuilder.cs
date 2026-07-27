using System.Collections.Generic;
using System.IO;
using QonaevLife.Bootstrap;
using QonaevLife.Content;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Создаёт ассеты контента прототипа: настройки сессии, локации, слова,
    /// NPC с расписанием, двуязычный диалог и курьерскую смену.
    /// Ассеты строит сам Unity, поэтому ссылки между ними всегда валидны.
    /// Повторный запуск переиспользует существующие файлы и не плодит копии.
    /// </summary>
    public static class PrototypeContentBuilder
    {
        private const string ContentRoot = "Assets/_Project/Content";
        private const string DefinitionsRoot = ContentRoot + "/Definitions";

        public const string ApartmentLocationId = "loc_apartment_01";
        public const string CourierHubLocationId = "loc_courier_hub";
        public const string ShopLocationId = "loc_shop_01";
        public const string CafeLocationId = "loc_cafe_01";
        public const string CourierJobId = "job_courier";
        public const string NpcAidanaId = "npc_aidana";
        public const string NpcDispatcherId = "npc_dispatcher";

        [MenuItem("Qonaev Life/Создать контент прототипа", priority = 10)]
        public static void BuildMenuCommand()
        {
            var database = Build();

            Selection.activeObject = database;
            EditorGUIUtility.PingObject(database);

            Debug.Log("[Прототип] Контент создан. " +
                      "Проверить целостность: Qonaev Life → Проверить контент.");
        }

        /// <summary>Создаёт или обновляет весь контент прототипа и возвращает базу.</summary>
        public static ContentDatabase Build()
        {
            EnsureFolders();

            var words = BuildWords();
            var locations = BuildLocations();
            var items = BuildItems();
            var dialogueNodes = BuildDialogue();
            var npcs = BuildNpcs();
            var jobs = BuildJobs();

            var database = LoadOrCreate<ContentDatabase>($"{ContentRoot}/ContentDatabase.asset");
            var databaseObject = new SerializedObject(database);
            AssignList(databaseObject, "words", words);
            AssignList(databaseObject, "items", items);
            AssignList(databaseObject, "jobs", jobs);
            AssignList(databaseObject, "npcs", npcs);
            AssignList(databaseObject, "dialogueNodes", dialogueNodes);
            AssignList(databaseObject, "locations", locations);
            databaseObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(database);

            EnsureSessionConfig();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            // Refresh переимпортирует ассеты, поэтому созданный до него экземпляр
            // может стать устаревшим. Перечитываем базу по пути: только такая
            // ссылка корректно сериализуется в сцену.
            return AssetDatabase.LoadAssetAtPath<ContentDatabase>(
                $"{ContentRoot}/ContentDatabase.asset");
        }

        /// <summary>Настройки сессии с балансом прототипа.</summary>
        public static GameSessionConfig EnsureSessionConfig()
        {
            var path = $"{ContentRoot}/Balance/GameSessionConfig.asset";
            var config = LoadOrCreate<GameSessionConfig>(path);

            var so = new SerializedObject(config);

            // Ускоренное время: сутки проходят за 24 реальные минуты, чтобы
            // фазы суток и расписания NPC можно было проверить за один сеанс.
            so.FindProperty("clock").FindPropertyRelative("minutesPerRealSecond").floatValue = 60f;
            so.FindProperty("startingCapital").longValue = 5000;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(config);

            AssetDatabase.SaveAssets();

            // Как и с базой контента: возвращаем ссылку, перечитанную по пути.
            return AssetDatabase.LoadAssetAtPath<GameSessionConfig>(path);
        }

        /// <summary>
        /// Загружает базу контента по пути. Нужен отдельным вызовом, потому что
        /// создание новой сцены обнуляет ранее полученные ссылки на ассеты.
        /// </summary>
        public static ContentDatabase LoadDatabase()
            => AssetDatabase.LoadAssetAtPath<ContentDatabase>(
                $"{ContentRoot}/ContentDatabase.asset");

        /// <summary>Загружает настройки сессии по пути. См. <see cref="LoadDatabase"/>.</summary>
        public static GameSessionConfig LoadSessionConfig()
            => AssetDatabase.LoadAssetAtPath<GameSessionConfig>(
                $"{ContentRoot}/Balance/GameSessionConfig.asset");

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/_Project", "Content");
            EnsureFolder(ContentRoot, "Definitions");
            EnsureFolder(ContentRoot, "Balance");
            EnsureFolder(DefinitionsRoot, "Locations");
            EnsureFolder(DefinitionsRoot, "Words");
            EnsureFolder(DefinitionsRoot, "Items");
            EnsureFolder(DefinitionsRoot, "Npcs");
            EnsureFolder(DefinitionsRoot, "Dialogues");
            EnsureFolder(DefinitionsRoot, "Jobs");
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = $"{parent}/{name}";
            if (!AssetDatabase.IsValidFolder(path))
                AssetDatabase.CreateFolder(parent, name);
        }

        // ------------------------------------------------------------------
        // Словарь
        // ------------------------------------------------------------------

        /// <summary>
        /// Базовый словарь прототипа. ТЗ (FR-045) требует 100 слов для
        /// вертикального среза — здесь их 15 для проверки механики, поэтому
        /// проверка контента будет сообщать о недоборе до стадии P3.
        /// Казахский текст подлежит вычитке редактором-носителем (п. 11 ТЗ).
        /// </summary>
        private static readonly (string Id, string Kazakh, string Russian, string Transcription,
            WordCategory Category)[] WordTable =
            {
                ("word_salem", "Сәлем", "Привет", "sá-lem", WordCategory.Greeting),
                ("word_sau_bolynyz", "Сау болыңыз", "До свидания", "saw bo-lyn-yz",
                    WordCategory.Greeting),
                ("word_rahmet", "Рақмет", "Спасибо", "raq-met", WordCategory.Courtesy),
                ("word_otinemin", "Өтінемін", "Пожалуйста", "ó-ti-ne-min",
                    WordCategory.Courtesy),
                ("word_kesirisiz", "Кешіріңіз", "Извините", "ke-shi-ri-ñiz",
                    WordCategory.Courtesy),
                ("word_iya", "Иә", "Да", "i-á", WordCategory.Everyday),
                ("word_zhok", "Жоқ", "Нет", "joq", WordCategory.Everyday),
                ("word_su", "Су", "Вода", "suw", WordCategory.Food),
                ("word_nan", "Нан", "Хлеб", "nan", WordCategory.Food),
                ("word_kofe", "Кофе", "Кофе", "ko-fe", WordCategory.Food),
                ("word_kala", "Қала", "Город", "qa-la", WordCategory.City),
                ("word_dukento", "Дүкен", "Магазин", "dü-ken", WordCategory.City),
                ("word_zhumys", "Жұмыс", "Работа", "ju-mys", WordCategory.Work),
                ("word_taksi", "Такси", "Такси", "tak-si", WordCategory.Transport),
                ("word_bir", "Бір", "Один", "bir", WordCategory.Numbers)
            };

        private static List<ScriptableObject> BuildWords()
        {
            var result = new List<ScriptableObject>(WordTable.Length);

            foreach (var row in WordTable)
            {
                var path = $"{DefinitionsRoot}/Words/{row.Id}.asset";
                var word = LoadOrCreate<WordDefinition>(path);

                var so = new SerializedObject(word);
                so.FindProperty("id").stringValue = row.Id;
                so.FindProperty("kazakh").stringValue = row.Kazakh;
                so.FindProperty("russian").stringValue = row.Russian;
                so.FindProperty("transcription").stringValue = row.Transcription;
                so.FindProperty("category").enumValueIndex = (int)row.Category;
                so.FindProperty("minLanguageLevel").intValue = 1;
                so.FindProperty("editorNote").stringValue = "Требует вычитки носителем языка.";
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(word);

                result.Add(word);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Локации
        // ------------------------------------------------------------------

        /// <summary>
        /// Точки интереса прототипа. Позиции маркеров совпадают с местами,
        /// куда генератор сцены ставит интерактивные объекты.
        /// </summary>
        internal static readonly (string Id, string NameKey, LocationKind Kind, Vector3 Position,
            bool Interior, bool AlwaysOpen, int OpenHour, int CloseHour)[] LocationTable =
            {
                (ApartmentLocationId, "loc.apartment", LocationKind.Apartment,
                    new Vector3(0f, 0f, 0f), true, true, 0, 23),
                (CourierHubLocationId, "loc.courier_hub", LocationKind.WorkHub,
                    new Vector3(18f, 0f, 6f), false, false, 6, 22),
                (ShopLocationId, "loc.shop", LocationKind.Shop,
                    new Vector3(-14f, 0f, 12f), false, false, 8, 22),
                (CafeLocationId, "loc.cafe", LocationKind.Cafe,
                    new Vector3(8f, 0f, -16f), false, false, 9, 23)
            };

        private static List<ScriptableObject> BuildLocations()
        {
            var result = new List<ScriptableObject>(LocationTable.Length);

            foreach (var row in LocationTable)
            {
                var path = $"{DefinitionsRoot}/Locations/{row.Id}.asset";
                var location = LoadOrCreate<LocationDefinition>(path);

                var so = new SerializedObject(location);
                so.FindProperty("id").stringValue = row.Id;
                so.FindProperty("displayNameKey").stringValue = row.NameKey;
                so.FindProperty("kind").enumValueIndex = (int)row.Kind;
                so.FindProperty("sectorId").stringValue = "sector_center";
                so.FindProperty("markerPosition").vector3Value = row.Position;
                so.FindProperty("isInterior").boolValue = row.Interior;
                so.FindProperty("interiorAddressableKey").stringValue =
                    row.Interior ? $"interior_{row.Id}" : string.Empty;
                so.FindProperty("alwaysOpen").boolValue = row.AlwaysOpen;
                so.FindProperty("openHour").intValue = row.OpenHour;
                so.FindProperty("closeHour").intValue = row.CloseHour;

                // Все точки прототипа открыты сразу: иначе смену нельзя начать.
                so.FindProperty("discoveredFromStart").boolValue = true;
                so.FindProperty("isTaxiDestination").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(location);

                result.Add(location);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Предметы
        // ------------------------------------------------------------------

        private static List<ScriptableObject> BuildItems()
        {
            var rows = new[]
            {
                ("item_bread", "item.bread", ItemCategory.Food, 250L, 50L, true, "hunger", 35f),
                ("item_water", "item.water", ItemCategory.Drink, 120L, 20L, true, "hunger", 10f),
                ("item_coffee", "item.coffee", ItemCategory.Drink, 400L, 80L, true, "energy", 30f)
            };

            var result = new List<ScriptableObject>(rows.Length);

            foreach (var row in rows)
            {
                var path = $"{DefinitionsRoot}/Items/{row.Item1}.asset";
                var item = LoadOrCreate<ItemDefinition>(path);

                var so = new SerializedObject(item);
                so.FindProperty("id").stringValue = row.Item1;
                so.FindProperty("displayNameKey").stringValue = row.Item2;
                so.FindProperty("category").enumValueIndex = (int)row.Item3;
                so.FindProperty("purchasePrice").longValue = row.Item4;
                so.FindProperty("salePrice").longValue = row.Item5;
                so.FindProperty("canBeSold").boolValue = true;
                so.FindProperty("isConsumable").boolValue = row.Item6;
                so.FindProperty("maxStackSize").intValue = 5;

                var effects = so.FindProperty("needEffects");
                effects.arraySize = 1;
                var effect = effects.GetArrayElementAtIndex(0);
                effect.FindPropertyRelative("needId").stringValue = row.Item7;
                effect.FindPropertyRelative("delta").floatValue = row.Item8;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(item);

                result.Add(item);
            }

            return result;
        }

        // ------------------------------------------------------------------
        // Диалог
        // ------------------------------------------------------------------

        private const string GreetingNodeId = "dlg_dispatcher_greeting";
        private const string OfferNodeId = "dlg_dispatcher_offer";
        private const string FarewellNodeId = "dlg_dispatcher_farewell";

        private static List<ScriptableObject> BuildDialogue()
        {
            // Узлы создаются заранее: варианты ссылаются друг на друга по ID.
            var greeting = LoadOrCreate<DialogueNodeDefinition>(
                $"{DefinitionsRoot}/Dialogues/{GreetingNodeId}.asset");
            var offer = LoadOrCreate<DialogueNodeDefinition>(
                $"{DefinitionsRoot}/Dialogues/{OfferNodeId}.asset");
            var farewell = LoadOrCreate<DialogueNodeDefinition>(
                $"{DefinitionsRoot}/Dialogues/{FarewellNodeId}.asset");

            ConfigureNode(greeting, GreetingNodeId, NpcDispatcherId,
                russian: "Привет! Нужна помощь с доставкой?",
                kazakh: "Сәлем! Жеткізуге көмек керек пе?",
                wordIds: new[] { "word_salem", "word_zhumys" },
                languageExperience: 20f,
                choices: new[]
                {
                    new ChoiceSpec
                    {
                        ChoiceId = "choice_yes",
                        Russian = "Да, я готов работать",
                        Kazakh = "Иә, жұмысқа дайынмын",
                        WordIds = new[] { "word_iya", "word_zhumys" },
                        NextNodeId = OfferNodeId,
                        TrustDelta = 0.05f
                    },
                    new ChoiceSpec
                    {
                        ChoiceId = "choice_polite_kazakh",
                        Russian = "Здравствуйте! Расскажите подробнее",
                        Kazakh = "Сәлеметсіз бе! Толығырақ айтыңыз",
                        WordIds = new[] { "word_salem" },
                        NextNodeId = OfferNodeId,
                        RequiredLanguageLevel = 2,
                        TrustDelta = 0.15f
                    },
                    new ChoiceSpec
                    {
                        ChoiceId = "choice_no",
                        Russian = "Нет, спасибо",
                        Kazakh = "Жоқ, рақмет",
                        WordIds = new[] { "word_zhok", "word_rahmet" },
                        NextNodeId = FarewellNodeId
                    }
                });

            ConfigureNode(offer, OfferNodeId, NpcDispatcherId,
                russian: "Забери посылку здесь и отнеси в кафе. Оплата после доставки.",
                kazakh: "Сәлемдемені осы жерден алып, дәмханаға жеткіз. Ақы жеткізуден кейін.",
                wordIds: new[] { "word_kofe" },
                languageExperience: 15f,
                choices: new[]
                {
                    new ChoiceSpec
                    {
                        ChoiceId = "choice_accept_shift",
                        Russian = "Беру смену",
                        Kazakh = "Жұмысты аламын",
                        WordIds = new[] { "word_zhumys" },
                        NextNodeId = string.Empty,
                        EffectType = "job",
                        EffectTargetId = CourierJobId,
                        EffectValue = 1f
                    },
                    new ChoiceSpec
                    {
                        ChoiceId = "choice_later",
                        Russian = "Позже вернусь",
                        Kazakh = "Кейінірек келемін",
                        NextNodeId = FarewellNodeId
                    }
                });

            ConfigureNode(farewell, FarewellNodeId, NpcDispatcherId,
                russian: "Хорошо, до встречи!",
                kazakh: "Жақсы, сау болыңыз!",
                wordIds: new[] { "word_sau_bolynyz" },
                languageExperience: 10f,
                choices: System.Array.Empty<ChoiceSpec>());

            return new List<ScriptableObject> { greeting, offer, farewell };
        }

        private struct ChoiceSpec
        {
            public string ChoiceId;
            public string Russian;
            public string Kazakh;
            public string[] WordIds;
            public string NextNodeId;
            public int RequiredLanguageLevel;
            public float RequiredTrust;
            public float TrustDelta;
            public string EffectType;
            public string EffectTargetId;
            public float EffectValue;
        }

        private static void ConfigureNode(DialogueNodeDefinition node, string id, string speakerId,
            string russian, string kazakh, string[] wordIds, float languageExperience,
            IReadOnlyList<ChoiceSpec> choices)
        {
            var so = new SerializedObject(node);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("speakerNpcId").stringValue = speakerId;
            so.FindProperty("languageExperience").floatValue = languageExperience;
            so.FindProperty("isTranslationExercise").boolValue = false;

            WriteLine(so.FindProperty("line"), russian, kazakh, wordIds);

            var choiceArray = so.FindProperty("choices");
            choiceArray.arraySize = choices.Count;

            for (var i = 0; i < choices.Count; i++)
            {
                var spec = choices[i];
                var element = choiceArray.GetArrayElementAtIndex(i);

                element.FindPropertyRelative("choiceId").stringValue = spec.ChoiceId;
                element.FindPropertyRelative("nextNodeId").stringValue =
                    spec.NextNodeId ?? string.Empty;
                element.FindPropertyRelative("requiredLanguageLevel").intValue =
                    spec.RequiredLanguageLevel;
                element.FindPropertyRelative("requiredTrust").floatValue = spec.RequiredTrust;
                element.FindPropertyRelative("requiredFlag").stringValue = string.Empty;

                WriteLine(element.FindPropertyRelative("line"),
                    spec.Russian, spec.Kazakh, spec.WordIds);

                var effects = element.FindPropertyRelative("effects");
                var effectList = new List<(string Type, string Target, float Value)>();

                if (spec.TrustDelta != 0f)
                    effectList.Add(("trust", speakerId, spec.TrustDelta));

                if (!string.IsNullOrEmpty(spec.EffectType))
                    effectList.Add((spec.EffectType, spec.EffectTargetId, spec.EffectValue));

                effects.arraySize = effectList.Count;
                for (var e = 0; e < effectList.Count; e++)
                {
                    var effect = effects.GetArrayElementAtIndex(e);
                    effect.FindPropertyRelative("effectType").stringValue = effectList[e].Type;
                    effect.FindPropertyRelative("targetId").stringValue = effectList[e].Target;
                    effect.FindPropertyRelative("value").floatValue = effectList[e].Value;
                }
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(node);
        }

        private static void WriteLine(SerializedProperty line, string russian, string kazakh,
            IReadOnlyList<string> wordIds)
        {
            line.FindPropertyRelative("russian").stringValue = russian ?? string.Empty;
            line.FindPropertyRelative("kazakh").stringValue = kazakh ?? string.Empty;

            var words = line.FindPropertyRelative("wordIds");
            var count = wordIds?.Count ?? 0;
            words.arraySize = count;

            for (var i = 0; i < count; i++)
                words.GetArrayElementAtIndex(i).stringValue = wordIds[i];
        }

        // ------------------------------------------------------------------
        // NPC
        // ------------------------------------------------------------------

        private static List<ScriptableObject> BuildNpcs()
        {
            var dispatcher = BuildNpc(
                id: NpcDispatcherId,
                nameKey: "npc.dispatcher",
                professionKey: "profession.dispatcher",
                homeLocationId: ApartmentLocationId,
                workLocationId: CourierHubLocationId,
                rootDialogueId: GreetingNodeId,
                prefersKazakh: true,
                schedule: new[]
                {
                    ("sched_morning", "Morning", CourierHubLocationId, "work"),
                    ("sched_day", "Day", CourierHubLocationId, "work"),
                    ("sched_evening", "Evening", CafeLocationId, "idle"),
                    ("sched_night", "Night", ApartmentLocationId, "sleep")
                });

            var aidana = BuildNpc(
                id: NpcAidanaId,
                nameKey: "npc.aidana",
                professionKey: "profession.barista",
                homeLocationId: ApartmentLocationId,
                workLocationId: CafeLocationId,
                rootDialogueId: string.Empty,
                prefersKazakh: false,
                schedule: new[]
                {
                    ("sched_morning", "Morning", CafeLocationId, "work"),
                    ("sched_day", "Day", CafeLocationId, "work"),
                    ("sched_evening", "Evening", ShopLocationId, "walk"),
                    ("sched_night", "Night", ApartmentLocationId, "sleep")
                });

            return new List<ScriptableObject> { dispatcher, aidana };
        }

        private static NpcDefinition BuildNpc(string id, string nameKey, string professionKey,
            string homeLocationId, string workLocationId, string rootDialogueId,
            bool prefersKazakh,
            IReadOnlyList<(string EntryId, string Phase, string LocationId, string Behaviour)>
                schedule)
        {
            var npc = LoadOrCreate<NpcDefinition>($"{DefinitionsRoot}/Npcs/{id}.asset");

            var so = new SerializedObject(npc);
            so.FindProperty("id").stringValue = id;
            so.FindProperty("displayNameKey").stringValue = nameKey;
            so.FindProperty("professionKey").stringValue = professionKey;
            so.FindProperty("homeLocationId").stringValue = homeLocationId;
            so.FindProperty("workLocationId").stringValue = workLocationId;
            so.FindProperty("rootDialogueId").stringValue = rootDialogueId;
            so.FindProperty("prefersKazakh").boolValue = prefersKazakh;
            so.FindProperty("baseMood").floatValue = 0.2f;
            so.FindProperty("initialTrust").floatValue = 0.5f;
            so.FindProperty("addressablePrefabKey").stringValue = $"npc_prefab_{id}";

            var scheduleArray = so.FindProperty("schedule");
            scheduleArray.arraySize = schedule.Count;

            for (var i = 0; i < schedule.Count; i++)
            {
                var entry = scheduleArray.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("entryId").stringValue = schedule[i].EntryId;
                entry.FindPropertyRelative("dayPhase").stringValue = schedule[i].Phase;
                entry.FindPropertyRelative("locationId").stringValue = schedule[i].LocationId;
                entry.FindPropertyRelative("behaviour").stringValue = schedule[i].Behaviour;
                entry.FindPropertyRelative("priority").intValue = 0;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(npc);

            return npc;
        }

        // ------------------------------------------------------------------
        // Работа
        // ------------------------------------------------------------------

        private static List<ScriptableObject> BuildJobs()
        {
            var job = LoadOrCreate<JobDefinition>($"{DefinitionsRoot}/Jobs/{CourierJobId}.asset");

            var so = new SerializedObject(job);
            so.FindProperty("id").stringValue = CourierJobId;
            so.FindProperty("displayNameKey").stringValue = "job.courier";
            so.FindProperty("descriptionKey").stringValue = "job.courier.description";
            so.FindProperty("hubLocationId").stringValue = CourierHubLocationId;
            so.FindProperty("basePayout").longValue = 800;
            so.FindProperty("onTimeBonus").longValue = 200;
            so.FindProperty("failurePenalty").longValue = 150;
            so.FindProperty("primarySkillId").stringValue = "skill_work";
            so.FindProperty("skillExperiencePerShift").floatValue = 10f;
            so.FindProperty("languageExperiencePerShift").floatValue = 15f;

            var phases = so.FindProperty("availablePhases");
            var allowed = new[] { "Morning", "Day", "Evening" };
            phases.arraySize = allowed.Length;
            for (var i = 0; i < allowed.Length; i++)
                phases.GetArrayElementAtIndex(i).stringValue = allowed[i];

            so.FindProperty("skillRequirements").arraySize = 0;

            var stages = so.FindProperty("stages");
            var stageRows = new[]
            {
                ("stage_pickup", "job.courier.pickup", CourierHubLocationId, 0f, 0L),
                ("stage_deliver", "job.courier.deliver", CafeLocationId, 90f, 0L)
            };

            stages.arraySize = stageRows.Length;
            for (var i = 0; i < stageRows.Length; i++)
            {
                var element = stages.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("stageId").stringValue = stageRows[i].Item1;
                element.FindPropertyRelative("objectiveKey").stringValue = stageRows[i].Item2;
                element.FindPropertyRelative("locationId").stringValue = stageRows[i].Item3;
                element.FindPropertyRelative("timeLimitMinutes").floatValue = stageRows[i].Item4;
                element.FindPropertyRelative("stagePayout").longValue = stageRows[i].Item5;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(job);

            return new List<ScriptableObject> { job };
        }

        // ------------------------------------------------------------------
        // Утилиты
        // ------------------------------------------------------------------

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null)
                return existing;

            var created = ScriptableObject.CreateInstance<T>();
            var directory = Path.GetDirectoryName(path)?.Replace('\\', '/');

            if (!string.IsNullOrEmpty(directory) && !AssetDatabase.IsValidFolder(directory))
                Directory.CreateDirectory(directory);

            AssetDatabase.CreateAsset(created, path);
            return created;
        }

        private static void AssignList(SerializedObject target, string field,
            IReadOnlyList<ScriptableObject> values)
        {
            var property = target.FindProperty(field);
            property.arraySize = values.Count;

            for (var i = 0; i < values.Count; i++)
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }
    }
}
