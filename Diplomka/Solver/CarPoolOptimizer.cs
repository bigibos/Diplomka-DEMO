using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.Solver
{
    public class CarPoolOptimizer
    {
        /// <summary>
        /// Záznam o sdílené jízdě – kdo je řidič a kdo spolujezdci
        /// </summary>
        public record CarPoolGroup
        {
            public Slot Slot { get; init; } = null!;
            public Referee Driver { get; init; } = null!;
            public List<Referee> Passengers { get; init; } = new();
        }

        /// <summary>
        /// Spustí optimalizaci sdílené dopravy nad finálním stavem přiřazení.
        /// Vrátí seznam skupin pro sdílenou dopravu a seznam rozhodčích bez auta
        /// kteří nebyli spárováni (upozornění).
        /// </summary>
        /// <param name="state">Finální stav přiřazení ze solveru</param>
        /// <param name="unpaired">Výstup: rozhodčí bez auta kteří nemají řidiče</param>
        /// <returns>Seznam skupin sdílené dopravy</returns>
        public List<CarPoolGroup> Optimize(State state, out List<Referee> unpaired)
        {
            var groups = new List<CarPoolGroup>();
            unpaired = new List<Referee>();

            // Seskup přiřazení podle zápasu (stejný slot = stejný čas + lokace)
            var matchGroups = state
                .Where(kv => kv.Value != null)
                .GroupBy(kv => (kv.Key.Start, kv.Key.End, kv.Key.Location))
                .ToList();

            foreach (var matchGroup in matchGroups)
            {
                var referees = matchGroup.Select(kv => kv.Value!).ToList();
                var slots = matchGroup.Select(kv => kv.Key).ToList();
                var slot = slots.First();

                var drivers = referees.Where(r => r.HasCar).ToList();
                var passengers = referees.Where(r => !r.HasCar).ToList();

                if (passengers.Count == 0) continue; // Všichni mají auto, nic k řešení

                if (drivers.Count == 0)
                {
                    // Nikdo nemá auto – všichni jsou nespárovaní (problém ke hlášení)
                    unpaired.AddRange(passengers);
                    continue;
                }

                // Přiřaď každého bez auta k nejbližšímu řidiči (FIFO – jednoduché párování)
                var group = new CarPoolGroup
                {
                    Slot = slot,
                    Driver = drivers.First()
                };

                group.Passengers.AddRange(passengers);
                groups.Add(group);
            }

            return groups;
        }
    }
}
