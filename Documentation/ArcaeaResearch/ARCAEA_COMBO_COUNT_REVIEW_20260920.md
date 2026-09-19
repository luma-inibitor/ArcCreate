# ArcCreate(Next) 判定点・ノーツ数の計算 確認結果

| 項目 | 内容 |
|---|---|
| 作成日 | 2026-09-20 |
| 作成者 | Claude（Claude Code / Claude Sonnet 5）、依頼: jinn |
| 対象 | `Assets/Scripts/Gameplay/Data/Events/Arc.Judgement.cs`、`Hold.cs`、`LongNote.cs`、`Chart/ArcConnection.cs` |
| 比較先 | 本家7.0.255の逆コンパイル（`0x17cb97c` I計算、`0xd92558` 粒生成、`0xba81e8` Arc上書き。ELF仮想アドレス） |
| 集計対象 | `D:\work\arcaea\songs` の 0〜3.aff にある、判定ありのArc/Hold 365097 本 |
| 再現 | `tools/review_combo_count_20260920.py`（同じ集計を再実行できる） |
| 注意 | この確認の時点では、ArcCreate側のコードは変更していない（コードを読んで、譜面で計算した） |
| 修正 | 同日、ブランチ `claude/fix-combo-count-honke` で、A・B・Cの1〜3（短いHoldの時刻、判定要求の時刻、二回分束ね）を修正した（0.10.4）。Cの4（`designant`）と5（接続の時刻条件）は未修正 |

## 結論

- 式の骨格（間隔、先頭/接続後続の開始添字、短い区間は1個、長さ0とトレースは0個）は、本家と一致している。
- **ノーツ数が本家とずれる原因が2つ**ある（下のAとB）。

## A. 単精度(float)と倍精度(double)の違い

本家は、間隔も箱の数も**float（単精度）**で計算している（`((60000/|BPM|)/係数)/D`、`(int)((float)長さ/I)`）。ArcCreateはdoubleで計算している。長さがちょうど箱の整数倍になる譜面で、箱の数が1ずれる。

実物譜面での該当: **20 本**（判定あり全体の約 0.0055%）

| 譜面 | 行 | 種類 | 開始 | 終了 | BPM | 本家(float)の箱の数 | ArcCreate(double)の箱の数 |
|---|---:|---|---:|---:|---:|---:|---:|
| alterego/2.aff | 1414 | hold | 106044 | 110044 | 780 | 51 | 52 |
| alterego/2.aff | 1415 | hold | 106044 | 110044 | 780 | 51 | 52 |
| andrevivemelody/1.aff | 830 | arc | 163132 | 167632 | 840 | 62 | 63 |
| andrevivemelody/1.aff | 836 | arc | 163132 | 167632 | 840 | 62 | 63 |
| flashback/2.aff | 366 | arc | 77538 | 79538 | 195 | 12 | 13 |
| flashback/2.aff | 367 | arc | 77538 | 79538 | 195 | 12 | 13 |
| laqryma/0.aff | 116 | arc | 60000 | 61250 | 168 | 7 | 6 |
| laqryma/2.aff | 371 | arc | 60000 | 61250 | 168 | 7 | 6 |
| mahoroba/2.aff | 372 | hold | 60615 | 62615 | 195 | 12 | 13 |
| manicjeer/0.aff | 185 | hold | 88461 | 90461 | 195 | 12 | 13 |
| particlearts/0.aff | 211 | hold | 124128 | 129128 | 168 | 28 | 27 |
| particlearts/0.aff | 214 | hold | 129842 | 134842 | 168 | 28 | 27 |
| particlearts/1.aff | 318 | arc | 121271 | 122521 | 168 | 7 | 6 |
| particlearts/1.aff | 319 | arc | 121271 | 122521 | 168 | 7 | 6 |
| particlearts/1.aff | 320 | hold | 122699 | 123949 | 168 | 7 | 6 |
| particlearts/1.aff | 321 | hold | 122699 | 123949 | 168 | 7 | 6 |
| particlearts/2.aff | 834 | hold | 119842 | 121092 | 168 | 7 | 6 |
| particlearts/2.aff | 835 | hold | 119842 | 121092 | 168 | 7 | 6 |
| particlearts/2.aff | 838 | arc | 122699 | 123949 | 168 | 7 | 6 |
| particlearts/2.aff | 839 | arc | 122699 | 123949 | 168 | 7 | 6 |

直し方の例（C#）:

```csharp
float bpmF = (float)System.Math.Abs(bpm);
float factor = bpmF < 255f ? 2f : 1f;
float a = 60000f / bpmF;
float b = a / factor;
float increment = b / (float)Values.TimingPointDensity;
int total = (int)((float)duration / increment);
```

各段階をfloat変数に受けて、高い精度で計算されるのを防ぐ。

## B. 始点のBPMが0のArc/Hold

ArcCreateは0個（`if (bpm == 0) return;`）。本家は、間隔が無限大になり、箱が0個になるので、**中央に1個**（長さが0でなければ）。
過去の調査でも、Arcahv Presentの公称ノーツ数とArcCreateの規則が2つ合わなかった（`AFF_NOTE_JUDGEMENT_RESEARCH.md`）。

今回の集計での該当: **5 本**（同文書は358件と書いている。条件の数え方が違う可能性があり、要照合）

| 譜面 | 行 | 種類 | 開始 | 終了 |
|---|---:|---|---:|---:|
| aethercrest/2.aff | 1317 | arc | 156010 | 156011 |
| aethercrest/2.aff | 1318 | arc | 156010 | 156011 |
| arcahv/1.aff | 205 | hold | 63455 | 63770 |
| arcahv/1.aff | 206 | hold | 63455 | 63770 |
| pragmatism/3.aff | 1443 | hold | 152757 | 152930 |

直し方: `bpm == 0`のとき、長さが0でなければ`TotalCombo = 1`にして、時刻は中央にする。

## C. 数には影響しないが、本家と違う点（要確認）

1. **短いHold**: `FirstJudgeTime = Timing`（始点）。本家は中央。
2. **判定要求の時刻**（`Arc.Judgement.cs`の`RequestJudgement`）: `Timing + t * TimeIncrement`。先頭Arcの最初の粒は、本家では`始点 + I`、短いArcでは中央。`ComboAt`（`FirstJudgeTime`を使う）と食い違う。
3. **まとめる処理（二回分束ね）**は未実装。コンボの合計は変わらないが、粒の時刻と重みが違う。
4. **`designant`線種**は、ArcCreateのGameplay/ChartFormatに扱いがない（検索で0件）。実物譜面には89本ある。本家での扱いは未確認。
5. **接続の時刻条件**: ArcCreateは`abs(差) <= 9`。本家が符号付きか絶対値かは、7.0.255では未確認。

## D. 一致していた点

- `I = (BPM>=255 ? 60000 : 30000) / |BPM| / D`（式は一致。精度だけがA）
- 先頭Arc: 箱の数 −1、接続後続: 箱の数（`comboModifier`）。短い区間は1個。
- 長さ0とトレース: 0個。`noinput`グループ: 0個。
- 接続の条件（X差 < 0.1、Yが一致、線種が同じ）
