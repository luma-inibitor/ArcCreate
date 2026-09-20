using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace ArcCreate.Gameplay.Audio.Practice
{
    /// <summary>
    /// The "this section" box of the practice pause screen: judgement counts, accuracy, loop count and
    /// an early/late histogram for the current loop, from <see cref="PracticeMenu.Stats"/>.
    /// </summary>
    public class PracticeStatsView : MonoBehaviour
    {
        [SerializeField] private PracticeMenu menu;
        [SerializeField] private TMP_Text loopsText;
        [SerializeField] private TMP_Text accuracyText;
        [SerializeField] private TMP_Text countsText;
        [SerializeField] private TMP_Text offsetText;

        [SerializeField] private HistogramGraphic histogram;

        private readonly int[] binCounts = new int[PracticeStats.BinCount];

        private void OnEnable()
        {
            Refresh();
        }

        /// <summary>
        /// Redraw from the current stats. Called when the box is shown and after the stats reset.
        /// </summary>
        public void Refresh()
        {
            PracticeStats stats = menu.Stats;

            loopsText.text = I18n.S("Gameplay.Practice.Stats.Loops", new Dictionary<string, object>()
            {
                { "count", stats.LoopCount },
            });

            if (stats.JudgedCount == 0)
            {
                accuracyText.text = I18n.S("Gameplay.Practice.Stats.NoNotes");
                countsText.text = string.Empty;
                offsetText.text = string.Empty;
            }
            else
            {
                accuracyText.text = I18n.S("Gameplay.Practice.Stats.Accuracy", new Dictionary<string, object>()
                {
                    { "percent", (stats.Accuracy * 100).ToString("f1") },
                });
                countsText.text = I18n.S("Gameplay.Practice.Stats.Counts", new Dictionary<string, object>()
                {
                    { "max", stats.MaxCount },
                    { "pure", stats.PerfectCount },
                    { "far", stats.GoodCount },
                    { "lost", stats.MissCount },
                });
                offsetText.text = stats.OffsetCount == 0
                    ? string.Empty
                    : I18n.S("Gameplay.Practice.Stats.Offset", new Dictionary<string, object>()
                    {
                        { "offset", stats.OffsetMean.ToString("+0;-0;0") },
                        { "early", stats.EarlyCount },
                        { "late", stats.LateCount },
                    });
            }

            for (int i = 0; i < binCounts.Length; i++)
            {
                binCounts[i] = stats.HistogramBin(i);
            }

            histogram.SetCounts(binCounts);
        }
    }
}
