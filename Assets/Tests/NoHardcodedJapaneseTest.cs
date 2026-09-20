using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Tests.Unit
{
    // UIの文字列はLocales/*.ymlに置き、I18n.Sで読み込む。
    // Assets/Scripts内の文字列リテラルに日本語が増えていないかを確認する。
    // コメントと、同じ行のDebug.Log系（開発者向けログ）は対象外。
    public class NoHardcodedJapaneseTest
    {
        // 既存の箇所。「ファイル: リテラル」の形式。ロケールへ移したら削除する。
        private static readonly HashSet<string> Baseline = new HashSet<string>
        {
            @"Compose/Project/ProjectService.cs: $""ファイルが見つかりません。\n{path}""",
            @"Compose/Project/ProjectService.cs: ""AFF、OGG、JPG、または曲フォルダをドロップしてください。""",
            @"Compose/Project/ProjectService.cs: $""譜面に対応するOGGが見つかりません。\n{Path.GetFileName(path)}""",
            @"Compose/Project/ProjectService.cs: $""曲フォルダに編集可能なAFFとOGG、またはbase.oggが見つかりません。\n{directory}""",
            @"Compose/Project/ProjectService.cs: ""キャンセル""",
            @"Compose/Project/ProjectService.cs: ""譜面を選択""",
            @"Compose/Project/ProjectService.cs: $""{GetSourceName(sourcePath)}で編集するAFFを選んでください。""",
            @"Compose/Project/ProjectService.cs: ""選択した譜面を開くためのOGGが見つかりません。""",
            @"Compose/Project/ProjectService.cs: ""履歴""",
            @"Compose/Project/ProjectService.cs: ""読み込み履歴はまだありません。""",
            @"Compose/Project/ProjectService.cs: ""読み込み履歴""",
            @"Compose/Project/ProjectService.cs: ""読み込む曲フォルダを選択してください。""",
            @"Compose/Project/ProjectService.cs: ""一度に開けるファイルは1個です。先頭のファイルを開きます。""",
            @"Compose/Project/SonglistResolver.cs: $""本家とArcCreate Nextのsonglistに同じIDがあります: {song.Name}""",
            @"Compose/Project/SonglistResolver.cs: $""songlistを読み込めませんでした: {exception.Message}""",
            @"Compose/Project/SonglistResolver.cs: $""{Path.GetFileName(path)}にsongs配列がありません。""",

            // 表示用ではなく作曲者名の判定に使う文字列。
            @"Compose/Project/UI/ChartInformationUI.cs: ""かめりあ""",
        };

        [Test]
        public void Scripts_HaveNoNewJapaneseStringLiterals()
        {
            string root = Path.Combine(Application.dataPath, "Scripts");
            List<string> found = new List<string>();
            HashSet<string> unused = new HashSet<string>(Baseline);

            foreach (string file in Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
            {
                string relative = file.Substring(root.Length + 1).Replace('\\', '/');
                foreach (JapaneseLiteral literal in JapaneseLiteralScanner.Scan(File.ReadAllText(file)))
                {
                    string key = $"{relative}: {literal.Text}";
                    unused.Remove(key);
                    if (!Baseline.Contains(key))
                    {
                        found.Add($"Assets/Scripts/{relative}:{literal.Line}: {literal.Text}");
                    }
                }
            }

            if (found.Count > 0)
            {
                Assert.Fail(
                    "日本語の文字列リテラルがC#に直接書かれています。"
                    + "Locales/*.ymlへ移してI18n.Sで参照してください。\n"
                    + "Hardcoded Japanese string literals found. Move them to Locales/*.yml and use I18n.S.\n"
                    + string.Join("\n", found));
            }

            if (unused.Count > 0)
            {
                Assert.Fail(
                    "見つからなくなったベースラインの項目があります。Baselineから削除してください。\n"
                    + "Baseline entries no longer found. Remove them from Baseline.\n"
                    + string.Join("\n", unused));
            }
        }

        [Test]
        public void Scan_FindsNormalVerbatimAndInterpolatedLiterals()
        {
            string source = string.Join("\n",
                "string a = \"ファイル\";",
                "string b = @\"C:\\曲\\\"\"x\"\"\";",
                "string c = $\"{path}が見つかりません。\";",
                "string d = $@\"{(ok ? \"\" : \"}\")}「」\";",
                "char e = '\"'; string f = \"。\";",
                "string g = \"English only\";");

            List<JapaneseLiteral> result = JapaneseLiteralScanner.Scan(source);

            Assert.That(result.Select(r => r.Line), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(result[0].Text, Is.EqualTo("\"ファイル\""));
            Assert.That(result[1].Text, Is.EqualTo("@\"C:\\曲\\\"\"x\"\"\""));
            Assert.That(result[2].Text, Is.EqualTo("$\"{path}が見つかりません。\""));
            Assert.That(result[3].Text, Is.EqualTo("$@\"{(ok ? \"\" : \"}\")}「」\""));
            Assert.That(result[4].Text, Is.EqualTo("\"。\""));
        }

        [Test]
        public void Scan_IgnoresCommentsAndDebugLog()
        {
            string source = string.Join("\r\n",
                "// \"コメント\"",
                "/* \"ブロック",
                "   コメント\" */",
                "/// <summary>\"説明\"</summary>",
                "Debug.Log($\"読み込みました: {path}\");",
                "Debug.LogWarning(\"警告\");",
                "UnityEngine.Debug.LogError(\"エラー\");",
                "string url = \"http://example.com\"; // \"後ろのコメント\"",
                "Notify(\"表示\");");

            List<JapaneseLiteral> result = JapaneseLiteralScanner.Scan(source);

            Assert.That(result.Count, Is.EqualTo(1));
            Assert.That(result[0].Line, Is.EqualTo(9));
            Assert.That(result[0].Text, Is.EqualTo("\"表示\""));
        }
    }

    public readonly struct JapaneseLiteral
    {
        public JapaneseLiteral(int line, string text)
        {
            Line = line;
            Text = text;
        }

        public int Line { get; }

        public string Text { get; }
    }

    // 文字列・文字リテラルとコメントだけを見分ける簡単なスキャナー。
    public static class JapaneseLiteralScanner
    {
        public static List<JapaneseLiteral> Scan(string source)
        {
            List<JapaneseLiteral> result = new List<JapaneseLiteral>();
            int line = 1;
            int i = 0;

            while (i < source.Length)
            {
                int start = i;
                if (At(source, i, "//"))
                {
                    i = source.IndexOf('\n', i);
                    if (i < 0)
                    {
                        break;
                    }
                }
                else if (At(source, i, "/*"))
                {
                    int end = source.IndexOf("*/", i + 2, StringComparison.Ordinal);
                    i = end < 0 ? source.Length : end + 2;
                }
                else if (source[i] == '\'')
                {
                    i = SkipCharLiteral(source, i);
                }
                else if (IsStringStart(source, i))
                {
                    i = SkipString(source, i);
                    string text = source.Substring(start, i - start);
                    if (ContainsJapanese(text) && !IsInDebugLog(source, start))
                    {
                        result.Add(new JapaneseLiteral(line, text));
                    }
                }
                else
                {
                    i++;
                }

                line += CountNewLines(source, start, i);
            }

            return result;
        }

        public static bool ContainsJapanese(string text)
        {
            foreach (char c in text)
            {
                if ((c >= '\u3000' && c <= '\u30FF') // 全角記号・ひらがな・カタカナ
                    || (c >= '\u31F0' && c <= '\u31FF')
                    || (c >= '\u3400' && c <= '\u4DBF') // 漢字
                    || (c >= '\u4E00' && c <= '\u9FFF')
                    || (c >= '\uFF00' && c <= '\uFFEF')) // 全角英数・半角カナ
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInDebugLog(string source, int index)
        {
            int lineStart = source.LastIndexOf('\n', Math.Max(index - 1, 0)) + 1;
            return source.IndexOf("Debug.Log", lineStart, index - lineStart, StringComparison.Ordinal) >= 0;
        }

        private static bool IsStringStart(string source, int i)
        {
            return At(source, i, "\"") || At(source, i, "@\"") || At(source, i, "$\"")
                || At(source, i, "$@\"") || At(source, i, "@$\"");
        }

        // 開始位置のリテラルを読み飛ばし、閉じ引用符の次の位置を返す。
        private static int SkipString(string source, int i)
        {
            bool verbatim = false;
            bool interpolated = false;
            while (source[i] != '"')
            {
                verbatim |= source[i] == '@';
                interpolated |= source[i] == '$';
                i++;
            }

            i++;
            while (i < source.Length)
            {
                char c = source[i];
                if (verbatim && At(source, i, "\"\""))
                {
                    i += 2;
                }
                else if (!verbatim && c == '\\')
                {
                    i += 2;
                }
                else if (c == '"')
                {
                    return i + 1;
                }
                else if (interpolated && (At(source, i, "{{") || At(source, i, "}}")))
                {
                    i += 2;
                }
                else if (interpolated && c == '{')
                {
                    i = SkipInterpolationHole(source, i + 1);
                }
                else
                {
                    i++;
                }
            }

            return source.Length;
        }

        // {...}の中は通常のコードなので、入れ子の文字列に注意して対応する}まで進む。
        private static int SkipInterpolationHole(string source, int i)
        {
            int depth = 1;
            while (i < source.Length)
            {
                char c = source[i];
                if (c == '\'')
                {
                    i = SkipCharLiteral(source, i);
                }
                else if (IsStringStart(source, i))
                {
                    i = SkipString(source, i);
                }
                else
                {
                    if (c == '{')
                    {
                        depth++;
                    }
                    else if (c == '}' && --depth == 0)
                    {
                        return i + 1;
                    }

                    i++;
                }
            }

            return source.Length;
        }

        private static int SkipCharLiteral(string source, int i)
        {
            i++;
            while (i < source.Length && source[i] != '\'' && source[i] != '\n')
            {
                i += source[i] == '\\' ? 2 : 1;
            }

            return Math.Min(i + 1, source.Length);
        }

        private static bool At(string source, int i, string value)
        {
            return string.CompareOrdinal(source, i, value, 0, value.Length) == 0;
        }

        private static int CountNewLines(string source, int start, int end)
        {
            int count = 0;
            for (int i = start; i < end && i < source.Length; i++)
            {
                if (source[i] == '\n')
                {
                    count++;
                }
            }

            return count;
        }
    }
}
