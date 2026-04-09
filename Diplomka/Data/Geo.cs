using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Data
{
    public class Geo
    {
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

        public override string ToString()
        {
            return $"{Latitude}, {Longitude}";
        }
    }
}
