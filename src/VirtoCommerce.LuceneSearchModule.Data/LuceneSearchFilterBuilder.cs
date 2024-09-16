using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Lucene.Net.Index;
using Lucene.Net.Queries;
using Lucene.Net.Search;
using Lucene.Net.Spatial.Queries;
using Lucene.Net.Spatial.Vector;
using Lucene.Net.Util;
using Spatial4n.Context;
using Spatial4n.Distance;
using VirtoCommerce.SearchModule.Core.Model;
using TermFilter = VirtoCommerce.SearchModule.Core.Model.TermFilter;

namespace VirtoCommerce.LuceneSearchModule.Data
{
    public class LuceneSearchFilterBuilder
    {
        public static Filter GetFilterRecursive(IFilter filter, ICollection<string> availableFields)
        {
            Filter result = null;

            var idsFilter = filter as IdsFilter;
            var termFilter = filter as TermFilter;
            var rangeFilter = filter as RangeFilter;
            var geoDistanceFilter = filter as GeoDistanceFilter;
            var notFilter = filter as NotFilter;
            var andFilter = filter as AndFilter;
            var orFilter = filter as OrFilter;
            var wildcardTermFilter = filter as WildCardTermFilter;

            if (idsFilter != null)
            {
                result = CreateIdsFilter(idsFilter);
            }
            else if (termFilter != null)
            {
                result = CreateTermFilter(termFilter, availableFields);
            }
            else if (rangeFilter != null)
            {
                result = CreateRangeFilter(rangeFilter, availableFields);
            }
            else if (geoDistanceFilter != null)
            {
                result = CreateGeoDistanceFilter(geoDistanceFilter);
            }
            else if (notFilter != null)
            {
                result = CreateNotFilter(notFilter, availableFields);
            }
            else if (andFilter != null)
            {
                result = CreateAndFilter(andFilter, availableFields);
            }
            else if (orFilter != null)
            {
                result = CreateOrFilter(orFilter, availableFields);
            }
            else if (wildcardTermFilter != null)
            {
                result = CreateWildcardTermFilter(wildcardTermFilter);
            }
            return result;
        }

        private static Filter CreateIdsFilter(IdsFilter idsFilter)
        {
            Filter result = null;

            if (idsFilter?.Values != null)
            {
                result = CreateTermsFilter(LuceneSearchHelper.KeyFieldName, idsFilter.Values);
            }

            return result;
        }

        private static Filter CreateTermFilter(TermFilter termFilter, ICollection<string> availableFields)
        {
            Filter result = null;

            if (termFilter?.FieldName != null && termFilter.Values != null)
            {
                var fieldName = LuceneSearchHelper.ToLuceneFieldName(termFilter.FieldName);

                var values = termFilter.Values.Select(v => GetFilterValue(fieldName, v, availableFields)).ToArray();
                result = CreateTermsFilter(fieldName, values);
            }

            return result;
        }

        private static Filter CreateRangeFilter(RangeFilter rangeFilter, ICollection<string> availableFields)
        {
            Filter result = null;

            if (rangeFilter?.FieldName != null && rangeFilter.Values != null)
            {
                var fieldName = LuceneSearchHelper.ToLuceneFieldName(rangeFilter.FieldName);

                var childFilters = rangeFilter.Values.Select(v => CreateRangeFilterForValue(fieldName, v, availableFields))
                    .Where(f => f != null)
                    .ToArray();

                result = JoinNonEmptyFilters(childFilters, Occur.SHOULD);
            }

            return result;
        }

        private static Filter CreateGeoDistanceFilter(GeoDistanceFilter geoDistanceFilter)
        {
            Filter result = null;

            if (geoDistanceFilter?.FieldName != null && geoDistanceFilter.Location != null)
            {
                var spatialContext = SpatialContext.Geo;
                var distance = DistanceUtils.Dist2Degrees(geoDistanceFilter.Distance, DistanceUtils.EarthMeanRadiusKilometers);
                var searchArea = spatialContext.MakeCircle(geoDistanceFilter.Location.Longitude, geoDistanceFilter.Location.Latitude, distance);
                var spatialArgs = new SpatialArgs(SpatialOperation.Intersects, searchArea);

                var fieldName = LuceneSearchHelper.ToLuceneFieldName(geoDistanceFilter.FieldName);
                var strategy = new PointVectorStrategy(spatialContext, fieldName);
                result = strategy.MakeFilter(spatialArgs);
            }

            return result;
        }

        private static Filter CreateNotFilter(NotFilter notFilter, ICollection<string> availableFields)
        {
            Filter result = null;

            var childFilter = GetFilterRecursive(notFilter.ChildFilter, availableFields);
            if (childFilter != null)
            {
                var booleanFilter = new BooleanFilter();
                booleanFilter.Add(new FilterClause(childFilter, Occur.MUST_NOT));
                result = booleanFilter;
            }

            return result;
        }

        private static Filter CreateAndFilter(AndFilter andFilter, ICollection<string> availableFields)
        {
            Filter result = null;

            if (andFilter?.ChildFilters != null)
            {
                var childFilters = andFilter.ChildFilters.Select(filter => GetFilterRecursive(filter, availableFields));
                result = JoinNonEmptyFilters(childFilters, Occur.MUST);
            }

            return result;
        }

        private static Filter CreateOrFilter(OrFilter orFilter, ICollection<string> availableFields)
        {
            Filter result = null;

            if (orFilter?.ChildFilters != null)
            {
                var childFilters = orFilter.ChildFilters.Select(filter => GetFilterRecursive(filter, availableFields));
                result = JoinNonEmptyFilters(childFilters, Occur.SHOULD);
            }

            return result;
        }

        private static Filter CreateWildcardTermFilter(WildCardTermFilter wildcardTermFilter)
        {
            QueryWrapperFilter result = null;

            if (wildcardTermFilter?.FieldName != null && wildcardTermFilter.Value != null)
            {
                var fieldName = LuceneSearchHelper.ToLuceneFieldName(wildcardTermFilter.FieldName);
                var term = new Term(fieldName, wildcardTermFilter.Value);

                var wildcardQuery = new WildcardQuery(term);
                result = new QueryWrapperFilter(wildcardQuery);
            }

            return result;
        }

        public static Filter JoinNonEmptyFilters(IEnumerable<Filter> filters, Occur occur)
        {
            Filter result = null;

            if (filters != null)
            {
                var childFilters = filters.Where(f => f != null).ToArray();

                if (childFilters.Length > 1)
                {
                    var booleanFilter = new BooleanFilter();

                    foreach (var filter in childFilters)
                    {
                        booleanFilter.Add(new FilterClause(filter, occur));
                    }

                    result = booleanFilter;
                }
                else if (childFilters.Length > 0)
                {
                    result = childFilters.First();
                }
            }

            return result;
        }

        private static Filter CreateRangeFilterForValue(string fieldName, RangeFilterValue value, ICollection<string> availableFields)
        {
            return CreateRangeFilterForValue(fieldName, value.Lower, value.Upper, value.IncludeLower, value.IncludeUpper, availableFields);
        }

        public static Filter CreateRangeFilterForValue(string fieldName, string lower, string upper, bool includeLower, bool includeUpper, ICollection<string> availableFields)
        {
            // If both bounds are empty, ignore this range
            if (string.IsNullOrEmpty(lower) && string.IsNullOrEmpty(upper))
            {
                return null;
            }

            if (availableFields.Contains(LuceneSearchHelper.GetDateTimeFieldName(fieldName)))
            {
                var lowerLong = ConvertToDateTimeTicks(lower);
                var upperLong = ConvertToDateTimeTicks(upper);
                if (lowerLong != null || upperLong != null)
                {
                    return NumericRangeFilter.NewInt64Range(fieldName, lowerLong, upperLong, includeLower, includeUpper);
                }
            }

            if (availableFields.Contains(LuceneSearchHelper.GetDoubleFieldName(fieldName)))
            {
                var lowerDouble = ConvertToDouble(lower);
                var upperDouble = ConvertToDouble(upper);
                if (lowerDouble != null || upperDouble != null)
                {
                    return NumericRangeFilter.NewDoubleRange(fieldName, lowerDouble, upperDouble, includeLower, includeUpper);
                }
            }

            if (availableFields.Contains(LuceneSearchHelper.GetIntegerFieldName(fieldName)))
            {
                var lowerDouble = ConvertToInteger(lower);
                var upperDouble = ConvertToInteger(upper);
                if (lowerDouble != null || upperDouble != null)
                {
                    return NumericRangeFilter.NewInt32Range(fieldName, lowerDouble, upperDouble, includeLower, includeUpper);
                }
            }

            return TermRangeFilter.NewStringRange(fieldName, lower, upper, includeLower, includeUpper);
        }

        public static Filter CreateTermsFilter(string fieldName, string value)
        {
            var query = new TermsFilter(new Term(fieldName, value));
            return query;
        }

        private static TermsFilter CreateTermsFilter(string fieldName, IEnumerable<string> values)
        {
            var query = new TermsFilter(values.Select(v => new Term(fieldName, v)).ToList());

            return query;
        }

        private static string GetFilterValue(string fieldName, string value, ICollection<string> availableFields)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            if (availableFields.Contains(LuceneSearchHelper.GetBooleanFieldName(fieldName)))
            {
                return bool.Parse(value).ToStringInvariant();
            }

            if (availableFields.Contains(LuceneSearchHelper.GetDateTimeFieldName(fieldName)))
            {
                var longValue = ConvertToDateTimeTicks(value)!.Value;
                var stringValue = ConvertLongToString(longValue);
                return stringValue;
            }

            if (availableFields.Contains(LuceneSearchHelper.GetDoubleFieldName(fieldName)))
            {
                var doubleValue = ConvertToDouble(value)!.Value;
                var longValue = NumericUtils.DoubleToSortableInt64(doubleValue);
                var stringValue = ConvertLongToString(longValue);
                return stringValue;
            }

            if (availableFields.Contains(LuceneSearchHelper.GetIntegerFieldName(fieldName)))
            {
                var intValue = ConvertToInteger(value)!.Value;
                var stringValue = ConvertIntToString(intValue);
                return stringValue;
            }

            return value;
        }

        private static long? ConvertToDateTimeTicks(string input)
        {
            if (DateTime.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
            {
                return value.Ticks;
            }

            return null;
        }

        private static double? ConvertToDouble(string input)
        {
            if (double.TryParse(input, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return null;
        }

        private static int? ConvertToInteger(string input)
        {
            if (int.TryParse(input, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            {
                return value;
            }

            return null;
        }

        private static string ConvertLongToString(long longValue)
        {
            var bytes = new BytesRef();
            NumericUtils.Int64ToPrefixCoded(longValue, 0, bytes);
            return bytes.Utf8ToString();
        }

        private static string ConvertIntToString(int intValue)
        {
            var bytes = new BytesRef();
            NumericUtils.Int32ToPrefixCoded(intValue, 0, bytes);
            return bytes.Utf8ToString();
        }
    }
}
