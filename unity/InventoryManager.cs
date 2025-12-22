using System.Collections.Generic;
using UnityEngine;

namespace RustlikeClient.UI
{
    /// <summary>
    /// Gerenciador central do inventário (sincroniza com servidor)
    /// </summary>
    public class InventoryManager : MonoBehaviour
    {
        public static InventoryManager Instance { get; private set; }

        [Header("Settings")]
        public const int INVENTORY_SIZE = 24;
        public const int HOTBAR_SIZE = 6;

        // Estado local do inventário (sincronizado com servidor)
        private Dictionary<int, SlotData> _slots = new Dictionary<int, SlotData>();
        private int _selectedHotbarSlot = 0;

        // Referências de UI
        private InventoryUI _inventoryUI;
        private HotbarUI _hotbarUI;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            // Inicializa slots vazios
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                _slots[i] = new SlotData { itemId = -1, quantity = 0 };
            }

            Debug.Log("[InventoryManager] Inicializado");
        }

        private void Start()
        {
            _inventoryUI = FindObjectOfType<InventoryUI>();
            _hotbarUI = FindObjectOfType<HotbarUI>();
        }

        /// <summary>
        /// Atualiza inventário completo (recebido do servidor)
        /// </summary>
        public void UpdateInventory(Network.InventoryUpdatePacket packet)
        {
            Debug.Log($"[InventoryManager] Recebendo update do servidor: {packet.Slots.Count} itens");

            // Limpa todos os slots
            for (int i = 0; i < INVENTORY_SIZE; i++)
            {
                _slots[i] = new SlotData { itemId = -1, quantity = 0 };
            }

            // Atualiza com dados do servidor
            foreach (var slotData in packet.Slots)
            {
                _slots[slotData.SlotIndex] = new SlotData
                {
                    itemId = slotData.ItemId,
                    quantity = slotData.Quantity
                };

                Debug.Log($"  → Slot {slotData.SlotIndex}: Item {slotData.ItemId} x{slotData.Quantity}");
            }

            // Atualiza UI
            RefreshUI();
        }

        /// <summary>
        /// Usa item do slot (envia para servidor)
        /// </summary>
        public async void UseItem(int slotIndex)
        {
            if (slotIndex < 0 || slotIndex >= INVENTORY_SIZE) return;
            if (_slots[slotIndex].itemId <= 0) return;

            Debug.Log($"[InventoryManager] Usando item do slot {slotIndex}");

            var packet = new Network.ItemUsePacket { SlotIndex = slotIndex };
            await Network.NetworkManager.Instance.SendPacketAsync(
                Network.PacketType.ItemUse,
                packet.Serialize()
            );

            // Feedback imediato (será corrigido quando servidor responder)
            PlayUseSound();
        }

        /// <summary>
        /// Move item entre slots (envia para servidor)
        /// </summary>
        public async void MoveItem(int fromSlot, int toSlot)
        {
            if (fromSlot == toSlot) return;
            if (fromSlot < 0 || fromSlot >= INVENTORY_SIZE) return;
            if (toSlot < 0 || toSlot >= INVENTORY_SIZE) return;

            Debug.Log($"[InventoryManager] Movendo item: {fromSlot} → {toSlot}");

            var packet = new Network.ItemMovePacket
            {
                FromSlot = fromSlot,
                ToSlot = toSlot
            };

            await Network.NetworkManager.Instance.SendPacketAsync(
                Network.PacketType.ItemMove,
                packet.Serialize()
            );
        }

        /// <summary>
        /// Seleciona slot da hotbar (teclas 1-6)
        /// </summary>
        public void SelectHotbarSlot(int index)
        {
            if (index < 0 || index >= HOTBAR_SIZE) return;

            _selectedHotbarSlot = index;
            Debug.Log($"[InventoryManager] Hotbar slot selecionado: {index + 1}");

            // Atualiza visual da hotbar
            if (_hotbarUI != null)
            {
                _hotbarUI.SetSelectedSlot(index);
            }
        }

        /// <summary>
        /// Usa item do slot selecionado da hotbar
        /// </summary>
        public void UseSelectedHotbarItem()
        {
            UseItem(_selectedHotbarSlot);
        }

        /// <summary>
        /// Atualiza todas as UIs
        /// </summary>
        private void RefreshUI()
        {
            // Atualiza inventário completo
            if (_inventoryUI != null)
            {
                _inventoryUI.RefreshAllSlots(_slots);
            }

            // Atualiza hotbar
            if (_hotbarUI != null)
            {
                _hotbarUI.RefreshAllSlots(_slots);
            }
        }

        /// <summary>
        /// Pega dados de um slot
        /// </summary>
        public SlotData GetSlot(int index)
        {
            return _slots.TryGetValue(index, out var slot) ? slot : new SlotData { itemId = -1, quantity = 0 };
        }

        /// <summary>
        /// Verifica se tem item
        /// </summary>
        public bool HasItem(int itemId)
        {
            foreach (var slot in _slots.Values)
            {
                if (slot.itemId == itemId && slot.quantity > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Conta quantidade de um item
        /// </summary>
        public int CountItem(int itemId)
        {
            int count = 0;
            foreach (var slot in _slots.Values)
            {
                if (slot.itemId == itemId)
                    count += slot.quantity;
            }
            return count;
        }

        private void PlayUseSound()
        {
            // TODO: Adicionar som de usar item
        }

        /// <summary>
        /// Hotkeys do inventário
        /// </summary>
        private void Update()
        {
            // Teclas 1-6: Seleciona hotbar
            for (int i = 0; i < HOTBAR_SIZE; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                {
                    SelectHotbarSlot(i);
                }
            }

            // Mouse scroll: Navega hotbar
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0f)
            {
                SelectHotbarSlot((_selectedHotbarSlot - 1 + HOTBAR_SIZE) % HOTBAR_SIZE);
            }
            else if (scroll < 0f)
            {
                SelectHotbarSlot((_selectedHotbarSlot + 1) % HOTBAR_SIZE);
            }

            // Tab ou I: Abre/fecha inventário
            if (Input.GetKeyDown(KeyCode.Tab) || Input.GetKeyDown(KeyCode.I))
            {
                ToggleInventory();
            }
        }

        private void ToggleInventory()
        {
            if (_inventoryUI != null)
            {
                _inventoryUI.Toggle();
            }
        }
    }

    /// <summary>
    /// Dados de um slot do inventário
    /// </summary>
    [System.Serializable]
    public class SlotData
    {
        public int itemId;
        public int quantity;
    }
}