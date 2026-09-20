using ArcCreate.Utility;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace ArcCreate.Gameplay.Audio.Practice
{
    public class PracticeTimeline : MonoBehaviour, IPointerClickHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private Camera viewCamera;
        [SerializeField] private RawImage image;
        [SerializeField] private RectTransform rect;
        [SerializeField] private RectTransform overviewRange;
        private readonly int timingShaderId = Shader.PropertyToID("_CurrentSample");
        private readonly int lengthShaderId = Shader.PropertyToID("_AudioLength");
        private readonly int repeatFromShaderId = Shader.PropertyToID("_RepeatSampleFrom");
        private readonly int repeatToShaderId = Shader.PropertyToID("_RepeatSampleTo");
        private AudioClip loadedClip;

        public Texture WaveformTexture => image.texture;

        public void OnPointerClick(PointerEventData eventData) => Seek(TimingAt(eventData));

        /// <summary>
        /// Only moves the timing while dragging. The judge reset happens once in <see cref="OnEndDrag"/>.
        /// </summary>
        public void OnDrag(PointerEventData eventData)
        {
            int timing = TimingAt(eventData);
            if (Services.Audio.IsPlaying)
            {
                Services.Audio.AudioTiming = timing;
            }
            else
            {
                Services.Audio.SetAudioTimingSilent(timing);
            }
        }

        public void OnEndDrag(PointerEventData eventData) => Seek(TimingAt(eventData));

        public void LoadWaveformFor(AudioClip clip)
        {
            if (clip == null || clip == loadedClip)
            {
                return;
            }

            if (image.texture != null)
            {
                Destroy(image.texture);
            }

            Texture2D texture = WaveformGenerator.EncodeTexture(clip);
            image.texture = texture;
            image.material.mainTexture = texture;
            image.enabled = true;
            image.material.SetFloat(lengthShaderId, WaveformGenerator.SecondToSample(clip.length, clip));
            loadedClip = clip;
        }

        public void SetRepeatRange(bool repeatOn, int repeatFromTiming, int repeatToTiming)
        {
            PlaceOverviewRange(repeatOn, repeatFromTiming, repeatToTiming);
            if (!repeatOn)
            {
                repeatFromTiming = repeatToTiming = -1;
            }

            image.material.SetFloat(repeatFromShaderId, WaveformGenerator.SecondToSample(repeatFromTiming / 1000f, gameplayData.AudioClip.Value));
            image.material.SetFloat(repeatToShaderId, WaveformGenerator.SecondToSample(repeatToTiming / 1000f, gameplayData.AudioClip.Value));
        }

        private void PlaceOverviewRange(bool repeatOn, int repeatFromTiming, int repeatToTiming)
        {
            if (overviewRange == null)
            {
                return;
            }

            AudioClip clip = gameplayData.AudioClip.Value;
            float length = clip == null ? 0 : clip.length * 1000;
            bool visible = repeatOn && length > 0;
            overviewRange.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            overviewRange.anchorMin = new Vector2(Mathf.Clamp01(repeatFromTiming / length), overviewRange.anchorMin.y);
            overviewRange.anchorMax = new Vector2(Mathf.Clamp01(repeatToTiming / length), overviewRange.anchorMax.y);
            overviewRange.offsetMin = new Vector2(0, overviewRange.offsetMin.y);
            overviewRange.offsetMax = new Vector2(0, overviewRange.offsetMax.y);
        }

        private void Seek(int timing)
        {
            Services.Audio.AudioTiming = timing;
            Services.Audio.SetResumeAt(timing);
        }

        private int TimingAt(PointerEventData eventData)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position, viewCamera, out Vector2 local);
            float t = Mathf.Clamp(local.x / rect.rect.width, -0.5f, 0.5f) + 0.5f;
            return Mathf.RoundToInt(t * Services.Audio.AudioLength);
        }

        private void Update()
        {
            image.material.SetFloat(
                timingShaderId,
                WaveformGenerator.SecondToSample(Services.Audio.AudioTiming / 1000f, gameplayData.AudioClip.Value));
        }
    }
}
