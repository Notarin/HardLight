saveban-examine-verb-text = Save Restrictions

saveban-hover-body-banned =
    {"[color=red]"}This item is banned from being saved!{"[/color]"}
    {"    [color=yellow]"}Reason{":"} { $reason }{"[/color]"}
saveban-hover-body-restricted =
    {"[color=orange]"}This item has restrictions applied when saved!{"[/color]"}
    {"    [color=yellow]"}Reason{":"} { $reason }{"[/color]"}
saveban-hover-body-contains-restricted =
    {"[color=orange]"}This item contains {$count ->
        [one]a save-restricted item!
        *[other]{ $count } save-restricted items!
    }{"[/color]"}
saveban-hover-item-listing-restricted = { $variant ->
        *[banned]{"[color=red]"}[bold]{ $name }[/bold] is banned from being saved.{"[/color]"}
                {"    [color=yellow]"}Reason{":"} { $reason }{"[/color]"}
        [restricted]{"[color=orange]"}[bold]{ $name }[/bold] is restricted from being saved.{"[/color]"}
                {"    [color=yellow]"}Reason{":"} { $reason }{"[/color]"}
    }

saveban-stash-rejected = The item was rejected! Check for save bans.
