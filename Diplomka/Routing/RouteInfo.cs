using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Routing
{
    public record RouteInfo(
        Geo From,
        Geo To,
        double DistanceKm,
        TimeSpan Duration
    );
}
