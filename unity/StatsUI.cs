using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace RustlikeClient.UI
{
    /// <summary>
    /// UI que exibe as stats de sobrevivência do jogador usando SLIDERS (estilo Rust)
    /// </summary>
    public class StatsUI : MonoBehaviour
    {
        public static StatsUI Instance { get; private set; }

        [Header("Health")]
        public Slider healthSlider;
        public TextMeshProUGUI healthText;
        public Image healthFill; // A imagem Fill do slider

        [Header("Hunger")]
        public Slider hungerSlider;
        public TextMeshProUGUI hungerText;
        public Image hungerFill;

        [Header("Thirst")]
        public Slider thirstSlider;
        public TextMeshProUGUI thirstText;
        public Image thirstFill;

        [Header("Temperature")]
        public Slider temperatureSlider;
        public TextMeshProUGUI temperatureText;
        public Image temperatureFill;
        public GameObject coldIndicator;
        public GameObject hotIndicator;

        [Header("Visual Effects")]
        public Image damageVignette; // Efeito vermelho nas bordas quando toma dano
        public float vignetteDecaySpeed = 2f;

        [Header("Colors")]
        public Color healthColor = new Color(0f, 0.8f, 0f);
        public Color healthLowColor = Color.red;
        public Color hungerColor = new Color(1f, 0.6f, 0f);
        public Color thirstColor = new Color(0f, 0.6f, 1f);
        public Color tempColdColor = new Color(0.3f, 0.5f, 1f);
        public Color tempNormalColor = Color.white;
        public Color tempHotColor = new Color(1f, 0.3f, 0f);

        private float _currentVignetteAlpha;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            
            // Configura sliders
            ConfigureSliders();
            
            // Inicializa vignette invisível
            if (damageVignette != null)
            {
                Color c = damageVignette.color;
                c.a = 0f;
                damageVignette.color = c;
            }
        }

        private void ConfigureSliders()
        {
            // Health: 0-100
            if (healthSlider != null)
            {
                healthSlider.minValue = 0;
                healthSlider.maxValue = 100;
                healthSlider.value = 100;
            }

            // Hunger: 0-100
            if (hungerSlider != null)
            {
                hungerSlider.minValue = 0;
                hungerSlider.maxValue = 100;
                hungerSlider.value = 100;
            }

            // Thirst: 0-100
            if (thirstSlider != null)
            {
                thirstSlider.minValue = 0;
                thirstSlider.maxValue = 100;
                thirstSlider.value = 100;
            }

            // Temperature: -50 a 50
            if (temperatureSlider != null)
            {
                temperatureSlider.minValue = -50;
                temperatureSlider.maxValue = 50;
                temperatureSlider.value = 20;
            }
        }

        private void Update()
        {
            // Fade out do damage vignette
            if (_currentVignetteAlpha > 0 && damageVignette != null)
            {
                _currentVignetteAlpha -= Time.deltaTime * vignetteDecaySpeed;
                _currentVignetteAlpha = Mathf.Max(0, _currentVignetteAlpha);
                
                Color c = damageVignette.color;
                c.a = _currentVignetteAlpha;
                damageVignette.color = c;
            }
        }

        /// <summary>
        /// Atualiza todas as stats na UI
        /// </summary>
        public void UpdateStats(float health, float hunger, float thirst, float temperature)
        {
            UpdateHealth(health);
            UpdateHunger(hunger);
            UpdateThirst(thirst);
            UpdateTemperature(temperature);
        }

        public void UpdateHealth(float value)
        {
            if (healthSlider != null)
            {
                healthSlider.value = value;
            }

            if (healthText != null)
            {
                healthText.text = $"{value:F0}";
            }

            // Muda cor baseado na vida
            if (healthFill != null)
            {
                if (value > 50)
                    healthFill.color = healthColor;
                else if (value > 25)
                    healthFill.color = Color.Lerp(healthLowColor, healthColor, (value - 25f) / 25f);
                else
                    healthFill.color = healthLowColor;
            }
        }

        public void UpdateHunger(float value)
        {
            if (hungerSlider != null)
            {
                hungerSlider.value = value;
            }

            if (hungerText != null)
            {
                hungerText.text = $"{value:F0}";
            }

            if (hungerFill != null)
            {
                hungerFill.color = hungerColor;
                
                // Pisca quando crítico
                if (value < 20f)
                {
                    float pulse = Mathf.PingPong(Time.time * 2f, 0.3f);
                    hungerFill.color = Color.Lerp(hungerColor, Color.red, pulse);
                }
            }
        }

        public void UpdateThirst(float value)
        {
            if (thirstSlider != null)
            {
                thirstSlider.value = value;
            }

            if (thirstText != null)
            {
                thirstText.text = $"{value:F0}";
            }

            if (thirstFill != null)
            {
                thirstFill.color = thirstColor;
                
                // Pisca quando crítico
                if (value < 20f)
                {
                    float pulse = Mathf.PingPong(Time.time * 2f, 0.3f);
                    thirstFill.color = Color.Lerp(thirstColor, Color.red, pulse);
                }
            }
        }

        public void UpdateTemperature(float value)
        {
            if (temperatureSlider != null)
            {
                temperatureSlider.value = value;
            }

            if (temperatureText != null)
            {
                temperatureText.text = $"{value:F0}°C";
            }

            // Cor baseada na temperatura
            if (temperatureFill != null)
            {
                if (value < 10)
                    temperatureFill.color = tempColdColor;
                else if (value > 30)
                    temperatureFill.color = tempHotColor;
                else
                    temperatureFill.color = tempNormalColor;
            }

            // Indicadores de temperatura extrema
            if (coldIndicator != null)
            {
                coldIndicator.SetActive(value < 0);
            }

            if (hotIndicator != null)
            {
                hotIndicator.SetActive(value > 40);
            }
        }

        /// <summary>
        /// Mostra efeito visual de dano
        /// </summary>
        public void ShowDamageEffect(float intensity = 0.5f)
        {
            _currentVignetteAlpha = Mathf.Clamp01(intensity);
        }

        /// <summary>
        /// Esconde toda a UI (útil quando morrer)
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Mostra a UI
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
            
            // Reset para valores iniciais
            UpdateStats(100, 100, 100, 20);
        }
    }
}