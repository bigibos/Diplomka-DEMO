using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Diplomka.Routing
{
    public class Geo
    {

        // Zeměpisná šířka (např. 50.0878)
        public double Lat { get; set; }

        // Zeměpisná délka (např. 14.4205)
        public double Lon { get; set; }

        public Geo(double lat, double lon)
        {
            Lat = lat;
            Lon = lon;
        }
        public double DistanceTo(Geo other)
        {
            double r = 6371; // Poloměr Země v km
            
            double dLat = double.DegreesToRadians(other.Lat - Lat);
            double dLon = double.DegreesToRadians(other.Lon - Lon);

            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                       Math.Cos(double.DegreesToRadians(Lat)) * Math.Cos(double.DegreesToRadians(other.Lat)) *
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
                         $"{Lon.ToString().Replace(',', '.')},{Lat.ToString().Replace(',', '.')};" +
                         $"{other.Lon.ToString().Replace(',', '.')},{other.Lat.ToString().Replace(',', '.')}" +
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
