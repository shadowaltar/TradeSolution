namespace TradeApp.ViewModels.Presets;

public class FavoriteViewModel : AbstractViewModel
{
    private string _caption;

    public string Caption { get => _caption; set => SetValue(ref _caption, value); }

    public FavoriteViewModel()
    {
        Caption = "Favorite";
    }
}
