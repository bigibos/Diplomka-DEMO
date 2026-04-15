using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Diplomka.Data
{
    public class Geo
    {
        public record RouteInfo(double DistanceKm, double DurationMinutes);

        // Zeměpisná šířka (např. 50.0878)
        public double Latitude { get; private set; }

        // Zeměpisná délka (např. 14.4205)
        public double Longitude { get; private set; }

        public Geo(double latitude, double longitude)
        {
            Latitude = latitude;
            Longitude = longitude;
        }
        public double DistanceTo(Geo other)
        {
            double r = 6371; // Poloměr Země v km
            
            double dLat = Double.DegreesToRadians(other.Latitude - this.Latitude);
            double dLon = Double.DegreesToRadians(other.Longitude - this.Longitude);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(Double.DegreesToRadians(this.Latitude)) * Math.Cos(Double.DegreesToRadians(other.Latitude)) *
                       Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return r * c;
        }

        public async Task<RouteInfo?> GetRoadRouteToAsync(Geo other)
        {
            using HttpClient client = new HttpClient();

            // OSRM API vyžaduje formát: longitude,latitude;longitude,latitude
            // Pozor: OSRM má prohozené pořadí oproti běžnému Lat,Lon!
            string url = $"http://router.project-osrm.org/route/v1/driving/" +
                         $"{this.Longitude.ToString().Replace(',', '.')},{this.Latitude.ToString().Replace(',', '.')};" +
                         $"{other.Longitude.ToString().Replace(',', '.')},{other.Latitude.ToString().Replace(',', '.')}" +
                         $"?overview=false";

            try
            {
                string response = await client.GetStringAsync(url);
                using JsonDocument doc = JsonDocument.Parse(response);

                // Kontrola, zda API našlo cestu
                string code = doc.RootElement.GetProperty("code").GetString();
                if (code != "Ok") return null;

                // Získání první (nejlepší) trasy
                var route = doc.RootElement.GetProperty("routes")[0];

                double distanceMeters = route.GetProperty("distance").GetDouble();
                double durationSeconds = route.GetProperty("duration").GetDouble();

                return new RouteInfo(
                    DistanceKm: distanceMeters / 1000,
                    DurationMinutes: durationSeconds / 60
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Chyba při volání API: {ex.Message}");
                return null;
            }
        }

        public override string ToString()
        {
            return $"{Latitude}, {Longitude}";
        }
    }
}
