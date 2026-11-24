using ChustaFlorg.Models;

namespace ChustaFlorg;

public enum LiveScreenRole
{
    Public,
    Operator,
    Stage
}

public sealed class LiveProductionSession
{
    public IReadOnlyList<Slide> Slides { get; }
    public int CurrentIndex { get; private set; }
    public event Action? Changed;

    public LiveProductionSession(IReadOnlyList<Slide> slides, int startIndex)
    {
        Slides = slides;
        CurrentIndex = Math.Clamp(startIndex, 0, Math.Max(0, slides.Count - 1));
    }

    public void Move(int offset)
    {
        if (Slides.Count == 0) return;
        var next = Math.Clamp(CurrentIndex + offset, 0, Slides.Count - 1);
        if (next == CurrentIndex) return;
        CurrentIndex = next;
        Changed?.Invoke();
    }

    public void GoTo(int index)
    {
        if (Slides.Count == 0) return;
        var next = Math.Clamp(index, 0, Slides.Count - 1);
        if (next == CurrentIndex) return;
        CurrentIndex = next;
        Changed?.Invoke();
    }
}

public sealed class LivePage : ContentPage
{
    private readonly LiveProductionSession _session;
    private readonly LiveScreenRole _role;
    private readonly Grid _root = new();
    private readonly Image _background = new() { Aspect = Aspect.AspectFill, Opacity = 0.72, IsVisible = false };
    private readonly Label _title = new() { FontSize = 26, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#D8B46A"), HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _body = new() { FontSize = 42, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };
    private readonly Label _counter = new() { FontSize = 13, TextColor = Color.FromArgb("#CBD5E1"), HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _nextTitle = new() { FontSize = 20, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#D8B46A"), HorizontalTextAlignment = TextAlignment.Center };
    private readonly Label _nextBody = new() { FontSize = 27, FontAttributes = FontAttributes.Bold, TextColor = Colors.White, HorizontalTextAlignment = TextAlignment.Center, VerticalTextAlignment = TextAlignment.Center };

    public LivePage(LiveProductionSession session, LiveScreenRole role)
    {
        _session = session;
        _role = role;
        BackgroundColor = Color.FromArgb("#080B12");
        Title = role switch
        {
            LiveScreenRole.Public => "Écran public",
            LiveScreenRole.Operator => "Écran opérateur",
            _ => "Écran de scène"
        };

        BuildLayout();
        _session.Changed += OnSessionChanged;
        UpdateScreen();
    }

    private void BuildLayout()
    {
        if (_role == LiveScreenRole.Public)
        {
            var publicContent = new VerticalStackLayout
            {
                Padding = 50,
                Spacing = 28,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Children = { _title, _body }
            };
            _root.Children.Add(_background);
            _root.Children.Add(publicContent);
        }
        else if (_role == LiveScreenRole.Stage)
        {
            var heading = new Label { Text = "ÉCRAN DE SCÈNE · DIAPOSITIVE ACTUELLE ET SUIVANTE", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#D8B46A"), HorizontalTextAlignment = TextAlignment.Center };
            var current = new VerticalStackLayout { Spacing = 12, Children = { new Label { Text = "EN COURS", FontSize = 12, TextColor = Color.FromArgb("#94A3B8"), HorizontalTextAlignment = TextAlignment.Center }, _title, _body } };
            var next = new VerticalStackLayout { Spacing = 12, Children = { new Label { Text = "À SUIVRE", FontSize = 12, TextColor = Color.FromArgb("#94A3B8"), HorizontalTextAlignment = TextAlignment.Center }, _nextTitle, _nextBody } };
            var columns = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 36, Padding = 30 };
            columns.Add(current, 0);
            columns.Add(next, 1);
            _root.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            _root.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            _root.Add(heading, 0, 0);
            _root.Add(columns, 0, 1);
        }
        else
        {
            var previous = new Button { Text = "← Précédente", BackgroundColor = Color.FromArgb("#202C40"), TextColor = Colors.White };
            previous.Clicked += (_, _) => _session.Move(-1);
            var next = new Button { Text = "Suivante →", BackgroundColor = Color.FromArgb("#7A1F3D"), TextColor = Colors.White, FontAttributes = FontAttributes.Bold };
            next.Clicked += (_, _) => _session.Move(1);
            var close = new Button { Text = "Fermer", BackgroundColor = Color.FromArgb("#30202A"), TextColor = Colors.White };
            close.Clicked += (_, _) => CloseThisWindow();

            var controls = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 10 };
            controls.Add(previous, 0);
            controls.Add(next, 1);
            controls.Add(close, 2);

            var content = new VerticalStackLayout
            {
                Padding = 32,
                Spacing = 20,
                Children = { new Label { Text = "ÉCRAN OPÉRATEUR", FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = Color.FromArgb("#D8B46A") }, _title, _body, _counter, controls }
            };
            content.VerticalOptions = LayoutOptions.Center;
            _root.Children.Add(content);
        }

        if (_role == LiveScreenRole.Public)
        {
            _root.Children.Add(new Grid
            {
                Children =
                {
                    new VerticalStackLayout
                    {
                        Padding = 50,
                        Spacing = 28,
                        HorizontalOptions = LayoutOptions.Center,
                        VerticalOptions = LayoutOptions.Center,
                        Children = { _title, _body }
                    }
                }
            });
        }

        Content = _root;
    }

    private void OnSessionChanged() => MainThread.BeginInvokeOnMainThread(UpdateScreen);

    private void UpdateScreen()
    {
        if (_session.Slides.Count == 0) return;
        var slide = _session.Slides[_session.CurrentIndex];
        BackgroundColor = Color.FromArgb(slide.BackgroundColor);
        _background.Source = !string.IsNullOrWhiteSpace(slide.BackgroundPath) && File.Exists(slide.BackgroundPath)
            ? ImageSource.FromFile(slide.BackgroundPath)
            : null;
        _background.IsVisible = _background.Source is not null;

        _title.Text = slide.Title;
        _body.Text = slide.Body;
        _title.TextColor = Color.FromArgb(slide.TextColor);
        _body.TextColor = Color.FromArgb(slide.TextColor);
        _counter.Text = $"Diapositive {_session.CurrentIndex + 1} / {_session.Slides.Count}";

        var nextIndex = _session.CurrentIndex + 1;
        if (nextIndex < _session.Slides.Count)
        {
            var nextSlide = _session.Slides[nextIndex];
            _nextTitle.Text = nextSlide.Title;
            _nextBody.Text = nextSlide.Body;
        }
        else
        {
            _nextTitle.Text = "Fin de la présentation";
            _nextBody.Text = "Aucune diapositive suivante";
        }
    }

    private void CloseThisWindow()
    {
        var window = Application.Current?.Windows.FirstOrDefault(candidate => candidate.Page == this);
        if (window is not null)
            Application.Current?.CloseWindow(window);
        else
            MainThread.BeginInvokeOnMainThread(async () => await Navigation.PopModalAsync());
    }

    protected override void OnDisappearing()
    {
        _session.Changed -= OnSessionChanged;
        base.OnDisappearing();
    }
}