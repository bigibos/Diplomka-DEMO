using Diplomka.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Diplomka.Routing
{
    public sealed class DistanceTable
    {
        private Dictionary<(Geo, Geo), RouteInfo> distances = new Dictionary<(Geo, Geo), RouteInfo>();

        private static DistanceTable instance;

        private DistanceTable() { }

        public static DistanceTable GetInstance()
        {
            if (instance == null)
                instance = new DistanceTable();

            return instance;
        }


        private static readonly HttpClient client = new HttpClient();

        public async Task<Dictionary<(Geo, Geo), RouteInfo>> GetDistanceMatrixAsync(List<Geo> locations)
        {
            var result = new Dictionary<(Geo, Geo), RouteInfo>();

            string coords = string.Join(";", locations.Select(l =>
                $"{l.Lon.ToString().Replace(',', '.')},{l.Lat.ToString().Replace(',', '.')}"
            ));

            string url = $"http://router.project-osrm.org/table/v1/driving/{coords}?annotations=distance,duration";

            string response = await client.GetStringAsync(url);


            using JsonDocument doc = JsonDocument.Parse(response);


            var distances = doc.RootElement.GetProperty("distances");
            var durations = doc.RootElement.GetProperty("durations");

            for (int i = 0; i < locations.Count; i++)
            {
                for (int j = 0; j < locations.Count; j++)
                {
                    if (i == j) continue;

                    double distanceMeters = distances[i][j].GetDouble();
                    double durationSeconds = durations[i][j].GetDouble();

                    result[(locations[i], locations[j])] = new RouteInfo(
                        DistanceKm: distanceMeters / 1000,
                        DurationMinutes: durationSeconds / 60
                    );
                }
            }

            return result;
        }

        // Vybudovani tabulky vzdalenosti mezi vsemi lokacemi
        public async Task Initialize(IEnumerable<Geo> locations)
        {
            var locationList = locations.ToList();


            distances = await GetDistanceMatrixAsync(locationList);
        }

        public RouteInfo GetRouteInfo(Geo from, Geo to)
        {
            if (from.Equals(to))
                return new RouteInfo(0, 0);


            return distances[(from, to)];
        }

        // Ziskani vzdalenosti z tabulky, nebo pres API, pokud chybi
        public async Task<RouteInfo> GetRouteInfoAsync(Geo from, Geo to)
        {
            if (distances.TryGetValue((from, to), out var info))
            {
                Console.WriteLine($"Cache hit for {from} -> {to}: {info.DistanceKm} km, {info.DurationMinutes} min");   
                return info;
            }
            else
            {
                var routeInfo = await from.GetRoadRouteToAsync(to);
                if (routeInfo == null)
                {
                    Console.WriteLine($"OSRM failed for {from} -> {to}, using straight-line distance as fallback.");
                    // Pokud OSRM nenajde cestu, použijeme vzdušnou vzdálenost jako záložní
                    routeInfo = new RouteInfo(from.DistanceTo(to), from.DistanceTo(to) / 60); // Předpokládejme průměrnou rychlost 60 km/h
                }
                distances[(from, to)] = routeInfo;
                Console.WriteLine($"Cache miss for {from} -> {to}: {routeInfo.DistanceKm} km, {routeInfo.DurationMinutes} min");
                return routeInfo;
            }
        }


        public override string ToString()
        {
            string result = "Distance Table:\n";
            foreach (var entry in distances)
            {
                result += $"{entry.Key.Item1} -> {entry.Key.Item2}: {entry.Value.DistanceKm:F2} km, {entry.Value.DurationMinutes:F2} min\n";
            }
            return result;
        }
    }
}
