using System.Text.Json;
using Learning.Core;
using Learning.Core.Content;
using Learning.Core.Learners;
using Learning.Core.Progress;
using Xunit;

namespace Learning.Core.Tests;

/// <summary>Plan 6.1–6.10: learners' files, PINs, recovery, merging, backups and the event log.</summary>
public sealed class LearnerTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 5, 9, 0, 0, TimeSpan.Zero);
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "learner-tests-" + Guid.NewGuid().ToString("N"));
    // Few PBKDF2 rounds, so the tests are quick; the program uses LearnerFile.DefaultIterations.
    private static readonly LearnerFileOptions A = new("TestApp", ".testprogress", "1.0", "PC-A", Iterations: 1000);
    private static readonly LearnerFileOptions B = A with { Computer = "PC-B" };

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, recursive: true);
    }

    private LearnerStore Store(string sub = "a", LearnerFileOptions? o = null) => new(Path.Combine(_dir, sub), o ?? A);

    private static ChallengeOutcome Outcome(bool passed, string hash = "h") =>
        new("ch", true, [], [], passed ? 10 : 4, 10, passed, hash);

    [Fact]
    public void A_new_learner_is_listed_by_name_without_unlocking_and_the_body_is_not_readable()
    {
        var store = Store();
        var (file, code) = store.Create("Ana Ruiz", "N123", "2468", T0);
        file.Dispose();
        Assert.Matches("^[0-9A-Z]{4}(-[0-9A-Z]{4}){4}$", code);

        var listed = Assert.Single(store.List());
        Assert.Equal("Ana Ruiz", listed.Name);
        Assert.Equal("PC-A", listed.Header!.SavedOn);
        var text = File.ReadAllText(listed.Path);
        Assert.DoesNotContain("N123", text);       // the student ID is only inside the encrypted part
        Assert.DoesNotContain("2468", text);       // nor the PIN, anywhere
        Assert.EndsWith(".testprogress", listed.Path);
    }

    [Fact]
    public void The_right_PIN_opens_it_and_progress_survives_a_save()
    {
        var store = Store();
        var (file, _) = store.Create("Ana", null, "2468", T0);
        file.Data.Progress.Log(EventKinds.Read, "item.1", "Read it.", T0);
        file.Data.Work["ch"] = new SavedWork { Content = "{program}", Saved = T0 };
        file.Save(T0.AddMinutes(1));
        file.Dispose();

        var result = store.Unlock(store.List()[0], "2468", T0.AddMinutes(2));
        Assert.Equal(UnlockStatus.Unlocked, result.Status);
        using var again = result.File!;
        Assert.Equal("Ana", again.Data.Profile.DisplayName);
        Assert.Equal("{program}", again.Data.Work["ch"].Content);
        Assert.Contains(again.Data.Progress.Events, e => e.Kind == EventKinds.Read);
        Assert.Contains(again.Data.Progress.Events, e => e.Kind == EventKinds.Created);
    }

    [Fact]
    public void Five_wrong_PINs_make_the_learner_wait_and_the_wait_grows()
    {
        var store = Store();
        store.Create("Ana", null, "2468", T0).File.Dispose();
        var me = store.List()[0];
        for (var i = 1; i <= 4; i++)
            Assert.Equal(UnlockStatus.WrongPin, store.Unlock(me, "1111", T0).Status);
        var fifth = store.Unlock(me, "1111", T0);
        Assert.Equal(UnlockStatus.MustWait, fifth.Status);
        Assert.Equal(T0.AddSeconds(30), fifth.WaitUntil);

        // Even the right PIN waits, so guessing is slow.
        Assert.Equal(UnlockStatus.MustWait, store.Unlock(me, "2468", T0.AddSeconds(10)).Status);
        // After the wait, another wrong one doubles it.
        var sixth = store.Unlock(me, "1111", T0.AddSeconds(31));
        Assert.Equal(T0.AddSeconds(31 + 60), sixth.WaitUntil);
        // The right PIN after the wait opens it and clears the count.
        var ok = store.Unlock(me, "2468", T0.AddSeconds(100));
        Assert.Equal(UnlockStatus.Unlocked, ok.Status);
        ok.File!.Dispose();
        Assert.Equal(0, LearnerFile.ReadHeader(me.Path).FailedPins);
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 30)]
    [InlineData(6, 60)]
    [InlineData(8, 240)]
    [InlineData(30, 1800)]
    public void The_wait_after_wrong_PINs(int failed, int seconds) =>
        Assert.Equal(TimeSpan.FromSeconds(seconds), PinLockout.WaitAfter(failed));

    [Fact]
    public void The_recovery_code_sets_a_new_PIN_and_a_wrong_code_does_not()
    {
        var store = Store();
        var (file, code) = store.Create("Ana", null, "2468", T0);
        file.Dispose();
        var path = store.List()[0].Path;

        Assert.Equal(UnlockStatus.WrongPin, LearnerFile.Recover(path, "0000-0000-0000-0000-0000", "1357", A, T0).Status);
        Assert.Equal(UnlockStatus.WrongPin, LearnerFile.Recover(path, "too short", "1357", A, T0).Status);

        // Typed in lower case, with spaces, and O for 0: still read right.
        var typed = code.ToLowerInvariant().Replace("-", " ").Replace('0', 'o');
        var result = LearnerFile.Recover(path, typed, "1357", A, T0);
        Assert.Equal(UnlockStatus.Unlocked, result.Status);
        result.File!.Dispose();

        Assert.Equal(UnlockStatus.WrongPin, LearnerFile.Unlock(path, "2468", A, T0).Status);
        using var reopened = LearnerFile.Unlock(path, "1357", A, T0).File!;
        Assert.Contains(reopened.Data.Progress.Events, e => e.Kind == EventKinds.PinReset);
        // The recovery code still works after a reset.
        LearnerFile.Recover(path, code, "9999", A, T0).File!.Dispose();
    }

    [Fact]
    public void Changing_the_PIN()
    {
        var store = Store();
        using var file = store.Create("Ana", null, "2468", T0).File;
        Assert.True(file.IsPin("2468"));
        Assert.False(file.IsPin("1111"));
        file.ChangePin("135790", T0);
        Assert.True(file.IsPin("135790"));
        Assert.Equal(UnlockStatus.WrongPin, LearnerFile.Unlock(file.Path, "2468", A, T0).Status);
        LearnerFile.Unlock(file.Path, "135790", A, T0).File!.Dispose();
    }

    [Theory]
    [InlineData("", "", "Type a PIN")]
    [InlineData("12a4", "12a4", "digits only")]
    [InlineData("123", "123", "4 to 6 digits")]
    [InlineData("1234567", "1234567", "4 to 6 digits")]
    [InlineData("1234", "1235", "not the same")]
    [InlineData("123456", "123456", null)]
    public void PIN_rules(string pin, string again, string? problem)
    {
        var said = PinRules.Problem(pin, again);
        if (problem is null) Assert.Null(said);
        else Assert.Contains(problem, said);
    }

    [Fact]
    public void Names_are_unique_on_a_computer()
    {
        var store = Store();
        store.Create("Ana", null, "2468", T0).File.Dispose();
        Assert.Contains("already a learner called", store.NameProblem(" ana "));
        Assert.Null(store.NameProblem("Ana R."));
        Assert.Throws<ArgumentException>(() => store.Create("ANA", null, "2468", T0));
    }

    [Fact]
    public void A_tampered_file_does_not_open_and_says_so()
    {
        var store = Store();
        store.Create("Ana", null, "2468", T0).File.Dispose();
        var path = store.List()[0].Path;
        var header = LearnerFile.ReadHeader(path);
        var body = Convert.FromBase64String(header.Body);
        body[^1] ^= 1;
        var text = File.ReadAllText(path).Replace(header.Body, Convert.ToBase64String(body));
        File.WriteAllText(path, text);
        var ex = Assert.Throws<LearnerFileException>(() => LearnerFile.Unlock(path, "2468", A, T0));
        Assert.Contains("damaged", ex.Message);
    }

    [Fact]
    public void Something_that_is_not_a_learner_file_is_listed_with_its_problem()
    {
        var store = Store();
        Directory.CreateDirectory(store.Folder);
        File.WriteAllText(Path.Combine(store.Folder, "junk.testprogress"), "{ not json");
        var listed = Assert.Single(store.List());
        Assert.Null(listed.Header);
        Assert.Contains("not a learner's progress file", listed.Problem);
    }

    [Fact]
    public void Importing_a_learner_new_to_this_computer_copies_the_file_and_the_same_PIN_opens_it()
    {
        var home = Store("home", B);
        using (var f = home.Create("Ana", null, "2468", T0).File) f.Save(T0.AddHours(1));
        var exported = Path.Combine(_dir, "usb", "Ana.testprogress");
        Directory.CreateDirectory(Path.GetDirectoryName(exported)!);
        File.Copy(home.List()[0].Path, exported);

        var lab = Store("lab");
        var plan = lab.PlanImport(exported);
        Assert.Null(plan.Existing);
        Assert.Equal("PC-B", plan.Incoming.SavedOn);   // shown so the learner knows which copy it is
        var added = lab.ImportNew(plan);
        lab.Unlock(added, "2468", T0).File!.Dispose();
    }

    [Fact]
    public void Two_copies_that_went_their_own_ways_merge_and_nothing_is_lost()
    {
        var lab = Store("lab");
        var (file, _) = lab.Create("Ana", null, "2468", T0);
        var item = TestPackage.Item("m1.a");
        file.Data.Progress.Record(item, Outcome(false), T0.AddMinutes(1));
        file.Save(T0.AddMinutes(1));
        file.Dispose();

        // The learner takes a copy home and works there.
        var home = Store("home", B);
        home.ImportNew(home.PlanImport(lab.List()[0].Path));
        using (var h = home.Unlock(home.List()[0], "2468", T0.AddHours(2)).File!)
        {
            h.Data.Progress.Record(item, Outcome(true, "home"), T0.AddHours(2));
            h.Data.Work["ch"] = new SavedWork { Content = "home program", Saved = T0.AddHours(2) };
            h.Save(T0.AddHours(2));
        }
        // Meanwhile, more work in the lab.
        using (var l = lab.Unlock(lab.List()[0], "2468", T0.AddHours(1)).File!)
        {
            l.Data.Progress.Record(TestPackage.Item("m1.b"), Outcome(false, "lab"), T0.AddHours(1));
            l.Data.Work["ch"] = new SavedWork { Content = "lab program", Saved = T0.AddHours(1) };
            l.Save(T0.AddHours(1));
        }

        // The home copy comes back to the lab.
        var plan = lab.PlanImport(home.List()[0].Path);
        Assert.NotNull(plan.Existing);
        var merged = lab.ImportMerge(plan, "2468", T0.AddHours(3));
        Assert.Equal(UnlockStatus.Unlocked, merged.Status);
        using var m = merged.File!;
        Assert.Equal(3, m.Data.Progress.Attempts.Count);           // one from before, one from each place, none doubled
        Assert.Equal(ItemState.Complete, m.Data.Progress.StateOf(item));
        Assert.Equal("home program", m.Data.Work["ch"].Content);   // the later of the two
        Assert.Contains(m.Data.Progress.Events, e => e.Kind == EventKinds.Merged && e.Detail.Contains("PC-B"));

        // Merging the same copy again changes nothing.
        m.Dispose();
        using var twice = lab.ImportMerge(lab.PlanImport(home.List()[0].Path), "2468", T0.AddHours(4)).File!;
        Assert.Equal(3, twice.Data.Progress.Attempts.Count);
    }

    [Fact]
    public void Merging_when_the_PIN_was_changed_on_the_other_computer_keeps_this_computers_PIN()
    {
        var lab = Store("lab");
        lab.Create("Ana", null, "2468", T0).File.Dispose();
        var home = Store("home", B);
        home.ImportNew(home.PlanImport(lab.List()[0].Path));
        using (var h = home.Unlock(home.List()[0], "2468", T0).File!) h.ChangePin("1357", T0.AddHours(1));

        var plan = lab.PlanImport(home.List()[0].Path);
        Assert.Equal(UnlockStatus.WrongPin, lab.ImportMerge(plan, "0000", T0.AddHours(2)).Status);
        var merged = lab.ImportMerge(plan, "1357", T0.AddHours(2));   // the new PIN, from the other copy
        Assert.Equal(UnlockStatus.Unlocked, merged.Status);
        merged.File!.Dispose();
        LearnerFile.Unlock(lab.List()[0].Path, "2468", A, T0.AddHours(3)).File!.Dispose();
        Assert.Equal(UnlockStatus.WrongPin, LearnerFile.Unlock(plan.Source, "0000", B, T0).Status); // the USB copy is untouched by tries
    }

    [Fact]
    public void Two_sessions_with_the_same_learner_merge_when_they_save()
    {
        var store = Store();
        store.Create("Ana", null, "2468", T0).File.Dispose();
        var me = store.List()[0];
        using var one = store.Unlock(me, "2468", T0).File!;
        using var two = store.Unlock(me, "2468", T0).File!;
        one.Data.Progress.Record(TestPackage.Item("m1.a"), Outcome(true), T0.AddMinutes(1));
        one.Save(T0.AddMinutes(1));
        two.Data.Progress.Record(TestPackage.Item("m1.b"), Outcome(true), T0.AddMinutes(2));
        two.Save(T0.AddMinutes(2));

        using var after = store.Unlock(me, "2468", T0.AddMinutes(3)).File!;
        Assert.Equal(2, after.Data.Progress.Attempts.Count);
    }

    [Fact]
    public void A_backup_is_out_of_date_once_the_learner_does_more()
    {
        var store = Store();
        using var file = store.Create("Ana", null, "2468", T0).File;
        Assert.False(file.Data.NeedsBackup);                 // nothing done yet
        file.Data.Progress.Record(TestPackage.Item("m1.a"), Outcome(true), T0.AddMinutes(1));
        file.Save(T0.AddMinutes(1));
        Assert.True(file.Data.NeedsBackup);
        Assert.True(LearnerFile.ReadHeader(file.Path).NeedsBackup);   // the start screen can say so without the PIN

        var usb = Path.Combine(_dir, "usb", "Ana.testprogress");
        file.MarkBackedUp(Path.GetDirectoryName(usb)!, T0.AddMinutes(2));
        file.CopyTo(usb);
        Assert.False(file.Data.NeedsBackup);
        Assert.False(LearnerFile.ReadHeader(usb).NeedsBackup);
        Assert.Equal(T0.AddMinutes(2), LearnerFile.ReadHeader(file.Path).LastBackup);
        Assert.Contains(file.Data.Progress.Events, e => e.Kind == EventKinds.BackedUp);
        // The backup opens with the same PIN.
        LearnerFile.Unlock(usb, "2468", A, T0).File!.Dispose();
    }

    [Fact]
    public void A_locked_file_cannot_be_saved()
    {
        var store = Store();
        var file = store.Create("Ana", null, "2468", T0).File;
        Assert.True(file.IsOpen);
        file.Dispose();
        Assert.False(file.IsOpen);
        Assert.Throws<InvalidOperationException>(() => file.Save(T0));
    }

    [Fact]
    public void Progress_saved_before_attempts_had_IDs_still_merges_without_doubling()
    {
        var json = """{ "attempts": [ { "itemId": "m1.a", "challengeId": "ch", "at": "2026-11-04T10:00:00+00:00", "score": 1, "maxScore": 1, "passed": true, "workHash": "x" } ] }""";
        var a = LearningProgress.FromJson(json);
        var b = LearningProgress.FromJson(json);
        Assert.Single(LearningProgress.Merge(a, b).Attempts);
        Assert.DoesNotContain("\"key\"", a.ToJson());
    }

    [Fact]
    public void The_file_has_a_readable_header_and_one_encrypted_block()
    {
        var store = Store();
        store.Create("Ana", null, "2468", T0).File.Dispose();
        using var doc = JsonDocument.Parse(File.ReadAllText(store.List()[0].Path));
        var names = doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        Assert.Equal("format", names[0]);
        Assert.Equal("body", names[^1]);
        Assert.Contains("displayName", names);
        Assert.Contains("appVersion", names);
        Assert.Equal("PBKDF2-SHA256", doc.RootElement.GetProperty("keys").GetProperty("kdf").GetString());
    }
}

/// <summary>Items to record progress against, without a content package on disk.</summary>
internal static class TestPackage
{
    private static readonly LearningModule Module = new() { Id = "m1", Number = 1, Title = "Test", Status = Learning.Core.Content.ModuleStatus.Ready };

    public static LearningItem Item(string id) => new() { Id = id, Number = "1." + id[^1], Title = id, Module = Module, Exercise = "ch" };
}
