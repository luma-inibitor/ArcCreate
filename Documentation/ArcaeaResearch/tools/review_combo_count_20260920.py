"""ArcCreate(Next)の判定点数の計算を、本家7.0.255の演算(float32)と比べて、実物譜面で差を数える。

作成: 2026-09-20 Claude（Claude Sonnet 5）、依頼: jinn
使い方: python review_combo_count_20260920.py [songsフォルダ] [出力md]
ArcCreateのコードは読み込まない。ArcCreateの式(double)を、この中に写している。
"""
import glob
import os
import re
import sys

import numpy as np

SONGS = sys.argv[1] if len(sys.argv) > 1 else r"D:\work\arcaea\songs"
OUT = sys.argv[2] if len(sys.argv) > 2 else os.path.join(
    os.path.dirname(os.path.abspath(__file__)), "..", "ARCAEA_COMBO_COUNT_REVIEW_20260920.md")

ARC = re.compile(r'\s*arc\((-?\d+),(-?\d+),(-?[\d.]+),(-?[\d.]+),(\w+),(-?[\d.]+),(-?[\d.]+),(\d+),(\w+),(true|false|designant)')
HOLD = re.compile(r'\s*hold\((-?\d+),(-?\d+),(\d+)\)')
TIMING = re.compile(r'\s*timing\((-?\d+),(-?[\d.]+),(-?[\d.]+)\)')
f32 = np.float32


def total_game(duration, bpm, density):
    """本家7.0.255: ((60000/|B|)/係数)/D をfloat32で、(int)((float)長さ/I)。"""
    b = f32(abs(bpm))
    factor = f32(2.0 if b < 255 else 1.0)
    inc = f32(f32(f32(60000) / b) / factor) / f32(density)
    return int(f32(duration) / inc)


def total_arccreate(duration, bpm, density):
    """ArcCreate: doubleで (BPM>=255 ? 60000 : 30000) / |BPM| / D、(int)(長さ/I)。"""
    b = abs(bpm)
    inc = (60000 if b >= 255 else 30000) / b / density
    return int(duration / inc)


def main():
    mismatch, zero_bpm, judged = [], [], 0
    for path in sorted(glob.glob(os.path.join(SONGS, "*", "[0-3].aff"))):
        try:
            lines = open(path, encoding="utf-8").read().splitlines()
        except UnicodeDecodeError as e:
            raise RuntimeError(f"UTF-8で読めない譜面: {path}") from e
        density = 1.0
        for line in lines[:6]:
            if line.startswith("TimingPointDensityFactor"):
                density = float(line.split(":")[1])
        groups, notes, cur = [[]], [[]], 0
        for idx, line in enumerate(lines):
            s = line.strip()
            if s.startswith("timinggroup("):
                groups.append([])
                notes.append([])
                cur = len(groups) - 1
                continue
            if cur and s.startswith("};"):
                cur = 0
                continue
            m = TIMING.match(line)
            if m:
                groups[cur].append((int(m.group(1)), float(m.group(2))))
                continue
            m = ARC.match(line)
            if m and m.group(10) == "false":
                notes[cur].append(("arc", int(m.group(1)), int(m.group(2)), idx + 1))
                continue
            m = HOLD.match(line)
            if m:
                notes[cur].append(("hold", int(m.group(1)), int(m.group(2)), idx + 1))
        rel = os.path.relpath(path, SONGS).replace("\\", "/")
        for g, timings in enumerate(groups):
            timings = sorted(timings)
            for kind, start, end, line_no in notes[g]:
                if end <= start or not timings:
                    continue
                bpm = timings[0][1]
                for t, b in timings:
                    if t <= start:
                        bpm = b
                judged += 1
                if bpm == 0:
                    zero_bpm.append((rel, line_no, kind, start, end))
                    continue
                a = total_game(end - start, bpm, density)
                c = total_arccreate(end - start, bpm, density)
                if a != c:
                    mismatch.append((rel, line_no, kind, start, end, bpm, a, c))

    out = []
    out.append("# ArcCreate(Next) 判定点・ノーツ数の計算 確認結果\n\n")
    out.append("| 項目 | 内容 |\n|---|---|\n")
    out.append("| 作成日 | 2026-09-20 |\n")
    out.append("| 作成者 | Claude（Claude Code / Claude Sonnet 5）、依頼: jinn |\n")
    out.append("| 対象 | `Assets/Scripts/Gameplay/Data/Events/Arc.Judgement.cs`、`Hold.cs`、`LongNote.cs`、`Chart/ArcConnection.cs` |\n")
    out.append("| 比較先 | 本家7.0.255の逆コンパイル（`0x17cb97c` I計算、`0xd92558` 粒生成、`0xba81e8` Arc上書き。ELF仮想アドレス） |\n")
    out.append(f"| 集計対象 | `{SONGS}` の 0〜3.aff にある、判定ありのArc/Hold {judged} 本 |\n")
    out.append("| 再現 | `tools/review_combo_count_20260920.py`（同じ集計を再実行できる） |\n")
    out.append("| 注意 | ArcCreate側のコードは変更していない。実行もしていない（コードを読んで、譜面で計算した） |\n\n")
    out.append("## 結論\n\n")
    out.append("- 式の骨格（間隔、先頭/接続後続の開始添字、短い区間は1個、長さ0とトレースは0個）は、本家と一致している。\n")
    out.append("- **ノーツ数が本家とずれる原因が2つ**ある（下のAとB）。\n\n")
    out.append("## A. 単精度(float)と倍精度(double)の違い\n\n")
    out.append("本家は、間隔も箱の数も**float（単精度）**で計算している（`((60000/|BPM|)/係数)/D`、`(int)((float)長さ/I)`）。ArcCreateはdoubleで計算している。長さがちょうど箱の整数倍になる譜面で、箱の数が1ずれる。\n\n")
    pct = 100 * len(mismatch) / judged if judged else 0
    out.append(f"実物譜面での該当: **{len(mismatch)} 本**（判定あり全体の約 {pct:.4f}%）\n\n")
    out.append("| 譜面 | 行 | 種類 | 開始 | 終了 | BPM | 本家(float)の箱の数 | ArcCreate(double)の箱の数 |\n|---|---:|---|---:|---:|---:|---:|---:|\n")
    for m in mismatch:
        out.append(f"| {m[0]} | {m[1]} | {m[2]} | {m[3]} | {m[4]} | {m[5]:g} | {m[6]} | {m[7]} |\n")
    out.append("\n直し方の例（C#）:\n\n```csharp\n")
    out.append("float bpmF = (float)System.Math.Abs(bpm);\nfloat factor = bpmF < 255f ? 2f : 1f;\nfloat a = 60000f / bpmF;\nfloat b = a / factor;\nfloat increment = b / (float)Values.TimingPointDensity;\nint total = (int)((float)duration / increment);\n")
    out.append("```\n\n各段階をfloat変数に受けて、高い精度で計算されるのを防ぐ。\n\n")
    out.append("## B. 始点のBPMが0のArc/Hold\n\n")
    out.append("ArcCreateは0個（`if (bpm == 0) return;`）。本家は、間隔が無限大になり、箱が0個になるので、**中央に1個**（長さが0でなければ）。\n")
    out.append("過去の調査でも、Arcahv Presentの公称ノーツ数とArcCreateの規則が2つ合わなかった（`AFF_NOTE_JUDGEMENT_RESEARCH.md`）。\n\n")
    out.append(f"今回の集計での該当: **{len(zero_bpm)} 本**（同文書は358件と書いている。条件の数え方が違う可能性があり、要照合）\n\n")
    out.append("| 譜面 | 行 | 種類 | 開始 | 終了 |\n|---|---:|---|---:|---:|\n")
    for z in zero_bpm:
        out.append(f"| {z[0]} | {z[1]} | {z[2]} | {z[3]} | {z[4]} |\n")
    out.append("\n直し方: `bpm == 0`のとき、長さが0でなければ`TotalCombo = 1`にして、時刻は中央にする。\n\n")
    out.append("## C. 数には影響しないが、本家と違う点（要確認）\n\n")
    out.append("1. **短いHold**: `FirstJudgeTime = Timing`（始点）。本家は中央。\n")
    out.append("2. **判定要求の時刻**（`Arc.Judgement.cs`の`RequestJudgement`）: `Timing + t * TimeIncrement`。先頭Arcの最初の粒は、本家では`始点 + I`、短いArcでは中央。`ComboAt`（`FirstJudgeTime`を使う）と食い違う。\n")
    out.append("3. **まとめる処理（二回分束ね）**は未実装。コンボの合計は変わらないが、粒の時刻と重みが違う。\n")
    out.append("4. **`designant`線種**は、ArcCreateのGameplay/ChartFormatに扱いがない（検索で0件）。実物譜面には89本ある。本家での扱いは未確認。\n")
    out.append("5. **接続の時刻条件**: ArcCreateは`abs(差) <= 9`。本家が符号付きか絶対値かは、7.0.255では未確認。\n\n")
    out.append("## D. 一致していた点\n\n")
    out.append("- `I = (BPM>=255 ? 60000 : 30000) / |BPM| / D`（式は一致。精度だけがA）\n")
    out.append("- 先頭Arc: 箱の数 −1、接続後続: 箱の数（`comboModifier`）。短い区間は1個。\n")
    out.append("- 長さ0とトレース: 0個。`noinput`グループ: 0個。\n")
    out.append("- 接続の条件（X差 < 0.1、Yが一致、線種が同じ）\n")
    with open(OUT, "w", encoding="utf-8", newline="") as fh:
        fh.write("".join(out))
    print(len(mismatch), len(zero_bpm), judged)


if __name__ == "__main__":
    main()
