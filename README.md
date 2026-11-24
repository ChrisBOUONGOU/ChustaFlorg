# ChustaFlorg

**One service. One workflow. Every screen in sync.**

ChustaFlorg is a church presentation and live-service application built with **C# and .NET MAUI**. Its goal is to combine the most useful workflows found in ProPresenter, Holyrics and StageFlow while reducing friction for volunteers and improving reliability on service day.

## Product direction

- **ProPresenter-inspired:** service flow, slide editing, operator preview and audience presentation.
- **Holyrics-inspired:** song and scripture workflows, fast search, accessible operation and remote-control direction.
- **StageFlow foundation:** the developer's existing C#/.NET presentation experience, local project workflow, media, lyrics, Bible panel, themes and live presentation concepts.
- **ChustaFlorg differentiators:** recovery-first design, offline-first libraries, volunteer-friendly guided mode, clear diagnostics and a coherent mobile/desktop experience.

ChustaFlorg is an independent project. It is not affiliated with or endorsed by Renewed Vision, Holyrics or their developers, and does not copy their branding, proprietary code or assets.

## Current implementation

The MAUI application currently provides an initial presentation workspace with:
- A slide playlist with add and delete actions.
- Slide title and text editing with a live preview.
- Local project save and open using a versioned JSON model.
- Debounced automatic local save and a startup recovery prompt.
- A live presentation view with previous/next navigation (separate window on Windows; full-screen modal view on mobile).
- A responsive workspace that rearranges and scrolls the playlist, preview and inspector on narrower screens.\n- A Bible module using the same Free.Bible JSON API endpoints as StageFlow, with French, English, Spanish and Portuguese translations, book/chapter browsing, in-chapter text search, verse favorites, personal notes and local offline cache for previously downloaded indexes and chapters.\n- One-click insertion of a selected Bible verse or an entire loaded chapter into the presentation playlist.
- A Worship song library with local JSON persistence, search across titles/authors/tags/lyrics, metadata (key and BPM), and editable sections (intro, verses, pre-chorus, chorus, bridge, final and outro).
- One-click conversion of song sections into slides, preserving their sequence and appending them to the service playlist.

This is an early MVP foundation, not a finished replacement for the three products. WebDAV sync requires an existing HTTPS WebDAV endpoint (for example, a Nextcloud app-password URL) configured on each device. Synchronization currently merges by song ID and last-modified timestamp; deletions are not propagated and simultaneous syncs from multiple devices should be avoided. Selecting a specific physical output display, stage/audience screen separation and production-grade media playback remain future work.

## Roadmap

1. **Reliability:** project backups, integrity checks, crash diagnostics and recovery testing.
2. **Worship library (first MVP delivered):** local song catalog, metadata, verse/chorus/bridge structure, tags, lyrics search and add-to-service flow. Advanced Worship tools: alternate arrangements, import/export, duplicate detection, chord sheets, transposition and WebDAV sync are now in the MVP.
3. **Bible module:** translations with appropriate distribution rights, offline availability, bookmarks and notes.
4. **Media engine:** image/video/audio playback, backgrounds, transition controls and missing-file repair.
5. **Live production:** explicit display selection, audience/stage outputs, next-slide confidence monitor and blackout.
6. **Remote control:** secure local-network pairing for phones and tablets, with connection status and fallback controls.
7. **Interoperability:** import/export for documented and legally supported formats.
8. **Volunteer mode:** guided setup, simple mode, keyboard shortcuts and contextual help.

See [Worship module guide](docs/WORSHIP.md) for import formats, chord notation and WebDAV setup. See [Bible module guide](docs/BIBLE.md) for the API source, translations, offline cache and local notes/favorites.

## Technology

- .NET 10
- .NET MAUI
- C#
- XAML
- JSON project persistence (initial implementation)

## Build

Install the .NET 10 SDK and the workloads required by your target platform.

Windows:
```powershell
dotnet workload restore
dotnet build .\ChustaFlorg.slnx -f net10.0-windows10.0.19041.0
dotnet run --project .\ChustaFlorg\ChustaFlorg.csproj -f net10.0-windows10.0.19041.0
```

Android:
```powershell
dotnet build .\ChustaFlorg\ChustaFlorg.csproj -f net10.0-android
```

iOS builds require a compatible Mac build host and the Apple toolchain.

## Licensing

A project license and third-party content policy will be selected before public distribution. Bible translations, song lyrics and media must only be bundled or imported where their licenses permit it.
