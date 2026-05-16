# Whisper Transcribe

whisper.cpp (Whisper.net) を使った Windows 向け文字起こし GUI。
30 分単位の録音ファイルを複数まとめて投入 → 連結 → ラウドネス正規化 → Whisper による文字起こしを 1 アプリで完結します。

## 特徴

- **複数ファイル対応**: WAV/MP3/M4A/FLAC/OGG/MP4/MOV/MKV など、ffmpeg がデコードできるものは何でも入力可
- **32bit float WAV を自動検出**して扱える（ffprobe でフォーマットを表示）
- **連結 + ラウドネス正規化 (loudnorm)**: 録音機が 30 分ごとに区切ったファイルを順番にくっつけて 1 本化
- **Whisper に最適な形式へ変換**: 16kHz / mono / 16bit PCM WAV へリサンプル
- **モデル切替**: tiny / base / small / medium / large-v1〜v3 / large-v3-turbo を GUI からダウンロード・切替
- **出力形式**: `.txt` / `.srt` / `.vtt`
- **ドラッグ&ドロップ**対応
- **CPU / CUDA / Vulkan ランタイム同梱**（Whisper.net.AllRuntimes）

## 必要環境

- Windows 10/11 x64
- .NET 8 Desktop Runtime
- インターネット接続（初回の ffmpeg・モデル取得時のみ）

## ビルド方法

```powershell
git clone https://github.com/<your>/<repo>.git
cd <repo>
dotnet build -c Release
```

実行ファイル: `bin\Release\net8.0-windows\WhisperTranscribe.exe`

## 使い方

1. 初回起動時、ffmpeg を自動ダウンロードする確認が出るので「はい」。
2. 「モデル管理...」ボタンから使いたい Whisper モデルをダウンロード（日本語は `large-v3` または `large-v3-turbo` 推奨）。
3. メイン画面に音声/動画ファイルをドラッグ&ドロップ。順序は上から連結されるので、必要なら「上へ／下へ」で並び替え。
4. モデル・言語・出力フォルダ・出力形式を選択。
5. 「文字起こし開始」。

## 内部処理フロー

```
入力 (任意形式・複数) ─┐
                       ├─[ffmpeg]─> 16kHz/mono/pcm_s16le 中間 WAV
                       ┘                                     │
                                                             ▼
                                       [ffmpeg concat + loudnorm]
                                                             │
                                                             ▼
                                            最終 WAV (16kHz/mono/16bit)
                                                             │
                                                             ▼
                                                [Whisper.net で文字起こし]
                                                             │
                                                             ▼
                                                  .txt / .srt / .vtt
```

## ファイル保管場所

- ffmpeg / ffprobe: `%LOCALAPPDATA%\WhisperTranscribe\ffmpeg\`
- モデル: `%LOCALAPPDATA%\WhisperTranscribe\models\`
- 中間ファイル: `%LOCALAPPDATA%\WhisperTranscribe\temp\`

## ライセンス

- 本アプリ: MIT
- whisper.cpp / Whisper.net: MIT
- ffmpeg: LGPL/GPL（同梱せず、初回起動時に gyan.dev から取得）
