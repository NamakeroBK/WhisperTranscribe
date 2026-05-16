using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WhisperTranscribe.Models;
using WhisperTranscribe.Services;

namespace WhisperTranscribe;

public partial class ModelManagerWindow : Window
{
    private readonly ModelManager _mgr = new();
    private CancellationTokenSource? _cts;

    public Action<string>? OnLog;

    public ModelManagerWindow()
    {
        InitializeComponent();
        _mgr.Log += msg => Dispatcher.Invoke(() => OnLog?.Invoke(msg));
        _mgr.Progress += p => Dispatcher.Invoke(() => DownloadProgress.Value = p * 100);
        Refresh();
    }

    private void Refresh()
    {
        var list = _mgr.RefreshAll();
        ModelsGrid.ItemsSource = null;
        ModelsGrid.ItemsSource = list;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModelsGrid.SelectedItem is not WhisperModelItem item) return;
        if (item.IsDownloaded)
        {
            MessageBox.Show(this, "既にダウンロード済みです。", "情報", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        DownloadButton.IsEnabled = false;
        DeleteButton.IsEnabled = false;
        _cts = new CancellationTokenSource();
        try
        {
            await _mgr.DownloadAsync(item, _cts.Token);
            Refresh();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "ダウンロード失敗:\n" + ex.Message, "エラー", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            DownloadButton.IsEnabled = true;
            DeleteButton.IsEnabled = true;
            DownloadProgress.Value = 0;
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (ModelsGrid.SelectedItem is not WhisperModelItem item) return;
        if (!item.IsDownloaded) return;
        if (MessageBox.Show(this, $"{item.Name} を削除しますか?", "確認",
            MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
        _mgr.Delete(item);
        Refresh();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }
}
