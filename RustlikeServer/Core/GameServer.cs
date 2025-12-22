using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using RustlikeServer.Network;
using RustlikeServer.World;

namespace RustlikeServer.Core
{
    public class GameServer
    {
        private TcpListener _listener;
        private Dictionary<int, Player> _players;
        private Dictionary<int, ClientHandler> _clients;
        private int _nextPlayerId;
        private bool _isRunning;
        private readonly int _port;
        private readonly object _playersLock = new object(); // ⭐ NOVO: Lock para thread safety

        public GameServer(int port = 7777)
        {
            _port = port;
            _players = new Dictionary<int, Player>();
            _clients = new Dictionary<int, ClientHandler>();
            _nextPlayerId = 1;
            _isRunning = false;
        }

        public async Task StartAsync()
        {
            try
            {
                _listener = new TcpListener(IPAddress.Any, _port);
                _listener.Start();
                _isRunning = true;

                Console.WriteLine($"╔════════════════════════════════════════════════╗");
                Console.WriteLine($"║  SERVIDOR RUST-LIKE INICIADO                   ║");
                Console.WriteLine($"║  Porta: {_port}                                    ║");
                Console.WriteLine($"║  Aguardando conexões...                        ║");
                Console.WriteLine($"╚════════════════════════════════════════════════╝");
                Console.WriteLine();

                Task acceptTask = AcceptClientsAsync();
                Task monitorTask = MonitorPlayersAsync();

                await Task.WhenAll(acceptTask, monitorTask);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[GameServer] Erro fatal: {ex.Message}");
            }
        }

        private async Task AcceptClientsAsync()
        {
            while (_isRunning)
            {
                try
                {
                    TcpClient client = await _listener.AcceptTcpClientAsync();
                    ClientHandler handler = new ClientHandler(client, this);
                    _ = handler.HandleClientAsync();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameServer] Erro ao aceitar cliente: {ex.Message}");
                }
            }
        }

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
                            Console.WriteLine($"   • ID {player.Id}: {player.Name}");
                            Console.WriteLine($"     Posição: ({player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1})");
                            Console.WriteLine($"     Último heartbeat: {(DateTime.Now - player.LastHeartbeat).TotalSeconds:F1}s atrás");
                        }
                    }
                    
                    Console.ResetColor();
                    Console.WriteLine();
                }
            }
        }

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
            Console.WriteLine($"   → Total de jogadores: {_players.Count}");
            Console.ResetColor();
            
            return player;
        }

        public void RemovePlayer(int playerId)
        {
            string playerName = "";
            bool removed = false;

            lock (_playersLock)
            {
                if (_players.ContainsKey(playerId))
                {
                    playerName = _players[playerId].Name;
                    _players.Remove(playerId);
                    removed = true;
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
                
                if (_clients.ContainsKey(playerId))
                {
                    _clients[playerId].Disconnect();
                    _clients.Remove(playerId);
                }

                BroadcastPlayerDisconnect(playerId);
            }
        }

        public void RegisterClient(int playerId, ClientHandler handler)
        {
            _clients[playerId] = handler;
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"[GameServer] ClientHandler registrado para Player ID: {playerId} | Total de clients: {_clients.Count}");
            Console.ResetColor();
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
            Console.WriteLine($"[GameServer] Broadcasting spawn de {player.Name} (ID: {player.Id}) para {_clients.Count - 1} outros jogadores");
            BroadcastToAll(PacketType.PlayerSpawn, data, player.Id);
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
            BroadcastToAll(PacketType.PlayerMovement, data, player.Id);
        }

        public void BroadcastPlayerDisconnect(int playerId)
        {
            byte[] data = BitConverter.GetBytes(playerId);
            Console.WriteLine($"[GameServer] Broadcasting disconnect de Player ID: {playerId}");
            BroadcastToAll(PacketType.PlayerDisconnect, data, playerId);
        }

        public async Task SendExistingPlayersTo(ClientHandler newClient)
        {
            var newPlayerId = newClient.GetPlayer()?.Id ?? -1;
            int count = 0;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[GameServer] ========== ENVIANDO PLAYERS EXISTENTES ==========");
            Console.WriteLine($"[GameServer] Novo player ID: {newPlayerId}");
            Console.ResetColor();

            // ⭐ CRITICAL: Cria snapshot dos players para evitar problemas de concorrência
            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
                Console.WriteLine($"[GameServer] Total de players no servidor: {playersSnapshot.Count}");
            }

            // Itera sobre o snapshot
            foreach (var player in playersSnapshot)
            {
                Console.WriteLine($"[GameServer] Verificando player: {player.Name} (ID: {player.Id})");
                
                if (player.Id == newPlayerId)
                {
                    Console.WriteLine($"[GameServer]   → Pulando (é o próprio player)");
                    continue;
                }

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
                Console.WriteLine($"[GameServer]      Dados: {data.Length} bytes | Pos=({player.Position.X:F1}, {player.Position.Y:F1}, {player.Position.Z:F1})");

                try
                {
                    await newClient.SendPacket(PacketType.PlayerSpawn, data);
                    
                    // ⭐ IMPORTANTE: Delay entre cada spawn
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

        private async void BroadcastToAll(PacketType type, byte[] data, int excludePlayerId = -1)
        {
            var tasks = new List<Task>();
            int sentCount = 0;

            foreach (var kvp in _clients)
            {
                if (kvp.Key == excludePlayerId) continue;
                if (!kvp.Value.IsConnected()) continue;

                tasks.Add(kvp.Value.SendPacket(type, data));
                sentCount++;
            }

            if (tasks.Count > 0)
            {
                await Task.WhenAll(tasks);
            }

            if (sentCount > 0)
            {
                Console.WriteLine($"[GameServer] Broadcast {type} enviado para {sentCount} jogadores (excluindo ID: {excludePlayerId})");
            }
        }

        public void Stop()
        {
            _isRunning = false;
            _listener?.Stop();
            
            foreach (var client in _clients.Values)
            {
                client.Disconnect();
            }

            Console.WriteLine("[GameServer] Servidor encerrado");
        }
    }
}