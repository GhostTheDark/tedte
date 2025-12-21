using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RustlikeClient.UI
{
    public class MainMenuUI : MonoBehaviour
    {
        [Header("UI Elements")]
        public TMP_InputField ipInputField;
        public TMP_InputField portInputField;
        public TMP_InputField nameInputField;
        public Button playButton;
        public TextMeshProUGUI statusText;

        [Header("Settings")]
        public string defaultIP = "127.0.0.1";
        public int defaultPort = 7777;

        private void Start()
        {
            // Configura valores padrão
            ipInputField.text = defaultIP;
            portInputField.text = defaultPort.ToString();
            nameInputField.text = $"Player_{Random.Range(1000, 9999)}";

            // Configura botão
            playButton.onClick.AddListener(OnPlayButtonClicked);
            
            UpdateStatus("Digite o IP e clique em PLAY", Color.white);
        }

        private void OnPlayButtonClicked()
        {
            string ip = ipInputField.text.Trim();
            string portStr = portInputField.text.Trim();
            string playerName = nameInputField.text.Trim();

            // Validações
            if (string.IsNullOrEmpty(ip))
            {
                UpdateStatus("IP inválido!", Color.red);
                return;
            }

            if (!int.TryParse(portStr, out int port) || port <= 0 || port > 65535)
            {
                UpdateStatus("Porta inválida!", Color.red);
                return;
            }

            if (string.IsNullOrEmpty(playerName))
            {
                UpdateStatus("Nome inválido!", Color.red);
                return;
            }

            // Desabilita UI durante conexão
            playButton.interactable = false;
            UpdateStatus($"Conectando a {ip}:{port}...", Color.yellow);

            // Conecta ao servidor
            Network.NetworkManager.Instance.Connect(ip, port, playerName);
        }

        private void UpdateStatus(string message, Color color)
        {
            if (statusText != null)
            {
                statusText.text = message;
                statusText.color = color;
            }
            Debug.Log($"[MainMenuUI] {message}");
        }

        private void OnDestroy()
        {
            if (playButton != null)
            {
                playButton.onClick.RemoveListener(OnPlayButtonClicked);
            }
        }
    }
}