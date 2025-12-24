using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LiteNetLib;
using LiteNetLib.Utils;
using RustlikeServer.Network;
using RustlikeServer.World;

namespace RustlikeServer.Core
{
    /// <summary>
    /// ⭐ MIGRADO PARA LITENETLIB - UDP com confiabilidade configurável
    /// </summary>
    public class GameServer : INetEventListener
    {
        private NetManager _netManager;
        private Dictionary<int, Player> _players;
        private Dictionary<NetPeer, ClientHandler> _clients; // ⭐ Mudou de int para NetPeer
        private Dictionary<int, NetPeer> _playerPeers; // ⭐ NOVO: Mapeia PlayerID -> NetPeer
        private int _nextPlayerId;
        private bool _isRunning;
        private readonly int _port;
        private readonly object _playersLock = new object();

        // Stats update
        private const float STATS_UPDATE_RATE = 1f;
        private const float STATS_SYNC_RATE = 2f;

        // ⭐ NOVO: Writer reutilizável (evita garbage collection)
        private NetDataWriter _reusableWriter;

        public GameServer(int port = 7777)
        {
            _port = port;
            _players = new Dictionary<int, Player>();
            _clients = new Dictionary<NetPeer, ClientHandler>();
            _playerPeers = new Dictionary<int, NetPeer>();
            _nextPlayerId = 1;
            _isRunning = false;
            _reusableWriter = new NetDataWriter();
        }

        public async Task StartAsync()
        {
            try
            {
                // ⭐ Configura LiteNetLib
                _netManager = new NetManager(this)
                {
                    AutoRecycle = true, // Recicla pacotes automaticamente
                    UpdateTime = 15, // Poll a cada 15ms
                    DisconnectTimeout = 10000, // 10 segundos timeout
                    PingInterval = 1000, // Ping a cada 1s
                    UnconnectedMessagesEnabled = false
                };

                _netManager.Start(_port);
                _isRunning = true;

                Console.WriteLine($"╔════════════════════════════════════════════════╗");
                Console.WriteLine($"║  SERVIDOR RUST-LIKE (LiteNetLib/UDP)           ║");
                Console.WriteLine($"║  Porta: {_port}                                    ║");
                Console.WriteLine($"║  Sistema de Sobrevivência: ATIVO               ║");
                Console.WriteLine($"║  Aguardando conexões...                        ║");
                Console.WriteLine($"╚════════════════════════════════════════════════╝");
                Console.WriteLine();

                Task updateTask = UpdateLoopAsync();
                Task statsTask = UpdateStatsLoopAsync();
                Task monitorTask = MonitorPlayersAsync();

                await Task.WhenAll(updateTask, statsTask, monitorTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServer] Erro fatal: {ex.Message}");
            }
        }

        /// <summary>
        /// ⭐ NOVO: Loop principal do LiteNetLib (deve ser chamado regularmente)
        /// </summary>
        private async Task UpdateLoopAsync()
        {
            while (_isRunning)
            {
                _netManager.PollEvents();
                await Task.Delay(15); // 15ms = ~66 ticks/segundo
            }
        }

        // ==================== LITENETLIB CALLBACKS ====================

        public void OnPeerConnected(NetPeer peer)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[GameServer] 🔗 Cliente conectado: {peer.Address}:{peer.Port}");
            Console.WriteLine($"[GameServer] Peer ID: {peer.Id} | Ping: {peer.Ping}ms");
            Console.ResetColor();

            // Cria ClientHandler para este peer
            var handler = new ClientHandler(peer, this);
            _clients[peer] = handler;
        }

        public void OnPeerDisconnected(NetPeer peer, DisconnectInfo disconnectInfo)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[GameServer] ❌ Cliente desconectado: {peer.Address}:{peer.Port}");
            Console.WriteLine($"[GameServer] Razão: {disconnectInfo.Reason}");
            Console.ResetColor();

            if (_clients.TryGetValue(peer, out var handler))
            {
                handler.Disconnect();
                _clients.Remove(peer);
            }
        }

        public void OnNetworkReceive(NetPeer peer, NetPacketReader reader, byte channel, DeliveryMethod deliveryMethod)
        {
            if (_clients.TryGetValue(peer, out var handler))
            {
                // Processa pacote no ClientHandler
                byte[] data = reader.GetRemainingBytes();
                _ = handler.ProcessPacketAsync(data);
            }

            reader.Recycle(); // ⭐ Importante: reciclar reader
        }

        public void OnNetworkError(System.Net.IPEndPoint endPoint, System.Net.Sockets.SocketError socketError)
        {
            Console.WriteLine($"[GameServer] Erro de rede: {socketError} em {endPoint}");
        }

        public void OnNetworkReceiveUnconnected(System.Net.IPEndPoint remoteEndPoint, NetPacketReader reader, UnconnectedMessageType messageType)
        {
            // Não usado
        }

        public void OnNetworkLatencyUpdate(NetPeer peer, int latency)
        {
            // Pode logar latência se necessário
        }

        public void OnConnectionRequest(ConnectionRequest request)
        {
            // Auto-aceita conexões (pode adicionar validação aqui)
            request.Accept();
        }

        // ==================== MÉTODOS PÚBLICOS ====================

        public Player CreatePlayer(string name)
        {
            int id = _nextPlayerId++;
            Player player = new Player(id, name);
            
            lock (_playersLock)
            {
                _players[id] = player;
            }
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n✅ [GameServer] NOVO PLAYER CRIADO:");
            Console.WriteLine($"   → Nome: {name}");
            Console.WriteLine($"   → ID: {id}");
            Console.WriteLine($"   → Stats iniciais: {player.Stats}");
            Console.WriteLine($"   → Total de jogadores: {_players.Count}");
            Console.ResetColor();
            
            return player;
        }

        public void RemovePlayer(int playerId)
        {
            string playerName = "";
            bool removed = false;
            NetPeer peerToRemove = null;

            lock (_playersLock)
            {
                if (_players.ContainsKey(playerId))
                {
                    playerName = _players[playerId].Name;
                    _players.Remove(playerId);
                    removed = true;
                }

                if (_playerPeers.TryGetValue(playerId, out peerToRemove))
                {
                    _playerPeers.Remove(playerId);
                }
            }

            if (removed)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n❌ [GameServer] PLAYER REMOVIDO:");
                Console.WriteLine($"   → Nome: {playerName}");
                Console.WriteLine($"   → ID: {playerId}");
                Console.WriteLine($"   → Jogadores restantes: {_players.Count}");
                Console.ResetColor();
                
                if (peerToRemove != null && _clients.ContainsKey(peerToRemove))
                {
                    _clients[peerToRemove].Disconnect();
                    _clients.Remove(peerToRemove);
                }

                BroadcastPlayerDisconnect(playerId);
            }
        }

        public void RegisterClient(int playerId, NetPeer peer, ClientHandler handler)
        {
            _playerPeers[playerId] = peer;
            _clients[peer] = handler;
            
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[GameServer] ClientHandler registrado: Player ID {playerId} | Total: {_clients.Count}");
            Console.ResetColor();
        }

        /// <summary>
        /// ⭐ NOVO: Envia pacote para um peer específico
        /// </summary>
        public void SendPacket(NetPeer peer, PacketType type, byte[] data, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            _reusableWriter.Reset();
            _reusableWriter.Put((byte)type);
            _reusableWriter.Put(data.Length);
            _reusableWriter.Put(data);
            
            peer.Send(_reusableWriter, method);
        }

        /// <summary>
        /// Broadcast com delivery method configurável
        /// </summary>
        public void BroadcastToAll(PacketType type, byte[] data, int excludePlayerId = -1, DeliveryMethod method = DeliveryMethod.ReliableOrdered)
        {
            _reusableWriter.Reset();
            _reusableWriter.Put((byte)type);
            _reusableWriter.Put(data.Length);
            _reusableWriter.Put(data);

            int sentCount = 0;

            foreach (var kvp in _playerPeers)
            {
                if (kvp.Key == excludePlayerId) continue;
                
                kvp.Value.Send(_reusableWriter, method);
                sentCount++;
            }

            // Log apenas broadcasts importantes
            if (sentCount > 0 && type != PacketType.PlayerMovement && type != PacketType.StatsUpdate)
            {
                Console.WriteLine($"[GameServer] Broadcast {type} enviado para {sentCount} jogadores (method: {method})");
            }
        }

        public void BroadcastPlayerSpawn(Player player)
        {
            var spawnPacket = new PlayerSpawnPacket
            {
                PlayerId = player.Id,
                PlayerName = player.Name,
                PosX = player.Position.X,
                PosY = player.Position.Y,
                PosZ = player.Position.Z
            };

            byte[] data = spawnPacket.Serialize();
            Console.WriteLine($"[GameServer] Broadcasting spawn de {player.Name} (ID: {player.Id})");
            BroadcastToAll(PacketType.PlayerSpawn, data, player.Id, DeliveryMethod.ReliableOrdered);
        }

        public void BroadcastPlayerMovement(Player player, ClientHandler sender)
        {
            var movementPacket = new PlayerMovementPacket
            {
                PlayerId = player.Id,
                PosX = player.Position.X,
                PosY = player.Position.Y,
                PosZ = player.Position.Z,
                RotX = player.Rotation.X,
                RotY = player.Rotation.Y
            };

            byte[] data = movementPacket.Serialize();
            
            // ⭐ IMPORTANTE: Movimento usa Sequenced (ignora pacotes antigos, não confiável)
            BroadcastToAll(PacketType.PlayerMovement, data, player.Id, DeliveryMethod.Sequenced);
        }

        public void BroadcastPlayerDisconnect(int playerId)
        {
            byte[] data = BitConverter.GetBytes(playerId);
            Console.WriteLine($"[GameServer] Broadcasting disconnect de Player ID: {playerId}");
            BroadcastToAll(PacketType.PlayerDisconnect, data, playerId, DeliveryMethod.ReliableOrdered);
        }

        public async Task SendExistingPlayersTo(ClientHandler newClient)
        {
            var newPlayerId = newClient.GetPlayer()?.Id ?? -1;
            var newPeer = newClient.GetPeer();
            
            int count = 0;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[GameServer] ========== ENVIANDO PLAYERS EXISTENTES ==========");
            Console.WriteLine($"[GameServer] Novo player ID: {newPlayerId}");
            Console.ResetColor();

            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
                Console.WriteLine($"[GameServer] Total de players no servidor: {playersSnapshot.Count}");
            }

            foreach (var player in playersSnapshot)
            {
                if (player.Id == newPlayerId) continue;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"[GameServer]   → ✅ Enviando spawn de {player.Name} para novo player...");
                Console.ResetColor();

                var spawnPacket = new PlayerSpawnPacket
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    PosX = player.Position.X,
                    PosY = player.Position.Y,
                    PosZ = player.Position.Z
                };

                byte[] data = spawnPacket.Serialize();

                try
                {
                    SendPacket(newPeer, PacketType.PlayerSpawn, data, DeliveryMethod.ReliableOrdered);
                    await Task.Delay(50);
                    
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"[GameServer]      ✅ ENVIADO COM SUCESSO!");
                    Console.ResetColor();
                    count++;
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"[GameServer]      ❌ ERRO: {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[GameServer] ========== FIM DO ENVIO ==========");
            Console.WriteLine($"[GameServer] Total de players enviados: {count}");
            Console.ResetColor();
            Console.WriteLine();
        }

        // ==================== STATS SYSTEM ====================

        private async Task UpdateStatsLoopAsync()
        {
            DateTime lastStatsUpdate = DateTime.Now;
            DateTime lastStatsSync = DateTime.Now;

            while (_isRunning)
            {
                await Task.Delay(100);

                DateTime now = DateTime.Now;

                if ((now - lastStatsUpdate).TotalSeconds >= STATS_UPDATE_RATE)
                {
                    lastStatsUpdate = now;
                    UpdateAllPlayersStats();
                }

                if ((now - lastStatsSync).TotalSeconds >= STATS_SYNC_RATE)
                {
                    lastStatsSync = now;
                    SyncAllPlayersStats();
                }
            }
        }

        private void UpdateAllPlayersStats()
        {
            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
            }

            foreach (var player in playersSnapshot)
            {
                player.UpdateStats();

                if (player.IsDead())
                {
                    HandlePlayerDeath(player);
                }
            }
        }

        private void SyncAllPlayersStats()
        {
            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
            }

            foreach (var player in playersSnapshot)
            {
                if (_playerPeers.TryGetValue(player.Id, out var peer))
                {
                    var statsPacket = new StatsUpdatePacket
                    {
                        PlayerId = player.Id,
                        Health = player.Stats.Health,
                        Hunger = player.Stats.Hunger,
                        Thirst = player.Stats.Thirst,
                        Temperature = player.Stats.Temperature
                    };

                    // Stats usa Unreliable (não crítico, será enviado de novo)
                    SendPacket(peer, PacketType.StatsUpdate, statsPacket.Serialize(), DeliveryMethod.Unreliable);
                }
            }
        }

        private void HandlePlayerDeath(Player player)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[GameServer] ☠️  MORTE: {player.Name} (ID: {player.Id})");
            Console.WriteLine($"[GameServer] Causa: {GetDeathCause(player)}");
            Console.ResetColor();

            var deathPacket = new PlayerDeathPacket
            {
                PlayerId = player.Id,
                KillerName = ""
            };

            BroadcastToAll(PacketType.PlayerDeath, deathPacket.Serialize(), -1, DeliveryMethod.ReliableOrdered);
        }

        private string GetDeathCause(Player player)
        {
            var stats = player.Stats;
            if (stats.Hunger <= 0) return "Fome";
            if (stats.Thirst <= 0) return "Sede";
            if (stats.Temperature < 0) return "Frio Extremo";
            if (stats.Temperature > 40) return "Calor Extremo";
            return "Desconhecida";
        }

        // ==================== MONITORING ====================

        private async Task MonitorPlayersAsync()
        {
            while (_isRunning)
            {
                await Task.Delay(5000);

                List<Player> timedOutPlayers;
                lock (_playersLock)
                {
                    timedOutPlayers = _players.Values.Where(p => p.IsTimedOut()).ToList();
                }
                
                foreach (var player in timedOutPlayers)
                {
                    Console.WriteLine($"[GameServer] Jogador {player.Name} (ID: {player.Id}) timeout");
                    RemovePlayer(player.Id);
                }

                // Status visual
                lock (_playersLock)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"\n╔════════════════════════════════════════════════╗");
                    Console.WriteLine($"║  JOGADORES ONLINE: {_players.Count,-2}                         ║");
                    Console.WriteLine($"║  CLIENTS CONECTADOS: {_clients.Count,-2}                      ║");
                    Console.WriteLine($"╚════════════════════════════════════════════════╝");
                    
                    if (_players.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine("\n→ Lista de Jogadores:");
                        foreach (var player in _players.Values)
                        {
                            NetPeer peer = _playerPeers.TryGetValue(player.Id, out var p) ? p : null;
                            Console.WriteLine($"   • ID {player.Id}: {player.Name}");
                            Console.WriteLine($"     Posição: ({player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1})");
                            Console.WriteLine($"     Stats: {player.Stats}");
                            Console.WriteLine($"     Ping: {(peer != null ? $"{peer.Ping}ms" : "N/A")}");
                            Console.WriteLine($"     Status: {(player.IsDead() ? "☠️ MORTO" : "✅ VIVO")}");
                        }
                    }
                    
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }
        }

        public Player GetPlayer(int playerId)
        {
            lock (_playersLock)
            {
                return _players.TryGetValue(playerId, out var player) ? player : null;
            }
        }

        public void Stop()
        {
            _isRunning = false;
            
            foreach (var client in _clients.Values)
            {
                client.Disconnect();
            }

            _netManager?.Stop();
            Console.WriteLine("[GameServer] Servidor encerrado");
        }
    }
}