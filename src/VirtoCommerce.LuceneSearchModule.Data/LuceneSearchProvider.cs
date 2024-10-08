using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Lucene.Net.Analysis;
using Lucene.Net.Analysis.Standard;
using Lucene.Net.Documents;
using Lucene.Net.Index;
using Lucene.Net.Search;
using Lucene.Net.Spatial.Vector;
using Lucene.Net.Store;
using Lucene.Net.Util;
using Microsoft.Extensions.Options;
using Spatial4n.Context;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;
using static VirtoCommerce.SearchModule.Core.Extensions.IndexDocumentExtensions;

namespace VirtoCommerce.LuceneSearchModule.Data
{
    public class LuceneSearchProvider : ISearchProvider
    {
        private static readonly object _providerLock = new();
        private static readonly Dictionary<string, IndexWriter> _indexWriters = new();
        private static readonly SpatialContext _spatialContext = SpatialContext.Geo;

        private readonly LuceneSearchOptions _luceneSearchOptions;
        private readonly SearchOptions _searchOptions;
        private readonly string[] _textFields = { ContentFieldName, "content" };

        public LuceneSearchProvider(IOptions<LuceneSearchOptions> luceneSearchOptions, IOptions<SearchOptions> searchOptions)
        {
            if (luceneSearchOptions == null)
            {
                throw new ArgumentNullException(nameof(luceneSearchOptions));
            }

            _luceneSearchOptions = luceneSearchOptions.Value;

            if (searchOptions == null)
            {
                throw new ArgumentNullException(nameof(searchOptions));
            }

            _searchOptions = searchOptions.Value;
        }

        public virtual Task DeleteIndexAsync(string documentType)
        {
            if (string.IsNullOrEmpty(documentType))
            {
                throw new ArgumentNullException(nameof(documentType));
            }

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

            lock (_providerLock)
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

            Analyzer analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
            var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer);

            using (var directory = FSDirectory.Open(directoryPath))
            using (var writer = new IndexWriter(directory, config))
            {
                foreach (var document in documents)
                {
                    var resultItem = new IndexingResultItem { Id = document.Id };
                    result.Items.Add(resultItem);

                    try
                    {
                        if (!string.IsNullOrEmpty(document.Id))
                        {
                            var trackingWriter = new TrackingIndexWriter(writer);
                            var term = new Term(LuceneSearchHelper.KeyFieldName, document.Id);
                            var deleteResult = trackingWriter.DeleteDocuments(new TermQuery(term));
                            resultItem.Succeeded = deleteResult == 1;
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
                using var directory = FSDirectory.Open(directoryPath);

                if (!DirectoryReader.IndexExists(directory))
                {
                    return Task.FromResult(new SearchResponse());
                }

                using (var reader = DirectoryReader.Open(directory))
                {
                    var searcher = new IndexSearcher(reader);

                    var availableFields = reader.GetAllFacetableFields();
                    var providerRequest = LuceneSearchRequestBuilder.BuildRequest(request, indexName, documentType, availableFields);

                    var query = string.IsNullOrEmpty(providerRequest?.Query?.ToString()) ? new MatchAllDocsQuery() : providerRequest.Query;
                    var filter = providerRequest?.Filter?.ToString()?.Equals("BooleanFilter()") == true ? null : providerRequest?.Filter;
                    var sort = providerRequest?.Sort;
                    var count = Math.Max(providerRequest?.Count ?? 0, 1);

                    var providerResponse = sort != null
                        ? searcher.Search(query, filter, count, sort, doDocScores: sort.NeedsScores, doMaxScore: sort.NeedsScores)
                        : searcher.Search(query, filter, count);

                    var result = providerResponse.ToSearchResponse(request, searcher, documentType, availableFields);
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

            document.Fields.Insert(0, new IndexDocumentField(LuceneSearchHelper.KeyFieldName, document.Id, IndexDocumentFieldValueType.String) { IsRetrievable = true, IsFilterable = true });

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

        protected virtual IList<IIndexableField> ConvertToProviderFields(IndexDocumentField field)
        {
            var result = new List<IIndexableField>();

            var fieldName = LuceneSearchHelper.ToLuceneFieldName(field.Name);

            switch (field.ValueType)
            {
                case IndexDocumentFieldValueType.String:
                    var isTextField = _textFields.Any(x => x.EqualsInvariant(field.Name));
                    var stored = isTextField ? TextField.TYPE_STORED : StringField.TYPE_STORED;
                    var notStored = isTextField ? TextField.TYPE_NOT_STORED : StringField.TYPE_NOT_STORED;
                    foreach (var value in field.Values)
                    {
                        var stringValue = (string)value;
                        result.Add(new Field(fieldName, stringValue, field.IsRetrievable ? stored : notStored));
                        if (field.IsSearchable)
                        {
                            result.Add(new Field(LuceneSearchHelper.SearchableFieldName, stringValue, notStored));
                        }
                    }
                    break;
                case IndexDocumentFieldValueType.Boolean:
                    foreach (var value in field.Values)
                    {
                        var stringValue = value.ToStringInvariant();
                        result.Add(new Field(fieldName, stringValue, field.IsRetrievable ? StringField.TYPE_STORED : StringField.TYPE_NOT_STORED));
                        result.Add(new Field(LuceneSearchHelper.GetBooleanFieldName(field.Name), stringValue, StringField.TYPE_NOT_STORED));
                    }
                    break;
                case IndexDocumentFieldValueType.DateTime:
                    foreach (var value in field.Values)
                    {
                        var stringValue = value.ToStringInvariant();
                        var numericField = new Int64Field(fieldName, ((DateTime)value).Ticks, field.IsRetrievable ? Int64Field.TYPE_STORED : Int64Field.TYPE_NOT_STORED);
                        result.Add(numericField);
                        result.Add(new Field(LuceneSearchHelper.GetDateTimeFieldName(field.Name), stringValue, StringField.TYPE_NOT_STORED));
                    }
                    break;
                case IndexDocumentFieldValueType.GeoPoint:
                    var geoPoint = (GeoPoint)field.Value;
                    var shape = _spatialContext.MakePoint(geoPoint.Longitude, geoPoint.Latitude);
                    var strategy = new PointVectorStrategy(_spatialContext, fieldName);
                    result.AddRange(strategy.CreateIndexableFields(shape));
                    result.Add(new StoredField(strategy.FieldName, shape.X.ToString(CultureInfo.InvariantCulture) + " " + shape.Y.ToString(CultureInfo.InvariantCulture)));
                    break;
                case IndexDocumentFieldValueType.Decimal:
                case IndexDocumentFieldValueType.Double:
                case IndexDocumentFieldValueType.Float:
                    foreach (var value in field.Values)
                    {
                        var stringValue = value.ToStringInvariant();
                        result.Add(new DoubleField(fieldName, double.Parse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture), field.IsRetrievable ? DoubleField.TYPE_STORED : DoubleField.TYPE_NOT_STORED));
                        result.Add(new Field(LuceneSearchHelper.GetDoubleFieldName(field.Name), stringValue, StringField.TYPE_NOT_STORED));
                    }
                    break;
                case IndexDocumentFieldValueType.Integer:
                    foreach (var value in field.Values)
                    {
                        var stringValue = value.ToStringInvariant();
                        result.Add(new Int32Field(fieldName, int.Parse(stringValue, NumberStyles.Integer, CultureInfo.InvariantCulture), field.IsRetrievable ? Int32Field.TYPE_STORED : Int32Field.TYPE_NOT_STORED));
                        result.Add(new Field(LuceneSearchHelper.GetIntegerFieldName(field.Name), stringValue, StringField.TYPE_NOT_STORED));
                    }
                    break;
                case IndexDocumentFieldValueType.Complex:
                    {
                        var stringValue = field.Value.SerializeJson();
                        result.Add(new StoredField(fieldName, stringValue));
                        result.Add(new Field(LuceneSearchHelper.GetComplexFieldName(field.Name), string.Empty, StringField.TYPE_NOT_STORED));
                    }
                    break;
                default:
                    result.AddRange(field.Values.Select(value =>
                        new Field(fieldName, value.ToStringInvariant(), field.IsRetrievable ? StringField.TYPE_STORED : StringField.TYPE_NOT_STORED)));
                    break;
            }

            return result;
        }

        protected virtual string GetIndexName(string documentType)
        {
            // Use different index for each document type
            return string.Join("-", _searchOptions.GetScope(documentType), documentType);
        }

        protected virtual void CloseWriter(string indexName, bool optimize)
        {
            lock (_providerLock)
            {
                if (_indexWriters.ContainsKey(indexName) && _indexWriters[indexName] != null)
                {
                    var writer = _indexWriters[indexName];
                    writer.Dispose(true); // added waiting for all merges to complete
                    _indexWriters.Remove(indexName);
                }
            }
        }

        protected virtual IndexWriter GetIndexWriter(string indexName, bool create, bool createNew)
        {
            IndexWriter result = null;

            lock (_providerLock)
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

                        Analyzer analyzer = new StandardAnalyzer(LuceneVersion.LUCENE_48);
                        var config = new IndexWriterConfig(LuceneVersion.LUCENE_48, analyzer)
                        {
                            OpenMode = createNew ? OpenMode.CREATE : OpenMode.CREATE_OR_APPEND
                        };
                        var writer = new IndexWriter(directory, config);
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
            return Path.Combine(_luceneSearchOptions.Path, indexName);
        }
    }
}
