# Worship module

## Goal

A volunteer-friendly song workflow combining fast lookup and local organization (Holyrics), structured sections and service preparation (ProPresenter), and the existing C#/.NET slide workflow (StageFlow). This is an independent implementation and does not reuse their source code, branding or proprietary assets.

## Current MVP

- Local JSON song catalog in the app's private data directory.
- Search across title, author, key, tags and lyric text.
- Metadata: title, author/composer, key, tags and optional BPM.
- Editable sections: introduction, numbered verses, pre-chorus, chorus, bridge, final/tag and outro.
- Automatic local save after edits.
- Convert non-empty sections into slides in their saved order and append them to the existing service playlist.
- Responsive desktop and compact layouts.

## User flow

1. Open **♫ Worship** from the studio toolbar.
2. Search for an existing song or create a new one.
3. Add sections in the intended performance order and edit each section's lyrics.
4. Choose **Ajouter le chant à la présentation** to append the sections to the service plan.
5. Review the slides in the studio and save the presentation project.

## Storage

The library is saved locally as `worship-library.json` in `FileSystem.AppDataDirectory`. It is separate from the presentation project file. No account, remote server or cloud sync is required by this MVP.

## Limitations / next steps

- No drag-and-drop reordering or alternate arrangements.
- No bulk import/export (TXT, CSV, OpenLyrics or ProPresenter formats).
- No duplicate detection, categories/groups or favorites.
- No chord charts, transpose tools or multilingual lyric variants.
- No cloud synchronization or team collaboration.
- No bundled lyrics: users must add content they own or are licensed to use.
- Add automated tests and verify behavior on Windows, Android and iOS.

## Product principles

- Search matches lyric words, not only titles.
- Editing autosaves locally.
- Song sections become presentation slides without retyping.
- Operators can prepare songs and work offline.
- Copyright status must be respected for lyrics and imported content.
