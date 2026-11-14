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
- [x] Add MIT license and third-party dependency notices.
- [x] Add README-driven implementation roadmap and acceptance plan.
- [x] Add contribution guidance for external contributors (CONTRIBUTING.md).

### Phase 1 — Desktop foundation
- [x] Create .NET 8 WPF solution and Codex-inspired shell.
- [x] Add transparent SVG mark and generate the Windows ICO during CI (256 px source; multi-size ICO expansion remains a polish item).
- [x] Add left navigation for Servers, Terminal, SFTP, Port Forwarding, Snippets, History, Appearance, Security, Updates and About.
- [x] Add local profile persistence, first-run Azure profile, search and grouping.
- [x] Verify an x64 self-contained publish and installer in GitHub Actions (release baseline v0.1.37).

### Phase 2 — Real SSH terminal
- [x] Bundle xterm.js and xterm-addon-fit in the Windows release.
- [x] Implement Windows ConPTY lifecycle, keyboard input, ANSI output and resize.
- [x] Discover Windows OpenSSH safely and show an actionable error if missing.
- [x] Start forced interactive SSH PTY sessions and support reconnect/disconnect.
- [x] Route normal terminal input through xterm.js (including Ctrl+C, Ctrl+D, Tab, Esc, arrows, Enter, Backspace and paste).
- [x] Configure first-use host-key prompt and a dedicated persistent known_hosts file.
- [ ] Complete Windows interactive keyboard/resize/host-key acceptance tests.

### Phase 3 — Cloudflare Access
- [x] Provision the official cloudflared Windows x64 binary on demand and verify its published SHA-256 digest when available.
- [x] Use `ProxyCommand cloudflared access ssh --hostname %h` for SSH and SFTP.
- [x] Delegate Access browser login and session cache to cloudflared/OpenSSH; the app does not copy Access cookies or tokens.
- [x] Surface startup, proxy and SSH errors without logging secrets.
- [ ] Verify Cloudflare Access and the exact Azure host from a Windows machine; do not alter Tunnel settings.

### Phase 4 — Key handling and profiles
- [x] Import RSA/OpenSSH private keys and encrypt them using DPAPI CurrentUser.
- [x] Write a short-lived, user-restricted plaintext key only when OpenSSH requires a file.
- [x] Delete temporary plaintext key material when the session ends and clean stale temp identities at next startup.
- [x] Add, edit, duplicate, delete, search and group server profiles.
- [x] Keep private key bytes out of Git, logs and release assets.

### Phase 5 — Productivity workspace
- [x] Remember terminal font size, theme and scrollback.
- [x] Add reusable command snippets with create/edit/delete and one-click send.
- [x] Add local connection history; it stores no commands or terminal output.
- [x] Add SSH keep-alive, manual reconnect and bounded automatic reconnect.
- [x] Provide a real interactive OpenSSH SFTP command session (`ls`, `cd`, `get`, `put`); a graphical SFTP browser remains open work.
- [x] Implement local, remote and SOCKS5/dynamic SSH port forwarding.
- [ ] Add multi-tab sessions and split panes.

### Phase 6 — Updates and distribution
- [x] Build portable self-contained x64 package as `HattrickSSH-Portable.zip`.
- [x] Build `HattrickSSH-Setup.exe` with per-user installation.
- [x] Generate `latest.json` with version, notes URL and installer SHA-256.
- [x] Implement GitHub Releases check and SHA-256 verification in the desktop updater.
- [x] Configure main-branch builds to publish versioned GitHub Releases; tag/manual dispatch builds are also supported.
- [x] Preserve app data outside the installation directory (profiles, DPAPI key files, known_hosts and preferences).

### Phase 7 — Validation and release
- [x] Build and package on GitHub Actions Windows runner (published baseline v0.1.55).
- [ ] Pass the new automated LocalStore persistence and SSH configuration security test suite in Windows CI.
- [x] Bundle WebView2 terminal assets in the Windows package.
- [ ] Smoke-test application startup on a real Windows desktop.
- [ ] Test direct SSH and Cloudflare Access separately.
- [ ] Test `hostname`, `whoami`, `df -h /`, `free -h` and `sudo systemctl status cloudflared`.
- [ ] Test key persistence after restart and update.
- [ ] Test Ctrl+C, resize, reconnect, host-key rejection and updater hash validation.
- [ ] Rebuild and publish the current main revision after the latest feature changes.

## Current status

The repository was created empty. The latest published Windows x64 baseline is v0.1.55; it includes the self-contained setup EXE, portable ZIP and SHA-256 update manifest. Main-branch CI generates a new version `0.1.<run number>` for each push. The current main revision also includes automated tests for local profile/settings/history persistence and SSH configuration validation, plus a stricter profile-ID check before creating SSH config files. Those latest changes are not part of v0.1.55; the CI run for the current main revision must pass before a newer release is considered validated. Azure acceptance tests still require a Windows device with the user's Cloudflare Access identity and locally imported private key; GitHub Actions cannot substitute for that interactive test.

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
