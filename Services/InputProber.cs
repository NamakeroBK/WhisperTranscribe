using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WhisperTranscribe.Models;

namespace WhisperTranscribe.Services;

public class InputProber
{
    public async Task<InputFileItem> ProbeAsync(string path, CancellationToken ct = default)
    {
        var item = new InputFileItem { FullPath = path };
        if (!File.Exists(AppPaths.FfprobeExe))
        {
            item.Status = "ffprobe 未取得";
            return item;
        }

        var psi = new ProcessStartInfo
        {
            FileName = AppPaths.FfprobeExe,
            Arguments = $"-v error -print_format json -show_format -show_streams -select_streams a:0 \"{path}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        using var proc = Process.Start(psi)!;
        var stdout = await proc.StandardOutput.ReadToEndAsync(ct);
        await proc.WaitForExitAsync(ct);

        if (proc.ExitCode != 0 || string.IsNullOrWhiteSpace(stdout))
        {
            item.Status = "解析失敗";
            return item;
        }

        try
        {
            using var doc = JsonDocument.Parse(stdout);
            if (doc.RootElement.TryGetProperty("streams", out var streams) && streams.GetArrayLength() > 0)
            {
                var s = streams[0];
                if (s.TryGetProperty("codec_name", out var v)) item.Codec = v.GetString() ?? "";
                if (s.TryGetProperty("sample_fmt", out v)) item.SampleFormat = v.GetString() ?? "";
                if (s.TryGetProperty("sample_rate", out v) && int.TryParse(v.GetString(), out var sr)) item.SampleRate = sr;
                if (s.TryGetProperty("channels", out v)) item.Channels = v.GetInt32();
            }
            if (doc.RootElement.TryGetProperty("format", out var fmt) &&
                fmt.TryGetProperty("duration", out var dv) &&
                double.TryParse(dv.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var dur))
            {
                item.DurationSec = dur;
            }
            item.Status = item.SampleFormat.Contains("flt") ? "32bit float 検出" : "OK";
        }
        catch (Exception ex)
        {
            item.Status = "解析エラー: " + ex.Message;
        }

        return item;
    }
}
