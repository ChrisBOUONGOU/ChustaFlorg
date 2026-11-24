using System.Collections.ObjectModel;
using ChustaFlorg.Models;
using ChustaFlorg.Services;

namespace ChustaFlorg;

public partial class MainPage : ContentPage
{
    private readonly ObservableCollection<Slide> _slides = new();
    private readonly ProjectService _projectService = new();
    private PresentationProject _project = new();
    private bool _loadingEditor;
    private bool _recoveryChecked;
    private bool? _compactLayout;
    private int _selectedIndex;
    private CancellationTokenSource? _autosaveCts;

    public MainPage()
    {
        InitializeComponent();
        foreach (var slide in _project.Slides)
            _slides.Add(slide);

        SlidesList.ItemsSource = _slides;
        SlidesList.SelectedItem = _slides.FirstOrDefault();
        UpdatePreview();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_recoveryChecked)
            return;

        _recoveryChecked = true;
        var recoveryPath = Path.Combine(FileSystem.AppDataDirectory, "autosave.chustaflorg");
        if (!File.Exists(recoveryPath))
            return;

        var recover = await DisplayAlert("Récupération", "Une sauvegarde automatique a été trouvée. Voulez-vous la restaurer ?", "Restaurer", "Ignorer");
        if (!recover)
            return;

        try
        {
            await using var stream = File.OpenRead(recoveryPath);
            _project = await _projectService.LoadAsync(stream);
            _slides.Clear();
            foreach (var slide in _project.Slides)
                _slides.Add(slide);
            if (_slides.Count == 0)
                _slides.Add(new Slide());
            SlidesList.SelectedItem = _slides[0];
            SetStatus("Sauvegarde récupérée automatiquement");
        }
        catch (Exception ex)
        {
            SetStatus("Récupération impossible : " + ex.Message);
        }
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        if (Workspace is null || width <= 0)
            return;

        var compact = width < 850;
        Workspace.HeightRequest = compact ? 960 : Math.Max(600, height - 90);
        if (_compactLayout == compact)
            return;
        _compactLayout = compact;

        Workspace.ColumnDefinitions.Clear();
        Workspace.RowDefinitions.Clear();

        if (compact)
        {
            Workspace.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Workspace.RowDefinitions.Add(new RowDefinition(new GridLength(210)));
            Workspace.RowDefinitions.Add(new RowDefinition(new GridLength(390)));
            Workspace.RowDefinitions.Add(new RowDefinition(new GridLength(330)));
            Grid.SetColumn(PlaylistPanel, 0);
            Grid.SetRow(PlaylistPanel, 0);
            Grid.SetColumn(PreviewPanel, 0);
            Grid.SetRow(PreviewPanel, 1);
            Grid.SetColumn(InspectorPanel, 0);
            Grid.SetRow(InspectorPanel, 2);
        }
        else
        {
            Workspace.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(220)));
            Workspace.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(280)));
            Grid.SetRow(PlaylistPanel, 0);
            Grid.SetColumn(PlaylistPanel, 0);
            Grid.SetRow(PreviewPanel, 0);
            Grid.SetColumn(PreviewPanel, 1);
            Grid.SetRow(InspectorPanel, 0);
            Grid.SetColumn(InspectorPanel, 2);
        }
    }

    private void OnSlideSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (SlidesList.SelectedItem is not Slide slide)
            return;

        _selectedIndex = _slides.IndexOf(slide);
        _loadingEditor = true;
        TitleEditor.Text = slide.Title;
        BodyEditor.Text = slide.Body;
        _loadingEditor = false;
        UpdatePreview();
    }

    private void OnEditorChanged(object? sender, EventArgs e)
    {
        if (_loadingEditor || SlidesList.SelectedItem is not Slide slide)
            return;

        slide.Title = TitleEditor.Text ?? string.Empty;
        slide.Body = BodyEditor.Text ?? string.Empty;
        UpdatePreview();
        SlidesList.ItemsSource = null;
        SlidesList.ItemsSource = _slides;
        SlidesList.SelectedItem = slide;
        ScheduleAutosave();
    }

    private void UpdatePreview()
    {
        if (_slides.Count == 0)
        {
            PreviewTitle.Text = "Aucune diapositive";
            PreviewBody.Text = "Ajoutez une diapositive pour commencer.";
            return;
        }

        var slide = SlidesList.SelectedItem as Slide ?? _slides[Math.Clamp(_selectedIndex, 0, _slides.Count - 1)];
        PreviewTitle.Text = slide.Title;
        PreviewBody.Text = slide.Body;
    }

    private void OnAddSlideClicked(object? sender, EventArgs e)
    {
        var slide = new Slide
        {
            Title = $"Diapositive {_slides.Count + 1}",
            Body = "Saisissez votre texte ici"
        };
        _slides.Add(slide);
        SlidesList.SelectedItem = slide;
        SetStatus("Diapositive ajoutée");
        ScheduleAutosave();
    }

    private async void OnDeleteSlideClicked(object? sender, EventArgs e)
    {
        if (SlidesList.SelectedItem is not Slide slide)
            return;

        var confirmed = await DisplayAlert("Supprimer", "Supprimer cette diapositive ?", "Supprimer", "Annuler");
        if (!confirmed)
            return;

        var index = _slides.IndexOf(slide);
        _slides.Remove(slide);
        if (_slides.Count == 0)
            OnAddSlideClicked(this, EventArgs.Empty);
        else
            SlidesList.SelectedItem = _slides[Math.Clamp(index, 0, _slides.Count - 1)];

        SetStatus("Diapositive supprimée");
        ScheduleAutosave();
    }

    private void OnPreviousClicked(object? sender, EventArgs e) => SelectSlide(_selectedIndex - 1);
    private void OnNextClicked(object? sender, EventArgs e) => SelectSlide(_selectedIndex + 1);

    private void SelectSlide(int index)
    {
        if (_slides.Count == 0)
            return;

        var bounded = Math.Clamp(index, 0, _slides.Count - 1);
        SlidesList.SelectedItem = _slides[bounded];
        _selectedIndex = bounded;
        UpdatePreview();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var name = await DisplayPromptAsync("Enregistrer le projet", "Nom du projet", initialValue: _project.Name);
            if (string.IsNullOrWhiteSpace(name))
                return;

            _project.Name = name.Trim();
            _project.Slides = _slides.ToList();
            var safeName = string.Concat(_project.Name.Split(Path.GetInvalidFileNameChars()));
            var path = Path.Combine(FileSystem.AppDataDirectory, safeName + ".chustaflorg");
            await _projectService.SaveAsync(_project, path);
            SetStatus("Enregistré localement · " + Path.GetFileName(path));
            ScheduleAutosave();
        }
        catch (Exception ex)
        {
            SetStatus("Erreur d'enregistrement : " + ex.Message);
        }
    }

    private async void OnOpenClicked(object? sender, EventArgs e)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Ouvrir un projet ChustaFlorg",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.WinUI] = new[] { ".chustaflorg", ".json" },
                    [DevicePlatform.Android] = new[] { "application/json", "application/octet-stream" },
                    [DevicePlatform.iOS] = new[] { "public.json", "public.data" },
                    [DevicePlatform.MacCatalyst] = new[] { "public.json", "public.data" }
                })
            });

            if (file is null)
                return;

            await using var stream = await file.OpenReadAsync();
            _project = await _projectService.LoadAsync(stream);
            _slides.Clear();
            foreach (var slide in _project.Slides)
                _slides.Add(slide);

            if (_slides.Count == 0)
                _slides.Add(new Slide());

            SlidesList.SelectedItem = _slides[0];
            _selectedIndex = 0;
            SetStatus("Projet ouvert · " + file.FileName);
        }
        catch (Exception ex)
        {
            SetStatus("Impossible d'ouvrir : " + ex.Message);
        }
    }

    private async void OnWorshipClicked(object? sender, EventArgs e)
    {
        var page = new WorshipPage(slides =>
        {
            foreach (var slide in slides)
                _slides.Add(slide);

            if (slides.Count > 0)
            {
                SlidesList.SelectedItem = slides[0];
                _selectedIndex = _slides.IndexOf(slides[0]);
                UpdatePreview();
            }

            SetStatus($"{slides.Count} diapositive(s) de chant ajoutée(s) au plan du culte.");
            ScheduleAutosave();
        });

        await Navigation.PushModalAsync(new NavigationPage(page));
    }

    private async void OnLiveClicked(object? sender, EventArgs e)
    {
        if (_slides.Count == 0)
        {
            SetStatus("Ajoutez une diapositive avant de démarrer.");
            return;
        }

        var page = new LivePage(_slides.ToList(), _selectedIndex);
#if WINDOWS
        Application.Current?.OpenWindow(new Window(page)
        {
            Title = "ChustaFlorg · Présentation en direct"
        });
        SetStatus("Fenêtre de présentation ouverte");
#else
        await Navigation.PushModalAsync(new NavigationPage(page));
        SetStatus("Présentation ouverte");
#endif
    }

    private void ScheduleAutosave()
    {
        _autosaveCts?.Cancel();
        _autosaveCts?.Dispose();
        var cancellation = new CancellationTokenSource();
        _autosaveCts = cancellation;
        _ = SaveAfterDelayAsync(cancellation);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource cancellation)
    {
        try
        {
            await Task.Delay(900, cancellation.Token);
            _project.Slides = _slides.ToList();
            var path = Path.Combine(FileSystem.AppDataDirectory, "autosave.chustaflorg");
            await _projectService.SaveAsync(_project, path, cancellation.Token);
            SetStatus("Sauvegarde automatique effectuée");
        }
        catch (OperationCanceledException)
        {
            // A newer edit has scheduled the next autosave.
        }
        catch (Exception ex)
        {
            SetStatus("Sauvegarde automatique impossible : " + ex.Message);
        }
    }

    private void SetStatus(string message) => StatusLabel.Text = message;
}