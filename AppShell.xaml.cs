namespace ParagensV2;

public partial class AppShell : Shell
{
	public AppShell()
	{
		InitializeComponent();
		Routing.RegisterRoute(nameof(Views.MapPage), typeof(Views.MapPage));
		Routing.RegisterRoute(nameof(Views.LineStopsPage), typeof(Views.LineStopsPage));
		Routing.RegisterRoute(nameof(Views.TrainStationDeparturesPage), typeof(Views.TrainStationDeparturesPage));
		Routing.RegisterRoute(nameof(Views.TrainMapPage), typeof(Views.TrainMapPage));
		Routing.RegisterRoute(nameof(Views.UnirLineStopsPage), typeof(Views.UnirLineStopsPage));
		Routing.RegisterRoute(nameof(Views.UnirStopDeparturesPage), typeof(Views.UnirStopDeparturesPage));
		Routing.RegisterRoute(nameof(Views.MetroLineStopsPage), typeof(Views.MetroLineStopsPage));
		Routing.RegisterRoute(nameof(Views.MetroStationDeparturesPage), typeof(Views.MetroStationDeparturesPage));
	}
}
