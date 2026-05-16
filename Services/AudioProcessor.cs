using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WhisperTranscribe.Services;

/// <summary>
/// 複数の入力ファイル(WAV/MP3/M4A/MP4 等、32bit float も可)を
/// 連結 → ラウドネス正規化 → 16kHz/mono/16bit PCM WAV に変換する。
/// </summary>
public class AudioProcessor
{
    public event Action<string>? Log;

    public async Task<string> ProcessAsync(
        IReadOnlyList<string> inputFiles,
        bool normalize,
        CancellationToken ct = default)
    {
        if (inputFiles.Count == 0) throw new ArgumentException("入力ファイルが空です。");
        if (!File.Exists(AppPaths.FfmpegExe))
            throw new InvalidOperationException("ffmpeg.exe が見つかりません。");

        AppPaths.EnsureDirectories();
        var work = Path.Combine(AppPaths.TempDir, "job_" + DateTime.Now.ToString("yyyyMMdd_HHmmss"));
        Directory.CreateDirectory(work);

        // 1. 各ファイルを 16kHz/mono/pcm_s16le の中間 WAV に変換
        var intermediates = new List<string>();
        for (int i = 0; i < inputFiles.Count; i++)
        {
            var src = inputFiles[i];
            var dst = Path.Combine(work, $"part_{i:D4}.wav");
            Log?.Invoke($"[{i + 1}/{inputFiles.Count}] デコード中: {Path.GetFileName(src)}");
            await RunFfmpegAsync(
                $"-y -hide_banner -loglevel error -i \"{src}\" -vn -ar 16000 -ac 1 -c:a pcm_s16le \"{dst}\"",
                ct);
            intermediates.Add(dst);
        }

        // 2. concat demuxer 用リスト作成
        var listFile = Path.Combine(work, "concat.txt");
        var sb = new StringBuilder();
        foreach (var p in intermediates)
            sb.AppendLine($"file '{p.Replace("'", "''")}'");
        await File.WriteAllTextAsync(listFile, sb.ToString(), new UTF8Encoding(false), ct);

        // 3. 連結 (+ 任意で loudnorm) して最終 WAV
        var finalWav = Path.Combine(work, "final.wav");
        var filter = normalize ? "-af loudnorm=I=-16:TP=-1.5:LRA=11 " : "";
        Log?.Invoke(normalize ? "連結 + ラウドネス正規化中..." : "連結中...");
        await RunFfmpegAsync(
            $"-y -hide_banner -loglevel error -f concat -safe 0 -i \"{listFile}\" {filter}-ar 16000 -ac 1 -c:a pcm_s16le \"{finalWav}\"",
            ct);

        // 中間ファイル削除（最終 WAV は残す）
        foreach (var p in intermediates)
        {
            try { File.Delete(p); } catch { }
        }
        try { File.Delete(listFile); } catch { }

        Log?.Invoke($"前処理完了: {finalWav}");
        return finalWav;
    }

    private async Task RunFfmpegAsync(string args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = AppPaths.FfmpegExe,
            Arguments = args,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        using var proc = Process.Start(psi)!;
        var stderr = await proc.StandardError.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0)
            throw new InvalidOperationException($"ffmpeg 失敗 (exit {proc.ExitCode}):\n{stderr}");
    }
}
