using ArcCreate.Gameplay.Chart;
using ArcCreate.Gameplay.Judgement;
using UnityEngine;

namespace ArcCreate.Gameplay.Data
{
    /// <summary>
    /// Partial class for judgement.
    /// </summary>
    public partial class Arc : LongNote, ILongNote, IArcJudgementReceiver
    {
        private int numJudgementRequestsSent = 0;
        private bool highlightRequestSent = false;
        private bool spawnedParticleThisFrame = false;

        public void ResetJudgeTo(int timing)
        {
            RecalculateJudgeTimings();
            highlight = highlight && timing >= Timing && timing <= EndTiming;
            longParticleUntil = int.MinValue;
            numJudgementRequestsSent = JudgePointCountAt(timing);
            highlightRequestSent = false;
            arcGroupAlpha = 1;
            hasBeenHitOnce = hasBeenHitOnce && timing >= Timing && timing <= EndTiming;
            for (int i = 0; i < segments.Count; i++)
            {
                ArcSegmentData segment = segments[i];
                segment.From = 0;
                segments[i] = segment;
            }
        }

        public override void RecalculateJudgeTimings()
        {
            TotalCombo = 0;
            FirstJudgeTime = double.MaxValue;
            TimeIncrement = double.MaxValue;
            JudgePoints.Clear();

            if (IsTrace || EndTiming == Timing)
            {
                return;
            }

            // A BPM of 0 gives an infinite interval, and the note has a single point in the middle, like the official client.
            float interval = JudgePointCalculator.CalculateInterval(TimingGroupInstance.GetBpm(Timing), Values.TimingPointDensity);
            TimeIncrement = interval;

            // The first arc of a chain has no point at its start, the arcs connected after it do.
            // The last two points of an arc that has an arc connected after it are merged when its end is on the interval grid.
            JudgePoints.AddRange(JudgePointCalculator.Calculate(
                Timing,
                EndTiming,
                interval,
                includeStart: !IsFirstArcOfGroup,
                hasNextArc: NextArc != null && !NextArc.IsTrace));
            TotalCombo = JudgePointCalculator.TotalCombo(JudgePoints);
            FirstJudgeTime = JudgePoints[0].Timing;
        }

        public void UpdateJudgement(int currentTiming, GroupProperties groupProperties)
        {
            int timing = groupProperties.EarlyJudgement ? Timing - Values.PerfectJudgeWindow : Timing;
            if (!IsTrace && currentTiming >= timing && Timing < EndTiming)
            {
                RequestJudgement(groupProperties);
            }
            if (!IsTrace && currentTiming >= Timing && Timing < EndTiming && !highlightRequestSent)
            {
                RequestHighlight(currentTiming, groupProperties);
                highlightRequestSent = true;
            }

            spawnedParticleThisFrame = false;
        }

        public void ProcessArcJudgement(bool isExpired, bool isJudgement, GroupProperties props)
        {
            int currentTiming = Services.Audio.ChartTiming;
            highlightRequestSent = false;
            float x = WorldXAt(currentTiming);
            float y = WorldYAt(currentTiming);
            Vector3 currentPos = new Vector3(x, y);

            if (isExpired)
            {
                SetGroupHighlight(false, int.MinValue);
                JudgementResult result = props.MapJudgementResult(JudgementResult.MissLate);

                if (isJudgement)
                {
                    if (!spawnedParticleThisFrame)
                    {
                        Services.Particle.PlayTextParticle(currentPos + props.CurrentJudgementOffset, result, Option<int>.None());
                        spawnedParticleThisFrame = true;
                    }

                    Services.Score.ProcessJudgement(TimingGroup, result, Option<int>.None());
                }
            }
            else if (currentTiming <= EndTiming + Values.HoldMissLateJudgeWindow)
            {
                SetGroupHighlight(true, currentTiming + Values.HoldParticlePersistDuration);
                if (!hasBeenHitOnce)
                {
                    Services.Hitsound.PlayArcHitsound(Timing);
                }

                hasBeenHitOnce = true;
                JudgementResult result = props.MapJudgementResult(JudgementResult.Max);

                if (isJudgement)
                {
                    if (!spawnedParticleThisFrame)
                    {
                        Services.Particle.PlayTextParticle(currentPos + props.CurrentJudgementOffset, result, Option<int>.None());
                        spawnedParticleThisFrame = true;
                    }

                    Services.Score.ProcessJudgement(TimingGroup, result, Option<int>.None());
                }
            }
        }

        private void RequestJudgement(GroupProperties props)
        {
            // A point that absorbed the last point of the arc counts twice, so it sends two requests at the same timing.
            int halfInterval = (int)System.Math.Min(TimeIncrement / 2, 1_000_000_000);
            for (int p = numJudgementRequestsSent; p < JudgePoints.Count; p++)
            {
                JudgePoint point = JudgePoints[p];
                int timing = point.Timing;
                int startTiming = timing - halfInterval;
                bool shortened = p == 0
                    && PreviousArc != null
                    && ArcConnection.HasDirectionChange(PreviousArc, this);
                int lateTiming = timing + ArcFormula.CalculateArcMissDuration((float)TimeIncrement, shortened);
                for (int w = 0; w < point.Weight; w++)
                {
                    Services.Judgement.Request(new ArcJudgementRequest()
                    {
                        StartAtTiming = startTiming,
                        ExpireAtTiming = lateTiming,
                        AutoAtTiming = timing,
                        Arc = this,
                        IsJudgement = true,
                        Receiver = this,
                        Properties = props,
                    });
                }
            }

            numJudgementRequestsSent = JudgePoints.Count;
        }

        private void RequestHighlight(int timing, GroupProperties props)
        {
            Services.Judgement.Request(new ArcJudgementRequest()
            {
                StartAtTiming = timing,
                ExpireAtTiming = timing + Values.HoldHighlightPersistDuration,
                AutoAtTiming = timing,
                Arc = this,
                IsJudgement = false,
                Receiver = this,
                Properties = props,
            });
        }
    }
}
