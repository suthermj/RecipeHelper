"""Copy the app's secrets from RecipeHelper/appsettings.Production.json into Key Vault.

Run from the repo root after `az login`:  python deploy/arc-keyvault/push-secrets.py
Values are passed to az via a private temp file (never on the command line or printed).
Vault secret names use "--" for ":" -- the Key Vault config provider maps them back.
"""
import json
import os
import subprocess
import sys
import tempfile

VAULT = "kv-recipehelper-gwc"
SUBSCRIPTION = "a4dd9a73-b1d2-4b67-94f0-a7e7fe0bacf7"
KEYS = ["OpenAI:ApiKey", "Spoonacular:apiKey", "Kroger:clientSecret", "Oauth:ClientSecret"]

with open("RecipeHelper/appsettings.Production.json", encoding="utf-8-sig") as f:
    config = json.load(f)

failed = False
for key in KEYS:
    section, name = key.split(":")
    value = (config.get(section) or {}).get(name)
    if not value:
        print(f"SKIP  {key}: not set in appsettings.Production.json")
        continue
    fd, path = tempfile.mkstemp()
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="") as tmp:
            tmp.write(value)
        result = subprocess.run(
            ["az", "keyvault", "secret", "set", "--vault-name", VAULT, "--subscription", SUBSCRIPTION,
             "--name", key.replace(":", "--"), "--file", path, "--encoding", "utf-8", "--output", "none"],
            capture_output=True, text=True, shell=(os.name == "nt"))
    finally:
        os.remove(path)
    if result.returncode == 0:
        print(f"OK    {key} -> {key.replace(':', '--')}")
    else:
        failed = True
        print(f"FAIL  {key}: {result.stderr.strip()}")

sys.exit(1 if failed else 0)
