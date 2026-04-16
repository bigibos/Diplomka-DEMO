using CsvHelper;
using CsvHelper.Configuration;
using Diplomka.ImportExport.Mapping;
using Diplomka.Model;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport
{
    public static class CsvExporter
    {
        static CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ";"
        };

        public static void SaveSlots(string path, List<Slot> slots)
        {
            using var writer = new StreamWriter(path);
            using var csv = new CsvWriter(writer, csvConfig);

            csv.Context.RegisterClassMap<SlotMap>();

            var dtos = slots.Select(CsvMapper.ToDto);

            csv.WriteRecords(dtos); // ✔ zapíše i hlavičku
        }

        public static void SaveReferees(string path, List<Referee> referees)
        {
            using var writer = new StreamWriter(path);
            using var csv = new CsvWriter(writer, csvConfig);

            csv.Context.RegisterClassMap<RefereeMap>();

            var dtos = referees.Select(CsvMapper.ToDto);

            csv.WriteRecords(dtos);
        }
    }
}
