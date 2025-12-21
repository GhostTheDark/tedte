using System;

namespace RustlikeServer.World
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public Vector3 Position { get; set; }
        public Vector2 Rotation { get; set; } // X = Yaw, Y = Pitch
        public DateTime LastHeartbeat { get; set; }
        public bool IsConnected { get; set; }

        public Player(int id, string name)
        {
            Id = id;
            Name = name;
            Position = new Vector3(0, 1, 0); // Spawn inicial
            Rotation = new Vector2(0, 0);
            LastHeartbeat = DateTime.Now;
            IsConnected = true;
        }

        public void UpdatePosition(float x, float y, float z)
        {
            Position = new Vector3(x, y, z);
        }

        public void UpdateRotation(float yaw, float pitch)
        {
            Rotation = new Vector2(yaw, pitch);
        }

        public void UpdateHeartbeat()
        {
            LastHeartbeat = DateTime.Now;
        }

        public bool IsTimedOut()
        {
            return (DateTime.Now - LastHeartbeat).TotalSeconds > 10;
        }
    }

    // Estruturas auxiliares
    public struct Vector3
    {
        public float X { get; set; }
        public float Y { get; set; }
        public float Z { get; set; }

        public Vector3(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public override string ToString()
        {
            return $"({X:F2}, {Y:F2}, {Z:F2})";
        }
    }

    public struct Vector2
    {
        public float X { get; set; }
        public float Y { get; set; }

        public Vector2(float x, float y)
        {
            X = x;
            Y = y;
        }
    }
}