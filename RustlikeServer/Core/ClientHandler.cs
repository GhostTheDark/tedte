using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using RustlikeServer.Network;
using RustlikeServer.World;

namespace RustlikeServer.Core
{
    public class ClientHandler
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private GameServer _server;
        private Player _player;
        private bool _isRunning;

        public ClientHandler(TcpClient client, GameServer server)
        {
            _client = client;
            _server = server;
            _stream = client.GetStream();
            _isRunning = true;
        }

        public async Task HandleClientAsync()
        {
            try
            {
                Console.WriteLine($"[ClientHandler] Novo cliente conectado: {_client.Client.RemoteEndPoint}");

                byte[] buffer = new byte[8192];
                
                while (_isRunning && _client.Connected)
                {
                    int bytesRead = await _stream.ReadAsync(buffer, 0, buffer.Length);
                    
                    if (bytesRead == 0)
                    {
                        Console.WriteLine("[ClientHandler] Cliente desconectado");
                        break;
                    }

                    byte[] receivedData = new byte[bytesRead];
                    Array.Copy(buffer, receivedData, bytesRead);
                    
                    await ProcessPacket(receivedData);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientHandler] Erro: {ex.Message}");
            }
            finally
            {
                Disconnect();
            }
        }

        private async Task ProcessPacket(byte[] data)
        {
            Packet packet = Packet.Deserialize(data);
            if (packet == null) return;

            switch (packet.Type)
            {
                case PacketType.ConnectionRequest:
                    await HandleConnectionRequest(packet.Data);
                    break;

                case PacketType.PlayerMovement:
                    HandlePlayerMovement(packet.Data);
                    break;

                case PacketType.Heartbeat:
                    HandleHeartbeat();
                    break;

                case PacketType.PlayerDisconnect:
                    Disconnect();
                    break;
            }
        }

        private async Task HandleConnectionRequest(byte[] data)
        {
            var request = ConnectionRequestPacket.Deserialize(data);
            Console.WriteLine($"[ClientHandler] Requisição de conexão de: {request.PlayerName}");

            // Cria o jogador no servidor
            _player = _server.CreatePlayer(request.PlayerName);

            // Envia resposta de aceitação
            var response = new ConnectionAcceptPacket
            {
                PlayerId = _player.Id,
                SpawnX = _player.Position.X,
                SpawnY = _player.Position.Y,
                SpawnZ = _player.Position.Z
            };

            await SendPacket(PacketType.ConnectionAccept, response.Serialize());
            Console.WriteLine($"[ClientHandler] Jogador {_player.Name} (ID: {_player.Id}) conectado e spawned");

            // Notifica outros jogadores sobre o novo jogador
            _server.BroadcastPlayerSpawn(_player);

            // Envia informações de jogadores existentes para o novo jogador
            await _server.SendExistingPlayersTo(this);
        }

        private void HandlePlayerMovement(byte[] data)
        {
            if (_player == null) return;

            var movement = PlayerMovementPacket.Deserialize(data);
            
            // Atualiza posição no servidor (server authoritative)
            _player.UpdatePosition(movement.PosX, movement.PosY, movement.PosZ);
            _player.UpdateRotation(movement.RotX, movement.RotY);
            _player.UpdateHeartbeat();

            // Broadcast para outros jogadores
            _server.BroadcastPlayerMovement(_player, this);
        }

        private void HandleHeartbeat()
        {
            if (_player != null)
            {
                _player.UpdateHeartbeat();
            }
        }

        public async Task SendPacket(PacketType type, byte[] data)
        {
            try
            {
                if (!_client.Connected) return;

                Packet packet = new Packet(type, data);
                byte[] serialized = packet.Serialize();
                await _stream.WriteAsync(serialized, 0, serialized.Length);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientHandler] Erro ao enviar pacote: {ex.Message}");
            }
        }

        public void Disconnect()
        {
            if (!_isRunning) return;

            _isRunning = false;

            if (_player != null)
            {
                Console.WriteLine($"[ClientHandler] Jogador {_player.Name} (ID: {_player.Id}) desconectado");
                _server.RemovePlayer(_player.Id);
            }

            _stream?.Close();
            _client?.Close();
        }

        public Player GetPlayer() => _player;
        public bool IsConnected() => _isRunning && _client.Connected;
    }
}