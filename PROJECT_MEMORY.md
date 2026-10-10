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
2. **DONE — Profiles + Google accounts + persistent sessions + account switcher** (§14-§20). Implemented via `WebView.CreationProperties = new CoreWebView2CreationProperties { ProfileName, IsInPrivateModeEnabled }` set BEFORE `EnsureCoreWebView2Async(sharedEnv)` — per-webview isolation under the ONE shared environment. In-window switcher: `Dictionary<profileId, tabs>`; switching only swaps visibility — other profiles' webviews stay ALIVE (sessions persist). Add profile → opens accounts.google.com in a real tab (login happens on the real Google page, never in LATCHI UI). Private window = MainWindow(profile, inPrivate) — no history recording.
3. **DONE — Extension manager (unpacked folders)** (§28-§34): ExtensionsWindow bound to the ACTIVE tab's `core.Profile` (extensions are per-profile!). `GetBrowserExtensionsAsync`, toggle via `EnableAsync(bool)` (no DisableAsync in the .NET wrapper), `RemoveAsync`, install via FolderBrowserDialog + `AddBrowserExtensionAsync(folder)`. Honest note in the window: no Chrome Web Store — that is a WebView2 platform limit, not ours.
4. **DONE — Downloads + bookmarks (+bar) + history** (§24-§27): DownloadManager wires `core.DownloadStarting` (Handled=true) and tracks `CoreWebView2DownloadOperation` live (Pause/Resume/Cancel are real op calls; ulong? → long cast for TotalBytesToReceive). BookmarkStore (global, toggle/star/Ctrl+D + bar Ctrl+Shift+B). HistoryStore per profile under `DataDir/Profiles/<id>/history.json` (cap 2000, consecutive-dedupe; private tabs never record).
5. **DONE — Settings + Gemini AI sidebar** (§51-§61): SettingsWindow (language/home/engine/AI toggle/model/key). AI sidebar = column in MainWindow (not a separate window); Gemini client is PURE (request build + response parse, unit-tested offline); key lives ONLY in DPAPI (SecureKeyStore, `System.Security.Cryptography.ProtectedData`, CurrentUser + static entropy); header `x-goog-api-key`; no key → honest guidance message, NEVER a fake reply. System prompt = §58 verbatim (asserted by test).
6. **DONE (honest subset) — Resources**: `MemoryUsageTargetLevel.Low` on minimize / `Normal` on restore (per tab, best-effort try/catch). **TrySuspendAsync is NOT reachable** from the WPF control (no controller exposure) — documented limitation, no fake claim. Zoom Ctrl+±0 via the WPF control's ZoomFactor (wraps controller). DevTools F12 = `OpenDevToolsWindow()` (NOT "OpenDevTools" — that's the WinRT name; the .NET wrapper name differs!). Multi-window Ctrl+N/Ctrl+Shift+N; App ShutdownMode=OnLastWindowClose.
7. **DONE — Installer + release** (§99-§113): `installer/latchi-browser.iss` (Inno, per-user, PrivilegesRequired=lowest, fixed AppId, NO [UninstallDelete] — user data survives). CI: portable artifact + choco innosetup → ISCC → Setup artifact; on tag: zip + SHA256SUMS + public GitHub Release.

## Testing
- xUnit on Core (any OS): UrlHelper matrix, SettingsStore (defaults/roundtrip/corrupt), SearchEngines, XamlBrushTests (Color-vs-Brush + missing StaticResource keys), **ProfileStore, BookmarkStore, HistoryStore, Gemini (request shape + response parse incl. empty candidates — a real bug the tests caught)**. 70/70 green.
- CI: GitHub Actions windows-latest — build + tests + self-contained win-x64 publish + Inno Setup installer + (on tag) release assets. UI itself: NOT TESTED until the user runs it (§108).

## Known limitations (current, v1.0.0 — all stated in README)
- Extensions: unpacked folders only (WebView2 platform limit). No Chrome Web Store.
- No TrySuspend on minimize (WPF control hides the controller) — MemoryUsageTargetLevel only.
- No find-in-page (no host API), no nested bookmark folders, dark theme only, no site-permissions UI, no PiP, no custom New Tab page, no Auto-Update.
- AI sees only what the user types/pastes (no page context yet).
- Removing a profile deletes OUR data (history.json etc.); WebView2's internal profile store stays under WebViewData until the app-data folder is cleaned (stated in the remove-confirm dialog).

