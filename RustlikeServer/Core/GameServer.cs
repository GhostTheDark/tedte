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
        private readonly object _playersLock = new object();

        // ⭐ NOVO: Configurações de update de stats
        private const float STATS_UPDATE_RATE = 1f; // Atualiza stats a cada 1 segundo
        private const float STATS_SYNC_RATE = 2f;   // Sincroniza com clientes a cada 2 segundos

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
                Console.WriteLine($"║  Sistema de Sobrevivência: ATIVO               ║");
                Console.WriteLine($"║  Aguardando conexões...                        ║");
                Console.WriteLine($"╚════════════════════════════════════════════════╝");
                Console.WriteLine();

                Task acceptTask = AcceptClientsAsync();
                Task monitorTask = MonitorPlayersAsync();
                Task statsTask = UpdateStatsLoopAsync(); // ⭐ NOVO: Loop de stats

                await Task.WhenAll(acceptTask, monitorTask, statsTask);
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

        // ⭐ NOVO: Loop que atualiza stats de todos os jogadores
        private async Task UpdateStatsLoopAsync()
        {
            DateTime lastStatsUpdate = DateTime.Now;
            DateTime lastStatsSync = DateTime.Now;

            while (_isRunning)
            {
                await Task.Delay(100); // Check a cada 100ms

                DateTime now = DateTime.Now;

                // Atualiza stats dos jogadores
                if ((now - lastStatsUpdate).TotalSeconds >= STATS_UPDATE_RATE)
                {
                    lastStatsUpdate = now;
                    UpdateAllPlayersStats();
                }

                // Sincroniza stats com clientes
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

                // Se morreu, notifica e processa morte
                if (player.IsDead())
                {
                    HandlePlayerDeath(player);
                }
            }
        }

        private async void SyncAllPlayersStats()
        {
            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
            }

            foreach (var player in playersSnapshot)
            {
                if (_clients.TryGetValue(player.Id, out var client))
                {
                    var statsPacket = new StatsUpdatePacket
                    {
                        PlayerId = player.Id,
                        Health = player.Stats.Health,
                        Hunger = player.Stats.Hunger,
                        Thirst = player.Stats.Thirst,
                        Temperature = player.Stats.Temperature
                    };

                    await client.SendPacket(PacketType.StatsUpdate, statsPacket.Serialize());
                }
            }
        }

        private void HandlePlayerDeath(Player player)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"\n[GameServer] ☠️  MORTE: {player.Name} (ID: {player.Id})");
            Console.WriteLine($"[GameServer] Causa: {GetDeathCause(player)}");
            Console.ResetColor();

            // Broadcast morte para todos
            var deathPacket = new PlayerDeathPacket
            {
                PlayerId = player.Id,
                KillerName = "" // Por enquanto, apenas morte por ambiente
            };

            BroadcastToAll(PacketType.PlayerDeath, deathPacket.Serialize());
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
                            Console.WriteLine($"     Stats: {player.Stats}");
                            Console.WriteLine($"     Status: {(player.IsDead() ? "☠️ MORTO" : "✅ VIVO")}");
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
            Console.WriteLine($"   → Stats iniciais: {player.Stats}");
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
            
            // ⭐ REMOVIDO: Log de movimento (muito spam!)
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

            List<Player> playersSnapshot;
            lock (_playersLock)
            {
                playersSnapshot = _players.Values.ToList();
                Console.WriteLine($"[GameServer] Total de players no servidor: {playersSnapshot.Count}");
            }

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

                try
                {
                    await newClient.SendPacket(PacketType.PlayerSpawn, data);
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

            // ⭐ OTIMIZADO: Só loga broadcasts importantes (não movimento)
            if (sentCount > 0 && type != PacketType.PlayerMovement)
            {
                Console.WriteLine($"[GameServer] Broadcast {type} enviado para {sentCount} jogadores (excluindo ID: {excludePlayerId})");
            }
        }

        // ⭐ NOVO: Permite acesso aos players para ClientHandler processar comandos
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
            _listener?.Stop();
            
            foreach (var client in _clients.Values)
            {
                client.Disconnect();
            }

            Console.WriteLine("[GameServer] Servidor encerrado");
        }
    }
}