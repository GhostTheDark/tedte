using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace RustlikeClient.UI
{
    /// <summary>
    /// Slot individual do inventário com drag & drop
    /// </summary>
    public class InventorySlotUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        [Header("UI References")]
        public Image itemIcon;
        public TextMeshProUGUI quantityText;
        public Image highlightBorder;
        public Image backgroundImage;

        [Header("Settings")]
        public int slotIndex;
        public Color normalColor = new Color(0.2f, 0.2f, 0.2f, 0.8f);
        public Color highlightColor = new Color(0.4f, 0.4f, 0.1f, 0.8f);
        public Color hotbarColor = new Color(0.3f, 0.3f, 0.3f, 0.9f);

        // Estado do slot
        private int _itemId = -1;
        private int _quantity = 0;
        private Items.ItemData _itemData;
        private bool _isEmpty = true;

        // Drag & drop
        private Canvas _canvas;
        private GameObject _dragIcon;
        private Vector3 _originalPosition;
        private Transform _originalParent;

        private void Awake()
        {
            _canvas = GetComponentInParent<Canvas>();
            
            if (highlightBorder != null)
                highlightBorder.gameObject.SetActive(false);

            UpdateVisuals();
        }

        /// <summary>
        /// Define o conteúdo do slot
        /// </summary>
        public void SetItem(int itemId, int quantity)
        {
            _itemId = itemId;
            _quantity = quantity;
            _isEmpty = (itemId <= 0 || quantity <= 0);

            if (!_isEmpty)
            {
                _itemData = Items.ItemDatabase.Instance?.GetItem(itemId);
            }
            else
            {
                _itemData = null;
            }

            UpdateVisuals();
        }

        /// <summary>
        /// Limpa o slot
        /// </summary>
        public void Clear()
        {
            SetItem(-1, 0);
        }

        /// <summary>
        /// Atualiza visual do slot
        /// </summary>
        private void UpdateVisuals()
        {
            if (_isEmpty || _itemData == null)
            {
                // Slot vazio
                if (itemIcon != null)
                {
                    itemIcon.gameObject.SetActive(false);
                }

                if (quantityText != null)
                {
                    quantityText.gameObject.SetActive(false);
                }
            }
            else
            {
                // Slot com item
                if (itemIcon != null)
                {
                    itemIcon.gameObject.SetActive(true);
                    itemIcon.sprite = _itemData.icon;
                    
                    // Se não tem sprite, mostra cor baseado no ID
                    if (_itemData.icon == null)
                    {
                        itemIcon.color = GetColorForItem(_itemId);
                    }
                    else
                    {
                        itemIcon.color = Color.white;
                    }
                }

                if (quantityText != null)
                {
                    quantityText.gameObject.SetActive(_quantity > 1);
                    quantityText.text = _quantity.ToString();
                }
            }

            // Atualiza cor do background (hotbar vs inventário)
            if (backgroundImage != null)
            {
                backgroundImage.color = slotIndex < 6 ? hotbarColor : normalColor;
            }
        }

        /// <summary>
        /// Cor placeholder baseada no tipo de item
        /// </summary>
        private Color GetColorForItem(int itemId)
        {
            // Comida = Verde, Água = Azul, Remédio = Vermelho, etc
            if (itemId <= 3) return new Color(0.4f, 0.8f, 0.2f); // Comida - Verde
            if (itemId <= 5) return new Color(0.2f, 0.6f, 1f);   // Água - Azul
            if (itemId <= 8) return new Color(1f, 0.3f, 0.3f);   // Remédio - Vermelho
            return new Color(1f, 1f, 0.4f);                       // Híbrido - Amarelo
        }

        /// <summary>
        /// Destaca o slot (ao passar mouse)
        /// </summary>
        public void Highlight(bool enable)
        {
            if (highlightBorder != null)
            {
                highlightBorder.gameObject.SetActive(enable);
            }

            if (backgroundImage != null && enable)
            {
                backgroundImage.color = highlightColor;
            }
            else if (backgroundImage != null)
            {
                backgroundImage.color = slotIndex < 6 ? hotbarColor : normalColor;
            }
        }

        // ==================== DRAG & DROP ====================

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isEmpty) return;

            // Cria ícone de drag
            _dragIcon = new GameObject("DragIcon");
            _dragIcon.transform.SetParent(_canvas.transform);
            _dragIcon.transform.SetAsLastSibling();

            Image dragImage = _dragIcon.AddComponent<Image>();
            dragImage.sprite = itemIcon.sprite;
            dragImage.color = itemIcon.color;
            dragImage.raycastTarget = false;

            RectTransform rt = _dragIcon.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(50, 50);

            _originalPosition = transform.position;
            _originalParent = transform.parent;

            // Feedback visual
            if (itemIcon != null)
            {
                var color = itemIcon.color;
                color.a = 0.5f;
                itemIcon.color = color;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragIcon != null)
            {
                _dragIcon.transform.position = Input.mousePosition;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_dragIcon != null)
            {
                Destroy(_dragIcon);
            }

            // Restaura alpha
            if (itemIcon != null)
            {
                var color = itemIcon.color;
                color.a = 1f;
                itemIcon.color = color;
            }

            // Verifica se dropou em outro slot
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            foreach (var result in results)
            {
                var targetSlot = result.gameObject.GetComponent<InventorySlotUI>();
                if (targetSlot != null && targetSlot != this)
                {
                    // Move item
                    InventoryManager.Instance?.MoveItem(slotIndex, targetSlot.slotIndex);
                    return;
                }
            }
        }

        // ==================== MOUSE EVENTS ====================

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isEmpty) return;

            // Right click = Usar item
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                InventoryManager.Instance?.UseItem(slotIndex);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            Highlight(true);

            // Mostra tooltip
            if (!_isEmpty && _itemData != null)
            {
                TooltipUI.Instance?.Show(_itemData.itemName, _itemData.description, Input.mousePosition);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            Highlight(false);
            TooltipUI.Instance?.Hide();
        }

        // ==================== GETTERS ====================

        public bool IsEmpty() => _isEmpty;
        public int GetItemId() => _itemId;
        public int GetQuantity() => _quantity;
    }
}