using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lucene.Net.Index;
using Newtonsoft.Json;

namespace VirtoCommerce.LuceneSearchModule.Data
{
    public static class LuceneSearchHelper
    {
        public const string KeyFieldName = "__id";
        public const string SearchableFieldName = "__all";
        public const string BooleanFieldSuffix = ".boolean";
        public const string DateTimeFieldSuffix = ".datetime";
        public const string DoubleFieldSuffix = ".double";
        public const string IntegerFieldSuffix = ".integer";
        public const string ComplexFieldSuffix = ".complex";

        public static readonly string[] SearchableFields = { SearchableFieldName };

        public static string ToLuceneFieldName(string originalName)
        {
            return originalName?.ToLowerInvariant();
        }

        public static string GetBooleanFieldName(string originalName)
        {
            return ToLuceneFieldName(originalName + BooleanFieldSuffix);
        }

        public static string GetDateTimeFieldName(string originalName)
        {
            return ToLuceneFieldName(originalName + DateTimeFieldSuffix);
        }

        public static string GetDoubleFieldName(string originalName)
        {
            return ToLuceneFieldName(originalName + DoubleFieldSuffix);
        }

        public static string GetIntegerFieldName(string originalName)
        {
            return ToLuceneFieldName(originalName + IntegerFieldSuffix);
        }

        public static string GetComplexFieldName(string originalName)
        {
            return ToLuceneFieldName(originalName + ComplexFieldSuffix);
        }

        public static string ToStringInvariant(this object value)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}", value);
        }

        public static IEnumerable<string> GetAllFieldValues(this IndexReader reader, string field)
        {
            if (string.IsNullOrEmpty(field))
            {
                yield break;
            }

            var atomicReaders = reader.Leaves.Select(c => c.AtomicReader);

            foreach (var atomicReader in atomicReaders)
            {
                var terms = atomicReader.GetTerms(field);

                if (terms != null)
                {
                    foreach (var termsEnum in terms)
                    {
                        yield return Term.ToString(termsEnum.Term);
                    }
                }
            }
        }

        public static string[] GetAllFacetableFields(this IndexReader reader)
        {
            var availableFields = reader.Leaves
                                .SelectMany(r => r.AtomicReader.Fields)
                                .Distinct()
                                .ToArray();

            return availableFields;
        }

        public static string SerializeJson(this object source, JsonSerializerSettings settings = null)
        {
            settings ??= new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Include,
                NullValueHandling = NullValueHandling.Ignore,
                Formatting = Formatting.None,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
                TypeNameHandling = TypeNameHandling.None,
            };

            return JsonConvert.SerializeObject(source, settings);
        }
    }
}
