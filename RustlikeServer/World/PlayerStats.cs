using System;
using System.Collections.Generic;

namespace RustlikeServer.World
{
    /// <summary>
    /// Sistema de estatísticas de sobrevivência do jogador (Server Authoritative)
    /// </summary>
    public class PlayerStats
    {
        // Constantes de configuração
        private const float MAX_HEALTH = 100f;
        private const float MAX_HUNGER = 100f;
        private const float MAX_THIRST = 100f;
        private const float MIN_TEMPERATURE = -50f;
        private const float MAX_TEMPERATURE = 50f;
        private const float NORMAL_TEMPERATURE = 20f;

        // Stats principais
        public float Health { get; private set; }
        public float Hunger { get; private set; }
        public float Thirst { get; private set; }
        public float Temperature { get; private set; }
        public bool IsDead { get; private set; }

        // Rates de decaimento (por segundo)
        private const float HUNGER_DECAY_RATE = 0.5f;  // Perde 0.5 de fome por segundo
        private const float THIRST_DECAY_RATE = 0.8f;  // Perde 0.8 de sede por segundo (mais rápido)
        
        // Dano por stats baixos
        private const float HUNGER_DAMAGE_RATE = 2f;   // 2 HP/s quando fome está zerada
        private const float THIRST_DAMAGE_RATE = 5f;   // 5 HP/s quando sede está zerada
        private const float COLD_DAMAGE_RATE = 3f;     // 3 HP/s em frio extremo
        private const float HOT_DAMAGE_RATE = 4f;      // 4 HP/s em calor extremo

        // Regeneração natural
        private const float HEALTH_REGEN_RATE = 1f;    // 1 HP/s quando bem nutrido
        private const float REGEN_HUNGER_THRESHOLD = 50f;  // Precisa ter mais de 50% de fome
        private const float REGEN_THIRST_THRESHOLD = 50f;  // Precisa ter mais de 50% de sede

        private DateTime _lastUpdate;

        public PlayerStats()
        {
            Health = MAX_HEALTH;
            Hunger = MAX_HUNGER;
            Thirst = MAX_THIRST;
            Temperature = NORMAL_TEMPERATURE;
            IsDead = false;
            _lastUpdate = DateTime.Now;
        }

        /// <summary>
        /// Atualiza todas as stats com base no tempo decorrido
        /// </summary>
        public void Update()
        {
            if (IsDead) return;

            DateTime now = DateTime.Now;
            float deltaTime = (float)(now - _lastUpdate).TotalSeconds;
            _lastUpdate = now;

            // Atualiza stats de sobrevivência
            UpdateHunger(deltaTime);
            UpdateThirst(deltaTime);
            UpdateTemperature(deltaTime);
            UpdateHealth(deltaTime);

            // Verifica morte
            if (Health <= 0)
            {
                Die();
            }
        }

        private void UpdateHunger(float deltaTime)
        {
            Hunger = Math.Max(0, Hunger - (HUNGER_DECAY_RATE * deltaTime));

            // Dano por fome
            if (Hunger <= 0)
            {
                TakeDamage(HUNGER_DAMAGE_RATE * deltaTime, DamageType.Hunger);
            }
        }

        private void UpdateThirst(float deltaTime)
        {
            Thirst = Math.Max(0, Thirst - (THIRST_DECAY_RATE * deltaTime));

            // Dano por sede
            if (Thirst <= 0)
            {
                TakeDamage(THIRST_DAMAGE_RATE * deltaTime, DamageType.Thirst);
            }
        }

        private void UpdateTemperature(float deltaTime)
        {
            // Por enquanto, temperatura tende à normal (20°C)
            // Futuramente será afetada por bioma, clima, roupas, etc.
            float targetTemp = NORMAL_TEMPERATURE;
            Temperature = Mathf.Lerp(Temperature, targetTemp, deltaTime * 0.1f);

            // Dano por temperatura extrema
            if (Temperature < 0)
            {
                TakeDamage(COLD_DAMAGE_RATE * deltaTime, DamageType.Cold);
            }
            else if (Temperature > 40)
            {
                TakeDamage(HOT_DAMAGE_RATE * deltaTime, DamageType.Heat);
            }
        }

        private void UpdateHealth(float deltaTime)
        {
            // Regeneração natural quando bem nutrido
            if (Hunger > REGEN_HUNGER_THRESHOLD && Thirst > REGEN_THIRST_THRESHOLD)
            {
                Health = Math.Min(MAX_HEALTH, Health + (HEALTH_REGEN_RATE * deltaTime));
            }
        }

        /// <summary>
        /// Aplica dano ao jogador
        /// </summary>
        public void TakeDamage(float amount, DamageType type = DamageType.Generic)
        {
            if (IsDead) return;

            Health = Math.Max(0, Health - amount);
            Console.WriteLine($"[PlayerStats] Dano recebido: {amount:F1} ({type}) | Health: {Health:F1}");

            if (Health <= 0)
            {
                Die();
            }
        }

        /// <summary>
        /// Cura o jogador
        /// </summary>
        public void Heal(float amount)
        {
            if (IsDead) return;
            Health = Math.Min(MAX_HEALTH, Health + amount);
        }

        /// <summary>
        /// Aumenta fome (comer)
        /// </summary>
        public void Eat(float amount)
        {
            if (IsDead) return;
            Hunger = Math.Min(MAX_HUNGER, Hunger + amount);
        }

        /// <summary>
        /// Aumenta sede (beber)
        /// </summary>
        public void Drink(float amount)
        {
            if (IsDead) return;
            Thirst = Math.Min(MAX_THIRST, Thirst + amount);
        }

        /// <summary>
        /// Define temperatura (será usado por sistema de clima/bioma)
        /// </summary>
        public void SetTemperature(float temp)
        {
            Temperature = Math.Clamp(temp, MIN_TEMPERATURE, MAX_TEMPERATURE);
        }

        private void Die()
        {
            IsDead = true;
            Health = 0;
            Console.WriteLine($"[PlayerStats] ☠️ Jogador MORREU!");
        }

        /// <summary>
        /// Respawn do jogador (reseta stats)
        /// </summary>
        public void Respawn()
        {
            Health = MAX_HEALTH;
            Hunger = MAX_HUNGER;
            Thirst = MAX_THIRST;
            Temperature = NORMAL_TEMPERATURE;
            IsDead = false;
            _lastUpdate = DateTime.Now;
            Console.WriteLine($"[PlayerStats] ♻️ Jogador RESPAWNOU!");
        }

        /// <summary>
        /// Serializa stats para envio ao cliente
        /// </summary>
        public byte[] Serialize()
        {
            byte[] data = new byte[17]; // 4 floats + 1 bool
            BitConverter.GetBytes(Health).CopyTo(data, 0);
            BitConverter.GetBytes(Hunger).CopyTo(data, 4);
            BitConverter.GetBytes(Thirst).CopyTo(data, 8);
            BitConverter.GetBytes(Temperature).CopyTo(data, 12);
            data[16] = IsDead ? (byte)1 : (byte)0;
            return data;
        }

        public static PlayerStats Deserialize(byte[] data)
        {
            var stats = new PlayerStats
            {
                Health = BitConverter.ToSingle(data, 0),
                Hunger = BitConverter.ToSingle(data, 4),
                Thirst = BitConverter.ToSingle(data, 8),
                Temperature = BitConverter.ToSingle(data, 12),
                IsDead = data[16] == 1
            };
            return stats;
        }

        public override string ToString()
        {
            return $"HP:{Health:F0} Hunger:{Hunger:F0} Thirst:{Thirst:F0} Temp:{Temperature:F0}°C";
        }
    }

    /// <summary>
    /// Tipos de dano para logs e futuros sistemas de resistência
    /// </summary>
    public enum DamageType
    {
        Generic,
        Melee,
        Bullet,
        Explosion,
        Fall,
        Hunger,
        Thirst,
        Cold,
        Heat,
        Radiation,
        Bleeding
    }

    // Classe auxiliar para Mathf (não existe em C# puro)
    public static class Mathf
    {
        public static float Lerp(float a, float b, float t)
        {
            return a + (b - a) * Math.Clamp(t, 0f, 1f);
        }

        public static float Clamp(float value, float min, float max)
        {
            return Math.Max(min, Math.Min(max, value));
        }
    }
}