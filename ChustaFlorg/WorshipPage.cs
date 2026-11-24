using System.Collections.ObjectModel;
using ChustaFlorg.Models;
using ChustaFlorg.Services;
using System.Text.Json;
using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace ChustaFlorg;

public sealed class WorshipPage : ContentPage
{
    private readonly ObservableCollection<WorshipSong> _songs = [];
    private readonly WorshipLibraryService _library = new();
    private readonly WorshipImportService _importer = new();
    private readonly WorshipSyncService _sync = new();
    private readonly Action<IReadOnlyList<Slide>> _addToPresentation;
    private readonly SearchBar _search = new() { Placeholder = "Rechercher un chant, un auteur, un mot des paroles…" };
    private readonly CollectionView _songList = new() { SelectionMode = SelectionMode.Single, HeightRequest = 330 };
    private readonly Entry _title = new() { Placeholder = "Titre du chant" };
    private readonly Entry _author = new() { Placeholder = "Auteur / compositeur" };
    private readonly Entry _key = new() { Placeholder = "Tonalité (ex. Sol, D, G)" };
    private readonly Entry _tempo = new() { Placeholder = "Tempo en BPM (facultatif)", Keyboard = Keyboard.Numeric };
    private readonly Entry _tags = new() { Placeholder = "Tags séparés par des virgules" };
    private readonly Editor _chordLyrics = new() { Placeholder = "Accords : [G]Amazing [D]grace", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 100 };
    private readonly Picker _arrangementPicker = new() { Title = "Arrangement" };
    private readonly ObservableCollection<ArrangementEntry> _arrangementRows = [];
    private readonly CollectionView _arrangementList = new() { SelectionMode = SelectionMode.Single, HeightRequest = 130 };
    private readonly Switch _includeChords = new();
    private readonly Picker _sectionType = new() { Title = "Type de section" };
    private readonly Editor _lyrics = new() { Placeholder = "Saisissez les paroles de cette section…", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 150 };
    private readonly CollectionView _sections = new() { SelectionMode = SelectionMode.Single, HeightRequest = 170 };
    private readonly Label _status = new() { Text = "Bibliothèque locale", TextColor = Color.FromArgb("#D8B46A"), FontSize = 12 };
    private readonly ObservableCollection<SongSection> _songSections = [];
    private WorshipSong? _selectedSong;
    private SongSection? _selectedSection;
    private SongArrangement? _selectedArrangement;
    private bool _loading;
    private CancellationTokenSource? _saveCts;
    private readonly Color _panel = Color.FromArgb("#121A28");
    private readonly Color _field = Color.FromArgb("#1A2435");
    private readonly Color _gold = Color.FromArgb("#D8B46A");

    public WorshipPage(Action<IReadOnlyList<Slide>> addToPresentation)
    {
        _addToPresentation = addToPresentation;
        Title = "Worship · Bibliothèque de chants";
        BackgroundColor = Color.FromArgb("#0B0F17");
        BuildInterface();
        _ = LoadLibraryAsync();
    }

    private void BuildInterface()
    {
        _sectionType.ItemsSource = Enum.GetValues<SongSectionType>()
            .Select(SectionTypeLabel).ToList();
        _sectionType.SelectedIndex = 1;

        _songList.ItemTemplate = new DataTemplate(() =>
        {
            var title = new Label { TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 14 };
            title.SetBinding(Label.TextProperty, nameof(WorshipSong.Title));
            var details = new Label { TextColor = Color.FromArgb("#94A3B8"), FontSize = 11 };
            details.SetBinding(Label.TextProperty, new Binding(nameof(WorshipSong.Author), stringFormat: "Auteur : {0}"));
            var count = new Label { TextColor = _gold, FontSize = 10 };
            count.SetBinding(Label.TextProperty, new Binding(nameof(WorshipSong.Sections), converter: new SectionCountConverter()));
            return new Border
            {
                BackgroundColor = _field, Stroke = Color.FromArgb("#334155"), StrokeThickness = 1,
                Padding = 11, Margin = new Thickness(0, 3),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                Content = new VerticalStackLayout { Spacing = 4, Children = { title, details, count } }
            };
        });
        _songList.ItemsSource = _songs;
        _songList.SelectionChanged += OnSongSelected;

        _sections.ItemTemplate = new DataTemplate(() =>
        {
            var name = new Label { TextColor = Colors.White, FontAttributes = FontAttributes.Bold, FontSize = 12 };
            name.SetBinding(Label.TextProperty, nameof(SongSection.DisplayName));
            var preview = new Label { TextColor = Color.FromArgb("#94A3B8"), FontSize = 10, MaxLines = 2 };
            preview.SetBinding(Label.TextProperty, nameof(SongSection.Lyrics));
            return new Border
            {
                BackgroundColor = _field, Stroke = Color.FromArgb("#334155"), StrokeThickness = 1,
                Padding = 9, Margin = new Thickness(0, 2),
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 7 },
                Content = new VerticalStackLayout { Spacing = 3, Children = { name, preview } }
            };
        });
        _sections.ItemsSource = _songSections;
        _sections.SelectionChanged += OnSectionSelected;
        _arrangementList.ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label { TextColor = Colors.White, FontSize = 11 };
            label.SetBinding(Label.TextProperty, nameof(ArrangementEntry.DisplayName));
            return new Border { BackgroundColor = _field, Stroke = Color.FromArgb("#334155"), Padding = 8,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 6 }, Content = label };
        });
        _arrangementList.ItemsSource = _arrangementRows;

        _search.TextChanged += (_, _) => FilterSongs();
        _title.TextChanged += (_, _) => UpdateSongMetadata();
        _author.TextChanged += (_, _) => UpdateSongMetadata();
        _key.TextChanged += (_, _) => UpdateSongMetadata();
        _tempo.TextChanged += (_, _) => UpdateSongMetadata();
        _tags.TextChanged += (_, _) => UpdateSongMetadata();
        _chordLyrics.TextChanged += (_, _) =>
        {
            if (!_loading && _selectedSection is not null)
            {
                _selectedSection.ChordLyrics = _chordLyrics.Text ?? string.Empty;
                ScheduleLibrarySave();
            }
        };
        _arrangementPicker.SelectedIndexChanged += OnArrangementChanged;
        _lyrics.TextChanged += (_, _) =>
        {
            if (!_loading && _selectedSection is not null)
            {
                _selectedSection.Lyrics = _lyrics.Text ?? string.Empty;
                ScheduleLibrarySave();
            }
        };

        var header = new Grid { Padding = new Thickness(16, 12), BackgroundColor = _panel, ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) } };
        header.Add(new VerticalStackLayout
        {
            Spacing = 2,
            Children =
            {
                new Label { Text = "WORSHIP", TextColor = _gold, FontSize = 10, FontAttributes = FontAttributes.Bold, CharacterSpacing = 2 },
                new Label { Text = "Bibliothèque de chants", TextColor = Colors.White, FontSize = 20, FontAttributes = FontAttributes.Bold }
            }
        });
        var close = MakeButton("Fermer", "#263246", () => Navigation.PopModalAsync());
        header.Add(close, 1);

        var libraryPane = new VerticalStackLayout { Spacing = 9, Padding = 12, BackgroundColor = _panel };
        libraryPane.Children.Add(new Label { Text = "MES CHANTS", TextColor = _gold, FontSize = 10, FontAttributes = FontAttributes.Bold });
        libraryPane.Children.Add(_search);
        libraryPane.Children.Add(_songList);
        libraryPane.Children.Add(MakeButton("+ Nouveau chant", "#7A1F3D", CreateSongAsync));
        libraryPane.Children.Add(MakeButton("Importer plusieurs chants", "#263246", ImportSongsAsync));
        libraryPane.Children.Add(MakeButton("Exporter la bibliothèque", "#263246", ExportSongsAsync));
        libraryPane.Children.Add(MakeButton("Détecter les doublons", "#263246", DetectDuplicatesAsync));
        libraryPane.Children.Add(MakeButton("Configurer la synchronisation", "#263246", ConfigureSyncAsync));
        libraryPane.Children.Add(MakeButton("Synchroniser maintenant", "#7A1F3D", SyncNowAsync));

        var editorPane = new VerticalStackLayout { Spacing = 9, Padding = 14, BackgroundColor = _panel };
        editorPane.Children.Add(new Label { Text = "FICHE DU CHANT", TextColor = _gold, FontSize = 10, FontAttributes = FontAttributes.Bold });
        editorPane.Children.Add(_title);
        editorPane.Children.Add(_author);
        editorPane.Children.Add(_key);
        editorPane.Children.Add(_tempo);
        editorPane.Children.Add(_tags);
        editorPane.Children.Add(new Label { Text = "ARRANGEMENTS ALTERNATIFS", TextColor = _gold, FontSize = 10, FontAttributes = FontAttributes.Bold });
        editorPane.Children.Add(_arrangementPicker);
        editorPane.Children.Add(MakeButton("+ Créer un arrangement", "#263246", CreateArrangementAsync));
        editorPane.Children.Add(_arrangementList);
        editorPane.Children.Add(new HorizontalStackLayout { Spacing = 6, Children =
        {
            MakeButton("Ajouter section active", "#263246", AddSectionToArrangementAsync),
            MakeButton("Répéter refrain", "#263246", AddChorusToArrangementAsync)
        }});
        editorPane.Children.Add(new HorizontalStackLayout { Spacing = 6, Children =
        {
            MakeButton("↑", "#263246", () => MoveArrangementEntryAsync(-1)),
            MakeButton("↓", "#263246", () => MoveArrangementEntryAsync(1)),
            MakeButton("Retirer", "#40232D", RemoveArrangementEntryAsync)
        }});
        editorPane.Children.Add(new Label { Text = "STRUCTURE DU CHANT", TextColor = _gold, FontSize = 10, FontAttributes = FontAttributes.Bold });
        editorPane.Children.Add(_sections);
        editorPane.Children.Add(_sectionType);
        editorPane.Children.Add(MakeButton("+ Ajouter une section", "#263246", AddSectionAsync));
        editorPane.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                MakeButton("↑ Monter", "#263246", () => MoveSectionAsync(-1)),
                MakeButton("↓ Descendre", "#263246", () => MoveSectionAsync(1))
            }
        });
        editorPane.Children.Add(_lyrics);
        editorPane.Children.Add(new Label { Text = "ACCORDS MUSICAUX", TextColor = _gold, FontSize = 10, FontAttributes = FontAttributes.Bold });
        editorPane.Children.Add(_chordLyrics);
        editorPane.Children.Add(new HorizontalStackLayout { Spacing = 6, Children =
        {
            MakeButton("Transpose −", "#263246", () => TransposeChordsAsync(-1)),
            MakeButton("Transpose +", "#263246", () => TransposeChordsAsync(1)),
            MakeButton("− 2 demi-tons", "#263246", () => TransposeChordsAsync(-2)),
            MakeButton("+ 2 demi-tons", "#263246", () => TransposeChordsAsync(2)),
            MakeButton("Arrangement −", "#263246", () => AdjustArrangementTransposeAsync(-1)),
            MakeButton("Arrangement +", "#263246", () => AdjustArrangementTransposeAsync(1))
        }});
        editorPane.Children.Add(new HorizontalStackLayout { Spacing = 8, Children =
        {
            new Label { Text = "Inclure les accords dans les diapositives", TextColor = Colors.White, VerticalTextAlignment = TextAlignment.Center, FontSize = 11 },
            _includeChords
        }});
        editorPane.Children.Add(new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                MakeButton("Supprimer section", "#40232D", DeleteSectionAsync),
                MakeButton("Supprimer chant", "#40232D", DeleteSongAsync)
            }
        });
        editorPane.Children.Add(MakeButton("Ajouter le chant à la présentation →", "#7A1F3D", AddSongToPresentationAsync));
        editorPane.Children.Add(_status);

        var workspace = new Grid { Padding = 12, ColumnSpacing = 12, RowSpacing = 12 };
        workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.9, GridUnitType.Star)));
        workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.4, GridUnitType.Star)));
        workspace.Add(libraryPane, 0, 0);
        workspace.Add(new ScrollView { Content = editorPane }, 1, 0);

        var root = new Grid { RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star) } };
        root.Add(header, 0, 0);
        root.Add(new ScrollView { Content = workspace }, 0, 1);
        Content = root;

        SizeChanged += (_, _) =>
        {
            var compact = Width < 760;
            workspace.ColumnDefinitions.Clear();
            workspace.RowDefinitions.Clear();
            if (compact)
            {
                workspace.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                workspace.RowDefinitions.Add(new RowDefinition(new GridLength(320)));
                workspace.RowDefinitions.Add(new RowDefinition(new GridLength(650)));
                Grid.SetColumn(libraryPane, 0); Grid.SetRow(libraryPane, 0);
                Grid.SetColumn(editorPane, 0); Grid.SetRow(editorPane, 1);
            }
            else
            {
                workspace.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(0.9, GridUnitType.Star)));
                workspace.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(1.4, GridUnitType.Star)));
                Grid.SetColumn(libraryPane, 0); Grid.SetRow(libraryPane, 0);
                Grid.SetColumn(editorPane, 1); Grid.SetRow(editorPane, 0);
            }
        };
    }

    private Button MakeButton(string text, string color, Func<Task> action)
    {
        var button = new Button { Text = text, BackgroundColor = Color.FromArgb(color), TextColor = Colors.White, FontSize = 12, Padding = new Thickness(10, 8) };
        button.Clicked += async (_, _) =>
        {
            try { await action(); }
            catch (Exception ex) { _status.Text = "Erreur : " + ex.Message; }
        };
        return button;
    }

    private async Task LoadLibraryAsync()
    {
        try
        {
            var loaded = await _library.LoadAsync();
            _songs.Clear();
            foreach (var song in loaded.OrderBy(x => x.Title))
                _songs.Add(song);
            if (_songs.Count > 0)
                _songList.SelectedItem = _songs[0];
            else
                _status.Text = "Bibliothèque vide · Créez votre premier chant";
        }
        catch (Exception ex) { _status.Text = "Chargement impossible : " + ex.Message; }
    }

    private void FilterSongs()
    {
        var query = _search.Text?.Trim() ?? string.Empty;
        var matches = _songs.Where(song =>
            string.Join(" ", song.Title, song.Author, song.Key, string.Join(" ", song.Tags),
                string.Join(" ", song.Sections.Select(section => section.Lyrics)))
            .Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        _songList.ItemsSource = matches;
        if (string.IsNullOrWhiteSpace(query))
            _songList.SelectedItem = _selectedSong;
    }

    private void OnSongSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (_songList.SelectedItem is not WorshipSong song)
            return;
        _selectedSong = song;
        _songSections.Clear();
        foreach (var section in song.Sections)
            _songSections.Add(section);
        _loading = true;
        _title.Text = song.Title;
        _author.Text = song.Author;
        _key.Text = song.Key;
        _tempo.Text = song.Tempo > 0 ? song.Tempo.ToString() : string.Empty;
        _tags.Text = string.Join(", ", song.Tags);
        _loading = false;
        _sections.SelectedItem = _songSections.FirstOrDefault();
        EnsureOriginalArrangement(song);
        _selectedArrangement = null;
        RefreshArrangements();
        _status.Text = $"{song.Sections.Count} section(s) · Modifications enregistrées automatiquement";
    }

    private void OnSectionSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedSection = _sections.SelectedItem as SongSection;
        _loading = true;
        _lyrics.Text = _selectedSection?.Lyrics ?? string.Empty;
        _chordLyrics.Text = _selectedSection?.ChordLyrics ?? string.Empty;
        _loading = false;
    }

    private async Task CreateSongAsync()
    {
        var song = new WorshipSong { Title = "Nouveau chant" };
        song.Sections.Add(new SongSection { Type = SongSectionType.Verse, Number = 1, Lyrics = string.Empty });
        _songs.Add(song);
        _songList.ItemsSource = _songs;
        _songList.SelectedItem = song;
        await SaveLibraryAsync();
    }

    private async Task AddSectionAsync()
    {
        if (_selectedSong is null)
        {
            _status.Text = "Sélectionnez ou créez d'abord un chant.";
            return;
        }

        var selectedLabel = _sectionType.SelectedItem?.ToString() ?? "Couplet";
        var type = Enum.GetValues<SongSectionType>().First(x => SectionTypeLabel(x) == selectedLabel);
        var number = type == SongSectionType.Verse
            ? _selectedSong.Sections.Count(x => x.Type == type) + 1 : 1;
        var section = new SongSection { Type = type, Number = number };
        _selectedSong.Sections.Add(section);
        _songSections.Add(section);
        if (_selectedArrangement is not null && _selectedArrangement.Name == "Version originale")
        {
            _selectedArrangement.SectionIds.Add(section.Id);
            RefreshArrangementRows();
        }
        _sections.SelectedItem = section;
        await SaveLibraryAsync();
    }

    private async Task MoveSectionAsync(int offset)
    {
        if (_selectedSong is null || _selectedSection is null)
            return;

        var currentIndex = _songSections.IndexOf(_selectedSection);
        var targetIndex = currentIndex + offset;
        if (currentIndex < 0 || targetIndex < 0 || targetIndex >= _songSections.Count)
            return;

        _songSections.Move(currentIndex, targetIndex);
        _selectedSong.Sections.Clear();
        _selectedSong.Sections.AddRange(_songSections);
        _sections.SelectedItem = _selectedSection;
        await SaveLibraryAsync();
    }
    private void UpdateSongMetadata()
    {
        if (_loading || _selectedSong is null) return;
        _selectedSong.Title = _title.Text?.Trim() ?? string.Empty;
        _selectedSong.Author = _author.Text?.Trim() ?? string.Empty;
        _selectedSong.Key = _key.Text?.Trim() ?? string.Empty;
        _selectedSong.Tempo = int.TryParse(_tempo.Text, out var tempo) ? Math.Clamp(tempo, 0, 400) : 0;
        _selectedSong.Tags = (_tags.Text ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        _selectedSong.ModifiedAtUtc = DateTime.UtcNow;
        ScheduleLibrarySave();
    }

    private async Task DeleteSectionAsync()
    {
        if (_selectedSong is null || _selectedSection is null) return;
        _selectedSong.Sections.Remove(_selectedSection);
        _songSections.Remove(_selectedSection);
        foreach (var arrangement in _selectedSong.Arrangements)
            arrangement.SectionIds.RemoveAll(id => id == _selectedSection.Id);
        _selectedSection = null;
        RefreshArrangements();
        _lyrics.Text = string.Empty;
        _sections.SelectedItem = null;
        await SaveLibraryAsync();
    }

    private async Task DeleteSongAsync()
    {
        if (_selectedSong is null) return;
        var confirm = await DisplayAlert("Supprimer le chant", $"Supprimer « {_selectedSong.Title} » ?", "Supprimer", "Annuler");
        if (!confirm) return;
        _songs.Remove(_selectedSong);
        _selectedSong = null;
        _songSections.Clear();
        _songList.SelectedItem = null;
        _title.Text = _author.Text = _key.Text = _tempo.Text = _tags.Text = _lyrics.Text = string.Empty;
        await SaveLibraryAsync();
    }

    private async Task AddSongToPresentationAsync()
    {
        if (_selectedSong is null || !_selectedSong.Sections.Any(x => !string.IsNullOrWhiteSpace(x.Lyrics)))
        {
            _status.Text = "Ce chant ne contient aucune parole à ajouter.";
            return;
        }

        var orderedIds = _selectedArrangement?.SectionIds ?? _selectedSong.Sections.Select(x => x.Id).ToList();
        var arrangedSections = orderedIds
            .Select(id => _selectedSong.Sections.FirstOrDefault(section => section.Id == id))
            .Where(section => section is not null && !string.IsNullOrWhiteSpace(section.Lyrics))
            .Cast<SongSection>()
            .ToList();
        var slides = arrangedSections.Select(x => new Slide
        {
            Title = $"{_selectedSong.Title} · {x.DisplayName}",
            Body = _includeChords.IsToggled && !string.IsNullOrWhiteSpace(x.ChordLyrics)
                ? x.Lyrics.Trim() + "\n\n" + ChordTransposer.Transpose(x.ChordLyrics, _selectedArrangement?.SemitoneOffset ?? 0)
                : x.Lyrics.Trim(),
            BackgroundColor = "#111827",
            TextColor = "#FFFFFF"
        }).ToList();
        _addToPresentation(slides);
        _status.Text = $"{slides.Count} diapositive(s) ajoutée(s) au plan du culte.";
        await DisplayAlert("Chant ajouté", $"{slides.Count} diapositive(s) ont été ajoutées à la fin de votre présentation.", "OK");
        await Navigation.PopModalAsync();
    }


    private void EnsureOriginalArrangement(WorshipSong song)
    {
        if (song.Arrangements.Count == 0)
            song.Arrangements.Add(new SongArrangement
            {
                Name = "Version originale",
                SectionIds = song.Sections.Select(section => section.Id).ToList()
            });
    }

    private void RefreshArrangements()
    {
        if (_selectedSong is null) return;
        EnsureOriginalArrangement(_selectedSong);
        var currentName = _selectedArrangement?.Name;
        _arrangementPicker.ItemsSource = _selectedSong.Arrangements.Select(x => x.Name).ToList();
        var index = _selectedSong.Arrangements.FindIndex(x => x.Name == currentName);
        _arrangementPicker.SelectedIndex = index >= 0 ? index : 0;
        if (_arrangementPicker.SelectedIndex >= 0)
            _selectedArrangement = _selectedSong.Arrangements[_arrangementPicker.SelectedIndex];
        RefreshArrangementRows();
    }

    private void OnArrangementChanged(object? sender, EventArgs e)
    {
        if (_selectedSong is null || _arrangementPicker.SelectedIndex < 0) return;
        _selectedArrangement = _selectedSong.Arrangements[_arrangementPicker.SelectedIndex];
        RefreshArrangementRows();
    }

    private void RefreshArrangementRows()
    {
        _arrangementRows.Clear();
        if (_selectedSong is null || _selectedArrangement is null) return;
        foreach (var id in _selectedArrangement.SectionIds)
        {
            var section = _selectedSong.Sections.FirstOrDefault(x => x.Id == id);
            if (section is not null)
                _arrangementRows.Add(new ArrangementEntry(Guid.NewGuid(), id, section.DisplayName));
        }
    }

    private async Task CreateArrangementAsync()
    {
        if (_selectedSong is null) { _status.Text = "Sélectionnez un chant."; return; }
        var name = await DisplayPromptAsync("Nouvel arrangement", "Nom de cette version (ex. Culte du dimanche)");
        if (string.IsNullOrWhiteSpace(name)) return;
        if (_selectedSong.Arrangements.Any(x => string.Equals(x.Name, name.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            await DisplayAlert("Nom déjà utilisé", "Choisissez un autre nom pour cet arrangement.", "OK");
            return;
        }
        var source = _selectedArrangement ?? _selectedSong.Arrangements.FirstOrDefault();
        var arrangement = new SongArrangement
        {
            Name = name.Trim(),
            SectionIds = source is null ? _selectedSong.Sections.Select(x => x.Id).ToList() : [.. source.SectionIds],
            SemitoneOffset = source?.SemitoneOffset ?? 0
        };
        _selectedSong.Arrangements.Add(arrangement);
        _selectedArrangement = arrangement;
        RefreshArrangements();
        _arrangementPicker.SelectedIndex = _selectedSong.Arrangements.Count - 1;
        await SaveLibraryAsync();
    }

    private async Task AddSectionToArrangementAsync()
    {
        if (_selectedSong is null || _selectedArrangement is null || _selectedSection is null) return;
        _selectedArrangement.SectionIds.Add(_selectedSection.Id);
        _selectedArrangement.ModifiedAtUtc = DateTime.UtcNow;
        RefreshArrangementRows();
        await SaveLibraryAsync();
    }

    private async Task AddChorusToArrangementAsync()
    {
        if (_selectedSong is null || _selectedArrangement is null) return;
        var chorus = _selectedSong.Sections.Where(x => x.Type == SongSectionType.Chorus).ToList();
        if (chorus.Count == 0) { _status.Text = "Ce chant ne contient pas de refrain."; return; }
        _selectedArrangement.SectionIds.AddRange(chorus.Select(x => x.Id));
        _selectedArrangement.ModifiedAtUtc = DateTime.UtcNow;
        RefreshArrangementRows();
        await SaveLibraryAsync();
    }

    private async Task MoveArrangementEntryAsync(int offset)
    {
        if (_selectedArrangement is null || _arrangementList.SelectedItem is not ArrangementEntry entry) return;
        var index = _arrangementRows.IndexOf(entry);
        var target = index + offset;
        if (index < 0 || target < 0 || target >= _arrangementRows.Count) return;
        _arrangementRows.Move(index, target);
        _selectedArrangement.SectionIds = _arrangementRows.Select(x => x.SectionId).ToList();
        _selectedArrangement.ModifiedAtUtc = DateTime.UtcNow;
        await SaveLibraryAsync();
    }

    private async Task RemoveArrangementEntryAsync()
    {
        if (_selectedArrangement is null || _arrangementList.SelectedItem is not ArrangementEntry entry) return;
        _arrangementRows.Remove(entry);
        _selectedArrangement.SectionIds = _arrangementRows.Select(x => x.SectionId).ToList();
        _selectedArrangement.ModifiedAtUtc = DateTime.UtcNow;
        await SaveLibraryAsync();
    }

    private async Task TransposeChordsAsync(int semitones)
    {
        if (_selectedSection is null) { _status.Text = "Sélectionnez une section contenant des accords."; return; }
        _selectedSection.ChordLyrics = ChordTransposer.Transpose(_selectedSection.ChordLyrics, semitones);
        _loading = true;
        _chordLyrics.Text = _selectedSection.ChordLyrics;
        _loading = false;
        await SaveLibraryAsync();
        _status.Text = $"Accords transposés de {semitones:+#;-#;0} demi-ton(s).";
    }

    private async Task AdjustArrangementTransposeAsync(int semitones)
    {
        if (_selectedArrangement is null) { _status.Text = "Créez ou sélectionnez un arrangement."; return; }
        _selectedArrangement.SemitoneOffset = Math.Clamp(_selectedArrangement.SemitoneOffset + semitones, -12, 12);
        _selectedArrangement.ModifiedAtUtc = DateTime.UtcNow;
        await SaveLibraryAsync();
        _status.Text = $"Transposition de l'arrangement : {_selectedArrangement.SemitoneOffset:+#;-#;0} demi-ton(s).";
    }

    private async Task ImportSongsAsync()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Importer une bibliothèque de chants",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                [DevicePlatform.WinUI] = [".json", ".csv", ".txt"],
                [DevicePlatform.Android] = ["application/json", "text/csv", "text/plain"],
                [DevicePlatform.iOS] = ["public.json", "public.comma-separated-values-text", "public.plain-text"],
                [DevicePlatform.MacCatalyst] = ["public.json", "public.comma-separated-values-text", "public.plain-text"]
            })
        });
        if (file is null) return;
        await using var stream = await file.OpenReadAsync();
        var imported = await _importer.ImportAsync(stream, file.FileName);
        var added = 0;
        var skipped = 0;
        foreach (var song in imported)
        {
            var duplicate = _songs.Any(existing =>
                WorshipImportService.Normalize(existing.Title) == WorshipImportService.Normalize(song.Title) &&
                (string.IsNullOrWhiteSpace(existing.Author) || string.IsNullOrWhiteSpace(song.Author) ||
                 WorshipImportService.Normalize(existing.Author) == WorshipImportService.Normalize(song.Author)));
            if (duplicate) { skipped++; continue; }
            EnsureOriginalArrangement(song);
            _songs.Add(song);
            added++;
        }
        await SaveLibraryAsync();
        FilterSongs();
        await DisplayAlert("Importation terminée", $"{added} chant(s) importé(s). {skipped} doublon(s) potentiel(s) ignoré(s).", "OK");
    }

    private async Task ExportSongsAsync()
    {
        var path = Path.Combine(FileSystem.CacheDirectory, $"chustaflorg-worship-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(_songs, new JsonSerializerOptions { WriteIndented = true }));
        await Share.Default.RequestAsync(new ShareFileRequest("Exporter la bibliothèque Worship", new ShareFile(path)));
    }

    private async Task DetectDuplicatesAsync()
    {
        var pairs = WorshipImportService.FindDuplicates(_songs);
        if (pairs.Count == 0)
        {
            await DisplayAlert("Détection des doublons", "Aucun doublon potentiel détecté.", "OK");
            return;
        }
        var report = string.Join("\n", pairs.Take(12).Select(pair => $"• {pair.First.Title}  ↔  {pair.Second.Title}"));
        if (pairs.Count > 12) report += $"\n… et {pairs.Count - 12} autre(s) paire(s).";
        await DisplayAlert($"{pairs.Count} doublon(s) potentiel(s)", report, "OK");
    }

    private async Task ConfigureSyncAsync()
    {
        var url = await DisplayPromptAsync("Synchronisation WebDAV", "URL HTTPS du fichier distant (ex. Nextcloud …/remote.php/dav/files/USER/ChustaFlorg/worship-library.json)");
        if (string.IsNullOrWhiteSpace(url)) return;
        var username = await DisplayPromptAsync("Compte WebDAV", "Nom d'utilisateur");
        if (username is null) return;
        var passwordEntry = new Entry { IsPassword = true, Placeholder = "Mot de passe WebDAV" };
        var completion = new TaskCompletionSource<string?>();
        var passwordPage = new ContentPage
        {
            Title = "Mot de passe WebDAV",
            BackgroundColor = Color.FromArgb("#0B0F17"),
            Content = new VerticalStackLayout
            {
                Padding = 24, Spacing = 16,
                Children =
                {
                    new Label { Text = "Saisissez le mot de passe du compte WebDAV.", TextColor = Colors.White },
                    passwordEntry,
                    new HorizontalStackLayout
                    {
                        Children =
                        {
                            new Button { Text = "Annuler", Command = new Command(async () => { completion.TrySetResult(null); await Navigation.PopModalAsync(); }) },
                            new Button { Text = "Enregistrer", Command = new Command(async () => { completion.TrySetResult(passwordEntry.Text); await Navigation.PopModalAsync(); }) }
                        }
                    }
                }
            }
        };
        await Navigation.PushModalAsync(new NavigationPage(passwordPage));
        passwordPage.Disappearing += (_, _) => completion.TrySetResult(null);
        var password = await completion.Task;
        if (password is null) return;
        await _sync.ConfigureAsync(url, username, password);
        _status.Text = "Synchronisation configurée · identifiants stockés de manière sécurisée";
    }

    private async Task SyncNowAsync()
    {
        try
        {
            if (!await _sync.IsConfiguredAsync())
            {
                await DisplayAlert("Synchronisation", "Configurez d'abord votre URL WebDAV HTTPS.", "OK");
                return;
            }
            var merged = await _sync.SyncAsync(_songs.ToList());
            _songs.Clear();
            foreach (var song in merged) _songs.Add(song);
            if (_selectedSong is not null)
                _selectedSong = _songs.FirstOrDefault(x => x.Id == _selectedSong.Id);
            await _library.SaveAsync(merged);
            FilterSongs();
            _status.Text = $"Synchronisation terminée · {merged.Count} chant(s)";
        }
        catch (Exception ex)
        {
            _status.Text = "Synchronisation impossible : " + ex.Message;
            await DisplayAlert("Erreur de synchronisation", ex.Message, "OK");
        }
    }

    private sealed record ArrangementEntry(Guid EntryId, Guid SectionId, string DisplayName);

    private void RefreshSections()
    {
        _sections.ItemsSource = null;
        _sections.ItemsSource = _songSections;
        _sections.SelectedItem = _selectedSection;
    }

    private void ScheduleLibrarySave()
    {
        _saveCts?.Cancel();
        _saveCts?.Dispose();
        var cts = new CancellationTokenSource();
        _saveCts = cts;
        _ = SaveLibraryAfterDelayAsync(cts);
    }

    private async Task SaveLibraryAfterDelayAsync(CancellationTokenSource cts)
    {
        try
        {
            await Task.Delay(350, cts.Token);
            await SaveLibraryAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _status.Text = "Sauvegarde impossible : " + ex.Message; }
    }

    private async Task SaveLibraryAsync()
    {
        if (_selectedSong is not null)
            _selectedSong.ModifiedAtUtc = DateTime.UtcNow;

        // Take a UI-thread snapshot so rapid edits cannot mutate the collection
        // while the JSON serializer is writing it asynchronously.
        var snapshot = _songs.Select(song => new WorshipSong
        {
            Id = song.Id,
            Title = song.Title,
            Author = song.Author,
            Key = song.Key,
            Tempo = song.Tempo,
            Language = song.Language,
            Tags = [.. song.Tags],
            ModifiedAtUtc = song.ModifiedAtUtc,
            Sections = song.Sections.Select(section => new SongSection
            {
                Id = section.Id,
                Type = section.Type,
                Number = section.Number,
                Label = section.Label,
                Lyrics = section.Lyrics,
                ChordLyrics = section.ChordLyrics
            }).ToList(),
            Arrangements = song.Arrangements.Select(arrangement => new SongArrangement
            {
                Id = arrangement.Id,
                Name = arrangement.Name,
                SectionIds = [.. arrangement.SectionIds],
                SemitoneOffset = arrangement.SemitoneOffset,
                ModifiedAtUtc = arrangement.ModifiedAtUtc
            }).ToList()
        }).ToList();

        await _library.SaveAsync(snapshot);
        _status.Text = "Bibliothèque enregistrée sur cet appareil";
    }

    private static string SectionTypeLabel(SongSectionType type) => type switch
    {
        SongSectionType.Intro => "Introduction",
        SongSectionType.Verse => "Couplet",
        SongSectionType.PreChorus => "Pré-refrain",
        SongSectionType.Chorus => "Refrain",
        SongSectionType.Bridge => "Pont",
        SongSectionType.Tag => "Final",
        SongSectionType.Outro => "Conclusion",
        _ => "Couplet"
    };

    private sealed class SectionCountConverter : IValueConverter
    {
        public object Convert(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => value is List<SongSection> sections ? $"{sections.Count} section(s)" : "0 section";
        public object ConvertBack(object? value, Type targetType, object? parameter, System.Globalization.CultureInfo culture)
            => throw new NotSupportedException();
    }
}
