using Diplomka.Entity;

namespace Diplomka.Solver
{
    /// <summary>
    /// Předpočítaný index pro rychlé operace v B&amp;B solveru.
    ///
    /// ── Proč tato třída existuje ──────────────────────────────────────────────────
    ///
    ///  Bez indexu má každý DFS uzel tyto náklady:
    ///
    ///    SelectSlotMRV:      O(S × R × k)   S=sloty, R=rozhodčí, k=prům. sloty/rozhodčí
    ///    LowerBoundForSlots: O(S × R × k)   totéž pro zbývající sloty
    ///    CanAssign (vnitřek): O(k)           s dict-lookupem do DistanceTable
    ///
    ///  Pro S=500, R=200, k=5 → ~500 000 operací NA UZEL. To je bottleneck.
    ///
    ///  S tímto indexem:
    ///
    ///    SelectSlotMRV:      O(S)            čtení z pole eligibleCounts[]
    ///    LowerBoundForSlots: O(S)            čtení z pole _slotMinCost[]
    ///    CanAssign (vnitřek): O(k)           přístup do bool[][] místo dict-lookupů
    ///    Update při assign:  O(|konflikty|)  většinou ~20-50 slotů
    ///
    /// ── Jak se používá ────────────────────────────────────────────────────────────
    ///
    ///  1. Vytvoř instanci a zavolej Build().
    ///  2. Předej BBSolveru. Solver si na začátku Dfs() vytvoří eligibleCounts[]
    ///     pomocí InitEligibleCounts() a udržuje je inkrementálně.
    ///  3. MRV = jen najdi min v eligibleCounts[] pro prázdné sloty → O(S).
    ///  4. CanAssign → používá ConflictMatrix[sI][sJ] místo Overlaps().
    ///  5. LowerBound → sum(_slotMinCost[i]) pro prázdné sloty → O(S).
    ///
    /// ── Build složitost ───────────────────────────────────────────────────────────
    ///   O(S² + S·R·log R)   jednou na začátku, pak jen rychlé čtení
    /// </summary>
    public class BbSearchIndex
    {
        // ── Veřejně přístupná metadata ────────────────────────────────────────────
        public int SlotCount { get; private set; }
        public int RefCount { get; private set; }

        // ── Mapování objekt → index (pro O(1) překlad) ───────────────────────────
        private Dictionary<Slot, int> _slotToIdx = new();
        private Dictionary<Referee, int> _refToIdx = new();

        // Index → objekt (pro výstup)
        private Slot[] _slots = Array.Empty<Slot>();
        private Referee[] _referees = Array.Empty<Referee>();

        // ── Hlavní datové struktury ────────────────────────────────────────────────

        /// <summary>
        /// ConflictMatrix[i][j] == true ⟺ sloty i a j si časově kolidují (vč. cestovního času).
        /// Symetrická matice. Přístup O(1).
        /// </summary>
        private bool[][] _conflictMatrix = Array.Empty<bool[]>();

        /// <summary>
        /// ConflictLists[i] = seznam indexů slotů, které kolidují se slotem i.
        /// Používá se při inkrementální aktualizaci eligibleCounts[].
        /// </summary>
        private int[][] _conflictLists = Array.Empty<int[]>();

        /// <summary>
        /// SortedCandidates[i] = seřazený seznam (refIdx, cena) pro slot i.
        /// Zahrnuje POUZE rozhodčí způsobilé podle ranku, seřazeno ASC podle ceny.
        /// NEŘEŠÍ časové kolize – ty se řeší dynamicky v DFS.
        /// </summary>
        private (int RefIdx, double Cost)[][] _sortedCandidates = Array.Empty<(int, double)[]>();

        /// <summary>
        /// RankEligible[i][j] == true ⟺ rozhodčí j je způsobilý pro slot i dle ranku.
        /// 500×200 = 100 000 bool, zanedbatelná paměť.
        /// Slouží pro O(1) lookup při inkrementální aktualizaci eligibleCounts.
        /// </summary>
        private bool[][] _rankEligible = Array.Empty<bool[]>();

        /// <summary>
        /// SlotMinCost[i] = minimální cena přiřazení rozhodčího ke slotu i
        /// přes VŠECHNY rankově způsobilé rozhodčí, BEZ ohledu na časové kolize.
        /// Slouží jako admissible lower bound pro každý slot.
        /// </summary>
        private double[] _slotMinCost = Array.Empty<double>();

        // ── Závislosti ────────────────────────────────────────────────────────────
        private readonly ConflictChecker _conflictChecker;
        private readonly CostCalculator _costCalculator;

        public BbSearchIndex(ConflictChecker conflictChecker, CostCalculator costCalculator)
        {
            _conflictChecker = conflictChecker;
            _costCalculator = costCalculator;
        }

        // ═════════════════════════════════════════════════════════════════════════
        // BUILD
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Sestaví celý index. Volej jednou před spuštěním solveru.
        /// Složitost: O(S² + S·R·log R).
        /// </summary>
        public void Build(IEnumerable<Slot> slots, IEnumerable<Referee> referees)
        {
            _slots = slots.ToArray();
            _referees = referees.ToArray();
            SlotCount = _slots.Length;
            RefCount = _referees.Length;

            // Přímé mapování objekt → index
            _slotToIdx = new Dictionary<Slot, int>(SlotCount);
            _refToIdx = new Dictionary<Referee, int>(RefCount);
            for (int i = 0; i < SlotCount; i++) _slotToIdx[_slots[i]] = i;
            for (int j = 0; j < RefCount; j++) _refToIdx[_referees[j]] = j;

            Console.WriteLine($"[Index] Sestavuji pro {SlotCount} slotů × {RefCount} rozhodčích...");

            BuildConflictMatrix();
            BuildCandidates();

            Console.WriteLine($"[Index] Hotovo.");
        }

        // ── Konfliktní matice ─────────────────────────────────────────────────────

        private void BuildConflictMatrix()
        {
            // Alokujeme jagged array (šetří cache při přístupu po řádcích)
            _conflictMatrix = new bool[SlotCount][];
            for (int i = 0; i < SlotCount; i++)
                _conflictMatrix[i] = new bool[SlotCount];

            var conflictListsTemp = new List<int>[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                conflictListsTemp[i] = new List<int>();

            // Iterujeme jen horní trojúhelník, výsledek zkopírujeme symetricky
            long totalConflicts = 0;
            for (int i = 0; i < SlotCount; i++)
            {
                for (int j = i + 1; j < SlotCount; j++)
                {
                    if (_conflictChecker.Overlaps(_slots[i], _slots[j]))
                    {
                        _conflictMatrix[i][j] = true;
                        _conflictMatrix[j][i] = true;
                        conflictListsTemp[i].Add(j);
                        conflictListsTemp[j].Add(i);
                        totalConflicts++;
                    }
                }
            }

            // Převedeme List na pole pro rychlejší iteraci
            _conflictLists = new int[SlotCount][];
            for (int i = 0; i < SlotCount; i++)
                _conflictLists[i] = conflictListsTemp[i].ToArray();

            double avgConflicts = (double)totalConflicts * 2 / SlotCount;
            Console.WriteLine($"[Index] Konfliktní matice: {totalConflicts * 2:N0} párů ({avgConflicts:F1} prům. konfliktů/slot)");
        }

        // ── Kandidáti a minima ────────────────────────────────────────────────────

        private void BuildCandidates()
        {
            _rankEligible = new bool[SlotCount][];
            _sortedCandidates = new (int, double)[SlotCount][];
            _slotMinCost = new double[SlotCount];

            for (int i = 0; i < SlotCount; i++)
            {
                _rankEligible[i] = new bool[RefCount];
                var slot = _slots[i];

                // Seřaď rankově způsobilé rozhodčí podle ceny
                var eligible = new List<(int RefIdx, double Cost)>(RefCount);
                for (int j = 0; j < RefCount; j++)
                {
                    var ref_ = _referees[j];
                    // Rank check – stejná logika jako ConflictChecker.CanAssign
                    if (ref_.Rank >= slot.RequiredRank)
                    {
                        double cost = _costCalculator.AssignmentCost(slot, ref_);
                        eligible.Add((j, cost));
                        _rankEligible[i][j] = true;
                    }
                }

                eligible.Sort((a, b) => a.Cost.CompareTo(b.Cost));
                _sortedCandidates[i] = eligible.ToArray();
                _slotMinCost[i] = eligible.Count > 0
                    ? eligible[0].Cost
                    : double.MaxValue;  // slot bez způsobilého rozhodčího – vždy infeasible
            }

            int avgCandidates = _sortedCandidates.Sum(c => c.Length) / SlotCount;
            Console.WriteLine($"[Index] Kandidáti: prům. {avgCandidates} rankově způsobilých na slot");
        }

        // ═════════════════════════════════════════════════════════════════════════
        // RYCHLÉ DOTAZY (volají se z každého DFS uzlu)
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Zjistí, zda si sloty i a j kolidují. O(1).
        /// Nahrazuje ConflictChecker.Overlaps() v CanAssignFast.
        /// </summary>
        public bool Conflicts(int slotIdxA, int slotIdxB)
            => _conflictMatrix[slotIdxA][slotIdxB];

        /// <summary>
        /// Seznam indexů slotů kolidujících se slotem slotIdx.
        /// Používá se při inkrementální aktualizaci eligibleCounts[].
        /// </summary>
        public ReadOnlySpan<int> GetConflictingSlotIndices(int slotIdx)
            => _conflictLists[slotIdx];

        /// <summary>
        /// Vrátí seřazené kandidáty pro slot (jen rank-eligible, bez časové kontroly).
        /// Caller iteruje a sám volá CanAssignFast.
        /// </summary>
        public ReadOnlySpan<(int RefIdx, double Cost)> GetSortedCandidates(int slotIdx)
            => _sortedCandidates[slotIdx];

        /// <summary>
        /// Vrátí true, pokud je rozhodčí j rankově způsobilý pro slot i. O(1).
        /// </summary>
        public bool IsRankEligible(int slotIdx, int refIdx)
            => _rankEligible[slotIdx][refIdx];

        /// <summary>
        /// Admissible lower bound pro slot i (ignoruje časové kolize).
        /// Vždy ≤ skutečné optimální ceně → lze bezpečně použít v B&amp;B.
        /// </summary>
        public double GetSlotMinCost(int slotIdx)
            => _slotMinCost[slotIdx];

        /// <summary>
        /// Vrátí index slotu v interním poli. O(1).
        /// </summary>
        public int SlotIndex(Slot slot) => _slotToIdx[slot];

        /// <summary>
        /// Vrátí index rozhodčího v interním poli. O(1).
        /// </summary>
        public int RefIndex(Referee referee) => _refToIdx[referee];

        /// <summary>
        /// Vrátí Slot podle interního indexu.
        /// </summary>
        public Slot GetSlot(int idx) => _slots[idx];

        /// <summary>
        /// Vrátí Referee podle interního indexu.
        /// </summary>
        public Referee GetReferee(int idx) => _referees[idx];

        // ═════════════════════════════════════════════════════════════════════════
        // INKREMENTÁLNÍ MRV POČÍTADLA
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Inicializuje pole eligibleCounts[] pro DFS.
        /// eligibleCounts[i] = počet rankově způsobilých rozhodčích pro slot i
        /// (bez ohledu na časové kolize – ty se odečítají inkrementálně).
        ///
        /// Volej jednou před spuštěním Dfs().
        /// </summary>
        public int[] InitEligibleCounts()
        {
            var counts = new int[SlotCount];
            for (int i = 0; i < SlotCount; i++)
                counts[i] = _sortedCandidates[i].Length;
            return counts;
        }

        /// <summary>
        /// Aktualizuje eligibleCounts[] po přiřazení rozhodčího refIdx ke slotu assignedSlotIdx.
        ///
        /// Pro každý slot T, který koliduje s assignedSlotIdx:
        ///   pokud je refIdx rankově způsobilý pro T → eligibleCounts[T]--.
        ///
        /// Složitost: O(|konflikty assignedSlotIdx|), typicky ~20-50.
        /// </summary>
        public void OnAssign(int assignedSlotIdx, int refIdx, int[] eligibleCounts)
        {
            foreach (int conflictingSlotIdx in _conflictLists[assignedSlotIdx])
            {
                if (_rankEligible[conflictingSlotIdx][refIdx])
                    eligibleCounts[conflictingSlotIdx]--;
            }
        }

        /// <summary>
        /// Zpětná aktualizace eligibleCounts[] po odebrání rozhodčího (backtrack).
        /// Symetrická k OnAssign.
        /// Složitost: O(|konflikty assignedSlotIdx|).
        /// </summary>
        public void OnUnassign(int assignedSlotIdx, int refIdx, int[] eligibleCounts)
        {
            foreach (int conflictingSlotIdx in _conflictLists[assignedSlotIdx])
            {
                if (_rankEligible[conflictingSlotIdx][refIdx])
                    eligibleCounts[conflictingSlotIdx]++;
            }
        }

        // ═════════════════════════════════════════════════════════════════════════
        // RYCHLÁ VERZE METOD PRO DFS
        // ═════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Rychlá verze CanAssign – používá conflict matrix místo dict-lookupů.
        ///
        /// Složitost: O(počet slotů přiřazených rozhodčímu), ale s O(1) na pár
        /// místo původního O(1) dict-lookup + DateTime aritmetika.
        ///
        /// DŮLEŽITÉ: Rank check je vynechán – předpokládáme, že caller iteruje
        /// přes _sortedCandidates[], který jsou předfiltrovaní dle ranku.
        /// </summary>
        public bool CanAssignFast(int newSlotIdx, List<Slot> assignedSlotsOfRef)
        {
            foreach (var assignedSlot in assignedSlotsOfRef)
            {
                int assignedIdx = _slotToIdx[assignedSlot];
                if (_conflictMatrix[newSlotIdx][assignedIdx])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Rychlá admissible lower bound pro zbývající prázdné sloty.
        ///
        /// LB = Σ _slotMinCost[i] pro každý prázdný slot i.
        ///
        /// Je admissible (nikdy nepřeceňuje), protože:
        ///   - ignoruje časové kolize → každý slot dostane svého nejlevnějšího ref
        ///   - výsledek ≤ skutečné minimum (které musí respektovat kolize)
        ///
        /// Složitost: O(S) místo původního O(S × R × k).
        /// </summary>
        public double FastLowerBound(IEnumerable<Slot> emptySlots, double alreadyAssignedCost)
        {
            double lb = alreadyAssignedCost;
            foreach (var slot in emptySlots)
            {
                double minCost = _slotMinCost[_slotToIdx[slot]];
                if (minCost == double.MaxValue)
                    return double.MaxValue; // Infeasible větev – okamžitý pruning
                lb += minCost;
            }
            return lb;
        }

        /// <summary>
        /// Tighter admissible lower bound – pro každý prázdný slot najde nejlevnějšího
        /// kandidáta, který nekoliduje s AKTUÁLNĚ přiřazenými sloty.
        ///
        /// Silnější pruning než FastLowerBound, ale pomalejší: O(S × K_avg).
        /// Vhodné pro menší stromy nebo hlubší uzly.
        /// </summary>
        public double TightLowerBound(IEnumerable<Slot> emptySlots, double alreadyAssignedCost,
            State state)
        {
            double lb = alreadyAssignedCost;
            foreach (var slot in emptySlots)
            {
                int sIdx = _slotToIdx[slot];
                var candidates = _sortedCandidates[sIdx];
                double minCost = double.MaxValue;

                foreach (var (refIdx, cost) in candidates)
                {
                    // Rychlá kontrola: koliduje ref v aktuálním stavu s tímto slotem?
                    var refObj = _referees[refIdx];
                    if (CanAssignFast(sIdx, state.GetSlotsByReferee(refObj)))
                    {
                        minCost = cost; // Candidates jsou seřazeni ASC → první = nejlevnější
                        break;
                    }
                }

                if (minCost == double.MaxValue)
                    return double.MaxValue; // Infeasible větev
                lb += minCost;
            }
            return lb;
        }

        /// <summary>
        /// MRV výběr slotu z eligibleCounts[].
        /// Vrátí index slotu s nejmenším počtem dostupných rozhodčích.
        ///
        /// Složitost: O(S) – jedno čtení z pole, žádné volání CanAssign.
        /// </summary>
        public int SelectMrvSlotIndex(IEnumerable<Slot> emptySlots, int[] eligibleCounts)
        {
            int bestIdx = -1;
            int bestCount = int.MaxValue;
            int bestRank = -1;

            foreach (var slot in emptySlots)
            {
                int idx = _slotToIdx[slot];
                int count = eligibleCounts[idx];

                if (count < bestCount || (count == bestCount && slot.RequiredRank > bestRank))
                {
                    bestCount = count;
                    bestIdx = idx;
                    bestRank = slot.RequiredRank;
                }
            }

            return bestIdx;
        }
    }
}