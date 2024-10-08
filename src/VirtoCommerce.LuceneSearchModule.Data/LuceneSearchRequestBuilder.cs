using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.QueryParsers.Classic;
using Lucene.Net.Search;
using VirtoCommerce.SearchModule.Core.Model;

namespace VirtoCommerce.LuceneSearchModule.Data
{
    public class LuceneSearchRequestBuilder
    {
        private const Lucene.Net.Util.LuceneVersion _matchVersion = Lucene.Net.Util.LuceneVersion.LUCENE_48;
        private static readonly string[] _keywordSeparator = { " " };

        public static LuceneSearchRequest BuildRequest(SearchRequest request, string indexName, string documentType, ICollection<string> availableFields)
        {
            var query = GetQuery(request);
            var filters = GetFilters(request, availableFields);

            var result = new LuceneSearchRequest
            {
                Query = query?.ToString().Equals(string.Empty) == true ? new MatchAllDocsQuery() : query,
                Filter = filters?.ToString().Equals("BooleanFilter()") == true ? null : filters,
                Sort = GetSorting(request, availableFields),
                Count = request.Take + request.Skip,
            };

            return result;
        }

        private static Sort GetSorting(SearchRequest request, ICollection<string> availableFields)
        {
            Sort result = null;

            if (request?.Sorting?.Any() == true)
            {
                result = new Sort(request.Sorting.Select(f => GetSortField(f, availableFields)).ToArray());
            }

            return result;
        }

        private static SortField GetSortField(SortingField field, ICollection<string> availableFields)
        {
            if (field.FieldName == "score")
            {
                return new SortField(null, SortFieldType.SCORE, field.IsDescending);

            }

            var dataType = SortFieldType.STRING_VAL;
            if (availableFields.Contains(LuceneSearchHelper.GetDoubleFieldName(field.FieldName)))
            {
                dataType = SortFieldType.DOUBLE;
            }
            else if (availableFields.Contains(LuceneSearchHelper.GetIntegerFieldName(field.FieldName)))
            {
                dataType = SortFieldType.INT32;
            }
            else if (availableFields.Contains(LuceneSearchHelper.GetDateTimeFieldName(field.FieldName)))
            {
                dataType = SortFieldType.INT64;
            }

            var result = new SortField(LuceneSearchHelper.ToLuceneFieldName(field.FieldName), dataType, field.IsDescending);
            return result;
        }

        private static Query GetQuery(SearchRequest request)
        {
            Query result = null;

            if (!string.IsNullOrEmpty(request?.SearchKeywords))
            {
                var searchKeywords = QueryParserBase.Escape(request.SearchKeywords);

                if (request.IsFuzzySearch)
                {
                    const string fuzzyMinSimilarity = "0.7";
                    searchKeywords = $"\"{searchKeywords}\"~{fuzzyMinSimilarity}";
                }

                var fields = request.SearchFields?.Select(LuceneSearchHelper.ToLuceneFieldName).ToArray() ?? LuceneSearchHelper.SearchableFields;
                var analyzer = new StandardAnalyzer(_matchVersion);

                var parser = new MultiFieldQueryParser(_matchVersion, fields, analyzer)
                {
                    DefaultOperator = QueryParserBase.AND_OPERATOR,
                    AllowLeadingWildcard = true
                };

                result = parser.Parse(searchKeywords);
            }

            return result;
        }

        private static Filter GetFilters(SearchRequest request, ICollection<string> availableFields)
        {
            return LuceneSearchFilterBuilder.GetFilterRecursive(request.Filter, availableFields);
        }
    }
}
