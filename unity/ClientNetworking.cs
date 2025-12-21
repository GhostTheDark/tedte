using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using UnityEngine;

namespace RustlikeClient.Network
{
    public class ClientNetworking : MonoBehaviour
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected;
        private byte[] _receiveBuffer;

        public event Action<Packet> OnPacketReceived;
        public event Action OnDisconnected;

        private void Awake()
        {
            _receiveBuffer = new byte[8192];
        }

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                Debug.Log($"[ClientNetworking] Conectando a {ip}:{port}...");
                
                _client = new TcpClient();
                await _client.ConnectAsync(ip, port);
                _stream = _client.GetStream();
                _isConnected = true;

                Debug.Log("[ClientNetworking] Conectado ao servidor!");

                // Inicia recepção de pacotes
                _ = ReceivePacketsAsync();

                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientNetworking] Erro ao conectar: {ex.Message}");
                return false;
            }
        }

        private async Task ReceivePacketsAsync()
        {
            try
            {
                while (_isConnected && _client.Connected)
                {
                    int bytesRead = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                    
                    if (bytesRead == 0)
                    {
                        Debug.LogWarning("[ClientNetworking] Servidor desconectou");
                        Disconnect();
                        break;
                    }

                    byte[] receivedData = new byte[bytesRead];
                    Array.Copy(_receiveBuffer, receivedData, bytesRead);
                    
                    Packet packet = Packet.Deserialize(receivedData);
                    if (packet != null)
                    {
                        // Invoca no main thread do Unity
                        UnityMainThreadDispatcher.Instance.Enqueue(() =>
                        {
                            OnPacketReceived?.Invoke(packet);
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientNetworking] Erro na recepção: {ex.Message}");
                Disconnect();
            }
        }

        public async Task SendPacketAsync(PacketType type, byte[] data)
        {
            if (!_isConnected || _stream == null) return;

            try
            {
                Packet packet = new Packet(type, data);
                byte[] serialized = packet.Serialize();
                await _stream.WriteAsync(serialized, 0, serialized.Length);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientNetworking] Erro ao enviar pacote: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;
            _stream?.Close();
            _client?.Close();

            Debug.Log("[ClientNetworking] Desconectado do servidor");
            
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                OnDisconnected?.Invoke();
            });
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        public bool IsConnected() => _isConnected && _client != null && _client.Connected;
    }

    // Helper para executar código na thread principal do Unity
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly System.Collections.Generic.Queue<Action> _executionQueue = new System.Collections.Generic.Queue<Action>();

        public static UnityMainThreadDispatcher Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("UnityMainThreadDispatcher");
                    _instance = go.AddComponent<UnityMainThreadDispatcher>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private void Update()
        {
            lock (_executionQueue)
            {
                while (_executionQueue.Count > 0)
                {
                    _executionQueue.Dequeue().Invoke();
                }
            }
        }

        public void Enqueue(Action action)
        {
            lock (_executionQueue)
            {
                _executionQueue.Enqueue(action);
            }
        }
    }
}