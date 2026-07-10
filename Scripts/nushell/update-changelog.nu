#!/usr/bin/env nu

def main [
    start_commit: string,
    changelog: path,
] {
    mut changelog_data = (open $changelog)
    mut entries = ($changelog_data | get Entries)

    let max_id = (
        $entries
        | get id
        | math max
        | default 0
    )

    mut next_id = $max_id

    let commits = (
        git rev-list --reverse $"($start_commit)^..HEAD"
        | lines
    )

    for commit in $commits {
        let author = (
            git show -s --format=%an $commit
            | str trim
        )

        let time = (
            git show -s --format=%aI $commit
            | str trim
        )

        mut changes = []

        let trailers = (
            git show -s '--format=%(trailers)' $commit
            | lines
        )

        for trailer in $trailers {
            if ($trailer | str starts-with "Change-Id:") {
                continue
            }

            let parsed = (
                if ($trailer | str starts-with "Change-Add: ") {
                    {
                        type: "Add"
                        message: ($trailer | str replace "Change-Add: " "")
                    }
                } else if ($trailer | str starts-with "Change-Tweak: ") {
                    {
                        type: "Tweak"
                        message: ($trailer | str replace "Change-Tweak: " "")
                    }
                } else if ($trailer | str starts-with "Change-Remove: ") {
                    {
                        type: "Remove"
                        message: ($trailer | str replace "Change-Remove: " "")
                    }
                } else if ($trailer | str starts-with "Change-Fix: ") {
                    {
                        type: "Fix"
                        message: ($trailer | str replace "Change-Fix: " "")
                    }
                } else {
                    null
                }
            )

            if $parsed != null {
                $changes = ($changes | append $parsed)
            }
        }

        if ($changes | is-empty) {
            continue
        }

        $next_id += 1

        $entries = (
            $entries
            | append {
                author: $author
                changes: $changes
                id: $next_id
                time: $time
            }
        )
    }

    $changelog_data = (
        $changelog_data
        | upsert Entries $entries
    )

    $changelog_data
    | to yaml
    | save --force $changelog
}
