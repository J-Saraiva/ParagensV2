namespace ParagensV2.Models
{
    public class StcpNetwork
    {
        public List<StcpLine> Lines { get; set; } = new();
        public List<StcpStop> AllStops { get; set; } = new();
    }

    public class StcpLine
    {
        public string LineNumber { get; set; } = "";
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
        public List<double[]> RouteShape { get; set; } = new();
        public List<StcpStop> Stops { get; set; } = new();
        public List<double[]> ReturnRouteShape { get; set; } = new();
        public List<StcpStop> ReturnStops { get; set; } = new();
    }

    public class StcpStop
    {
        public string Code { get; set; } = "";
        public string Name { get; set; } = "";
        public double Lat { get; set; }
        public double Lng { get; set; }
        public string Zone { get; set; } = "PRT1";
    }
}
