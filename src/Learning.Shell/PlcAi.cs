using System.IO;
using System.Net.Http;
using System.Windows;
using Eaglin.AiManager;
using Eaglin.AiManager.Wpf;
using Learning.Core.Content;

namespace Learning.Shell;

/// <summary>One answer from the AI, or the reason there is not one.</summary>
public sealed record AiReply(string Text, string Source, string Problem)
{
    public bool Ok => Problem.Length == 0;

    public static AiReply Failed(string problem) => new("", "", problem);
}

/// <summary>
/// The AI in PLC+ (Ron, 2026-11-15: "make the shell AI enabled (optional)"; Learning Core plan 3.8). Everything goes through
/// the shared AI Manager package: never a provider's own library, never a second key store. The key typed in AI settings is
/// the one every one of Dr. Ron Eaglin's programs uses, kept on this computer. Opened lazily; never throws on the way in.
/// PLC+ works fully without it: Ask the AI says what it needs when no key is set up.
/// What is sent: the learner's question and the item's lesson (the book's words). Never the learner's name, PIN, progress,
/// notes or programs (NFR-2: no learner data leaves except as files the learner saves).
/// </summary>
public sealed class PlcAi
{
    public const string AppName = "PLCPlus";

    public const string NeedsSettings =
        "Ask the AI is optional, and no AI key is set up on this computer yet. An AI key is a password-like code from an AI company — " +
        "Anthropic (console.anthropic.com), Google (aistudio.google.com), OpenAI and others: you sign up there, add a way to pay (a " +
        "question usually costs a cent or two), copy the key and paste it into AI ▸ AI settings…. Your instructor may give the class one. " +
        "It is kept on this computer only. Everything else in PLC+ works without it.";

    private AiHub? _hub;
    private string _openProblem = "";

    public static PlcAi Shared { get; } = new();

    public bool IsReady => Hub() is { } h && h.IsAvailable();

    /// <summary>What asking sends and costs, ready or not. Never empty.</summary>
    public string Status
    {
        get
        {
            if (Hub() is not { } hub) return _openProblem.Length > 0 ? "The AI could not be opened: " + _openProblem : NeedsSettings;
            if (!hub.IsAvailable()) return NeedsSettings;
            var p = hub.DefaultProvider;
            return $"Ask a question about this item and the AI ({AiHub.DisplayName(p)}, model {hub.DefaultModel(p)}) answers it from the lesson. Asking sends your " +
                   "question and this item's lesson — not your name, progress or notes — and charges the account whose key was entered " +
                   "(usually a cent or two). You choose whether to keep the answer in My notes.";
        }
    }

    public bool ShowSettings(Window owner)
    {
        if (Hub() is not { } hub)
        {
            MessageBox.Show(owner, "The AI could not be opened.\n\n" + _openProblem + "\n\nEverything else in PLC+ works without it.",
                "PLC+ — AI settings", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
        var saved = new AiSettingsWindow(hub) { Owner = owner }.ShowDialog() == true;
        hub.Reload();
        return saved;
    }

    public void ShowUsage(Window owner)
    {
        if (Hub() is not { } hub)
        {
            MessageBox.Show(owner, "The AI could not be opened, so there is nothing to show.\n\n" + _openProblem,
                "PLC+ — What the AI has cost", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        new UsageDashboardWindow(hub, AppName) { Owner = owner }.Show();
    }

    /// <summary>The tutor's instructions: answer from the lesson, for a beginner, briefly, and say so when it goes beyond the book.</summary>
    public static string TutorInstructions(string program) =>
        $"You are a patient tutor inside {program}, a self-paced course that teaches PLC programming (ladder logic, IEC 61131-3, " +
        "Allen-Bradley in the lab) with simulated machines. A learner is reading one item of the course and asks you a question about it. " +
        "Answer for a beginner, in plain words, in at most 250 words. Base the answer on the lesson you are given; if the question goes " +
        "beyond the lesson, answer briefly and say that it goes beyond this item. If the learner asks you to do an exercise for them, " +
        "explain the idea and the next step instead of giving the whole program. Write plain text, because the answer is kept as " +
        "a note: short paragraphs, or a list with \"- \" at the start of each line; no Markdown symbols (no asterisks, no # headings), " +
        "no tables. If you are not sure, say so.";

    /// <summary>The question with the item it is about.</summary>
    public static string Prompt(LearningItem item, string lesson, string question) =>
        $"The item: {item.Number} {item.Title} (module {item.Module.Number}, {item.Module.Title}).\n\n" +
        $"The lesson the learner is reading:\n<lesson>\n{lesson.Trim()}\n</lesson>\n\nThe learner's question:\n{question.Trim()}";

    /// <summary>Asks one question. Anything that goes wrong comes back as <see cref="AiReply.Problem"/>, in words.</summary>
    public async Task<AiReply> AskAsync(string system, string prompt, CancellationToken ct = default)
    {
        if (Hub() is not { } hub) return AiReply.Failed(_openProblem.Length > 0 ? _openProblem : NeedsSettings);
        if (!hub.IsAvailable()) return AiReply.Failed(NeedsSettings);
        try
        {
            var r = await hub.CompleteAsync(new AiRequest { System = system, User = prompt, MaxTokens = 1200, Purpose = "Ask the AI about an item" }, ct).ConfigureAwait(true);
            return new AiReply(Plain(r.Text), $"{AiHub.DisplayName(r.Provider)}, model {r.Model}", "");
        }
        catch (AiException ex) when (ex.Kind == AiErrorKind.Refused)
        {
            return AiReply.Failed("The AI declined this question. Try asking it another way. A refusal costs nothing.");
        }
        catch (AiException ex)
        {
            return AiReply.Failed(ex.Message);
        }
        catch (HttpRequestException ex)
        {
            return AiReply.Failed("PLC+ could not reach the AI. Check that this computer is online and try again. Details for support: " + ex.Message);
        }
        catch (OperationCanceledException)
        {
            return AiReply.Failed(ct.IsCancellationRequested ? "Stopped: nothing was kept." : "The AI took too long and the question was stopped. Try again.");
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return AiReply.Failed("Something went wrong asking the AI. Try again. Details for support: " + ex.Message);
        }
    }

    /// <summary>The answer as plain text for a note: Markdown's bold and heading marks taken out, if the AI used them anyway.</summary>
    public static string Plain(string text)
    {
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n')
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^\s{0,3}#{1,6}\s+", ""))
            .Select(l => System.Text.RegularExpressions.Regex.Replace(l, @"^(\s*)\* ", "$1- "))
            .Select(l => l.Replace("**", "", StringComparison.Ordinal).Replace("__", "", StringComparison.Ordinal));
        return string.Join(Environment.NewLine, lines).Trim();
    }

    private AiHub? Hub()
    {
        if (_hub is not null) return _hub;
        try
        {
            _hub = AiHub.Open(AppName);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            _openProblem = ex.Message;
        }
        return _hub;
    }
}
