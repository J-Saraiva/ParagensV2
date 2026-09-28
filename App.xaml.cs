using Microsoft.Extensions.DependencyInjection;

namespace ParagensV2;

public partial class App : Application
{
	public App()
	{
		InitializeComponent();
		Services.ThemeService.Instance.Initialize();
	}

	protected override Window CreateWindow(IActivationState? activationState)
	{
		return new Window(new AppShell());
	}
}
