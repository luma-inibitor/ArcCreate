using System;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Playback speed control of the practice pause screen: a - and + button stepping by
    /// <see cref="Increment"/>, preset buttons, and a bar showing where the speed sits in the range.
    /// </summary>
    public class SpeedSlider : MonoBehaviour
    {
        public const float DefaultValue = 1;
        public const float MaxValue = 1.5f;
        public const float MinValue = 0.25f;
        public const float Increment = 0.05f;

        [SerializeField] private RectTransform fillRect;
        [SerializeField] private Button incrementButtton;
        [SerializeField] private Button decrementButtton;

        [Header("Presets")]
        [SerializeField] private Button[] presetButtons;
        [SerializeField] private float[] presetValues;
        [SerializeField] private Color presetColor = new Color(0, 0, 0, 0.3f);
        [SerializeField] private Color presetSelectedColor = new Color(0.3882353f, 0.7176471f, 0.81960785f, 0.78431374f);

        private float value = DefaultValue;

        public event Action<float> OnValueChanged;

        public float Value
        {
            get => value;
            set { SetValue(value); }
        }

        public void SetValue(float value)
        {
            SetValueWithoutNotify(value);
            OnValueChanged?.Invoke(this.value);
        }

        public void SetValueWithoutNotify(float value)
        {
            this.value = Snap(value);
            UpdateUI();
        }

        /// <summary>
        /// Round a speed to the nearest step and keep it inside the range.
        /// </summary>
        public static float Snap(float speed)
        {
            float snapped = Mathf.Round(speed / Increment) * Increment;
            return Mathf.Clamp(snapped, MinValue, MaxValue);
        }

        private void Awake()
        {
            incrementButtton.onClick.AddListener(IncrementSpeed);
            decrementButtton.onClick.AddListener(DecrementSpeed);
            for (int i = 0; i < presetButtons.Length; i++)
            {
                float preset = presetValues[i];
                presetButtons[i].onClick.AddListener(() => SetValue(preset));
            }

            UpdateUI();
        }

        private void OnDestroy()
        {
            incrementButtton.onClick.RemoveListener(IncrementSpeed);
            decrementButtton.onClick.RemoveListener(DecrementSpeed);
            foreach (Button button in presetButtons)
            {
                button.onClick.RemoveAllListeners();
            }
        }

        private void IncrementSpeed()
        {
            SetValue(Value + Increment);
        }

        private void DecrementSpeed()
        {
            SetValue(Value - Increment);
        }

        private void UpdateUI()
        {
            fillRect.anchorMin = new Vector2(0, 0);
            fillRect.anchorMax = new Vector2(Mathf.InverseLerp(MinValue, MaxValue, value), 1);

            for (int i = 0; i < presetButtons.Length; i++)
            {
                bool selected = Mathf.Approximately(presetValues[i], value);
                presetButtons[i].image.color = selected ? presetSelectedColor : presetColor;
            }
        }
    }
}
