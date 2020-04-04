using System.Collections.Generic;
using JetsonModels;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace JetsonService.Data
{
    /// <summary>
    /// Converts a collection of <see cref="CpuCore" /> to a <see cref="string" />.
    /// </summary>
    public class CpuCoreToStringConverter : ValueConverter<ICollection<CpuCore>, string>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CpuCoreToStringConverter"/> class.
        /// </summary>
        /// <param name="mappingHints">
        ///  Hints that can be used by the <see cref="ITypeMappingSource" /> to create data types with appropriate
        ///  facets for the converted data.
        /// </param>
        public CpuCoreToStringConverter(ConverterMappingHints mappingHints = null)
            : base(
                v => ConvertToCSVString(v),
                v => CSVStringToCpuCores(v),
                mappingHints)
        {
        }

        private static string ConvertToCSVString(ICollection<CpuCore> cores)
        {
            var sortedCores = new List<CpuCore>(cores);
            sortedCores.Sort((a, b) => (int)(a.CoreNumber - b.CoreNumber));

            var results = new List<string>();

            uint coreNumber = 0;
            foreach (var core in sortedCores)
            {
                if (coreNumber < core.CoreNumber)
                {
                    for (uint i = coreNumber; i < core.CoreNumber; i++)
                    {
                        results.Add(string.Empty);
                    }
                }

                results.Add(core.UtilizationPercentage.ToString());
                coreNumber = core.CoreNumber + 1;
            }

            return string.Join(",", results);
        }

        private static List<CpuCore> CSVStringToCpuCores(string data)
        {
            var result = new List<CpuCore>();

            var split = data.Split(",");
            for (uint i = 0; i < split.Length; i++)
            {
                var part = split[i];
                if (!string.IsNullOrEmpty(part))
                {
                    result.Add(new CpuCore() { CoreNumber = i, UtilizationPercentage = float.Parse(part) });
                }
            }

            return result;
        }
    }
}
