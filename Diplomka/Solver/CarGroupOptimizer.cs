using Diplomka.Entity;

namespace Diplomka.Solver
{
    public class CarGroupOptimizer
    {
        public record CarGroup
        {
            public Slot Slot { get; init; } = null!;
            public Referee Driver { get; init; } = null!;
            public List<Referee> Passengers { get; init; } = new();
        }

        public List<CarGroup> Optimize(State state, out List<Referee> unpaired)
        {
            var groups = new List<CarGroup>();
            unpaired = new List<Referee>();

            var matchGroups = state
                .Where(kv => kv.Value != null)
                .GroupBy(kv => (kv.Key.Start, kv.Key.End, kv.Key.Location))
                .ToList();

            foreach (var matchGroup in matchGroups)
            {
                var slot = matchGroup.First().Key;
                var referees = matchGroup.Select(kv => kv.Value!).ToList();

                var drivers = referees.Where(r => r.HasCar).ToList();
                var passengers = referees.Where(r => !r.HasCar).ToList();

                if (passengers.Count == 0) continue;

                foreach (var passenger in passengers)
                {
                    var passengerOrigin = GetOrigin(passenger, slot, state);
                    var passengerDest = GetDestination(passenger, slot, state);

                    if (passengerOrigin == null || passengerDest == null)
                    {
                        unpaired.Add(passenger);
                        continue;
                    }

                    // Hledáme řidiče se shodným výchozím i cílovým bodem
                    var matchingDriver = drivers.FirstOrDefault(d =>
                    {
                        var driverOrigin = GetOrigin(d, slot, state);
                        var driverDest = GetDestination(d, slot, state);

                        // Pokud kterýkoliv z bodů chybí, párování není možné
                        if (passengerOrigin == null || passengerDest == null) return false;
                        if (driverOrigin == null || driverDest == null) return false;

                        return driverOrigin.Equals(passengerOrigin) &&
                               driverDest.Equals(passengerDest);
                    });

                    if (matchingDriver == null)
                    {
                        unpaired.Add(passenger);
                        continue;
                    }

                    // Přidej pasažéra do existující skupiny tohoto řidiče nebo vytvoř novou
                    var group = groups.FirstOrDefault(g =>
                        g.Slot.Equals(slot) && g.Driver.Equals(matchingDriver));

                    if (group == null)
                    {
                        group = new CarGroup { Slot = slot, Driver = matchingDriver };
                        groups.Add(group);
                    }

                    group.Passengers.Add(passenger);
                }
            }

            return groups;
        }

        /// <summary>
        /// Odkud rozhodčí na tento slot přijíždí –
        /// z předchozího slotu pokud existuje, jinak z domova.
        /// </summary>
        private static Geo? GetOrigin(Referee referee, Slot currentSlot, State state)
        {
            var previous = state.GetSlotsByReferee(referee)
                .Where(s => s.End <= currentSlot.Start)
                .OrderByDescending(s => s.End)
                .FirstOrDefault();

            return previous?.Location ?? referee.Location;
        }

        /// <summary>
        /// Kam rozhodčí po tomto slotu pojede –
        /// na další slot pokud existuje, jinak domů.
        /// </summary>
        private static Geo? GetDestination(Referee referee, Slot currentSlot, State state)
        {
            var next = state.GetSlotsByReferee(referee)
                .Where(s => s.Start >= currentSlot.End)
                .OrderBy(s => s.Start)
                .FirstOrDefault();

            return next?.Location ?? referee.Location;
        }
    }
}