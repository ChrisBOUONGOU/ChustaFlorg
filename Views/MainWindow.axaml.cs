using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Interactivity;
using ChustaFlorg.Models;
using ChustaFlorg.Services;

namespace ChustaFlorg.Views;

public partial class MainWindow : Window
{
    private readonly ProjectService _projects = new();
    private readonly PresentationProject _project = new();
    private readonly ObservableCollection<Slide> _slides = [];
    private readonly ListBox _slideList = new();
    private readonly TextBox _titleEditor = new();
    private readonly TextBox _bodyEditor = new();
    private readonly TextBlock _previewTitle = new();
    private readonly TextBlock _previewBody = new();
    private readonly TextBlock _status = new();
    private string? _currentPath;
    private LiveWindow? _liveWindow;
    private bool _loadingSelection;

    public MainWindow()
    {
        InitializeComponent();
        foreach (var slide in _project.Slides)
            _slides.Add(slide);
        Content = BuildWorkspace();
        _slideList.SelectedIndex = 0;
        UpdateStatus("Prêt · Enregistrement local disponible");
    }

    private Control BuildWorkspace()
    {
        var root = new Grid { RowDefinitions = new RowDefinitions("Auto,*"), Background = Brush("#0B0F17") };
        root.Children.Add(BuildTopBar());
        var workspace = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("250,*,300"),
            Margin = new Thickness(18, 14, 18, 18),
            ColumnSpacing = 14
        };
        Grid.SetRow(workspace, 1);
        workspace.Children.Add(BuildLibraryPanel());
        var center = BuildEditorPanel();
        Grid.SetColumn(center, 1);
        workspace.Children.Add(center);
        var inspector = BuildInspectorPanel();
        Grid.SetColumn(inspector, 2);
        workspace.Children.Add(inspector);
        root.Children.Add(workspace);
        return root;
    }

    private Control BuildTopBar()
    {
        var bar = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,Auto"),
            Background = Brush("#121A28"),
            Padding = new Thickness(20, 12)
        };
        var brand = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12, VerticalAlignment = VerticalAlignment.Center };
        brand.Children.Add(new Border
        {
            Width = 38, Height = 38, CornerRadius = new CornerRadius(11),
            Background = Brush("#7A1F3D"),
            Child = new TextBlock { Text = "C", FontSize = 22, FontWeight = FontWeight.Bold, Foreground = Brushes.White, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        });
        var names = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        names.Children.Add(new TextBlock { Text = "CHUSTAFLORG", FontSize = 15, FontWeight = FontWeight.Bold, Foreground = Brushes.White });
        names.Children.Add(new TextBlock { Text = "STUDIO DE PRÉSENTATION", FontSize = 10, Foreground = Brush("#94A3B8"), CharacterSpacing = 1.4 });
        brand.Children.Add(names);
        bar.Children.Add(brand);

        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, VerticalAlignment = VerticalAlignment.Center };
        actions.Children.Add(ActionButton("Ouvrir", OpenProjectAsync));
        actions.Children.Add(ActionButton("Enregistrer", SaveProjectAsync));
        actions.Children.Add(ActionButton("Ajouter une diapo", AddSlide));
        var live = ActionButton("▶  Démarrer le direct", StartLive);
        live.Background = Brush("#7A1F3D");
        live.Foreground = Brushes.White;
        actions.Children.Add(live);
        Grid.SetColumn(actions, 1);
        bar.Children.Add(actions);
        return bar;
    }

    private Control BuildLibraryPanel()
    {
        var panel = Panel();
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(SectionLabel("PLAN DU CULTE"));
        stack.Children.Add(new TextBlock { Text = _project.Name, FontSize = 17, FontWeight = FontWeight.SemiBold, Foreground = Brushes.White });
        stack.Children.Add(new TextBlock { Text = "DIAPOSITIVES", FontSize = 10, FontWeight = FontWeight.Bold, Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 12, 0, 0) });
        _slideList.ItemsSource = _slides;
        _slideList.ItemTemplate = new FuncDataTemplate<Slide>((slide, _) =>
            new TextBlock { Text = slide?.Title ?? "", Foreground = Brushes.White, FontSize = 13, Margin = new Thickness(8, 10) }, true);
        _slideList.Background = Brushes.Transparent;
        _slideList.BorderThickness = new Thickness(0);
        _slideList.SelectionChanged += (_, _) => LoadSelectedSlide();
        stack.Children.Add(_slideList);
        var add = ActionButton("+  Ajouter une diapositive", AddSlide);
        add.HorizontalAlignment = HorizontalAlignment.Stretch;
        stack.Children.Add(add);
        var delete = ActionButton("Supprimer la diapositive", DeleteSlide);
        delete.HorizontalAlignment = HorizontalAlignment.Stretch;
        stack.Children.Add(delete);
        panel.Child = stack;
        return panel;
    }

    private Control BuildEditorPanel()
    {
        var outer = new Grid { RowDefinitions = new RowDefinitions("Auto,*,Auto"), RowSpacing = 12 };
        var header = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
        header.Children.Add(SectionLabel("ÉDITEUR DE DIAPOSITIVE"));
        var hint = new TextBlock { Text = "Modifications locales", Foreground = Brush("#64748B"), FontSize = 11, VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(hint, 1);
        header.Children.Add(hint);
        outer.Children.Add(header);

        var canvas = new Border
        {
            Background = Brush("#111827"),
            BorderBrush = Brush("#263246"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(24),
            Child = new Grid
            {
                RowDefinitions = new RowDefinitions("*,Auto,*"),
                Children =
                {
                    new Border { Background = Brush("#1A2435"), CornerRadius = new CornerRadius(10), Margin = new Thickness(0,0,0,8), Child = new TextBlock { Text = "APERÇU PUBLIC", Foreground = Brush("#94A3B8"), FontSize = 10, Margin = new Thickness(12,7) } },
                    BuildPreviewContent()
                }
            }
        };
        outer.Children.Add(canvas);
        Grid.SetRow(canvas, 1);

        var footer = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10, HorizontalAlignment = HorizontalAlignment.Right };
        footer.Children.Add(ActionButton("← Précédente", () => MoveSlide(-1)));
        footer.Children.Add(ActionButton("Suivante →", () => MoveSlide(1)));
        Grid.SetRow(footer, 2);
        outer.Children.Add(footer);
        return outer;
    }

    private Control BuildPreviewContent()
    {
        var content = new StackPanel { Spacing = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, MaxWidth = 760 };
        _previewTitle.Text = "Bienvenue";
        _previewTitle.FontSize = 24;
        _previewTitle.FontWeight = FontWeight.SemiBold;
        _previewTitle.Foreground = Brush("#D8B46A");
        _previewTitle.HorizontalAlignment = HorizontalAlignment.Center;
        _previewBody.Text = "Votre présentation commence ici";
        _previewBody.FontSize = 34;
        _previewBody.FontWeight = FontWeight.Bold;
        _previewBody.Foreground = Brushes.White;
        _previewBody.TextAlignment = TextAlignment.Center;
        _previewBody.TextWrapping = TextWrapping.Wrap;
        content.Children.Add(_previewTitle);
        content.Children.Add(_previewBody);
        return content;
    }

    private Control BuildInspectorPanel()
    {
        var panel = Panel();
        var stack = new StackPanel { Spacing = 12 };
        stack.Children.Add(SectionLabel("PROPRIÉTÉS"));
        stack.Children.Add(FieldLabel("TITRE"));
        _titleEditor.Watermark = "Titre de la diapositive";
        _titleEditor.TextChanged += (_, _) => UpdateSelectedSlide();
        stack.Children.Add(_titleEditor);
        stack.Children.Add(FieldLabel("CONTENU"));
        _bodyEditor.AcceptsReturn = true;
        _bodyEditor.TextWrapping = TextWrapping.Wrap;
        _bodyEditor.MinHeight = 220;
        _bodyEditor.VerticalContentAlignment = VerticalAlignment.Top;
        _bodyEditor.Watermark = "Saisissez le texte à afficher…";
        _bodyEditor.TextChanged += (_, _) => UpdateSelectedSlide();
        stack.Children.Add(_bodyEditor);
        stack.Children.Add(new TextBlock
        {
            Text = "CONSEIL\nPréparez et vérifiez vos diapositives avant de lancer la présentation. Le mode direct ouvre une fenêtre séparée.",
            Foreground = Brush("#94A3B8"), FontSize = 11, TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 18, 0, 0)
        });
        _status.TextWrapping = TextWrapping.Wrap;
        _status.Foreground = Brush("#D8B46A");
        _status.FontSize = 11;
        stack.Children.Add(_status);
        panel.Child = stack;
        return panel;
    }

    private void LoadSelectedSlide()
    {
        if (_slideList.SelectedItem is not Slide slide) return;
        _loadingSelection = true;
        _titleEditor.Text = slide.Title;
        _bodyEditor.Text = slide.Body;
        _previewTitle.Text = slide.Title;
        _previewBody.Text = slide.Body;
        _loadingSelection = false;
        _liveWindow?.ShowSlide(slide);
    }

    private void UpdateSelectedSlide()
    {
        if (_loadingSelection || _slideList.SelectedItem is not Slide slide) return;
        slide.Title = _titleEditor.Text ?? "";
        slide.Body = _bodyEditor.Text ?? "";
        _previewTitle.Text = slide.Title;
        _previewBody.Text = slide.Body;
        _slideList.InvalidateVisual();
        _liveWindow?.ShowSlide(slide);
    }

    private void AddSlide()
    {
        var slide = new Slide { Title = $"Diapositive {_slides.Count + 1}", Body = "Saisissez votre texte ici" };
        _slides.Add(slide);
        _slideList.SelectedItem = slide;
        UpdateStatus("Nouvelle diapositive ajoutée");
    }

    private void DeleteSlide()
    {
        if (_slideList.SelectedItem is not Slide slide) return;
        var index = _slideList.SelectedIndex;
        _slides.Remove(slide);
        if (_slides.Count == 0) AddSlide();
        else _slideList.SelectedIndex = Math.Clamp(index, 0, _slides.Count - 1);
        UpdateStatus("Diapositive supprimée");
    }

    private void MoveSlide(int direction)
    {
        if (_slideList.SelectedItem is not Slide slide) return;
        var index = _slides.IndexOf(slide);
        var next = index + direction;
        if (next < 0 || next >= _slides.Count) return;
        _slides.Move(index, next);
        _slideList.SelectedIndex = next;
    }

    private async Task SaveProjectAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_currentPath))
            {
                var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Enregistrer le projet ChustaFlorg",
                    SuggestedFileName = "MonCulte.chustaflorg",
                    FileTypeChoices = [new FilePickerFileType("Projet ChustaFlorg") { Patterns = ["*.chustaflorg", "*.json"] }]
                });
                _currentPath = file?.TryGetLocalPath();
            }
            if (string.IsNullOrWhiteSpace(_currentPath)) return;
            _project.Slides = _slides.ToList();
            await _projects.SaveAsync(_project, _currentPath);
            UpdateStatus("Projet enregistré · " + Path.GetFileName(_currentPath));
        }
        catch (Exception ex) { UpdateStatus("Échec de l'enregistrement : " + ex.Message); }
    }

    private async Task OpenProjectAsync()
    {
        try
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Ouvrir un projet ChustaFlorg",
                AllowMultiple = false,
                FileTypeFilter = [new FilePickerFileType("Projet ChustaFlorg") { Patterns = ["*.chustaflorg", "*.json"] }]
            });
            var path = files.FirstOrDefault()?.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(path)) return;
            var loaded = await _projects.LoadAsync(path);
            _project.Name = loaded.Name;
            _project.Slides = loaded.Slides ?? [];
            _slides.Clear();
            foreach (var slide in _project.Slides) _slides.Add(slide);
            if (_slides.Count == 0) AddSlide();
            _currentPath = path;
            _slideList.SelectedIndex = 0;
            UpdateStatus("Projet ouvert · " + Path.GetFileName(path));
        }
        catch (Exception ex) { UpdateStatus("Impossible d'ouvrir le projet : " + ex.Message); }
    }

    private void StartLive()
    {
        if (_liveWindow is null || !_liveWindow.IsVisible)
        {
            _liveWindow = new LiveWindow();
            _liveWindow.Closed += (_, _) => _liveWindow = null;
            _liveWindow.Show();
        }
        if (_slideList.SelectedItem is Slide slide) _liveWindow.ShowSlide(slide);
        UpdateStatus("Sortie directe ouverte · vérifiez l'écran avant diffusion");
    }

    private void UpdateStatus(string message) => _status.Text = message;

    private static Border Panel() => new()
    {
        Background = Brush("#121A28"),
        BorderBrush = Brush("#263246"),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(12),
        Padding = new Thickness(14)
    };

    private static TextBlock SectionLabel(string text) => new()
    {
        Text = text, FontSize = 10, FontWeight = FontWeight.Bold,
        Foreground = Brush("#D8B46A"), CharacterSpacing = 1.5
    };

    private static TextBlock FieldLabel(string text) => new()
    {
        Text = text, FontSize = 10, FontWeight = FontWeight.Bold,
        Foreground = Brush("#94A3B8"), Margin = new Thickness(0, 10, 0, -5)
    };

    private static Button ActionButton(string text, Action action)
    {
        var button = new Button
        {
            Content = text, Padding = new Thickness(13, 9),
            Background = Brush("#202C40"), Foreground = Brushes.White,
            BorderBrush = Brush("#334155"), BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8), FontSize = 12
        };
        button.Click += (_, _) => action();
        return button;
    }

    private static Button ActionButton(string text, Func<Task> action)
    {
        var button = ActionButton(text, () => _ = action());
        return button;
    }

    private static SolidColorBrush Brush(string color) => new(Color.Parse(color));
}

internal sealed class LiveWindow : Window
{
    private readonly TextBlock _title = new();
    private readonly TextBlock _body = new();

    public LiveWindow()
    {
        Title = "ChustaFlorg · Sortie publique";
        Width = 1280;
        Height = 720;
        Background = new SolidColorBrush(Color.Parse("#080B12"));
        WindowStartupLocation = WindowStartupLocation.CenterScreen;
        var content = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Spacing = 24,
            MaxWidth = 1100
        };
        _title.FontSize = 30;
        _title.FontWeight = FontWeight.SemiBold;
        _title.Foreground = new SolidColorBrush(Color.Parse("#D8B46A"));
        _title.HorizontalAlignment = HorizontalAlignment.Center;
        _body.FontSize = 56;
        _body.FontWeight = FontWeight.Bold;
        _body.Foreground = Brushes.White;
        _body.TextAlignment = TextAlignment.Center;
        _body.TextWrapping = TextWrapping.Wrap;
        content.Children.Add(_title);
        content.Children.Add(_body);
        Content = content;
        KeyDown += (_, e) =>
        {
            if (e.Key == Avalonia.Input.Key.Escape) Close();
        };
    }

    public void ShowSlide(Slide slide)
    {
        _title.Text = slide.Title;
        _body.Text = slide.Body;
    }
}
