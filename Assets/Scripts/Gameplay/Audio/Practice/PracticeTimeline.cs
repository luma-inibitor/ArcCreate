using System;
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
        private readonly int timingShaderId = Shader.PropertyToID("_CurrentSample");
        private readonly int lengthShaderId = Shader.PropertyToID("_AudioLength");
        private readonly int repeatFromShaderId = Shader.PropertyToID("_RepeatSampleFrom");
        private readonly int repeatToShaderId = Shader.PropertyToID("_RepeatSampleTo");
        private AudioClip loadedClip;

        /// <summary>
        /// Raised with the audio timing after a click or a completed drag.
        /// </summary>
        public event Action<int> OnSeek;

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
            if (!repeatOn)
            {
                repeatFromTiming = repeatToTiming = -1;
            }

            image.material.SetFloat(repeatFromShaderId, WaveformGenerator.SecondToSample(repeatFromTiming / 1000f, gameplayData.AudioClip.Value));
            image.material.SetFloat(repeatToShaderId, WaveformGenerator.SecondToSample(repeatToTiming / 1000f, gameplayData.AudioClip.Value));
        }

        private void Seek(int timing)
        {
            Services.Audio.AudioTiming = timing;
            Services.Audio.SetResumeAt(timing);
            OnSeek?.Invoke(timing);
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
