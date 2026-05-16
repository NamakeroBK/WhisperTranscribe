using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using WhisperTranscribe.Models;

namespace WhisperTranscribe.Services;

public class ModelManager
{
    public event Action<string>? Log;
    public event Action<double>? Progress;

    public IReadOnlyList<WhisperModelItem> RefreshAll()
    {
        AppPaths.EnsureDirectories();
        foreach (var m in ModelCatalog.All) m.Refresh();
        return ModelCatalog.All;
    }

    public async Task DownloadAsync(WhisperModelItem item, CancellationToken ct = default)
    {
        AppPaths.EnsureDirectories();
        Log?.Invoke($"モデル {item.Name} をダウンロードしています...");

        var tmp = item.FilePath + ".part";
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromHours(2);

        using var response = await http.GetAsync(item.DownloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var total = response.Content.Headers.ContentLength ?? item.ApproxSizeBytes;
        await using (var src = await response.Content.ReadAsStreamAsync(ct))
        await using (var dst = File.Create(tmp))
        {
            var buffer = new byte[1024 * 256];
            long readTotal = 0;
            int read;
            while ((read = await src.ReadAsync(buffer, ct)) > 0)
            {
                await dst.WriteAsync(buffer.AsMemory(0, read), ct);
                readTotal += read;
                if (total > 0) Progress?.Invoke((double)readTotal / total);
            }
        }

        if (File.Exists(item.FilePath)) File.Delete(item.FilePath);
        File.Move(tmp, item.FilePath);
        item.Refresh();
        Log?.Invoke($"モデル {item.Name} のダウンロード完了");
    }

    public void Delete(WhisperModelItem item)
    {
        if (File.Exists(item.FilePath))
        {
            File.Delete(item.FilePath);
            Log?.Invoke($"モデル {item.Name} を削除しました");
        }
        item.Refresh();
    }
}
