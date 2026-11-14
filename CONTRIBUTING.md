# Contributing

## Before changing code

1. Read the implementation roadmap and acceptance plan in [README.md](README.md).
2. Keep the app Windows-first and preserve real OpenSSH/ConPTY transport.
3. Do not replace SSH, Cloudflare Access or SFTP behaviour with simulated output.
4. Keep secrets out of commits, screenshots, logs and CI artifacts.

## Build and test

Requirements: Windows 10/11 x64, .NET 8 SDK, Node.js 20+, Microsoft Edge WebView2 Runtime, Windows OpenSSH Client.

```powershell
cd src/HattrickSSH
npm install
New-Item -ItemType Directory -Force wwwroot/lib | Out-Null
Copy-Item node_modules/@xterm/xterm/lib/xterm.js wwwroot/lib/xterm.js
Copy-Item node_modules/@xterm/xterm/css/xterm.css wwwroot/lib/xterm.css
Copy-Item node_modules/@xterm/addon-fit/lib/addon-fit.js wwwroot/lib/addon-fit.js
cd ../..
./scripts/generate-icon.ps1
dotnet restore src/HattrickSSH/HattrickSSH.csproj
dotnet build src/HattrickSSH/HattrickSSH.csproj -c Release
dotnet test tests/HattrickSSH.Persistence.Tests/HattrickSSH.Persistence.Tests.csproj -c Release
```

Never add a real private key, Access token or personal server credential to a test fixture. Use a disposable SSH test host for transport tests. The GitHub Actions Windows workflow runs the persistence and SSH configuration tests before packaging.

## Pull requests

- Keep commits small and explain why a change is needed.
- Include build results and the manual test steps performed.
- Update README checkboxes only when there is evidence.
- For security-sensitive changes, explain the threat addressed and how it was validated.

## Security reports

Do not post private keys, Access cookies, or vulnerability details in public issues. Contact the repository owner privately.
