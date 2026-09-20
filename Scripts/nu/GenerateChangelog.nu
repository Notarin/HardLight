#!/usr/bin/env nu

# Incredibly shitty script I sneezed onto the screen.
def main [] {
    print -e "Determining the last tag..."

    let tag_info = (do -i { git describe --tags --abbrev=0 } | complete)

    let commit_range = if $tag_info.exit_code == 0 {
        let tag = ($tag_info.stdout | str trim)
        print -e $"Found last tag: ($tag)"
        $"($tag)..HEAD"
    }

    let raw_log = (git log $commit_range --format="<COMMIT>%H<SEP>%an<SEP>%(trailers:unfold)")
    
    let commits = ($raw_log | split row "<COMMIT>" | where {|c| ($c | str trim) != "" })
    mut changes = []
    print -e $"Parsing (($commits | length)) commits for git trailers..."

    for commit in $commits {
        let parts = ($commit | split row "<SEP>")
        if ($parts | length) < 3 { continue }

        let hash = ($parts.0 | str trim)
        let author = ($parts.1 | str trim)
        let trailers = ($parts.2 | str trim)

        if $trailers == "" { continue }

        let parsed = ($trailers 
            | lines 
            | parse --regex '^(?i)Change-(?P<type>Add|Remove|Tweak|Fix):\s*(?P<message>.*)$')

        for item in $parsed {
            let cat = ($item.type | str downcase)
            $changes = ($changes | append {
                category: $cat
                message: ($item.message | str trim)
                author: $author
                hash: $hash
            })
        }
    }

    if ($changes | is-empty) {
        print -e "\n[Warning] No changelog trailers found in the given commit range."
    } else {
        print -e $"Found (($changes | length)) change entries."
    }

    let release_data = if ($changes | is-empty) {
        {}
    } else {
        $changes | group-by category | transpose key items | reduce --fold {} {|row, acc|
            let cleaned = ($row.items | select message author hash)
            $acc | insert $row.key $cleaned
        }
    }

    print -e ""
    let version = (input "Enter the new semantic version (e.g., 0.1.0): " | str trim)
    let add_summary = (input "Do you want to add a summary? (y/N): " | str downcase | str trim)

    mut final_data = $release_data

    # Holy jesus I really made some dogshit with this one.
    if $add_summary == "y" or $add_summary == "yes" {
        print -e "\nEnter summary (Type a completely empty line and press Enter to finish):"
        mut summary = ""
        loop {
            let line = (input "> ")
            if $line == "" {
                break
            }
            $summary = ($summary + $line + "\n")
        }
        
        let trimmed_summary = ($summary | str trim)
        if $trimmed_summary != "" {
            $final_data = ($final_data | insert summary $trimmed_summary)
        }
    }

    # puke this shit right back out the pipeline
    { $version: $final_data }
}
