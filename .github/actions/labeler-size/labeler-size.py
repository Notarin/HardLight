import json, os, sys
import urllib.parse
import urllib.request

# Env Vars
FORGEJO_URL = os.getenv('FORGEJO_URL', 'https://code.hardlight.space')
FORGEJO_REPO = os.getenv('REPO', os.getenv('FORGEJO_REPOSITORY', 'HardLight/HardLight'))
FORGEJO_PR_NO = os.getenv('PR', '1')
FORGEJO_TOKEN = os.getenv('TOKEN', None)
LABEL_SIZES = os.getenv('SIZES', '{"0": "XS", "10": "S", "30": "M", "100": "L", "500": "XL", "1000": "XXL"}')

def getChangeCounts(pr_no):
    api_url = f"{FORGEJO_URL}/api/v1/repos/{FORGEJO_REPO}/pulls/{pr_no}"
    req = urllib.request.Request(api_url)
    if FORGEJO_TOKEN:
        req.add_header('Authorization', f"token {FORGEJO_TOKEN}")

    additions = 0
    deletions = 0
    with urllib.request.urlopen(req) as response:
        data = json.loads(response.read().decode('utf-8'))
        additions = int(data['additions'])
        deletions = int(data['deletions'])
    return {'additions': additions, 'deletions': deletions}

def getSizeLabel(totalCount):
    size_map = json.loads(LABEL_SIZES)
    flippedLabels = dict(sorted(size_map.items(), reverse=True, key=lambda x: int(x[0])))
    label = ""
    for l in flippedLabels.items():
        if totalCount >= int(l[0]):
            label = "size/" + l[1]
    return label

def getCurrentLabel(pr_no):
    api_url = f"{FORGEJO_URL}/api/v1/repos/{FORGEJO_REPO}/pulls/{pr_no}"
    req = urllib.request.Request(api_url)
    if FORGEJO_TOKEN:
        req.add_header('Authorization', f"token {FORGEJO_TOKEN}")

    currentLabel = None
    with urllib.request.urlopen(req) as response:
        data = json.loads(response.read().decode('utf-8'))
        if 'labels' not in data.keys():
            return None
        for l in data['labels']:
            if l['name'].startswith('size/'):
                currentLabel = l['name']
    return currentLabel

def removeLabel(pr_no, label):
    api_url = f"{FORGEJO_URL}/api/v1/repos/{FORGEJO_REPO}/issues/{pr_no}/labels/{urllib.parse.quote_plus(label)}"

    req = urllib.request.Request(api_url, method='DELETE')
    if FORGEJO_TOKEN:
        req.add_header('Authorization', f"token {FORGEJO_TOKEN}")
    req.add_header('Content-Type', 'application/json')

    with urllib.request.urlopen(req) as response:
        if response.status != 204:
            sys.exit(f"Failed to remove label '{labelNo}', error: {response.status}")

def addLabel(pr_no, label):
    api_url = f"{FORGEJO_URL}/api/v1/repos/{FORGEJO_REPO}/issues/{pr_no}/labels"
    payload = {'labels': [label]}
    payload_bytes = json.dumps(payload).encode('utf-8')

    req = urllib.request.Request(api_url, data=payload_bytes, method='POST')
    if FORGEJO_TOKEN:
        req.add_header('Authorization', f"token {FORGEJO_TOKEN}")
    req.add_header('Content-Type', 'application/json')

    with urllib.request.urlopen(req) as response:
        if response.status != 200:
            sys.exit(f"Failed to add label '{label}', error: {response.status}")

def main():
    counts = getChangeCounts(FORGEJO_PR_NO)
    total_count = counts['additions'] + counts['deletions']
    newLabel = getSizeLabel(total_count)
    currentLabel = getCurrentLabel(FORGEJO_PR_NO)
    if newLabel != currentLabel:
        if currentLabel != None:
            removeLabel(FORGEJO_PR_NO, currentLabel)
        addLabel(FORGEJO_PR_NO, newLabel)

if __name__ == "__main__":
    main()
