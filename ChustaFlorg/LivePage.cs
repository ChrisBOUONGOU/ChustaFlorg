using ChustaFlorg.Models;

namespace ChustaFlorg;

public sealed class LivePage : ContentPage
{
    private readonly IReadOnlyList<Slide> _slides;
    private readonly Label _title;
    private readonly Label _body;
    private readonly Image _background;
    private readonly Grid _contentLayer;
    private int _index;

    public LivePage(IReadOnlyList<Slide> slides, int startIndex)
    {
        _slides = slides;
        _index = Math.Clamp(startIndex, 0, slides.Count - 1);
        BackgroundColor = Color.FromArgb("#080B12");

        _background = new Image { Aspect = Aspect.AspectFill, Opacity = 0.65, IsVisible = false };
        _title = new Label
        {
            FontSize = 26,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#D8B46A"),
            HorizontalTextAlignment = TextAlignment.Center
        };
        _body = new Label
        {
            FontSize = 42,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center
        };

        var previous = new Button { Text = "← Précédente", BackgroundColor = Color.FromArgb("#202C40"), TextColor = Colors.White };
        previous.Clicked += (_, _) => ShowSlide(_index - 1);
        var next = new Button { Text = "Suivante →", BackgroundColor = Color.FromArgb("#7A1F3D"), TextColor = Colors.White };
        next.Clicked += (_, _) => ShowSlide(_index + 1);
        var close = new Button { Text = "Fermer", BackgroundColor = Color.FromArgb("#30202A"), TextColor = Colors.White };
        close.Clicked += async (_, _) =>
        {
            var parentWindow = Application.Current?.Windows.FirstOrDefault(window => window.Page == this);
            if (parentWindow is not null)
                Application.Current?.CloseWindow(parentWindow);
            else
                await Navigation.PopModalAsync();
        };

        var controls = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 10 };
        controls.Add(previous, 0);
        controls.Add(next, 1);
        controls.Add(close, 2);

        _contentLayer = new Grid
        {
            Padding = 36,
            RowDefinitions = new RowDefinitionCollection { new(GridLength.Star), new(GridLength.Auto) },
            RowSpacing = 24,
            Children =
            {
                new VerticalStackLayout
                {
                    Spacing = 28,
                    VerticalOptions = LayoutOptions.Center,
                    HorizontalOptions = LayoutOptions.Center,
                    Children = { _title, _body }
                },
                controls
            }
        };
        Grid.SetRow(controls, 1);

        Content = new Grid
        {
            Children = { _background, _contentLayer }
        };
        ShowSlide(_index);
    }

    private async void ShowSlide(int index)
    {
        _index = Math.Clamp(index, 0, _slides.Count - 1);
        var slide = _slides[_index];

        try
        {
            switch (slide.Transition)
            {
                case "Fondu":
                    await _contentLayer.FadeTo(0, 120);
                    break;
                case "Glissement":
                    await _contentLayer.TranslateTo(35, 0, 120);
                    break;
            }

            BackgroundColor = Color.FromArgb(slide.BackgroundColor);
            _background.Source = !string.IsNullOrWhiteSpace(slide.BackgroundPath) && File.Exists(slide.BackgroundPath)
                ? ImageSource.FromFile(slide.BackgroundPath)
                : null;
            _background.IsVisible = _background.Source is not null;
            _title.Text = slide.Title;
            _body.Text = slide.Body;

            _contentLayer.TranslationX = slide.Transition == "Glissement" ? -35 : 0;
            await _contentLayer.FadeTo(1, slide.Transition == "Aucune" ? 0 : 180);
            if (slide.Transition == "Glissement")
                await _contentLayer.TranslateTo(0, 0, 180);
        }
        catch
        {
            _contentLayer.Opacity = 1;
            _contentLayer.TranslationX = 0;
        }
    }
}
