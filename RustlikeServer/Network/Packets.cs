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
        Heartbeat = 6
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

        // Serializa o pacote para envio pela rede
        public byte[] Serialize()
        {
            byte[] result = new byte[1 + 4 + Data.Length];
            result[0] = (byte)Type;
            BitConverter.GetBytes(Data.Length).CopyTo(result, 1);
            Data.CopyTo(result, 5);
            return result;
        }

        // Desserializa pacote recebido
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
}