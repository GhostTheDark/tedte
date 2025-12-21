using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace RustlikeClient.Network
{
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
        
        [Header("Settings")]
        public float movementSendRate = 0.05f;
        private float _lastMovementSend;

        private bool _localPlayerReady = false;

        private void Awake()
        {
            Debug.Log("[NetworkManager] ========== AWAKE ==========");
            
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
            
            Debug.Log("[NetworkManager] NetworkManager inicializado com sucesso");
        }

        public async void Connect(string ip, int port, string playerName)
        {
            Debug.Log($"[NetworkManager] ===== INICIANDO CONEXÃO =====");
            Debug.Log($"[NetworkManager] IP: {ip}, Port: {port}, Nome: {playerName}");
            
            bool connected = await _networking.ConnectAsync(ip, port);
            
            if (connected)
            {
                Debug.Log("[NetworkManager] ✅ Conectado! Enviando ConnectionRequest...");
                var request = new ConnectionRequestPacket { PlayerName = playerName };
                await _networking.SendPacketAsync(PacketType.ConnectionRequest, request.Serialize());
                Debug.Log("[NetworkManager] ConnectionRequest enviado");
            }
            else
            {
                Debug.LogError("[NetworkManager] ❌ Falha ao conectar ao servidor");
            }
        }

        private void HandlePacket(Packet packet)
        {
            Debug.Log($"[NetworkManager] <<<< PACOTE RECEBIDO: {packet.Type} >>>>");
            
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

            Debug.Log($"[NetworkManager] ✅ Conexão aceita!");
            Debug.Log($"[NetworkManager] Meu Player ID: {_myPlayerId}");
            Debug.Log($"[NetworkManager] Spawn Position: {response.SpawnPosition}");

            _localPlayerReady = false;
            _otherPlayers.Clear();

            Debug.Log($"[NetworkManager] Estado resetado. Carregando cena Gameplay...");
            
            // ⭐ CORREÇÃO CRÍTICA: PAUSA processamento de pacotes ANTES de carregar a cena
            Debug.Log("[NetworkManager] ⏸️ PAUSANDO processamento de pacotes durante carregamento...");
            _networking.PausePacketProcessing();
            
            SceneManager.LoadScene("Gameplay");
            StartCoroutine(SpawnLocalPlayerAfterSceneLoad(response.SpawnPosition));
        }

        private IEnumerator SpawnLocalPlayerAfterSceneLoad(Vector3 spawnPos)
        {
            Debug.Log("[NetworkManager] Aguardando cena carregar...");
            yield return new WaitForSeconds(0.5f);

            Debug.Log("[NetworkManager] ========== SPAWNING LOCAL PLAYER ==========");
            Debug.Log($"[NetworkManager] playerPrefab null? {playerPrefab == null}");
            Debug.Log($"[NetworkManager] otherPlayerPrefab null? {otherPlayerPrefab == null}");

            if (playerPrefab == null)
            {
                Debug.LogError("[NetworkManager] ❌ ERRO CRÍTICO: playerPrefab não está configurado no Inspector!");
                yield break;
            }

            if (otherPlayerPrefab == null)
            {
                Debug.LogError("[NetworkManager] ❌ ERRO CRÍTICO: otherPlayerPrefab não está configurado no Inspector!");
            }

            _myPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            _myPlayer.name = $"LocalPlayer_{_myPlayerId}";
            
            Debug.Log($"[NetworkManager] ✅ Player local spawned na posição {spawnPos}");
            Debug.Log($"[NetworkManager] Player object: {_myPlayer.name}");

            _localPlayerReady = true;
            Debug.Log($"[NetworkManager] _localPlayerReady = TRUE");

            // ⭐ CORREÇÃO CRÍTICA: RESUME processamento de pacotes AGORA
            Debug.Log("[NetworkManager] ▶️ RESUMINDO processamento de pacotes...");
            _networking.ResumePacketProcessing();

            StartCoroutine(SendHeartbeat());
        }

        private void HandlePlayerSpawn(byte[] data)
        {
            Debug.Log("[NetworkManager] ========== PLAYER SPAWN RECEBIDO ==========");
            
            var spawn = PlayerSpawnPacket.Deserialize(data);
            
            Debug.Log($"[NetworkManager] Spawn Info:");
            Debug.Log($"  - PlayerID: {spawn.PlayerId}");
            Debug.Log($"  - PlayerName: {spawn.PlayerName}");
            Debug.Log($"  - Position: {spawn.Position}");
            Debug.Log($"  - Meu ID: {_myPlayerId}");
            Debug.Log($"  - É meu próprio spawn? {spawn.PlayerId == _myPlayerId}");
            
            if (spawn.PlayerId == _myPlayerId)
            {
                Debug.Log($"[NetworkManager] ⏭️ Ignorando spawn do próprio player");
                return;
            }

            Debug.Log($"[NetworkManager] Estado atual:");
            Debug.Log($"  - _localPlayerReady: {_localPlayerReady}");
            Debug.Log($"  - _otherPlayers.Count: {_otherPlayers.Count}");

            if (!_localPlayerReady)
            {
                Debug.LogWarning($"[NetworkManager] ⚠️ Player local ainda não está pronto! Spawn pode falhar.");
            }

            SpawnOtherPlayer(spawn);
        }

        private void SpawnOtherPlayer(PlayerSpawnPacket spawn)
        {
            Debug.Log($"[NetworkManager] ========== SPAWN OTHER PLAYER ==========");
            Debug.Log($"[NetworkManager] Tentando spawnar: {spawn.PlayerName} (ID: {spawn.PlayerId})");
            Debug.Log($"[NetworkManager] Position: {spawn.Position}");

            if (_otherPlayers.ContainsKey(spawn.PlayerId))
            {
                Debug.LogWarning($"[NetworkManager] ⚠️ Jogador {spawn.PlayerId} JÁ EXISTE! Ignorando spawn duplicado.");
                return;
            }

            if (otherPlayerPrefab == null)
            {
                Debug.LogError($"[NetworkManager] ❌ ERRO CRÍTICO: otherPlayerPrefab é NULL! Não é possível spawnar!");
                Debug.LogError($"[NetworkManager] SOLUÇÃO: Configure o otherPlayerPrefab no Inspector do NetworkManager!");
                return;
            }

            Debug.Log($"[NetworkManager] Instantiando prefab...");
            GameObject otherPlayer = Instantiate(otherPlayerPrefab, spawn.Position, Quaternion.identity);
            otherPlayer.name = $"Player_{spawn.PlayerId}_{spawn.PlayerName}";
            
            _otherPlayers[spawn.PlayerId] = otherPlayer;

            Debug.Log($"[NetworkManager] ✅✅✅ SUCESSO! Jogador spawned:");
            Debug.Log($"  - Nome no Unity: {otherPlayer.name}");
            Debug.Log($"  - Posição: {otherPlayer.transform.position}");
            Debug.Log($"  - Ativo? {otherPlayer.activeSelf}");
            Debug.Log($"  - Total de outros jogadores agora: {_otherPlayers.Count}");

            // Lista todos os outros jogadores
            foreach (var kvp in _otherPlayers)
            {
                Debug.Log($"    → Player ID {kvp.Key}: {kvp.Value.name} (ativo: {kvp.Value.activeSelf})");
            }
        }

        private void HandlePlayerMovement(byte[] data)
        {
            var movement = PlayerMovementPacket.Deserialize(data);
            
            if (movement.PlayerId == _myPlayerId) return;

            if (_otherPlayers.TryGetValue(movement.PlayerId, out GameObject otherPlayer))
            {
                otherPlayer.transform.position = Vector3.Lerp(
                    otherPlayer.transform.position, 
                    movement.Position, 
                    Time.deltaTime * 10f
                );

                otherPlayer.transform.rotation = Quaternion.Euler(0, movement.Rotation.x, 0);
            }
        }

        private void HandlePlayerDisconnect(byte[] data)
        {
            int playerId = System.BitConverter.ToInt32(data, 0);
            
            Debug.Log($"[NetworkManager] ========== PLAYER DISCONNECT ==========");
            Debug.Log($"[NetworkManager] Player ID: {playerId}");

            if (_otherPlayers.TryGetValue(playerId, out GameObject player))
            {
                Debug.Log($"[NetworkManager] Destruindo player {player.name}");
                Destroy(player);
                _otherPlayers.Remove(playerId);
                Debug.Log($"[NetworkManager] Total de outros jogadores restantes: {_otherPlayers.Count}");
            }
            else
            {
                Debug.LogWarning($"[NetworkManager] Player {playerId} não encontrado na lista");
            }
        }

        private void HandleDisconnect()
        {
            Debug.LogWarning("[NetworkManager] ========== DESCONECTADO DO SERVIDOR ==========");
            
            _localPlayerReady = false;
            
            foreach (var player in _otherPlayers.Values)
            {
                if (player != null) Destroy(player);
            }
            _otherPlayers.Clear();

            if (_myPlayer != null) Destroy(_myPlayer);

            SceneManager.LoadScene("MainMenu");
        }

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

            SendMovementAsync(movement);
        }

        private async void SendMovementAsync(PlayerMovementPacket movement)
        {
            await _networking.SendPacketAsync(PacketType.PlayerMovement, movement.Serialize());
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
            await _networking.SendPacketAsync(PacketType.Heartbeat, new byte[0]);
        }

        public int GetMyPlayerId() => _myPlayerId;
        public bool IsConnected() => _networking.IsConnected();

        private void Update()
        {
            // F1 para ver status detalhado
            if (Input.GetKeyDown(KeyCode.F1))
            {
                Debug.Log("========================================");
                Debug.Log("========== NETWORK STATUS ==========");
                Debug.Log("========================================");
                Debug.Log($"My Player ID: {_myPlayerId}");
                Debug.Log($"Local Player Ready: {_localPlayerReady}");
                Debug.Log($"Other Players Count: {_otherPlayers.Count}");
                Debug.Log($"Connected: {IsConnected()}");
                Debug.Log("----------------------------------------");
                Debug.Log("Other Players List:");
                if (_otherPlayers.Count == 0)
                {
                    Debug.Log("  (nenhum outro jogador)");
                }
                else
                {
                    foreach (var kvp in _otherPlayers)
                    {
                        var player = kvp.Value;
                        Debug.Log($"  - ID {kvp.Key}: {player.name}");
                        Debug.Log($"    Position: {player.transform.position}");
                        Debug.Log($"    Active: {player.activeSelf}");
                    }
                }
                Debug.Log("========================================");
            }

            // F2 para testar spawn manual
            if (Input.GetKeyDown(KeyCode.F2))
            {
                Debug.Log("========== TESTE MANUAL DE SPAWN ==========");
                if (otherPlayerPrefab == null)
                {
                    Debug.LogError("otherPlayerPrefab é NULL!");
                    return;
                }

                var testPos = new Vector3(Random.Range(-5, 5), 1, Random.Range(-5, 5));
                Debug.Log($"Spawnando player de teste na posição {testPos}");
                var testPlayer = Instantiate(otherPlayerPrefab, testPos, Quaternion.identity);
                testPlayer.name = "TEST_PLAYER";
                Debug.Log($"✅ Player de teste spawned: {testPlayer.name}");
            }
        }
    }
}