using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace WhisperTranscribe.Services;

/// <summary>
/// 半手動 Colab 連携。
/// 1) 音声を 16kHz mono WAV へ前処理
/// 2) Colab inbox 用フォルダにコピー
/// 3) フォルダを Explorer で開く
/// 4) Colab Notebook URL をブラウザで開く
/// </summary>
public class ColabHelper
{
    public const string NotebookUrl =
        "https://colab.research.google.com/github/NamakeroBK/WhisperTranscribe/blob/main/colab/whisper_transcribe.ipynb";

    public static string InboxDir { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
        "WhisperTranscribe_Colab_Inbox");

    public event Action<string>? Log;

    /// <summary>
    /// 入力ファイル群を連結・正規化・16kHz/mono/16bit WAV にして、Colab inbox にコピーする。
    /// </summary>
    public async Task<string> PrepareForColabAsync(
        System.Collections.Generic.IReadOnlyList<string> inputFiles,
        bool normalize,
        string outputName,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(InboxDir);

        var audio = new AudioProcessor();
        audio.Log += s => Log?.Invoke(s);
        var wav = await audio.ProcessAsync(inputFiles, normalize, ct);

        var safeName = string.IsNullOrWhiteSpace(outputName) ? "audio" : SanitizeFileName(outputName);
        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var dest = Path.Combine(InboxDir, $"{safeName}_{stamp}.wav");
        File.Copy(wav, dest, overwrite: true);
        Log?.Invoke($"Colab 用 WAV 出力: {dest}");
        return dest;
    }

    public void OpenInbox()
    {
        Directory.CreateDirectory(InboxDir);
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{InboxDir}\"") { UseShellExecute = true });
    }

    public void OpenNotebook()
    {
        Process.Start(new ProcessStartInfo(NotebookUrl) { UseShellExecute = true });
    }

    private static string SanitizeFileName(string s)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
        return s;
    }
}
