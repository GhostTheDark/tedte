using UnityEngine;

namespace RustlikeClient.Network
{
    /// <summary>
    /// ⭐ NOVO: Configurações centralizadas de rede (LiteNetLib)
    /// </summary>
    [CreateAssetMenu(fileName = "NetworkConfig", menuName = "Rustlike/Network Config")]
    public class NetworkConfig : ScriptableObject
    {
        [Header("🌐 Conexão")]
        [Tooltip("IP padrão do servidor")]
        public string defaultServerIP = "127.0.0.1";
        
        [Tooltip("Porta padrão")]
        public int defaultPort = 7777;
        
        [Tooltip("Timeout de conexão (ms)")]
        public int connectionTimeout = 10000;
        
        [Tooltip("Timeout de desconexão (ms)")]
        public int disconnectTimeout = 10000;

        [Header("📡 Performance")]
        [Tooltip("Intervalo de PollEvents (ms) - Menor = mais responsivo")]
        [Range(10, 100)]
        public int pollInterval = 15; // 66 ticks/s
        
        [Tooltip("Intervalo de ping (ms)")]
        [Range(500, 5000)]
        public int pingInterval = 1000;

        [Header("🏃 Movimento")]
        [Tooltip("Taxa de envio de movimento (pacotes/segundo)")]
        [Range(10, 60)]
        public int movementTickRate = 20; // 20 pkt/s = 0.05s
        
        [Tooltip("Distância mínima para enviar update (metros)")]
        [Range(0.001f, 0.5f)]
        public float minMovementThreshold = 0.01f;
        
        [Tooltip("Ângulo mínimo para enviar update (graus)")]
        [Range(0.1f, 5f)]
        public float minRotationThreshold = 1f;
        
        [Header("💓 Heartbeat")]
        [Tooltip("Intervalo de heartbeat (segundos)")]
        [Range(1f, 10f)]
        public float heartbeatInterval = 5f;
        
        [Header("📊 Stats")]
        [Tooltip("Taxa de sincronização de stats (segundos)")]
        [Range(0.5f, 5f)]
        public float statsSyncRate = 2f;
        
        [Header("🎒 Inventário")]
        [Tooltip("Debounce para sync de inventário (segundos)")]
        [Range(0.1f, 1f)]
        public float inventorySyncDebounce = 0.3f;

        [Header("🔧 Avançado")]
        [Tooltip("Ativa auto-recycling de pacotes")]
        public bool autoRecycle = true;
        
        [Tooltip("Ativa simulação de lag (debug)")]
        public bool simulateLag = false;
        
        [Tooltip("Lag simulado (ms)")]
        [Range(0, 500)]
        public int simulatedLag = 100;
        
        [Tooltip("Perda de pacotes simulada (%)")]
        [Range(0, 50)]
        public int simulatedPacketLoss = 0;

        [Header("📈 Otimizações")]
        [Tooltip("Usa Sequenced para movimento (recomendado)")]
        public bool useSequencedMovement = true;
        
        [Tooltip("Usa Unreliable para stats (recomendado)")]
        public bool useUnreliableStats = true;
        
        [Tooltip("Comprime pacotes grandes (>1KB)")]
        public bool compressLargePackets = false;
        
        [Tooltip("Threshold para compressão (bytes)")]
        public int compressionThreshold = 1024;

        // Calcula movementSendRate baseado no tickrate
        public float MovementSendRate => 1f / movementTickRate;

        /// <summary>
        /// Valida configuração
        /// </summary>
        private void OnValidate()
        {
            // Garante valores mínimos
            if (pollInterval < 10) pollInterval = 10;
            if (movementTickRate < 10) movementTickRate = 10;
            if (movementTickRate > 60) movementTickRate = 60;
            
            // Aviso de performance
            if (movementTickRate > 30)
            {
                Debug.LogWarning($"[NetworkConfig] MovementTickRate alto ({movementTickRate}) pode causar overhead. Recomendado: 20");
            }
            
            if (pollInterval > 30)
            {
                Debug.LogWarning($"[NetworkConfig] PollInterval alto ({pollInterval}ms) pode causar latência. Recomendado: 15ms");
            }
        }

        /// <summary>
        /// Configuração para LAN (baixa latência)
        /// </summary>
        public void SetLANProfile()
        {
            pollInterval = 10;
            movementTickRate = 30;
            pingInterval = 500;
            Debug.Log("[NetworkConfig] Perfil LAN aplicado");
        }

        /// <summary>
        /// Configuração para Internet (alta latência)
        /// </summary>
        public void SetInternetProfile()
        {
            pollInterval = 15;
            movementTickRate = 20;
            pingInterval = 1000;
            Debug.Log("[NetworkConfig] Perfil Internet aplicado");
        }

        /// <summary>
        /// Configuração para Mobile (economia de bateria)
        /// </summary>
        public void SetMobileProfile()
        {
            pollInterval = 20;
            movementTickRate = 15;
            pingInterval = 2000;
            Debug.Log("[NetworkConfig] Perfil Mobile aplicado");
        }

        /// <summary>
        /// Exibe estatísticas estimadas
        /// </summary>
        [ContextMenu("Show Estimated Stats")]
        public void ShowEstimatedStats()
        {
            float packetsPerSecond = movementTickRate + (1f / heartbeatInterval) + (1f / statsSyncRate);
            float bytesPerSecond = packetsPerSecond * 50; // Estimativa: 50 bytes/pacote
            float kbps = (bytesPerSecond * 8) / 1024f;
            
            Debug.Log("========== ESTIMATED NETWORK STATS ==========");
            Debug.Log($"Movement: {movementTickRate} pkt/s");
            Debug.Log($"Total packets/s: ~{packetsPerSecond:F1}");
            Debug.Log($"Bandwidth: ~{kbps:F1} Kbps");
            Debug.Log($"For 50 players: ~{kbps * 50:F0} Kbps ({kbps * 50 / 1024f:F1} Mbps)");
            Debug.Log("============================================");
        }
    }
}