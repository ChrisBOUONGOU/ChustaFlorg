namespace ChustaFlorg.Models;

public sealed class BibleTranslation
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Language { get; set; } = string.Empty;
    public override string ToString() => $"{Name} ({ShortName})";
}

public sealed class BibleBook
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public int ChapterCount { get; set; }
    public override string ToString() => Name;
}

public sealed class BibleVerse
{
    public int Verse { get; set; }
    public string Text { get; set; } = string.Empty;
    public string BookName { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public string Reference => $"{BookName} {Chapter}:{Verse}";
    public string DisplayText => $"{Verse}. {Text}";
}

public sealed class BibleChapter
{
    public string BookId { get; set; } = string.Empty;
    public string BookName { get; set; } = string.Empty;
    public int Number { get; set; }
    public List<BibleVerse> Verses { get; set; } = [];
    public string Reference => $"{BookName} {Number}";
}

public sealed class BibleBookmark
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TranslationId { get; set; } = string.Empty;
    public string TranslationName { get; set; } = string.Empty;
    public string BookId { get; set; } = string.Empty;
    public string BookName { get; set; } = string.Empty;
    public int Chapter { get; set; }
    public int Verse { get; set; }
    public string VerseText { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    public string Reference => $"{BookName} {Chapter}:{Verse}";
}