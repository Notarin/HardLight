using Content.Client.Stylesheets;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._HL.Changelog;

/// <summary>
/// Primarily the main-menu changelog button.
/// However, has some seldom uses elsewhere.
/// It listens to the <see cref="ChangelogManager"/> so it can magically turn red and show "New!" when there are unread changes.
/// </summary>
public sealed class ChangelogButton : Button
{
    [Dependency] private readonly ChangelogManager _changelogManager = null!;

    public ChangelogButton()
    {
        IoCManager.InjectDependencies(this);

        // So that measuring before opening returns a correct height,
        // and the window has the correct size when opened.
        Text = " ";
    }

    /// <summary>
    /// Called when the button is added to the UI.
    /// </summary>
    protected override void EnteredTree()
    {
        base.EnteredTree();

        _changelogManager.NewChangelogEntriesChanged += UpdateStuff;
        UpdateStuff();
    }

    /// <summary>
    /// Called when the button is removed from the UI.
    /// </summary>
    protected override void ExitedTree()
    {
        base.ExitedTree();

        _changelogManager.NewChangelogEntriesChanged -= UpdateStuff;
    }

    /// <summary>
    /// Turns the button red and changes the text to have "New Entries" if there are unread changelogs.
    /// </summary>
    private void UpdateStuff()
    {
        if (_changelogManager.NewChangelogEntries)
        {
            Text = Loc.GetString("changelog-button-new-entries");
            StyleClasses.Add(StyleClass.Negative);
        }
        else
        {
            Text = Loc.GetString("changelog-button");
            StyleClasses.Remove(StyleClass.Negative);
        }
    }
}
