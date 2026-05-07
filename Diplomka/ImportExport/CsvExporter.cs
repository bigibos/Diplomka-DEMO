using CsvHelper;
using CsvHelper.Configuration;
using Diplomka.ImportExport.Mapping;
using Diplomka.Entity;
using Diplomka.Solver;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport
{
    /// <summary>
    /// Nástroj pro exportování dat do CSV souboru
    /// </summary>
    public static class CsvExporter
    {
        // Konfigurace formatu CSV
        static readonly CsvConfiguration _csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ";"
        };

        /// <summary>
        /// Metoda pro uložení slotů do CSV souboru
        /// </summary>
        /// <param name="path">Cesta k souboru, který se má vytvořit</param>
        /// <param name="slots">Seznam slotů, které se mají převést a uložit</param>
        public static void SaveSlots(string path, List<Slot> slots)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
            using var csv = new CsvWriter(writer, _csvConfig);

            csv.Context.RegisterClassMap<SlotMap>();

            var dtos = slots.Select(CsvMapper.ToDto);

            csv.WriteRecords(dtos);
        }

        /// <summary>
        /// Metoda pro uložení rozhodčích do CSV souboru
        /// </summary>
        /// <param name="path">Cesta k souboru, který se má vytvořit</param>
        /// <param name="referees">Seznam rozhodčích, kteří se mají převést a uložit</param>
        public static void SaveReferees(string path, List<Referee> referees)
        {
            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
            using var csv = new CsvWriter(writer, _csvConfig);

            csv.Context.RegisterClassMap<RefereeMap>();

            var dtos = referees.Select(CsvMapper.ToDto);

            csv.WriteRecords(dtos);
        }

        /// <summary>
        /// Metoda pro uložení stavu do CSV souboru
        /// </summary>
        /// <param name="path">Cesta k souboru, který se má vytvořit</param>
        /// <param name="state">Stav, který se má převést a uložit</param>
        /// <param name="routeSolver">Kalkulátor, který k ukládyným přiřazenám ve stavu vypočítá a uloží záznamy o trasách</param>
        public static void SaveState(string path, State state, RouteSolver routeSolver)
        {
            var dtos = StateCsvMapper.ToDtoList(state, routeSolver);
            dtos = dtos.OrderBy(s => s.From).ToList();

            using var writer = new StreamWriter(path, false, new UTF8Encoding(true));

            using var csv = new CsvWriter(writer, _csvConfig);

            csv.WriteRecords(dtos);
        }
    }
}
