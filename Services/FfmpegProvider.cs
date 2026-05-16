using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace WhisperTranscribe.Services;

public class FfmpegProvider
{
    private const string DownloadUrl =
        "https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip";

    public bool IsInstalled =>
        File.Exists(AppPaths.FfmpegExe) && File.Exists(AppPaths.FfprobeExe);

    public event Action<string>? Log;
    public event Action<double>? Progress;

    public async Task EnsureInstalledAsync(CancellationToken ct = default)
    {
        if (IsInstalled) return;

        AppPaths.EnsureDirectories();
        Log?.Invoke("ffmpeg をダウンロードしています...");

        var zipPath = Path.Combine(AppPaths.TempDir, "ffmpeg.zip");
        await DownloadAsync(DownloadUrl, zipPath, ct);

        Log?.Invoke("ffmpeg を展開しています...");
        ExtractFfmpeg(zipPath);

        try { File.Delete(zipPath); } catch { }

        if (!IsInstalled)
            throw new InvalidOperationException("ffmpeg のインストールに失敗しました。");

        Log?.Invoke("ffmpeg のインストール完了");
    }

    private async Task DownloadAsync(string url, string dest, CancellationToken ct)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromMinutes(30);
        using var response = await http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? -1L;
        await using var src = await response.Content.ReadAsStreamAsync(ct);
        await using var dst = File.Create(dest);

        var buffer = new byte[81920];
        long readTotal = 0;
        int read;
        while ((read = await src.ReadAsync(buffer, ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, read), ct);
            readTotal += read;
            if (total > 0) Progress?.Invoke((double)readTotal / total);
        }
    }

    private void ExtractFfmpeg(string zipPath)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        foreach (var entry in archive.Entries)
        {
            var name = Path.GetFileName(entry.FullName);
            if (string.IsNullOrEmpty(name)) continue;

            if (name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
                name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
            {
                var dest = Path.Combine(AppPaths.FfmpegDir, name);
                entry.ExtractToFile(dest, overwrite: true);
            }
        }
    }
}
