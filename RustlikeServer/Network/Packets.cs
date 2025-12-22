using System;
using System.Text;

namespace RustlikeServer.Network
{
    // Tipos de pacotes
    public enum PacketType : byte
    {
        ConnectionRequest = 0,
        ConnectionAccept = 1,
        PlayerSpawn = 2,
        PlayerMovement = 3,
        PlayerDisconnect = 4,
        WorldState = 5,
        Heartbeat = 6,
        ClientReady = 7,
        
        // ⭐ NOVOS: Sistema de Stats
        StatsUpdate = 8,        // Servidor -> Cliente (sync de stats)
        PlayerDeath = 9,        // Servidor -> Todos (notifica morte)
        PlayerRespawn = 10,     // Cliente -> Servidor (solicita respawn)
        TakeDamage = 11,        // Servidor -> Cliente (feedback visual de dano)
        ConsumeItem = 12,       // Cliente -> Servidor (usa item de consumo)
        
        // ⭐ NOVOS: Sistema de Inventário
        InventoryUpdate = 13,   // Servidor -> Cliente (sync completo do inventário)
        ItemUse = 14,           // Cliente -> Servidor (usa item do slot)
        ItemMove = 15,          // Cliente -> Servidor (move item entre slots)
        ItemDrop = 16,          // Cliente -> Servidor (dropa item no chão)
        HotbarSelect = 17       // Cliente -> Servidor (seleciona slot da hotbar)
    }

    // Classe base para serialização de pacotes
    public class Packet
    {
        public PacketType Type { get; set; }
        public byte[] Data { get; set; }

        public Packet(PacketType type, byte[] data)
        {
            Type = type;
            Data = data;
        }

        public byte[] Serialize()
        {
            byte[] result = new byte[1 + 4 + Data.Length];
            result[0] = (byte)Type;
            BitConverter.GetBytes(Data.Length).CopyTo(result, 1);
            Data.CopyTo(result, 5);
            return result;
        }

        public static Packet Deserialize(byte[] data)
        {
            if (data.Length < 5) return null;
            
            PacketType type = (PacketType)data[0];
            int dataLength = BitConverter.ToInt32(data, 1);
            byte[] packetData = new byte[dataLength];
            Array.Copy(data, 5, packetData, 0, dataLength);
            
            return new Packet(type, packetData);
        }
    }

    // Pacote de requisição de conexão
    public class ConnectionRequestPacket
    {
        public string PlayerName { get; set; }

        public byte[] Serialize()
        {
            return Encoding.UTF8.GetBytes(PlayerName);
        }

        public static ConnectionRequestPacket Deserialize(byte[] data)
        {
            return new ConnectionRequestPacket
            {
                PlayerName = Encoding.UTF8.GetString(data)
            };
        }
    }

    // Pacote de aceitação de conexão
    public class ConnectionAcceptPacket
    {
        public int PlayerId { get; set; }
        public float SpawnX { get; set; }
        public float SpawnY { get; set; }
        public float SpawnZ { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[16];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(SpawnX).CopyTo(data, 4);
            BitConverter.GetBytes(SpawnY).CopyTo(data, 8);
            BitConverter.GetBytes(SpawnZ).CopyTo(data, 12);
            return data;
        }

        public static ConnectionAcceptPacket Deserialize(byte[] data)
        {
            return new ConnectionAcceptPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                SpawnX = BitConverter.ToSingle(data, 4),
                SpawnY = BitConverter.ToSingle(data, 8),
                SpawnZ = BitConverter.ToSingle(data, 12)
            };
        }
    }

    // Pacote de movimento do jogador
    public class PlayerMovementPacket
    {
        public int PlayerId { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }
        public float RotX { get; set; }
        public float RotY { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[24];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(PosX).CopyTo(data, 4);
            BitConverter.GetBytes(PosY).CopyTo(data, 8);
            BitConverter.GetBytes(PosZ).CopyTo(data, 12);
            BitConverter.GetBytes(RotX).CopyTo(data, 16);
            BitConverter.GetBytes(RotY).CopyTo(data, 20);
            return data;
        }

        public static PlayerMovementPacket Deserialize(byte[] data)
        {
            return new PlayerMovementPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                PosX = BitConverter.ToSingle(data, 4),
                PosY = BitConverter.ToSingle(data, 8),
                PosZ = BitConverter.ToSingle(data, 12),
                RotX = BitConverter.ToSingle(data, 16),
                RotY = BitConverter.ToSingle(data, 20)
            };
        }
    }

    // Pacote de spawn de jogador
    public class PlayerSpawnPacket
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; }
        public float PosX { get; set; }
        public float PosY { get; set; }
        public float PosZ { get; set; }

        public byte[] Serialize()
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(PlayerName);
            byte[] data = new byte[20 + nameBytes.Length];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(nameBytes.Length).CopyTo(data, 4);
            nameBytes.CopyTo(data, 8);
            BitConverter.GetBytes(PosX).CopyTo(data, 8 + nameBytes.Length);
            BitConverter.GetBytes(PosY).CopyTo(data, 12 + nameBytes.Length);
            BitConverter.GetBytes(PosZ).CopyTo(data, 16 + nameBytes.Length);
            return data;
        }

        public static PlayerSpawnPacket Deserialize(byte[] data)
        {
            int nameLength = BitConverter.ToInt32(data, 4);
            return new PlayerSpawnPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                PlayerName = Encoding.UTF8.GetString(data, 8, nameLength),
                PosX = BitConverter.ToSingle(data, 8 + nameLength),
                PosY = BitConverter.ToSingle(data, 12 + nameLength),
                PosZ = BitConverter.ToSingle(data, 16 + nameLength)
            };
        }
    }

    // ⭐ NOVO: Pacote de atualização de stats
    public class StatsUpdatePacket
    {
        public int PlayerId { get; set; }
        public float Health { get; set; }
        public float Hunger { get; set; }
        public float Thirst { get; set; }
        public float Temperature { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[20]; // 5 x float (4 bytes each)
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(Health).CopyTo(data, 4);
            BitConverter.GetBytes(Hunger).CopyTo(data, 8);
            BitConverter.GetBytes(Thirst).CopyTo(data, 12);
            BitConverter.GetBytes(Temperature).CopyTo(data, 16);
            return data;
        }

        public static StatsUpdatePacket Deserialize(byte[] data)
        {
            return new StatsUpdatePacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                Health = BitConverter.ToSingle(data, 4),
                Hunger = BitConverter.ToSingle(data, 8),
                Thirst = BitConverter.ToSingle(data, 12),
                Temperature = BitConverter.ToSingle(data, 16)
            };
        }
    }

    // ⭐ NOVO: Pacote de morte de jogador
    public class PlayerDeathPacket
    {
        public int PlayerId { get; set; }
        public string KillerName { get; set; } // Vazio se morreu por fome/sede/etc

        public byte[] Serialize()
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(KillerName ?? "");
            byte[] data = new byte[8 + nameBytes.Length];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(nameBytes.Length).CopyTo(data, 4);
            nameBytes.CopyTo(data, 8);
            return data;
        }

        public static PlayerDeathPacket Deserialize(byte[] data)
        {
            int nameLength = BitConverter.ToInt32(data, 4);
            return new PlayerDeathPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                KillerName = nameLength > 0 ? Encoding.UTF8.GetString(data, 8, nameLength) : ""
            };
        }
    }

    // ⭐ NOVO: Pacote de consumo de item (comer/beber)
    public class ConsumeItemPacket
    {
        public int ItemId { get; set; }
        public ConsumeType Type { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[5];
            BitConverter.GetBytes(ItemId).CopyTo(data, 0);
            data[4] = (byte)Type;
            return data;
        }

        public static ConsumeItemPacket Deserialize(byte[] data)
        {
            return new ConsumeItemPacket
            {
                ItemId = BitConverter.ToInt32(data, 0),
                Type = (ConsumeType)data[4]
            };
        }
    }

    public enum ConsumeType : byte
    {
        Food = 0,
        Water = 1,
        Medicine = 2
    }

    // ⭐ NOVO: Pacote de atualização completa do inventário
    public class InventoryUpdatePacket
    {
        public List<InventorySlotData> Slots { get; set; }

        public InventoryUpdatePacket()
        {
            Slots = new List<InventorySlotData>();
        }

        public byte[] Serialize()
        {
            // Formato: [SlotCount(4)] + [Slot1] + [Slot2] + ...
            // Cada slot: [Index(4)] + [ItemId(4)] + [Quantity(4)]
            
            List<byte> data = new List<byte>();
            data.AddRange(BitConverter.GetBytes(Slots.Count));

            foreach (var slot in Slots)
            {
                data.AddRange(BitConverter.GetBytes(slot.SlotIndex));
                data.AddRange(BitConverter.GetBytes(slot.ItemId));
                data.AddRange(BitConverter.GetBytes(slot.Quantity));
            }

            return data.ToArray();
        }

        public static InventoryUpdatePacket Deserialize(byte[] data)
        {
            var packet = new InventoryUpdatePacket();
            int offset = 0;

            int slotCount = BitConverter.ToInt32(data, offset);
            offset += 4;

            for (int i = 0; i < slotCount; i++)
            {
                int slotIndex = BitConverter.ToInt32(data, offset);
                offset += 4;
                int itemId = BitConverter.ToInt32(data, offset);
                offset += 4;
                int quantity = BitConverter.ToInt32(data, offset);
                offset += 4;

                packet.Slots.Add(new InventorySlotData
                {
                    SlotIndex = slotIndex,
                    ItemId = itemId,
                    Quantity = quantity
                });
            }

            return packet;
        }
    }

    public class InventorySlotData
    {
        public int SlotIndex { get; set; }
        public int ItemId { get; set; }
        public int Quantity { get; set; }
    }

    // ⭐ NOVO: Pacote de uso de item
    public class ItemUsePacket
    {
        public int SlotIndex { get; set; }

        public byte[] Serialize()
        {
            return BitConverter.GetBytes(SlotIndex);
        }

        public static ItemUsePacket Deserialize(byte[] data)
        {
            return new ItemUsePacket
            {
                SlotIndex = BitConverter.ToInt32(data, 0)
            };
        }
    }

    // ⭐ NOVO: Pacote de mover item
    public class ItemMovePacket
    {
        public int FromSlot { get; set; }
        public int ToSlot { get; set; }

        public byte[] Serialize()
        {
            byte[] data = new byte[8];
            BitConverter.GetBytes(FromSlot).CopyTo(data, 0);
            BitConverter.GetBytes(ToSlot).CopyTo(data, 4);
            return data;
        }

        public static ItemMovePacket Deserialize(byte[] data)
        {
            return new ItemMovePacket
            {
                FromSlot = BitConverter.ToInt32(data, 0),
                ToSlot = BitConverter.ToInt32(data, 4)
            };
        }
    }
}