using System;
using System.IO;

namespace WhisperTranscribe.Services;

public static class AppPaths
{
    public static string Root { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WhisperTranscribe");

    public static string FfmpegDir { get; } = Path.Combine(Root, "ffmpeg");
    public static string ModelsDir { get; } = Path.Combine(Root, "models");
    public static string TempDir { get; } = Path.Combine(Root, "temp");

    public static string FfmpegExe => Path.Combine(FfmpegDir, "ffmpeg.exe");
    public static string FfprobeExe => Path.Combine(FfmpegDir, "ffprobe.exe");

    /// <summary>
    /// ユーザ向け成果物 (文字起こし結果, Colab 用 WAV 等) のルート。
    /// 既定: Desktop\Claude\
    /// 環境変数 WHISPER_USER_ROOT で上書き可能。
    /// </summary>
    public static string UserRoot { get; } =
        Environment.GetEnvironmentVariable("WHISPER_USER_ROOT") is { Length: > 0 } overrideRoot
            ? overrideRoot
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "Claude");

    public static string DefaultOutputDir { get; } = Path.Combine(UserRoot, "WhisperTranscribe");
    public static string ColabInboxDir { get; } = Path.Combine(UserRoot, "WhisperTranscribe_Colab_Inbox");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(FfmpegDir);
        Directory.CreateDirectory(ModelsDir);
        Directory.CreateDirectory(TempDir);
        Directory.CreateDirectory(UserRoot);
    }
}
