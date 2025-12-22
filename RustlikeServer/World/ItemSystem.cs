using System;
using System.Collections.Generic;

namespace RustlikeServer.Items
{
    /// <summary>
    /// Tipos de itens no jogo
    /// </summary>
    public enum ItemType
    {
        Consumable,    // Comida, água, remédios
        Resource,      // Madeira, pedra, metal
        Tool,          // Machado, picareta, arma
        Building,      // Fundação, parede, porta
        Clothing       // Roupas, armadura
    }

    /// <summary>
    /// Categoria de consumíveis
    /// </summary>
    public enum ConsumableType
    {
        Food,          // Restaura fome
        Water,         // Restaura sede
        Medicine,      // Restaura vida
        Hybrid         // Restaura múltiplas stats
    }

    /// <summary>
    /// Definição de um item (template/blueprint)
    /// </summary>
    public class ItemDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public ItemType Type { get; set; }
        public int MaxStack { get; set; }
        public bool IsConsumable { get; set; }
        
        // Efeitos do consumível
        public ConsumableType ConsumableCategory { get; set; }
        public float HealthRestore { get; set; }
        public float HungerRestore { get; set; }
        public float ThirstRestore { get; set; }

        public ItemDefinition(int id, string name, string desc, ItemType type, int maxStack)
        {
            Id = id;
            Name = name;
            Description = desc;
            Type = type;
            MaxStack = maxStack;
            IsConsumable = false;
        }

        /// <summary>
        /// Define efeitos de consumível
        /// </summary>
        public ItemDefinition SetConsumableEffect(ConsumableType category, float health, float hunger, float thirst)
        {
            IsConsumable = true;
            ConsumableCategory = category;
            HealthRestore = health;
            HungerRestore = hunger;
            ThirstRestore = thirst;
            return this;
        }
    }

    /// <summary>
    /// Instância de um item no inventário
    /// </summary>
    public class ItemStack
    {
        public int ItemId { get; set; }
        public int Quantity { get; set; }
        public ItemDefinition Definition { get; set; }

        public ItemStack(ItemDefinition definition, int quantity = 1)
        {
            Definition = definition;
            ItemId = definition.Id;
            Quantity = Math.Min(quantity, definition.MaxStack);
        }

        /// <summary>
        /// Verifica se pode adicionar mais itens à stack
        /// </summary>
        public bool CanAddMore(int amount)
        {
            return (Quantity + amount) <= Definition.MaxStack;
        }

        /// <summary>
        /// Adiciona quantidade à stack
        /// </summary>
        public int Add(int amount)
        {
            int space = Definition.MaxStack - Quantity;
            int toAdd = Math.Min(amount, space);
            Quantity += toAdd;
            return amount - toAdd; // Retorna sobra
        }

        /// <summary>
        /// Remove quantidade da stack
        /// </summary>
        public bool Remove(int amount)
        {
            if (amount > Quantity) return false;
            Quantity -= amount;
            return true;
        }

        public bool IsEmpty() => Quantity <= 0;
    }

    /// <summary>
    /// Database de itens do jogo
    /// </summary>
    public static class ItemDatabase
    {
        private static Dictionary<int, ItemDefinition> _items = new Dictionary<int, ItemDefinition>();

        static ItemDatabase()
        {
            InitializeItems();
        }

        private static void InitializeItems()
        {
            // === CONSUMÍVEIS - COMIDA ===
            RegisterItem(new ItemDefinition(1, "Apple", "Uma maçã fresca. Restaura fome.", ItemType.Consumable, 10)
                .SetConsumableEffect(ConsumableType.Food, 0, 20, 5));

            RegisterItem(new ItemDefinition(2, "Cooked Meat", "Carne cozida. Muito nutritivo.", ItemType.Consumable, 20)
                .SetConsumableEffect(ConsumableType.Food, 0, 50, 0));

            RegisterItem(new ItemDefinition(3, "Chocolate Bar", "Barra de chocolate. Energia rápida.", ItemType.Consumable, 10)
                .SetConsumableEffect(ConsumableType.Food, 0, 30, 10));

            // === CONSUMÍVEIS - ÁGUA ===
            RegisterItem(new ItemDefinition(4, "Water Bottle", "Garrafa de água. Mata a sede.", ItemType.Consumable, 5)
                .SetConsumableEffect(ConsumableType.Water, 0, 0, 50));

            RegisterItem(new ItemDefinition(5, "Soda Can", "Refrigerante. Hidrata e energiza.", ItemType.Consumable, 10)
                .SetConsumableEffect(ConsumableType.Water, 0, 10, 40));

            // === CONSUMÍVEIS - REMÉDIOS ===
            RegisterItem(new ItemDefinition(6, "Bandage", "Bandagem. Restaura 20 HP.", ItemType.Consumable, 10)
                .SetConsumableEffect(ConsumableType.Medicine, 20, 0, 0));

            RegisterItem(new ItemDefinition(7, "Medical Syringe", "Seringa médica. Restaura 50 HP.", ItemType.Consumable, 5)
                .SetConsumableEffect(ConsumableType.Medicine, 50, 0, 0));

            RegisterItem(new ItemDefinition(8, "Large Medkit", "Kit médico grande. Full heal.", ItemType.Consumable, 3)
                .SetConsumableEffect(ConsumableType.Medicine, 100, 0, 0));

            // === CONSUMÍVEIS - HÍBRIDOS ===
            RegisterItem(new ItemDefinition(9, "Survival Ration", "Ração de sobrevivência. Restaura tudo um pouco.", ItemType.Consumable, 5)
                .SetConsumableEffect(ConsumableType.Hybrid, 10, 30, 30));

            RegisterItem(new ItemDefinition(10, "Energy Drink", "Bebida energética. Boost completo!", ItemType.Consumable, 5)
                .SetConsumableEffect(ConsumableType.Hybrid, 20, 40, 60));

            // === RECURSOS (para depois) ===
            RegisterItem(new ItemDefinition(100, "Wood", "Madeira. Material de construção básico.", ItemType.Resource, 1000));
            RegisterItem(new ItemDefinition(101, "Stone", "Pedra. Mais resistente que madeira.", ItemType.Resource, 1000));
            RegisterItem(new ItemDefinition(102, "Metal Ore", "Minério de metal. Muito valioso.", ItemType.Resource, 500));

            Console.WriteLine($"[ItemDatabase] {_items.Count} itens carregados");
        }

        private static void RegisterItem(ItemDefinition item)
        {
            _items[item.Id] = item;
        }

        public static ItemDefinition GetItem(int itemId)
        {
            return _items.TryGetValue(itemId, out var item) ? item : null;
        }

        public static bool ItemExists(int itemId)
        {
            return _items.ContainsKey(itemId);
        }

        public static IEnumerable<ItemDefinition> GetAllItems()
        {
            return _items.Values;
        }

        public static IEnumerable<ItemDefinition> GetConsumables()
        {
            foreach (var item in _items.Values)
            {
                if (item.IsConsumable)
                    yield return item;
            }
        }
    }
}