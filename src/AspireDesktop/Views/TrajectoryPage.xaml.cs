namespace AspireDesktop.Views;

public partial class TrajectoryPage : ContentPage
{
    public TrajectoryPage()
    {
        InitializeComponent();
        BindingContext = App.Session;
    }
}
