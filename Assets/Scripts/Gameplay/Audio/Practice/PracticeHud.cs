using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Practice controls on the unpaused HUD: jump buttons next to the pause button, a count-in during
    /// the lead-in, and the loop range on the progress bar. Active only in practice mode.
    /// </summary>
    public class PracticeHud : MonoBehaviour
    {
        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private PracticeMenu practiceMenu;
        [SerializeField] private TMP_Text countInText;

        [Header("Jumps")]
        [SerializeField] private Button toStartButton;
        [SerializeField] private Button backButton;
        [SerializeField] private Button forwardButton;
        [SerializeField] private Button toEndButton;
        [SerializeField] private int jumpMs = 5000;

        [Header("Elsewhere on the HUD")]
        [SerializeField] private RectTransform progressMarker;

        private int shownFrom = -1;
        private int shownTo = -1;
        private bool shownEnabled;
        private int shownCountIn = -1;

        private void Awake()
        {
            toStartButton.onClick.AddListener(JumpToStart);
            backButton.onClick.AddListener(JumpBack);
            forwardButton.onClick.AddListener(JumpForward);
            toEndButton.onClick.AddListener(JumpToEnd);
        }

        private void OnDestroy()
        {
            toStartButton.onClick.RemoveListener(JumpToStart);
            backButton.onClick.RemoveListener(JumpBack);
            forwardButton.onClick.RemoveListener(JumpForward);
            toEndButton.onClick.RemoveListener(JumpToEnd);
        }

        private void OnEnable()
        {
            gameplayData.OnGameplayUpdate += OnGameplayUpdate;
            Invalidate();
        }

        private void OnDisable()
        {
            gameplayData.OnGameplayUpdate -= OnGameplayUpdate;
        }

        private void JumpToStart()
        {
            practiceMenu.JumpToLoopStart();
        }

        private void JumpBack()
        {
            JumpBy(-jumpMs);
        }

        private void JumpForward()
        {
            JumpBy(jumpMs);
        }

        /// <summary>
        /// Skip to the last few seconds of the loop, or of the song when repeat is off. Landing exactly
        /// on B would only restart the loop.
        /// </summary>
        private void JumpToEnd()
        {
            PracticeLoop loop = practiceMenu.Loop;
            int end = loop.Enabled ? loop.To : loop.AudioLength;
            int start = loop.Enabled ? loop.From : 0;
            JumpTo(Mathf.Max(start, end - JumpDuration()));
        }

        private void JumpBy(int ms)
        {
            JumpTo(Services.Audio.AudioTiming + (ms < 0 ? -JumpDuration() : JumpDuration()));
        }

        private int JumpDuration() => Mathf.RoundToInt(jumpMs * gameplayData.PlaybackSpeed.Value);

        private void JumpTo(int audioTiming)
        {
            audioTiming = Mathf.Clamp(audioTiming, 0, Services.Audio.AudioLength);
            practiceMenu.Loop.ResetTracking();
            Services.Audio.Pause();
            Services.Audio.PlayWithDelay(audioTiming, Values.DelayBeforeAudioResume);
        }

        private void Invalidate()
        {
            shownFrom = -1;
            shownTo = -1;
            shownCountIn = -1;
        }

        private void OnGameplayUpdate(int chartTiming)
        {
            PracticeLoop loop = practiceMenu.Loop;
            BeatGrid grid = practiceMenu.Grid;
            int offset = Services.Audio.FullOffset;
            float speed = gameplayData.PlaybackSpeed.Value;

            if (loop.Enabled != shownEnabled || loop.From != shownFrom || loop.To != shownTo)
            {
                shownFrom = loop.From;
                shownTo = loop.To;
                if (progressMarker != null)
                {
                    progressMarker.gameObject.SetActive(loop.Enabled && loop.AudioLength > 0);
                    if (loop.AudioLength > 0)
                    {
                        progressMarker.anchorMin = new Vector2((float)loop.From / loop.AudioLength, 0);
                        progressMarker.anchorMax = new Vector2((float)loop.To / loop.AudioLength, 1);
                    }
                }
            }

            shownEnabled = loop.Enabled;
            UpdateCountIn(loop, grid, offset, speed);
        }

        /// <summary>
        /// During the lead-in before A, count the beats down to the bar the loop starts on.
        /// </summary>
        private void UpdateCountIn(PracticeLoop loop, BeatGrid grid, int offset, float speed)
        {
            int audioTiming = Services.Audio.AudioTiming;
            int beats = 0;
            if (loop.Enabled && audioTiming < loop.From)
            {
                double bar = grid.BarLengthAt(loop.From - offset);
                double beat = grid.BeatLengthAt(loop.From - offset);
                int preroll = Mathf.RoundToInt(loop.RestartDelayMs(bar, speed) * speed);
                if (beat > 0 && audioTiming >= loop.From - preroll)
                {
                    beats = (int)Math.Ceiling((loop.From - audioTiming) / beat);
                }
            }

            if (beats == shownCountIn)
            {
                return;
            }

            shownCountIn = beats;
            countInText.text = beats == 0
                ? string.Empty
                : I18n.S("Gameplay.Practice.Hud.CountIn", new Dictionary<string, object>()
                {
                    { "bar", grid.BarIndexAt(loop.From - offset) + 1 },
                    { "beats", beats },
                });
        }
    }
}
