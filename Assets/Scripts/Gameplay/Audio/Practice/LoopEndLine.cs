using ArcCreate.Gameplay.Chart;
using UnityEngine;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// A line across the track at the loop's end, scrolling with the notes of timing group 0, so the
    /// restart is never a surprise. Placed the way beatlines are.
    /// </summary>
    public class LoopEndLine : MonoBehaviour
    {
        [SerializeField] private GameplayData gameplayData;
        [SerializeField] private PracticeMenu practiceMenu;
        [SerializeField] private BeatlineBehaviour line;
        [SerializeField] private Color color = new Color(1, 0.95f, 0.33f, 1);
        [SerializeField] private float thickness = Values.BeatlineThickness * 1.5f;

        private void OnEnable()
        {
            gameplayData.OnGameplayUpdate += OnGameplayUpdate;
        }

        private void OnDisable()
        {
            gameplayData.OnGameplayUpdate -= OnGameplayUpdate;
            line.gameObject.SetActive(false);
        }

        private void OnGameplayUpdate(int chartTiming)
        {
            PracticeLoop loop = practiceMenu.Loop;
            if (!loop.Enabled)
            {
                line.gameObject.SetActive(false);
                return;
            }

            TimingGroup group = Services.Chart.GetTimingGroup(0);
            double endPosition = group.GetFloorPosition(loop.To - Services.Audio.FullOffset);
            double nowPosition = group.GetFloorPosition(chartTiming);
            float z = ArcFormula.FloorPositionToZ(endPosition - nowPosition, group);
            bool visible = z <= Values.TrackLengthBackward && z >= -Values.TrackLengthForward;
            line.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            Transform lineTransform = line.transform;
            lineTransform.localPosition = new Vector3(0, 0, z);
            lineTransform.localScale = new Vector3(
                lineTransform.localScale.x,
                ArcFormula.CalculateBeatlineSizeScalar(thickness, z),
                1);
            line.SetColor(color);
        }
    }
}
