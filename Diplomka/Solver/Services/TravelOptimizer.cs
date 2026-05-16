using Diplomka.Entity;

namespace Diplomka.Solver.Services
{
    /// <summary>
    /// Třída sloužící k optimalizování a návrhu lepšího cestování pomocí společné cesty rozhodčích.
    /// </summary>
    public class TravelOptimizer
    {
        /// <summary>
        /// Pomocný třída pro vytvoření cestovních skupin - jeden řidič a seznam pasažérů
        /// </summary>
        public record CarGroup
        {
            public Slot Slot { get; init; } = null!;
            public Referee Driver { get; init; } = null!;
            public List<Referee> Passengers { get; init; } = new();
        }

        /// <summary>
        /// Hlavní metoda algoritmu, pro vytvoření cestovních skupin.
        /// </summary>
        /// <param name="state">Stave pro kontext</param>
        /// <param name="unpaired">Seznam nespárovaných rozhodčích</param>
        /// <returns>Seznam cestovních skupin</returns>
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
        /// Odkud rozhodčí na tento slot přijíždí – z předchozího slotu pokud existuje, jinak z domova.
        /// </summary>
        /// <param name="referee">Rozhodčí pro vyhodnocení</param>
        /// <param name="slot">Slot pro vyhodnocení</param>
        /// <param name="state">Stav pro kontext</param>
        /// <returns>Lokace odkud rozhodčí přijíždí</returns>
        private static Geo? GetOrigin(Referee referee, Slot slot, State state)
        {
            var previous = state.GetSlotsByReferee(referee)
                .Where(s => s.End <= slot.Start)
                .OrderByDescending(s => s.End)
                .FirstOrDefault();

            return previous?.Location ?? referee.Location;
        }

        /// <summary>
        /// Kam rozhodčí po tomto slotu pojede – na další slot pokud existuje, jinak domů.
        /// </summary>
        /// <param name="referee">Rozhodčí pro vyhodnocení</param>
        /// <param name="slot">Slot pro vyhodnocení</param>
        /// <param name="state">Stav pro kontext</param>
        /// <returns>Lokace kam rozhodčí pojede</returns>
        private static Geo? GetDestination(Referee referee, Slot slot, State state)
        {
            var next = state.GetSlotsByReferee(referee)
                .Where(s => s.Start >= slot.End)
                .OrderBy(s => s.Start)
                .FirstOrDefault();

            return next?.Location ?? referee.Location;
        }
    }
}