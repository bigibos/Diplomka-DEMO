using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Diplomka.Utils
{
    /// <summary>
    /// Modulární generování datových scnénářů pro přiřazení.
    /// </summary>
    public class ScenerioGenerator
    {
        /// <summary>
        /// Záznam pro nastavování distrubuce úrovní rozhodčích a požadovaných úrovní slotů
        /// </summary>
        public record RankBucket
        {
            public int Min { get; set; }
            public int Max { get; set; }
            public double Weight { get; set; }
        }

        /// <summary>
        /// Záznam pro nastavení rolí rozhodčího.
        /// Umožňuje generovat scénáře pro různé sporty, soutěžě a turnaje s rozdílnými počty a typy rozhodčích
        /// </summary>
        public record RefereeRole
        {
            public string Name { get; set; } = String.Empty;
            public int MinRank { get; set; }
            public int MaxRank { get; set; }
        }

        /// <summary>
        /// Prefixový název slotů
        /// </summary>
        /// <example>
        /// TELH U20, NBL 2025/2026 z.č apod.
        /// </example>
        public string SlotPrefix { get; set; } = "NHL z.č.";

        /// <summary>
        /// Prefixový náze rozhodčích
        /// </summary>
        /// <example>
        /// Referee, Rozhodčí apod.
        /// </example>
        public string RefereePrefix { get; set; } = "Rozhodčí";

        /// <summary>
        /// Počet zápasů ve scnénáři.
        /// Výsledný počet slotů bude záviset na této hodnotě a na počtu rozhodčích
        /// potřebných pro každý zápas podle <see cref="RefereeRoleSetup"/>
        /// </summary>
        public int MatchCount { get; set; } = 50;

        /// <summary>
        /// Celkový počet rozhodčích pro přiřazení
        /// </summary>
        public int RefereeNumber { get; set; } = 20;

        /// <summary>
        /// Nastavení počtu a typů rozhodčích pro každý zápas, včetně rozsahu jejich úrovní.
        /// </summary>
        public List<RefereeRole> RefereeRoleSetup { get; set; } = new()
        {
            /*
            // Basketball
            new RefereeRole { Name = "První", MinRank = 65, MaxRank = 80 },
            new RefereeRole { Name = "Druhý", MinRank = 35, MaxRank = 50 },
            new RefereeRole { Name = "Třetí", MinRank = 10, MaxRank = 25 }
            */
            // Hokej
            new RefereeRole { Name = "Hlavní 1", MinRank = 75, MaxRank = 85 },
            new RefereeRole { Name = "Hlavní 2", MinRank = 75, MaxRank = 85 },
            new RefereeRole { Name = "Čárový 1", MinRank = 20, MaxRank = 40 },
            new RefereeRole { Name = "Čárový 2", MinRank = 20, MaxRank = 40 },
        };


        /// <summary>
        /// Koeficient ovlivňující kolik procent slotů bude shlukováno do jednoho dne.
        /// </summary>
        /// <example>
        /// Hodnota 0 - minimum shluků, Hodnota 1 - extrémní shluky
        /// </example>
        public double DayClustering { get; set; } = 0;

        /// <summary>
        /// Koeficient ovlivňující kolik procent slotů bude shlukováno do jedné lokace
        /// </summary>
        /// <example>
        /// Hodnota 0 - minimum shluků, Hodnota 1 - extrémní shluky
        /// </example>
        public double LocationClustering { get; set; } = 0;

 
        /// <summary>
        /// Koeficient pravděpodobnosti časového překrytí slotů
        /// </summary>
        public double OverlapProbability { get; set; } = 0.5;

        /// <summary>
        /// Koeficient pravděpodobnosti potřeby elitního rozhodčího. (vzácný rozhodčí pro sloty s vysokými nároky)
        /// </summary>
        public double EliteRefereeProbability { get; set; } = 0.2;

        /// <summary>
        /// Nastavení distribuce úrovní rozhodčích
        /// </summary>
        public List<RankBucket> RefereeRankDistribution { get; set; } = new()
        {
            new RankBucket { Min = 70, Max = 100, Weight = 0.25 },
            new RankBucket { Min = 30, Max = 70, Weight = 0.50 },
            new RankBucket { Min = 10, Max = 30, Weight = 0.25 }
        };

        /// <summary>
        /// Nastavení distribuce požadovaných úrovní slotů
        /// </summary>
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

        /// <summary>
        /// Generování náhodné úrovně, resp. požadované úrovně na základě distribuce
        /// </summary>
        /// <param name="random">Generátor náhody</param>
        /// <param name="distribution">Distribuce úrovní</param>
        /// <returns>Náhodnou úroveň podle distribuce</returns>
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

        /// <summary>
        /// Náhodně vybere lokace, které budou sloužit k prostorovému shlukování slotů
        /// </summary>
        /// <param name="random">Generátor náhody</param>
        /// <param name="count">Počet lokací pro shluk</param>
        /// <returns>Seznam lokací pro shluk</returns>
        private List<Geo> GenerateHotLocation(Random random, int count)
        {
            return _locations.OrderBy(x => random.Next()).Take(count).ToList();
        }

        /// <summary>
        /// Náhodně vygeneruje dny, které budou sloužit k časovému shlukování slotů
        /// </summary>
        /// <param name="from">Od kdy se mají dny vybírat</param>
        /// <param name="to">Do kdy se mají dny vybírat</param>
        /// <param name="count">Počet dnů pro shluk</param>
        /// <param name="random">Generátor náhody</param>
        /// <returns>Seznam dnů pro shluk</returns>
        private List<DateTime> GenerateHotDays(DateTime from, DateTime to, int count, Random random)
        {
            var days = Enumerable.Range(0, (to - from).Days)
                .Select(d => from.Date.AddDays(d))
                .ToList();

            return days.OrderBy(x => random.Next()).Take(count).ToList();
        }

        /// <summary>
        /// Náhodně generuje sloty podle nastavených parametrů
        /// </summary>
        /// <param name="dateFrom">Čas od kdy se mají sloty generovat</param>
        /// <param name="dateTo">Čas do kdy se mají sloty generovat</param>
        /// <returns>Seznam náhodně vygenerovaných slotů</returns>
        public List<Slot> GenerateSlots(DateTime dateFrom, DateTime dateTo)
        {
            var random = new Random();

            var hotDays = GenerateHotDays(dateFrom, dateTo, 3, random);
            var hotLocations = GenerateHotLocation(random, 5);

            var slots = new List<Slot>();
            int slotIdCounter = 1;

            // Iterovani pres zapasy
            for (int matchIndex = 0; matchIndex < MatchCount; matchIndex++)
            {
                // Vyber dne pro zapas
                DateTime day = (random.NextDouble() < DayClustering)
                    ? hotDays[random.Next(hotDays.Count)]
                    : dateFrom.Date.AddDays(random.Next((dateTo - dateFrom).Days));

                // Vyber casu s prekryvem pro zapas
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

                // Vyber lokace pro zapas
                Geo location = (random.NextDouble() < LocationClustering)
                    ? hotLocations[random.Next(hotLocations.Count)]
                    : _locations[random.Next(_locations.Count)];

                int matchId = matchIndex + 1;

                // Generovani jednotlivych slotu pro dany zapas
                foreach (var roleConfig in RefereeRoleSetup)
                {
                    // Nahodny rank v povolenem rozsahu pro danou roli rozhodciho
                    int requiredRank = random.Next(roleConfig.MinRank, roleConfig.MaxRank + 1);

                    slots.Add(new Slot
                    {
                        Id = slotIdCounter++,
                        Name = $"{SlotPrefix} {matchId}, {roleConfig.Name}",
                        Location = location,
                        Start = dateStart,
                        End = dateEnd,
                        RequiredRank = requiredRank
                    });
                }
            }

            return slots;
        }

        /// <summary>
        /// Náhodně generuje rozhodčí podle nastavených parametrů
        /// </summary>
        /// <returns>Seznam náhodně vygenerovaných rozhodčí</returns>
        public List<Referee> GenerateReferess()
        {
            var random = new Random();

            var referees = new List<Referee>();

            var hotLocations = GenerateHotLocation(random, 5);

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
                    Name = $"{RefereePrefix} {i + 1}",
                    Location = location,
                    Rank = rank
                });
            }

            return referees;
        }
    }
}