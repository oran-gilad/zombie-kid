# ZombieKid

ZombieKid is a Windows system tray monitor for limiting daily gaming time. It watches configured process names, records daily activity JSON, closes configured processes after the daily limit is reached, and publishes a static dashboard from the `docs` folder for GitHub Pages.

## Build

Requires .NET 8 SDK on Windows.

```powershell
dotnet build .\ZombieKid.sln -c Release
```

Publish binaries to the configured publish folder:

```powershell
dotnet publish .\src\ZombieKid\ZombieKid.csproj -c Release -o "C:\Users\oran\OneDrive\Documents\zombie-kid\publish"
```

Release builds also auto-publish to that same folder. In Visual Studio, choose the `Release` configuration and build the `ZombieKid` project or solution.

Run the published tray app:

```powershell
& "C:\Users\oran\OneDrive\Documents\zombie-kid\publish\ZombieKid.exe"
```

## Configuration

Edit `config/settings.json` before publishing, or edit `publish/config/settings.json` after publishing.

Important settings:

- `dailyLimit`: total allowed daily time in `HH:MM:SS`, for example `00:02:00`.
- `pollIntervalSeconds`: process polling interval. Default is `5`.
- `almostOverMinutes`: minutes remaining when the one-time warning balloon appears.
- `processNames`: executable names to monitor and close. Defaults include `notepad.exe` and `notepad++.exe` for testing.
- `dataDirectory`: runtime JSON location. Default is `C:\Users\oran\OneDrive\Documents\zombie-kid\data`.

Time is counted once per poll when at least one configured process is running. If several configured processes run at the same time, total time is not double-counted, but each process gets its own accumulated time.

## Notifications and Closing Processes

ZombieKid shows a tray notification when the configured warning threshold is reached. When the daily limit is reached, it shows another notification, closes only configured running processes, and keeps monitoring so reopening those processes closes them again.

## Email

Email is configured in `config/settings.json` under `email`.

- Set `enabled` to `true` only after SMTP values are configured.
- Put recipients in `recipients`.
- Put local summary times in `summaryEmailTimes`, for example `["17:00", "21:00"]`.
- Leave `password` empty in committed files. Do not commit real SMTP passwords, tokens, or credentials.

ZombieKid sends summary emails at configured local times and sends one threshold email per day when configured processes are closed after the daily limit is reached.

## Data Files

Runtime data is written under:

```text
C:\Users\oran\OneDrive\Documents\zombie-kid\data\
```

Each day gets one JSON file, such as `data/2026-05-16.json`. The `data/index.json` file lists all available daily JSON files for the dashboard.

## Dashboard

GitHub Pages can serve the static dashboard from `/docs`:

- `docs/index.html`
- `docs/app.js`
- `docs/style.css`

The dashboard fetches `../data/index.json`, then loads daily files from `../data/YYYY-MM-DD.json`. It shows today, the last 7 days, total time, limit, remaining time, and per-process breakdown.

## GitHub Sync

The app periodically runs these commands in the repository directory:

```powershell
git add data docs
git commit -m "Update activity data"
git push
```

If there are no `data` or `docs` changes, it skips committing and does not fail. Authentication should be handled by your local Git/GitHub setup. Do not store GitHub tokens, email passwords, or credentials in this repo.

## GitHub API Sync Without Git

For a simple deployment on another Windows machine, publish ZombieKid as a self-contained app and enable `githubApiSync` in the deployed `config/settings.json`. This mode uploads the daily JSON files directly to GitHub through the GitHub REST API, so the child machine does not need Git installed or authenticated.

Create a fine-grained GitHub token with access only to this repository and only the `Contents: Read and write` permission. Put that token only in the deployed machine's local `config/settings.json`; do not commit it.

Example deployed config:

```json
"gitSync": {
  "enabled": false,
  "repositoryDirectory": "",
  "syncIntervalMinutes": 10
},
"githubApiSync": {
  "enabled": true,
  "owner": "oran-gilad",
  "repo": "zombie-kid",
  "branch": "master",
  "token": "PASTE_FINE_GRAINED_TOKEN_ON_DEPLOYED_MACHINE_ONLY",
  "syncIntervalMinutes": 10
}
```

The API sync uploads each `data/*.json` file both to `data/` and to `docs/data/` so GitHub Pages can read the dashboard data.
