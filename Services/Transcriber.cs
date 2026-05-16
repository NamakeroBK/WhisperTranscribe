using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Whisper.net;
using Whisper.net.SamplingStrategy;

namespace WhisperTranscribe.Services;

/// <summary>
/// Whisper.net による文字起こし。
/// パラメータは zenn.dev/hongbod/articles/def04f586cf168 の推奨設定を C# 相当で適用:
///   - temperature = 0.0 (確定的デコーディング)
///   - no_context  = true (前回コンテキストを引き継がず幻覚を抑制)
///   - beam_size   = 5 (標準) / 10 (高品質モード)
/// </summary>
public class Transcriber
{
    public event Action<string>? Log;
    public event Action<int>? Progress;

    public class Options
    {
        public string Language { get; init; } = "auto";
        public bool HighQuality { get; init; } = false;
        public int Threads { get; init; } = 0; // 0 = 自動

        /// <summary>
        /// Whisper の initial_prompt。指示文ではなく「模範的な書き起こし例」として書く。
        /// 上限 224 トークン (日本語で約 150〜180 文字)。
        /// 句読点付きの完成文 2〜3 文 + 固有名詞 10〜15 語が目安。
        /// </summary>
        public string? InitialPrompt { get; init; }
    }

    public class Segment
    {
        public TimeSpan Start { get; init; }
        public TimeSpan End { get; init; }
        public string Text { get; init; } = "";
    }

    public async Task<IReadOnlyList<Segment>> TranscribeAsync(
        string wavPath,
        string modelPath,
        Options options,
        CancellationToken ct = default)
    {
        if (!File.Exists(modelPath))
            throw new FileNotFoundException("モデルファイルが見つかりません。", modelPath);

        Log?.Invoke($"モデル読み込み: {Path.GetFileName(modelPath)}");
        using var factory = WhisperFactory.FromPath(modelPath);

        var beamSize = options.HighQuality ? 10 : 5;
        var threads = options.Threads > 0 ? options.Threads : Math.Max(1, Environment.ProcessorCount - 1);

        var builder = factory.CreateBuilder()
            .WithLanguage(string.IsNullOrWhiteSpace(options.Language) ? "auto" : options.Language)
            .WithThreads(threads)
            .WithTemperature(0.0f)
            .WithNoContext()                       // condition_on_previous_text=False 相当 (幻覚抑制)
            .WithEntropyThreshold(2.4f)
            .WithLogProbThreshold(-1.0f)
            .WithProgressHandler(p => Progress?.Invoke(p));

        if (!string.IsNullOrWhiteSpace(options.InitialPrompt))
        {
            builder = builder.WithPrompt(options.InitialPrompt);
            Log?.Invoke($"initial_prompt 設定 ({options.InitialPrompt.Length} 文字)");
        }

        var beam = (BeamSearchSamplingStrategyBuilder)builder.WithBeamSearchSamplingStrategy();
        beam.WithBeamSize(beamSize).WithPatience(1.0f);
        builder = beam.ParentBuilder;

        using var processor = builder.Build();

        Log?.Invoke($"文字起こし開始 (beam={beamSize}, threads={threads}, lang={options.Language})");
        var results = new List<Segment>();
        await using var fs = File.OpenRead(wavPath);

        await foreach (var seg in processor.ProcessAsync(fs, ct))
        {
            results.Add(new Segment
            {
                Start = seg.Start,
                End = seg.End,
                Text = seg.Text?.Trim() ?? ""
            });
        }

        Log?.Invoke($"文字起こし完了 ({results.Count} セグメント)");
        return results;
    }

    public static void WriteText(string path, IReadOnlyList<Segment> segs)
    {
        var sb = new StringBuilder();
        foreach (var s in segs) sb.AppendLine(s.Text);
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static void WriteSrt(string path, IReadOnlyList<Segment> segs)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < segs.Count; i++)
        {
            sb.AppendLine((i + 1).ToString(CultureInfo.InvariantCulture));
            sb.Append(FormatTime(segs[i].Start, ',')).Append(" --> ").AppendLine(FormatTime(segs[i].End, ','));
            sb.AppendLine(segs[i].Text);
            sb.AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    public static void WriteVtt(string path, IReadOnlyList<Segment> segs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("WEBVTT");
        sb.AppendLine();
        foreach (var s in segs)
        {
            sb.Append(FormatTime(s.Start, '.')).Append(" --> ").AppendLine(FormatTime(s.End, '.'));
            sb.AppendLine(s.Text);
            sb.AppendLine();
        }
        File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
    }

    private static string FormatTime(TimeSpan t, char msSep)
    {
        return string.Format(CultureInfo.InvariantCulture, "{0:D2}:{1:D2}:{2:D2}{3}{4:D3}",
            (int)t.TotalHours, t.Minutes, t.Seconds, msSep, t.Milliseconds);
    }
}
