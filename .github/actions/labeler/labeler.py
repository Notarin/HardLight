import json, os, yaml, fnmatch, re, sys
import urllib.parse
import urllib.request

# Env Vars
labeler_file = os.getenv('LABELER_FILE', './labeler.yml')
url = os.getenv('FORGEJO_URL', 'https://code.hardlight.space')
repo = os.getenv('FORGEJO_REPO', 'Drekk/HardLight')
pr_index = os.getenv('PR', '1')
token = os.getenv('TOKEN', None)

api_url = f"{url}/api/v1/repos/{repo}/pulls/{pr_index}/files"

# We have to do some processing because git uses a weird glob format
def processGlob(files, glob):
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
        return [f for f in files if not regex_compiled.match(f)]
    else:
        return [f for f in files if regex_compiled.match(f)]

# I'm sure there's more of these but I ain't doing all that
def processLabelBlock(labelBlock):
	action = list(labelBlock.keys())[0]
	if action == 'any-glob-to-any-file':
		if type(labelBlock[action]) == str:
			if processGlob(files, labelBlock[action]):
				return True
		elif type(labelBlock[action]) == list:
			for glob in labelBlock[action]:
				if processGlob(files, glob):
					return True
		return False

	elif action == 'all-globs-to-any-file':
		if type(labelBlock[action]) == str:
			if not processGlob(files, labelBlock[action]):
				return False
		elif type(labelBlock[action]) == list:
			for glob in labelBlock[action]:
				if not processGlob(files, glob):
					return False
		return True

	elif action == 'all-globs-to-all-files':
		if type(labelBlock[action]) == str:
			if len(processGlob(files, labelBlock[action])) != len(files):
				return False
		elif type(labelBlock[action]) == list:
			for glob in labelBlock[action]:
				if len(processGlob(files, glob)) != len(files):
					return False
		return True
	return False

hasMore = True
page = 1
limit = 50
files = []
try:
	while hasMore:
		params = urllib.parse.urlencode({'page': page, 'limit': limit})
		req = urllib.request.Request(api_url + '?' + params)
		if token:
			req.add_header('Authorization', f"token {token}")

		with urllib.request.urlopen(req) as response:
			if response.status == 200:
				files_data = json.loads(response.read().decode('utf-8'))
				hasMore = response.getheader('X-HasMore') == 'true'
				page = page + 1

				for file_obj in files_data:
					files.append(file_obj.get('filename'))
			else:
				sys.exit(f"Failed to get files with status code: {response.status}")
except urllib.error.HTTPError as e:
	sys.exit(f"HTTP Error: {e.code} - {e.reason}")

print(f"Got {len(files)} files, processing tags")

with open(labeler_file, 'r') as f:
	labeler = yaml.safe_load(f)

allTags = list(labeler.keys())
apply_tags = []

# Make sure all the tags exist, and if not create em
# Get all tags for the repo
req = urllib.request.Request(f"{url}/api/v1/repos/{repo}/labels")
req.add_header('Authorization', f"token {token}")
with urllib.request.urlopen(req) as response:
	if response.status != 200:
		sys.exit(f"Failed to get tags, code: {response.status}, error: {response.text}")
	repoTagsRaw = json.loads(response.read().decode('utf-8'))
	repoTags = []
	for t in repoTagsRaw:
		repoTags.append(t['name'])
	tagsToAdd = []
	for t in allTags:
		if t not in repoTags:
			tagsToAdd.append(t)

	for t in tagsToAdd: # We have tags to add, lets add em
		payload = {'color': '#d93f0b', 'name': t}
		payload_bytes = json.dumps(payload).encode('utf-8')
		req2 = urllib.request.Request(f"{url}/api/v1/repos/{repo}/labels", data=payload_bytes, method='POST')
		req2.add_header('Authorization', f"token {token}")
		req2.add_header('Content-Type', 'application/json')
		with urllib.request.urlopen(req2) as resp2:
			if resp2.status != 201:
				sys.exit(f"Failed to add tags to repo with status code {resp2.status}, error: {resp2.text}")

# Check the file list against all the globs
for t in allTags:
	apply = False
	block = labeler[t][0]
	if 'changed-files' in block.keys(): #No Nesting
		for act in block['changed-files']:
			if processLabelBlock(act):
				apply = True
	elif 'all' in block.keys(): # We want ALL these to match
		blocks = block['all'][0]['changed-files']
		needed = len(blocks)
		for act in blocks:
			if processLabelBlock(act):
				needed = needed - 1
		if needed == 0: # If all matched
			apply = True
	if apply:
		apply_tags.append(t)
	print(f"Processed tag '{t}', adding: {apply}")

# Add the tags to the PR
url = f"{url}/api/v1/repos/{repo}/issues/{pr_index}/labels"
payload = {'labels': apply_tags}
payload_bytes = json.dumps(payload).encode('utf-8')
req = urllib.request.Request(url, data=payload_bytes, method='POST')
if token:
	req.add_header('Authorization', f"token {token}")
req.add_header('Content-Type', 'application/json')
with urllib.request.urlopen(req) as response:
	if response.status != 200:
		sys.exit(f"Failed to add tags with status code {response.status}, error: {response.text}")
