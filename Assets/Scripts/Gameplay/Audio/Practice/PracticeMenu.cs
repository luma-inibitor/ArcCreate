using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    public class PracticeMenu : MonoBehaviour
    {
        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private PracticeTimeline timeline;

        [Header("Speed")]
        [SerializeField] private SpeedSlider speedSlider;
        [SerializeField] private TMP_Text speedText;

        [Header("Repeat")]
        [SerializeField] private GameObject repeatOff;
        [SerializeField] private GameObject repeatOn;
        [SerializeField] private Button repeatOffButton;
        [SerializeField] private Button repeatOnButton;
        [SerializeField] private Button repeatFromButton;
        [SerializeField] private Button repeatToButton;

        private readonly PracticeLoop loop = new PracticeLoop();
        private BeatGrid grid;

        public PracticeLoop Loop => loop;

        /// <summary>
        /// Gets the bar and beat grid of timing group 0, rebuilt after chart or timing edits.
        /// </summary>
        public BeatGrid Grid
        {
            get
            {
                if (grid == null)
                {
                    grid = new BeatGrid(Services.Chart.GetTimingGroup(0).Timings);
                }

                return grid;
            }
        }

        /// <summary>
        /// Build the waveform texture ahead of the first pause. Safe to call while inactive.
        /// </summary>
        public void PrepareWaveform(AudioClip clip)
        {
            timeline.LoadWaveformFor(clip);
        }

        private void Awake()
        {
            gameplayData.AudioClip.OnValueChange += OnClipChange;
            if (gameplayData.AudioClip.Value != null)
            {
                OnClipChange(gameplayData.AudioClip.Value);
            }

            gameplayData.OnChartTimingEdit += InvalidateGrid;
            gameplayData.OnChartEdit += InvalidateGrid;
            timeline.OnSeek += OnSeek;
            speedSlider.OnValueChanged += OnSpeedChange;
            repeatOffButton.onClick.AddListener(TurnRepeatOff);
            repeatOnButton.onClick.AddListener(TurnRepeatOn);
            repeatFromButton.onClick.AddListener(SetRepeatFrom);
            repeatToButton.onClick.AddListener(SetRepeatTo);
        }

        private void OnDestroy()
        {
            gameplayData.AudioClip.OnValueChange -= OnClipChange;
            gameplayData.OnGameplayUpdate -= CheckRepeat;
            gameplayData.OnChartTimingEdit -= InvalidateGrid;
            gameplayData.OnChartEdit -= InvalidateGrid;
            timeline.OnSeek -= OnSeek;
            speedSlider.OnValueChanged -= OnSpeedChange;
            repeatOffButton.onClick.RemoveListener(TurnRepeatOff);
            repeatOnButton.onClick.RemoveListener(TurnRepeatOn);
            repeatFromButton.onClick.RemoveListener(SetRepeatFrom);
            repeatToButton.onClick.RemoveListener(SetRepeatTo);
        }

        private void InvalidateGrid()
        {
            grid = null;
        }

        private void OnClipChange(AudioClip clip)
        {
            timeline.LoadWaveformFor(clip);
            loop.SetAudioLength(Mathf.RoundToInt(clip.length * 1000));
            InvalidateGrid();
            UpdateRepeatRange();
        }

        private void OnSpeedChange(float speed)
        {
            speedText.text = speed.ToString("f2") + "x";
            gameplayData.PlaybackSpeed.Value = speed;
        }

        private void TurnRepeatOff()
        {
            loop.Enabled = false;
            repeatOff.SetActive(true);
            repeatOn.SetActive(false);
            UpdateRepeatRange();
            gameplayData.OnGameplayUpdate -= CheckRepeat;
        }

        private void TurnRepeatOn()
        {
            loop.Enabled = true;
            repeatOff.SetActive(false);
            repeatOn.SetActive(true);
            UpdateRepeatRange();
            gameplayData.OnGameplayUpdate -= CheckRepeat;
            gameplayData.OnGameplayUpdate += CheckRepeat;
        }

        private void SetRepeatFrom()
        {
            loop.SetFrom(SnapToBar(Services.Audio.AudioTiming));
            UpdateRepeatRange();
        }

        private void SetRepeatTo()
        {
            loop.SetTo(SnapToBar(Services.Audio.AudioTiming));
            UpdateRepeatRange();
        }

        /// <summary>
        /// Move one loop edge to an audio timing, snapped to the nearest bar. The edge never crosses the other one.
        /// </summary>
        public void DragLoopEdge(bool from, int audioTiming)
        {
            int snapped = SnapToBar(audioTiming);
            if (from)
            {
                loop.MoveFrom(snapped);
            }
            else
            {
                loop.MoveTo(snapped);
            }

            UpdateRepeatRange();
        }

        private int SnapToBar(int audioTiming)
        {
            int offset = Services.Audio.FullOffset;
            return Grid.SnapToBar(audioTiming - offset) + offset;
        }

        private void UpdateRepeatRange()
        {
            timeline.SetRepeatRange(loop.Enabled, loop.From, loop.To);
        }

        /// <summary>
        /// A seek that lands outside the loop range turns the loop off, so resuming plays from
        /// where the user went instead of snapping back to the loop start.
        /// </summary>
        private void OnSeek(int audioTiming)
        {
            if (loop.Enabled && !loop.Contains(audioTiming))
            {
                TurnRepeatOff();
            }
        }

        private void CheckRepeat(int chartTiming)
        {
            int timing = Services.Audio.AudioTiming;
            int length = Services.Audio.AudioLength;
            bool audioEnd = timing >= length - 100 && loop.To >= length - 100;
            if (loop.ShouldRestart(timing, Services.Audio.IsPlaying) || (audioEnd && !gameObject.activeInHierarchy))
            {
                Restart();
            }
        }

        private void Restart()
        {
            float speed = gameplayData.PlaybackSpeed.Value;
            double barLength = Grid.BarLengthAt(loop.From - Services.Audio.FullOffset);
            int delay = loop.RestartDelayMs(barLength, speed);
            loop.MarkRestarted(delay, speed);
            Services.Audio.Pause();
            Services.Audio.PlayWithDelay(loop.From, delay);
        }
    }
}
