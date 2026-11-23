namespace ChustaFlorg.Models;

public sealed class PresentationProject
{
    public int SchemaVersion { get; set; } = 1;
    public string Name { get; set; } = "Nouveau culte";
    public DateTime ModifiedAtUtc { get; set; } = DateTime.UtcNow;
    public List<Slide> Slides { get; set; } =
    [
        new() { Title = "Bienvenue", Body = "Bienvenue dans ChustaFlorg\nVotre présentation commence ici" },
        new() { Title = "Annonce", Body = "Ajoutez vos annonces et vos informations" },
        new() { Title = "Lecture biblique", Body = "Ajoutez un passage biblique" }
    ];
}