using UnityEngine;
using TMPro;

namespace RustlikeClient.UI
{
    /// <summary>
    /// Tooltip que aparece ao passar mouse sobre itens
    /// </summary>
    public class TooltipUI : MonoBehaviour
    {
        public static TooltipUI Instance { get; private set; }

        [Header("UI References")]
        public GameObject tooltipPanel;
        public TextMeshProUGUI titleText;
        public TextMeshProUGUI descriptionText;
        public RectTransform rectTransform;

        [Header("Settings")]
        public Vector2 offset = new Vector2(10, -10);
        public float fadeSpeed = 5f;

        private CanvasGroup _canvasGroup;
        private bool _isVisible = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;

            _canvasGroup = tooltipPanel.GetComponent<CanvasGroup>();
            if (_canvasGroup == null)
            {
                _canvasGroup = tooltipPanel.AddComponent<CanvasGroup>();
            }

            Hide();
        }

        private void Update()
        {
            if (_isVisible && tooltipPanel.activeSelf)
            {
                // Segue o mouse
                Vector3 mousePos = Input.mousePosition;
                rectTransform.position = mousePos + (Vector3)offset;

                // Mantém dentro da tela
                ClampToScreen();

                // Fade in
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 1f, Time.deltaTime * fadeSpeed);
            }
            else if (tooltipPanel.activeSelf)
            {
                // Fade out
                _canvasGroup.alpha = Mathf.Lerp(_canvasGroup.alpha, 0f, Time.deltaTime * fadeSpeed);
                
                if (_canvasGroup.alpha < 0.01f)
                {
                    tooltipPanel.SetActive(false);
                }
            }
        }

        /// <summary>
        /// Mostra tooltip
        /// </summary>
        public void Show(string title, string description, Vector3 position)
        {
            _isVisible = true;

            if (titleText != null)
                titleText.text = title;

            if (descriptionText != null)
                descriptionText.text = description;

            tooltipPanel.SetActive(true);
            _canvasGroup.alpha = 0f;

            rectTransform.position = position + (Vector3)offset;
        }

        /// <summary>
        /// Esconde tooltip
        /// </summary>
        public void Hide()
        {
            _isVisible = false;
        }

        /// <summary>
        /// Mantém tooltip dentro da tela
        /// </summary>
        private void ClampToScreen()
        {
            Vector3 pos = rectTransform.position;
            Vector2 size = rectTransform.sizeDelta;

            // Limites da tela
            float minX = size.x / 2;
            float maxX = Screen.width - size.x / 2;
            float minY = size.y / 2;
            float maxY = Screen.height - size.y / 2;

            pos.x = Mathf.Clamp(pos.x, minX, maxX);
            pos.y = Mathf.Clamp(pos.y, minY, maxY);

            rectTransform.position = pos;
        }
    }
}