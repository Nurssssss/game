using QonaevLife.Core;
using UnityEngine;

namespace QonaevLife.UI
{
    /// <summary>
    /// База для экранов интерфейса. Показывает себя, когда маршрутизатор
    /// выводит соответствующий экран, и скрывает в остальных случаях —
    /// поэтому экраны не могут случайно перекрыть друг друга.
    /// </summary>
    public abstract class ScreenView : MonoBehaviour
    {
        [SerializeField] [Tooltip("Корень экрана — скрывается целиком.")]
        private GameObject root;

        private IEventBus _eventBus;

        /// <summary>Какому экрану маршрутизатора соответствует представление.</summary>
        public abstract UiScreen Screen { get; }

        protected IUiRouter Router { get; private set; }

        protected ILocalizedText Text { get; private set; }

        public bool IsVisible => root != null && root.activeSelf;

        /// <summary>Подключает экран к сессии.</summary>
        public void BindScreen(IEventBus eventBus, IUiRouter router, ILocalizedText text)
        {
            UnbindScreen();

            _eventBus = eventBus;
            Router = router;
            Text = text;

            _eventBus.Subscribe<ScreenChangedEvent>(OnScreenChanged);

            OnBound();
            SetVisible(Router.Current == Screen);
        }

        public void UnbindScreen()
        {
            if (_eventBus == null)
                return;

            _eventBus.Unsubscribe<ScreenChangedEvent>(OnScreenChanged);
            _eventBus = null;

            OnUnbound();
        }

        protected virtual void OnDestroy() => UnbindScreen();

        /// <summary>Вызывается после подключения к сессии.</summary>
        protected virtual void OnBound()
        {
        }

        protected virtual void OnUnbound()
        {
        }

        /// <summary>Вызывается при показе экрана — здесь обновляют содержимое.</summary>
        protected virtual void OnShown()
        {
        }

        protected virtual void OnHidden()
        {
        }

        private void OnScreenChanged(ScreenChangedEvent changed)
            => SetVisible(changed.Current == Screen);

        private void SetVisible(bool visible)
        {
            if (root == null || root.activeSelf == visible)
                return;

            root.SetActive(visible);

            if (visible)
                OnShown();
            else
                OnHidden();
        }

        /// <summary>Закрывает экран — для кнопки «Назад».</summary>
        public void CloseSelf() => Router?.Pop();

        /// <summary>Настройка из редакторного генератора сцены.</summary>
        public void ConfigureRoot(GameObject screenRoot) => root = screenRoot;
    }
}
