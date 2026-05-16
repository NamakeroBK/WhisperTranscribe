using System.Diagnostics;
using WhisperTranscribe.Services;

if (args.Length < 1 || args[0] is "-h" or "--help")
{
    Console.WriteLine("wtcli - WhisperTranscribe CLI");
    Console.WriteLine();
    Console.WriteLine("Usage:");
    Console.WriteLine("  wtcli <input> [--model NAME] [--lang LANG] [--prompt TEXT] [--hq] [--no-norm] [--out DIR]");
    Console.WriteLine();
    Console.WriteLine("Options:");
    Console.WriteLine("  --model NAME    tiny | base | small | medium | large-v3 | large-v3-turbo (default: small)");
    Console.WriteLine("  --lang LANG     ja | en | auto (default: ja)");
    Console.WriteLine("  --prompt TEXT   initial_prompt (例示的な書き起こしを書く)");
    Console.WriteLine("  --hq            高品質モード (beam=10)");
    Console.WriteLine("  --no-norm       ラウドネス正規化を無効化");
    Console.WriteLine("  --out DIR       出力フォルダ (default: 入力と同じ場所)");
    Console.WriteLine();
    Console.WriteLine("複数ファイル連結:");
    Console.WriteLine("  wtcli file1.wav file2.wav file3.wav --model small");
    return 1;
}

// 引数パース
var inputs = new List<string>();
string modelName = "small";
string lang = "ja";
string? prompt = null;
bool hq = false;
bool noNorm = false;
string? outDir = null;

for (int i = 0; i < args.Length; i++)
{
    var a = args[i];
    if (a == "--model" && i + 1 < args.Length) { modelName = args[++i]; }
    else if (a == "--lang" && i + 1 < args.Length) { lang = args[++i]; }
    else if (a == "--prompt" && i + 1 < args.Length) { prompt = args[++i]; }
    else if (a == "--hq") { hq = true; }
    else if (a == "--no-norm") { noNorm = true; }
    else if (a == "--out" && i + 1 < args.Length) { outDir = args[++i]; }
    else if (a.StartsWith("--")) { Console.Error.WriteLine($"Unknown option: {a}"); return 1; }
    else { inputs.Add(Path.GetFullPath(a)); }
}

foreach (var f in inputs)
    if (!File.Exists(f)) { Console.Error.WriteLine($"Input not found: {f}"); return 1; }

if (inputs.Count == 0) { Console.Error.WriteLine("No input file."); return 1; }

AppPaths.EnsureDirectories();

// ffmpeg
var ffmpeg = new FfmpegProvider();
ffmpeg.Log += s => Console.WriteLine("[ffmpeg-setup] " + s);
ffmpeg.Progress += p => Console.Write($"\r[ffmpeg-setup] downloading {p * 100,5:0.0}%");
if (!ffmpeg.IsInstalled)
{
    await ffmpeg.EnsureInstalledAsync();
    Console.WriteLine();
}
else Console.WriteLine("[ffmpeg-setup] already installed at " + AppPaths.FfmpegExe);

// model
var modelMgr = new ModelManager();
modelMgr.Log += s => Console.WriteLine("[model] " + s);
modelMgr.Progress += p => Console.Write($"\r[model] downloading {p * 100,5:0.0}%");
var models = modelMgr.RefreshAll();
var model = models.FirstOrDefault(m => m.Name == modelName);
if (model == null) { Console.Error.WriteLine($"Unknown model: {modelName}"); return 1; }
if (!model.IsDownloaded)
{
    await modelMgr.DownloadAsync(model);
    Console.WriteLine();
}
else Console.WriteLine($"[model] {model.Name} already downloaded");

// audio pipeline
var audio = new AudioProcessor();
audio.Log += s => Console.WriteLine("[audio] " + s);
var sw = Stopwatch.StartNew();
var wav = await audio.ProcessAsync(inputs, normalize: !noNorm);
sw.Stop();
Console.WriteLine($"[audio] elapsed: {sw.Elapsed:mm\\:ss\\.ff}");

// transcribe
var trans = new Transcriber();
trans.Log += s => Console.WriteLine("[whisper] " + s);
trans.Progress += p => Console.Write($"\r[whisper] {p,3}%");
sw.Restart();
var segs = await trans.TranscribeAsync(wav, model.FilePath, new Transcriber.Options
{
    Language = lang,
    HighQuality = hq,
    InitialPrompt = prompt,
});
sw.Stop();
Console.WriteLine();
Console.WriteLine($"[whisper] elapsed: {sw.Elapsed:mm\\:ss\\.ff}");

// outputs
outDir ??= Path.GetDirectoryName(inputs[0])!;
Directory.CreateDirectory(outDir);
var baseName = Path.GetFileNameWithoutExtension(inputs[0]) + "_whisper";
var baseOut = Path.Combine(outDir, baseName);
Transcriber.WriteText(baseOut + ".txt", segs);
Transcriber.WriteSrt(baseOut + ".srt", segs);
Transcriber.WriteVtt(baseOut + ".vtt", segs);
Console.WriteLine($"[out] {baseOut}.txt / .srt / .vtt");

Console.WriteLine();
Console.WriteLine("===== Transcript (head) =====");
foreach (var s in segs.Take(15))
    Console.WriteLine($"[{s.Start:hh\\:mm\\:ss\\.fff} -> {s.End:hh\\:mm\\:ss\\.fff}] {s.Text}");
if (segs.Count > 15) Console.WriteLine($"... ({segs.Count - 15} more)");

return 0;
