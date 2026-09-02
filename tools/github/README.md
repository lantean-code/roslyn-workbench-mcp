# GitHub Repository Tools

This directory contains repository-side helpers for applying GitHub configuration from reviewed files.

## Label synchronisation

`.github/labels.json` is the canonical managed label set. Run either platform-specific script from any working directory:

```bash
bash tools/github/sync-labels.sh
```

```powershell
.\tools\github\sync-labels.ps1
```

Both scripts use the authenticated GitHub CLI repository, creating missing labels and updating existing labels to match the manifest. Labels absent from the manifest are left unchanged by default.

Pass `--prune` to the Bash script or `-Prune` to the PowerShell script only when deliberately removing every unmanaged label. Pruning is destructive and is not part of routine synchronisation.

For the initial repository setup, run the non-destructive synchronisation first and inspect the unmanaged labels left on GitHub. After confirming that none should be retained, explicitly approve one pruned synchronisation to remove GitHub's default labels and avoid synonyms for the managed taxonomy.
