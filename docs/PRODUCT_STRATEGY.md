# Product strategy and user-problem map

This document turns public user feedback and the existing StageFlow codebase into concrete ChustaFlorg design requirements. Individual reviews are anecdotal; the goal is to test these hypotheses with church volunteers before claiming that a product has solved them.

## What the three products contribute

- **ProPresenter:** mature service sequencing, slide composition, production-oriented workflows, stage/audience presentation and rich media.
- **Holyrics:** accessible church-first workflows for lyrics and scripture, plus mobile/remote usage.
- **StageFlow:** an existing C#/.NET + Avalonia project with local projects, slide editing, lyrics, Bible models/services, media library, themes and presentation output.

## Pain points to validate

| User problem | Product response | Acceptance check |
|---|---|---|
| A crash or freeze during a service | Debounced autosave, atomic file replacement, recovery prompt and backups | Reopen after forced termination and recover the last autosaved version |
| Volunteers struggle with complex interfaces | Guided setup, simple mode, clear labels and contextual hints | A first-time operator can prepare and run a short service without documentation |
| Large media libraries slow down preparation | Background indexing, thumbnails, tags and fast local search | Search remains responsive with a representative church media library |
| Remote control disconnects | Pairing status, reconnect, visible network diagnostics and local fallback controls | Operator can continue from the main computer after remote disconnect |
| Bible or lyrics unavailable without internet | Downloadable licensed content and explicit offline indicators | Installed content remains searchable with Wi-Fi disabled |
| Confusion between preview and public output | Distinct preview/public indicators, next-slide preview and output health | Operator can identify exactly what the audience sees |
| Updates introduce service-day risk | Versioned project schema, migration checks, backups and rollback guidance | Older project opens safely or provides a clear recovery path |
| Importing/exporting content is difficult | Documented interchange formats and import validation | Invalid files are rejected with a useful explanation, not a crash |

## MVP already in the repository

- Responsive .NET MAUI presentation workspace.
- Add/delete slides and edit title/body.
- Preview of the selected slide.
- Local JSON project save/open.
- Debounced autosave and a startup recovery prompt.
- Separate live window with previous/next navigation.

## Next implementation milestones

1. **Harden the core:** automated tests for serialization, atomic saves, recovery and malformed files.
2. **Improve live output:** choose the physical display, fullscreen audience mode, operator preview, blackout and output status.
3. **Add worship content:** song sections (verse, chorus, bridge), tags, search, scripture references and favorites.
4. **Add media playback:** image/video/audio, preload, codecs, missing-media detection and graceful fallback.
5. **Add remote control:** secure local-network pairing, QR setup, reconnect and permissions.
6. **Add migration/import:** evaluate formats and document exactly which fields are preserved.

## Product guardrails

- Never bundle copyrighted lyrics, Bible translations, fonts or media without the required rights.
- Never claim a feature is offline-capable until its content and dependencies are installed locally.
- Never silently switch the public output when an operator is editing.
- Never perform an update immediately before a scheduled service without a backup and a clear confirmation.
- Keep a keyboard-only path for the main live controls.
