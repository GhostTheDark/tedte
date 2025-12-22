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
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[ClientHandler] ===== NOVA CONEXÃO =====");
            Console.WriteLine($"[ClientHandler] Nome: {request.PlayerName}");
            Console.ResetColor();

            // 1. Cria o jogador no servidor
            _player = _server.CreatePlayer(request.PlayerName);
            Console.WriteLine($"[ClientHandler] Player criado com ID: {_player.Id}");

            // 2. Registra este ClientHandler no servidor
            _server.RegisterClient(_player.Id, this);
            Console.WriteLine($"[ClientHandler] ClientHandler registrado");

            // 3. Envia resposta de aceitação
            var response = new ConnectionAcceptPacket
            {
                PlayerId = _player.Id,
                SpawnX = _player.Position.X,
                SpawnY = _player.Position.Y,
                SpawnZ = _player.Position.Z
            };

            await SendPacket(PacketType.ConnectionAccept, response.Serialize());
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅ ConnectionAccept ENVIADO para {_player.Name} (ID: {_player.Id})");
            Console.ResetColor();

            // ⭐⭐⭐ CRÍTICO: AGUARDA o cliente pausar o processamento ⭐⭐⭐
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"[ClientHandler] ⏳ Aguardando 200ms para cliente pausar processamento...");
            Console.ResetColor();
            await Task.Delay(200); // ⭐ AUMENTADO para 200ms

            // 4. Envia informações de jogadores existentes
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[ClientHandler] 📤 Enviando players existentes para {_player.Name}...");
            Console.ResetColor();
            await _server.SendExistingPlayersTo(this);

            // ⭐ Delay adicional para garantir que todos os spawns foram enviados
            await Task.Delay(100);

            // 5. Notifica outros jogadores sobre o novo jogador
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[ClientHandler] 📢 Broadcasting spawn de {_player.Name} para outros jogadores...");
            Console.ResetColor();
            _server.BroadcastPlayerSpawn(_player);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅✅✅ CONEXÃO COMPLETA: {_player.Name} (ID: {_player.Id})");
            Console.ResetColor();
            Console.WriteLine();
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
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[ClientHandler] ❌ Erro ao enviar pacote: {ex.Message}");
                Console.ResetColor();
            }
        }

        public void Disconnect()
        {
            if (!_isRunning) return;

            _isRunning = false;

            if (_player != null)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"\n[ClientHandler] ❌ Jogador {_player.Name} (ID: {_player.Id}) desconectado");
                Console.ResetColor();
                _server.RemovePlayer(_player.Id);
            }

            _stream?.Close();
            _client?.Close();
        }

        public Player GetPlayer() => _player;
        public bool IsConnected() => _isRunning && _client.Connected;
    }
}