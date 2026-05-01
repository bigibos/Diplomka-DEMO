using Diplomka.Entity;

namespace Diplomka.Solver
{
    /// <summary>
    /// Branch &amp; Bound solver pro Referee Assignment Problem.
    ///
    /// ── Architektura ──────────────────────────────────────────────────────────────
    ///
    ///  1. GREEDY FÁZE
    ///     Sestaví počáteční přiřazení greedy heuristikou. Výsledek slouží
    ///     jako horní mez (UB) pro B&amp;B.
    ///
    ///  2. REPAIR FÁZE
    ///     Pokud greedy nezaplní všechny sloty, chain-repair heuristika doplní zbývající.
    ///
    ///  3. PŘEDVÝPOČET (jednou před DFS)
    ///     - Matice překryvů slotů: _overlaps[i] = pole indexů slotů překrývajících slot i
    ///     - Rank-eligible množiny: _rankOk[i] = pole indexů rozhodčích s dostatečnou hodností
    ///     - Matice cen (flat pole): _cost[i*R + j] = statická cena přiřazení ref j ke slotu i
    ///     Vše se počítá JEDNOU, O(S²) resp. O(S×R), a pak se jen čte.
    ///
    ///  4. BRANCH &amp; BOUND FÁZE (DFS s agresivním ořezáváním)
    ///
    ///     Větvení – MRV (Minimum Remaining Values):
    ///       Vybírá slot s nejmenším počtem *aktuálně* způsobilých rozhodčích.
    ///       Eligible množiny jsou udržovány inkrementálně (žádné volání GetEligibleReferees
    ///       uvnitř DFS), takže MRV je O(S) místo původního O(S×R).
    ///
    ///     Forward Checking:
    ///       Po každém přiřazení se okamžitě ověří, zda žádný zbývající slot
    ///       nezůstal bez způsobilého rozhodčího. Detekce mrtvé větve dříve
    ///       než při rekurzi.
    ///
    ///     Inkrementální dolní mez (Lower Bound):
    ///       _lbRemainder = Σ minCost[s] pro všechny nepřiřazené sloty s.
    ///       Po přiřazení se _lbRemainder updatuje pouze pro sloty dotčené
    ///       daným rozhodčím (sloty s překryvem), nikoli přepočítává od nuly.
    ///       LB = costSoFar + _lbRemainder je přípustná (admissible).
    ///
    ///     Ořezávání:
    ///       Pokud LB ≥ UB, větev se opustí.
    ///
    ///     Nulová alokace v horké smyčce:
    ///       Kandidáti, journal pro backtracking i pomocné pole jsou předalokovány
    ///       před DFS. Uvnitř rekurze nevzniká žádný heap objekt.
    ///
    /// ── Složitost ─────────────────────────────────────────────────────────────────
    ///   Předvýpočet: O(S² + S×R)
    ///   Nejhorší případ DFS: exponenciální; v praxi silně prořezáno greedy UB
    ///   a forward checkingem.
    ///
    /// ── Poznámka ke cost funkci ───────────────────────────────────────────────────
    ///   B&amp;B i dolní mez používají *statické* ceny (AssignmentCost(slot, ref) bez State).
    ///   Pokud vaše CostCalculator přidává stavově závislé penalizace (load-balancing apod.),
    ///   tyto nejsou zahrnuty a B&amp;B optimalizuje statickou složku. LB zůstává přípustná
    ///   za předpokladu staticka_cena ≤ skutečná_cena.
    /// </summary>
    public class BBSolver : ISolver
    {
        // ─── Konfigurace ──────────────────────────────────────────────────────────
        private readonly List<Referee> _referees;
        private readonly TimeSpan _timeLimit;
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;
        private readonly SolverConfiguration _config;

        // ─── Výsledky hledání ─────────────────────────────────────────────────────
        private State? _bestState;
        private double _bestCost;
        private DateTime _startTime;
        private bool _timeLimitExceeded;

        private double _bestActualCost; // Skutečná cena pro UB a konečný výsledek
        private double _bestStaticCost; // Pouze pro logování, abychom věděli, jak se mění statická složka

        public long NodesExplored { get; private set; }
        public double BestCost => _bestActualCost;

        // ─── Předvypočítaná data (nastavují se jednou v Precompute) ───────────────

        // Indexované pole slotů a rozhodčích pro O(1) přístup
        private Slot[] _slots = Array.Empty<Slot>();
        private Referee[] _refs = Array.Empty<Referee>();
        private int _S; // počet slotů
        private int _R; // počet rozhodčích

        // _overlaps[i] = pole indexů slotů, které se časově překrývají se slotem i
        private int[][] _overlaps = Array.Empty<int[]>();

        // _rankOk[i] = pole indexů rozhodčích s hodností >= RequiredRank slotu i
        // (rank-eligible nezávisí na přiřazeních, takže stačí předpočítat jednou)
        private int[][] _rankOk = Array.Empty<int[]>();

        // Flat matice cen: _cost[i * R + j] = AssignmentCost(slot_i, ref_j)
        // Flat pole je cache-friendlier než 2D array (row-major přístup v kritických smyčkách)
        private double[] _cost = Array.Empty<double>();

        // ─── Inkrementální DFS stav (mutuje se během hledání, žádná heap alokace) ─

        // _assigned[i] = index rozhodčího přiřazeného ke slotu i; -1 = nepřiřazeno
        private int[] _assigned = Array.Empty<int>();

        // _unassigned[i] = true právě tehdy, gdy slot i nemá rozhodčího
        private bool[] _unassigned = Array.Empty<bool>();

        // _eligible[i * R + j] = true, pokud je ref j aktuálně způsobilý pro slot i
        // Eligible = rank ok AND nepřiřazen ke koliznímu slotu.
        // Udržuje se inkrementálně: přiřazení → remove; backtrack → restore.
        private bool[] _eligible = Array.Empty<bool>();

        // _eligCnt[i] = počet aktuálně způsobilých rozhodčích pro slot i
        private int[] _eligCnt = Array.Empty<int>();

        // _minCost[i] = min(_cost[i*R+j]) přes všechny aktuálně eligible j pro slot i
        // Nebo 0.0 pokud je slot i přiřazen (nepřispívá do _lbRemainder)
        private double[] _minCost = Array.Empty<double>();

        // Inkrementální dolní mez: Σ _minCost[i] pro všechny nepřiřazené sloty i
        // Po přiřazení a backtrackingu se updatuje lokálně, nikoliv přepočítává.
        private double _lbRemainder;

        // ─── Předalokované buffery pro backtracking (žádná alokace v DFS) ─────────

        // Pro každou hloubku DFS ukládáme, které sloty byly ovlivněny přiřazením,
        // a jejich původní _minCost – potřebujeme to pro správný backtrack.
        private int[][] _journalSlots = Array.Empty<int[]>();     // [depth][k] = index ovlivněného slotu
        private double[][] _journalOldMin = Array.Empty<double[]>(); // [depth][k] = původní _minCost
        private int[] _journalLen = Array.Empty<int>();            // [depth] = počet záznamů v journalu
        private double[] _journalOldMinAssigned = Array.Empty<double>(); // [depth] = _minCost přiřazeného slotu před přiřazením

        // Předalokované pole kandidátů pro každou hloubku DFS (žádný List/LINQ v horké smyčce)
        private int[][] _candidateBuffers = Array.Empty<int[]>();  // [depth][k] = index ref kandidáta

        // ─── Inkrementální skutečná cena (per-rozhodčí) ──────────────────────────
        //
        // Statická LB (_lbRemainder) a skutečná UB (_bestActualCost) jsou různé veličiny
        // a jejich přímé porovnání způsobovalo nepřípustné ořezávání.
        // Řešení: udržujeme _actualCostSoFar inkrementálně.
        // LB = _actualCostSoFar + _lbRemainder je přípustná pokud
        // static_minCost <= skutečná_marginální_cena (platí pro izolovaný routing).

        // Per-rozhodčí seřazený seznam přiřazených slotů (index do _slots[])
        private List<int>[] _refSlots = Array.Empty<List<int>>();
        // Aktuální skutečná cena každého rozhodčího
        private double[] _refActualCost = Array.Empty<double>();
        // Součet _refActualCost = inkrementální skutečná cena přiřazení dosud
        private double _actualCostSoFar;
        // Journal pro backtrack skutečné ceny
        private int[] _journalAffectedRef = Array.Empty<int>();
        private double[] _journalOldRefCost = Array.Empty<double>();
        private double[] _journalOldActualCost = Array.Empty<double>();

        // ─── Konstruktor ──────────────────────────────────────────────────────────
        public BBSolver(
            IEnumerable<Referee> referees,
            ConflictChecker conflictChecker,
            CostCalculator costCalculator,
            SolverConfiguration config,
            TimeSpan? timeLimit = null)
        {
            _referees = referees.ToList();
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
            _config = config;
            _timeLimit = timeLimit ?? TimeSpan.FromSeconds(30);
        }

        // ─── Veřejné vstupní body ─────────────────────────────────────────────────

        /// <summary>Solve s externím warm startem (State jako horní mez).</summary>
        public State Solve(State state)
        {
            _bestState = (State)state.Clone();
            _bestActualCost = _costCalculator.TotalCost(state);

            // UB pro pruning = skutečná cena warm startu + skip penalizace.
            // LB = _actualCostSoFar (skutečná) + _lbRemainder (statická dolní mez zbytku).
            // Díky inkrementálnímu sledování skutečné ceny není potřeba 1.15 tolerance.
            int emptyCount = state.GetEmptySlots().Count();
            _bestStaticCost = _bestActualCost + emptyCount * _config.UnassignedCost;

            Console.WriteLine($"[B&B] Warm start - Skutečná: {_bestActualCost:F2} | UB pro pruning: {_bestStaticCost:F2}");
            return RunBB(state.GetSlots());
        }

        /// <summary>Solve od nuly: greedy warm start → repair → B&B.</summary>
        public State Solve(IEnumerable<Slot> slots)
        {
            var slotList = slots.ToList();

            Console.WriteLine("[B&B] Spouštím greedy heuristiku...");
            var greedy = new GreedySolver(_referees, _conflictChecker, _costCalculator).Solve(slotList);

            var emptyAfterGreedy = greedy.GetEmptySlots().ToList();
            if (emptyAfterGreedy.Count > 0)
            {
                Console.WriteLine($"[B&B] Greedy nezaplnil {emptyAfterGreedy.Count} slotů – spouštím repair...");
                greedy = new RepairHeuristic(_referees, _conflictChecker, _costCalculator).Repair(greedy);
            }


            _bestState = greedy;
            _bestActualCost = _costCalculator.TotalCost(greedy);

            // UB = skutečná cena greedy + penalizace za prázdné sloty.
            // B&B ve skip větvi platí stejnou UnassignedCost → srovnatelné veličiny.
            // Díky inkrementálnímu _actualCostSoFar není potřeba 1.15 tolerance.
            var emptySlots = greedy.GetEmptySlots().ToList();
            _bestStaticCost = _bestActualCost + emptySlots.Count * _config.UnassignedCost;

            Console.WriteLine($"[B&B] Počáteční cena - Skutečná: {_bestActualCost:F2} | UB pro pruning: {_bestStaticCost:F2}");


            return RunBB(slotList);
        }

        // ─── Orchestrace B&B ─────────────────────────────────────────────────────

        private State RunBB(IEnumerable<Slot> slotSource)
        {
            _startTime = DateTime.UtcNow;
            NodesExplored = 0;
            _timeLimitExceeded = false;

            var slotList = slotSource.ToList();
            Console.WriteLine($"[B&B] Předvýpočet pro {slotList.Count} slotů, {_referees.Count} rozhodčích...");
            Precompute(slotList);

            Console.WriteLine($"[B&B] Spouštím DFS (limit: {_timeLimit.TotalSeconds} s, UB statická: {_bestStaticCost:F2}, skutečná: {_bestActualCost:F2})...");
            InitSearchState();
            Dfs(depth: 0, costSoFar: 0.0, unassigned: _S);

            Console.WriteLine($"[B&B] Hotovo. Uzlů: {NodesExplored}, cena: {_bestActualCost:F2}");
            return _bestState!;
        }

        private double ComputeStaticCost(State state)
        {
            double staticCost = 0;
            foreach (var slot in state.GetSlots())
            {
                var referee = state.GetRefereeForSlot(slot);
                if (referee != null)
                {
                    staticCost += _costCalculator.AssignmentCost(slot, referee);
                }
            }
            return staticCost;
        }

        // ─── Předvýpočet ──────────────────────────────────────────────────────────

        private void Precompute(List<Slot> slots)
        {
            _slots = slots.ToArray();
            _refs = _referees.ToArray();
            _S = _slots.Length;
            _R = _refs.Length;

            // 1. Překryvy slotů – O(S²)
            //    Volání _conflictChecker.Overlaps je O(1), takže celkově O(S²).
            _overlaps = new int[_S][];
            for (int i = 0; i < _S; i++)
            {
                var ov = new List<int>(_S / 10); // heuristická kapacita
                for (int j = 0; j < _S; j++)
                    if (i != j && _conflictChecker.Overlaps(_slots[i], _slots[j]))
                        ov.Add(j);
                _overlaps[i] = ov.ToArray();
            }

            // 2. Rank-eligible množiny – O(S×R)
            _rankOk = new int[_S][];
            for (int i = 0; i < _S; i++)
            {
                var ok = new List<int>(_R);
                for (int j = 0; j < _R; j++)
                    if (_refs[j].Rank >= _slots[i].RequiredRank)
                        ok.Add(j);
                _rankOk[i] = ok.ToArray();
            }

            // 3. Statická matice cen – O(S×R)
            //    Flat pole: _cost[i*R+j], row-major = přístup přes ref-index je sekvenční
            _cost = new double[_S * _R];
            for (int i = 0; i < _S; i++)
                for (int j = 0; j < _R; j++)
                    _cost[i * _R + j] = _costCalculator.AssignmentCost(_slots[i], _refs[j]);

            // 4. Alokace mutable DFS polí
            _assigned = new int[_S];
            _unassigned = new bool[_S];
            _eligible = new bool[_S * _R];
            _eligCnt = new int[_S];
            _minCost = new double[_S];

            // 5. Předalokovaný backtrack journal a candidature buffery
            //    Maximální šířka journalu na jednu hloubku = maximální počet překryvů jednoho slotu
            int maxOverlaps = _overlaps.Length > 0
                ? _overlaps.Max(ov => ov.Length)
                : 0;

            _journalSlots = new int[_S][];
            _journalOldMin = new double[_S][];
            _journalLen = new int[_S];
            _journalOldMinAssigned = new double[_S];
            _candidateBuffers = new int[_S][];

            for (int d = 0; d < _S; d++)
            {
                _journalSlots[d] = new int[maxOverlaps];
                _journalOldMin[d] = new double[maxOverlaps];
                _candidateBuffers[d] = new int[_R]; // v nejhorším případě _R kandidátů
            }

            // 6. Per-rozhodčí struktury pro inkrementální skutečnou cenu
            _refSlots = new List<int>[_R];
            _refActualCost = new double[_R];
            for (int j = 0; j < _R; j++)
                _refSlots[j] = new List<int>(8); // průměrně málo slotů na rozhodčího

            _journalAffectedRef = new int[_S];
            _journalOldRefCost = new double[_S];
            _journalOldActualCost = new double[_S];

            Console.WriteLine($"[B&B] Předvýpočet hotov. " +
                $"Prům. překryvů/slot: {(_S > 0 ? _overlaps.Average(o => o.Length) : 0):F1}, " +
                $"max: {maxOverlaps}");
        }

        // ─── Inicializace DFS stavu ───────────────────────────────────────────────

        private void InitSearchState()
        {
            Array.Fill(_assigned, -1);
            Array.Fill(_unassigned, true);
            Array.Clear(_eligible, 0, _eligible.Length);

            // Eligible = rank ok (žádné přiřazení ještě neexistuje → žádná časová kolize)
            for (int i = 0; i < _S; i++)
            {
                foreach (int j in _rankOk[i])
                    _eligible[i * _R + j] = true;
                _eligCnt[i] = _rankOk[i].Length;
            }

            // Počáteční _lbRemainder = součet minCost přes všechny sloty
            _lbRemainder = 0.0;
            for (int i = 0; i < _S; i++)
            {
                _minCost[i] = ComputeSlotMinCost(i);
                _lbRemainder += _minCost[i];
            }

            // Reset per-rozhodčí skutečné ceny
            for (int j = 0; j < _R; j++)
            {
                _refSlots[j].Clear();
                _refActualCost[j] = 0.0;
            }
            _actualCostSoFar = 0.0;
        }



        // ─── Jádro B&B: DFS s forward checkingem ──────────────────────────────────

        /// <summary>
        /// Rekurzivní DFS.
        /// <param name="depth">Hloubka rekurze (= počet dosud přiřazených slotů).</param>
        /// <param name="costSoFar">Součet statických cen dosud přiřazených slotů.</param>
        /// <param name="unassigned">Počet dosud nepřiřazených slotů.</param>
        /// </summary>
        private void Dfs(int depth, double costSoFar, int unassigned)
        {
            NodesExplored++;

            // Periodická kontrola časového limitu (každých 10 000 uzlů)
            if (NodesExplored % 250_000 == 0)
            {
                Console.WriteLine(
                    $"[B&B] Uzlů: {NodesExplored,10:N0} | hloubka: {depth,4} | " +
                    $"LB: {_actualCostSoFar + costSoFar + _lbRemainder,10:F2} | UB: {_bestStaticCost,10:F2} | " +
                    $"Empty: {unassigned} | " +
                    $"čas: {(DateTime.UtcNow - _startTime).TotalSeconds:F1}s");

                if (DateTime.UtcNow - _startTime > _timeLimit)
                    _timeLimitExceeded = true;
            }

            if (_timeLimitExceeded) return;


            // ── Listový uzel: všechny sloty zpracovány ──────────────────────────────
            if (unassigned == 0)
            {
                // costSoFar = součet UnassignedCost za přeskočené sloty (assign větve ho nemění).
                // _actualCostSoFar = přesná skutečná cena tras všech přiřazených slotů.
                // Celková cena = obě složky dohromady.
                double leafCost = _actualCostSoFar + costSoFar;

                if (leafCost < _bestStaticCost)
                {
                    _bestStaticCost = leafCost;
                    _bestActualCost = leafCost;
                    _bestState = BuildState();
                    Console.WriteLine($"[B&B] -*- Nové optimum! Skutečná+skip: {leafCost:F2} | uzel {NodesExplored:N0}");
                }
                return;
            }

            // ── MRV: výběr nejkonstrikovanějšího nepřiřazeného slotu ──────────────
            int slotIdx = SelectMrvSlot();

            // ── Assign větve: zkusíme přiřadit každého způsobilého rozhodčího ────
            // (Pokud _eligCnt == 0, tuto část přeskočíme a jdeme rovnou na skip větev.)
            if (_eligCnt[slotIdx] > 0)
            {
                int[] candBuf = _candidateBuffers[depth];
                int cCount = 0;
                int slotBase = slotIdx * _R;

                foreach (int j in _rankOk[slotIdx])
                    if (_eligible[slotBase + j])
                        candBuf[cCount++] = j;

                // Řazení podle statické ceny (value ordering – levnější rozhodčí mají větší
                // šanci vést k lepšímu řešení a budou prozkoumáni dříve).
                InsertionSortByCost(candBuf, cCount, slotBase);

                for (int ci = 0; ci < cCount; ci++)
                {
                    int refIdx = candBuf[ci];

                    // Pre-pruning: odhadneme LB pomocí statické ceny tohoto přiřazení.
                    // Skutečná cena bude >= statická, takže toto je přípustný dolní odhad.
                    double staticAssignCost = _cost[slotBase + refIdx];
                    double lbEstimate = (_actualCostSoFar + staticAssignCost)
                                      + costSoFar
                                      + (_lbRemainder - _minCost[slotIdx]);
                    if (lbEstimate >= _bestStaticCost * (1.0 - _config.RelativeGap))
                        continue; // prune before assign

                    // Assign: aktualizuje _actualCostSoFar, _eligible, _lbRemainder.
                    // costSoFar se NEMĚNÍ – assign větve nenesou skip penalizaci.
                    Assign(depth, slotIdx, refIdx, out bool forwardOk);

                    if (forwardOk)
                    {
                        // Post-pruning s přesnou skutečnou cenou (po Assign ji známe).
                        double lb = _actualCostSoFar + costSoFar + _lbRemainder;
                        if (lb < _bestStaticCost * (1.0 - _config.RelativeGap))
                            Dfs(depth + 1, costSoFar, unassigned - 1); // costSoFar beze změny
                    }

                    Unassign(depth, slotIdx, refIdx);

                    if (_timeLimitExceeded) return;
                }
            }

            // ── Skip větev: slot zůstane nepřiřazený, platíme UnassignedCost ─────
            // costSoFar kumuluje skip penalizace; _actualCostSoFar sleduje skutečné tras. náklady.
            {
                double skipCost = costSoFar + _config.UnassignedCost;
                double lbAfterSkip = _actualCostSoFar + skipCost + (_lbRemainder - _minCost[slotIdx]);
                if (lbAfterSkip < _bestStaticCost * (1.0 - _config.RelativeGap))
                {
                    double savedMinCost = _minCost[slotIdx];
                    _unassigned[slotIdx] = false;
                    _lbRemainder -= savedMinCost;

                    Dfs(depth + 1, skipCost, unassigned - 1); // skipCost nese penalizaci

                    _unassigned[slotIdx] = true;
                    _lbRemainder += savedMinCost;
                }
            }
        }

        // ─── MRV výběr slotu (O(S), žádné extern. volání) ─────────────────────────

        private int SelectMrvSlot()
        {
            int best = -1;
            int bestCnt = int.MaxValue;
            int bestRank = -1;

            for (int i = 0; i < _S; i++)
            {
                if (!_unassigned[i]) continue;

                int cnt = _eligCnt[i];
                int rank = _slots[i].RequiredRank;

                // Primární kritérium: nejmenší počet způsobilých (MRV).
                // Sekundární: nejvyšší RequiredRank (tie-breaking – nejtěžší obsadit nejdřív).
                if (cnt < bestCnt || (cnt == bestCnt && rank > bestRank))
                {
                    bestCnt = cnt;
                    bestRank = rank;
                    best = i;
                }
            }

            return best; // -1 pokud žádný nepřiřazený slot (nekane se stát pokud unassigned > 0)
        }

        // ─── Assign: přiřazení + inkrementální update ────────────────────────────

        /// <summary>
        /// Přiřadí rozhodčího <paramref name="refIdx"/> ke slotu <paramref name="slotIdx"/> a
        /// inkrementálně aktualizuje _eligible, _eligCnt, _minCost a _lbRemainder.
        /// Výsledek uloží do journalu pro pozdější backtrack.
        /// </summary>
        /// <param name="forwardOk">
        ///   false pokud po přiřazení existuje nepřiřazený slot bez způsobilého rozhodčího
        ///   (forward checking detekuje mrtvou větev).
        /// </param>
        private void Assign(int depth, int slotIdx, int refIdx, out bool forwardOk)
        {
            _assigned[slotIdx] = refIdx;
            _unassigned[slotIdx] = false;

            // Uložíme původní minCost přiřazeného slotu (pro backtrack)
            _journalOldMinAssigned[depth] = _minCost[slotIdx];

            // Odečteme příspěvek přiřazeného slotu z _lbRemainder
            _lbRemainder -= _minCost[slotIdx];

            int journalLen = 0;
            forwardOk = true;

            // Pro každý slot překrývající se s přiřazeným slotem:
            // pokud je refIdx v jeho eligible množině, odstraníme ho
            // a aktualizujeme _minCost a _lbRemainder.
            foreach (int s2 in _overlaps[slotIdx])
            {
                // Přeskočíme již přiřazené sloty (jejich minCost nepřispívá do _lbRemainder)
                if (!_unassigned[s2]) continue;

                int idx = s2 * _R + refIdx;
                if (!_eligible[idx]) continue; // refIdx nebyl eligible pro s2 → nic se nemění

                // Odstranění refIdx z eligible množiny slotu s2
                _eligible[idx] = false;
                _eligCnt[s2]--;

                // Uložíme do journalu (pro backtrack)
                _journalSlots[depth][journalLen] = s2;
                _journalOldMin[depth][journalLen] = _minCost[s2];
                journalLen++;

                // Aktualizace _lbRemainder: odečteme starý příspěvek, přidáme nový
                _lbRemainder -= _minCost[s2];

                double newMin;
                if (_eligCnt[s2] == 0)
                {
                    // Forward checking: žádný způsobilý rozhodčí → mrtvá větev
                    forwardOk = false;
                    newMin = 0.0; // hodnota není důležitá, větev se prořeže
                }
                else if (_cost[s2 * _R + refIdx] <= _minCost[s2] + 1e-9)
                {
                    // Odstraněný rozhodčí byl nejlevnější (nebo sdílel minimum) →
                    // minimum se mohlo zvýšit, přepočítáme.
                    newMin = ComputeSlotMinCost(s2);
                }
                else
                {
                    // Odstraněný rozhodčí byl dražší než minimum → minimum se nezměnilo.
                    newMin = _minCost[s2];
                }

                _minCost[s2] = newMin;
                _lbRemainder += newMin;
            }

            _journalLen[depth] = journalLen;

            // ── Inkrementální skutečná cena ──────────────────────────────────────────
            // Uložíme starý stav do journalu, pak přidáme slot do per-ref seznamu
            // a přepočítáme skutečnou cenu pouze pro tohoto rozhodčího.
            _journalAffectedRef[depth] = refIdx;
            _journalOldRefCost[depth] = _refActualCost[refIdx];
            _journalOldActualCost[depth] = _actualCostSoFar;

            // Seřazený insert podle Start času
            int insertPos = 0;
            while (insertPos < _refSlots[refIdx].Count &&
                   _slots[_refSlots[refIdx][insertPos]].Start <= _slots[slotIdx].Start)
                insertPos++;
            _refSlots[refIdx].Insert(insertPos, slotIdx);

            // Přepočet skutečné ceny rozhodčího (pouze jeho sloty)
            double newRefCost = ComputeRefereeActualCost(refIdx);
            _actualCostSoFar += newRefCost - _refActualCost[refIdx];
            _refActualCost[refIdx] = newRefCost;
        }

        // ─── Unassign: backtrack – obnova stavu ───────────────────────────────────

        /// <summary>
        /// Odvolá přiřazení provedené metodou <see cref="Assign"/> a obnoví
        /// _eligible, _eligCnt, _minCost a _lbRemainder do stavu před přiřazením.
        /// Díky předaklokovanému journalu bez heap alokací.
        /// </summary>
        private void Unassign(int depth, int slotIdx, int refIdx)
        {
            _assigned[slotIdx] = -1;
            _unassigned[slotIdx] = true;

            // Obnovíme minCost a _lbRemainder pro přiřazený slot
            double restoredMin = _journalOldMinAssigned[depth];
            _lbRemainder += restoredMin;
            _minCost[slotIdx] = restoredMin;

            // Obnovíme každý slot zaznamenaný v journalu
            int len = _journalLen[depth];
            for (int k = 0; k < len; k++)
            {
                int s2 = _journalSlots[depth][k];
                double oldMin = _journalOldMin[depth][k];

                // Odečteme aktuální příspěvek (vzniklý v Assign) a přidáme původní
                _lbRemainder -= _minCost[s2];
                _eligible[s2 * _R + refIdx] = true;
                _eligCnt[s2]++;
                _minCost[s2] = oldMin;
                _lbRemainder += oldMin;
            }

            // ── Backtrack skutečné ceny ───────────────────────────────────────────
            _refSlots[refIdx].Remove(slotIdx); // odstraníme slot ze seřazeného seznamu
            _refActualCost[refIdx] = _journalOldRefCost[depth];
            _actualCostSoFar = _journalOldActualCost[depth];
        }

        // ─── Pomocné metody ───────────────────────────────────────────────────────

        /// <summary>
        /// Vypočítá minimální statickou cenu přiřazení pro slot <paramref name="slotIdx"/>
        /// přes všechny aktuálně eligible rozhodčí.
        /// Volá se pouze tehdy, gdy _eligCnt[slotIdx] > 0.
        /// </summary>
        private double ComputeSlotMinCost(int slotIdx)
        {
            double min = double.MaxValue;
            int slotBase = slotIdx * _R;

            // Iterujeme pouze rank-eligible (podmnožina všech R) → typicky mnohem méně položek
            foreach (int j in _rankOk[slotIdx])
            {
                if (_eligible[slotBase + j])
                {
                    double c = _cost[slotBase + j];
                    if (c < min) min = c;
                }
            }

            return min < double.MaxValue ? min : 0.0;
        }

        /// <summary>
        /// Insertion sort kandidátů podle ceny (slotBase je slotIdx*R).
        /// Pro typicky malé pole (cCount ≤ 30 rozhodčích) je insertion sort
        /// rychlejší než Array.Sort kvůli absenci overhead a přátelskosti k cache.
        /// </summary>
        private void InsertionSortByCost(int[] buf, int count, int slotBase)
        {
            for (int i = 1; i < count; i++)
            {
                int key = buf[i];
                double keyCost = _cost[slotBase + key];
                int j = i - 1;
                while (j >= 0 && _cost[slotBase + buf[j]] > keyCost)
                {
                    buf[j + 1] = buf[j];
                    j--;
                }
                buf[j + 1] = key;
            }
        }

        /// <summary>
        /// Vypočítá skutečnou cenu rozhodčího <paramref name="refIdx"/> pro jeho aktuálně
        /// přiřazené sloty (_refSlots[refIdx]). Sestaví dočasný State jen pro tohoto rozhodčího
        /// a zavolá TotalCost – díky tomu výpočet zahrnuje sdílení tras mezi sloty.
        /// Volá se v každém uzlu DFS (při Assign), ale jen pro jednoho rozhodčího → O(k) kde
        /// k = počet jeho slotů, typicky malé číslo.
        /// </summary>
        private double ComputeRefereeActualCost(int refIdx)
        {
            var slots = _refSlots[refIdx];
            if (slots.Count == 0) return 0.0;

            var tempState = new State();
            foreach (int si in slots)
            {
                tempState.AddSlot(_slots[si]);
                tempState.SetReferee(_slots[si], _refs[refIdx]);
            }
            return _costCalculator.TotalCost(tempState);
        }

        /// <summary>
        /// Sestaví State objekt z aktuálního _assigned[] pole.
        /// Volá se pouze při nalezení nového optima → není v horké smyčce.
        /// </summary>
        private State BuildState()
        {
            var state = new State();
            for (int i = 0; i < _S; i++)
                state.AddSlot(_slots[i]);
            for (int i = 0; i < _S; i++)
                if (_assigned[i] >= 0)
                    state.SetReferee(_slots[i], _refs[_assigned[i]]);
            return state;
        }
    }
}