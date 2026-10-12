using System;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using ArcCreate.Gameplay.Data;
using ArcCreate.Gameplay.Judgement;
using UnityEngine;

namespace ArcCreate.Gameplay
{
    public static class PlayLog
    {
        private const int FlushIntervalMs = 1000;
        private static readonly object PendingGate = new object();
        private static readonly object WriterGate = new object();
        private static readonly StringBuilder Line = new StringBuilder(512);
        private static readonly int[] Counts = new int[10];
        private static StringBuilder pending = new StringBuilder(8192);
        private static StringBuilder writing = new StringBuilder(8192);
        private static StreamWriter writer;
        private static Timer timer;
        private static bool active;
        private static string lastLoop;

        public static string LogDirectory => Path.Combine(Application.persistentDataPath, "playlogs");

        public static void Begin(GameplayData data, string levelId, string chartPath)
        {
            End();
            try
            {
                Directory.CreateDirectory(LogDirectory);
                DateTimeOffset now = DateTimeOffset.Now;
                string name = $"{now:yyyyMMdd-HHmmss-fff}_{Sanitize(levelId)}_{Sanitize(data.DifficultyName.Value)}.jsonl";
                writer = new StreamWriter(Path.Combine(LogDirectory, name), false, new UTF8Encoding(false));
                Array.Clear(Counts, 0, Counts.Length);
                lastLoop = null;
                active = true;
                timer = new Timer(_ => Flush(), null, FlushIntervalMs, FlushIntervalMs);

                Start("header");
                Field("version", 1);
                Clocks(now);
                Field("level", levelId);
                Field("chart_path", chartPath);
                Field("title", data.Title.Value);
                Field("composer", data.Composer.Value);
                Field("difficulty", data.DifficultyName.Value);
                Field("base_bpm", data.BaseBpm.Value);
                Field("chart_offset_ms", Values.ChartAudioOffset);
                Field("global_offset_ms", Settings.GlobalAudioOffset.Value);
                Field("full_offset_ms", Services.Audio.FullOffset);
                Field("note_speed", Settings.DropRate.Value);
                Field("playback_speed", data.PlaybackSpeed.Value);
                Field("practice", data.EnablePracticeMode.Value);
                Field("autoplay", data.EnableAutoplayMode.Value);
                Field("audio_length_ms", Services.Audio.AudioLength);
                Field("note_count", Services.Score.NoteCount);
                Field("max_window_ms", Values.MaxJudgeWindow);
                Field("perfect_window_ms", Values.PerfectJudgeWindow);
                Field("good_window_ms", Values.GoodJudgeWindow);
                Field("miss_window_ms", Values.MissJudgeWindow);
                Field("device", SystemInfo.deviceModel);
                Field("app_version", Application.version);
                Field("refresh_rate", Screen.currentResolution.refreshRateRatio.value);
                Field("target_fps", Application.targetFrameRate);
                Commit();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        public static void End()
        {
            if (!active)
            {
                return;
            }

            try
            {
                Start("footer");
                Clocks(DateTimeOffset.Now);
                Counters();
                Commit();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"PlayLog: {e.Message}");
            }

            active = false;
            timer?.Dispose();
            timer = null;
            Flush();
            lock (WriterGate)
            {
                try
                {
                    writer?.Dispose();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"PlayLog: {e.Message}");
                }

                writer = null;
            }
        }

        public static void Flush()
        {
            lock (WriterGate)
            {
                if (writer == null)
                {
                    return;
                }

                lock (PendingGate)
                {
                    (pending, writing) = (writing, pending);
                }

                try
                {
                    if (writing.Length > 0)
                    {
                        writer.Write(writing);
                        writer.Flush();
                    }
                }
                catch (Exception e)
                {
                    active = false;
                    writer = null;
                    Debug.LogWarning($"PlayLog: {e.Message}");
                }

                writing.Clear();
            }
        }

        public static void Judge(Tap note, JudgementResult result, int offset)
        {
            if (!StartJudgement("tap", note, result, offset))
            {
                return;
            }

            Field("lane", note.Lane);
            Commit();
        }

        public static void Judge(ArcTap note, JudgementResult result, int offset)
        {
            if (!StartJudgement("arctap", note, result, offset))
            {
                return;
            }

            Field("x", note.WorldX);
            Field("y", note.WorldY);
            Field("arc_id", note.Arc != null ? RuntimeHelpers.GetHashCode(note.Arc) : 0);
            Commit();
        }

        public static void Judge(Hold note, JudgementResult result)
        {
            if (!StartJudgement("hold", note, result, null))
            {
                return;
            }

            Field("end", note.EndTiming);
            Field("lane", note.Lane);
            Commit();
        }

        public static void Judge(Arc note, JudgementResult result)
        {
            if (!StartJudgement("arc", note, result, null))
            {
                return;
            }

            Field("end", note.EndTiming);
            Field("color", note.Color);
            Field("x0", note.XStart);
            Field("x1", note.XEnd);
            Field("y0", note.YStart);
            Field("y1", note.YEnd);
            Commit();
        }

        public static void Play(int fromAudioTiming, int delayMs, bool resetJudge)
        {
            if (!StartEvent("play"))
            {
                return;
            }

            Field("from_audio_ms", fromAudioTiming);
            Field("delay_ms", delayMs);
            Field("reset_judge", resetJudge);
            Commit();
        }

        public static void Pause()
        {
            if (StartEvent("pause"))
            {
                Commit();
            }
        }

        public static void Stop()
        {
            if (StartEvent("stop"))
            {
                Commit();
            }
        }

        public static void Seek(int toAudioTiming)
        {
            if (!StartEvent("seek"))
            {
                return;
            }

            Field("to_audio_ms", toAudioTiming);
            Commit();
        }

        public static void ResumeAt(int audioTiming)
        {
            if (!StartEvent("resume_at"))
            {
                return;
            }

            Field("to_audio_ms", audioTiming);
            Commit();
        }

        public static void Speed(float speed)
        {
            if (!StartEvent("speed"))
            {
                return;
            }

            Field("value", speed);
            Field("full_offset_ms", Services.Audio.FullOffset);
            Commit();
        }

        public static void Loop(bool enabled, int fromAudioTiming, int toAudioTiming, string leadIn)
        {
            if (!active)
            {
                return;
            }

            string state = $"{enabled} {fromAudioTiming} {toAudioTiming} {leadIn}";
            if (state == lastLoop || !StartEvent("loop"))
            {
                return;
            }

            lastLoop = state;

            Field("enabled", enabled);
            Field("from_audio_ms", fromAudioTiming);
            Field("to_audio_ms", toAudioTiming);
            Field("lead_in", leadIn);
            Commit();
        }

        public static void LoopWrap(string kind, int fromAudioTiming, int toAudioTiming, int delayMs)
        {
            if (!StartEvent("loop_wrap"))
            {
                return;
            }

            Field("kind", kind);
            Field("from_audio_ms", fromAudioTiming);
            Field("to_audio_ms", toAudioTiming);
            Field("delay_ms", delayMs);
            Commit();
        }

        public static void Retry()
        {
            if (StartEvent("retry"))
            {
                Commit();
            }
        }

        public static void AudioEnd()
        {
            if (!StartEvent("audio_end"))
            {
                return;
            }

            Field("score", Services.Score.CurrentScore);
            Counters();
            Commit();
        }

        private static bool StartJudgement(string type, ArcEvent note, JudgementResult result, int? offset)
        {
            if (!StartEvent("judge"))
            {
                return false;
            }

            Counts[(int)result]++;
            Field("type", type);
            Field("id", RuntimeHelpers.GetHashCode(note));
            Field("timing", note.Timing);
            Field("group", note.TimingGroup);
            Field("result", result.ToString());
            Field("judge", result.IsMiss() ? "lost" : result.IsGood() ? "far" : "pure");
            Field("max", result.IsMax());
            Field("side", Side(result));
            if (offset.HasValue)
            {
                Field("offset_ms", offset.Value);
            }
            else
            {
                Null("offset_ms");
            }

            return true;
        }

        private static bool StartEvent(string type)
        {
            if (!active)
            {
                return false;
            }

            try
            {
                Start(type);
                Field("audio_ms", Services.Audio.AudioTiming);
                Field("chart_ms", Services.Audio.ChartTiming);
                Field("playing", Services.Audio.IsPlaying);
                return true;
            }
            catch (Exception e)
            {
                Fail(e);
                return false;
            }
        }

        private static string Side(JudgementResult result)
        {
            switch (result)
            {
                case JudgementResult.MissEarly:
                case JudgementResult.GoodEarly:
                case JudgementResult.PerfectEarly:
                    return "early";
                case JudgementResult.MissLate:
                case JudgementResult.GoodLate:
                case JudgementResult.PerfectLate:
                    return "late";
                default:
                    return null;
            }
        }

        private static void Clocks(DateTimeOffset now)
        {
            Field("wall", now.ToString("yyyy-MM-ddTHH:mm:ss.fffzzz", CultureInfo.InvariantCulture));
            Field("unix_ms", (now.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks) / (double)TimeSpan.TicksPerMillisecond);
#if UNITY_ANDROID && !UNITY_EDITOR
            using (AndroidJavaClass clock = new AndroidJavaClass("android.os.SystemClock"))
            {
                Field("uptime_ms", clock.CallStatic<long>("uptimeMillis"));
                Field("elapsed_realtime_ms", clock.CallStatic<long>("elapsedRealtimeNanos") / 1e6);
            }
#endif
        }

        private static void Counters()
        {
            int pure = 0;
            int far = 0;
            int lost = 0;
            int early = 0;
            int late = 0;
            for (int i = 0; i < Counts.Length; i++)
            {
                JudgementResult result = (JudgementResult)i;
                Field(result.ToString(), Counts[i]);
                if (result.IsMiss())
                {
                    lost += Counts[i];
                }
                else if (result.IsGood())
                {
                    far += Counts[i];
                }
                else
                {
                    pure += Counts[i];
                }

                string side = Side(result);
                if (side == "early")
                {
                    early += Counts[i];
                }
                else if (side == "late")
                {
                    late += Counts[i];
                }
            }

            Field("pure", pure);
            Field("far", far);
            Field("lost", lost);
            Field("early", early);
            Field("late", late);
        }

        private static void Start(string type)
        {
            Line.Clear();
            Line.Append("{\"t\":\"").Append(type).Append('"');
            Field("rt_ms", Time.realtimeSinceStartupAsDouble * 1000);
            Field("frame", Time.frameCount);
        }

        private static void Commit()
        {
            Line.Append("}\n");
            lock (PendingGate)
            {
                pending.Append(Line);
            }
        }

        private static void Fail(Exception e)
        {
            Debug.LogWarning($"PlayLog: {e.Message}");
            active = false;
            timer?.Dispose();
            timer = null;
            lock (WriterGate)
            {
                try
                {
                    writer?.Dispose();
                }
                catch
                {
                }

                writer = null;
            }
        }

        private static void Key(string key) => Line.Append(",\"").Append(key).Append("\":");

        private static void Null(string key)
        {
            Key(key);
            Line.Append("null");
        }

        private static void Field(string key, int value)
        {
            Key(key);
            Line.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Field(string key, long value)
        {
            Key(key);
            Line.Append(value.ToString(CultureInfo.InvariantCulture));
        }

        private static void Field(string key, double value)
        {
            Key(key);
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                Line.Append("null");
            }
            else
            {
                Line.Append(value.ToString("0.###", CultureInfo.InvariantCulture));
            }
        }

        private static void Field(string key, float value) => Field(key, (double)value);

        private static void Field(string key, bool value)
        {
            Key(key);
            Line.Append(value ? "true" : "false");
        }

        private static void Field(string key, string value)
        {
            Key(key);
            if (value == null)
            {
                Line.Append("null");
                return;
            }

            Line.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        Line.Append("\\\"");
                        break;
                    case '\\':
                        Line.Append("\\\\");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            Line.Append("\\u").Append(((int)c).ToString("x4"));
                        }
                        else
                        {
                            Line.Append(c);
                        }

                        break;
                }
            }

            Line.Append('"');
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "unknown";
            }

            var sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '-' ? c : '_');
            }

            return sb.ToString();
        }
    }
}
