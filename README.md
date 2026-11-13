# Hattrick SSH

Modern Windows SSH workspace with a Codex-inspired dark interface. The project is built around real OpenSSH sessions, Cloudflare Access, locally protected SSH keys, a persistent server library, and GitHub Releases updates.

> This is an original implementation. Feature ideas are informed by publicly documented workflows in WindTerm and Tabby; their source code and branding are not copied.

## Product goals

- Windows 10/11 x64 desktop app with a self-contained portable EXE and a normal setup EXE.
- Real interactive remote shells (PTY), not a command-output simulator.
- Cloudflare Tunnel SSH using the documented client-side `cloudflared access ssh` ProxyCommand flow.
- RSA/OpenSSH private-key import. Key bytes are encrypted with Windows DPAPI for the current Windows user and never enter GitHub or release artifacts.
- Server profiles, connection status, terminal font/scrollback controls, reconnect, local command snippets, and connection diagnostics.
- GitHub Actions builds every push to `main`; tagged releases publish portable and setup EXEs.
- In-app update check reads the latest GitHub Release, verifies the installer SHA-256, then launches the signed-off silent installer flow.

## UI direction

Codex-inspired workstation: narrow left navigation, server/session list, terminal workspace, understated charcoal surfaces, restrained green connection indicators, compact typography, keyboard-first interactions, and a transparent terminal/cloud/shield app mark.

## Architecture

| Area | Choice | Reason |
|---|---|---|
| Desktop shell | .NET 8 WPF | Native Windows windowing, accessibility and packaging |
| Terminal renderer | xterm.js + WebView2 | ANSI/VT sequences, scrollback, keyboard input and terminal sizing |
| Local PTY | Windows ConPTY | Resizable pseudo console for native OpenSSH |
| SSH transport | Windows OpenSSH client | Proven compatibility with Cloudflare's ProxyCommand model |
| Cloudflare | cloudflared | Official client-side Access SSH authentication |
| Secrets | DPAPI CurrentUser | Private keys encrypted on this Windows account |
| Updates | GitHub Releases + SHA-256 manifest | Public, auditable update channel |
| CI/release | GitHub Actions + Inno Setup | Repeatable Windows builds and installer |

### Connection flow

`Hattrick SSH → ConPTY → ssh.exe → ProxyCommand cloudflared access ssh → Cloudflare Access → Tunnel → Azure/Linux SSH`

For direct SSH, the ProxyCommand is omitted. OpenSSH handles first-use host-key prompts in the terminal. The app never disables host-key verification.

## Roadmap and completion checklist

The README is the source of truth for delivery. Check items only after their tests are performed.

### Phase 0 — Repository and product definition
- [x] Inspect the empty repository and confirm `main` is the default branch.
- [x] Define architecture, release format, and acceptance criteria.
- [ ] Add license, dependency notices, and contribution guidance.

### Phase 1 — Desktop foundation
- [ ] Create .NET 8 WPF solution and Codex-inspired shell.
- [ ] Generate transparent multi-size Windows icon from source vector artwork.
- [ ] Add left navigation for Servers, Terminal, SFTP, Port Forwarding, Snippets, History, Appearance, Security, Updates and About.
- [ ] Add local profile persistence and first-run default profile.
- [ ] Verify x64 self-contained publish.

### Phase 2 — Real SSH terminal
- [ ] Bundle xterm.js and xterm-addon-fit.
- [ ] Implement Windows ConPTY lifecycle, keyboard input, ANSI output and resize.
- [ ] Discover Windows OpenSSH safely and show an actionable error if missing.
- [ ] Start forced interactive SSH PTY sessions and support reconnect/disconnect.
- [ ] Support Ctrl+C, Ctrl+D, Tab, Esc, arrows, Enter, Backspace and paste.
- [ ] Validate first-use host key prompt and persistent known_hosts.

### Phase 3 — Cloudflare Access
- [ ] Provision official cloudflared Windows x64 binary on demand.
- [ ] Use `ProxyCommand cloudflared access ssh --hostname %h` for SSH-type routes.
- [ ] Open Access browser login through cloudflared/OpenSSH and preserve its session cache.
- [ ] Surface authentication, proxy and SSH failures without exposing tokens.
- [ ] Verify the exact Azure host from a Windows machine; do not alter Tunnel settings.

### Phase 4 — Key handling and profiles
- [ ] Import RSA/OpenSSH private keys and encrypt them using DPAPI CurrentUser.
- [ ] Write a short-lived, user-restricted plaintext key only when OpenSSH requires a file.
- [ ] Delete temporary plaintext key material when the session ends.
- [ ] Add, edit, duplicate, delete, search and group server profiles.
- [ ] Keep credentials and private keys out of logs, Git and release assets.

### Phase 5 — Productivity workspace
- [ ] Remember terminal font size, theme, scrollback and window layout.
- [ ] Add reusable command snippets with one-click send.
- [ ] Add connection history and sanitized diagnostics.
- [ ] Add SSH keep-alive and an explicit reconnect action.
- [ ] Implement SFTP browser and upload/download.
- [ ] Implement local, remote and dynamic port forwarding.
- [ ] Add multi-tab sessions and split panes.

### Phase 6 — Updates and distribution
- [x] Build portable self-contained x64 package as `HattrickSSH-Portable.zip`.
- [x] Build `HattrickSSH-Setup.exe` with per-user installation.
- [x] Generate `latest.json` with version, notes URL and installer SHA-256.
- [x] Implement GitHub Releases check and SHA-256 verification in the desktop updater.
- [x] Configure main-branch builds to publish versioned GitHub Releases; tag/manual dispatch builds are also supported.
- [x] Preserve app data outside the installation directory (profiles, DPAPI key files, known_hosts and preferences).

### Phase 7 — Validation and release
- [ ] Build on GitHub Actions Windows runner.
- [ ] Smoke-test application startup and WebView2 terminal assets.
- [ ] Test direct SSH and Cloudflare Access separately.
- [ ] Test `hostname`, `whoami`, `df -h /`, `free -h` and `sudo systemctl status cloudflared`.
- [ ] Test key persistence after restart and update.
- [ ] Test Ctrl+C, resize, reconnect, host-key rejection and updater hash validation.
- [ ] Publish first stable release only after Windows-side acceptance tests.

## Current status

The repository was created empty. The initial Windows x64 build and setup package have passed GitHub Actions. Main-branch CI builds are published as versioned GitHub Releases (0.1.<run number>); create a `vX.Y.Z` tag for a manually selected release version. Azure acceptance tests still require a Windows device with access to the user's Cloudflare Access identity and imported private key; GitHub Actions cannot truthfully substitute for that interactive test.

## Build locally

Requirements: .NET 8 SDK, Windows 10/11 x64, WebView2 Runtime, Node.js 20+.

```powershell
cd src/HattrickSSH
npm install
dotnet restore
dotnet build -c Release
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

For the cloudflared SSH transport, the Windows OpenSSH Client capability and a valid Cloudflare Access policy are required. The app can install cloudflared automatically, but it cannot create or modify Cloudflare Tunnel routes.

## Design references

- [WindTerm](https://github.com/kingToolbox/WindTerm): session tree, tabs, SFTP, tunnels, command snippets, reconnect.
- [Tabby](https://github.com/Eugeny/tabby): terminal tabs, split layouts, themes, keymaps and plugins.
- [SSH.NET](https://github.com/sshnet/SSH.NET): MIT-licensed .NET SSH/SFTP library (evaluated; this implementation uses OpenSSH for Cloudflare ProxyCommand compatibility).
- [Cloudflare SSH with client-side cloudflared](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/use-cases/ssh/ssh-cloudflared-authentication/).

## Security

Never commit private keys, Access tokens, identity-provider cookies, local settings, or known_hosts data. The app intentionally stores only local encrypted key files and profile metadata. Do not turn off SSH host-key checks to work around a connection error.
