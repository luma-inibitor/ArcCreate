using System.Collections.Generic;
using UnityEngine;

namespace ArcCreate.Gameplay.Data
{
    public abstract class LongNote : Note
    {
        public int EndTiming { get; set; }

        public double EndFloorPosition { get; set; }

        public double TimeIncrement { get; protected set; }

        public double FirstJudgeTime { get; protected set; }

        /// <summary>
        /// The judgement points of this note, ordered by timing.
        /// </summary>
        protected List<JudgePoint> JudgePoints { get; } = new List<JudgePoint>();

        /// <summary>
        /// Recalculate the judge timings value of this note.
        /// </summary>
        public abstract void RecalculateJudgeTimings();

        public override int ComboAt(int timing)
        {
            int combo = 0;
            for (int i = 0; i < JudgePoints.Count; i++)
            {
                if (JudgePoints[i].Timing > timing)
                {
                    break;
                }

                combo += JudgePoints[i].Weight;
            }

            return Mathf.Clamp(combo, 0, TotalCombo);
        }

        /// <summary>
        /// Gets the number of judgement points that are not later than the timing.
        /// </summary>
        /// <param name="timing">The chart timing.</param>
        /// <returns>The number of points.</returns>
        public int JudgePointCountAt(int timing)
        {
            int count = 0;
            for (int i = 0; i < JudgePoints.Count; i++)
            {
                if (JudgePoints[i].Timing > timing)
                {
                    break;
                }

                count++;
            }

            return count;
        }

        public override void Assign(ArcEvent newValues)
        {
            base.Assign(newValues);
            LongNote e = newValues as LongNote;
            EndTiming = e.EndTiming;
        }

        public override void RecalculateFloorPosition()
        {
            base.RecalculateFloorPosition();
            EndFloorPosition = TimingGroupInstance.GetFloorPosition(EndTiming);
        }

        public float EndZPos(double floorPosition)
            => ArcFormula.FloorPositionToZ(EndFloorPosition - floorPosition, TimingGroup);
    }
}
