using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Diplomka.Entity
{
    /// <summary>
    /// Datová entita pro uchovávání geolokačních dat.
    /// Využívá se v jiných datových entitách a primárně se s ní pracuje při výpočtech tras
    /// </summary>
    public class Geo
    {

        /// <summary>
        /// Zeměpisná šířka
        /// </summary>
        ///<example>
        /// Zeměpisná šířka města Pardubice: 50.03658
        /// </example>
        [Range(-90, 90)]
        public double Lat { get; set; }

        /// <summary>
        /// Zeměpisná délka
        /// </summary>
        /// <example>
        /// Zeměpisná délka města Pardubice: 15.77679
        /// </example>
        [Range(-180, 180)]
        public double Lon { get; set; }

        public Geo(double lat, double lon)
        {
            Lat = lat;
            Lon = lon;
        }

        /// <summary>
        /// Metoda pro nalezení vzdušné vzdálenosti k jiné lokaci pomocí Haversinova vzorce
        /// </summary>
        /// <param name="other">Cílová lokace</param>
        /// <returns>Vzdálenost v kilometrech</returns>
        public double DistanceTo(Geo other)
        {
            const double earthRadiusKm = 6371; // Polomer zeme v km
            
            double deltaLat = double.DegreesToRadians(other.Lat - Lat);
            double deltaLon = double.DegreesToRadians(other.Lon - Lon);

            double lat = double.DegreesToRadians(Lat);
            double latOther = double.DegreesToRadians(other.Lat);

            double a = Math.Sin(deltaLat / 2) * Math.Sin(deltaLat / 2) +
                       Math.Cos(lat) * Math.Cos(latOther) *
                       Math.Sin(deltaLon / 2) * Math.Sin(deltaLon / 2);

            double centralAngle = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return earthRadiusKm * centralAngle;
        }

        /// <summary>
        /// Asynchronně získává trasové údaje k jiné lokaci pomocí veřejné OSRM API
        /// </summary>
        /// <param name="other">Cílová lokace</param>
        /// <returns>
        /// Task s trasovým údajem <see cref="RouteInfo"/>, pokud je nalezen,
        /// nebo <see langword="null"/> pokud není.
        /// </returns>
        public async Task<RouteInfo?> TravelDistanceTo(Geo other)
        {
            using HttpClient client = new HttpClient();

            // Formatovaní cesty k API
            string url = string.Format(
                CultureInfo.InvariantCulture,
                "http://router.project-osrm.org/route/v1/driving/{0},{1};{2},{3}?overview=false",
                Lon, Lat, other.Lon, other.Lat
                );

            try
            {
                // Získání odpovědi a kontrola
                var response = await client.GetAsync(url);
                if (response.StatusCode != HttpStatusCode.OK)
                    return null;

                // Získání těla odpovědi
                string body = await response.Content.ReadAsStringAsync();
                using var json = JsonDocument.Parse(body);

                // Kontrola zdali trasa byla nalezena
                string code = json.RootElement.GetProperty("code").GetString() ?? "";
                if (code.ToLower() != "ok")
                    return null;

                // Vybrani prvni nejlepsi trasy
                var route = json.RootElement.GetProperty("routes")[0];

                // Ziskani casovych a vzdalenostnich údajů
                double distanceMeters = route.GetProperty("distance").GetDouble();
                double durationSeconds = route.GetProperty("duration").GetDouble();


                return new RouteInfo
                {
                    To = other,
                    From = this,
                    DistanceKm = distanceMeters / 1000,
                    Duration = TimeSpan.FromSeconds(durationSeconds)
                };
            }
            catch (HttpRequestException)
            {
                return null; // Doslo k cybe site
            }
        }

        /// <summary>
        /// Převede údaje o zeměpisné šířce a délce do decimálních stupňů (DD - Decimal Degrees).
        /// Díky tomu je možné geolokační údaje jednoduše přenést do jiných aplikací (např. mapy)
        /// </summary>
        /// <returns>Čítelný formát v podobě řetězce</returns>
        public string ToReadableDecimal()
        {
            string latDir = Lat >= 0 ? "N" : "S";
            string lonDir = Lon >= 0 ? "E" : "W";

            return $"{Math.Abs(Lat):F5}° {latDir}, {Math.Abs(Lon):F5}° {lonDir}";
        }


        public override bool Equals(object? obj)
        {
            if (obj is Geo other)
            {
                return Lat.Equals(other.Lat) && Lon.Equals(other.Lon);
            }
            return false;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Lat, Lon);
        }

        public override string ToString()
        {
            return $"{Lat:F4}, {Lon:F4}";
        }
    }
}
