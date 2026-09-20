using System.Linq;
using Content.Client._HL.Changelog;

namespace Content.IntegrationTests.Tests;

[TestFixture]
public sealed class ChangelogUITest
{
    /// <summary>
    /// Verifies that providing valid changelog data to the ChangelogTab successfully generates UI elements.
    /// Spin up a client/server pair, grab the ChangelogManager.
    /// Load the changelog data.
    /// `WaitAssertion` to create a fresh ChangelogTab on the game thread.
    /// Manually calculate and attach the "unread" state, this isn't a normal client.
    /// Populate the tab, then traverse the UI to ensure entries were added.
    /// </summary>
    /// <remarks>
    /// WHY THIS USES LINQ FOR UI TRAVERSAL:
    /// The first impl relied on `tab.GetChild(0).GetChild(0).GetChild(0)`. YEUCKK!!!
    /// Also, if anyone wraps the UI in an extra Container later (probably gonna be my ass tbh),
    /// the test would eat shit, `IndexOutOfRangeException`.
    /// Using `SelectMany`, we flatten the nested UI tree without pain in the ass loops. FP FTW.
    /// </remarks>
    [Test]
    public async Task ChangelogTab_WhenGivenValidData_ShouldRenderUiEntries()
    {
        // Steroids: We do not need a network connection or a generated map to test a UI tab.
        // Fuck all that noise, we need no game-loop or ticker smh.
        await using var pair = await PoolManager.GetServerClient(new PoolSettings
        {
            Connected = false
        });

        var client = pair.Client;
        var changelogManager = client.ResolveDependency<ChangelogManager>();

        // Yoink the primary changelog, with OrDefault to cuck exceptions.
        var changelogs = await changelogManager.LoadGameChangelogs();
        var mainChangelog = changelogs.FirstOrDefault();

        Assert.That(mainChangelog, Is.Not.Null,
            "No changelogs were loaded! The headless test environment likely didn't mount the changelog VFS.");

        await client.WaitAssertion(() =>
        {
            var tab = new ChangelogTab();

            // Technically, the manager expects that we're loading the *actual* cached "read" changelog,
            // but this is a test! Silly changelog, I'm not even real!
            // To skirt this, we just lie through our teeth and pass the same damn changelog as the "read" changelog.
            mainChangelog!.UnreadEntries = changelogManager.GetUnreadEntries(mainChangelog.Name);

            tab.PopulateChangelog(mainChangelog);

            // Yyyyyoiink the grandchild.
            var entryContainer = tab.Children
                .SelectMany(child => child.Children)
                .SelectMany(grandchild => grandchild.Children)
                .FirstOrDefault();

            using (Assert.EnterMultipleScope())
            {
                Assert.That(entryContainer, Is.Not.Null,
                    "Could not find the nested entry container. Did the ChangelogTab UI layout change?");

                // LSP is really upset about this line, but trust me bro.
                // This is inside an NUnit multiple scope, and NUnit does fucky shit with the control flow.
                // The above Assert asserts that entryContainer is *not* null, and on failing, that the control flow escapes.
                // However, NUnit changes that... I think. It will make the function just keep trucking along, causing
                // the guarantee that the LSP **thinks** is valid to be entirely invalid.
                // ReSharper disable once ConstantNullCoalescingCondition
                // ReSharper disable once ConstantConditionalAccessQualifier
                Assert.That(entryContainer?.Children.Any() ?? false, Is.True,
                    "The changelog container was found, but it failed to generate any visible entry elements!");
            }
        });

        // LSP can mald, this has to be here.
        // Signals to the test runtime that this test is finished,
        // without it assumes something is funky and will skip our shit :(
        // ReSharper disable once DisposeOnUsingVariable
        await pair.CleanReturnAsync();
    }
}