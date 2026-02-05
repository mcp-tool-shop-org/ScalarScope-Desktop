using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input;
using Windows.System;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace ScalarScope.WinUI;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : MauiWinUIApplication
{
	private bool _keyboardHooked = false;

	/// <summary>
	/// Initializes the singleton application object.  This is the first line of authored code
	/// executed, and as such is the logical equivalent of main() or WinMain().
	/// </summary>
	public App()
	{
		this.InitializeComponent();
	}

	protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

	protected override void OnLaunched(LaunchActivatedEventArgs args)
	{
		base.OnLaunched(args);

		// Try to hook keyboard immediately, and also set up retry via dispatcher
		TryHookKeyboard();

		// Also schedule a retry after the UI is fully loaded
		Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread()?.TryEnqueue(
			Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
			() => TryHookKeyboard());
	}

	private void TryHookKeyboard()
	{
		if (_keyboardHooked) return;

		try
		{
			// Try to get the native WinUI window
			if (Microsoft.Maui.MauiWinUIApplication.Current?.Application?.Windows?.Count > 0)
			{
				var mauiWindow = Microsoft.Maui.MauiWinUIApplication.Current.Application.Windows[0];
				var nativeWindow = mauiWindow?.Handler?.PlatformView as Microsoft.UI.Xaml.Window;

				if (nativeWindow?.Content is Microsoft.UI.Xaml.UIElement content)
				{
					content.KeyDown += OnKeyDown;
					content.PreviewKeyDown += OnPreviewKeyDown;
					_keyboardHooked = true;
				}
			}
		}
		catch
		{
			// Window not ready yet, will retry
		}
	}

	private void OnPreviewKeyDown(object sender, KeyRoutedEventArgs e)
	{
		// PreviewKeyDown fires before the event reaches child elements
		// This catches keys that might otherwise be swallowed
		HandleKey(e);
	}

	private void OnKeyDown(object sender, KeyRoutedEventArgs e)
	{
		HandleKey(e);
	}

	private void HandleKey(KeyRoutedEventArgs e)
	{
		if (e.Handled) return;

		// Don't handle if a text input has focus
		if (e.OriginalSource is Microsoft.UI.Xaml.Controls.TextBox)
			return;

		var ctrl = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Control).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
		var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift).HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

		var keyName = e.Key switch
		{
			VirtualKey.Space => "Space",
			VirtualKey.Left => "Left",
			VirtualKey.Right => "Right",
			VirtualKey.Home => "Home",
			VirtualKey.End => "End",
			VirtualKey.Up => "Up",
			VirtualKey.Down => "Down",
			VirtualKey.Add => "Add",
			VirtualKey.Subtract => "Subtract",
			VirtualKey.Number0 => "D0",
			VirtualKey.Number1 => "D1",
			VirtualKey.Number2 => "D2",
			VirtualKey.Number3 => "D3",
			VirtualKey.Number4 => "D4",
			VirtualKey.Number5 => "D5",
			VirtualKey.Number6 => "D6",
			VirtualKey.S => "S",
			VirtualKey.A => "A",
			_ => null
		};

		// Handle +/- on main keyboard (OemPlus/OemMinus)
		if (keyName == null)
		{
			keyName = (int)e.Key switch
			{
				187 => "OemPlus",  // = / + key
				189 => "OemMinus", // - / _ key
				191 => "OemQuestion", // ? key
				_ => null
			};
		}

		if (keyName != null)
		{
			try
			{
				var handled = ScalarScope.App.Keyboard.HandleKeyPress(keyName, ctrl, shift);
				if (handled)
				{
					e.Handled = true;
				}
			}
			catch
			{
				// Keyboard service not ready yet
			}
		}
	}
}

