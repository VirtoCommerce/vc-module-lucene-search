using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Spatial.Vector;
using Lucene.Net.Store;
using Spatial4n.Core.Context;
using VirtoCommerce.Domain.Search;

namespace VirtoCommerce.LuceneSearchModule.Data
{
    public class LuceneSearchProvider : ISearchProvider
    {
        private static readonly object _providerlock = new object();
        private static readonly Dictionary<string, IndexWriter> _indexWriters = new Dictionary<string, IndexWriter>();
        private static readonly SpatialContext _spatialContext = SpatialContext.GEO;


        public string DataDirectoryPath { get; }
        public string Scope { get; }

        public LuceneSearchProvider(LuceneSearchProviderSettings settings)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            DataDirectoryPath = settings.DataDirectoryPath;
            Scope = settings.Scope;
        }

        public virtual Task DeleteIndexAsync(string documentType)
        {
            if (string.IsNullOrEmpty(documentType))
                throw new ArgumentNullException(nameof(documentType));

            var indexName = GetIndexName(documentType);

            // Make sure the existing writer is closed
            CloseWriter(indexName, false);

            // re-initialize the write, so all documents are deleted
            GetIndexWriter(indexName, true, true);

            // now close the write so changes are saved
            CloseWriter(indexName, false);

            return Task.FromResult<object>(null);
        }

        public virtual Task<IndexingResult> IndexAsync(string documentType, IList<IndexDocument> documents)
        {
            var result = new IndexingResult
            {
                Items = new List<IndexingResultItem>(documents.Count)
            };

            var indexName = GetIndexName(documentType);

            lock (_providerlock)
            {
                var writer = GetIndexWriter(indexName, true, false);

                foreach (var document in documents)
                {
                    var resultItem = new IndexingResultItem { Id = document.Id };
                    result.Items.Add(resultItem);

                    try
                    {
                        var providerDocument = ConvertToProviderDocument(document, documentType);

                        if (!string.IsNullOrEmpty(document.Id))
                        {
                            var term = new Term(LuceneSearchHelper.KeyFieldName, document.Id);
                            writer.UpdateDocument(term, providerDocument);
                            resultItem.Succeeded = true;
                        }
                        else
                        {
                            resultItem.ErrorMessage = "Document ID is empty";
                        }
                    }
                    catch (Exception ex)
                    {
                        resultItem.ErrorMessage = ex.ToString();
                    }
                }
            }

            CloseWriter(indexName, true);

            return Task.FromResult(result);
        }

        public virtual Task<IndexingResult> RemoveAsync(string documentType, IList<IndexDocument> documents)
        {
            var result = new IndexingResult
            {
                Items = new List<IndexingResultItem>(documents.Count)
            };

            var indexName = GetIndexName(documentType);
            var directoryPath = GetDirectoryPath(indexName);

            CloseWriter(indexName, false);

            using (var directory = FSDirectory.Open(directoryPath))
            using (var reader = IndexReader.Open(directory, false))
            {
                foreach (var document in documents)
                {
                    var resultItem = new IndexingResultItem { Id = document.Id };
                    result.Items.Add(resultItem);

                    try
                    {
                        if (!string.IsNullOrEmpty(document.Id))
                        {
                            var term = new Term(LuceneSearchHelper.KeyFieldName, document.Id);
                            var num = reader.DeleteDocuments(term);
                            resultItem.Succeeded = num == 1;
                        }
                        else
                        {
                            resultItem.ErrorMessage = "Document ID is empty";
                        }
                    }
                    catch (Exception ex)
                    {
                        resultItem.ErrorMessage = ex.ToString();
                    }
                }
            }

            return Task.FromResult(result);
        }

        public virtual Task<SearchResponse> SearchAsync(string documentType, SearchRequest request)
        {
            try
            {
                var indexName = GetIndexName(documentType);
                var directoryPath = GetDirectoryPath(indexName);

                using (var directory = FSDirectory.Open(directoryPath))
                using (var searcher = new IndexSearcher(directory))
                {
                    var reader = searcher.IndexReader;
                    var availableFields = reader.GetFieldNames(IndexReader.FieldOption.ALL);
                    var providerRequest = LuceneSearchRequestBuilder.BuildRequest(request, indexName, documentType, availableFields);

                    var query = string.IsNullOrEmpty(providerRequest?.Query?.ToString()) ? new MatchAllDocsQuery() : providerRequest.Query;
                    var filter = providerRequest?.Filter?.ToString().Equals("BooleanFilter()") == true ? null : providerRequest?.Filter;
                    var sort = providerRequest?.Sort;
                    var count = Math.Max(providerRequest?.Count ?? 0, 1);

                    var providerResponse = sort != null
                        ? searcher.Search(query, filter, count, sort)
                        : searcher.Search(query, filter, count);

                    var result = providerResponse.ToSearchResponse(request, searcher, documentType, availableFields, query);
                    return Task.FromResult(result);
                }
            }
            catch (Exception ex)
            {
                throw new SearchException(ex.Message, ex);
            }
        }


        protected virtual Document ConvertToProviderDocument(IndexDocument document, string documentType)
        {
            var result = new Document();

            document.Fields.Insert(0, new IndexDocumentField(LuceneSearchHelper.KeyFieldName, document.Id) { IsRetrievable = true, IsFilterable = true });

            var providerFields = document.Fields
                .Where(f => f.Value != null)
                .OrderBy(f => f.Name)
                .SelectMany(ConvertToProviderFields)
                .ToArray();

            foreach (var providerField in providerFields)
            {
                result.Add(providerField);
            }

            return result;
        }

        protected virtual IList<IFieldable> ConvertToProviderFields(IndexDocumentField field)
        {
            // TODO: Introduce and use metadata describing value type

            var result = new List<IFieldable>();

            var fieldName = LuceneSearchHelper.ToLuceneFieldName(field.Name);
            var store = field.IsRetrievable ? Field.Store.YES : Field.Store.NO;
            var index = field.IsSearchable ? Field.Index.ANALYZED : field.IsFilterable ? Field.Index.NOT_ANALYZED : Field.Index.NO;

            if (field.Value is string)
            {
                foreach (var value in field.Values)
                {
                    result.Add(new Field(fieldName, (string)value, store, index));

                    if (field.IsSearchable)
                    {
                        result.Add(new Field(LuceneSearchHelper.SearchableFieldName, (string)value, Field.Store.NO, Field.Index.ANALYZED));
                    }
                }
            }
            else if (field.Value is bool)
            {
                var booleanFieldName = LuceneSearchHelper.GetBooleanFieldName(field.Name);

                foreach (var value in field.Values)
                {
                    var stringValue = value.ToStringInvariant();
                    result.Add(new Field(fieldName, stringValue, store, index));
                    result.Add(new Field(booleanFieldName, stringValue, Field.Store.NO, Field.Index.NOT_ANALYZED));
                }
            }
            else if (field.Value is DateTime)
            {
                var dateTimeFieldName = LuceneSearchHelper.GetDateTimeFieldName(field.Name);

                foreach (var value in field.Values)
                {
                    var numericField = new NumericField(fieldName, store, index != Field.Index.NO);
                    numericField.SetLongValue(((DateTime)value).Ticks);
                    result.Add(numericField);
                    result.Add(new Field(dateTimeFieldName, value.ToStringInvariant(), Field.Store.NO, Field.Index.NOT_ANALYZED));
                }
            }
            else if (field.Value is GeoPoint)
            {
                var geoPoint = (GeoPoint)field.Value;

                result.Add(new Field(fieldName, geoPoint.ToString(), Field.Store.YES, Field.Index.NOT_ANALYZED));

                var shape = _spatialContext.MakePoint(geoPoint.Longitude, geoPoint.Latitude);
                var strategy = new PointVectorStrategy(_spatialContext, fieldName);

                foreach (var f in strategy.CreateIndexableFields(shape))
                {
                    result.Add(f);
                }
            }
            else
            {
                double t;
                if (double.TryParse(field.Value.ToStringInvariant(), NumberStyles.Float, CultureInfo.InvariantCulture, out t))
                {
                    var facetableFieldName = LuceneSearchHelper.GetFacetableFieldName(field.Name);

                    foreach (var value in field.Values)
                    {
                        var stringValue = value.ToStringInvariant();

                        var numericField = new NumericField(fieldName, store, index != Field.Index.NO);
                        numericField.SetDoubleValue(double.Parse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture));
                        result.Add(numericField);

                        result.Add(new Field(facetableFieldName, stringValue, Field.Store.NO, Field.Index.NOT_ANALYZED));
                    }
                }
                else
                {
                    result.AddRange(field.Values.Select(value => new Field(fieldName, value.ToStringInvariant(), store, index)));
                }
            }

            return result;
        }

        protected virtual string GetIndexName(string documentType)
        {
            // Use different index for each document type
            return string.Join("-", Scope, documentType);
        }

        protected virtual void CloseWriter(string indexName, bool optimize)
        {
            lock (_providerlock)
            {
                if (_indexWriters.ContainsKey(indexName) && _indexWriters[indexName] != null)
                {
                    var writer = _indexWriters[indexName];
                    if (optimize)
                    {
                        writer.Optimize();
                    }

                    writer.Dispose(true); // added waiting for all merges to complete
                    _indexWriters.Remove(indexName);
                }
            }
        }

        protected virtual IndexWriter GetIndexWriter(string indexName, bool create, bool createNew)
        {
            IndexWriter result = null;

            lock (_providerlock)
            {
                if (!_indexWriters.ContainsKey(indexName) || _indexWriters[indexName] == null)
                {
                    if (create)
                    {
                        var directory = FSDirectory.Open(GetDirectoryPath(indexName));

                        // Create new directory if it doesn't exist
                        if (!directory.Directory.Exists)
                        {
                            createNew = true;
                        }

                        var writer = new IndexWriter(directory, new StandardAnalyzer(Lucene.Net.Util.Version.LUCENE_30), createNew, IndexWriter.MaxFieldLength.LIMITED);
                        _indexWriters[indexName] = writer;

                        result = writer;
                    }
                }
                else
                {
                    result = _indexWriters[indexName];
                }
            }

            return result;
        }

        protected virtual string GetDirectoryPath(string indexName)
        {
            return Path.Combine(DataDirectoryPath, indexName);
        }
    }
}
