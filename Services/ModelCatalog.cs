using System.Collections.Generic;
using WhisperTranscribe.Models;

namespace WhisperTranscribe.Services;

public static class ModelCatalog
{
    private const string Base = "https://huggingface.co/ggerganov/whisper.cpp/resolve/main/";

    public static IReadOnlyList<WhisperModelItem> All { get; } = new[]
    {
        Make("tiny",           "ggml-tiny.bin",            77 * 1024L * 1024,   "最速・最軽量"),
        Make("tiny.en",        "ggml-tiny.en.bin",         77 * 1024L * 1024,   "tiny の英語専用版"),
        Make("base",           "ggml-base.bin",           148 * 1024L * 1024,   "軽量・高速"),
        Make("base.en",        "ggml-base.en.bin",        148 * 1024L * 1024,   "base の英語専用版"),
        Make("small",          "ggml-small.bin",          488 * 1024L * 1024,   "バランス型"),
        Make("small.en",       "ggml-small.en.bin",       488 * 1024L * 1024,   "small の英語専用版"),
        Make("medium",         "ggml-medium.bin",        1530L * 1024 * 1024,   "高精度"),
        Make("medium.en",      "ggml-medium.en.bin",     1530L * 1024 * 1024,   "medium の英語専用版"),
        Make("large-v1",       "ggml-large-v1.bin",      3094L * 1024 * 1024,   "Large v1 (旧)"),
        Make("large-v2",       "ggml-large-v2.bin",      3094L * 1024 * 1024,   "Large v2"),
        Make("large-v3",       "ggml-large-v3.bin",      3094L * 1024 * 1024,   "Large v3 (高精度)"),
        Make("large-v3-turbo", "ggml-large-v3-turbo.bin", 1620L * 1024 * 1024,  "Large v3 turbo (高速・高精度)"),
    };

    private static WhisperModelItem Make(string name, string file, long size, string desc) => new()
    {
        Name = name,
        FileName = file,
        DownloadUrl = Base + file,
        ApproxSizeBytes = size,
        Description = desc,
    };
}
