using System.Collections.ObjectModel;
using System.IO;
using ChustaFlorg.Models;
using ChustaFlorg.Services;

namespace ChustaFlorg;

public partial class MainPage : ContentPage
{
    private readonly ObservableCollection<Slide> _slides = new();
    private readonly ObservableCollection<MediaLibraryItem> _mediaAssets = new();
    private MediaLibraryItem? _selectedMedia;
    private sealed class MediaLibraryItem
    {
        public string Name { get; init; } = string.Empty;
        public string Path { get; init; } = string.Empty;
        public string Kind { get; init; } = string.Empty;
        public string Icon => Kind switch { "Image" => "▧", "Video" => "▶", "Audio" => "♫", _ => "•" };
    }
    private readonly ProjectService _projectService = new();
    private PresentationProject _project = new();
    private bool _loadingEditor;
    private bool _recoveryChecked;
    private bool? _compactLayout;
    private int _selectedIndex;
    private CancellationTokenSource? _autosaveCts;
    private LiveProductionSession? _activeSession;
    private RemoteControlService? _remoteServer;
    private string? _remoteBaseUrl;
    private string? _remotePin;
    private readonly HttpClient _remoteHttp = new() { Timeout = TimeSpan.FromSeconds(3) };

    public MainPage()
    {
        InitializeComponent();
        foreach (var slide in _project.Slides)
            _slides.Add(slide);

        SlidesList.ItemsSource = _slides;
        MediaAssetsView.ItemsSource = _mediaAssets;
        BibleContentHost.Content = new BiblePage(AddSlidesToPlaylist);
        WorshipContentHost.Content = new WorshipPage(AddSlidesToPlaylist);
        LoadMediaLibrary();
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
        RightToolsPanel.ColumnDefinitions.Clear();
        RightToolsPanel.RowDefinitions.Clear();
        ToolBarLayout.ColumnDefinitions.Clear();
        ToolBarLayout.RowDefinitions.Clear();

        if (compact)
        {
            Workspace.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Workspace.RowDefinitions.Add(new RowDefinition(new GridLength(210)));
            Workspace.RowDefinitions.Add(new RowDefinition(new GridLength(390)));
            Workspace.RowDefinitions.Add(new RowDefinition(new GridLength(390)));
            Grid.SetColumn(PlaylistPanel, 0);
            Grid.SetRow(PlaylistPanel, 0);
            Grid.SetColumn(PreviewPanel, 0);
            Grid.SetRow(PreviewPanel, 1);
            Grid.SetColumn(RightToolsPanel, 0);
            Grid.SetRow(RightToolsPanel, 2);

            RightToolsPanel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            RightToolsPanel.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            RightToolsPanel.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetRow(ToolBarBorder, 0);
            Grid.SetColumn(ToolBarBorder, 0);
            Grid.SetRow(RightPanelHost, 1);
            Grid.SetColumn(RightPanelHost, 0);
            RightPanelHost.Margin = new Thickness(0);
            ToolBarLayout.ColumnSpacing = 8;
            ToolBarLayout.RowSpacing = 6;
            for (var column = 0; column < 4; column++)
                ToolBarLayout.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            for (var row = 0; row < 2; row++)
                ToolBarLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var toolButtons = new[] { ToolPropertiesButton, ToolMediaButton, ToolThemesButton, ToolLyricsButton, ToolBibleButton, ToolAppearanceButton, ToolRemoteButton };
            for (var i = 0; i < toolButtons.Length; i++)
            {
                Grid.SetRow(toolButtons[i], i / 4);
                Grid.SetColumn(toolButtons[i], i % 4);
                toolButtons[i].WidthRequest = 42;
                toolButtons[i].HeightRequest = 42;
            }
        }
        else
        {
            Workspace.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(220)));
            Workspace.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            Workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(340)));
            Grid.SetRow(PlaylistPanel, 0);
            Grid.SetColumn(PlaylistPanel, 0);
            Grid.SetRow(PreviewPanel, 0);
            Grid.SetColumn(PreviewPanel, 1);
            Grid.SetRow(RightToolsPanel, 0);
            Grid.SetColumn(RightToolsPanel, 2);

            RightToolsPanel.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(48)));
            RightToolsPanel.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            RightToolsPanel.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            Grid.SetRow(ToolBarBorder, 0);
            Grid.SetColumn(ToolBarBorder, 0);
            Grid.SetRow(RightPanelHost, 0);
            Grid.SetColumn(RightPanelHost, 1);
            RightPanelHost.Margin = new Thickness(9, 0, 0, 0);
            ToolBarLayout.ColumnSpacing = 0;
            ToolBarLayout.RowSpacing = 7;
            ToolBarLayout.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(38)));
            for (var row = 0; row < 7; row++)
                ToolBarLayout.RowDefinitions.Add(new RowDefinition(GridLength.Auto));

            var toolButtons = new[] { ToolPropertiesButton, ToolMediaButton, ToolThemesButton, ToolLyricsButton, ToolBibleButton, ToolAppearanceButton, ToolRemoteButton };
            for (var i = 0; i < toolButtons.Length; i++)
            {
                Grid.SetRow(toolButtons[i], i);
                Grid.SetColumn(toolButtons[i], 0);
                toolButtons[i].WidthRequest = 38;
                toolButtons[i].HeightRequest = 42;
            }
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
        TransitionPicker.SelectedItem = slide.Transition;
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
        PreviewBackground.Source = !string.IsNullOrWhiteSpace(slide.BackgroundPath) && File.Exists(slide.BackgroundPath) ? ImageSource.FromFile(slide.BackgroundPath) : null;
        PreviewBackground.IsVisible = PreviewBackground.Source is not null;
        PreviewPanel.BackgroundColor = Color.FromArgb(slide.BackgroundColor);
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
        _activeSession?.GoTo(bounded);
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


    private void ShowRightPanel(string panel)
    {
        InspectorPanel.IsVisible = panel == "Properties";
        MediaPanel.IsVisible = panel == "Media";
        ThemePanel.IsVisible = panel == "Themes";
        ApplicationThemePanel.IsVisible = panel == "ApplicationTheme";
        LyricsPanel.IsVisible = panel == "Lyrics";
        BiblePanel.IsVisible = panel == "Bible";
        RemotePanel.IsVisible = panel == "Remote";
    }

    private void OnToolPropertiesClicked(object? sender, EventArgs e) => ShowRightPanel("Properties");
    private void OnToolMediaClicked(object? sender, EventArgs e) => ShowRightPanel("Media");
    private void OnToolThemesClicked(object? sender, EventArgs e) => ShowRightPanel("Themes");
    private void OnToolApplicationThemeClicked(object? sender, EventArgs e) => ShowRightPanel("ApplicationTheme");
    private void OnToolLyricsClicked(object? sender, EventArgs e) => ShowRightPanel("Lyrics");
    private void OnToolBibleClicked(object? sender, EventArgs e) => ShowRightPanel("Bible");
    private void OnToolRemoteClicked(object? sender, EventArgs e) => ShowRightPanel("Remote");

    private Slide? SelectedSlide => SlidesList.SelectedItem as Slide;

    private void ApplySlideTheme(string background, string foreground)
    {
        if (SelectedSlide is not Slide slide)
        {
            SetStatus("Sélectionnez une diapositive avant de choisir un thème.");
            return;
        }

        slide.BackgroundColor = background;
        slide.TextColor = foreground;
        SetStatus("Thème appliqué à la diapositive sélectionnée.");
        ScheduleAutosave();
    }

    private void OnThemeNightClicked(object? sender, EventArgs e) => ApplySlideTheme("#111827", "#FFFFFF");
    private void OnThemeBurgundyClicked(object? sender, EventArgs e) => ApplySlideTheme("#5B1D35", "#FFFFFF");
    private void OnThemeForestClicked(object? sender, EventArgs e) => ApplySlideTheme("#164E3B", "#FFFFFF");
    private void OnThemeLightClicked(object? sender, EventArgs e) => ApplySlideTheme("#F1F5F9", "#111827");

    private void OnDarkAppearanceClicked(object? sender, EventArgs e)
    {
        BackgroundColor = Color.FromArgb("#0B0F17");
        SetStatus("Mode sombre activé.");
    }

    private void OnLightAppearanceClicked(object? sender, EventArgs e)
    {
        BackgroundColor = Color.FromArgb("#E2E8F0");
        SetStatus("Mode clair activé pour l'arrière-plan de l'espace de travail.");
    }

    private string MediaDirectory => System.IO.Path.Combine(FileSystem.AppDataDirectory, "MediaLibrary");

    private void LoadMediaLibrary()
    {
        Directory.CreateDirectory(MediaDirectory);
        _mediaAssets.Clear();
        foreach (var path in Directory.EnumerateFiles(MediaDirectory).OrderBy(p => System.IO.Path.GetFileName(p)))
        {
            var ext = System.IO.Path.GetExtension(path).ToLowerInvariant();
            var kind = ext is ".mp4" or ".mov" or ".avi" or ".mkv" or ".wmv" or ".webm" ? "Video" : ext is ".mp3" or ".wav" or ".m4a" or ".aac" or ".ogg" or ".flac" or ".wma" ? "Audio" : "Image";
            _mediaAssets.Add(new MediaLibraryItem { Name = System.IO.Path.GetFileName(path), Path = path, Kind = kind });
        }
    }

    private void OnMediaSearchChanged(object? sender, TextChangedEventArgs e)
    {
        var query = e.NewTextValue?.Trim() ?? string.Empty;
        MediaAssetsView.ItemsSource = string.IsNullOrEmpty(query) ? _mediaAssets :
            _mediaAssets.Where(item => item.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    private void OnMediaAssetSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedMedia = e.CurrentSelection.FirstOrDefault() as MediaLibraryItem;
        MediaStatusLabel.Text = _selectedMedia is null ? "Aucun média sélectionné" : $"{_selectedMedia.Kind} · {_selectedMedia.Name}";
    }

    private async Task ImportMediaAsync(string kind)
    {
        try
        {
            var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = $"Importer {kind.ToLowerInvariant()}" });
            if (file is null) return;
            Directory.CreateDirectory(MediaDirectory);
            var destination = System.IO.Path.Combine(MediaDirectory, $"{Guid.NewGuid():N}_{System.IO.Path.GetFileName(file.FileName)}");
            await using (var source = await file.OpenReadAsync())
            await using (var target = File.Create(destination))
                await source.CopyToAsync(target);
            LoadMediaLibrary();
            _selectedMedia = _mediaAssets.FirstOrDefault(item => item.Path == destination);
            MediaAssetsView.SelectedItem = _selectedMedia;
            MediaStatusLabel.Text = $"{kind} importé : {file.FileName}";
            SetStatus("Média ajouté à la médiathèque locale.");
        }
        catch (Exception ex) { SetStatus("Importation impossible : " + ex.Message); }
    }

    private async void OnImportImageClicked(object? sender, EventArgs e) => await ImportMediaAsync("Image");
    private async void OnImportVideoClicked(object? sender, EventArgs e) => await ImportMediaAsync("Vidéo");
    private async void OnImportAudioClicked(object? sender, EventArgs e) => await ImportMediaAsync("Audio");

    private void OnInsertMediaClicked(object? sender, EventArgs e)
    {
        if (_selectedMedia is null || SelectedSlide is not Slide slide) { SetStatus("Sélectionnez un média et une diapositive."); return; }
        if (_selectedMedia.Kind == "Image")
        {
            slide.BackgroundPath = _selectedMedia.Path;
            SetStatus("Image associée à la diapositive sélectionnée.");
        }
        else SetStatus("Vidéo conservée dans la médiathèque. Le lecteur vidéo Live reste à intégrer.");
        ScheduleAutosave();
    }

    private void OnSetMediaBackgroundClicked(object? sender, EventArgs e)
    {
        if (_selectedMedia is null || _selectedMedia.Kind != "Image" || SelectedSlide is not Slide slide)
        {
            SetStatus("Sélectionnez une image et une diapositive pour définir l'arrière-plan.");
            return;
        }

        slide.BackgroundPath = _selectedMedia.Path;
        UpdatePreview();
        SetStatus("Arrière-plan image appliqué à la diapositive.");
        ScheduleAutosave();
    }

    private void OnSetSlideAudioClicked(object? sender, EventArgs e)
    {
        if (_selectedMedia is null || _selectedMedia.Kind != "Audio" || SelectedSlide is not Slide slide)
        {
            SetStatus("Sélectionnez un fichier audio et une diapositive.");
            return;
        }

        slide.BackgroundAudioPath = _selectedMedia.Path;
        SetStatus("Piste audio associée à la diapositive. Elle est conservée avec le projet local.");
        ScheduleAutosave();
    }

    private async void OnOpenMediaClicked(object? sender, EventArgs e)
    {
        if (_selectedMedia is null || !File.Exists(_selectedMedia.Path))
        {
            SetStatus("Sélectionnez un média existant.");
            return;
        }

        try
        {
            await Launcher.Default.OpenAsync(new OpenFileRequest("Ouvrir le média", new ReadOnlyFile(_selectedMedia.Path)));
        }
        catch (Exception ex)
        {
            SetStatus("Ouverture du média impossible : " + ex.Message);
        }
    }

    private void ApplyMediaBackgroundColor(string color)
    {
        if (SelectedSlide is not Slide slide)
        {
            SetStatus("Sélectionnez une diapositive.");
            return;
        }

        slide.BackgroundColor = color;
        slide.BackgroundPath = null;
        UpdatePreview();
        SetStatus("Couleur d'arrière-plan appliquée.");
        ScheduleAutosave();
    }

    private void OnMediaBackgroundNightClicked(object? sender, EventArgs e) => ApplyMediaBackgroundColor("#111827");
    private void OnMediaBackgroundBurgundyClicked(object? sender, EventArgs e) => ApplyMediaBackgroundColor("#5B1D35");
    private void OnMediaBackgroundForestClicked(object? sender, EventArgs e) => ApplyMediaBackgroundColor("#164E3B");
    private void OnMediaBackgroundLightClicked(object? sender, EventArgs e) => ApplyMediaBackgroundColor("#F1F5F9");

    private void OnApplyTransitionClicked(object? sender, EventArgs e)
    {
        if (SelectedSlide is not Slide slide)
        {
            SetStatus("Sélectionnez une diapositive.");
            return;
        }

        slide.Transition = TransitionPicker.SelectedItem?.ToString() ?? "Fondu";
        SetStatus("Transition enregistrée : " + slide.Transition);
        ScheduleAutosave();
    }

    private void OnDeleteMediaClicked(object? sender, EventArgs e)
    {
        if (_selectedMedia is null) return;
        try
        {
            if (File.Exists(_selectedMedia.Path)) File.Delete(_selectedMedia.Path);
            _mediaAssets.Remove(_selectedMedia);
            _selectedMedia = null;
            MediaAssetsView.SelectedItem = null;
            MediaStatusLabel.Text = "Média supprimé de la médiathèque.";
        }
        catch (Exception ex) { SetStatus("Suppression impossible : " + ex.Message); }
    }

    private void AddSlidesToPlaylist(IReadOnlyList<Slide> slides)
    {
        foreach (var slide in slides) _slides.Add(slide);
        if (slides.Count > 0)
        {
            SlidesList.SelectedItem = slides[0];
            _selectedIndex = _slides.IndexOf(slides[0]);
            UpdatePreview();
        }
        SetStatus($"{slides.Count} diapositive(s) ajoutée(s) à la présentation.");
        ScheduleAutosave();
    }

    private void OnBibleClicked(object? sender, EventArgs e) => ShowRightPanel("Bible");

    private void OnWorshipClicked(object? sender, EventArgs e) => ShowRightPanel("Lyrics");

    private async void OnLiveClicked(object? sender, EventArgs e)
    {
        if (_slides.Count == 0)
        {
            SetStatus("Ajoutez une diapositive avant de démarrer.");
            return;
        }

        var session = _activeSession ?? new LiveProductionSession(_slides.ToList(), _selectedIndex);
        _activeSession = session;
#if WINDOWS
        var app = Application.Current;
        if (app is null)
        {
            SetStatus("Impossible d'ouvrir les écrans.");
            return;
        }

        app.OpenWindow(new Window(new LivePage(session, LiveScreenRole.Public))
        {
            Title = "ChustaFlorg · Écran public"
        });
        app.OpenWindow(new Window(new LivePage(session, LiveScreenRole.Operator))
        {
            Title = "ChustaFlorg · Écran opérateur"
        });
        app.OpenWindow(new Window(new LivePage(session, LiveScreenRole.Stage))
        {
            Title = "ChustaFlorg · Écran de scène"
        });
        SetStatus("Production lancée : 3 fenêtres synchronisées. Déplacez chaque fenêtre sur l'écran souhaité.");
#else
        await Navigation.PushModalAsync(new NavigationPage(new LivePage(session, LiveScreenRole.Operator)));
        SetStatus("Présentation ouverte. Le mode multi-fenêtres dépend de la plateforme.");
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


    private async void OnStartRemoteHostClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_remoteServer is not null)
            {
                RemoteConnectionStatusLabel.Text = "Serveur déjà actif.";
                return;
            }

            _activeSession ??= new LiveProductionSession(_slides.ToList(), _selectedIndex);
            _remoteServer = new RemoteControlService(_activeSession);
            await _remoteServer.StartAsync();
            _remoteBaseUrl = _remoteServer.BaseUrl;
            _remotePin = _remoteServer.Pin;
            RemoteHostAddressLabel.Text = "Adresse : " + _remoteServer.BaseUrl;
            RemotePinLabel.Text = "Code PIN : " + _remotePin;
            RemoteConnectionStatusLabel.Text = "Serveur actif · En attente du téléphone";
            SetStatus("Télécommande activée sur le réseau local.");
        }
        catch (Exception ex)
        {
            _remoteServer = null;
            RemoteConnectionStatusLabel.Text = "Démarrage impossible : " + ex.Message;
        }
    }

    private async void OnStopRemoteHostClicked(object? sender, EventArgs e)
    {
        if (_remoteServer is not null)
        {
            await _remoteServer.DisposeAsync();
            _remoteServer = null;
        }
        RemoteHostAddressLabel.Text = "Serveur arrêté";
        RemotePinLabel.Text = "Code PIN : —";
        RemoteConnectionStatusLabel.Text = "Serveur arrêté";
    }

    private async void OnConnectRemoteClicked(object? sender, EventArgs e)
    {
        try
        {
            var address = RemoteAddressEntry.Text?.Trim().TrimEnd('/');
            var pin = RemotePinEntry.Text?.Trim();
            if (string.IsNullOrWhiteSpace(address) || !Uri.TryCreate(address, UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) || string.IsNullOrWhiteSpace(pin))
            {
                RemoteConnectionStatusLabel.Text = "Adresse ou code PIN invalide.";
                return;
            }

            var response = await _remoteHttp.GetStringAsync($"{address}/status?pin={Uri.EscapeDataString(pin)}");
            using var json = System.Text.Json.JsonDocument.Parse(response);
            if (!json.RootElement.TryGetProperty("ok", out var ok) || !ok.GetBoolean())
                throw new InvalidOperationException("Code PIN incorrect.");
            _remoteBaseUrl = address;
            _remotePin = pin;
            var current = json.RootElement.GetProperty("index").GetInt32() + 1;
            var count = json.RootElement.GetProperty("count").GetInt32();
            RemoteSlideStatusLabel.Text = $"Diapositive {current} / {count}";
            RemoteConnectionStatusLabel.Text = "Connecté à l'ordinateur hôte.";
        }
        catch (Exception ex)
        {
            _remoteBaseUrl = null;
            _remotePin = null;
            RemoteConnectionStatusLabel.Text = "Connexion impossible : " + ex.Message;
        }
    }

    private async Task SendRemoteCommandAsync(string command)
    {
        if (string.IsNullOrWhiteSpace(_remoteBaseUrl) || string.IsNullOrWhiteSpace(_remotePin))
        {
            RemoteConnectionStatusLabel.Text = "Connectez-vous d'abord à l'ordinateur hôte.";
            return;
        }
        try
        {
            var url = $"{_remoteBaseUrl}/{command}?pin={Uri.EscapeDataString(_remotePin)}";
            var response = await _remoteHttp.GetStringAsync(url);
            using var json = System.Text.Json.JsonDocument.Parse(response);
            var root = json.RootElement;
            if (!root.GetProperty("ok").GetBoolean()) throw new InvalidOperationException("Commande refusée.");
            RemoteSlideStatusLabel.Text = $"Diapositive {root.GetProperty("index").GetInt32() + 1} / {root.GetProperty("count").GetInt32()}";
            RemoteConnectionStatusLabel.Text = "Télécommande connectée.";
        }
        catch (Exception ex)
        {
            RemoteConnectionStatusLabel.Text = "Connexion perdue : " + ex.Message;
        }
    }

    private async void OnRemotePreviousClicked(object? sender, EventArgs e) => await SendRemoteCommandAsync("previous");
    private async void OnRemoteNextClicked(object? sender, EventArgs e) => await SendRemoteCommandAsync("next");

    private void SetStatus(string message) => StatusLabel.Text = message;
}