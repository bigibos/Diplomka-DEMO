using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Diplomka.Model;

namespace Diplomka.Solver
{
    public class HybridLnsSolver
    {
        private Random _random = new Random();
        private List<Referee> _referees;
        private List<Slot> _slots;

        // Globální stav pro sledování nejlepšího nalezeného
        private State _bestState;
        private double _bestCost;

        public HybridLnsSolver(List<Slot> slots, List<Referee> referees)
        {
            _slots = slots;
            _referees = referees;
            _bestState = new State();
            // Inicializace prázdného stavu
            foreach (var s in _slots) _bestState.AddSlot(s);
            _bestCost = double.MaxValue;
        }

        public State Solve(int totalTimeLimitSeconds)
        {
            Stopwatch sw = Stopwatch.StartNew();

            // --- FÁZE 1: NAIVNÍ HEURISTIKA (Greedy) ---
            Console.WriteLine("[Phase 1] Spouštím Greedy konstrukci...");
            InitialGreedy();
            _bestCost = CalculateTotalCost(_bestState);
            Console.WriteLine($"[INFO] Výchozí cena: {_bestCost}");

            // --- FÁZE 2: OPRAVNÝ ALGORITMUS ---
            // (Greedy mohl nechat některé sloty prázdné kvůli kolizím)
            Console.WriteLine("[Phase 2] Opravuji neobsazené sloty...");
            RepairFeasibility();

            // --- FÁZE 3: VYLEPŠOVÁNÍ (LNS + Mini B&B) ---
            Console.WriteLine("[Phase 3] Optimalizuji pomocí LNS...");

            int iterations = 0;
            while (sw.Elapsed.TotalSeconds < totalTimeLimitSeconds)
            {
                // 1. "Ruin" - vybereme malý cluster slotů k přeplánování (např. 6-8 slotů)
                var neighborhood = SelectNeighborhood(size: 7);

                // 2. "Recreate" - zkusíme tyto sloty obsadit pomocí mini B&B
                OptimizeSubProblem(neighborhood);

                iterations++;
                if (iterations % 50 == 0)
                {
                    Console.WriteLine($"[LNS] Iterace {iterations}, Aktuální cena: {_bestCost:F2}");
                }
            }

            Console.WriteLine($"[FINISH] Optimalizace dokončena. Celkem iterací: {iterations}");
            return _bestState;
        }

        private void InitialGreedy()
        {
            // Seřadíme sloty chronologicky
            var sortedSlots = _slots.OrderBy(s => s.Start).ToList();

            foreach (var slot in sortedSlots)
            {
                Referee? bestRef = null;
                double minCost = double.MaxValue;

                foreach (var referee in _referees)
                {
                    if (IsFeasible(_bestState, slot, referee))
                    {
                        double cost = AssignmentCost(slot, referee);
                        if (cost < minCost)
                        {
                            minCost = cost;
                            bestRef = referee;
                        }
                    }
                }

                if (bestRef != null)
                {
                    _bestState.SetReferee(slot, bestRef);
                }
            }
        }

        private void RepairFeasibility()
        {
            var emptySlots = _bestState.GetEmptySlots();
            foreach (var slot in emptySlots)
            {
                // Zkusíme najít kohokoliv, kdo může, bez ohledu na cenu
                foreach (var referee in _referees)
                {
                    if (IsFeasible(_bestState, slot, referee))
                    {
                        _bestState.SetReferee(slot, referee);
                        break;
                    }
                }
            }
        }

        private List<Slot> SelectNeighborhood(int size)
        {
            // Strategie: Vybereme jeden náhodný slot a k němu 'size-1' nejbližších v čase
            var pivot = _slots[_random.Next(_slots.Count)];
            return _slots
                .OrderBy(s => Math.Abs((s.Start - pivot.Start).TotalMinutes))
                .Take(size)
                .ToList();
        }

        private void OptimizeSubProblem(List<Slot> neighborhood)
        {
            // Uložíme si původní přiřazení pro případ, že B&B nic nezlepší
            var backup = new Dictionary<Slot, Referee?>();
            foreach (var s in neighborhood)
            {
                backup[s] = GetRefereeFromState(_bestState, s);
                _bestState.ClearSlot(s); // Uvolníme sloty pro B&B
            }

            // Spustíme tvůj princip B&B na malém vzorku
            // Protože je neighborhood malý (7 slotů), proběhne to bleskově
            var subSolver = new MiniBBSolver(neighborhood, _referees, _bestState);
            var (bestSubAssignment, subCost) = subSolver.Solve();

            // Výpočet ceny "před" a "po" jen pro danou oblast
            double oldCost = backup.Sum(kvp => kvp.Value != null ? AssignmentCost(kvp.Key, kvp.Value) : 1000000);

            if (subCost < oldCost)
            {
                // Zlepšení nalezeno!
                foreach (var kvp in bestSubAssignment)
                {
                    _bestState.SetReferee(kvp.Key, kvp.Value);
                }
                _bestCost = CalculateTotalCost(_bestState);
            }
            else
            {
                // Nic lepšího nebylo, vrátíme původní
                foreach (var kvp in backup)
                {
                    _bestState.SetReferee(kvp.Key, kvp.Value);
                }
            }
        }

        // --- POMOCNÉ METODY (Logika z tvých BBSolver.cs) ---

        private bool IsFeasible(State state, Slot slot, Referee referee)
        {
            foreach (var assignment in state)
            {
                if (assignment.Value != null && assignment.Value.Id == referee.Id)
                {
                    // Časová kolize
                    if (slot.Start < assignment.Key.End && assignment.Key.Start < slot.End)
                        return false;
                }
            }
            return true;
        }

        private double AssignmentCost(Slot slot, Referee referee)
        {
            return CostCalculator.AssignmentCost(slot, referee);    
        }

        private double CalculateTotalCost(State state)
        {
            double total = 0;
            foreach (var assignment in state)
            {
                if (assignment.Value != null)
                    total += AssignmentCost(assignment.Key, assignment.Value);
                else
                    total += 1000000; // Penalizace za neobsazený slot
            }
            return total;
        }

        private Referee? GetRefereeFromState(State state, Slot slot)
        {
            foreach (var a in state) if (a.Key.Id == slot.Id) return a.Value;
            return null;
        }
    }

    // Zjednodušený B&B pro malé podproblémy
    internal class MiniBBSolver
    {
        private List<Slot> _subSlots;
        private List<Referee> _allReferees;
        private State _globalState;
        private Dictionary<Slot, Referee?> _bestSubAssignment;
        private double _bestSubCost = double.MaxValue;

        public MiniBBSolver(List<Slot> subSlots, List<Referee> allReferees, State globalState)
        {
            _subSlots = subSlots;
            _allReferees = allReferees;
            _globalState = globalState;
            _bestSubAssignment = new Dictionary<Slot, Referee?>();
        }

        public (Dictionary<Slot, Referee?>, double) Solve()
        {
            Backtrack(0, 0, new Dictionary<Slot, Referee?>());
            return (_bestSubAssignment, _bestSubCost);
        }

        private void Backtrack(int idx, double currentCost, Dictionary<Slot, Referee?> currentMap)
        {
            if (currentCost >= _bestSubCost) return;

            if (idx == _subSlots.Count)
            {
                _bestSubCost = currentCost;
                _bestSubAssignment = new Dictionary<Slot, Referee?>(currentMap);
                return;
            }

            var slot = _subSlots[idx];
            foreach (var refObj in _allReferees)
            {
                if (IsFeasibleInGlobal(slot, refObj, currentMap))
                {
                    currentMap[slot] = refObj;
                    Backtrack(idx + 1, currentCost + AssignmentCost(slot, refObj), currentMap);
                    currentMap.Remove(slot);
                }
            }
        }

        private bool IsFeasibleInGlobal(Slot slot, Referee referee, Dictionary<Slot, Referee?> local)
        {
            // Kontrola proti lokálně vybraným v rámci B&B
            foreach (var l in local)
                if (l.Value.Id == referee.Id && slot.Start < l.Key.End && l.Key.Start < slot.End) return false;

            // Kontrola proti fixním v globálním stavu
            foreach (var g in _globalState)
                if (g.Value != null && g.Value.Id == referee.Id && slot.Start < g.Key.End && g.Key.Start < slot.End) return false;

            return true;
        }

        private double AssignmentCost(Slot slot, Referee referee) => Math.Abs(slot.RequiredRank - referee.Rank) + referee.Location.DistanceTo(slot.Location);
    }
}