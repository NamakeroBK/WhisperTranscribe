using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using WhisperTranscribe.Services;

namespace WhisperTranscribe.Models;

public class WhisperModelItem : INotifyPropertyChanged
{
    public string Name { get; init; } = "";
    public string FileName { get; init; } = "";
    public string DownloadUrl { get; init; } = "";
    public string Description { get; init; } = "";
    public long ApproxSizeBytes { get; init; }

    public string FilePath => Path.Combine(AppPaths.ModelsDir, FileName);

    private bool _isDownloaded;
    public bool IsDownloaded
    {
        get => _isDownloaded;
        set { _isDownloaded = value; OnPropertyChanged(); OnPropertyChanged(nameof(StatusText)); OnPropertyChanged(nameof(DisplayName)); }
    }

    public string StatusText => IsDownloaded ? "ダウンロード済み" : "未取得";

    public string SizeText
    {
        get
        {
            double mb = ApproxSizeBytes / 1024.0 / 1024.0;
            return mb >= 1024 ? $"{mb / 1024:0.00} GB" : $"{mb:0} MB";
        }
    }

    public string DisplayName => IsDownloaded ? $"{Name}  (約 {SizeText})" : $"{Name}  (約 {SizeText} / 未取得)";

    public void Refresh()
    {
        IsDownloaded = File.Exists(FilePath) && new FileInfo(FilePath).Length > 1024 * 1024;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
