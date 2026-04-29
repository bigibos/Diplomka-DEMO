using Diplomka.Entity;

namespace Diplomka.Solver
{
    /// <summary>
    /// Branch &amp; Bound solver pro Referee Assignment Problem (RAP) implementovaný
    /// dle algoritmu Ross &amp; Soland (1975) pro Generalized Assignment Problem.
    ///
    /// ── Přehled algoritmu ─────────────────────────────────────────────────────────
    ///
    ///  Každý uzel B&amp;B stromu (kandidátský problém) reprezentuje dílčí přiřazení,
    ///  v němž jsou některé dvojice slot–rozhodčí FIXOVÁNY (x = 1) nebo VYLOUČENY (x = 0).
    ///
    ///  1. RELAXACE (PR)
    ///     Každý slot je nezávisle přiřazen k nejlevnějšímu způsobilému rozhodčímu,
    ///     přičemž se IGNORUJÍ časové konflikty mezi sloty přiřazenými témuž rozhodčímu.
    ///     Výsledek dává dolní mez Z pro daný kandidátský problém.
    ///
    ///  2. ŘEŠENÍ KONFLIKTŮ – KNAPSACK PODPROBLÉMY (analogie k PK_i z Ross &amp; Soland)
    ///     Pro každého rozhodčího r, jehož sloty z relaxace se časově překrývají,
    ///     hledáme minimální-penalizační množinu slotů k odebrání tak, aby zbývající
    ///     sloty byly pairwise bezkonfliktní.
    ///
    ///     Penalizace za odebrání slotu j od rozhodčího r:
    ///       p_j = min_{k způsobilý pro j, k ≠ r, k nevyloučen} cost(j, k) − cost(j, r)
    ///
    ///     Podproblém je řešen přesnou enumerací podmnožin (praktické: |konflikty| ≤ ~10).
    ///     Pro n > 20 padback na matching lower bound (admisibilní).
    ///
    ///  3. DOLNÍ MEZ
    ///     LB = Z + Σ_r penalty(r)   – admisibilní (nikdy nepřeceňuje skutečné optimum).
    ///
    ///  4. TEST PŘÍPUSTNOSTI
    ///     Relaxační řešení je přípustné pro (P) právě tehdy, gdy žádný rozhodčí
    ///     nemá dva či více konfliktních slotů (conflictDetails.Count == 0).
    ///
    ///  5. VÝBĚR VĚTVÍCÍ PROMĚNNÉ (t-ratio, Ross &amp; Soland §2)
    ///     Pro každý konfliktní slot j přiřazený rozhodčímu r definujeme:
    ///       t(j, r) = p_j / C(j, r)
    ///     kde C(j, r) = počet slotů rozhodčího r, se kterými j koliduje.
    ///     Větvíme na dvojici (j*, r*) s nejvyšším t – slot, který má největší
    ///     "oprávnění" zůstat u rozhodčího r vzhledem ke způsobovaným konfliktům.
    ///     Jedno dítě fixuje x_{j*,r*} = 1, druhé nastavuje x_{j*,r*} = 0.
    ///
    ///  6. LIFO – prohledávání do hloubky
    ///     Kandidátské problémy jsou udržovány v zásobníku (Stack).
    ///     Větev s x = 1 ("ponechat přiřazení") se prozkoumá jako první,
    ///     což rychle produkuje přípustná řešení a zostřuje UB.
    ///
    /// ── Admisibilita dolní meze ──────────────────────────────────────────────────
    ///   Z ≤ OPT, protože PR relaxuje konfliktní omezení.
    ///   Penalizační korekce jsou spočteny přesně (nebo konzervativně dolním odhadem),
    ///   takže LB = Z + Σ penalties ≤ OPT pro každý kandidátský problém.
    ///
    /// ── Odkaz ────────────────────────────────────────────────────────────────────
    ///   G. T. Ross &amp; R. M. Soland, "A Branch and Bound Algorithm for the
    ///   Generalized Assignment Problem", Mathematical Programming 8 (1975) 91–103.
    /// </summary>
    public class RossSolandBBSolver : ISolver
    {
        // ── Konfigurace ────────────────────────────────────────────────────────────
        private readonly List<Referee> _referees;
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly TimeSpan _timeLimit;

        /// <summary>
        /// Penalizace použitá, pokud pro slot neexistuje žádný alternativní rozhodčí.
        /// Dostatečně velká, aby taková větev byla vždy ořezána.
        /// </summary>
        private const double NoAlternativePenalty = 1e6;

        // ── Výsledky hledání ────────────────────────────────────────────────────────
        private State? _bestState;
        private double _bestCost = double.MaxValue;
        private DateTime _startTime;

        public long NodesExplored { get; private set; }
        public double BestCost => _bestCost;

        // ── Předvypočítaná data (nastavují se jednou v Precompute) ─────────────────

        private Slot[] _slots = Array.Empty<Slot>();
        private Referee[] _refs = Array.Empty<Referee>();
        private int _S;   // počet slotů
        private int _R;   // počet rozhodčích

        // _slotConflicts[i][j]: true, pokud slot i a slot j mají časový konflikt
        // (Overlaps = přiřazení obou témuž rozhodčímu by bylo neplatné)
        private bool[][] _slotConflicts = Array.Empty<bool[]>();

        // Flat matice cen: _cost[i * R + j] = statická cena přiřazení ref j ke slotu i
        private double[] _cost = Array.Empty<double>();

        // _rankOk[i]: seznam indexů rozhodčích s dostatečnou hodností pro slot i
        private int[][] _rankOk = Array.Empty<int[]>();

        // ── Kandidátský problém ────────────────────────────────────────────────────

        /// <summary>
        /// Uzel B&amp;B stromu. Kóduje, které dvojice slot–rozhodčí jsou FIXOVÁNY (x = 1)
        /// nebo VYLOUČENY (x = 0). Zbytek je volný a rozhoduje relaxace PR.
        /// </summary>
        private sealed class CandidateProblem
        {
            /// <summary>
            /// fixedRef[i] ≥ 0: slot i je pevně přiřazen k tomuto rozhodčímu.
            /// fixedRef[i] = -1: slot i je volný.
            /// </summary>
            public readonly int[] FixedRef;

            /// <summary>
            /// excluded[i * R + j] = true: rozhodčí j nesmí být přiřazen ke slotu i.
            /// Flat pole pro O(1) přístup (cache-friendly).
            /// </summary>
            public readonly bool[] Excluded;

            /// <summary>Dolní mez zděděná z nadřazeného uzlu (pro logování).</summary>
            public double InheritedLB;

            public CandidateProblem(int S, int R)
            {
                FixedRef = new int[S];
                Array.Fill(FixedRef, -1);
                Excluded = new bool[S * R];
            }

            private CandidateProblem(int[] fixedRef, bool[] excluded, double lb)
            {
                FixedRef = (int[])fixedRef.Clone();
                Excluded = (bool[])excluded.Clone();
                InheritedLB = lb;
            }

            public CandidateProblem Clone() => new(FixedRef, Excluded, InheritedLB);
        }

        // ── Konstruktor ────────────────────────────────────────────────────────────

        public RossSolandBBSolver(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            TimeSpan? timeLimit = null)
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _timeLimit = timeLimit ?? TimeSpan.FromSeconds(60);
        }

        // ── Veřejné vstupní body ────────────────────────────────────────────────────

        /// <summary>Solve s greedy warm startem.</summary>
        public State Solve(IEnumerable<Slot> slots)
        {
            var slotList = slots.ToList();

            Console.WriteLine("[RS-BB] Spouštím greedy warm start...");
            var greedy = new GreedySolver(_referees, _conflictChecker, _costCalculator)
                .Solve(slotList);

            if (greedy.GetEmptySlots().Any())
            {
                Console.WriteLine("[RS-BB] Repair heuristika pro nezaplněné sloty...");
                greedy = new RepairHeuristic(_referees, _conflictChecker, _costCalculator)
                    .Repair(greedy);
            }

            _bestState = greedy;
            _bestCost = _costCalculator.TotalCost(greedy);
            Console.WriteLine($"[RS-BB] Počáteční UB (greedy + repair): {_bestCost:F2}");

            return RunBB(slotList);
        }

        /// <summary>Solve s externím warm startem (State jako horní mez).</summary>
        public State Solve(State warmStart)
        {
            _bestState = (State)warmStart.Clone();
            _bestCost = _costCalculator.TotalCost(warmStart);
            Console.WriteLine($"[RS-BB] Warm start UB: {_bestCost:F2}");
            return RunBB(warmStart.GetSlots().ToList());
        }

        // ── Hlavní smyčka B&B ──────────────────────────────────────────────────────

        private State RunBB(List<Slot> slotList)
        {
            _startTime = DateTime.UtcNow;
            NodesExplored = 0;

            Precompute(slotList);

            // Kořenový kandidátský problém – žádné fixace ani vyloučení
            var root = new CandidateProblem(_S, _R);
            var stack = new Stack<CandidateProblem>();
            stack.Push(root);

            while (stack.Count > 0)
            {
                if (DateTime.UtcNow - _startTime > _timeLimit)
                {
                    Console.WriteLine("[RS-BB] Časový limit vypršel.");
                    break;
                }

                var cp = stack.Pop();
                NodesExplored++;

                // ── Krok 1: Řešení relaxace PR ─────────────────────────────────────
                // Každý slot → nejlevnější způsobilý, nevyloučený rozhodčí (ignorujeme konflikty).
                // Fixovaná přiřazení z cp jsou respektována.
                var (Z, assignment, infeasibleSlot) = SolveRelaxation(cp);

                // Alespoň jeden slot nemá žádného způsobilého rozhodčího → větev je mrtvá.
                if (infeasibleSlot >= 0)
                    continue;

                // Rychlé ořezání jen na základě Z (před výpočtem knapsacků)
                if (Z >= _bestCost - 1e-9)
                    continue;

                // ── Krok 2: Augmentace dolní meze pomocí knapsack podproblémů ───────
                // Pro každého rozhodčího s konfliktními sloty: min-penalizační odebrání.
                var (lb, conflictDetails) = ComputeLowerBound(Z, assignment, cp);

                if (lb >= _bestCost - 1e-9)
                    continue; // ořez

                // ── Krok 3: Test přípustnosti ───────────────────────────────────────
                // Relaxace je přípustná pro (P) právě tehdy, je-li conflictDetails prázdný.
                if (conflictDetails.Count == 0)
                {
                    // Sestavíme State a ověříme skutečnou (kontextovou) cenu
                    var candidateState = BuildState(assignment);
                    double actualCost = _costCalculator.TotalCost(candidateState);

                    if (actualCost < _bestCost)
                    {
                        _bestCost = actualCost;
                        _bestState = candidateState;
                        Console.WriteLine(
                            $"[RS-BB] ★ Nové optimum! Skutečná cena: {_bestCost:F2} " +
                            $"(statická LB: {lb:F2}) v uzlu {NodesExplored:N0}");
                    }
                    continue;
                }

                // ── Krok 4: Výběr větvící proměnné (t-ratio) ────────────────────────
                var (branchSlot, branchRef) = SelectBranchingVariable(assignment, conflictDetails, cp);

                if (branchSlot < 0)
                    continue; // bezpečnostní zarážka (nenastane při neprázdném conflictDetails)

                // ── Krok 5: Větvení ─────────────────────────────────────────────────
                // "One-branch":  x_{branchSlot, branchRef} = 1  (slot zůstane u rozhodčího)
                var cpOne = cp.Clone();
                cpOne.FixedRef[branchSlot] = branchRef;
                cpOne.InheritedLB = lb;

                // "Zero-branch": x_{branchSlot, branchRef} = 0  (slot se musí přesunout)
                var cpZero = cp.Clone();
                cpZero.Excluded[branchSlot * _R + branchRef] = true;
                cpZero.InheritedLB = lb;

                // LIFO: zero-branch na zásobník první → one-branch se prozkoumá jako první.
                // Dle Ross & Soland: "the candidate problem in which the separation variable
                // is fixed to one is the problem examined next."
                stack.Push(cpZero);
                stack.Push(cpOne);

                if (NodesExplored % 500 == 0)
                    LogProgress(stack.Count, lb);
            }

            Console.WriteLine(
                $"[RS-BB] Hotovo. Uzlů: {NodesExplored:N0}, nejlepší cena: {_bestCost:F2}");
            return _bestState!;
        }

        private void LogProgress(int stackSize, double lb) =>
            Console.WriteLine(
                $"[RS-BB] Uzlů: {NodesExplored,8:N0} | zásobník: {stackSize,5} | " +
                $"LB: {lb,10:F2} | UB: {_bestCost,10:F2} | " +
                $"čas: {(DateTime.UtcNow - _startTime).TotalSeconds:F1}s");

        // ── Předvýpočet ────────────────────────────────────────────────────────────

        private void Precompute(List<Slot> slots)
        {
            _slots = slots.ToArray();
            _refs = _referees.ToArray();
            _S = _slots.Length;
            _R = _refs.Length;

            // Matice časových konfliktů – O(S²)
            // _slotConflicts[i][j] = true iff přiřazení slotů i a j témuž rozhodčímu je nepřípustné.
            _slotConflicts = new bool[_S][];
            for (int i = 0; i < _S; i++)
            {
                _slotConflicts[i] = new bool[_S];
                for (int j = 0; j < _S; j++)
                    if (i != j)
                        _slotConflicts[i][j] = _conflictChecker.Overlaps(_slots[i], _slots[j]);
            }

            // Rank-způsobilé množiny – O(S·R)
            // Poznámka: používáme stejný threshold jako BBSolver (Rank >= RequiredRank).
            // Pokud je v konfiguraci RankDiffMargin > 0, ConflictChecker.CanAssign je benevolentnější;
            // tato implementace je konzervativnější (přísnější → LB zůstává admisibilní).
            _rankOk = new int[_S][];
            for (int i = 0; i < _S; i++)
            {
                var ok = new List<int>(_R);
                for (int j = 0; j < _R; j++)
                    if (_refs[j].Rank >= _slots[i].RequiredRank)
                        ok.Add(j);
                _rankOk[i] = ok.ToArray();
            }

            // Statická matice cen – O(S·R)
            // Tyto ceny se použijí v relaxaci a knapsackách.
            // Kontextová složka (AssignmentCost(State, slot, ref)) se uplatní pouze
            // při vyhodnocení listových uzlů přes TotalCost(State).
            _cost = new double[_S * _R];
            for (int i = 0; i < _S; i++)
                for (int j = 0; j < _R; j++)
                    _cost[i * _R + j] = _costCalculator.AssignmentCost(_slots[i], _refs[j]);

            int maxConflictsPerSlot = _slotConflicts.Max(r => r.Count(x => x));
            double avgConflicts = _slotConflicts.Average(r => r.Count(x => x));
            Console.WriteLine(
                $"[RS-BB] Předvýpočet hotov: {_S} slotů, {_R} rozhodčích | " +
                $"konflikty/slot: prům. {avgConflicts:F1}, max. {maxConflictsPerSlot}");
        }

        // ── Relaxace PR ────────────────────────────────────────────────────────────

        /// <summary>
        /// Řeší relaxaci PR pro daný kandidátský problém.
        ///
        /// Pro každý slot i (zleva doprava):
        ///   – Je-li i fixován v cp → použij fixovaného rozhodčího.
        ///   – Jinak → vyber nejlevnějšího způsobilého, nevyloučeného rozhodčího.
        ///
        /// Vrací:
        ///   Z               – celková cena relaxačního přiřazení.
        ///   assignment[]    – assignment[i] = index rozhodčího pro slot i.
        ///   infeasibleSlot  – index prvního slotu bez způsobilého rozhodčího (-1 = všechny OK).
        ///
        /// Časová složitost: O(S · R_avg) kde R_avg = průměrná velikost _rankOk[i].
        /// </summary>
        private (double Z, int[] assignment, int infeasibleSlot) SolveRelaxation(CandidateProblem cp)
        {
            int[] assignment = new int[_S];
            double Z = 0.0;

            for (int i = 0; i < _S; i++)
            {
                // Respektujeme fixovaná přiřazení
                if (cp.FixedRef[i] >= 0)
                {
                    int r = cp.FixedRef[i];
                    assignment[i] = r;
                    Z += _cost[i * _R + r];
                    continue;
                }

                // Nejlevnější způsobilý, nevyloučený rozhodčí
                double minC = double.MaxValue;
                int bestR = -1;

                foreach (int j in _rankOk[i])
                {
                    if (cp.Excluded[i * _R + j]) continue;
                    double c = _cost[i * _R + j];
                    if (c < minC) { minC = c; bestR = j; }
                }

                if (bestR < 0)
                    return (double.MaxValue, assignment, i); // nepřípustná větev → prune

                assignment[i] = bestR;
                Z += minC;
            }

            return (Z, assignment, -1);
        }

        // ── Dolní mez (Z + Σ knapsack penalizace) ─────────────────────────────────

        /// <summary>
        /// Augmentuje relaxační dolní mez Z o součet minimálních penalizací za
        /// rozřešení konfliktů každého přetíženého rozhodčího.
        ///
        /// Analogie: Z odpovídá hodnotě Z z Ross &amp; Soland (§2, strana 93–94).
        /// Penalizace odpovídají hodnotám z*_i ze součtu Σ_{i∈I'} z*_i.
        /// Výsledek LB = Z + Σ penalties je validní dolní mez pro (P).
        ///
        /// Vrací:
        ///   lb             – augmentovaná dolní mez.
        ///   conflictDetails – per-rozhodčí info o konfliktech (prázdné = přípustné řešení).
        /// </summary>
        private (double lb, List<ConflictInfo> conflictDetails)
            ComputeLowerBound(double Z, int[] assignment, CandidateProblem cp)
        {
            // Seskupíme sloty podle přiřazeného rozhodčího
            var refToSlots = new Dictionary<int, List<int>>(_R);
            for (int i = 0; i < _S; i++)
            {
                int r = assignment[i];
                if (!refToSlots.TryGetValue(r, out var list))
                    refToSlots[r] = list = new List<int>();
                list.Add(i);
            }

            double totalPenalty = 0.0;
            var conflictDetails = new List<ConflictInfo>();

            foreach (var (refIdx, slotsOfRef) in refToSlots)
            {
                if (slotsOfRef.Count < 2) continue;

                // Najdeme sloty zapojené do alespoň jednoho konfliktu u tohoto rozhodčího
                var conflictingSlots = FindConflictingSlots(slotsOfRef);
                if (conflictingSlots.Count == 0) continue;

                // Řešení knapsack podproblému PK_r: minimální penalizace za odebrání slotů
                double penalty = SolveConflictKnapsack(refIdx, conflictingSlots, cp);
                totalPenalty += penalty;

                conflictDetails.Add(new ConflictInfo(refIdx, conflictingSlots, slotsOfRef));
            }

            return (Z + totalPenalty, conflictDetails);
        }

        /// <summary>Vrátí seznam slotů ze <paramref name="slotsOfRef"/> zapojených do alespoň jednoho konfliktu.</summary>
        private List<int> FindConflictingSlots(List<int> slotsOfRef)
        {
            var involved = new HashSet<int>();
            for (int a = 0; a < slotsOfRef.Count; a++)
                for (int b = a + 1; b < slotsOfRef.Count; b++)
                    if (_slotConflicts[slotsOfRef[a]][slotsOfRef[b]])
                    {
                        involved.Add(slotsOfRef[a]);
                        involved.Add(slotsOfRef[b]);
                    }
            return [.. involved];
        }

        // ── Knapsack podproblém PK_r ────────────────────────────────────────────────

        /// <summary>
        /// Computes the minimum-penalty set of slots to remove from referee
        /// <paramref name="refIdx"/> so that the remaining slots are pairwise conflict-free.
        ///
        /// RAP analogue of the binary knapsack PK_i from Ross &amp; Soland (1975).
        ///
        /// Penalizace za odebrání slotu j od rozhodčího r:
        ///   p_j = max(0, min_{k ∈ způsobilí pro j, k ≠ r, k nevyloučen} cost(j,k) − cost(j,r))
        ///
        /// Implementace:
        ///   n ≤ 20 → přesná enumerace podmnožin (O(2^n · n²))
        ///   n > 20 → matching lower bound (admisibilní, konzervativnější dolní odhad)
        ///
        /// Poznámka: v reálných instancích RAP bývá n ≤ 6, přesná větev pokryje vše.
        /// </summary>
        private double SolveConflictKnapsack(
            int refIdx,
            List<int> conflictingSlots,
            CandidateProblem cp)
        {
            int n = conflictingSlots.Count;

            // Penalizace za odebrání každého konfliktuijícího slotu
            double[] penalty = new double[n];
            for (int k = 0; k < n; k++)
            {
                int slotI = conflictingSlots[k];
                double currentCost = _cost[slotI * _R + refIdx];
                double minAlt = double.MaxValue;

                foreach (int j in _rankOk[slotI])
                {
                    if (j == refIdx || cp.Excluded[slotI * _R + j]) continue;
                    double c = _cost[slotI * _R + j];
                    if (c < minAlt) minAlt = c;
                }

                // p_j ≥ 0 vždy: v relaxaci je refIdx nejlevnější dostupný,
                // takže jakákoli alternativa je stejně drahá nebo dražší.
                // Math.Max chrání před floating-point nepřesnostmi.
                penalty[k] = minAlt == double.MaxValue
                    ? NoAlternativePenalty
                    : Math.Max(0.0, minAlt - currentCost);
            }

            // Lokální matice konfliktů mezi conflictingSlots
            bool[,] localConf = new bool[n, n];
            for (int a = 0; a < n; a++)
                for (int b = a + 1; b < n; b++)
                    if (_slotConflicts[conflictingSlots[a]][conflictingSlots[b]])
                        localConf[a, b] = localConf[b, a] = true;

            return n <= 20
                ? ExactMinPenaltyRemoval(n, penalty, localConf)
                : MatchingLowerBoundPenalty(n, penalty, localConf);
        }

        /// <summary>
        /// Přesné minimum-penalizační odebrání pomocí enumerace bitmasků.
        /// O(2^n · n²); pro n ≤ 20 (typicky 2–6) je to rychlé.
        ///
        /// Pro každou podmnožinu slotů k odebrání (kódovanou jako bitmask):
        ///   – ověříme, zda zbývající sloty jsou pairwise bezkonfliktní,
        ///   – pokud ano, zaznamenáme celkovou penalizaci podmnožiny.
        /// Vrátíme minimum přes všechny platné podmnožiny.
        /// </summary>
        private static double ExactMinPenaltyRemoval(int n, double[] penalty, bool[,] conf)
        {
            double best = double.MaxValue;

            for (int mask = 0; mask < (1 << n); mask++)
            {
                // Ověříme, zda odebráním 'mask' zmizí všechny konflikty
                bool valid = true;
                for (int a = 0; a < n && valid; a++)
                {
                    if ((mask >> a & 1) == 1) continue; // a odebráno
                    for (int b = a + 1; b < n && valid; b++)
                    {
                        if ((mask >> b & 1) == 1) continue; // b odebráno
                        if (conf[a, b]) valid = false;       // zbylý konflikt!
                    }
                }

                if (!valid) continue;

                double p = 0.0;
                for (int k = 0; k < n; k++)
                    if ((mask >> k & 1) == 1)
                        p += penalty[k];

                if (p < best) best = p;
            }

            // Odebrání všech slotů (mask = (1<<n)-1) je vždy platné →
            // best != MaxValue zaručeno.
            return best;
        }

        /// <summary>
        /// Admisibilní dolní mez penalizace pro velké konfliktuijící množiny (n > 20).
        ///
        /// Princip (matching lower bound):
        ///   Pro každou konfliktní hranu (a, b) musí být alespoň jeden z vrcholů odebrán,
        ///   čímž vzniká cena ≥ min(p_a, p_b). Při maximálním párování (matchingu)
        ///   v konfliktuijícím grafu jsou hrany disjunktní → žádné dvojnásobné počítání.
        ///   Součet min(p_a, p_b) přes matching je admisibilní dolní mezí pro minimum vertex cover.
        ///
        /// Poznámka: v praxi n > 20 nenastane; tato větev je čistě obranná.
        /// </summary>
        private static double MatchingLowerBoundPenalty(int n, double[] penalty, bool[,] conf)
        {
            bool[] matched = new bool[n];
            double lb = 0.0;

            for (int a = 0; a < n; a++)
            {
                if (matched[a]) continue;
                for (int b = a + 1; b < n; b++)
                {
                    if (matched[b] || !conf[a, b]) continue;

                    // Hrana (a, b) přidá do dolní meze min(p_a, p_b)
                    lb += Math.Min(penalty[a], penalty[b]);
                    matched[a] = matched[b] = true;
                    break;
                }
            }

            return lb;
        }

        // ── Výběr větvící proměnné ─────────────────────────────────────────────────

        /// <summary>
        /// Vybere větvící dvojici (slotIdx, refIdx) dle t-ratio z Ross &amp; Soland (§2):
        ///
        ///   t(j, r) = p_j / C(j, r)
        ///
        /// kde p_j = penalizace za odebrání slotu j od rozhodčího r,
        ///     C(j, r) = počet slotů rozhodčího r, s nimiž j koliduje.
        ///
        /// Větvíme na dvojici s nejvyšším t:
        ///   – Slot s vysokým t má velkou penalizaci za přesun a způsobuje málo konfliktů,
        ///     takže je "nejhodnotnější" ponechat u současného rozhodčího.
        ///   – Tato volba vede k rychlému prořezávání obou větví.
        ///
        /// Fixovaná přiřazení (cp.FixedRef[i] >= 0) jsou přeskočena – nelze je větvit.
        /// </summary>
        private (int slotIdx, int refIdx) SelectBranchingVariable(
            int[] assignment,
            List<ConflictInfo> conflictDetails,
            CandidateProblem cp)
        {
            double bestT = -1;
            int bestSlot = -1;
            int bestRef = -1;

            foreach (var ci in conflictDetails)
            {
                int refIdx = ci.RefIdx;

                foreach (int slotI in ci.ConflictingSlots)
                {
                    // Fixované sloty nelze větvit
                    if (cp.FixedRef[slotI] >= 0) continue;

                    // Penalizace p_j pro tento slot a rozhodčího
                    double currentCost = _cost[slotI * _R + refIdx];
                    double minAlt = double.MaxValue;
                    foreach (int j in _rankOk[slotI])
                    {
                        if (j == refIdx || cp.Excluded[slotI * _R + j]) continue;
                        double c = _cost[slotI * _R + j];
                        if (c < minAlt) minAlt = c;
                    }
                    double pJ = minAlt == double.MaxValue
                        ? NoAlternativePenalty
                        : Math.Max(0.0, minAlt - currentCost);

                    // C(j, r) = počet konfliktů slotu slotI s ostatními sloty refIdx
                    int conflictCount = ci.ConflictingSlots.Count(s => s != slotI && _slotConflicts[slotI][s]);
                    if (conflictCount < 1) conflictCount = 1; // dělení nulou

                    double t = pJ / conflictCount;

                    if (t > bestT)
                    {
                        bestT = t;
                        bestSlot = slotI;
                        bestRef = refIdx;
                    }
                }
            }

            return (bestSlot, bestRef);
        }

        // ── Pomocné struktury a metody ─────────────────────────────────────────────

        /// <summary>
        /// Informace o konfliktech rozhodčího v rámci jednoho uzlu B&amp;B stromu.
        /// Předávána z ComputeLowerBound do SelectBranchingVariable.
        /// </summary>
        private sealed class ConflictInfo
        {
            /// <summary>Index rozhodčího s konfliktními sloty.</summary>
            public readonly int RefIdx;

            /// <summary>
            /// Sloty přiřazené tomuto rozhodčímu, které se účastní alespoň jednoho konfliktu.
            /// </summary>
            public readonly List<int> ConflictingSlots;

            /// <summary>Všechny sloty přiřazené tomuto rozhodčímu v relaxaci.</summary>
            public readonly List<int> AllSlotsOfRef;

            public ConflictInfo(int refIdx, List<int> conflictingSlots, List<int> allSlotsOfRef)
            {
                RefIdx = refIdx;
                ConflictingSlots = conflictingSlots;
                AllSlotsOfRef = allSlotsOfRef;
            }
        }

        /// <summary>
        /// Sestaví objekt State z pole přiřazení assignment[].
        /// Volá se pouze při nalezení nového optima → není ve smyčce.
        /// </summary>
        private State BuildState(int[] assignment)
        {
            var state = new State();
            for (int i = 0; i < _S; i++)
                state.AddSlot(_slots[i]);
            for (int i = 0; i < _S; i++)
                if (assignment[i] >= 0)
                    state.SetReferee(_slots[i], _refs[assignment[i]]);
            return state;
        }
    }
}