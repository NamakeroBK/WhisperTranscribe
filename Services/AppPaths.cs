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

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(FfmpegDir);
        Directory.CreateDirectory(ModelsDir);
        Directory.CreateDirectory(TempDir);
    }
}
