using AspireDesktop.ViewModels;

namespace AspireDesktop;

public partial class App : Application
{
    public static VortexSessionViewModel Session { get; } = new();

    public App()
    {
        InitializeComponent();
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var shell = new AppShell();
        shell.BindingContext = Session;
        return new Window(shell);
    }
}
