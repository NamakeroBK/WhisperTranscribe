using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using WhisperTranscribe.Models;
using WhisperTranscribe.Services;

namespace WhisperTranscribe;

public partial class MainWindow : Window
{
    private static readonly string[] AudioVideoExtensions = new[]
    {
        ".wav", ".mp3", ".m4a", ".aac", ".flac", ".ogg", ".opus", ".wma",
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v", ".ts"
    };

    private static readonly (string Code, string Label)[] Languages = new[]
    {
        ("auto","自動検出"),
        ("ja","日本語"),
        ("en","英語"),
        ("zh","中国語"),
        ("ko","韓国語"),
        ("es","スペイン語"),
        ("fr","フランス語"),
        ("de","ドイツ語"),
        ("it","イタリア語"),
        ("pt","ポルトガル語"),
        ("ru","ロシア語"),
    };

    private readonly ObservableCollection<InputFileItem> _files = new();
    private readonly FfmpegProvider _ffmpeg = new();
    private readonly ModelManager _modelMgr = new();
    private readonly InputProber _prober = new();
    private CancellationTokenSource? _cts;

    public MainWindow()
    {
        InitializeComponent();
        FilesGrid.ItemsSource = _files;

        _ffmpeg.Log += Log;
        _modelMgr.Log += Log;

        OutDirBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "WhisperTranscribe");
        OutNameBox.Text = "transcript";

        LanguageCombo.ItemsSource = Languages.Select(l => $"{l.Label} ({l.Code})").ToArray();
        LanguageCombo.SelectedIndex = 1; // 日本語

        RefreshModelCombo();
        AppPaths.EnsureDirectories();

        Loaded += async (_, _) =>
        {
            if (!_ffmpeg.IsInstalled)
            {
                var r = MessageBox.Show(this,
                    "ffmpeg がまだインストールされていません。今すぐダウンロードしますか? (約100MB)",
                    "ffmpeg セットアップ",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (r == MessageBoxResult.Yes) await InstallFfmpegAsync();
            }
        };
    }

    private void RefreshModelCombo()
    {
        var list = _modelMgr.RefreshAll();
        ModelCombo.ItemsSource = null;
        ModelCombo.ItemsSource = list;
        var first = list.FirstOrDefault(m => m.IsDownloaded);
        if (first != null) ModelCombo.SelectedItem = first;
        else ModelCombo.SelectedIndex = 0;
    }

    // ----- ログ -----
    private void Log(string msg)
    {
        Dispatcher.Invoke(() =>
        {
            LogBox.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
            LogBox.ScrollToEnd();
        });
    }

    // ----- ファイル操作 -----
    private void Window_DragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
        await AddPathsAsync(paths);
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Multiselect = true,
            Filter = "音声・動画ファイル|*.wav;*.mp3;*.m4a;*.aac;*.flac;*.ogg;*.opus;*.wma;*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.ts|すべて|*.*"
        };
        if (dlg.ShowDialog(this) == true)
            await AddPathsAsync(dlg.FileNames);
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog(this) == true)
            await AddPathsAsync(new[] { dlg.FolderName });
    }

    private async Task AddPathsAsync(IEnumerable<string> paths)
    {
        var collected = new List<string>();
        foreach (var p in paths)
        {
            if (Directory.Exists(p))
            {
                foreach (var f in Directory.EnumerateFiles(p, "*", SearchOption.AllDirectories))
                    if (AudioVideoExtensions.Contains(Path.GetExtension(f).ToLowerInvariant()))
                        collected.Add(f);
            }
            else if (File.Exists(p) &&
                     AudioVideoExtensions.Contains(Path.GetExtension(p).ToLowerInvariant()))
            {
                collected.Add(p);
            }
        }
        collected.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (var f in collected)
        {
            if (_files.Any(x => string.Equals(x.FullPath, f, StringComparison.OrdinalIgnoreCase))) continue;
            var item = new InputFileItem { FullPath = f, Status = "解析中..." };
            _files.Add(item);
        }

        if (!_ffmpeg.IsInstalled)
        {
            Log("ffmpeg 未取得のためファイル解析をスキップしました。「ffmpeg取得」を実行してください。");
            return;
        }

        foreach (var item in _files.Where(x => x.Status == "解析中...").ToList())
        {
            try
            {
                var probed = await _prober.ProbeAsync(item.FullPath);
                item.Codec = probed.Codec;
                item.SampleFormat = probed.SampleFormat;
                item.SampleRate = probed.SampleRate;
                item.Channels = probed.Channels;
                item.DurationSec = probed.DurationSec;
                item.Status = probed.Status;
            }
            catch (Exception ex)
            {
                item.Status = "解析エラー";
                Log("解析失敗: " + ex.Message);
            }
        }
    }

    private void MoveUp_Click(object sender, RoutedEventArgs e)
    {
        var sel = FilesGrid.SelectedItems.Cast<InputFileItem>().OrderBy(_files.IndexOf).ToList();
        foreach (var s in sel)
        {
            var i = _files.IndexOf(s);
            if (i > 0) _files.Move(i, i - 1);
        }
    }

    private void MoveDown_Click(object sender, RoutedEventArgs e)
    {
        var sel = FilesGrid.SelectedItems.Cast<InputFileItem>().OrderByDescending(_files.IndexOf).ToList();
        foreach (var s in sel)
        {
            var i = _files.IndexOf(s);
            if (i >= 0 && i < _files.Count - 1) _files.Move(i, i + 1);
        }
    }

    private void Remove_Click(object sender, RoutedEventArgs e)
    {
        foreach (var s in FilesGrid.SelectedItems.Cast<InputFileItem>().ToList())
            _files.Remove(s);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => _files.Clear();

    private void BrowseOutDir_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog();
        if (dlg.ShowDialog(this) == true) OutDirBox.Text = dlg.FolderName;
    }

    // ----- ffmpeg / モデル管理 -----
    private async void InstallFfmpeg_Click(object sender, RoutedEventArgs e) => await InstallFfmpegAsync();

    private async Task InstallFfmpegAsync()
    {
        try
        {
            StartButton.IsEnabled = false;
            _ffmpeg.Progress += SetMainProgress;
            await _ffmpeg.EnsureInstalledAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ffmpeg セットアップ失敗:\n" + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _ffmpeg.Progress -= SetMainProgress;
            MainProgress.Value = 0;
            StartButton.IsEnabled = true;
        }
    }

    private void SetMainProgress(double p) => Dispatcher.Invoke(() => MainProgress.Value = p * 100);

    private void ManageModels_Click(object sender, RoutedEventArgs e)
    {
        var win = new ModelManagerWindow { Owner = this, OnLog = Log };
        win.ShowDialog();
        RefreshModelCombo();
    }

    // ----- 実行 -----
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_files.Count == 0)
        {
            MessageBox.Show(this, "入力ファイルを追加してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!_ffmpeg.IsInstalled)
        {
            MessageBox.Show(this, "先に ffmpeg を取得してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (ModelCombo.SelectedItem is not WhisperModelItem model || !model.IsDownloaded)
        {
            MessageBox.Show(this, "モデルをダウンロードして選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (!OutTxt.IsChecked!.Value && !OutSrt.IsChecked!.Value && !OutVtt.IsChecked!.Value)
        {
            MessageBox.Show(this, "出力形式を1つ以上選択してください。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }
        if (string.IsNullOrWhiteSpace(OutDirBox.Text)) OutDirBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "WhisperTranscribe");
        Directory.CreateDirectory(OutDirBox.Text);

        var lang = Languages[Math.Max(0, LanguageCombo.SelectedIndex)].Code;
        var outName = string.IsNullOrWhiteSpace(OutNameBox.Text) ? "transcript" : OutNameBox.Text.Trim();
        var normalize = NormalizeCheck.IsChecked == true;

        StartButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        MainProgress.Value = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var audio = new AudioProcessor();
            audio.Log += Log;

            var transcriber = new Transcriber();
            transcriber.Log += Log;
            transcriber.Progress += p => Dispatcher.Invoke(() => MainProgress.Value = p);

            // 1. 連結 + 正規化 + 16kHz mono 16bit
            var wav = await audio.ProcessAsync(_files.Select(f => f.FullPath).ToList(), normalize, _cts.Token);

            // 2. 文字起こし
            var promptText = InitialPromptBox.Text?.Trim();
            var opts = new Transcriber.Options
            {
                Language = lang,
                HighQuality = HighQualityCheck.IsChecked == true,
                InitialPrompt = string.IsNullOrWhiteSpace(promptText) ? null : promptText,
            };
            var segs = await transcriber.TranscribeAsync(wav, model.FilePath, opts, _cts.Token);

            // 3. 出力
            var baseOut = Path.Combine(OutDirBox.Text, outName);
            if (OutTxt.IsChecked!.Value)
            {
                Transcriber.WriteText(baseOut + ".txt", segs);
                Log($"書き出し: {baseOut}.txt");
            }
            if (OutSrt.IsChecked!.Value)
            {
                Transcriber.WriteSrt(baseOut + ".srt", segs);
                Log($"書き出し: {baseOut}.srt");
            }
            if (OutVtt.IsChecked!.Value)
            {
                Transcriber.WriteVtt(baseOut + ".vtt", segs);
                Log($"書き出し: {baseOut}.vtt");
            }
            Log("=== 完了 ===");
            MessageBox.Show(this, "文字起こしが完了しました。", "完了", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (OperationCanceledException)
        {
            Log("中止されました。");
        }
        catch (Exception ex)
        {
            Log("エラー: " + ex.Message);
            MessageBox.Show(this, ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            StartButton.IsEnabled = true;
            CancelButton.IsEnabled = false;
            _cts?.Dispose();
            _cts = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();
}
