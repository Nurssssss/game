using System.Collections.Generic;
using QonaevLife.Content;
using UnityEditor;
using UnityEngine;

namespace QonaevLife.Editor
{
    /// <summary>
    /// Редакторная команда проверки контента (п. 6 ТЗ): сообщает об отсутствующих ID,
    /// пустых локализациях, несуществующих ссылках и недостаточном объёме контента.
    /// </summary>
    public sealed class ContentValidationWindow : EditorWindow
    {
        private ContentDatabase _database;
        private readonly List<string> _errors = new();
        private Vector2 _scroll;
        private bool _hasRun;

        [MenuItem("Qonaev Life/Проверить контент", priority = 100)]
        private static void Open()
        {
            var window = GetWindow<ContentValidationWindow>();
            window.titleContent = new GUIContent("Проверка контента");
            window.minSize = new Vector2(520f, 320f);
            window.Show();
        }

        private void OnEnable()
        {
            if (_database == null)
                _database = FindDatabase();
        }

        private void OnGUI()
        {
            EditorGUILayout.Space(6f);
            _database = (ContentDatabase)EditorGUILayout.ObjectField(
                "База контента", _database, typeof(ContentDatabase), allowSceneObjects: false);

            EditorGUILayout.Space(6f);

            using (new EditorGUI.DisabledScope(_database == null))
            {
                if (GUILayout.Button("Проверить", GUILayout.Height(28f)))
                    RunValidation();
            }

            if (_database == null)
            {
                EditorGUILayout.HelpBox(
                    "Укажите ассет ContentDatabase. Создать: " +
                    "Assets → Create → Qonaev Life → База контента.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.Space(6f);

            if (!_hasRun)
            {
                EditorGUILayout.HelpBox("Проверка ещё не запускалась.", MessageType.None);
                return;
            }

            if (_errors.Count == 0)
            {
                EditorGUILayout.HelpBox("Ошибок не найдено. Контент готов к сборке.",
                    MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox($"Найдено проблем: {_errors.Count}.", MessageType.Error);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);
            foreach (var error in _errors)
                EditorGUILayout.LabelField("• " + error, EditorStyles.wordWrappedLabel);
            EditorGUILayout.EndScrollView();

            if (GUILayout.Button("Скопировать отчёт в буфер обмена"))
                EditorGUIUtility.systemCopyBuffer = string.Join("\n", _errors);
        }

        private void RunValidation()
        {
            _errors.Clear();
            _database.ValidateAll(_errors);
            _hasRun = true;

            if (_errors.Count == 0)
                Debug.Log("[Проверка контента] Ошибок не найдено.");
            else
                Debug.LogError($"[Проверка контента] Найдено проблем: {_errors.Count}. " +
                               "Подробности в окне «Проверка контента».");
        }

        private static ContentDatabase FindDatabase()
        {
            var guids = AssetDatabase.FindAssets($"t:{nameof(ContentDatabase)}");
            if (guids.Length == 0)
                return null;

            var path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<ContentDatabase>(path);
        }
    }
}
