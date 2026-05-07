using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Diplomka.Routing
{
    /// <summary>
    /// Tabulka (matice) tras pro rychlejší přístupy bez nutnosti opakovaného volání OSRM API.
    /// Využívá se zde možnosti dávkového (chunk) získávání matice vzdálenosti přes OSRM API, což je využito při inicializace této tabulky.
    /// </summary>
    public class RouteTable
    {
        private Dictionary<(Geo, Geo), RouteInfo> _routes = new Dictionary<(Geo, Geo), RouteInfo>();

        private static readonly HttpClient _client = new HttpClient();

        private const string OsrmUrl = "http://router.project-osrm.org/table/v1/driving/";
        private const double FallbackSpeedKmh = 60;
        private const int RequestDelayMs = 300;
        private const int ChunkSize = 90;

        /// <summary>
        /// Hlavní metoda pro budování tabulky tras.
        /// Trasy se získávájí mezi všemi vzádlenostmi, které jsou poskytnuty.
        ///     1) Rozloží lokace do chunků
        ///     2) Naofrmátuje cestu požadavku a získá odpověď z OSRM v podobě matice vzádelností
        ///     3) Z odpovědi vybuduje část tabulky tras
        ///     4) Opakuje se dokud nejsou všechny chunky zpracovány
        /// </summary>
        /// <param name="locations">Seznam lokací pro vybudování tabulky tras</param>
        /// <returns>
        /// Výsledná tabulka tras v podobě <see cref="Dictionary{TKey, TValue}"/>,
        /// kde klíčem je dvojice lokací <see cref="Geo"/> a hodnotou <see cref="RouteInfo"/>
        /// </returns>
        /// <exception cref="InvalidOperationException">Při selhání spojení s ORM API, nebo když se z ní nepodaří získat správnou odpověď</exception>
        private async Task<Dictionary<(Geo, Geo), RouteInfo>> GetRouteTableAsync(IEnumerable<Geo> locations)
        {
            var routes = new Dictionary<(Geo, Geo), RouteInfo>();

            // Rozdeleni lokaci do chunků po ChunkSize
            var chunks = locations
                .Select((loc, i) => (loc, i))
                .GroupBy(x => x.i / ChunkSize)
                .Select(g => g.Select(x => x.loc).ToList())
                .ToList();

            int totalCalls = chunks.Count * chunks.Count;
            int callsDone = 0;

            // Console.WriteLine($"[DistanceTable] Inicialization: {locations.Count} locations, {totalCalls} OSRM requests");


            // Buduje se tabulka tras mezi vsemi lokacemi (kazdy s kazdym)
            foreach (var sourceChunk in chunks)
            {
                foreach (var destChunk in chunks)
                {
                    // Sestaveni sjednoceneho seznamu lokaci pro tento request
                    // Union zachova poradi a odstrani duplicity
                    var allLocs = sourceChunk.Union(destChunk).ToList();

                    // Vytvoreni indexace potrebne pro volani OSRM API
                    var sourceIndices = string.Join(";", sourceChunk.Select(l => allLocs.IndexOf(l)));
                    var destIndices = string.Join(";", destChunk.Select(l => allLocs.IndexOf(l)));

                    // Formatovani souradnic
                    string coords = string.Join(";", allLocs.Select(l =>
                        string.Format(CultureInfo.InvariantCulture, "{0},{1}", l.Lon, l.Lat)
                    ));

                    // Sestaveni cesty k pozadavku do OSRM API
                    string url = $"{OsrmUrl}{coords}" +
                                 $"?sources={sourceIndices}&destinations={destIndices}&annotations=distance,duration";

                    // Ziskani odpovedi a kontrola
                    var response = await _client.GetAsync(url);
                    if (response.StatusCode != HttpStatusCode.OK)
                        throw new InvalidOperationException("Chyba při posílání požadavku na OSRM API");

                    // Ziskani tela odpovedi
                    string body = await response.Content.ReadAsStringAsync();
                    using var json = JsonDocument.Parse(body);

                    // Kontrola zdali matice z OSRM byla nalezena a vytvorena
                    string code = json.RootElement.GetProperty("code").GetString() ?? "";
                    if (code.ToLower() != "ok")
                    {
                        string message = json.RootElement.TryGetProperty("message", out var jsonMessage)
                            ? jsonMessage.GetString() ?? "Neznámá chyba"
                            : "Neznámá chyba";
                        throw new InvalidOperationException($"Chyba OSRM API: {message}");
                    }

                    // Ziskani vzdalenosti a casu
                    var distances = json.RootElement.GetProperty("distances");
                    var durations = json.RootElement.GetProperty("durations");

                    // Sestaveni casti chunku tabulky ze ziskanych udaju
                    for (int i = 0; i < sourceChunk.Count; i++)
                    {
                        for (int j = 0; j < destChunk.Count; j++)
                        {
                            var from = sourceChunk[i];
                            var to = destChunk[j];

                            if (from.Equals(to)) continue;

                            // OSRM muze vratit null pro nedosazitelne lokace
                            if (distances[i][j].ValueKind == JsonValueKind.Null || durations[i][j].ValueKind == JsonValueKind.Null)
                            {
                                // Zalozni vzdusna vzdalenost s nastevnou prumernou rychlosti
                                double fallbackKm = from.DistanceTo(to);
                                double fallbackMin = fallbackKm / FallbackSpeedKmh;
                                routes[(from, to)] = new RouteInfo
                                {
                                    From = from,
                                    To = to,
                                    DistanceKm = fallbackKm,
                                    Duration = TimeSpan.FromMinutes(fallbackMin)
                                };
                                continue;
                            }

                            double distanceMeters = distances[i][j].GetDouble();
                            double durationSeconds = durations[i][j].GetDouble();

                            routes[(from, to)] = new RouteInfo
                            {
                                From = from,
                                To = to,
                                DistanceKm = distanceMeters / 1000.0,
                                Duration = TimeSpan.FromSeconds(durationSeconds)
                            };
                            
                        }
                    }

                    callsDone++;
                    // Console.WriteLine($"[DistanceTable] {callsDone}/{totalCalls} done");

                    // Prodleva mezi requesty — ochrana pred rate limitem OSRM API
                    if (callsDone < totalCalls)
                        await Task.Delay(RequestDelayMs);
                }
            }

            // Console.WriteLine($"[DistanceTable] Complete. {routes.Count} pairs cached.");
            return routes;
        }

        /// <summary>
        /// Inicializuje tabulku vzdáleností pomocí <see cref="GetRouteTableAsync(List{Geo})"/>
        /// </summary>
        /// <param name="locations">Kolekce lokací</param>
        /// <returns>Task pro asynchronní volání</returns>
        public async Task Initialize(IEnumerable<Geo> locations)
        {
            _routes = await GetRouteTableAsync(locations);
        }

        /// <summary>
        /// Získání trasových údajů mezi dvěma lokacemi pomocí trasové tabulky.
        /// </summary>
        /// <param name="from">Lokace ze které má vést trasa</param>
        /// <param name="to">Lokace do které má vést trasa</param>
        /// <returns>Trasové údaje mezi lokacemi</returns>
        public RouteInfo? GetRouteInfo(Geo from, Geo to)
        {
            // Start a cil trasy je stejna lokace
            if (from.Equals(to))
                return new RouteInfo{
                    From = from, 
                    To = to,
                    DistanceKm = 0, 
                    Duration = TimeSpan.Zero
                };

            // Ziskani trasy z tabulky
            if (_routes.TryGetValue((from, to), out var info))
                return info;

            return null;
        }

        public override string ToString()
        {
            string result = "Distance Table:\n";
            foreach (var entry in _routes)
            {
                result += $"{entry.Key.Item1} -> {entry.Key.Item2}: {entry.Value.DistanceKm:F2} km, {entry.Value.Duration:F2} min\n";
            }
            return result;
        }
    }
}