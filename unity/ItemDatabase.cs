using System.Collections.Generic;
using UnityEngine;

namespace RustlikeClient.Items
{
    /// <summary>
    /// Database local de itens (sincronizado com servidor)
    /// ⭐ MELHORADO: Inicialização automática de itens padrão
    /// </summary>
    public class ItemDatabase : MonoBehaviour
    {
        public static ItemDatabase Instance { get; private set; }

        [Header("Item Icons")]
        public List<ItemData> items = new List<ItemData>();

        [Header("Auto Setup")]
        [Tooltip("Se true, cria itens padrão caso a lista esteja vazia")]
        public bool autoCreateDefaultItems = true;

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

            // ⭐ NOVO: Cria itens padrão automaticamente se necessário
            if (autoCreateDefaultItems && items.Count == 0)
            {
                CreateDefaultItems();
            }

            // Cria dictionary para acesso rápido
            _itemDict = new Dictionary<int, ItemData>();
            foreach (var item in items)
            {
                if (item != null && item.id > 0)
                {
                    _itemDict[item.id] = item;
                }
            }

            Debug.Log($"[ItemDatabase] {items.Count} itens carregados");
        }

        public ItemData GetItem(int itemId)
        {
            return _itemDict.TryGetValue(itemId, out var item) ? item : null;
        }

        /// <summary>
        /// Cria itens placeholder sem precisar de sprites
        /// ⭐ MELHORADO: Agora é chamado automaticamente
        /// </summary>
        public void CreateDefaultItems()
        {
            if (items.Count > 0)
            {
                Debug.LogWarning("[ItemDatabase] Já existem itens configurados, pulando criação automática");
                return;
            }

            Debug.Log("[ItemDatabase] Criando itens padrão...");

            // === CONSUMÍVEIS - COMIDA ===
            items.Add(CreateItem(1, "Apple", "Uma maçã fresca. Restaura fome.", 10, true));
            items.Add(CreateItem(2, "Cooked Meat", "Carne cozida. Muito nutritivo.", 20, true));
            items.Add(CreateItem(3, "Chocolate Bar", "Barra de chocolate. Energia rápida.", 10, true));

            // === CONSUMÍVEIS - ÁGUA ===
            items.Add(CreateItem(4, "Water Bottle", "Garrafa de água. Mata a sede.", 5, true));
            items.Add(CreateItem(5, "Soda Can", "Refrigerante. Hidrata e energiza.", 10, true));

            // === CONSUMÍVEIS - REMÉDIOS ===
            items.Add(CreateItem(6, "Bandage", "Bandagem. Restaura 20 HP.", 10, true));
            items.Add(CreateItem(7, "Medical Syringe", "Seringa médica. Restaura 50 HP.", 5, true));
            items.Add(CreateItem(8, "Large Medkit", "Kit médico grande. Full heal.", 3, true));

            // === CONSUMÍVEIS - HÍBRIDOS ===
            items.Add(CreateItem(9, "Survival Ration", "Ração de sobrevivência. Restaura tudo um pouco.", 5, true));
            items.Add(CreateItem(10, "Energy Drink", "Bebida energética. Boost completo!", 5, true));

            // === RECURSOS (para depois) ===
            items.Add(CreateItem(100, "Wood", "Madeira. Material de construção básico.", 1000, false));
            items.Add(CreateItem(101, "Stone", "Pedra. Mais resistente que madeira.", 1000, false));
            items.Add(CreateItem(102, "Metal Ore", "Minério de metal. Muito valioso.", 500, false));

            Debug.Log($"[ItemDatabase] {items.Count} itens padrão criados");
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
                icon = null // Será configurado depois no Inspector
            };

            _itemDict[id] = item;
            return item;
        }

        /// <summary>
        /// ⭐ NOVO: Valida se todos os itens do servidor existem localmente
        /// </summary>
        public bool ValidateItem(int itemId)
        {
            bool exists = _itemDict.ContainsKey(itemId);
            if (!exists)
            {
                Debug.LogWarning($"[ItemDatabase] Item ID {itemId} não encontrado no database local!");
            }
            return exists;
        }

        /// <summary>
        /// ⭐ NOVO: Recarrega o database (útil para hot-reload)
        /// </summary>
        [ContextMenu("Reload Database")]
        public void ReloadDatabase()
        {
            _itemDict.Clear();
            
            foreach (var item in items)
            {
                if (item != null && item.id > 0)
                {
                    _itemDict[item.id] = item;
                }
            }

            Debug.Log($"[ItemDatabase] Database recarregado: {_itemDict.Count} itens");
        }
    }
}