using Diplomka.Entity;
using Diplomka.Routing;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Diplomka.Utils
{
    public class ScenerioGenerator
    {
        public class RankBucket
        {
            public int Min { get; set; }
            public int Max { get; set; }
            public double Weight { get; set; }
        }

        public class RefereeRole
        {
            public string Name { get; set; } = String.Empty;
            public int MinRank { get; set; }
            public int MaxRank { get; set; }
        }

        public string CompetitionName { get; set; } = "NHL z.č.";

        public int MatchCount { get; set; } = 50;

        public int RefereeNumber { get; set; } = 20;

        public List<RefereeRole> RefereeRoleSetup { get; set; } = new()
        {
            /*
            new RefereeRole { Name = "První", MinRank = 65, MaxRank = 80 },
            new RefereeRole { Name = "Druhý", MinRank = 35, MaxRank = 50 },
            new RefereeRole { Name = "Třetí", MinRank = 10, MaxRank = 25 }
            */
            new RefereeRole { Name = "Hlavní 1", MinRank = 75, MaxRank = 85 },
            new RefereeRole { Name = "Hlavní 2", MinRank = 75, MaxRank = 85 },
            new RefereeRole { Name = "Čárový 1", MinRank = 20, MaxRank = 40 },
            new RefereeRole { Name = "Čárový 2", MinRank = 20, MaxRank = 40 },
        };

        /*
         * Shlukovani dnu a lokaci.
         * Kolik procent slotu bude v ramci jednoho dne a v ramci jednoho mista
         * 
         * 0 = bez shluku, 1 = extrémní shluky
         */
        public double DayClustering { get; set; } = 0;
        public double LocationClustering { get; set; } = 0;

        /*
         * Pravdepodonbost casoveho prekryti slotu a
         * pravdepodonost potreby elitniho rozhodciho
         */
        public double OverlapProbability { get; set; } = 0.5;
        public double EliteRefereeProbability { get; set; } = 0.2;

        /*
         * Distribuce mnoziny rozhodcich na zaklade urovni
         */
        public List<RankBucket> RefereeRankDistribution { get; set; } = new()
        {
            new RankBucket { Min = 70, Max = 100, Weight = 0.25 },
            new RankBucket { Min = 30, Max = 70, Weight = 0.50 },
            new RankBucket { Min = 10, Max = 30, Weight = 0.25 }
        };

        /*
         * Distribuce mnoziny slotu na zaklade potrebnych urovni
         */
        public List<RankBucket> SlotRankDistribution { get; set; } = new()
        {
            new RankBucket { Min = 70, Max = 100, Weight = 0.25 },
            new RankBucket { Min = 30, Max = 70, Weight = 0.50 },
            new RankBucket { Min = 10, Max = 30, Weight = 0.25 }
        };

        private readonly List<Geo> _locations = new()
        {
            new Geo(50.0755, 14.4378),
            new Geo(49.1951, 16.6068),
            new Geo(49.8209, 18.2625),
            new Geo(49.7475, 13.3776),
            new Geo(50.2092, 15.8328),
            new Geo(50.0343, 15.7812),
            new Geo(49.2264, 17.6707),
            new Geo(49.5938, 17.2509),
            new Geo(50.7671, 15.0562),
            new Geo(50.6607, 14.0323),
            new Geo(50.2319, 12.8717),
            new Geo(48.9747, 14.4749),
            new Geo(49.3961, 15.5912),
            new Geo(50.6855, 14.5376),
            new Geo(49.9480, 17.9070),
            new Geo(49.7384, 13.3736),
            new Geo(49.4144, 14.6578),
            new Geo(49.0830, 14.4353),
            new Geo(49.1445, 16.8780),
            new Geo(49.3000, 17.3930),
            new Geo(49.4710, 17.1110),
            new Geo(49.5940, 18.0100),
            new Geo(49.6833, 18.3500),
            new Geo(49.7610, 18.6260),
            new Geo(50.0737, 12.3730),
            new Geo(50.2410, 12.8710),
            new Geo(50.7700, 15.0500),
            new Geo(50.6100, 15.1600),
            new Geo(50.4370, 15.3510),
            new Geo(49.9640, 16.9700),
            new Geo(49.4585, 17.1380),
            new Geo(49.0830, 17.4650),
            new Geo(49.0680, 17.4590),
        };

        private int GenerateRank(Random random, List<RankBucket> distribution)
        {
            double roll = random.NextDouble();
            double cumulative = 0;

            foreach (var bucket in distribution)
            {
                cumulative += bucket.Weight;
                if (roll <= cumulative)
                    return random.Next(bucket.Min, bucket.Max + 1);
            }

            return distribution.Last().Min;
        }

        private List<Geo> GetHotLocations(Random random, int count)
        {
            return _locations.OrderBy(x => random.Next()).Take(count).ToList();
        }

        private List<DateTime> GenerateHotDays(DateTime from, DateTime to, int count, Random random)
        {
            var days = Enumerable.Range(0, (to - from).Days)
                .Select(d => from.Date.AddDays(d))
                .ToList();

            return days.OrderBy(x => random.Next()).Take(count).ToList();
        }

        public List<Slot> GenerateSlots(DateTime dateFrom, DateTime dateTo)
        {
            var random = new Random();

            var hotDays = GenerateHotDays(dateFrom, dateTo, 3, random);
            var hotLocations = GetHotLocations(random, 5);

            var slots = new List<Slot>();
            int slotIdCounter = 1; // Počítadlo pro unikátní ID slotu

            // Nyní iterujeme přes ZÁPASY místo jednotlivých slotů
            for (int matchIndex = 0; matchIndex < MatchCount; matchIndex++)
            {
                // Výběr dne pro zápas
                DateTime day = (random.NextDouble() < DayClustering)
                    ? hotDays[random.Next(hotDays.Count)]
                    : dateFrom.Date.AddDays(random.Next((dateTo - dateFrom).Days));

                // Výběr času s překryvem pro zápas
                DateTime dateStart;

                if (random.NextDouble() < OverlapProbability && slots.Count > 0)
                {
                    var baseSlot = slots[random.Next(slots.Count)];
                    dateStart = baseSlot.Start.AddMinutes(random.Next(-30, 30));
                }
                else
                {
                    dateStart = day.AddMinutes(random.Next(8 * 60, 20 * 60));
                }

                if (dateStart < dateFrom) dateStart = dateFrom;
                if (dateStart > dateTo.AddHours(-2)) dateStart = dateTo.AddHours(-2);

                var dateEnd = dateStart + TimeSpan.FromHours(2);

                // Výběr lokace pro celý zápas
                Geo location = (random.NextDouble() < LocationClustering)
                    ? hotLocations[random.Next(hotLocations.Count)]
                    : _locations[random.Next(_locations.Count)];

                int matchId = matchIndex + 1;

                // Generování jednotlivých slotů (rozhodčích) pro tento konkrétní zápas
                foreach (var roleConfig in RefereeRoleSetup)
                {
                    // Náhodný rank v rámci povoleného rozmezí pro danou roli
                    int requiredRank = random.Next(roleConfig.MinRank, roleConfig.MaxRank + 1);

                    slots.Add(new Slot
                    {
                        Id = slotIdCounter++,
                        Name = $"{CompetitionName} {matchId}, {roleConfig.Name}", // Např: "NHL 1, Hlavní 1"
                        Location = location,
                        Start = dateStart,
                        End = dateEnd,
                        RequiredRank = requiredRank
                    });
                }
            }

            return slots;
        }

        public List<Referee> GenerateReferess()
        {
            var random = new Random();

            var referees = new List<Referee>();

            var hotLocations = GetHotLocations(random, 5);

            for (int i = 0; i < RefereeNumber; i++)
            {
                var location = (random.NextDouble() < 0.7)
                    ? hotLocations[random.Next(hotLocations.Count)]
                    : _locations[random.Next(_locations.Count)];

                var rank = (random.NextDouble() < EliteRefereeProbability)
                    ? random.Next(85, 101)
                    : GenerateRank(random, RefereeRankDistribution);

                referees.Add(new Referee
                {
                    Id = i + 1,
                    Name = $"Referee {i + 1}",
                    Location = location,
                    Rank = rank
                });
            }

            return referees;
        }
    }
}