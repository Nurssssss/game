using System.Collections.Generic;
using UnityEngine;

namespace QonaevLife.Content
{
    public enum ItemCategory
    {
        Food = 0,
        Drink = 1,
        Clothing = 2,
        Key = 3,
        Furniture = 4,
        Tool = 5,
        Misc = 6
    }

    /// <summary>Какую потребность и насколько восстанавливает предмет (FR-061).</summary>
    [System.Serializable]
    public struct NeedEffect
    {
        [Tooltip("Стабильный идентификатор потребности: hunger, energy, fatigue, mood.")]
        public string needId;

        [Tooltip("Насколько изменяется потребность при использовании.")]
        public float delta;
    }

    /// <summary>
    /// Определение предмета (п. 6 ТЗ): цена, категория, эффект, возможность продажи.
    /// Цены редактируются без перекомпиляции кода (п. 10 ТЗ).
    /// </summary>
    [CreateAssetMenu(
        fileName = "Item_",
        menuName = "Qonaev Life/Экономика/Предмет",
        order = 20)]
    public sealed class ItemDefinition : ContentDefinition
    {
        [Header("Отображение")]
        [SerializeField] [Tooltip("Ключ локализации названия, а не готовый текст.")]
        private string displayNameKey = string.Empty;

        [SerializeField] private string descriptionKey = string.Empty;
        [SerializeField] private Sprite icon;

        [Header("Категория и цены")]
        [SerializeField] private ItemCategory category = ItemCategory.Misc;

        [SerializeField] [Tooltip("Цена покупки в магазине.")] [Min(0)]
        private long purchasePrice;

        [SerializeField] [Tooltip("Цена продажи. Не должна превышать цену покупки.")] [Min(0)]
        private long salePrice;

        [SerializeField] private bool canBeSold = true;

        [Header("Использование")]
        [SerializeField] [Tooltip("Расходуется ли предмет при использовании.")]
        private bool isConsumable;

        [SerializeField] [Tooltip("Сколько единиц влезает в один слот инвентаря.")] [Min(1)]
        private int maxStackSize = 1;

        [SerializeField] private List<NeedEffect> needEffects = new();

        public string DisplayNameKey => displayNameKey;
        public string DescriptionKey => descriptionKey;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public long PurchasePrice => purchasePrice;
        public long SalePrice => salePrice;
        public bool CanBeSold => canBeSold;
        public bool IsConsumable => isConsumable;
        public int MaxStackSize => maxStackSize;
        public IReadOnlyList<NeedEffect> NeedEffects => needEffects;

        public override void Validate(List<string> errors)
        {
            base.Validate(errors);

            if (string.IsNullOrWhiteSpace(displayNameKey))
                errors.Add($"{name}: не заполнен ключ названия.");

            // Отрицательная цена запрещена без явного флага (п. 10 ТЗ);
            // здесь поля ограничены Min(0), проверяем соотношение цен.
            if (canBeSold && salePrice > purchasePrice)
                errors.Add($"{name}: цена продажи ({salePrice}) выше цены покупки ({purchasePrice}).");

            if (!canBeSold && salePrice > 0)
                errors.Add($"{name}: предмет нельзя продать, но задана цена продажи.");

            if (maxStackSize < 1)
                errors.Add($"{name}: maxStackSize должен быть не меньше 1.");

            foreach (var effect in needEffects)
            {
                if (string.IsNullOrWhiteSpace(effect.needId))
                    errors.Add($"{name}: в списке эффектов есть запись без needId.");
            }

            if (isConsumable && needEffects.Count == 0 && category is ItemCategory.Food or ItemCategory.Drink)
                errors.Add($"{name}: съедобный предмет не восстанавливает ни одну потребность.");
        }
    }
}
