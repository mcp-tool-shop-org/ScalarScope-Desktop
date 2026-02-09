namespace ScalarScope.Views;

using ScalarScope.ViewModels;

public partial class WelcomePage : ContentPage
{
    public WelcomePage()
    {
        InitializeComponent();
        BindingContext = new WelcomeViewModel();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        
        // Refresh recent comparisons when page appears
        if (BindingContext is WelcomeViewModel vm)
        {
            vm.RefreshRecentComparisons();
        }
    }
}
