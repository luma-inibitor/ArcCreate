using System;
using ArcCreate.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Zoomed loop editor: a chart strip and a waveform over a few bars around the playhead,
    /// sharing one time axis, with the loop range, lead-in and draggable A/B handles drawn on top.
    /// </summary>
    public class PracticeZoomView : MonoBehaviour
    {
        public const int MinBars = 2;
        public const int MaxBars = 32;
        private const int FallbackBarMs = 2000;

        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private PracticeMenu menu;
        [SerializeField] private PracticeTimeline timeline;
        [SerializeField] private Camera viewCamera;

        [Header("Time axis")]
        [SerializeField] private RectTransform area;
        [SerializeField] private ChartStripGraphic chartStrip;
        [SerializeField] private RawImage waveform;
        [SerializeField] private RectTransform playhead;

        [Header("Loop")]
        [SerializeField] private RectTransform loopRegion;
        [SerializeField] private RectTransform leadInRegion;
        [SerializeField] private RectTransform handleFrom;
        [SerializeField] private RectTransform handleTo;
        [SerializeField] private TMP_Text readout;

        [Header("Zoom")]
        [SerializeField] private Button zoomInButton;
        [SerializeField] private Button zoomOutButton;
        [SerializeField] private TMP_Text zoomLabel;
        [SerializeField] private RectTransform overviewWindow;
        [SerializeField] private int bars = 8;

        private readonly TimelineWindow window = new TimelineWindow();
        private readonly int fromSampleShaderId = Shader.PropertyToID("_FromSample");
        private readonly int toSampleShaderId = Shader.PropertyToID("_ToSample");
        private Material waveformMaterial;
        private int readoutFrom = int.MinValue;
        private int readoutTo = int.MinValue;
        private bool readoutEnabled;
        private int zoomLabelBars = -1;

        /// <summary>
        /// Audio timing under a screen position on the time axis, clamped to the visible window.
        /// </summary>
        public int AudioTimingAt(Vector2 screenPosition)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(area, screenPosition, viewCamera, out Vector2 local);
            Rect r = area.rect;
            return window.Denormalize(Mathf.InverseLerp(r.xMin, r.xMax, local.x));
        }

        private void Awake()
        {
            if (waveform != null && waveform.material != null)
            {
                waveformMaterial = new Material(waveform.material);
                waveform.material = waveformMaterial;
            }

            if (zoomInButton != null)
            {
                zoomInButton.onClick.AddListener(ZoomIn);
            }

            if (zoomOutButton != null)
            {
                zoomOutButton.onClick.AddListener(ZoomOut);
            }

            gameplayData.OnChartEdit += RefreshStrip;
            gameplayData.OnChartTimingEdit += RefreshStrip;
        }

        private void OnDestroy()
        {
            if (zoomInButton != null)
            {
                zoomInButton.onClick.RemoveListener(ZoomIn);
            }

            if (zoomOutButton != null)
            {
                zoomOutButton.onClick.RemoveListener(ZoomOut);
            }

            gameplayData.OnChartEdit -= RefreshStrip;
            gameplayData.OnChartTimingEdit -= RefreshStrip;

            if (waveformMaterial != null)
            {
                Destroy(waveformMaterial);
            }
        }

        private void Update()
        {
            AudioClip clip = gameplayData.AudioClip.Value;
            if (clip == null || Services.Chart == null || Services.Audio == null)
            {
                return;
            }

            int offset = Services.Audio.FullOffset;
            int playheadTiming = Services.Audio.AudioTiming;
            BeatGrid grid = menu.Grid;

            double barMs = grid.BarLengthAt(playheadTiming - offset);
            if (barMs <= 0)
            {
                barMs = FallbackBarMs;
            }

            window.SetBounds(0, Services.Audio.AudioLength);
            window.CenterOn(playheadTiming, (int)Math.Round(barMs * bars));

            chartStrip.SetWindow(window.From - offset, window.To - offset, grid);
            UpdateWaveform(clip);
            PlaceMarker(playhead, playheadTiming, true);
            UpdateLoop(grid, offset);
            UpdateZoomIndicator();
        }

        /// <summary>
        /// Box the visible window on the overview waveform and show the zoom level.
        /// </summary>
        private void UpdateZoomIndicator()
        {
            if (overviewWindow != null)
            {
                float length = Mathf.Max(1, Services.Audio.AudioLength);
                overviewWindow.anchorMin = new Vector2(Mathf.Clamp01(window.From / length), overviewWindow.anchorMin.y);
                overviewWindow.anchorMax = new Vector2(Mathf.Clamp01(window.To / length), overviewWindow.anchorMax.y);
                overviewWindow.offsetMin = new Vector2(0, overviewWindow.offsetMin.y);
                overviewWindow.offsetMax = new Vector2(0, overviewWindow.offsetMax.y);
            }

            if (zoomLabel != null && zoomLabelBars != bars)
            {
                zoomLabelBars = bars;
                zoomLabel.text = $"{bars} bars";
            }
        }

        private void UpdateWaveform(AudioClip clip)
        {
            if (waveformMaterial == null)
            {
                return;
            }

            Texture texture = timeline.WaveformTexture;
            if (texture != null && waveform.texture != texture)
            {
                waveform.texture = texture;
                waveformMaterial.mainTexture = texture;
            }

            waveformMaterial.SetInt(fromSampleShaderId, WaveformGenerator.SecondToSample(window.From / 1000f, clip));
            waveformMaterial.SetInt(toSampleShaderId, WaveformGenerator.SecondToSample(window.To / 1000f, clip));
        }

        private void UpdateLoop(BeatGrid grid, int offset)
        {
            PracticeLoop loop = menu.Loop;
            float speed = gameplayData.PlaybackSpeed.Value;
            double barAtFrom = grid.BarLengthAt(loop.From - offset);
            int preroll = Mathf.RoundToInt(loop.RestartDelayMs(barAtFrom, speed) * speed);

            PlaceSpan(loopRegion, loop.From, loop.To, loop.Enabled);
            PlaceSpan(leadInRegion, loop.From - preroll, loop.From, loop.Enabled);
            PlaceMarker(handleFrom, loop.From, loop.Enabled);
            PlaceMarker(handleTo, loop.To, loop.Enabled);
            UpdateReadout(grid, offset, loop);
        }

        private void UpdateReadout(BeatGrid grid, int offset, PracticeLoop loop)
        {
            if (readout == null || (loop.From == readoutFrom && loop.To == readoutTo && loop.Enabled == readoutEnabled))
            {
                return;
            }

            readoutFrom = loop.From;
            readoutTo = loop.To;
            readoutEnabled = loop.Enabled;

            if (!loop.Enabled)
            {
                readout.text = string.Empty;
                return;
            }

            int barFrom = grid.BarIndexAt(loop.From - offset);
            int barTo = grid.BarIndexAt(loop.To - offset);
            readout.text = $"A {FormatTime(loop.From)} · bar {barFrom + 1}     {barTo - barFrom} bars     B {FormatTime(loop.To)} · bar {barTo + 1}";
        }

        private void PlaceMarker(RectTransform marker, int audioTiming, bool enabled)
        {
            if (marker == null)
            {
                return;
            }

            float t = window.Normalize(audioTiming);
            bool visible = enabled && t >= 0 && t <= 1;
            SetActive(marker, visible);
            if (!visible)
            {
                return;
            }

            marker.anchorMin = new Vector2(t, marker.anchorMin.y);
            marker.anchorMax = new Vector2(t, marker.anchorMax.y);
            marker.anchoredPosition = new Vector2(0, marker.anchoredPosition.y);
        }

        private void PlaceSpan(RectTransform span, int fromTiming, int toTiming, bool enabled)
        {
            if (span == null)
            {
                return;
            }

            float a = Mathf.Clamp01(window.Normalize(fromTiming));
            float b = Mathf.Clamp01(window.Normalize(toTiming));
            bool visible = enabled && b > a;
            SetActive(span, visible);
            if (!visible)
            {
                return;
            }

            span.anchorMin = new Vector2(a, span.anchorMin.y);
            span.anchorMax = new Vector2(b, span.anchorMax.y);
            span.offsetMin = new Vector2(0, span.offsetMin.y);
            span.offsetMax = new Vector2(0, span.offsetMax.y);
        }

        private static void SetActive(RectTransform rect, bool active)
        {
            if (rect.gameObject.activeSelf != active)
            {
                rect.gameObject.SetActive(active);
            }
        }

        private static string FormatTime(int ms)
        {
            ms = Mathf.Max(0, ms);
            return $"{ms / 60000}:{(ms % 60000) / 1000f:00.00}";
        }

        private void ZoomIn()
        {
            bars = Mathf.Max(MinBars, bars / 2);
        }

        private void ZoomOut()
        {
            bars = Mathf.Min(MaxBars, bars * 2);
        }

        private void RefreshStrip()
        {
            chartStrip.Refresh();
        }
    }
}
