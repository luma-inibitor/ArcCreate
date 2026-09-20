using System;
using System.Collections.Generic;
using ArcCreate.Utility;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// Zoomed loop editor: a chart strip and a waveform over a few seconds around the playhead,
    /// sharing one time axis, with the loop range, lead-in and draggable A/B handles drawn on top.
    /// </summary>
    public class PracticeZoomView : MonoBehaviour
    {
        public const int MinSeconds = 2;
        public const int MaxSeconds = 32;

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
        [FormerlySerializedAs("bars")]
        [SerializeField] private int seconds = 8;

        private readonly TimelineWindow window = new TimelineWindow();
        private readonly int fromSampleShaderId = Shader.PropertyToID("_FromSample");
        private readonly int toSampleShaderId = Shader.PropertyToID("_ToSample");
        private Material waveformMaterial;
        private int readoutFrom = int.MinValue;
        private int readoutTo = int.MinValue;
        private bool readoutEnabled;
        private int zoomLabelSeconds = -1;

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

            // The window may reach before 0 so the playhead stays centred and visible during the lead-in.
            int span = seconds * 1000;
            window.SetBounds(Math.Min(0, playheadTiming - (span / 2)), Services.Audio.AudioLength);
            window.CenterOn(playheadTiming, span);

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

            if (zoomLabel != null && zoomLabelSeconds != seconds)
            {
                zoomLabelSeconds = seconds;
                zoomLabel.text = I18n.S("Gameplay.Practice.ZoomSeconds", new Dictionary<string, object>()
                {
                    { "seconds", seconds },
                });
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

            // There is no audio before 0, and the shader cannot sample there, so the waveform starts at 0.
            RectTransform waveformRect = waveform.rectTransform;
            waveformRect.anchorMin = new Vector2(Mathf.Clamp01(window.Normalize(0)), waveformRect.anchorMin.y);
            waveformRect.offsetMin = new Vector2(0, waveformRect.offsetMin.y);
            waveformMaterial.SetInt(fromSampleShaderId, WaveformGenerator.SecondToSample(Mathf.Max(0, window.From) / 1000f, clip));
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
            readout.text = I18n.S("Gameplay.Practice.LoopReadout", new Dictionary<string, object>()
            {
                { "from", FormatTime(loop.From) },
                { "barFrom", barFrom + 1 },
                { "bars", barTo - barFrom },
                { "to", FormatTime(loop.To) },
                { "barTo", barTo + 1 },
            });
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
            seconds = Mathf.Max(MinSeconds, seconds / 2);
        }

        private void ZoomOut()
        {
            seconds = Mathf.Min(MaxSeconds, seconds * 2);
        }

        private void RefreshStrip()
        {
            chartStrip.Refresh();
        }
    }
}
