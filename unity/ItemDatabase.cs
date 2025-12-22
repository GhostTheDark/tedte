using System.Collections.Generic;
using UnityEngine;

namespace RustlikeClient.Items
{
    /// <summary>
    /// Database local de itens (sincronizado com servidor)
    /// </summary>
    public class ItemDatabase : MonoBehaviour
    {
        public static ItemDatabase Instance { get; private set; }

        [Header("Item Icons")]
        public List<ItemData> items = new List<ItemData>();

        private Dictionary<int, ItemData> _itemDict;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Cria dictionary para acesso rápido
            _itemDict = new Dictionary<int, ItemData>();
            foreach (var item in items)
            {
                _itemDict[item.id] = item;
            }

            Debug.Log($"[ItemDatabase] {items.Count} itens carregados");
        }

        public ItemData GetItem(int itemId)
        {
            return _itemDict.TryGetValue(itemId, out var item) ? item : null;
        }

        /// <summary>
        /// Para criar itens placeholder sem precisar de sprites
        /// </summary>
        public void CreateDefaultItems()
        {
            if (items.Count > 0) return; // Já tem itens configurados

            // Cria itens básicos sem sprites (você pode adicionar depois)
            items.Add(CreateItem(1, "Apple", "Uma maçã fresca", 10, true));
            items.Add(CreateItem(2, "Cooked Meat", "Carne cozida", 20, true));
            items.Add(CreateItem(3, "Chocolate Bar", "Barra de chocolate", 10, true));
            items.Add(CreateItem(4, "Water Bottle", "Garrafa de água", 5, true));
            items.Add(CreateItem(5, "Soda Can", "Refrigerante", 10, true));
            items.Add(CreateItem(6, "Bandage", "Bandagem", 10, true));
            items.Add(CreateItem(7, "Medical Syringe", "Seringa médica", 5, true));
            items.Add(CreateItem(8, "Large Medkit", "Kit médico grande", 3, true));
            items.Add(CreateItem(9, "Survival Ration", "Ração de sobrevivência", 5, true));
            items.Add(CreateItem(10, "Energy Drink", "Bebida energética", 5, true));

            Debug.Log("[ItemDatabase] Itens padrão criados");
        }

        private ItemData CreateItem(int id, string name, string desc, int maxStack, bool consumable)
        {
            var item = new ItemData
            {
                id = id,
                itemName = name,
                description = desc,
                maxStack = maxStack,
                isConsumable = consumable,
                icon = null // Será configurado depois
            };

            _itemDict[id] = item;
            return item;
        }
    }
}