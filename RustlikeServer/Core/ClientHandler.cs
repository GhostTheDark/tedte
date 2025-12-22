using System;
using System.Net.Sockets;
using System.Threading.Tasks;
using RustlikeServer.Network;
using RustlikeServer.World;
using RustlikeServer.Items;

namespace RustlikeServer.Core
{
    public class ClientHandler
    {
        private TcpClient _client;
        private NetworkStream _stream;
        private GameServer _server;
        private Player _player;
        private bool _isRunning;
        private bool _isFullyLoaded = false;

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

                case PacketType.ClientReady:
                    await HandleClientReady();
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

                // ⭐ NOVOS: Pacotes de inventário
                case PacketType.ItemUse:
                    await HandleItemUse(packet.Data);
                    break;

                case PacketType.ItemMove:
                    await HandleItemMove(packet.Data);
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

            _player = _server.CreatePlayer(request.PlayerName);
            Console.WriteLine($"[ClientHandler] Player criado com ID: {_player.Id}");

            _server.RegisterClient(_player.Id, this);
            Console.WriteLine($"[ClientHandler] ClientHandler registrado");

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
            Console.WriteLine($"[ClientHandler] ⏳ AGUARDANDO ClientReady do cliente...");
            Console.ResetColor();
        }

        private async Task HandleClientReady()
        {
            _isFullyLoaded = true;

            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n[ClientHandler] 📢 CLIENT READY RECEBIDO de {_player.Name} (ID: {_player.Id})");
            Console.WriteLine($"[ClientHandler] Cliente carregou completamente! Iniciando sincronização...");
            Console.ResetColor();

            await Task.Delay(150);

            // Envia inventário inicial
            await SendInventoryUpdate();

            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine($"[ClientHandler] 📤 Enviando players existentes para {_player.Name}...");
            Console.ResetColor();
            await _server.SendExistingPlayersTo(this);

            await Task.Delay(300);

            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine($"[ClientHandler] 📢 Broadcasting spawn de {_player.Name} para outros jogadores...");
            Console.ResetColor();
            _server.BroadcastPlayerSpawn(_player);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅✅✅ SINCRONIZAÇÃO COMPLETA: {_player.Name} (ID: {_player.Id})");
            Console.ResetColor();
            Console.WriteLine();
        }

        private void HandlePlayerMovement(byte[] data)
        {
            if (_player == null) return;

            var movement = PlayerMovementPacket.Deserialize(data);
            
            _player.UpdatePosition(movement.PosX, movement.PosY, movement.PosZ);
            _player.UpdateRotation(movement.RotX, movement.RotY);
            _player.UpdateHeartbeat();

            _server.BroadcastPlayerMovement(_player, this);
        }

        private void HandleHeartbeat()
        {
            if (_player != null)
            {
                _player.UpdateHeartbeat();
            }
        }

        // ⭐ NOVO: Usa item do inventário
        private async Task HandleItemUse(byte[] data)
        {
            if (_player == null) return;

            var packet = ItemUsePacket.Deserialize(data);
            Console.WriteLine($"[ClientHandler] 🎒 {_player.Name} usou item do slot {packet.SlotIndex}");

            // Consome o item
            var itemDef = _player.Inventory.ConsumeItem(packet.SlotIndex);
            if (itemDef == null)
            {
                Console.WriteLine($"[ClientHandler] ⚠️ Slot {packet.SlotIndex} vazio ou item não consumível");
                return;
            }

            // Aplica efeitos nas stats
            if (itemDef.HealthRestore > 0)
                _player.Stats.Heal(itemDef.HealthRestore);
            
            if (itemDef.HungerRestore > 0)
                _player.Stats.Eat(itemDef.HungerRestore);
            
            if (itemDef.ThirstRestore > 0)
                _player.Stats.Drink(itemDef.ThirstRestore);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅ Efeitos aplicados: HP+{itemDef.HealthRestore} Hunger+{itemDef.HungerRestore} Thirst+{itemDef.ThirstRestore}");
            Console.ResetColor();

            // Sincroniza inventário atualizado
            await SendInventoryUpdate();
        }

        // ⭐ NOVO: Move item entre slots
        private async Task HandleItemMove(byte[] data)
        {
            if (_player == null) return;

            var packet = ItemMovePacket.Deserialize(data);
            Console.WriteLine($"[ClientHandler] 🎒 {_player.Name} moveu item: {packet.FromSlot} → {packet.ToSlot}");

            bool success = _player.Inventory.MoveItem(packet.FromSlot, packet.ToSlot);
            if (success)
            {
                await SendInventoryUpdate();
            }
        }

        // ⭐ NOVO: Envia inventário completo para o cliente
        private async Task SendInventoryUpdate()
        {
            var inventoryPacket = new InventoryUpdatePacket();
            var slots = _player.Inventory.GetAllSlots();

            for (int i = 0; i < slots.Length; i++)
            {
                if (slots[i] != null)
                {
                    inventoryPacket.Slots.Add(new InventorySlotData
                    {
                        SlotIndex = i,
                        ItemId = slots[i].ItemId,
                        Quantity = slots[i].Quantity
                    });
                }
            }

            await SendPacket(PacketType.InventoryUpdate, inventoryPacket.Serialize());
            Console.WriteLine($"[ClientHandler] 📦 Inventário sincronizado: {inventoryPacket.Slots.Count} slots com itens");
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
        public bool IsFullyLoaded() => _isFullyLoaded;
    }
}