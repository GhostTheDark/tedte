using System.Collections.Generic;
using UnityEngine;

namespace RustlikeClient.UI
{
    /// <summary>
    /// UI do inventário completo (grid de slots)
    /// </summary>
    public class InventoryUI : MonoBehaviour
    {
        [Header("UI References")]
        public GameObject inventoryPanel;
        public Transform slotsContainer;
        public GameObject slotPrefab;

        [Header("Settings")]
        public int startSlotIndex = 6; // Começa após hotbar (0-5)
        public int displaySlots = 18;  // Mostra 18 slots (6x3 grid)

        private List<InventorySlotUI> _slotUIs = new List<InventorySlotUI>();
        private bool _isOpen = false;

        private void Start()
        {
            CreateSlots();
            
            // Começa fechado
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
            }
        }

        /// <summary>
        /// Cria os slots do inventário
        /// </summary>
        private void CreateSlots()
        {
            if (slotPrefab == null || slotsContainer == null)
            {
                Debug.LogError("[InventoryUI] slotPrefab ou slotsContainer não configurado!");
                return;
            }

            // Limpa slots existentes
            foreach (Transform child in slotsContainer)
            {
                Destroy(child.gameObject);
            }
            _slotUIs.Clear();

            // Cria 18 slots (6x3 grid)
            for (int i = 0; i < displaySlots; i++)
            {
                int actualSlotIndex = startSlotIndex + i; // Slots 6-23

                GameObject slotObj = Instantiate(slotPrefab, slotsContainer);
                slotObj.name = $"Slot_{actualSlotIndex}";

                InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.slotIndex = actualSlotIndex;
                    _slotUIs.Add(slotUI);
                }
            }

            Debug.Log($"[InventoryUI] {_slotUIs.Count} slots criados");
        }

        /// <summary>
        /// Atualiza todos os slots com dados do inventário
        /// </summary>
        public void RefreshAllSlots(Dictionary<int, SlotData> slots)
        {
            foreach (var slotUI in _slotUIs)
            {
                int index = slotUI.slotIndex;
                if (slots.TryGetValue(index, out var data))
                {
                    slotUI.SetItem(data.itemId, data.quantity);
                }
                else
                {
                    slotUI.Clear();
                }
            }
        }

        /// <summary>
        /// Abre inventário
        /// </summary>
        public void Open()
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(true);
                _isOpen = true;

                // Libera cursor
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                Debug.Log("[InventoryUI] Inventário aberto");
            }
        }

        /// <summary>
        /// Fecha inventário
        /// </summary>
        public void Close()
        {
            if (inventoryPanel != null)
            {
                inventoryPanel.SetActive(false);
                _isOpen = false;

                // Trava cursor novamente
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;

                Debug.Log("[InventoryUI] Inventário fechado");
            }
        }

        /// <summary>
        /// Alterna entre aberto/fechado
        /// </summary>
        public void Toggle()
        {
            if (_isOpen)
                Close();
            else
                Open();
        }

        public bool IsOpen() => _isOpen;
    }
}