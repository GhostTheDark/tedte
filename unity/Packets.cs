using System;
using System.Text;
using UnityEngine;

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
        Heartbeat = 6
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
}