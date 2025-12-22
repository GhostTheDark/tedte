using System;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

namespace RustlikeClient.Network
{
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
        StatsUpdate = 8,
        PlayerDeath = 9,
        PlayerRespawn = 10,
        TakeDamage = 11,
        ConsumeItem = 12,
        
        // ⭐ NOVOS: Sistema de Inventário
        InventoryUpdate = 13,
        ItemUse = 14,
        ItemMove = 15,
        ItemDrop = 16,
        HotbarSelect = 17
    }

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

    [Serializable]
    public class ConnectionRequestPacket
    {
        public string PlayerName;

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

    [Serializable]
    public class ConnectionAcceptPacket
    {
        public int PlayerId;
        public Vector3 SpawnPosition;

        public byte[] Serialize()
        {
            byte[] data = new byte[16];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(SpawnPosition.x).CopyTo(data, 4);
            BitConverter.GetBytes(SpawnPosition.y).CopyTo(data, 8);
            BitConverter.GetBytes(SpawnPosition.z).CopyTo(data, 12);
            return data;
        }

        public static ConnectionAcceptPacket Deserialize(byte[] data)
        {
            return new ConnectionAcceptPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                SpawnPosition = new Vector3(
                    BitConverter.ToSingle(data, 4),
                    BitConverter.ToSingle(data, 8),
                    BitConverter.ToSingle(data, 12)
                )
            };
        }
    }

    [Serializable]
    public class PlayerMovementPacket
    {
        public int PlayerId;
        public Vector3 Position;
        public Vector2 Rotation;

        public byte[] Serialize()
        {
            byte[] data = new byte[24];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(Position.x).CopyTo(data, 4);
            BitConverter.GetBytes(Position.y).CopyTo(data, 8);
            BitConverter.GetBytes(Position.z).CopyTo(data, 12);
            BitConverter.GetBytes(Rotation.x).CopyTo(data, 16);
            BitConverter.GetBytes(Rotation.y).CopyTo(data, 20);
            return data;
        }

        public static PlayerMovementPacket Deserialize(byte[] data)
        {
            return new PlayerMovementPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                Position = new Vector3(
                    BitConverter.ToSingle(data, 4),
                    BitConverter.ToSingle(data, 8),
                    BitConverter.ToSingle(data, 12)
                ),
                Rotation = new Vector2(
                    BitConverter.ToSingle(data, 16),
                    BitConverter.ToSingle(data, 20)
                )
            };
        }
    }

    [Serializable]
    public class PlayerSpawnPacket
    {
        public int PlayerId;
        public string PlayerName;
        public Vector3 Position;

        public byte[] Serialize()
        {
            byte[] nameBytes = Encoding.UTF8.GetBytes(PlayerName);
            byte[] data = new byte[20 + nameBytes.Length];
            BitConverter.GetBytes(PlayerId).CopyTo(data, 0);
            BitConverter.GetBytes(nameBytes.Length).CopyTo(data, 4);
            nameBytes.CopyTo(data, 8);
            BitConverter.GetBytes(Position.x).CopyTo(data, 8 + nameBytes.Length);
            BitConverter.GetBytes(Position.y).CopyTo(data, 12 + nameBytes.Length);
            BitConverter.GetBytes(Position.z).CopyTo(data, 16 + nameBytes.Length);
            return data;
        }

        public static PlayerSpawnPacket Deserialize(byte[] data)
        {
            int nameLength = BitConverter.ToInt32(data, 4);
            return new PlayerSpawnPacket
            {
                PlayerId = BitConverter.ToInt32(data, 0),
                PlayerName = Encoding.UTF8.GetString(data, 8, nameLength),
                Position = new Vector3(
                    BitConverter.ToSingle(data, 8 + nameLength),
                    BitConverter.ToSingle(data, 12 + nameLength),
                    BitConverter.ToSingle(data, 16 + nameLength)
                )
            };
        }
    }

    // ⭐ NOVO: Pacote de atualização de stats
    [Serializable]
    public class StatsUpdatePacket
    {
        public int PlayerId;
        public float Health;
        public float Hunger;
        public float Thirst;
        public float Temperature;

        public byte[] Serialize()
        {
            byte[] data = new byte[20];
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
    [Serializable]
    public class PlayerDeathPacket
    {
        public int PlayerId;
        public string KillerName;

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

    // ⭐ NOVO: Pacote de atualização de inventário
    [Serializable]
    public class InventoryUpdatePacket
    {
        public List<InventorySlotData> Slots;

        public InventoryUpdatePacket()
        {
            Slots = new List<InventorySlotData>();
        }

        public byte[] Serialize()
        {
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

    [Serializable]
    public class InventorySlotData
    {
        public int SlotIndex;
        public int ItemId;
        public int Quantity;
    }

    // ⭐ NOVO: Pacote de uso de item
    [Serializable]
    public class ItemUsePacket
    {
        public int SlotIndex;

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
    [Serializable]
    public class ItemMovePacket
    {
        public int FromSlot;
        public int ToSlot;

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