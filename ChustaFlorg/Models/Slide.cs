namespace ChustaFlorg.Models;

public sealed class Slide
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = "Nouvelle diapositive";
    public string Body { get; set; } = "Votre texte ici";
    public string? BackgroundPath { get; set; }
    public string BackgroundColor { get; set; } = "#111827";
    public string TextColor { get; set; } = "#FFFFFF";
    public int DurationSeconds { get; set; }
    public string? BackgroundAudioPath { get; set; }
    public string Transition { get; set; } = "Fondu";
}