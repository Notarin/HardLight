import json, os, fnmatch, re, sys
import urllib.parse
import urllib.request

# Env Vars
output_format = os.getenv('FORMAT', 'json')
output_filter = os.getenv('FILTER', '*').splitlines()
url = os.getenv('FORGEJO_URL', 'https://code.hardlight.space')
repo = os.getenv('FORGEJO_REPO', 'Drekk/HardLight')
pr_index = os.getenv('PR', '1')
token = os.getenv('TOKEN', None)

# We have to do some processing because git uses a weird glob format
def processGlob(fileName, glob):
    is_negated = glob.startswith('!')
    if is_negated:
        glob = glob[1:]
    if glob.endswith('/'):
        glob += '*'
    raw_regex = fnmatch.translate(glob)

    if '**' not in glob:
        raw_regex = raw_regex.replace('.*', '[^/]*')
    if not glob.startswith('/'):
        raw_regex = raw_regex.replace(r'\A', r'(^|.*/)')

    regex_compiled = re.compile(raw_regex)
    if is_negated:
        return not regex_compiled.match(fileName)
    else:
        return regex_compiled.match(fileName)

def matchesFilters(fileName, filters):
    for filt in filters:
        if processGlob(fileName, filt):
            return True
    return False

# TODO: get PR head, and master head, and run compare endpoint to get files
# get PR, start is merge_base, end is head.sha
# Get the PR information
prReq = urllib.request.Request(f"{url}/api/v1/repos/{repo}/pulls/{pr_index}")
if token:
    prReq.add_header('Authorization', f"token {token}")

baseSha = ''
headSha = ''
with urllib.request.urlopen(prReq) as resp:
    if resp.status != 200:
        sys.exit(f"Failed to get PR {pr_index}: HTTP {resp.status}")
    prData = json.loads(resp.read().decode('utf-8'))
    baseSha = prData['merge_base']
    headSha = prData['head']['sha']

# Get all the files for the commits
print(f"Getting files for PR {pr_index}: Base SHA: {baseSha}, Head SHA: {headSha}")
fileReq = urllib.request.Request(f"{url}/api/v1/repos/{repo}/compare/{baseSha}..{headSha}")
if token:
    fileReq.add_header('Authorization', f"token {token}")

out_all = []
with urllib.request.urlopen(fileReq) as resp:
    if resp.status != 200:
        sys.exit(f"Failed to get changed files between Base SHA: '{baseSha}' and Head SHA: '{headSha}'")
    fileData = json.loads(resp.read().decode('utf-8'))
    for commit in fileData['commits']:
        out_all += commit['files']

out_added = []
out_modified = []
out_removed = []
out_renamed = []

# https://forgejo.dev/forgejo.dev/forgejo/src/commit/c928cf39948dbdecdfcf205ce3b7d52ffd1abeee/modules/structs/hook.go#L96
for f in out_all:
    if f['status'] == 'added':
        out_added.append(f)
    elif f['status'] == 'modified':
        out_modified.append(f)
    elif f['status'] == 'removed':
        out_removed.append(f)

allFormatted = ''
addedFormatted = ''
modifiedFormatted = ''
removedFormatted = ''
renamedFormatted = ''

if output_format == 'space-delimited':
    for f in out_all:
        if f['filename'].find(' ') != -1: # At least one file has a space
            sys.exit('One of your files includes a space. Consider using a different output format or removing spaces from your filenames.')
    allFormatted = ' '.join(f['filename'] for f in out_all)
    addedFormatted = ' '.join(f['filename'] for f in out_added)
    modifiedFormatted = ' '.join(f['filename'] for f in out_modified)
    removedFormatted = ' '.join(f['filename'] for f in out_removed)
    renamedFormatted = ' '.join(f['filename'] for f in out_renamed)

elif output_format == 'csv':
    allFormatted = ','.join(f['filename'] for f in out_all)
    addedFormatted = ','.join(f['filename'] for f in out_added)
    modifiedFormatted = ','.join(f['filename'] for f in out_modified)
    removedFormatted = ','.join(f['filename'] for f in out_removed)
    renamedFormatted = ','.join(f['filename'] for f in out_renamed)

elif output_format == 'json':
    allFormatted = json.dumps(out_all)
    addedFormatted = json.dumps(out_added)
    modifiedFormatted = json.dumps(out_modified)
    removedFormatted = json.dumps(out_removed)
    renamedFormatted = json.dumps(out_renamed)

print(f"All: {allFormatted}")
print(f"Added: {addedFormatted}")
print(f"Modified: {modifiedFormatted}")
print(f"Removed: {removedFormatted}")
print(f"Renamed: {renamedFormatted}")

f = open(os.environ['FORGEJO_OUTPUT'], 'a')
print(f"all={allFormatted}", file=f)
print(f"added={addedFormatted}", file=f)
print(f"modified={modifiedFormatted}", file=f)
print(f"removed={removedFormatted}", file=f)
print(f"renamed={renamedFormatted}", file=f)
f.close()
