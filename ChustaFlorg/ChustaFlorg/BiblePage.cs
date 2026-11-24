using System.Collections.ObjectModel;
using ChustaFlorg.Models;
using ChustaFlorg.Services;

namespace ChustaFlorg;

public sealed class BiblePage : ContentView
{
    private readonly BibleService _bible = new();
    private readonly Action<IReadOnlyList<Slide>> _addSlides;
    private readonly Picker _translationPicker = new() { Title = "Traduction" };
    private readonly Picker _bookPicker = new() { Title = "Livre biblique" };
    private readonly Entry _chapterEntry = new() { Text = "1", Keyboard = Keyboard.Numeric, WidthRequest = 70 };
    private readonly Entry _searchEntry = new() { Placeholder = "Rechercher un mot dans ce chapitre", HorizontalOptions = LayoutOptions.FillAndExpand };
    private readonly Label _status = new() { Text = "Choisissez une traduction.", TextColor = Color.FromArgb("#D8B46A"), FontSize = 12 };
    private readonly Label _reference = new() { Text = "Aucun chapitre chargé", TextColor = Colors.White, FontSize = 18, FontAttributes = FontAttributes.Bold };
    private readonly CollectionView _verses = new() { SelectionMode = SelectionMode.Single, HeightRequest = 310 };
    private readonly CollectionView _results = new() { SelectionMode = SelectionMode.Single, HeightRequest = 140 };
    private readonly CollectionView _bookmarksView = new() { SelectionMode = SelectionMode.Single, HeightRequest = 150 };
    private readonly Editor _noteEditor = new() { Placeholder = "Vos notes personnelles sur ce verset…", AutoSize = EditorAutoSizeOption.TextChanges, MinimumHeightRequest = 90 };
    private readonly ObservableCollection<BibleVerse> _verseItems = [];
    private readonly ObservableCollection<BibleVerse> _searchItems = [];
    private readonly ObservableCollection<BibleBookmark> _bookmarks = [];
    private BibleTranslation? _translation;
    private BibleBook? _book;
    private BibleVerse? _selectedVerse;
    private BibleChapter? _chapter;
    private bool _loading;

    public BiblePage(Action<IReadOnlyList<Slide>> addSlides)
    {
        _addSlides = addSlides;
        Padding = new Thickness(4);
        _translationPicker.ItemsSource = _bible.GetTranslations().ToList();
        _translationPicker.ItemDisplayBinding = new Binding(nameof(BibleTranslation.Name));
        _bookPicker.ItemDisplayBinding = new Binding(nameof(BibleBook.Name));
        _verses.ItemsSource = _verseItems;
        _verses.ItemTemplate = VerseTemplate();
        _verses.SelectionChanged += OnVerseSelected;
        _results.ItemsSource = _searchItems;
        _results.ItemTemplate = VerseTemplate();
        _results.SelectionChanged += OnSearchResultSelected;
        _bookmarksView.ItemsSource = _bookmarks;
        _bookmarksView.ItemTemplate = new DataTemplate(() =>
        {
            var label = new Label { TextColor = Colors.White, FontSize = 13 };
            label.SetBinding(Label.TextProperty, nameof(BibleBookmark.Reference));
            return new Border { Padding = 8, Margin = new Thickness(0, 2), Stroke = Color.FromArgb("#334155"), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = new CornerRadius(8) }, Content = label };
        });
        _bookmarksView.SelectionChanged += OnBookmarkSelected;

        Content = new ScrollView { Content = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                Header(),
                SectionLabel("LECTURE"),
                _translationPicker,
                _bookPicker,
                new HorizontalStackLayout { Spacing = 8, Children = { new Label { Text = "Chapitre", TextColor = Colors.White, VerticalOptions = LayoutOptions.Center }, _chapterEntry, ActionButton("Charger", OnLoadChapterClicked) } },
                _reference,
                _verses,
                new HorizontalStackLayout { Spacing = 8, Children = { ActionButton("Ajouter le verset", OnAddVerseClicked), ActionButton("Ajouter le chapitre", OnAddChapterClicked) } },
                SectionLabel("RECHERCHE"),
                new HorizontalStackLayout { Spacing = 8, Children = { _searchEntry, ActionButton("Chercher", OnSearchClicked) } },
                _results,
                SectionLabel("FAVORIS ET NOTES"),
                new HorizontalStackLayout { Spacing = 8, Children = { ActionButton("☆ Favori", OnFavoriteClicked), ActionButton("Enregistrer la note", OnSaveNoteClicked) } },
                _noteEditor,
                _bookmarksView,
                _status
            }
        }};
        _translationPicker.SelectedIndexChanged += OnTranslationChanged;
        _bookPicker.SelectedIndexChanged += OnBookChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_bookmarks.Count > 0) return;
        foreach (var bookmark in await _bible.LoadBookmarksAsync()) _bookmarks.Add(bookmark);
        if (_translationPicker.SelectedIndex < 0 && _translationPicker.ItemsSource is System.Collections.IList translations && translations.Count > 0)
            _translationPicker.SelectedIndex = 0;
    }

    private View Header() => new VerticalStackLayout
    {
        Spacing = 3,
        Children =
        {
            new Label { Text = "BIBLE", TextColor = Color.FromArgb("#D8B46A"), FontSize = 11, FontAttributes = FontAttributes.Bold, CharacterSpacing = 2 },
            new Label { Text = "Lire, rechercher et préparer les Écritures", TextColor = Colors.White, FontSize = 20, FontAttributes = FontAttributes.Bold },
            new Label { Text = "Free.Bible · Cache local pour les chapitres consultés", TextColor = Color.FromArgb("#94A3B8"), FontSize = 11 }
        }
    };

    private static Label SectionLabel(string text) => new() { Text = text, TextColor = Color.FromArgb("#D8B46A"), FontSize = 10, FontAttributes = FontAttributes.Bold, CharacterSpacing = 1.4 };
    private static Button ActionButton(string text, Action action)
    {
        var button = new Button { Text = text, BackgroundColor = Color.FromArgb("#263246"), TextColor = Colors.White, FontSize = 12, Padding = new Thickness(12, 8), CornerRadius = 8 };
        button.Clicked += (_, _) => action();
        return button;
    }

    private static DataTemplate VerseTemplate() => new(() =>
    {
        var label = new Label { TextColor = Colors.White, FontSize = 14, LineBreakMode = LineBreakMode.WordWrap };
        label.SetBinding(Label.TextProperty, nameof(BibleVerse.DisplayText));
        return new Border { Padding = 9, Margin = new Thickness(0, 2), Stroke = Color.FromArgb("#263246"), StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }, Content = label };
    });

    private async void OnTranslationChanged(object? sender, EventArgs e)
    {
        if (_loading) return;
        if (_translationPicker.SelectedItem is not BibleTranslation translation) return;
        _translation = translation;
        _bookPicker.ItemsSource = null;
        _book = null;
        _status.Text = "Chargement de l'index des livres…";
        var books = await _bible.GetBooksAsync(translation);
        _bookPicker.ItemsSource = books.ToList();
        if (books.Count > 0) _bookPicker.SelectedIndex = 0;
        _status.Text = books.Count > 0 ? $"{books.Count} livres disponibles · réseau ou cache local" : "Index indisponible. Connectez-vous une première fois pour télécharger les livres.";
    }

    private void OnBookChanged(object? sender, EventArgs e)
    {
        _book = _bookPicker.SelectedItem as BibleBook;
        _chapterEntry.Text = "1";
    }

    private async void OnLoadChapterClicked() => await LoadChapterAsync();
    private async Task LoadChapterAsync()
    {
        if (_translation is null || _book is null) { _status.Text = "Choisissez une traduction et un livre."; return; }
        if (!int.TryParse(_chapterEntry.Text, out var number) || number < 1 || (_book.ChapterCount > 0 && number > _book.ChapterCount))
        { _status.Text = "Numéro de chapitre invalide."; return; }
        _status.Text = "Chargement du chapitre…";
        var chapter = await _bible.GetChapterAsync(_translation, _book, number);
        if (chapter is null || chapter.Verses.Count == 0) { _status.Text = "Chapitre indisponible. Il doit être téléchargé une première fois avec Internet."; return; }
        _chapter = chapter;
        _verseItems.Clear();
        foreach (var verse in chapter.Verses) _verseItems.Add(verse);
        _searchItems.Clear();
        _reference.Text = $"{chapter.Reference} · {_translation.ShortName}";
        _status.Text = "Chapitre chargé et mis en cache sur cet appareil. Il sera disponible hors ligne.";
    }

    private async void OnSearchClicked()
    {
        if (_translation is null || _book is null || _chapter is null) { _status.Text = "Chargez d'abord un chapitre."; return; }
        var found = await _bible.SearchInChapterAsync(_translation, _book, _chapter.Number, _searchEntry.Text ?? string.Empty);
        _searchItems.Clear();
        foreach (var verse in found) _searchItems.Add(verse);
        _status.Text = $"{found.Count} résultat(s) dans {_chapter.Reference}.";
    }

    private void OnVerseSelected(object? sender, SelectionChangedEventArgs e)
    {
        _selectedVerse = e.CurrentSelection.FirstOrDefault() as BibleVerse;
        if (_selectedVerse is not null) LoadNoteForSelectedVerse();
    }

    private void OnSearchResultSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is BibleVerse verse)
        {
            _selectedVerse = verse;
            _verses.SelectedItem = verse;
            LoadNoteForSelectedVerse();
        }
    }

    private void LoadNoteForSelectedVerse()
    {
        if (_selectedVerse is null || _translation is null) return;
        var existing = _bookmarks.FirstOrDefault(x => x.TranslationId == _translation.Id && x.BookId == _book?.Id && x.Chapter == _selectedVerse.Chapter && x.Verse == _selectedVerse.Verse);
        _noteEditor.Text = existing?.Note ?? string.Empty;
    }

    private async void OnFavoriteClicked()
    {
        if (_selectedVerse is null || _translation is null || _book is null) { _status.Text = "Sélectionnez un verset."; return; }
        var existing = _bookmarks.FirstOrDefault(x => x.TranslationId == _translation.Id && x.BookId == _book.Id && x.Chapter == _selectedVerse.Chapter && x.Verse == _selectedVerse.Verse);
        if (existing is null)
        {
            _bookmarks.Add(new BibleBookmark { TranslationId = _translation.Id, TranslationName = _translation.Name, BookId = _book.Id, BookName = _book.Name, Chapter = _selectedVerse.Chapter, Verse = _selectedVerse.Verse, VerseText = _selectedVerse.Text });
            _status.Text = "Verset ajouté aux favoris.";
        }
        else
        {
            _bookmarks.Remove(existing);
            _status.Text = "Verset retiré des favoris.";
        }
        await SaveBookmarksAsync();
    }

    private async void OnSaveNoteClicked()
    {
        if (_selectedVerse is null || _translation is null || _book is null) { _status.Text = "Sélectionnez un verset pour lui associer une note."; return; }
        var existing = _bookmarks.FirstOrDefault(x => x.TranslationId == _translation.Id && x.BookId == _book.Id && x.Chapter == _selectedVerse.Chapter && x.Verse == _selectedVerse.Verse);
        if (existing is null)
        {
            existing = new BibleBookmark { TranslationId = _translation.Id, TranslationName = _translation.Name, BookId = _book.Id, BookName = _book.Name, Chapter = _selectedVerse.Chapter, Verse = _selectedVerse.Verse, VerseText = _selectedVerse.Text };
            _bookmarks.Add(existing);
        }
        existing.Note = _noteEditor.Text ?? string.Empty;
        existing.UpdatedAtUtc = DateTime.UtcNow;
        await SaveBookmarksAsync();
        _status.Text = "Note enregistrée sur cet appareil.";
    }

    private async Task SaveBookmarksAsync()
    {
        await _bible.SaveBookmarksAsync(_bookmarks);
        _bookmarksView.ItemsSource = null;
        _bookmarksView.ItemsSource = _bookmarks;
    }

    private async void OnBookmarkSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not BibleBookmark bookmark) return;
        var translation = _bible.GetTranslations().FirstOrDefault(x => x.Id == bookmark.TranslationId);
        if (translation is null) { _status.Text = "Traduction du favori indisponible."; return; }
        _loading = true;
        _translationPicker.SelectedItem = translation;
        _translation = translation;
        var books = await _bible.GetBooksAsync(translation);
        _bookPicker.ItemsSource = books.ToList();
        _book = books.FirstOrDefault(x => x.Id == bookmark.BookId);
        _bookPicker.SelectedItem = _book;
        _loading = false;
        if (_book is null) return;
        _chapterEntry.Text = bookmark.Chapter.ToString();
        await LoadChapterAsync();
        _selectedVerse = _chapter?.Verses.FirstOrDefault(x => x.Verse == bookmark.Verse);
        _verses.SelectedItem = _selectedVerse;
        _noteEditor.Text = bookmark.Note;
    }

    private void OnAddVerseClicked()
    {
        if (_selectedVerse is null) { _status.Text = "Sélectionnez un verset."; return; }
        _addSlides([new Slide { Title = _selectedVerse.Reference, Body = _selectedVerse.Text, BackgroundColor = "#111827", TextColor = "#FFFFFF" }]);
        _status.Text = "Verset ajouté à la présentation.";
    }

    private void OnAddChapterClicked()
    {
        if (_chapter is null) { _status.Text = "Chargez d'abord un chapitre."; return; }
        var slides = _chapter.Verses.Select(v => new Slide { Title = v.Reference, Body = v.Text, BackgroundColor = "#111827", TextColor = "#FFFFFF" }).ToList();
        _addSlides(slides);
        _status.Text = $"{slides.Count} verset(s) ajouté(s) à la présentation.";
    }
}