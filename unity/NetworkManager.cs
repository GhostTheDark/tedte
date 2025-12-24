using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using LiteNetLib;

namespace RustlikeClient.Network
{
    /// <summary>
    /// ⭐ ATUALIZADO: Usa LiteNetLib com delivery methods otimizados
    /// </summary>
    public class NetworkManager : MonoBehaviour
    {
        public static NetworkManager Instance { get; private set; }

        [Header("Prefabs")]
        public GameObject playerPrefab;
        public GameObject otherPlayerPrefab;

        [Header("Network")]
        private ClientNetworking _networking;
        private int _myPlayerId = -1;
        private GameObject _myPlayer;
        private Dictionary<int, GameObject> _otherPlayers = new Dictionary<int, GameObject>();
        
        [Header("Movement Settings")]
        [Tooltip("Taxa de envio de movimento (segundos)")]
        public float movementSendRate = 0.05f; // 20 pacotes/segundo
        private float _lastMovementSend;

        private Vector3 _pendingSpawnPosition;

        private void Awake()
        {
            Debug.Log("[NetworkManager] ========== AWAKE (LiteNetLib) ==========");
            
            if (Instance != null && Instance != this)
            {
                Debug.Log("[NetworkManager] Instância duplicada detectada, destruindo...");
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _networking = gameObject.AddComponent<ClientNetworking>();
            _networking.OnPacketReceived += HandlePacket;
            _networking.OnDisconnected += HandleDisconnect;
            
            Debug.Log("[NetworkManager] NetworkManager inicializado com LiteNetLib");
        }

        public async void Connect(string ip, int port, string playerName)
        {
            Debug.Log($"[NetworkManager] ===== INICIANDO CONEXÃO (UDP) =====");
            Debug.Log($"[NetworkManager] IP: {ip}, Port: {port}, Nome: {playerName}");
            
            if (UI.LoadingScreen.Instance != null)
            {
                UI.LoadingScreen.Instance.Show();
                UI.LoadingScreen.Instance.SetProgress(0.1f, "Conectando ao servidor (UDP)...");
            }

            bool connected = await _networking.ConnectAsync(ip, port);
            
            if (connected)
            {
                Debug.Log("[NetworkManager] ✅ Conectado! Enviando ConnectionRequest...");
                
                if (UI.LoadingScreen.Instance != null)
                {
                    UI.LoadingScreen.Instance.SetProgress(0.3f, "Autenticando...");
                }

                var request = new ConnectionRequestPacket { PlayerName = playerName };
                
                // ⭐ ConnectionRequest usa ReliableOrdered
                await _networking.SendPacketAsync(
                    PacketType.ConnectionRequest, 
                    request.Serialize(),
                    DeliveryMethod.ReliableOrdered
                );
                
                Debug.Log("[NetworkManager] ConnectionRequest enviado");
            }
            else
            {
                Debug.LogError("[NetworkManager] ❌ Falha ao conectar ao servidor");
                
                if (UI.LoadingScreen.Instance != null)
                {
                    UI.LoadingScreen.Instance.Hide();
                }
            }
        }

        private void HandlePacket(Packet packet)
        {
            // Logs menos verbosos (só pacotes importantes)
            if (packet.Type != PacketType.PlayerMovement && packet.Type != PacketType.StatsUpdate)
            {
                Debug.Log($"[NetworkManager] <<<< PACOTE: {packet.Type} >>>>");
            }
            
            switch (packet.Type)
            {
                case PacketType.ConnectionAccept:
                    HandleConnectionAccept(packet.Data);
                    break;

                case PacketType.PlayerSpawn:
                    HandlePlayerSpawn(packet.Data);
                    break;

                case PacketType.PlayerMovement:
                    HandlePlayerMovement(packet.Data);
                    break;

                case PacketType.PlayerDisconnect:
                    HandlePlayerDisconnect(packet.Data);
                    break;

                case PacketType.StatsUpdate:
                    HandleStatsUpdate(packet.Data);
                    break;

                case PacketType.PlayerDeath:
                    HandlePlayerDeath(packet.Data);
                    break;

                case PacketType.InventoryUpdate:
                    HandleInventoryUpdate(packet.Data);
                    break;
                    
                default:
                    Debug.LogWarning($"[NetworkManager] Tipo de pacote desconhecido: {packet.Type}");
                    break;
            }
        }

        private void HandleConnectionAccept(byte[] data)
        {
            Debug.Log("[NetworkManager] ========== CONNECTION ACCEPT ==========");
            
            var response = ConnectionAcceptPacket.Deserialize(data);
            _myPlayerId = response.PlayerId;
            _pendingSpawnPosition = response.SpawnPosition;

            Debug.Log($"[NetworkManager] ✅ Conexão aceita!");
            Debug.Log($"[NetworkManager] Meu Player ID: {_myPlayerId}");
            Debug.Log($"[NetworkManager] Spawn Position: {_pendingSpawnPosition}");
            Debug.Log($"[NetworkManager] Ping: {_networking.GetPing()}ms");

            _otherPlayers.Clear();

            Debug.Log($"[NetworkManager] Iniciando carregamento...");
            
            if (UI.LoadingScreen.Instance != null)
            {
                UI.LoadingScreen.Instance.SetProgress(0.5f, "Carregando mundo...");
            }

            SceneManager.LoadScene("Gameplay");
            StartCoroutine(CompleteLoadingSequence());
        }

        private IEnumerator CompleteLoadingSequence()
        {
            Debug.Log("[NetworkManager] ========== INICIANDO SEQUÊNCIA DE LOADING ==========");
            
            yield return new WaitForSeconds(0.3f);

            if (UI.LoadingScreen.Instance != null)
            {
                UI.LoadingScreen.Instance.SetProgress(0.6f, "Preparando spawn...");
            }

            yield return new WaitForSeconds(0.2f);

            Debug.Log("[NetworkManager] ========== SPAWNING LOCAL PLAYER ==========");
            
            if (playerPrefab == null)
            {
                Debug.LogError("[NetworkManager] ❌ ERRO CRÍTICO: playerPrefab não está configurado!");
                yield break;
            }

            _myPlayer = Instantiate(playerPrefab, _pendingSpawnPosition, Quaternion.identity);
            _myPlayer.name = $"LocalPlayer_{_myPlayerId}";
            
            if (_myPlayer.GetComponent<Player.PlayerStatsClient>() == null)
            {
                _myPlayer.AddComponent<Player.PlayerStatsClient>();
            }
            
            Debug.Log($"[NetworkManager] ✅ Player local spawned: {_myPlayer.name}");

            if (UI.LoadingScreen.Instance != null)
            {
                UI.LoadingScreen.Instance.SetProgress(0.8f, "Sincronizando jogadores...");
            }

            yield return new WaitForSeconds(0.5f);

            Debug.Log("[NetworkManager] 📢 ENVIANDO CLIENT READY PARA SERVIDOR");
            SendClientReadyAsync();

            if (UI.LoadingScreen.Instance != null)
            {
                UI.LoadingScreen.Instance.SetProgress(0.9f, "Aguardando sincronização...");
            }

            yield return new WaitForSeconds(1.0f);

            if (UI.LoadingScreen.Instance != null)
            {
                UI.LoadingScreen.Instance.SetProgress(1f, "Pronto!");
                yield return new WaitForSeconds(0.3f);
                UI.LoadingScreen.Instance.Hide();
            }

            if (UI.StatsUI.Instance != null)
            {
                UI.StatsUI.Instance.Show();
            }

            Debug.Log($"[NetworkManager] ========== LOADING COMPLETO ==========");

            StartCoroutine(SendHeartbeat());
        }

        private void HandleInventoryUpdate(byte[] data)
        {
            Debug.Log("[NetworkManager] ========== INVENTORY UPDATE ==========");
            
            var inventoryPacket = InventoryUpdatePacket.Deserialize(data);
            Debug.Log($"[NetworkManager] Recebido inventário com {inventoryPacket.Slots.Count} itens");

            if (UI.InventoryManager.Instance != null)
            {
                UI.InventoryManager.Instance.UpdateInventory(inventoryPacket);
            }
            else
            {
                Debug.LogError("[NetworkManager] InventoryManager não encontrado!");
            }
        }

        private async void SendClientReadyAsync()
        {
            await _networking.SendPacketAsync(
                PacketType.ClientReady, 
                new byte[0],
                DeliveryMethod.ReliableOrdered
            );
        }

        private void HandlePlayerSpawn(byte[] data)
        {
            var spawn = PlayerSpawnPacket.Deserialize(data);
            
            Debug.Log($"[NetworkManager] Player Spawn: {spawn.PlayerName} (ID: {spawn.PlayerId})");
            
            if (spawn.PlayerId == _myPlayerId)
            {
                Debug.Log($"[NetworkManager] ⏭️ Ignorando spawn do próprio player");
                return;
            }

            SpawnOtherPlayer(spawn);
        }

        private void SpawnOtherPlayer(PlayerSpawnPacket spawn)
        {
            Debug.Log($"[NetworkManager] Spawning other player: {spawn.PlayerName} (ID: {spawn.PlayerId})");

            if (_otherPlayers.ContainsKey(spawn.PlayerId))
            {
                Debug.LogWarning($"[NetworkManager] ⚠️ Jogador {spawn.PlayerId} JÁ EXISTE!");
                return;
            }

            if (otherPlayerPrefab == null)
            {
                Debug.LogError($"[NetworkManager] ❌ ERRO: otherPlayerPrefab é NULL!");
                return;
            }

            GameObject otherPlayer = Instantiate(otherPlayerPrefab, spawn.Position, Quaternion.identity);
            otherPlayer.name = $"Player_{spawn.PlayerId}_{spawn.PlayerName}";
            
            _otherPlayers[spawn.PlayerId] = otherPlayer;

            Debug.Log($"[NetworkManager] ✅ Jogador spawned: {otherPlayer.name}");
        }

        private void HandlePlayerMovement(byte[] data)
        {
            var movement = PlayerMovementPacket.Deserialize(data);
            
            if (movement.PlayerId == _myPlayerId) return;

            if (_otherPlayers.TryGetValue(movement.PlayerId, out GameObject otherPlayer))
            {
                var networkSync = otherPlayer.GetComponent<NetworkPlayerSync>();
                if (networkSync == null)
                {
                    networkSync = otherPlayer.AddComponent<NetworkPlayerSync>();
                }
                
                networkSync.UpdateTargetTransform(movement.Position, movement.Rotation.x);
            }
        }

        private void HandleStatsUpdate(byte[] data)
        {
            var stats = StatsUpdatePacket.Deserialize(data);
            
            if (stats.PlayerId != _myPlayerId) return;

            if (_myPlayer != null)
            {
                var playerStats = _myPlayer.GetComponent<Player.PlayerStatsClient>();
                if (playerStats != null)
                {
                    playerStats.UpdateStats(stats.Health, stats.Hunger, stats.Thirst, stats.Temperature);
                }
            }

            if (UI.StatsUI.Instance != null)
            {
                UI.StatsUI.Instance.UpdateStats(stats.Health, stats.Hunger, stats.Thirst, stats.Temperature);
            }
        }

        private void HandlePlayerDeath(byte[] data)
        {
            var death = PlayerDeathPacket.Deserialize(data);
            
            Debug.Log($"[NetworkManager] ========== PLAYER DEATH ==========");
            Debug.Log($"[NetworkManager] Player ID: {death.PlayerId}");

            if (death.PlayerId == _myPlayerId)
            {
                Debug.LogWarning("[NetworkManager] 💀 VOCÊ MORREU!");
                HandleMyDeath();
            }
            else
            {
                if (_otherPlayers.TryGetValue(death.PlayerId, out GameObject player))
                {
                    Debug.Log($"[NetworkManager] Jogador {player.name} morreu");
                }
            }
        }

        private void HandleMyDeath()
        {
            if (_myPlayer != null)
            {
                var controller = _myPlayer.GetComponent<Player.PlayerController>();
                if (controller != null)
                {
                    controller.enabled = false;
                }
            }

            Debug.Log("[NetworkManager] Mostrando tela de morte...");
            StartCoroutine(AutoRespawn());
        }

        private IEnumerator AutoRespawn()
        {
            yield return new WaitForSeconds(5f);
            
            Debug.Log("[NetworkManager] Solicitando respawn...");
            SendRespawnAsync();
        }

        private async void SendRespawnAsync()
        {
            await _networking.SendPacketAsync(
                PacketType.PlayerRespawn, 
                new byte[0],
                DeliveryMethod.ReliableOrdered
            );
        }

        private void HandlePlayerDisconnect(byte[] data)
        {
            int playerId = System.BitConverter.ToInt32(data, 0);
            
            Debug.Log($"[NetworkManager] Player Disconnect: ID {playerId}");

            if (_otherPlayers.TryGetValue(playerId, out GameObject player))
            {
                Debug.Log($"[NetworkManager] Destruindo player {player.name}");
                Destroy(player);
                _otherPlayers.Remove(playerId);
            }
        }

        private void HandleDisconnect()
        {
            Debug.LogWarning("[NetworkManager] ========== DESCONECTADO DO SERVIDOR ==========");
            
            foreach (var player in _otherPlayers.Values)
            {
                if (player != null) Destroy(player);
            }
            _otherPlayers.Clear();

            if (_myPlayer != null) Destroy(_myPlayer);

            if (UI.LoadingScreen.Instance != null)
            {
                UI.LoadingScreen.Instance.Hide();
            }

            if (UI.StatsUI.Instance != null)
            {
                UI.StatsUI.Instance.Hide();
            }

            SceneManager.LoadScene("MainMenu");
        }

        /// <summary>
        /// ⭐ OTIMIZADO: Usa Sequenced para movimento (ignora pacotes antigos)
        /// </summary>
        public void SendPlayerMovement(Vector3 position, Vector2 rotation)
        {
            if (Time.time - _lastMovementSend < movementSendRate) return;
            if (!_networking.IsConnected()) return;

            _lastMovementSend = Time.time;

            var movement = new PlayerMovementPacket
            {
                PlayerId = _myPlayerId,
                Position = position,
                Rotation = rotation
            };

            // ⭐ Movimento usa Sequenced (rápido, descarta pacotes antigos)
            _networking.SendPacket(
                PacketType.PlayerMovement, 
                movement.Serialize(),
                DeliveryMethod.Sequenced
            );
        }

        private IEnumerator SendHeartbeat()
        {
            while (_networking.IsConnected())
            {
                SendHeartbeatAsync();
                yield return new WaitForSeconds(5f);
            }
        }

        private async void SendHeartbeatAsync()
        {
            await _networking.SendPacketAsync(
                PacketType.Heartbeat, 
                new byte[0],
                DeliveryMethod.Unreliable
            );
        }

        public int GetMyPlayerId() => _myPlayerId;
        public bool IsConnected() => _networking.IsConnected();
        public int GetOtherPlayersCount() => _otherPlayers.Count;
        public int GetPing() => _networking.GetPing();

        /// <summary>
        /// ⭐ ATUALIZADO: Permite especificar delivery method
        /// </summary>
        public async System.Threading.Tasks.Task SendPacketAsync(PacketType type, byte[] data, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            await _networking.SendPacketAsync(type, data, method);
        }

        private void Update()
        {
            // F1 para ver status
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Debug.Log("========================================");
                Debug.Log("========== NETWORK STATUS (LiteNetLib) ==========");
                Debug.Log($"My Player ID: {_myPlayerId}");
                Debug.Log($"Connected: {IsConnected()}");
                Debug.Log($"Ping: {GetPing()}ms");
                Debug.Log($"Other Players: {_otherPlayers.Count}");
                Debug.Log("========================================");
            }
        }
    }
}