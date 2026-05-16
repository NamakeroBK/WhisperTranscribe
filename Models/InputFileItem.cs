using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;

namespace WhisperTranscribe.Models;

public class InputFileItem : INotifyPropertyChanged
{
    public string FullPath { get; init; } = "";
    public string FileName => Path.GetFileName(FullPath);

    private string _codec = "";
    public string Codec { get => _codec; set { _codec = value; OnPropertyChanged(); } }

    private string _sampleFmt = "";
    public string SampleFormat { get => _sampleFmt; set { _sampleFmt = value; OnPropertyChanged(); } }

    private int _sampleRate;
    public int SampleRate { get => _sampleRate; set { _sampleRate = value; OnPropertyChanged(); } }

    private int _channels;
    public int Channels { get => _channels; set { _channels = value; OnPropertyChanged(); } }

    private double _durationSec;
    public double DurationSec { get => _durationSec; set { _durationSec = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationText)); } }

    public string DurationText
    {
        get
        {
            var t = System.TimeSpan.FromSeconds(DurationSec);
            return $"{(int)t.TotalHours:D2}:{t.Minutes:D2}:{t.Seconds:D2}";
        }
    }

    private string _status = "";
    public string Status { get => _status; set { _status = value; OnPropertyChanged(); } }

    public string FormatSummary =>
        string.IsNullOrEmpty(Codec) ? "" : $"{Codec} / {SampleFormat} / {SampleRate}Hz / {Channels}ch";

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
