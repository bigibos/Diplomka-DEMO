using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Diplomka.Routing
{
    public class DistanceTable
    {
        private Dictionary<(Geo, Geo), RouteInfo> _distances = new Dictionary<(Geo, Geo), RouteInfo>();

        private static readonly HttpClient _client = new HttpClient();

        // Asynchroni volani OSRM API pro ziskani matice vzdalenosti mezi vsemi lokacemi
        private async Task<Dictionary<(Geo, Geo), RouteInfo>> GetDistanceMatrixAsync(List<Geo> locations)
        {
            var result = new Dictionary<(Geo, Geo), RouteInfo>();

            string coords = string.Join(";", locations.Select(l =>
                $"{l.Lon.ToString().Replace(',', '.')},{l.Lat.ToString().Replace(',', '.')}"
            ));

            string url = $"http://router.project-osrm.org/table/v1/driving/{coords}?annotations=distance,duration";

            string response = await _client.GetStringAsync(url);


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
                        From: locations[i],
                        To: locations[j],
                        DistanceKm: distanceMeters / 1000,
                        Duration: TimeSpan.FromSeconds(durationSeconds)
                    );
                }
            }

            return result;
        }

        // Inicializace tabulky vzdalenosti pro zadany seznam lokaci
        public async Task Initialize(IEnumerable<Geo> locations)
        {
            var locationList = locations.ToList();


            _distances = await GetDistanceMatrixAsync(locationList);
        }

        // Diskani vzdalenosti z tabulky
        public RouteInfo? GetRouteInfo(Geo from, Geo to)
        {
            if (from.Equals(to))
                return new RouteInfo(from, to, 0, TimeSpan.Zero); // Stejna lokace, vzdalenost i cas jsou nula

            if (_distances.TryGetValue((from, to), out var info))
                return info;

            return null;
        }

        // Ziskani vzdalenosti z tabulky, nebo pres API, pokud chybi
        public async Task<RouteInfo> GetRouteInfoAsync(Geo from, Geo to)
        {
            if (_distances.TryGetValue((from, to), out var info))
            {  
                return info;
            }
            else
            {
                var routeInfo = await from.GetRoadRouteToAsync(to);
                if (routeInfo == null)
                {
                    var timeInMinutes = from.DistanceTo(to) / 60;
                    // Pokud OSRM nenajde cestu, použijeme vzdušnou vzdálenost jako záložní
                    routeInfo = new RouteInfo(from, to, from.DistanceTo(to), TimeSpan.FromMinutes(timeInMinutes)); // Předpokládejme průměrnou rychlost 60 km/h
                }
                _distances[(from, to)] = routeInfo;
                return routeInfo;
            }
        }


        public override string ToString()
        {
            string result = "Distance Table:\n";
            foreach (var entry in _distances)
            {
                result += $"{entry.Key.Item1} -> {entry.Key.Item2}: {entry.Value.DistanceKm:F2} km, {entry.Value.Duration:F2} min\n";
            }
            return result;
        }
    }
}
