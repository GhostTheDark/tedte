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
        public float movementSendRate = 0.05f; // Envia movimento a cada 50ms
        private float _lastMovementSend;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _networking = gameObject.AddComponent<ClientNetworking>();
            _networking.OnPacketReceived += HandlePacket;
            _networking.OnDisconnected += HandleDisconnect;
        }

        public async void Connect(string ip, int port, string playerName)
        {
            bool connected = await _networking.ConnectAsync(ip, port);
            
            if (connected)
            {
                // Envia requisição de conexão
                var request = new ConnectionRequestPacket { PlayerName = playerName };
                await _networking.SendPacketAsync(PacketType.ConnectionRequest, request.Serialize());
            }
            else
            {
                Debug.LogError("[NetworkManager] Falha ao conectar ao servidor");
            }
        }

        private void HandlePacket(Packet packet)
        {
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
            }
        }

        private void HandleConnectionAccept(byte[] data)
        {
            var response = ConnectionAcceptPacket.Deserialize(data);
            _myPlayerId = response.PlayerId;

            Debug.Log($"[NetworkManager] Conexão aceita! ID: {_myPlayerId}");

            // Carrega a cena de gameplay
            SceneManager.LoadScene("Gameplay");
            StartCoroutine(SpawnLocalPlayerAfterSceneLoad(response.SpawnPosition));
        }

        private IEnumerator SpawnLocalPlayerAfterSceneLoad(Vector3 spawnPos)
        {
            yield return new WaitForSeconds(0.5f); // Aguarda a cena carregar

            if (playerPrefab == null)
            {
                Debug.LogError("[NetworkManager] Player prefab não configurado!");
                yield break;
            }

            _myPlayer = Instantiate(playerPrefab, spawnPos, Quaternion.identity);
            _myPlayer.name = $"LocalPlayer_{_myPlayerId}";
            
            Debug.Log($"[NetworkManager] Player local spawned na posição {spawnPos}");

            // Inicia envio de heartbeat
            StartCoroutine(SendHeartbeat());
        }

        private void HandlePlayerSpawn(byte[] data)
        {
            var spawn = PlayerSpawnPacket.Deserialize(data);
            
            if (spawn.PlayerId == _myPlayerId) return; // Ignora próprio player

            Debug.Log($"[NetworkManager] Outro jogador spawned: {spawn.PlayerName} (ID: {spawn.PlayerId})");

            if (otherPlayerPrefab == null)
            {
                Debug.LogError("[NetworkManager] OtherPlayer prefab não configurado!");
                return;
            }

            GameObject otherPlayer = Instantiate(otherPlayerPrefab, spawn.Position, Quaternion.identity);
            otherPlayer.name = $"Player_{spawn.PlayerId}_{spawn.PlayerName}";
            _otherPlayers[spawn.PlayerId] = otherPlayer;
        }

        private void HandlePlayerMovement(byte[] data)
        {
            var movement = PlayerMovementPacket.Deserialize(data);
            
            if (movement.PlayerId == _myPlayerId) return; // Ignora próprio movimento

            if (_otherPlayers.TryGetValue(movement.PlayerId, out GameObject otherPlayer))
            {
                // Interpolação suave de posição
                otherPlayer.transform.position = Vector3.Lerp(
                    otherPlayer.transform.position, 
                    movement.Position, 
                    Time.deltaTime * 10f
                );

                // Atualiza rotação
                otherPlayer.transform.rotation = Quaternion.Euler(0, movement.Rotation.x, 0);
            }
        }

        private void HandlePlayerDisconnect(byte[] data)
        {
            int playerId = System.BitConverter.ToInt32(data, 0);
            
            Debug.Log($"[NetworkManager] Jogador {playerId} desconectou");

            if (_otherPlayers.TryGetValue(playerId, out GameObject player))
            {
                Destroy(player);
                _otherPlayers.Remove(playerId);
            }
        }

        private void HandleDisconnect()
        {
            Debug.LogWarning("[NetworkManager] Desconectado do servidor!");
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
    }
}