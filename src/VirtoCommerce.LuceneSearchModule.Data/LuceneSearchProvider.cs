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
using Spatial4n.Core.Context;
using VirtoCommerce.Platform.Core.Common;
using VirtoCommerce.SearchModule.Core.Exceptions;
using VirtoCommerce.SearchModule.Core.Model;
using VirtoCommerce.SearchModule.Core.Services;

namespace VirtoCommerce.LuceneSearchModule.Data
{
    public class LuceneSearchProvider : ISearchProvider
    {
        private static readonly object _providerlock = new object();
        private static readonly Dictionary<string, IndexWriter> _indexWriters = new Dictionary<string, IndexWriter>();
        private static readonly SpatialContext _spatialContext = SpatialContext.GEO;

        private readonly LuceneSearchOptions _luceneSearchOptions;
        private readonly SearchOptions _searchOptions;
        private readonly string[] textFields = new[] { "__content", "content" };

        public LuceneSearchProvider(IOptions<LuceneSearchOptions> luceneSearchOptions, IOptions<SearchOptions> searchOptions)
        {
            if (luceneSearchOptions == null)
                throw new ArgumentNullException(nameof(luceneSearchOptions));
            _luceneSearchOptions = luceneSearchOptions.Value;

            if (searchOptions == null)
                throw new ArgumentNullException(nameof(searchOptions));
            _searchOptions = searchOptions.Value;
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

                if (!System.IO.Directory.Exists(directoryPath))
                {
                    System.IO.Directory.CreateDirectory(directoryPath);
                }

                using (var directory = DirectoryReader.Open(FSDirectory.Open(directoryPath)))
                {
                    var searcher = new IndexSearcher(directory);

                    var availableFields = directory.GetAllFacetableFields();
                    var providerRequest = LuceneSearchRequestBuilder.BuildRequest(request, indexName, documentType, availableFields);

                    var query = string.IsNullOrEmpty(providerRequest?.Query?.ToString()) ? new MatchAllDocsQuery() : providerRequest.Query;
                    var filter = providerRequest?.Filter?.ToString().Equals("BooleanFilter()") == true ? null : providerRequest?.Filter;
                    var sort = providerRequest?.Sort;
                    var count = Math.Max(providerRequest?.Count ?? 0, 1);

                    var providerResponse = sort != null
                        ? searcher.Search(query, filter, count, sort)
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

        protected virtual IList<IIndexableField> ConvertToProviderFields(IndexDocumentField field)
        {
            var result = new List<IIndexableField>();

            var fieldName = LuceneSearchHelper.ToLuceneFieldName(field.Name);

            switch (field.Value)
            {
                case string _:
                    if (textFields.Any(x => x.EqualsInvariant(field.Name)))
                    {
                        result.AddRange(field.Values.Select(v =>
                            new Field(fieldName, (string)v, field.IsRetrievable ? TextField.TYPE_STORED : TextField.TYPE_NOT_STORED)));
                        if (field.IsSearchable)
                        {
                            result.AddRange(field.Values.Select(v =>
                                new Field(LuceneSearchHelper.SearchableFieldName, (string)v, TextField.TYPE_NOT_STORED)));
                        }
                    }
                    else
                    {
                        result.AddRange(field.Values.Select(v =>
                            new Field(fieldName, (string)v, field.IsRetrievable ? StringField.TYPE_STORED : StringField.TYPE_NOT_STORED)));
                        if (field.IsSearchable)
                        {
                            result.AddRange(field.Values.Select(v =>
                                new Field(LuceneSearchHelper.SearchableFieldName, (string)v, StringField.TYPE_NOT_STORED)));
                        }
                    }
                    break;
                case bool _:
                    var booleanFieldName = LuceneSearchHelper.GetBooleanFieldName(field.Name);

                    foreach (var value in field.Values)
                    {
                        var stringValue = value.ToStringInvariant();
                        result.Add(new Field(fieldName, stringValue, field.IsRetrievable ? StringField.TYPE_STORED : StringField.TYPE_NOT_STORED));
                        result.Add(new Field(booleanFieldName, stringValue, StringField.TYPE_NOT_STORED));
                    }
                    break;
                case DateTime _:
                    var dateTimeFieldName = LuceneSearchHelper.GetDateTimeFieldName(field.Name);

                    foreach (var value in field.Values)
                    {
                        var numericField = new Int64Field(fieldName, ((DateTime)value).Ticks, field.IsRetrievable ? Int64Field.TYPE_STORED : Int64Field.TYPE_NOT_STORED);
                        result.Add(numericField);
                        result.Add(new Field(dateTimeFieldName, value.ToStringInvariant(), StringField.TYPE_NOT_STORED));
                    }
                    break;
                case GeoPoint _:
                    var geoPoint = (GeoPoint)field.Value;
                    var shape = _spatialContext.MakePoint(geoPoint.Longitude, geoPoint.Latitude);
                    var strategy = new PointVectorStrategy(_spatialContext, fieldName);

                    result.AddRange(strategy.CreateIndexableFields(shape));
                    result.Add(new StoredField(strategy.FieldName, shape.X.ToString(CultureInfo.InvariantCulture) + " " + shape.Y.ToString(CultureInfo.InvariantCulture)));

                    break;
                default:
                    if (double.TryParse(field.Value.ToStringInvariant(), NumberStyles.Float, CultureInfo.InvariantCulture, out _))
                    {
                        var facetableFieldName = LuceneSearchHelper.GetFacetableFieldName(field.Name);

                        foreach (var value in field.Values)
                        {
                            var stringValue = value.ToStringInvariant();

                            var doubleField = new DoubleField(fieldName, double.Parse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture), field.IsRetrievable ? DoubleField.TYPE_STORED : DoubleField.TYPE_NOT_STORED);

                            result.Add(doubleField);

                            result.Add(new Field(facetableFieldName, stringValue, StringField.TYPE_NOT_STORED));
                        }
                    }
                    else
                    {
                        result.AddRange(field.Values.Select(value =>
                            new Field(fieldName, value.ToStringInvariant(), field.IsRetrievable ? StringField.TYPE_STORED : StringField.TYPE_NOT_STORED)));
                    }
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
            lock (_providerlock)
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
