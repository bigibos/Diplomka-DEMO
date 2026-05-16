using Diplomka.Entity;
using Diplomka.Solver.Config;
using Diplomka.Solver.Services;

namespace Diplomka.Solver.Solvers
{
    /// <summary>
    /// Hlavní optimalizační algoritmus.
    ///     - Large Neighborhood Search (LNS) s opravným Branch & Bound
    ///     - Řešení strategicky zvolených skupin stavového prostoru
    /// </summary>
    public class SolverLNS : SolverBase
    {
        private readonly List<Referee> _referees;
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SolverConfiguration _config;
        private readonly Random _random = new();


        public int NeighborhoodSize { get; set; } = 10;

        public int MaxIterations { get; set; } = 150;

        public int MaxAttempts { get; set; } = 30;

        public TimeSpan IterationTimeLimit { get; set; } = TimeSpan.FromSeconds(3);

        public NeighborhoodStrategy Strategy { get; set; } = NeighborhoodStrategy.CostWeighted;

        /// <summary>
        /// Strategie pro výběr skupin/sousedství
        ///     1) <see cref="Random"/> - výběr skupiny slotů je náhodný
        ///     2) <see cref="CostWeighted"/> - výběr skupiny slotů s nejdražší cenou přiřazení (největší potenciál zlepšení)
        ///     3) <see cref="Clustered"/> - výběr kotevního slotu a pak jeho časově sousedních slotů
        /// </summary>
        public enum NeighborhoodStrategy
        {
            Random,
            CostWeighted,
            Clustered
        }

        public int TotalIterations { get; private set; }
        public int ImprovingIterations { get; private set; }
        public double BestCost { get; private set; }

        public SolverLNS(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            SolverConfiguration config)
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _config = config;
        }

        /// <summary>
        /// Přetížení hlavní metody algoritmu <see cref="Solve(State)"/> s vlastní tvorbou počátečního stavu pomocí <see cref="SolverGreedy"/> a <see cref="SolverRepair"/>
        /// </summary>
        /// <param name="slots">Seznam slotů k zaplnění</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        override public State Solve(IEnumerable<Slot> slots)
        {
            // Console.WriteLine("[LNS] Spouštím greedy warm start...");
            var initial = new SolverGreedy(_referees, _conflictChecker, _costCalculator).Solve(slots.ToList());
            initial = new SolverRepair(_referees, _conflictChecker, _costCalculator, _config).Solve(initial);
            return Solve(initial);
        }


        /// <summary>
        /// Hlavní metoda algoritmu.
        /// Použije počáteční řešení a pokusí se ho vylepšít fázemi:
        ///     1) Destroy - vybere se určitý počet slotů, kterée se uvolní pro nová přiřazení pomocí zvolené strategie
        ///     2) Restric - vyberou se vhodní kandidáti pro přiřazení do uvolněné skupiny slotů
        ///     3) Repair - pomocí <see cref="SolverBB"/> se provedou přiřazení na vybrané skupně slotů s vybranými kandidátmi
        ///     4) Merge - skupina zaplněných slotů se sloučí se zbytkem stavu
        ///     5) Accept - při zlepšení se akceptuje nové řešení, jinak se zkouší znovu až do překročení limitu
        /// </summary>
        /// <param name="state">Počateční stav k vylepšení</param>
        /// <returns>Stav nejlepšího nalezené řešení</returns>
        override public State Solve(State state)
        {
            TotalIterations = 0;
            ImprovingIterations = 0;

            var best = (State)state.Clone();
            var current = best;
            BestCost = _costCalculator.TotalCost(best);
            int noImprovementCount = 0;

            Emit(new SolverEvent.StartEvent(BestCost));
            // Console.WriteLine($"[LNS] Start, cena: {BestCost:F2}, iterací: {MaxIterations}");

            for (int iter = 0; iter < MaxIterations; iter++)
            {
                TotalIterations++;

                var neighborhood = SelectNeighborhood(current);
                var available = GetAvailableReferees(current, neighborhood);

                if (available.Count == 0) { noImprovementCount++; continue; }

                // var repaired = Repair(current, neighborhood, available);
                // var merged = MergeStates(current, neighborhood, repaired);
                var merged = Repair(current, neighborhood, available);
                double mergedCost = _costCalculator.TotalCost(merged);

                if (mergedCost < BestCost)
                {
                    if (mergedCost + 10 < BestCost) // jen pro výraznější zlepšení
                    {
                        Emit(new SolverEvent.ImprovementEvent(BestCost, mergedCost));
                    }
                    BestCost = mergedCost;
                    best = (State)merged.Clone();
                    current = best;
                    noImprovementCount = 0;
                    ImprovingIterations++;
                }
                else
                {
                    noImprovementCount++;
                    if (noImprovementCount >= MaxAttempts)
                    {
                        current = (State)best.Clone();
                        noImprovementCount = 0;
                    }
                }
            }

            Emit(new SolverEvent.FinishEvent(BestCost));    

            return best;
        }

        /// <summary>
        /// Repair fáze vuyžívající <see cref="SolverBB"/>
        /// </summary>
        /// <param name="current">Aktuální stav</param>
        /// <param name="neighborhood">Sousedství vybrané strategií</param>
        /// <param name="candidates">Dostupní rozhodčí</param>
        /// <returns>Opravený stav řešení</returns>
        private State Repair(State current, List<Slot> neighborhood, List<Referee> candidates)
        {
            try
            {
                var solver = new SolverBB(candidates, _conflictChecker, _costCalculator, _config, IterationTimeLimit);
                // solver.OnEvent += Forward;

                return solver.Solve(current, neighborhood);
            }
            catch (Exception ex)
            {
                // Console.WriteLine($"[LNS] Repair selhal: {ex.Message}");
                return current; // fallback
            }
        }

        /// <summary>
        /// Destroy fáze pro vybrání, a uvolnění slotu v sousedství podle strategie
        /// </summary>
        /// <param name="state">Stav ve kterém se provádí výběr</param>
        /// <returns>Vybrané sousedství slotů</returns>
        private List<Slot> SelectNeighborhood(State state)
        {
            var slots = state.GetSlots();
            int groupSize = Math.Min(NeighborhoodSize, slots.Count);

            return Strategy switch
            {
                NeighborhoodStrategy.Random => SelectRandom(slots, groupSize),
                NeighborhoodStrategy.CostWeighted => SelectCostWeighted(state, slots, groupSize),
                NeighborhoodStrategy.Clustered => SelectClustered(slots, groupSize),
                _ => SelectRandom(slots, groupSize)
            };
        }

        /// <summary>
        /// Strategie <see cref="NeighborhoodStrategy.Random"/> pro výběr sousedství
        /// </summary>
        /// <param name="slots">Sloty pro výběr</param>
        /// <param name="groupSize">Velikost sousedství</param>
        /// <returns>Vybrané sousedství slotů</returns>
        private List<Slot> SelectRandom(List<Slot> slots, int groupSize)
        {
            return slots.OrderBy(s => _random.Next()).Take(groupSize).ToList();
        }

        /// <summary>
        /// Strategie <see cref="NeighborhoodStrategy.CostWeighted"/> pro výběr sousedství
        /// </summary>
        /// <param name="state">Stav pro kontext</param>
        /// <param name="slots">Sloty pro výběr</param>
        /// <param name="groupSize">Velikost sousedství</param>
        /// <returns>Vybrané sousedství slotů</returns>
        private List<Slot> SelectCostWeighted(State state, List<Slot> slots, int groupSize)
        {
            // Kdyby byla vaha 0, tak by dalsi faze nepracoval spravne. Proto se pouziva offset
            const double weightOffset = 1.0;

            // Vyber prirazene sloty a jajich cenu
            var candidatesWithCost = slots.Select(s => {
                var referee = state.GetReferee(s);
                double cost = referee != null
                    ? _costCalculator.AssignmentCost(s, referee)
                    : _config.UnassignedCost;
                return (slot: s, weight: cost + weightOffset);
            }).ToList();

            var group = new List<Slot>(groupSize);
            double total = candidatesWithCost.Sum(x => x.weight);

            // Vytvoreni listu pro vybranou skupinu
            while (group.Count < groupSize && candidatesWithCost.Count > 0)
            {
                double randomTreshold = _random.NextDouble() * total;
                int cId = candidatesWithCost.Count - 1;
                double cost = 0;
                for (int j = 0; j < candidatesWithCost.Count - 1; j++)
                {
                    cost += candidatesWithCost[j].weight;
                    if (randomTreshold <= cost)
                    {
                        cId = j; 
                        break;
                    }
                }
                group.Add(candidatesWithCost[cId].slot);
                total -= candidatesWithCost[cId].weight; // Asi lepsi nez pocitat vse pres sum pokazde?
                candidatesWithCost.RemoveAt(cId);
            }
            return group;
        }

        /// <summary>
        /// Strategie <see cref="NeighborhoodStrategy.Clustered"/> pro výběr sousedství
        /// </summary>
        /// <param name="slots">Sloty pro výběr</param>
        /// <param name="groupSize">Velikost sousedství</param>
        /// <returns>Vybrané sousedství slotů</returns>
        private List<Slot> SelectClustered(List<Slot> slots, int groupSize)
        {
            // Vybere se zakladni/kotevni slot
            var baseSlot = slots[_random.Next(slots.Count)];

            // Sloty se odfiltruji podle spolecne casove oblasti - jsou v casove clusteru
            var group = slots
                .OrderBy(s => Math.Abs((s.Start - baseSlot.Start).TotalMinutes))
                .Take(groupSize)
                .ToList();

            return group;
        }


        /// <summary>
        /// Restrict fáze ve které se získají kandidátní rozhodčí pro vybranné sloty sousedství
        /// </summary>
        /// <param name="state">Stav pro kontext</param>
        /// <param name="group">Sousedství slotů</param>
        /// <returns>Vhodní kandidáti pro sousedství slotů</returns>
        private List<Referee> GetAvailableReferees(State state, List<Slot> group)
        {
            var relaxedSet = new HashSet<Slot>(group);
            var blocked = new HashSet<Referee>();

            // Puvodni rozhodci z uvolnenych slotu
            var relaxedReferees = group
                .Select(s => state.GetReferee(s))
                .Where(r => r != null)
                .ToHashSet();


            foreach (var s in state.GetSlots())
            {
                if (relaxedSet.Contains(s)) 
                    continue; // preskoceni uvolnenych slotu

                var referee = state.GetReferee(s);
                if (referee == null) 
                    continue; // prazdny slot nikoho neblokuje

                if (relaxedReferees.Contains(referee))
                    continue;

                if (blocked.Contains(referee)) 
                    continue; // rozhodci je uz zablokovany pro prirazeni

                foreach (var r in group)
                {
                    // prirazeni by vytvorilo casovy prekryv
                    if (_conflictChecker.Overlaps(s, r))
                    {
                        blocked.Add(referee); // zablokuj tedy rozhodciho
                        break;
                    }
                }
            }

            // rozhodci, kteri nejsou v blokaci pro uvolnene sloty
            return _referees.Where(r => !blocked.Contains(r)).ToList();
        }
    }
}