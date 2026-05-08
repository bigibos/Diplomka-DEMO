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

        /// <summary>
        /// Exportuje šablonu CSV pro sloty s hlavičkou a dvěma vzorovými řádky.
        /// Slouží jako návod pro ruční vyplnění vstupních dat.
        /// </summary>
        /// <param name="path">Cesta k výstupnímu souboru</param>
        public static void SaveSlotsTemplate(string path)
        {
            var lines = new[]
            {
                "Id;Name;RequiredRank;StartDay;StartMonth;StartYear;StartHour;StartMinute;EndDay;EndMonth;EndYear;EndHour;EndMinute;Lat;Lon",
                "1;BK Olomoucko - ERA Basketball Nymburk;70;1;9;2025;17;0;1;9;2025;19;0;49.4719;17.1102",
                "2;BK Olomoucko - ERA Basketball Nymburk;50;1;9;2025;17;0;1;9;2025;19;0;49.4719;17.1102",
                "3;BK Olomoucko - ERA Basketball Nymburk;20;1;9;2025;17;0;1;9;2025;19;0;49.4719;17.1102",
            };
            File.WriteAllLines(path, lines, new UTF8Encoding(true));
        }

        /// <summary>
        /// Exportuje šablonu CSV pro rozhodčí s hlavičkou a vzorovými řádky.
        /// Slouží jako návod pro ruční vyplnění vstupních dat.
        /// </summary>
        /// <param name="path">Cesta k výstupnímu souboru</param>
        public static void SaveRefereesTemplate(string path)
        {
            var lines = new[]
            {
                "Id;Name;Rank;Lat;Lon;HasCar",
                "1;Novák Jan;78;50.0755;14.4378;0",
                "2;Svoboda Petr;65;49.1951;16.6068;1",
                "3;Dvořák Tomáš;25;49.8209;18.2625;1",
            };
            File.WriteAllLines(path, lines, new UTF8Encoding(true));
        }

        /// <summary>
        /// Exportuje šablonu CSV pro zákazy přiřazení (pouze hlavička).
        /// </summary>
        public static void SaveBansTemplate(string path)
        {
            File.WriteAllLines(path, new[] { "RefereeId;SlotId" }, new UTF8Encoding(true));
        }

        /// <summary>
        /// Exportuje šablonu CSV pro nesnášenlivosti (pouze hlavička).
        /// </summary>
        public static void SaveIncompatTemplate(string path)
        {
            File.WriteAllLines(path, new[] { "RefereeIdA;RefereeIdB" }, new UTF8Encoding(true));
        }
    }
}
