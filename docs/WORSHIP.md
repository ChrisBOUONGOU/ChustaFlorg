# Worship — advanced library tools

## Alternate arrangements
Each song can contain multiple arrangements. An arrangement stores an ordered list of section IDs, so the same chorus or verse can be repeated more than once. Create a named version, select a section in the song structure, and use Ajouter section active or Répéter refrain. Move or remove entries to build the order for that service. The arrangement's semitone offset is independent from the original song.

## Bulk import
Use Importer plusieurs chants and select one JSON, CSV or TXT file. JSON is a list of WorshipSong objects and is the best format for a full-fidelity backup/restore.
CSV uses one row per section. Required columns:
`Title,Author,Key,Tempo,SectionType,SectionNumber,Lyrics,Chords,Tags`
- SectionType uses enum values: Intro, Verse, PreChorus, Chorus, Bridge, Tag, Outro.
- Tags are separated with semicolons.
- Repeat title and author on each row belonging to the same song.
- Quote CSV cells containing commas, line breaks or double quotes; double embedded quotes.
TXT uses `# Song title` to start a song, optional `@author:`, `@key:` and `@tempo:` metadata lines, then sections such as `[Verse 1]`, `[Chorus]` and `[Bridge]`. Text imports contain lyrics; use CSV or JSON to import chord sheets too.
Import skips same-title/same-author candidates. The Détecter les doublons action reports exact normalized title matches and highly similar lyric text; it does not delete anything automatically.

## Chords and transposition
Enter chord sheets with bracketed chord tokens, for example:
`[G]Amazing [D]grace, how [C]sweet the [G]sound`
The editor's transpose buttons rewrite the selected section's chord sheet. Arrangement transpose buttons change that arrangement's semitone offset without modifying the original chord text. Enable Inclure les accords dans les diapositives to append the transposed chord sheet to generated slides.
Only bracketed chord tokens are transposed, so ordinary lyric words are not mistaken for chords. Supported notation includes roots, sharps/flats, common qualities, extensions and slash bass notes.

## Cross-device synchronization (WebDAV)
The app supports a user-provided WebDAV JSON file endpoint over HTTPS. Configure the same endpoint and credentials on each device, then select Synchroniser maintenant. A Nextcloud example endpoint is:
`https://cloud.example.com/remote.php/dav/files/USERNAME/ChustaFlorg/worship-library.json`
Create the parent folder in Nextcloud first, and use an app password where available. Enter the complete file URL, account name and app password in the app. The URL must use HTTPS. Credentials are stored with .NET MAUI SecureStorage, not in the song library JSON.
The sync operation downloads the remote library, merges songs by their stable IDs, chooses the newest ModifiedAtUtc version for matching IDs, and uploads the merged result. Distinct songs are retained. Run sync sequentially: do not trigger simultaneous syncs from multiple devices.
Current limitations: deletions are not synchronized (a deleted song on one device can reappear from another device); there is no user/team authentication layer beyond the WebDAV account, no server-side locking, and no conflict-resolution screen. Keep independent backups before using a shared library with important content.

## Copyright
The importer does not download or bundle lyrics. Import only content you own or are authorized to use. The app does not include copyrighted song catalogs.