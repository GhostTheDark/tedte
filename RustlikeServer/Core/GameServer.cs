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

                var timedOutPlayers = _players.Values.Where(p => p.IsTimedOut()).ToList();
                
                foreach (var player in timedOutPlayers)
                {
                    Console.WriteLine($"[GameServer] Jogador {player.Name} (ID: {player.Id}) timeout");
                    RemovePlayer(player.Id);
                }

                Console.WriteLine($"[GameServer] Jogadores online: {_players.Count} | Clients conectados: {_clients.Count}");
            }
        }

        public Player CreatePlayer(string name)
        {
            int id = _nextPlayerId++;
            Player player = new Player(id, name);
            _players[id] = player;
            Console.WriteLine($"[GameServer] Player criado: {name} (ID: {id})");
            return player;
        }

        public void RemovePlayer(int playerId)
        {
            if (_players.ContainsKey(playerId))
            {
                var playerName = _players[playerId].Name;
                _players.Remove(playerId);
                Console.WriteLine($"[GameServer] Player removido: {playerName} (ID: {playerId})");
                
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
            Console.WriteLine($"[GameServer] ClientHandler registrado para Player ID: {playerId} | Total de clients: {_clients.Count}");
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

            Console.WriteLine($"[GameServer] ========== ENVIANDO PLAYERS EXISTENTES ==========");
            Console.WriteLine($"[GameServer] Novo player ID: {newPlayerId}");
            Console.WriteLine($"[GameServer] Total de players no servidor: {_players.Count}");

            foreach (var player in _players.Values)
            {
                Console.WriteLine($"[GameServer] Verificando player: {player.Name} (ID: {player.Id})");
                
                if (player.Id == newPlayerId)
                {
                    Console.WriteLine($"[GameServer]   → Pulando (é o próprio player)");
                    continue;
                }

                Console.WriteLine($"[GameServer]   → Enviando spawn de {player.Name} para novo player...");

                var spawnPacket = new PlayerSpawnPacket
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    PosX = player.Position.X,
                    PosY = player.Position.Y,
                    PosZ = player.Position.Z
                };

                byte[] data = spawnPacket.Serialize();
                Console.WriteLine($"[GameServer]   → Dados serializados: {data.Length} bytes");
                Console.WriteLine($"[GameServer]   → PlayerID={player.Id}, Name={player.Name}, Pos=({player.Position.X}, {player.Position.Y}, {player.Position.Z})");

                try
                {
                    await newClient.SendPacket(PacketType.PlayerSpawn, data);
                    Console.WriteLine($"[GameServer]   → ✅ Pacote PlayerSpawn ENVIADO com sucesso!");
                    count++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[GameServer]   → ❌ ERRO ao enviar: {ex.Message}");
                }
            }

            Console.WriteLine($"[GameServer] ========== FIM DO ENVIO ==========");
            Console.WriteLine($"[GameServer] Total de players enviados: {count}");
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