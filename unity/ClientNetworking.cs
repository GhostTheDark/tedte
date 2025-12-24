using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using LiteNetLib;
using LiteNetLib.Utils;

namespace RustlikeClient.Network
{
    /// <summary>
    /// ⭐ MIGRADO PARA LITENETLIB - Cliente UDP com confiabilidade configurável
    /// </summary>
    public class ClientNetworking : MonoBehaviour, INetEventListener
    {
        private NetManager _netManager;
        private NetPeer _serverPeer;
        private bool _isConnected;

        public event Action<Packet> OnPacketReceived;
        public event Action OnDisconnected;

        // ⭐ Writer reutilizável
        private NetDataWriter _writer;

        // Queue para processar pacotes na main thread do Unity
        private Queue<byte[]> _packetQueue = new Queue<byte[]>();
        private object _queueLock = new object();

        private void Awake()
        {
            _writer = new NetDataWriter();

            // ⭐ Configura LiteNetLib
            _netManager = new NetManager(this)
            {
                AutoRecycle = true,
                UpdateTime = 15,
                DisconnectTimeout = 10000,
                PingInterval = 1000,
                UnconnectedMessagesEnabled = false
            };
        }

        private void Update()
        {
            // ⭐ IMPORTANTE: PollEvents deve ser chamado na thread do Unity
            if (_netManager != null)
            {
                _netManager.PollEvents();
            }

            // Processa pacotes na main thread
            ProcessPacketQueue();
        }

        public async Task<bool> ConnectAsync(string ip, int port)
        {
            try
            {
                Debug.Log($"[ClientNetworking] Conectando a {ip}:{port} (LiteNetLib/UDP)...");
                
                _netManager.Start();
                _serverPeer = _netManager.Connect(ip, port, "");

                // Aguarda conexão (timeout 5s)
                float timeout = 5f;
                while (_serverPeer.ConnectionState == ConnectionState.Outgoing && timeout > 0)
                {
                    await Task.Delay(100);
                    timeout -= 0.1f;
                }

                if (_serverPeer.ConnectionState == ConnectionState.Connected)
                {
                    _isConnected = true;
                    Debug.Log("[ClientNetworking] ✅ Conectado ao servidor!");
                    return true;
                }
                else
                {
                    Debug.LogError($"[ClientNetworking] ❌ Timeout na conexão (estado: {_serverPeer.ConnectionState})");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientNetworking] Erro ao conectar: {ex.Message}");
                return false;
            }
        }

        // ==================== LITENETLIB CALLBACKS ====================

        public void OnPeerConnected(NetPeer peer)
        {
            Debug.Log($"[ClientNetworking] 🔗 Conectado ao servidor: {peer.Address}:{peer.Port}");
            _serverPeer = peer;
            _isConnected = true;
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Debug.LogWarning($"[ClientNetworking] ❌ Desconectado: {disconnectInfo.Reason}");
            _isConnected = false;
            _serverPeer = null;

            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                OnDisconnected?.Invoke();
            });
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            // Lê dados do pacote
            byte[] data = reader.GetRemainingBytes();
            
            // Adiciona à fila para processar na main thread
            lock (_queueLock)
            {
                _packetQueue.Enqueue(data);
            }

            reader.Recycle();
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            Debug.LogError($"[ClientNetworking] Erro de rede: {socketError}");
        }

        public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            // Não usado
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            // Opcional: logar latência
            if (latency > 200)
            {
                Debug.LogWarning($"[ClientNetworking] ⚠️ Alta latência: {latency}ms");
            }
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            // Cliente não recebe connection requests
        }

        // ==================== ENVIO DE PACOTES ====================

        /// <summary>
        /// Envia pacote com delivery method configurável
        /// </summary>
        public async Task SendPacketAsync(PacketType type, byte[] data, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            if (!_isConnected || _serverPeer == null) return;

            try
            {
                _writer.Reset();
                _writer.Put((byte)type);
                _writer.Put(data.Length);
                _writer.Put(data);
                
                _serverPeer.Send(_writer, method);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientNetworking] Erro ao enviar pacote: {ex.Message}");
            }
        }

        /// <summary>
        /// Versão síncrona para envios rápidos (movimento)
        /// </summary>
        public void SendPacket(PacketType type, byte[] data, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            if (!_isConnected || _serverPeer == null) return;

            try
            {
                _writer.Reset();
                _writer.Put((byte)type);
                _writer.Put(data.Length);
                _writer.Put(data);
                
                _serverPeer.Send(_writer, method);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[ClientNetworking] Erro ao enviar pacote: {ex.Message}");
            }
        }

        private void ProcessPacketQueue()
        {
            lock (_queueLock)
            {
                while (_packetQueue.Count > 0)
                {
                    byte[] data = _packetQueue.Dequeue();
                    
                    Packet packet = Packet.Deserialize(data);
                    if (packet != null)
                    {
                        OnPacketReceived?.Invoke(packet);
                    }
                }
            }
        }

        public void Disconnect()
        {
            if (!_isConnected) return;

            _isConnected = false;

            if (_serverPeer != null)
            {
                _serverPeer.Disconnect();
            }

            _netManager?.Stop();

            Debug.Log("[ClientNetworking] Desconectado do servidor");
        }

        private void OnApplicationQuit()
        {
            Disconnect();
        }

        private void OnDestroy()
        {
            Disconnect();
        }

        public bool IsConnected() => _isConnected && _serverPeer != null && _serverPeer.ConnectionState == ConnectionState.Connected;
        
        public int GetPing() => _serverPeer?.Ping ?? -1;

        /// <summary>
        /// Para debug
        /// </summary>
        private void OnGUI()
        {
            if (Input.GetKey(KeyCode.F5) && _isConnected)
            {
                GUI.Box(new Rect(10, 450, 250, 100), "LiteNetLib Stats (F5)");
                GUI.Label(new Rect(20, 475, 230, 20), $"Ping: {GetPing()}ms");
                GUI.Label(new Rect(20, 495, 230, 20), $"State: {_serverPeer?.ConnectionState}");
                GUI.Label(new Rect(20, 515, 230, 20), $"Packets Sent: {_serverPeer?.Statistics.PacketsSent ?? 0}");
                GUI.Label(new Rect(20, 535, 230, 20), $"Packets Received: {_serverPeer?.Statistics.PacketsReceived ?? 0}");
            }
        }
    }

    /// <summary>
    /// Helper para executar código na thread principal do Unity
    /// </summary>
    public class UnityMainThreadDispatcher : MonoBehaviour
    {
        private static UnityMainThreadDispatcher _instance;
        private readonly Queue<Action> _executionQueue = new Queue<Action>();

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