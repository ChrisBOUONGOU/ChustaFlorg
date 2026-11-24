# Bible module

ChustaFlorg reuses the same Bible data provider and public JSON URL pattern used by the developer's StageFlow project.

## Data provider

- Provider: [Free.Bible](https://free.bible/)
- Base URL: `https://free.bible/bible`
- No API key is configured in StageFlow or in this integration.
- Translation identifiers currently offered:
  - `segond` — Louis Segond (French)
  - `web` — World English Bible (English)
  - `rv1909` — Reina-Valera 1909 (Spanish)
  - `pt` — Bíblia Portuguesa Mundial (Portuguese)

The integration uses the same endpoint patterns as StageFlow:

- Translation index: `{base}/{translationId}/index.json`
- Chapter: `{base}/{translationId}/{bookId}/{chapterNumber}.json`

The provider implementation from StageFlow is at [Services/FreeBibleProvider.cs](https://github.com/ChrisBOUONGOU/StageFlow/blob/master/Services/FreeBibleProvider.cs).

## Features

- Choose a translation, book and chapter.
- Read the verses of a chapter.
- Search for a word or phrase within the currently loaded chapter.
- Select a verse and add it to the service presentation.
- Add all verses from a loaded chapter to the presentation (one slide per verse).
- Save verse favorites and personal notes locally on the device.

## Offline behavior

Translation indexes and chapter JSON files are cached under the app's local application-data directory. A cached response is reused for up to 30 days; if a network request fails, an existing cached response is used as a fallback.

For offline use, open each required translation and chapter at least once while connected to the Internet. The Bible index must also have been downloaded once. A chapter that has never been opened online cannot be displayed offline.

Favorites and notes are stored locally in `bible-bookmarks.json`. They are not synchronized between devices in this version.

## Current limitations

- Search is limited to the currently selected chapter; it is not yet a global search across the complete Bible.
- The module currently offers the four translation IDs listed above.
- The app does not redistribute a bundled Bible database; content is retrieved from Free.Bible at runtime and cached on the device.
- Before public distribution, verify the current terms and distribution rights for each Bible translation and the provider's content.

