# ChustaFlorg

**One service. One workflow. Every screen in sync.**

ChustaFlorg is a new, independent church presentation and live-production application built with C#, .NET 10 and Avalonia UI. It brings together the practical strengths of ProPresenter, Holyrics and StageFlow while prioritizing reliability, a clear workflow for volunteers, offline-first operation and safe live presentation.

## Product principles

- **Reliable on service day:** autosave, recoverable projects, safe updates and a clear output status.
- **Easy for volunteers:** guided first-run setup, simple mode, keyboard shortcuts and contextual help.
- **Offline-first:** local projects, songs, media and Bible content remain usable without an internet connection when the relevant content is installed.
- **One coherent workflow:** plan a service, arrange songs/scripture/media/slides, preview the next item and present it live.
- **Multi-screen clarity:** separate audience output and operator view, with explicit display selection and a confidence preview.
- **Portable content:** documented project format and import/export tools to reduce lock-in.

## Synthesis

| Product | Strengths to build on |
|---|---|
| ProPresenter | Service playlists, rich slide design, layers, stage/audience outputs and production workflow |
| Holyrics | Song and Bible workflows, accessible operation, remote control and church-focused tools |
| StageFlow | C#/.NET + Avalonia foundation, local projects, lyrics, Bible panel, media library, themes and live presentation |

ChustaFlorg is not affiliated with or endorsed by Renewed Vision, Holyrics or their developers. It will not copy their branding, proprietary code or assets.

## MVP roadmap

1. Project and service editor: create, reorder, edit and save slides.
2. Live output: audience window, operator preview, next/previous, black screen and clear output state.
3. Content library: songs, scripture references, images, videos and reusable slide templates.
4. Reliability: autosave, crash recovery, missing-media warnings, backup/restore and diagnostics.
5. Church workflow: service plans, volunteer-friendly mode, keyboard shortcuts and optional remote control.
6. Interoperability: import/export common formats where technically and legally possible.

## Current implementation

The first repository milestone establishes the Avalonia desktop application and its project model. Features in the roadmap are not considered complete until implemented and tested.

## Build

Requires the .NET 10 SDK.

```bash
dotnet restore
dotnet build
dotnet run
```

## License

A license will be selected before public distribution. Until then, all rights are reserved.
