using CsvHelper;
using CsvHelper.Configuration;
using Diplomka.ImportExport.Dto;
using Diplomka.ImportExport.Mapping;
using Diplomka.Entity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Diplomka.ImportExport
{
    public static class CsvImporter
    {
        static CsvConfiguration csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true,
            Delimiter = ";"
        };

        public static List<Slot> LoadSlots(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, csvConfig);

            csv.Context.RegisterClassMap<SlotMap>();

            // důležité:
            csv.Read();
            csv.ReadHeader();

            var records = csv.GetRecords<SlotCsvDto>().ToList();

            return records.Select(CsvMapper.ToEntity).ToList();
        }

        public static List<Referee> LoadReferees(string path)
        {
            using var reader = new StreamReader(path);
            using var csv = new CsvReader(reader, csvConfig);

            csv.Context.RegisterClassMap<RefereeMap>();

            csv.Read();
            csv.ReadHeader();

            var records = csv.GetRecords<RefereeCsvDto>().ToList();

            return records.Select(CsvMapper.ToEntity).ToList();
        }
    }
}
