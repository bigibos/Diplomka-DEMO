using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
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

        // Maximalni pocet lokaci v jednom OSRM requestu (verejny server ma limit 100)
        private const int ChunkSize = 90;

        // Asynchronni volani OSRM API pro ziskani matice vzdalenosti mezi vsemi lokacemi
        // Lokace jsou rozdeleny do davek, aby se neprekrocil limit OSRM verejneho serveru
        private async Task<Dictionary<(Geo, Geo), RouteInfo>> GetDistanceMatrixAsync(List<Geo> locations)
        {
            var result = new Dictionary<(Geo, Geo), RouteInfo>();

            // Rozdeleni lokaci do chunků po ChunkSize
            var chunks = locations
                .Select((loc, i) => (loc, i))
                .GroupBy(x => x.i / ChunkSize)
                .Select(g => g.Select(x => x.loc).ToList())
                .ToList();

            int totalCalls = chunks.Count * chunks.Count;
            int callsDone = 0;

            Console.WriteLine($"[DistanceTable] Inicialization: {locations.Count} locations, {totalCalls} OSRM requests");

            foreach (var sourceChunk in chunks)
            {
                foreach (var destChunk in chunks)
                {
                    // Sestaveni sjednoceneho seznamu lokaci pro tento request
                    // (Union zachova poradi a odstrani duplicity pres Equals/GetHashCode)
                    var allLocs = sourceChunk.Union(destChunk).ToList();

                    // Indexy sources a destinations v ramci allLocs
                    var sourceIndices = string.Join(";", sourceChunk.Select(l => allLocs.IndexOf(l)));
                    var destIndices = string.Join(";", destChunk.Select(l => allLocs.IndexOf(l)));

                    // InvariantCulture — zarucuje tecku jako desetinny oddelovac bez ohledu na nastaveni systemu
                    string coords = string.Join(";", allLocs.Select(l =>
                        $"{l.Lon.ToString(CultureInfo.InvariantCulture)},{l.Lat.ToString(CultureInfo.InvariantCulture)}"
                    ));

                    string url = $"http://router.project-osrm.org/table/v1/driving/{coords}" +
                                 $"?sources={sourceIndices}&destinations={destIndices}&annotations=distance,duration";

                    string response = await _client.GetStringAsync(url);

                    using JsonDocument doc = JsonDocument.Parse(response);

                    // Kontrola ze OSRM vratil uspesnou odpoved
                    if (doc.RootElement.TryGetProperty("code", out var codeEl) &&
                        codeEl.GetString() != "Ok")
                    {
                        string msg = doc.RootElement.TryGetProperty("message", out var msgEl)
                            ? msgEl.GetString() ?? ""
                            : "Unknown OSRM error";
                        throw new InvalidOperationException($"[DistanceTable] OSRM error: {msg}");
                    }

                    var distances = doc.RootElement.GetProperty("distances");
                    var durations = doc.RootElement.GetProperty("durations");

                    // Odpoved je matice |sourceChunk| x |destChunk|
                    for (int i = 0; i < sourceChunk.Count; i++)
                    {
                        for (int j = 0; j < destChunk.Count; j++)
                        {
                            var from = sourceChunk[i];
                            var to = destChunk[j];

                            if (from.Equals(to)) continue;

                            // OSRM muze vratit null pro nedosazitelne lokace (napr. ostrov bez mostu)
                            if (distances[i][j].ValueKind == JsonValueKind.Null ||
                                durations[i][j].ValueKind == JsonValueKind.Null)
                            {
                                // Zalozni vzdusna vzdalenost pri 60 km/h
                                double fallbackKm = from.DistanceTo(to);
                                double fallbackMin = fallbackKm / 60.0;
                                result[(from, to)] = new RouteInfo(
                                    From: from,
                                    To: to,
                                    DistanceKm: fallbackKm,
                                    Duration: TimeSpan.FromMinutes(fallbackMin)
                                );
                                continue;
                            }

                            double distanceMeters = distances[i][j].GetDouble();
                            double durationSeconds = durations[i][j].GetDouble();

                            result[(from, to)] = new RouteInfo(
                                From: from,
                                To: to,
                                DistanceKm: distanceMeters / 1000.0,
                                Duration: TimeSpan.FromSeconds(durationSeconds)
                            );
                        }
                    }

                    callsDone++;
                    Console.WriteLine($"[DistanceTable] {callsDone}/{totalCalls} done");

                    // Prodleva mezi requesty — ochrana pred rate limitingem verejneho serveru
                    if (callsDone < totalCalls)
                        await Task.Delay(300);
                }
            }

            Console.WriteLine($"[DistanceTable] Complete. {result.Count} pairs cached.");
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
                return new RouteInfo(from, to, 0, TimeSpan.Zero);

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
                    routeInfo = new RouteInfo(from, to, from.DistanceTo(to), TimeSpan.FromMinutes(timeInMinutes));
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