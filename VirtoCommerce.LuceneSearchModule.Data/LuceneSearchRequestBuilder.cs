using System;
using System.Collections.Generic;
using System.Linq;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.QueryParsers;
using Lucene.Net.Search;
using VirtoCommerce.Domain.Search;
using Version = Lucene.Net.Util.Version;

namespace VirtoCommerce.LuceneSearchModule.Data
{
    public class LuceneSearchRequestBuilder
    {
        private const Version _matchVersion = Version.LUCENE_30;
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

            if (request?.Sorting != null)
            {
                result = new Sort(request.Sorting.Select(f => GetSortField(f, availableFields)).ToArray());
            }

            return result;
        }

        private static SortField GetSortField(SortingField field, ICollection<string> availableFields)
        {
            var dataType = availableFields.Contains(LuceneSearchHelper.GetFacetableFieldName(field.FieldName)) ? SortField.DOUBLE : SortField.STRING;
            var result = new SortField(LuceneSearchHelper.ToLuceneFieldName(field.FieldName), dataType, field.IsDescending);
            return result;
        }

        private static Query GetQuery(SearchRequest request)
        {
            Query result = null;

            if (!string.IsNullOrEmpty(request?.SearchKeywords))
            {
                var searchKeywords = request.SearchKeywords;

                if (request.IsFuzzySearch)
                {
                    const string fuzzyMinSimilarity = "0.7";
                    var keywords = searchKeywords.Replace("~", string.Empty).Split(_keywordSeparator, StringSplitOptions.RemoveEmptyEntries);

                    searchKeywords = string.Empty;
                    searchKeywords = keywords.Aggregate(searchKeywords, (current, keyword) => current + $"{keyword}~{fuzzyMinSimilarity}");
                }

                var fields = request.SearchFields?.Select(LuceneSearchHelper.ToLuceneFieldName).ToArray() ?? LuceneSearchHelper.SearchableFields;
                var analyzer = new StandardAnalyzer(_matchVersion);

                var parser = new MultiFieldQueryParser(_matchVersion, fields, analyzer)
                {
                    DefaultOperator = QueryParser.Operator.AND
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
