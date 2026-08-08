import os, json
from pathlib import Path
from collections import defaultdict

modified = os.getenv('modified', '').split(' ')
removed = os.getenv('removed', '').split(' ')
added = os.getenv('added', '').split(' ')

baseName = os.getenv('basename', '')
baseSHA = os.getenv('basesha', '')
headName = os.getenv('headname', '')
headSHA = os.getenv('headsha', '')

rsiStates = defaultdict(list)

def CheckApplicable(pth):
    if pth.suffix != '.png':
        return False
    if '.rsi' not in str(pth):
        return False
    return True

def GetGitHubRawImageLink(fullName, sha, location):
    return f"https://code.hardlight.space/{fullName}/raw/commit/{sha}/{location}"

def WrapInCollapsible(markdown, title):
    return f"<details><summary>{title}</summary>\n<p>\n\n{markdown}\n\n</p>\n</details>"

def CreateTable(states):
    table = "| State | Old | New | Status\n| --- | --- | --- | --- |\n"
    for state in states:
        status = 'modified'
        if state[1] == None:
            status = 'added'
        elif state[2] == None:
            status = 'removed'

        table += f"| {state[0]} | ![]({state[1] or ''}) | ![]({state[2] or ''}) | {status}\n"
    return table

for state in modified:
    pth = Path(state)
    if not CheckApplicable(pth):
        continue
    rsi = str(pth.parent)
    changed = (
        pth.stem,
        GetGitHubRawImageLink(baseName, baseSHA, state),
        GetGitHubRawImageLink(headName, headSHA, state)
    )
    rsiStates[rsi].append(changed)

for state in removed:
    pth = Path(state)
    if not CheckApplicable(pth):
        continue
    rsi = str(pth.parent)
    changed = (
        pth.stem,
        GetGitHubRawImageLink(baseName, baseSHA, state),
        None
    )
    rsiStates[rsi].append(changed)

for state in added:
    pth = Path(state)
    if not CheckApplicable(pth):
        continue
    rsi = str(pth.parent)
    changed = (
        pth.stem,
        None,
        GetGitHubRawImageLink(headName, headSHA, state)
    )
    rsiStates[rsi].append(changed)

if len(rsiStates) > 0:
    output = f"RSI Diff Bot; head commit {headSHA} merging into {baseSHA}\n"
    output += "This PR makes changes to 1 or more RSIs. Here is a summary of all changes:"

    for rsi in rsiStates:
        output += WrapInCollapsible(CreateTable(rsiStates[rsi]), rsi)

    safe_output = json.dumps(output)[1:-1]
    print(f"Setting summary-details to: {output}")
    f = open(os.environ['FORGEJO_OUTPUT'], 'a')
    print(f"summary-details={safe_output}", file=f)
    print(f"hasDiffs=true", file=f)
    f.close()
else:
    print("No RSI changes found!")
    f = open(os.environ['FORGEJO_OUTPUT'], 'a')
    print(f"hasDiffs=false", file=f)
    f.close()
