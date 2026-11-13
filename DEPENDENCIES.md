# Third-party components

Hattrick SSH is an original application. It uses the following third-party components:

| Component | Purpose | License |
|---|---|---|
| Microsoft .NET 8 / WPF | Windows desktop runtime and UI | MIT |
| Microsoft WebView2 SDK | Embedded terminal host | Microsoft license terms |
| xterm.js | ANSI/VT terminal renderer | MIT |
| xterm-addon-fit | Fit terminal to available space | MIT |
| Windows OpenSSH Client | SSH transport and PTY client | BSD-style OpenSSH license |
| cloudflared | Cloudflare Access SSH proxy | Apache-2.0 |
| Inno Setup | Windows setup packaging | Inno Setup license |

The workflow downloads xterm.js packages from npm and cloudflared from the official Cloudflare GitHub release. Their source projects and licenses remain with their respective maintainers. No source code or branding from WindTerm or Tabby is copied.
