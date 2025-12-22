using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RustlikeClient.UI
{
    public class LoadingScreen : MonoBehaviour
    {
        public static LoadingScreen Instance { get; private set; }

        [Header("UI Elements")]
        public GameObject loadingPanel;
        public Image progressBar;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI percentageText;
        public TextMeshProUGUI tipText;

        [Header("Settings")]
        public float minLoadingTime = 2f; // Tempo mínimo para garantir que tudo carregou
        public string[] loadingTips = new string[]
        {
            "Dica: Use SHIFT para correr",
            "Dica: Pressione ESC para liberar o cursor",
            "Dica: Pressione F1 para ver informações de rede",
            "Conectando ao servidor...",
            "Aguardando sincronização...",
            "Preparando mundo..."
        };

        private float _currentProgress = 0f;
        private float _targetProgress = 0f;
        private bool _isLoading = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }
        }

        public void Show()
        {
            Debug.Log("[LoadingScreen] Mostrando loading screen");
            
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(true);
            }

            _currentProgress = 0f;
            _targetProgress = 0f;
            _isLoading = true;

            UpdateUI();
            StartCoroutine(AnimateProgress());
            StartCoroutine(CycleTips());
        }

        public void Hide()
        {
            Debug.Log("[LoadingScreen] Escondendo loading screen");
            
            _isLoading = false;
            
            if (loadingPanel != null)
            {
                loadingPanel.SetActive(false);
            }

            StopAllCoroutines();
        }

        public void SetProgress(float progress, string status = "")
        {
            _targetProgress = Mathf.Clamp01(progress);
            
            if (!string.IsNullOrEmpty(status) && statusText != null)
            {
                statusText.text = status;
            }

            Debug.Log($"[LoadingScreen] Progress: {_targetProgress * 100:F0}% - {status}");
        }

        private IEnumerator AnimateProgress()
        {
            while (_isLoading)
            {
                // Anima suavemente até o target
                _currentProgress = Mathf.Lerp(_currentProgress, _targetProgress, Time.deltaTime * 3f);
                
                UpdateUI();
                
                yield return null;
            }
        }

        private IEnumerator CycleTips()
        {
            int tipIndex = 0;

            while (_isLoading)
            {
                if (tipText != null && loadingTips.Length > 0)
                {
                    tipText.text = loadingTips[tipIndex];
                    tipIndex = (tipIndex + 1) % loadingTips.Length;
                }

                yield return new WaitForSeconds(3f);
            }
        }

        private void UpdateUI()
        {
            if (progressBar != null)
            {
                progressBar.fillAmount = _currentProgress;
            }

            if (percentageText != null)
            {
                percentageText.text = $"{_currentProgress * 100:F0}%";
            }
        }

        // Método auxiliar para loading completo com etapas
        public IEnumerator LoadWithSteps(System.Action onComplete = null)
        {
            Show();
            
            float startTime = Time.time;

            // Etapa 1: Conectando
            SetProgress(0.1f, "Conectando ao servidor...");
            yield return new WaitForSeconds(0.3f);

            // Etapa 2: Autenticando
            SetProgress(0.3f, "Autenticando...");
            yield return new WaitForSeconds(0.3f);

            // Etapa 3: Carregando mundo
            SetProgress(0.5f, "Carregando mundo...");
            yield return new WaitForSeconds(0.3f);

            // Etapa 4: Sincronizando jogadores
            SetProgress(0.7f, "Sincronizando jogadores...");
            yield return new WaitForSeconds(0.3f);

            // Etapa 5: Preparando spawn
            SetProgress(0.9f, "Preparando spawn...");
            yield return new WaitForSeconds(0.3f);

            // Garante tempo mínimo
            float elapsed = Time.time - startTime;
            if (elapsed < minLoadingTime)
            {
                yield return new WaitForSeconds(minLoadingTime - elapsed);
            }

            // Completo
            SetProgress(1f, "Pronto!");
            yield return new WaitForSeconds(0.3f);

            Hide();
            onComplete?.Invoke();
        }
    }
}