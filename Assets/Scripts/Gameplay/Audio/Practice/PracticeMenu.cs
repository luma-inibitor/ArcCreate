using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    public class PracticeMenu : MonoBehaviour
    {
        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private PracticeTimeline timeline;
        [SerializeField] private PauseMenu pauseMenu;
        [SerializeField] private Button restartLoopButton;

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

        [Header("Lead-in")]
        [Tooltip("One per PracticeLoop.LeadInMode, in enum order.")]
        [SerializeField] private Button[] leadInButtons;
        [SerializeField] private Color leadInColor = new Color(0, 0, 0, 0.3f);
        [SerializeField] private Color leadInSelectedColor = new Color(0.3882353f, 0.7176471f, 0.81960785f, 0.78431374f);

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

        /// <summary>
        /// Jump back to A with the lead-in while playing, for the HUD's skip-to-start button.
        /// </summary>
        public void JumpToLoopStart()
        {
            loop.ResetTracking();
            Restart();
        }

        private static string FormatSpeed(float speed) => speed.ToString("f2") + "x";

        private void Awake()
        {
            gameplayData.AudioClip.OnValueChange += OnClipChange;
            if (gameplayData.AudioClip.Value != null)
            {
                OnClipChange(gameplayData.AudioClip.Value);
            }

            gameplayData.OnChartTimingEdit += InvalidateGrid;
            gameplayData.OnChartEdit += InvalidateGrid;
            speedSlider.OnValueChanged += OnSpeedChange;
            speedText.text = FormatSpeed(speedSlider.Value);
            repeatOffButton.onClick.AddListener(TurnRepeatOff);
            repeatOnButton.onClick.AddListener(TurnRepeatOn);
            repeatFromButton.onClick.AddListener(SetRepeatFrom);
            repeatToButton.onClick.AddListener(SetRepeatTo);
            restartLoopButton.onClick.AddListener(RestartFromPause);
            for (int i = 0; i < leadInButtons.Length; i++)
            {
                PracticeLoop.LeadInMode mode = (PracticeLoop.LeadInMode)i;
                leadInButtons[i].onClick.AddListener(() => SetLeadIn(mode));
            }

            UpdateLeadInButtons();
        }

        private void OnDestroy()
        {
            restartLoopButton.onClick.RemoveListener(RestartFromPause);
            foreach (Button button in leadInButtons)
            {
                button.onClick.RemoveAllListeners();
            }

            gameplayData.AudioClip.OnValueChange -= OnClipChange;
            gameplayData.OnGameplayUpdate -= CheckRepeat;
            gameplayData.OnChartTimingEdit -= InvalidateGrid;
            gameplayData.OnChartEdit -= InvalidateGrid;
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
            if (clip == null)
            {
                return;
            }

            timeline.LoadWaveformFor(clip);
            loop.SetAudioLength(Mathf.RoundToInt(clip.length * 1000));
            InvalidateGrid();
            UpdateRepeatRange();
        }

        private void OnSpeedChange(float speed)
        {
            speedText.text = FormatSpeed(speed);
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
            loop.ResetTracking();
            repeatOff.SetActive(false);
            repeatOn.SetActive(true);
            UpdateRepeatRange();
            gameplayData.OnGameplayUpdate -= CheckRepeat;
            gameplayData.OnGameplayUpdate += CheckRepeat;
        }

        private void SetRepeatFrom()
        {
            loop.SetFrom(SnapToBar(Services.Audio.AudioTiming, BeatGrid.Rounding.Down));
            UpdateRepeatRange();
        }

        private void SetRepeatTo()
        {
            loop.SetTo(SnapToBar(Services.Audio.AudioTiming, BeatGrid.Rounding.Up));
            UpdateRepeatRange();
        }

        private void SetLeadIn(PracticeLoop.LeadInMode mode)
        {
            loop.LeadIn = mode;
            UpdateLeadInButtons();
        }

        private void UpdateLeadInButtons()
        {
            for (int i = 0; i < leadInButtons.Length; i++)
            {
                bool selected = (PracticeLoop.LeadInMode)i == loop.LeadIn;
                leadInButtons[i].image.color = selected ? leadInSelectedColor : leadInColor;
            }
        }

        private int SnapToBar(int audioTiming, BeatGrid.Rounding rounding = BeatGrid.Rounding.Nearest)
        {
            int offset = Services.Audio.FullOffset;
            return Grid.SnapToBar(audioTiming - offset, rounding) + offset;
        }

        private void UpdateRepeatRange()
        {
            timeline.SetRepeatRange(loop.Enabled, loop.From, loop.To);
        }

        private void CheckRepeat(int chartTiming)
        {
            int timing = Services.Audio.AudioTiming;
            if (loop.ShouldRestart(timing, Services.Audio.IsPlaying) || (loop.ReachedEnd(timing) && !gameObject.activeInHierarchy))
            {
                Restart();
            }
        }

        private void Restart()
        {
            Services.Audio.Pause();
            Services.Audio.PlayWithDelay(loop.From, RestartDelay());
        }

        /// <summary>
        /// The "Restart loop" button: leave the pause screen and play from A with the lead-in.
        /// </summary>
        private void RestartFromPause()
        {
            loop.ResetTracking();
            pauseMenu.ResumeAt(loop.From, RestartDelay());
        }

        private int RestartDelay()
        {
            double barLength = Grid.BarLengthAt(loop.From - Services.Audio.FullOffset);
            return loop.RestartDelayMs(barLength, gameplayData.PlaybackSpeed.Value);
        }
    }
}
