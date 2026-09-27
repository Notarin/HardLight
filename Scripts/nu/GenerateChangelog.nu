#!/usr/bin/env nu

# Gets the commits in a range formatted with separators (custom) and raw trailers.
def get-commits [
    range: string # The git commit range expression (e.g., v0.1.0..HEAD)
]: nothing -> list<string> {
    let raw_log = (git log $range --format="<COMMIT>%H<SEP>%an<SEP>%(trailers)")
    $raw_log | split row "<COMMIT>" | where {|c| ($c | str trim) != "" }
}

# Splits a raw trailer block into individual trailer strings, preserving multiline values.
#
# Folds over lines, identifying new trailers by their header and appending indented continuation lines.
def split-trailers []: string -> list<string> {
    $in | lines | reduce --fold [] {|line, acc|
        if ($line =~ '^[a-zA-Z0-9_-]+:') {
            $acc | append $line
        } else if not ($acc | is-empty) {
            let last_idx = (($acc | length) - 1)
            let new_val = $"($acc | get $last_idx)\n($line | str trim)"
            $acc | update $last_idx $new_val
        } else {
            $acc
        }
    }
}

# Parses individual trailer strings into structured records.
#
# Extracts the trailer name and message using regex, returning a standardized list of records.
def parse-trailers []: list<string> -> list<record<name: string, message: string>> {
    $in | each {|trailer|
        # Sure hope you don't need to edit this regex bc I made that shit so badly.
        let parsed = ($trailer | parse --regex '^(?P<name>[a-zA-Z0-9_-]+): (?P<message>.*)$')
        if not ($parsed | is-empty) {
            { name: $parsed.0.name, message: ($parsed.0.message | str trim) }
        } else {
            null
        }
    } | compact
}

# Filters a list of parsed trailers to only include valid changelog trailers.
#
# Matches the trailer name against Change-Add, Change-Remove, Change-Tweak, and Change-Fix.
def filter-changelog-trailers []: list<record<name: string, message: string>> -> list<record<name: string, message: string>> {
    $in | where name =~ '(?i)^Change-(Add|Remove|Tweak|Fix)$'
}

# Parses raw commit strings and extracts multiline changelog trailers.
def parse-commits [
    commits: list<string> # The list of raw commit strings output by get-commits
]: nothing -> list<any> {
    $commits | each {|commit|
        let parts = ($commit | split row "<SEP>")
        if ($parts | length) < 3 { return null }

        let hash = ($parts.0 | str trim)
        let author = ($parts.1 | str trim)

        # Not trimming left, because we want to preserve git trailers multiline indentation.
        let trailers = ($parts.2 | str trim --right)

        if $trailers == "" { return null }

        $trailers
        | split-trailers
        | parse-trailers
        | filter-changelog-trailers
        | each {|t|
            let cat = ($t.name | split row "-" | last | str lowercase)
            {
                category: $cat
                message: $t.message
                author: $author
                hash: $hash
            }
        }
    } | compact | flatten
}

# Builds the structured changelog entry grouped by category from parsed changes.
def build-entry [
    changes: list<any> # The parsed change records
]: nothing -> record {
    if ($changes | is-empty) {
        return {}
    }

    $changes | group-by category | transpose key items | reduce --fold {} {|row, acc|
        let cleaned = ($row.items | select message author hash)
        $acc | insert $row.key $cleaned
    }
}

# Generates a changelog from git trailers and either returns it or natively updates a YAML file.
#
# The command scans a specified git range (or dynamically computes from the last tag
# to HEAD) for specific changelog git trailers (Change-Add, Change-Remove, Change-Tweak,
# Change-Fix) and generates structured changelog data.
export def main [
    filepath?: string # path to a YAML changelog file to update. If omitted, outputs Nu data.
    --version: string # The semantic version for the new release (e.g., 1.14.3).
    --range: string # The git commit range (e.g., staging..HEAD). Defaults to last_tag..HEAD.
    --summary: string # A summary of the release to include in the changelog.
]: nothing -> any {
    if $version == null {
        error make { msg: "The --version flag is required." }
    }

    let commit_range = if $range != null {
        $range
    } else {
        let tag_info = (do -i { git describe --tags --abbrev=0 } | complete)
        if $tag_info.exit_code == 0 {
            let tag = ($tag_info.stdout | str trim)
            $"($tag)..HEAD"
        } else {
            "HEAD" # Should panic tbh, but I just dont care rn.
        }
    }

    let raw_commits = (get-commits $commit_range)
    let parsed_changes = (parse-commits $raw_commits)
    let new_entry_base = (build-entry $parsed_changes)

    let new_entry = if $summary != null {
        $new_entry_base | insert summary ($summary | str trim)
    } else {
        $new_entry_base
    }

    let result = { $version: $new_entry }

    if $filepath != null {
        let existing_data = if ($filepath | path exists) {
            let data = (open $filepath)
            if ($data | describe | str starts-with "record") {
                $data
            } else {
                {}
            }
        } else {
            {}
        }

        # Remove the existing version key if it already exists, so it gets overwritten
        let clean_existing = if ($version in ($existing_data | columns)) {
            $existing_data | reject $version
        } else {
            $existing_data
        }

        let final_data = ($clean_existing | merge $result)
        $final_data | to yaml | save --force $filepath
        return
    } else {
        return $result
    }
}

