namespace ChustaFlorg.Models;

public sealed class WorshipSong
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Nouveau chant";
    public string Author { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int Tempo { get; set; }
    public string Language { get; set; } = "Français";
    public List<string> Tags { get; set; } = [];
    public List<SongSection> Sections { get; set; } = [];
    public List<SongArrangement> Arrangements { get; set; } = [];
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SongSection
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public SongSectionType Type { get; set; } = SongSectionType.Verse;
    public int Number { get; set; } = 1;
    public string Label { get; set; } = string.Empty;
    public string Lyrics { get; set; } = string.Empty;
    public string ChordLyrics { get; set; } = string.Empty;

    public string DisplayName => Type switch
    {
        SongSectionType.Intro => "Introduction",
        SongSectionType.Verse => $"Couplet {Number}",
        SongSectionType.PreChorus => "Pré-refrain",
        SongSectionType.Chorus => "Refrain",
        SongSectionType.Bridge => "Pont",
        SongSectionType.Tag => "Final",
        SongSectionType.Outro => "Conclusion",
        _ => "Section"
    };
}

public sealed class SongArrangement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Version originale";
    public List<Guid> SectionIds { get; set; } = [];
    public int SemitoneOffset { get; set; }
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
}

public enum SongSectionType { Intro, Verse, PreChorus, Chorus, Bridge, Tag, Outro }
