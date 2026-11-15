using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Media.SpeechSynthesis;
using Windows.Storage.Streams;

namespace Learning.Shell;

/// <summary>A voice the Reader can use: one of Windows' own (free, offline), or a Google voice through AI Manager (costs a little).</summary>
public sealed record ReaderVoice(string Key, string Name, bool Google)
{
    /// <summary>Windows' default voice, which every Windows computer has.</summary>
    public static ReaderVoice WindowsDefault => SpeechSynthesizer.DefaultVoice is { } v
        ? new("windows:" + v.Id, v.DisplayName + " (Windows, free)", false)
        : new("windows:", "no Windows voice installed", false);

    /// <summary>The Windows voices on this computer, English first.</summary>
    public static IReadOnlyList<ReaderVoice> WindowsVoices() =>
        [.. SpeechSynthesizer.AllVoices
            .OrderBy(v => v.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? 0 : 1).ThenBy(v => v.Language).ThenBy(v => v.DisplayName)
            .Select(v => new ReaderVoice("windows:" + v.Id, $"{v.DisplayName} ({v.Language}, Windows, free)", false))];

    public string Id => Key[(Key.IndexOf(':', StringComparison.Ordinal) + 1)..];
}

/// <summary>
/// The Reader (Learning Core plan 3.15; Ron, 2026-11-15: "a reader, like we have in Author+ … an audio option to read the text
/// to the user"). Reads a page block by block, saying which block it is on so the page can mark it; pause, go on, stop.
/// Windows voices work offline on any computer with no key; a Google voice is spoken through the shared AI Manager and each
/// block's sound is kept for the session, so hearing it again costs nothing.
/// </summary>
public sealed class Reader : IDisposable
{
    private readonly MediaPlayer _player = new();
    private CancellationTokenSource? _cts;
    private TaskCompletionSource? _ended;
    private static readonly Dictionary<(string Voice, double Rate, string Text), (byte[] Audio, string Type)> Heard = [];

    /// <summary>The block being read (its index), or null when reading stops.</summary>
    public event Action<int?>? Reading;

    public bool IsReading => _cts is not null;
    public bool IsPaused { get; private set; }

    public Reader()
    {
        _player.MediaEnded += (_, _) => _ended?.TrySetResult();
        _player.MediaFailed += (_, e) => _ended?.TrySetException(new InvalidOperationException(e.ErrorMessage));
    }

    /// <summary>
    /// Reads <paramref name="blocks"/> from <paramref name="from"/> to the end, unless stopped. Returns null when it read to
    /// the end or was stopped, or what went wrong, in words.
    /// </summary>
    public async Task<string?> ReadAsync(IReadOnlyList<string> blocks, int from, ReaderVoice voice, double rate)
    {
        Stop();
        var cts = new CancellationTokenSource();
        _cts = cts;
        IsPaused = false;
        try
        {
            var order = Enumerable.Range(Math.Max(0, from), Math.Max(0, blocks.Count - Math.Max(0, from))).Where(i => blocks[i].Trim().Length > 0).ToList();
            Task<(byte[] Audio, string Type)?>? next = order.Count > 0 ? Speak(blocks[order[0]], voice, rate, cts.Token) : null;
            for (var k = 0; k < order.Count && !cts.IsCancellationRequested; k++)
            {
                var sound = await next!;
                // The next block's sound is made while this one plays, so there is no gap.
                next = k + 1 < order.Count ? Speak(blocks[order[k + 1]], voice, rate, cts.Token) : null;
                if (sound is null || cts.IsCancellationRequested) break;
                Reading?.Invoke(order[k]);
                _ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var stream = new InMemoryRandomAccessStream();
                await stream.WriteAsync(sound.Value.Audio.AsBuffer());
                stream.Seek(0);
                _player.Source = MediaSource.CreateFromStream(stream, sound.Value.Type);
                if (!IsPaused) _player.Play();
                using (cts.Token.Register(() => _ended?.TrySetCanceled()))
                    await _ended.Task;
            }
            return null;
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch (Exception ex) when (ex is Eaglin.AiManager.AiException or InvalidOperationException or System.Net.Http.HttpRequestException or System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            return voice.Google
                ? "The Google voice could not read the page: " + ex.Message + " Choose a Windows voice in View ▸ Reader voice and speed…, which works without the internet."
                : "Windows could not read the page aloud (" + ex.Message.Trim() + "). Choose another voice in View ▸ Reader voice and speed…, " +
                  "or add a voice in Windows Settings ▸ Time & language ▸ Speech.";
        }
        finally
        {
            if (_cts == cts)
            {
                _cts = null;
                IsPaused = false;
                _player.Pause();
                Reading?.Invoke(null);
            }
            cts.Dispose();
        }
    }

    private static async Task<(byte[] Audio, string Type)?> Speak(string text, ReaderVoice voice, double rate, CancellationToken ct)
    {
        var key = (voice.Key, rate, text);
        if (Heard.TryGetValue(key, out var kept)) return kept;
        (byte[], string) made;
        if (voice.Google)
        {
            var speech = await PlcAi.Shared.SpeakAsync(text, voice.Id, rate, ct);
            made = (speech.Audio, speech.Format == Eaglin.AiManager.AiAudioFormat.Mp3 ? "audio/mpeg" : "audio/wav");
        }
        else
        {
            using var synth = new SpeechSynthesizer();
            synth.Voice = SpeechSynthesizer.AllVoices.FirstOrDefault(v => v.Id == voice.Id) ?? SpeechSynthesizer.DefaultVoice
                ?? throw new InvalidOperationException("No Windows voice is installed on this computer.");
            synth.Options.SpeakingRate = Math.Clamp(rate, 0.5, 3.0);
            using var stream = await synth.SynthesizeTextToStreamAsync(text);
            var bytes = new byte[stream.Size];
            await stream.ReadAsync(bytes.AsBuffer(), (uint)stream.Size, InputStreamOptions.None);
            made = (bytes, stream.ContentType);
        }
        ct.ThrowIfCancellationRequested();
        Heard[key] = made;
        return made;
    }

    public void Pause()
    {
        if (!IsReading || IsPaused) return;
        _player.Pause();
        IsPaused = true;
    }

    public void Resume()
    {
        if (!IsReading || !IsPaused) return;
        _player.Play();
        IsPaused = false;
    }

    public void Stop()
    {
        _cts?.Cancel();
        _player.Pause();
    }

    /// <summary>Forgets the sound kept for the session (another learner).</summary>
    public static void ForgetAll() => Heard.Clear();

    public void Dispose()
    {
        Stop();
        _player.Dispose();
    }
}
