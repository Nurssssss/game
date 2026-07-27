using UnityEngine;

namespace QonaevLife.Content
{
    /// <summary>
    /// База для всех контентных определений (п. 6 ТЗ). Определение — неизменяемая
    /// конфигурация из сборки; пользовательский прогресс живёт в save-файле.
    /// Стабильный строковый <see cref="Id"/> используется в сохранениях и связях,
    /// поэтому его нельзя менять после релиза без миграции.
    /// </summary>
    public abstract class ContentDefinition : ScriptableObject
    {
        [Header("Идентификация")]
        [SerializeField]
        [Tooltip("Стабильный контролируемый ключ. Не менять после релиза без миграции сохранений.")]
        private string id = string.Empty;

        [SerializeField]
        [Tooltip("Служебное имя для команды. Не показывается игроку.")]
        private string editorNote = string.Empty;

        public string Id => id;

        public string EditorNote => editorNote;

        /// <summary>
        /// Проверка данных для редакторной команды валидации (п. 6 ТЗ).
        /// Добавляет сообщения об ошибках в <paramref name="errors"/>.
        /// </summary>
        public virtual void Validate(System.Collections.Generic.List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(id))
                errors.Add($"{name} ({GetType().Name}): не заполнен обязательный Id.");
        }
    }
}
