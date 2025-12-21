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

                // Task para aceitar conexões
                Task acceptTask = AcceptClientsAsync();

                // Task para monitorar jogadores
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
                    _ = handler.HandleClientAsync(); // Fire and forget
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
                await Task.Delay(5000); // Verifica a cada 5 segundos

                var timedOutPlayers = _players.Values.Where(p => p.IsTimedOut()).ToList();
                
                foreach (var player in timedOutPlayers)
                {
                    Console.WriteLine($"[GameServer] Jogador {player.Name} (ID: {player.Id}) timeout");
                    RemovePlayer(player.Id);
                }

                // Log de status
                Console.WriteLine($"[GameServer] Jogadores online: {_players.Count}");
            }
        }

        public Player CreatePlayer(string name)
        {
            int id = _nextPlayerId++;
            Player player = new Player(id, name);
            _players[id] = player;
            return player;
        }

        public void RemovePlayer(int playerId)
        {
            if (_players.ContainsKey(playerId))
            {
                _players.Remove(playerId);
                
                // Remove o cliente handler
                if (_clients.ContainsKey(playerId))
                {
                    _clients[playerId].Disconnect();
                    _clients.Remove(playerId);
                }

                // Notifica outros jogadores sobre a desconexão
                BroadcastPlayerDisconnect(playerId);
            }
        }

        public void RegisterClient(int playerId, ClientHandler handler)
        {
            _clients[playerId] = handler;
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
            BroadcastToAll(PacketType.PlayerDisconnect, data, playerId);
        }

        public async Task SendExistingPlayersTo(ClientHandler newClient)
        {
            foreach (var player in _players.Values)
            {
                if (player.Id == newClient.GetPlayer()?.Id) continue;

                var spawnPacket = new PlayerSpawnPacket
                {
                    PlayerId = player.Id,
                    PlayerName = player.Name,
                    PosX = player.Position.X,
                    PosY = player.Position.Y,
                    PosZ = player.Position.Z
                };

                await newClient.SendPacket(PacketType.PlayerSpawn, spawnPacket.Serialize());
            }
        }

        private async void BroadcastToAll(PacketType type, byte[] data, int excludePlayerId = -1)
        {
            var tasks = new List<Task>();

            foreach (var kvp in _clients)
            {
                if (kvp.Key == excludePlayerId) continue;
                if (!kvp.Value.IsConnected()) continue;

                tasks.Add(kvp.Value.SendPacket(type, data));
            }

            await Task.WhenAll(tasks);
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