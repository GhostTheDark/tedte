using System;
using System.Threading.Tasks;
using LiteNetLib;
using RustlikeServer.Network;
using RustlikeServer.World;
using RustlikeServer.Items;

namespace RustlikeServer.Core
{
    /// <summary>
    /// ⭐ MIGRADO PARA LITENETLIB - Gerencia um cliente conectado
    /// </summary>
    public class ClientHandler
    {
        private NetPeer _peer;
        private GameServer _server;
        private Player _player;
        private bool _isRunning;
        private bool _isFullyLoaded = false;

        public ClientHandler(NetPeer peer, GameServer server)
        {
            _peer = peer;
            _server = server;
            _isRunning = true;

            Console.WriteLine($"[ClientHandler] Novo ClientHandler criado para Peer ID: {peer.Id}");
        }

        public async Task ProcessPacketAsync(byte[] data)
        {
            try
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

                    case PacketType.ItemUse:
                        await HandleItemUse(packet.Data);
                        break;

                    case PacketType.ItemMove:
                        await HandleItemMove(packet.Data);
                        break;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ClientHandler] Erro ao processar pacote: {ex.Message}");
            }
        }

        private async Task HandleConnectionRequest(byte[] data)
        {
            var request = ConnectionRequestPacket.Deserialize(data);
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"\n[ClientHandler] ===== NOVA CONEXÃO =====");
            Console.WriteLine($"[ClientHandler] Nome: {request.PlayerName}");
            Console.WriteLine($"[ClientHandler] Peer ID: {_peer.Id}");
            Console.WriteLine($"[ClientHandler] Endpoint: {_peer.Address}:{_peer.Port}");
            Console.ResetColor();

            _player = _server.CreatePlayer(request.PlayerName);
            Console.WriteLine($"[ClientHandler] Player criado com ID: {_player.Id}");

            _server.RegisterClient(_player.Id, _peer, this);
            Console.WriteLine($"[ClientHandler] ClientHandler registrado");

            var response = new ConnectionAcceptPacket
            {
                PlayerId = _player.Id,
                SpawnX = _player.Position.X,
                SpawnY = _player.Position.Y,
                SpawnZ = _player.Position.Z
            };

            SendPacket(PacketType.ConnectionAccept, response.Serialize());
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅ ConnectionAccept ENVIADO para {_player.Name} (ID: {_player.Id})");
            Console.WriteLine($"[ClientHandler] ⏳ AGUARDANDO ClientReady do cliente...");
            Console.ResetColor();

            await Task.CompletedTask;
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

        private async Task HandleItemUse(byte[] data)
        {
            if (_player == null) return;

            var packet = ItemUsePacket.Deserialize(data);
            Console.WriteLine($"[ClientHandler] 🎒 {_player.Name} usou item do slot {packet.SlotIndex}");

            var itemDef = _player.Inventory.ConsumeItem(packet.SlotIndex);
            if (itemDef == null)
            {
                Console.WriteLine($"[ClientHandler] ⚠️ Slot {packet.SlotIndex} vazio ou item não consumível");
                return;
            }

            if (itemDef.HealthRestore > 0)
                _player.Stats.Heal(itemDef.HealthRestore);
            
            if (itemDef.HungerRestore > 0)
                _player.Stats.Eat(itemDef.HungerRestore);
            
            if (itemDef.ThirstRestore > 0)
                _player.Stats.Drink(itemDef.ThirstRestore);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"[ClientHandler] ✅ Efeitos aplicados: HP+{itemDef.HealthRestore} Hunger+{itemDef.HungerRestore} Thirst+{itemDef.ThirstRestore}");
            Console.ResetColor();

            await SendInventoryUpdate();
        }

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

            SendPacket(PacketType.InventoryUpdate, inventoryPacket.Serialize());
            Console.WriteLine($"[ClientHandler] 📦 Inventário sincronizado: {inventoryPacket.Slots.Count} slots com itens");

            await Task.CompletedTask;
        }

        /// <summary>
        /// ⭐ NOVO: Envia pacote via LiteNetLib
        /// </summary>
        public void SendPacket(PacketType type, byte[] data, LiteNetLib.DeliveryMethod method = LiteNetLib.DeliveryMethod.ReliableOrdered)
        {
            try
            {
                if (_peer == null || _peer.ConnectionState != ConnectionState.Connected)
                    return;

                _server.SendPacket(_peer, type, data, method);
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

            if (_peer != null && _peer.ConnectionState == ConnectionState.Connected)
            {
                _peer.Disconnect();
            }
        }

        public Player GetPlayer() => _player;
        public NetPeer GetPeer() => _peer;
        public bool IsConnected() => _isRunning && _peer != null && _peer.ConnectionState == ConnectionState.Connected;
        public bool IsFullyLoaded() => _isFullyLoaded;
    }
}