using System.Collections.Generic;
using UnityEngine;

namespace RustlikeClient.UI
{
    /// <summary>
    /// Hotbar (barra de atalhos 1-6 na parte inferior da tela)
    /// </summary>
    public class HotbarUI : MonoBehaviour
    {
        [Header("UI References")]
        public Transform slotsContainer;
        public GameObject slotPrefab;

        [Header("Selection")]
        public Color normalBorderColor = new Color(0.3f, 0.3f, 0.3f, 1f);
        public Color selectedBorderColor = new Color(1f, 0.8f, 0f, 1f);

        private List<InventorySlotUI> _hotbarSlots = new List<InventorySlotUI>();
        private int _selectedSlot = 0;

        private void Start()
        {
            CreateHotbarSlots();
            SetSelectedSlot(0);
        }

        /// <summary>
        /// Cria os 6 slots da hotbar
        /// </summary>
        private void CreateHotbarSlots()
        {
            if (slotPrefab == null || slotsContainer == null)
            {
                Debug.LogError("[HotbarUI] slotPrefab ou slotsContainer não configurado!");
                return;
            }

            // Limpa slots existentes
            foreach (Transform child in slotsContainer)
            {
                Destroy(child.gameObject);
            }
            _hotbarSlots.Clear();

            // Cria 6 slots (indices 0-5)
            for (int i = 0; i < InventoryManager.HOTBAR_SIZE; i++)
            {
                GameObject slotObj = Instantiate(slotPrefab, slotsContainer);
                slotObj.name = $"HotbarSlot_{i}";

                InventorySlotUI slotUI = slotObj.GetComponent<InventorySlotUI>();
                if (slotUI != null)
                {
                    slotUI.slotIndex = i;
                    _hotbarSlots.Add(slotUI);

                    // Adiciona número do slot (1-6)
                    AddSlotNumber(slotObj, i + 1);
                }
            }

            Debug.Log($"[HotbarUI] {_hotbarSlots.Count} hotbar slots criados");
        }

        /// <summary>
        /// Adiciona número visual ao slot (1, 2, 3, etc)
        /// </summary>
        private void AddSlotNumber(GameObject slotObj, int number)
        {
            var numberObj = new GameObject("SlotNumber");
            numberObj.transform.SetParent(slotObj.transform);

            var text = numberObj.AddComponent<TMPro.TextMeshProUGUI>();
            text.text = number.ToString();
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TMPro.TextAlignmentOptions.TopLeft;

            var rectTransform = numberObj.GetComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0, 1);
            rectTransform.anchorMax = new Vector2(0, 1);
            rectTransform.pivot = new Vector2(0, 1);
            rectTransform.anchoredPosition = new Vector2(5, -5);
            rectTransform.sizeDelta = new Vector2(20, 20);
        }

        /// <summary>
        /// Atualiza todos os slots da hotbar
        /// </summary>
        public void RefreshAllSlots(Dictionary<int, SlotData> slots)
        {
            for (int i = 0; i < _hotbarSlots.Count; i++)
            {
                if (slots.TryGetValue(i, out var data))
                {
                    _hotbarSlots[i].SetItem(data.itemId, data.quantity);
                }
                else
                {
                    _hotbarSlots[i].Clear();
                }
            }
        }

        /// <summary>
        /// Define qual slot está selecionado (visual)
        /// </summary>
        public void SetSelectedSlot(int index)
        {
            if (index < 0 || index >= _hotbarSlots.Count) return;

            _selectedSlot = index;

            // Atualiza visual de todos os slots
            for (int i = 0; i < _hotbarSlots.Count; i++)
            {
                bool isSelected = (i == index);
                _hotbarSlots[i].Highlight(isSelected);
            }
        }

        public int GetSelectedSlot() => _selectedSlot;
    }
}