namespace AspireDesktop.Views;

public partial class OverviewPage : ContentPage
{
    public OverviewPage()
    {
        InitializeComponent();
        BindingContext = App.Session;
    }
}
