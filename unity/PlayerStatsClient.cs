using UnityEngine;

namespace RustlikeClient.Player
{
    /// <summary>
    /// Armazena as stats do jogador no cliente (recebe do servidor)
    /// </summary>
    public class PlayerStatsClient : MonoBehaviour
    {
        [Header("Current Stats (Read Only)")]
        [SerializeField] private float _health = 100f;
        [SerializeField] private float _hunger = 100f;
        [SerializeField] private float _thirst = 100f;
        [SerializeField] private float _temperature = 20f;

        public float Health => _health;
        public float Hunger => _hunger;
        public float Thirst => _thirst;
        public float Temperature => _temperature;

        private void Start()
        {
            // Atualiza UI inicial
            UpdateUI();
        }

        /// <summary>
        /// Atualiza stats (chamado pelo NetworkManager quando recebe StatsUpdate)
        /// </summary>
        public void UpdateStats(float health, float hunger, float thirst, float temperature)
        {
            bool healthChanged = !Mathf.Approximately(_health, health);
            bool tookDamage = health < _health;

            _health = health;
            _hunger = hunger;
            _thirst = thirst;
            _temperature = temperature;

            // Atualiza UI
            UpdateUI();

            // Efeito visual de dano
            if (tookDamage && healthChanged && UI.StatsUI.Instance != null)
            {
                float damageAmount = Mathf.Abs(_health - health);
                float intensity = Mathf.Clamp01(damageAmount / 100f);
                UI.StatsUI.Instance.ShowDamageEffect(intensity);
            }

            // Log quando stats estão críticas
            if (_hunger < 20f && Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("[PlayerStats] ⚠️ FOME CRÍTICA!");
            }

            if (_thirst < 20f && Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("[PlayerStats] ⚠️ SEDE CRÍTICA!");
            }

            if (_health < 30f && Time.frameCount % 300 == 0)
            {
                Debug.LogWarning("[PlayerStats] ⚠️ SAÚDE CRÍTICA!");
            }
        }

        private void UpdateUI()
        {
            if (UI.StatsUI.Instance != null)
            {
                UI.StatsUI.Instance.UpdateStats(_health, _hunger, _thirst, _temperature);
            }
        }

        /// <summary>
        /// Verifica se está vivo
        /// </summary>
        public bool IsAlive()
        {
            return _health > 0;
        }

        /// <summary>
        /// Verifica se está com fome
        /// </summary>
        public bool IsHungry()
        {
            return _hunger < 50f;
        }

        /// <summary>
        /// Verifica se está com sede
        /// </summary>
        public bool IsThirsty()
        {
            return _thirst < 50f;
        }

        /// <summary>
        /// Verifica se está com frio
        /// </summary>
        public bool IsCold()
        {
            return _temperature < 10f;
        }

        /// <summary>
        /// Verifica se está com calor
        /// </summary>
        public bool IsHot()
        {
            return _temperature > 30f;
        }

        /// <summary>
        /// Para debug no Inspector
        /// </summary>
        private void OnGUI()
        {
            if (Input.GetKey(KeyCode.F2))
            {
                GUI.Box(new Rect(10, 100, 200, 120), "Player Stats (F2)");
                GUI.Label(new Rect(20, 125, 180, 20), $"Health: {_health:F1}");
                GUI.Label(new Rect(20, 145, 180, 20), $"Hunger: {_hunger:F1}");
                GUI.Label(new Rect(20, 165, 180, 20), $"Thirst: {_thirst:F1}");
                GUI.Label(new Rect(20, 185, 180, 20), $"Temperature: {_temperature:F1}°C");
            }
        }
    }
}