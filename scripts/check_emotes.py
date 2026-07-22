import json
import os
import re
import subprocess
import urllib.request
from bs4 import BeautifulSoup

JSON_FILE_PATH = "ref/json/emotes.json"  # Path to your local supported emotes JSON
WIKI_URL = "https://wiki.guildwars2.com/wiki/Emote"

# 1. Load local supported emotes file
if not os.path.exists(JSON_FILE_PATH):
    print(f"Error: Could not find '{JSON_FILE_PATH}' in the repository root.")
    exit(1)

with open(JSON_FILE_PATH, 'r', encoding='utf-8') as f:
    raw_data = json.load(f)

# Extract emote keys from list of dicts e.g. [{"id": "yes", "command": "/yes"}]
supported_emotes = set()
if isinstance(raw_data, list):
    for item in raw_data:
        if isinstance(item, dict):
            # Capture 'id' (e.g., "yes") and clean 'command' (e.g., "/yes" -> "yes")
            if "id" in item and item["id"]:
                supported_emotes.add(str(item["id"]).lower().strip())
            if "command" in item and item["command"]:
                supported_emotes.add(str(item["command"]).lstrip("/").lower().strip())

print(f"Loaded {len(supported_emotes)} unique emote identifiers from {JSON_FILE_PATH}.")

# 2. Scrape the Guild Wars 2 Wiki table
req = urllib.request.Request(
    WIKI_URL,
    headers={'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64) GW2EmoteChecker/1.0'}
)

try:
    with urllib.request.urlopen(req) as response:
        html = response.read().decode('utf-8')
except Exception as e:
    print(f"Error fetching GW2 Wiki: {e}")
    exit(1)
soup = BeautifulSoup(html, 'html.parser')
wiki_emotes = {}

for table in soup.find_all('table', class_='mech1'):
    for row in table.find_all('tr'):
        cols = row.find_all(['td'])
        if not cols:
            continue

        command_cell_text = cols[0].get_text()
        raw_commands = re.findall(r'/[a-zA-Z0-9]+', command_cell_text)

        if raw_commands:
            primary_command = raw_commands[0].lower()
            clean_name = primary_command.lstrip('/')

            wiki_emotes[clean_name] = primary_command

# 3. Filter for Wiki emotes not present in local supported set
missing_emotes = {name: data for name, data in wiki_emotes.items() if name not in supported_emotes}

if not missing_emotes:
    print("All wiki emotes are already present in emotes.json. Nothing to do!")
    exit(0)

print(f"Found {len(missing_emotes)} missing emote(s): {list(missing_emotes.keys())}")

# 4. Check GitHub issues for missing emotes before opening a new issue
#for name, data in missing_emotes.items():
#    title = f"New GW2 Emote Discovered: /{name}"
#
#    result = subprocess.run(
#        ['gh', 'issue', 'list', '--state', 'all', '--search', f'"{title}"', '--json', 'title'],
#        capture_output=True,
#        text=True
#    )
#
#    existing_issues = json.loads(result.stdout) if result.returncode == 0 else []
#
#    if not existing_issues:
#        print(f"Creating issue for missing emote: /{name}...")
#        body = (
#            f"A new emote was found on the GW2 Wiki that is missing from `{JSON_FILE_PATH}`!\n\n"
#            f"- **Emote ID:** `{name}`\n"
#            f"- **Command:** `{data['command']}`\n"
#            f"- **Source:** {data['source']}\n\n"
#            f"View details on the [GW2 Wiki Emote Page]({WIKI_URL})."
#        )
#        subprocess.run(['gh', 'issue', 'create', '--title', title, '--body', body])
#    else:
#        print(f"Issue for '/{name}' already exists in GitHub. Skipping.")
