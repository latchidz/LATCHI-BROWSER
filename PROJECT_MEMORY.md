# PROJECT MEMORY — LATCHI BROWSER

## Project
- **What**: a REAL lightweight Windows desktop browser (not a web-app shell): tabs, multi Google profiles with isolated sessions, browser extensions, downloads, bookmarks, history, settings, Gemini AI assistant.
- **Stack**: WPF + .NET 8 (net8.0-windows, x64, WinExe) + Microsoft Edge WebView2 SDK **1.0.4258.31** (same proven version as LATCHI xCLOUD).
- **Spec**: the full 113-section specification (priorities 1-19, §107 "IF A BUTTON EXISTS, IT MUST WORK", §108 "NO FALSE PASS").

## Purpose
One shared engine, real websites, real Google sessions, real tabs/extensions/video/fullscreen — with LOW resource usage on weak hardware (Win10/11, 4GB RAM, iGPU, HDD).

## Architecture (and WHY)
- **BrowserEngine (static)**: ONE lazy `CoreWebView2Environment` for the entire app — every tab/window/profile is created FROM it, so all webviews share one browser process (§3/§4/§82; Microsoft perf guidance). Options: `AreBrowserExtensionsEnabled=true` from day one (so the extensions round never needs a data reset), Language=ar|en. GPU acceleration NEVER disabled (§67).
- **BrowserTab**: one tab = one `WebView2` control from the shared env, created lazily (never before the tab exists). Model carries §12 fields: TabId, ProfileId ("default" until profiles round), Url/Address, Title, FaviconUrl, IsPinned, IsActive, LastUsedUtc. Events: PopupRequested (target=_blank → NEW TAB, real browser behavior §47), ContentFullscreenChanged (§8 video fullscreen ≠ F11).
- **MainWindow**: custom chrome — the tab strip IS the WindowChrome caption (CaptionHeight=40); every interactive element opts in with `WindowChrome.IsHitTestVisibleInChrome=True` (mouse hover+click MUST work everywhere — standing user rule). Real window buttons (min/max/restore/close §7). Maximize glyph toggles via StateChanged.
- **Fullscreen (§8) — three distinct cases**: F11 (app fullscreen: chrome hidden + borderless via WindowChrome swap + maximize), content fullscreen (page/video driven via ContainsFullScreenChanged — borderless, restores when page exits), and plain window maximize. `ExitFullscreenIfIdle()` restores only when BOTH flags are off.
- **UrlHelper (Core, pure)**: omnibox resolution — empty→home, explicit scheme→as-is, localhost/IP→http, domain-like→https, everything else→search (Google/Bing/DuckDuckGo, §23). Never throws.
- **Startup (§87/§88)**: window shows FIRST; then settings → runtime check → engine → first tab only.
- **Error UX (§5/§75)**: runtime missing or engine failure → full-screen fatal panel with the official Evergreen download link, never a dead window. WebView2 built-in error pages stay ENABLED (a browser must show "can't reach this page", not a custom fake).
- **Data layout (§80/§81)**: `%LOCALAPPDATA%\LATCHI\Browser` — settings.json, Logs\app.log, WebViewData (WebView2 user data folder; profiles will live under it as WebView2 Profiles\<name>).
- **Logger (§49/§73)**: hosts only, never full URLs, never secrets. Logging never throws.

## Critical rules (violating any of these broke real apps before)
1. **NEVER block on WebView2 async APIs** (`GetAwaiter().GetResult()`/`.Wait()` on UI thread = deadlock — the WebView2 completion needs the UI pump). Always `await`. (Root cause of a real freeze bug in LATCHI xCLOUD 1.1/1.2.)
2. **Never a Color resource on a Brush property** — XamlBrushTests guards this class forever.
3. **No fake features (§107)**: buttons exist only when their function works. Roadmap features have NO button yet.
4. **No false PASS (§108)**: report PASS/FAIL/NOT TESTED honestly; UI behavior is NOT TESTED until the user runs it on Windows.
5. **API keys never in source/git/logs/README/memory (§56/§57)** — the key the user once pasted in chat is considered COMPROMISED and must never be written anywhere; the AI round reads it from DPAPI/Credential Manager only, entered by the user in Settings.
6. **One shared environment — never per-tab environments**; no WebView without a tab.
7. **GPU acceleration always on**; no heavy animations; DPI is default WPF system-aware (§76).

## Extensions (round 3 plan, §28-§34 — honest constraints)
WebView2 officially supports browser extensions: `AreBrowserExtensionsEnabled=true` + `CoreWebView2Profile.AddBrowserExtensionAsync(unpackedFolderPath)` (folder with manifest.json; NOT Chrome-Web-Store one-click, NOT direct CRX). GetBrowserExtensionsAsync lists; Enable/Remove work; extensions persist per profile. We build our OWN extension manager UI and show WebView2's real limits to the user — no hacks, no promises the platform can't keep.

## Rounds roadmap (spec priorities)
1. **DONE — Engine, WebView2 init, navigation, tabs, window/fullscreen** (+ §7 window controls, §11 shortcuts T/W/Shift+T/Tab, §22-23 engines, §5/§75 error UX, AR/EN).
2. Profiles + Google accounts + persistent sessions + account switcher (§14-§20; ProfileName via CoreWebView2CreationProperties or EnsureCoreWebView2Async overload).
3. Extension manager (unpacked folders) + video/audio validation (§28-34, §9-10).
4. Downloads + bookmarks (+bar) + history (§24-27).
5. Settings pages + Gemini AI sidebar (§51-61; DPAPI key storage; latest stable Flash model; NO key in code).
6. Performance (TrySuspendAsync, MemoryUsageTargetLevel, tab suspension §66) + Premium UI (§83-86; user's 3D icon is assets/appicon.ico).
7. Installer (Inno Setup, per-user, no admin) + release testing (§99-113).

## Testing
- xUnit on Core (any OS): UrlHelper matrix, SettingsStore (defaults/roundtrip/corrupt), SearchEngines, XamlBrushTests (Color-vs-Brush + missing StaticResource keys).
- CI: GitHub Actions windows-latest — build + tests + self-contained win-x64 publish artifact. UI itself: NOT TESTED until user runs it (§108) — a Windows smoke runner is planned for a later round.

## Known limitations (current)
- Profiles all use the default WebView2 profile until round 2 (ProfileId placeholder in tab model).
- Downloads use WebView2's default UI until round 4.
- Tab pin/move/duplicate planned with the tab-polish round; Ctrl+N multi-window planned with the window-manager round.
- Context menu is the WebView2 default (English) — localized custom menu comes with the UI round.
