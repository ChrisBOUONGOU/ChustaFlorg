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
- A separate live presentation window with previous/next navigation.
- A responsive layout that rearranges the playlist, preview and inspector on narrower screens.

This is an early MVP foundation, not a finished replacement for the three products. The current live window is a separate application window; selecting a specific physical output display and production-grade media playback remain future work.

## Roadmap

1. **Reliability:** autosave, crash recovery, project backups, integrity checks and diagnostics.
2. **Worship library:** songs, verse/chorus structure, tags, search and duplicate detection.
3. **Bible module:** translations with appropriate distribution rights, offline availability, bookmarks and notes.
4. **Media engine:** image/video/audio playback, backgrounds, transition controls and missing-file repair.
5. **Live production:** explicit display selection, audience/stage outputs, next-slide confidence monitor and blackout.
6. **Remote control:** secure local-network pairing for phones and tablets, with connection status and fallback controls.
7. **Interoperability:** import/export for documented and legally supported formats.
8. **Volunteer mode:** guided setup, simple mode, keyboard shortcuts and contextual help.

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
