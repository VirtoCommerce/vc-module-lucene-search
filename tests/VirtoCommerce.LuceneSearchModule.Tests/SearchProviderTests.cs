using System;
using System.Linq;
using System.Threading.Tasks;
using VirtoCommerce.SearchModule.Core.Model;
using Xunit;

namespace VirtoCommerce.LuceneSearchModule.Tests
{
    [TestCaseOrderer(PriorityTestCaseOrderer.TypeName, PriorityTestCaseOrderer.AssembyName)]
    [Trait("Category", "IntegrationTest")]
    public abstract class SearchProviderTests : SearchProviderTestsBase
    {
        public const string DocumentType = "item";

        protected SearchProviderTests()
        {
            var provider = GetSearchProvider();

            provider.DeleteIndexAsync(DocumentType).GetAwaiter().GetResult();
            provider.IndexAsync(DocumentType, GetPrimaryDocuments()).GetAwaiter().GetResult();
            provider.IndexAsync(DocumentType, GetSecondaryDocuments()).GetAwaiter().GetResult();
        }

        [Fact]
        public virtual async Task CanAddAndRemoveDocuments()
        {
            // Arrange
            var provider = GetSearchProvider();

            // Act
            // Delete index
            await provider.DeleteIndexAsync(DocumentType);


            // Create index and add documents
            var primaryDocuments = GetPrimaryDocuments();
            var response = await provider.IndexAsync(DocumentType, primaryDocuments);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Items);
            Assert.Equal(primaryDocuments.Count, response.Items.Count);
            Assert.All(response.Items, i => Assert.True(i.Succeeded));

            // Act
            // Update index with new fields and add more documents
            var secondaryDocuments = GetSecondaryDocuments();
            response = await provider.IndexAsync(DocumentType, secondaryDocuments);

            // Assert
            Assert.NotNull(response);
            Assert.NotNull(response.Items);
            Assert.Equal(secondaryDocuments.Count, response.Items.Count);
            Assert.All(response.Items, i => Assert.True(i.Succeeded));

            //todo
            //// Remove some documents
            //response = await provider.RemoveAsync(DocumentType, new[] { new IndexDocument("Item-7"), new IndexDocument("Item-8") });

            //Assert.NotNull(response);
            //Assert.NotNull(response.Items);
            //Assert.Equal(2, response.Items.Count);
            //Assert.All(response.Items, i => Assert.True(i.Succeeded));
        }

        [Fact]
        public virtual async Task CanLimitResults()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Skip = 4,
                Take = 3,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(3, response.DocumentsCount);
            Assert.Equal(7, response.TotalCount);
        }

        [Fact]
        public virtual async Task CanRetriveStringCollection()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            var document = response?.Documents?.FirstOrDefault();
            Assert.NotNull(document);

            var stringCollection = document["catalog"] as object[];
            Assert.NotNull(stringCollection);
            Assert.Equal(2, stringCollection.Length);
        }

        //todo
        [Fact]
        public virtual async Task CanSortByStringField()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Sorting = new[]
                {
                    // Sorting by non-existent field should be ignored
                    new SortingField { FieldName = "non-existent-field" },
                    new SortingField { FieldName = "Name" },
                },
                Take = 1,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(1, response.DocumentsCount);

            var productName = response.Documents.First()["name"] as string;
            Assert.Equal("Black Sox", productName);

            // Arrange
            request = new SearchRequest
            {
                Sorting = new[] { new SortingField { FieldName = "Name", IsDescending = true } },
                Take = 1,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(1, response.DocumentsCount);

            productName = response.Documents.First()["name"] as string;
            Assert.Equal("Sample Product", productName);
        }

        [Fact]
        public virtual async Task CanSortByNumericField()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Sorting = new[] { new SortingField { FieldName = "Size", IsDescending = true } },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            var firstProduct = response.Documents.First();
            var lastProduct = response.Documents.Last();
            Assert.Equal("Green Sox", firstProduct["name"] as string);
            Assert.Equal(7, response.DocumentsCount);
            Assert.Equal(30, Convert.ToInt32(firstProduct["size"]));
            Assert.Equal(2, Convert.ToInt32(lastProduct["size"]));
        }

        //todo
        //[Fact]
        //public virtual async Task CanSortByGeoDistance()
        //{
        //    var provider = GetSearchProvider();

        //    var request = new SearchRequest
        //    {
        //        Sorting = new SortingField[]
        //        {
        //            new GeoDistanceSortingField
        //            {
        //                FieldName = "Location",
        //                Location = GeoPoint.TryParse("0, 14")
        //            }
        //        },
        //        Take = 10,
        //    };

        //    var response = await provider.SearchAsync(DocumentType, request);

        //    Assert.Equal(6, response.DocumentsCount);

        //    Assert.Equal("Item-2", response.Documents[0].Id);
        //    Assert.Equal("Item-3", response.Documents[1].Id);
        //    Assert.Equal("Item-1", response.Documents[2].Id);
        //    Assert.Equal("Item-4", response.Documents[3].Id);
        //    Assert.Equal("Item-5", response.Documents[4].Id);
        //    Assert.Equal("Item-6", response.Documents[5].Id);
        //}

        //todo
        //[Fact]
        //public virtual async Task CanSortByGeoDistanceDescending()
        //{
        //    var provider = GetSearchProvider();

        //    var request = new SearchRequest
        //    {
        //        Sorting = new SortingField[]
        //        {
        //            new GeoDistanceSortingField
        //            {
        //                FieldName = "Location",
        //                Location = GeoPoint.Parse("0, 14"),
        //                IsDescending = true,
        //            }
        //        },
        //        Take = 10,
        //    };

        //    var response = await provider.SearchAsync(DocumentType, request);

        //    Assert.Equal(6, response.DocumentsCount);

        //    Assert.Equal("Item-6", response.Documents[0].Id);
        //    Assert.Equal("Item-5", response.Documents[1].Id);
        //    Assert.Equal("Item-4", response.Documents[2].Id);
        //    Assert.Equal("Item-1", response.Documents[3].Id);
        //    Assert.Equal("Item-3", response.Documents[4].Id);
        //    Assert.Equal("Item-2", response.Documents[5].Id);
        //}

        [Fact]
        public virtual async Task CanSearchByKeywords()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                SearchKeywords = " shirt ",
                SearchFields = new[] { "Content" },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(3, response.DocumentsCount);

            // Arrange
            request = new SearchRequest
            {
                SearchKeywords = "red shirt",
                SearchFields = new[] { "Content" },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task CanFilterByIds()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Filter = new IdsFilter
                {
                    Values = new[] { "Item-2", "Item-3", "Item-9" },
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);

            Assert.True(response.Documents.Any(d => d.Id == "Item-2"), "Cannot find 'Item-2'");
            Assert.True(response.Documents.Any(d => d.Id == "Item-3"), "Cannot find 'Item-3'");
        }

        //todo
        [Fact]
        public virtual async Task CanFilterByTerm()
        {
            // Arrange
            var provider = GetSearchProvider();

            // Filter by code with integer value
            var request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "code",
                    Values = new[]
                    {
                        "565567699"
                    }
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(1, response.DocumentsCount);

            // Arrange
            // Filtering by non-existent field name leads to empty result
            request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "non-existent-field",
                    Values = new[] { "value-does-not-matter" }
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);

            // Arrange
            // Filtering by non-existent field value leads to empty result
            request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "Color",
                    Values = new[]
                    {
                        "non-existent-value-1",
                        "non-existent-value-2",
                    }
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);

            // Arrange
            request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "Color",
                    Values = new[]
                    {
                            "Red",
                            "Blue",
                            "Black",
                        }
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(5, response.DocumentsCount);

            // Arrange
            request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "Is",
                    Values = new[]
                    {
                            "Red",
                            "Blue",
                            "Black",
                        }
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(5, response.DocumentsCount);

            // Arrange
            request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "Size",
                    Values = new[]
                    {
                            "1",
                            "2",
                            "3",
                        }
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);

            // Arrange
            request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "Date",
                    Values = new[]
                    {
                            "2017-04-29T15:24:31.180Z",
                            "2017-04-28T15:24:31.180Z",
                            "2017-04-27T15:24:31.180Z",
                        }
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task CanFilterByBooleanTerm()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "HasMultiplePrices",
                    Values = new[] { "tRue" } // Value should be case insensitive
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);

            // Arrange
            request = new SearchRequest
            {
                Filter = new TermFilter
                {
                    FieldName = "HasMultiplePrices",
                    Values = new[] { "fAlse" } // Value should be case insensitive
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(5, response.DocumentsCount);
        }


        [Fact]
        public virtual async Task CanFilterByRange()
        {
            // Arrange
            var provider = GetSearchProvider();

            // Filtering by non-existent field name leads to empty result
            var request = new SearchRequest
            {
                Filter = new RangeFilter
                {
                    FieldName = "non-existent-field",
                    Values = new[]
                    {
                        new RangeFilterValue { Lower = "0", Upper = "4" },
                    }
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);

            // Arrange
            request = new SearchRequest
            {
                Filter = new RangeFilter
                {
                    FieldName = "Size",
                    Values = new[]
                    {
                        new RangeFilterValue { Lower = "0", Upper = "4" },
                        new RangeFilterValue { Lower = "", Upper = "4" },
                        new RangeFilterValue { Lower = null, Upper = "4" },
                        new RangeFilterValue { Lower = "4", Upper = "10" },
                    }
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task CanFilterByDateRange()
        {
            // Arrange
            var provider = GetSearchProvider();
            var criteria = new SearchRequest { Take = 0, Filter = CreateRangeFilter("Date", "2017-04-23T15:24:31.180Z", "2017-04-28T15:24:31.180Z", true, true) };
            // Act
            var response = await provider.SearchAsync(DocumentType, criteria);
            // Assert
            Assert.Equal(6, response.TotalCount);

            // Arrange
            criteria = new SearchRequest { Take = 0, Filter = CreateRangeFilter("Date", "2017-04-23T15:24:31.180Z", "2017-04-28T15:24:31.180Z", false, true) };
            // Act
            response = await provider.SearchAsync(DocumentType, criteria);
            // Assert
            Assert.Equal(5, response.TotalCount);

            // Arrange
            criteria = new SearchRequest { Take = 0, Filter = CreateRangeFilter("Date", "2017-04-23T15:24:31.180Z", "2017-04-28T15:24:31.180Z", true, false) };
            // Act
            response = await provider.SearchAsync(DocumentType, criteria);
            // Assert
            Assert.Equal(5, response.TotalCount);

            // Arrange
            criteria = new SearchRequest { Take = 0, Filter = CreateRangeFilter("Date", "2017-04-23T15:24:31.180Z", "2017-04-28T15:24:31.180Z", false, false) };
            // Act
            response = await provider.SearchAsync(DocumentType, criteria);
            // Assert
            Assert.Equal(4, response.TotalCount);

            // Arrange
            criteria = new SearchRequest { Take = 0, Filter = CreateRangeFilter("Date", null, "2017-04-28T15:24:31.180Z", true, true) };
            // Act
            response = await provider.SearchAsync(DocumentType, criteria);
            // Assert
            Assert.Equal(6, response.TotalCount);

            // Arrange
            criteria = new SearchRequest { Take = 0, Filter = CreateRangeFilter("Date", null, "2017-04-28T15:24:31.180Z", true, false) };
            // Act
            response = await provider.SearchAsync(DocumentType, criteria);
            // Assert
            Assert.Equal(5, response.TotalCount);

            // Arrange
            criteria = new SearchRequest { Take = 0, Filter = CreateRangeFilter("Date", "2017-04-23T15:24:31.180Z", null, true, false) };
            // Act
            response = await provider.SearchAsync(DocumentType, criteria);
            // Assert
            Assert.Equal(7, response.TotalCount);

            // Arrange
            criteria = new SearchRequest { Take = 0, Filter = CreateRangeFilter("Date", "2017-04-23T15:24:31.180Z", null, false, false) };
            // Act
            response = await provider.SearchAsync(DocumentType, criteria);
            // Assert
            Assert.Equal(6, response.TotalCount);
        }

        [Fact]
        public virtual async Task CanFilterByGeoDistance()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Filter = new GeoDistanceFilter
                {
                    FieldName = "Location",
                    Location = GeoPoint.TryParse("0, 14"),
                    Distance = 1110, // less than 10 degrees (1 degree at the equater is about 111 km)
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task CanInvertFilterWithNot()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Filter = new NotFilter
                {
                    ChildFilter = new TermFilter
                    {
                        FieldName = "Size",
                        Values = new[]
                        {
                            "1",
                            "2",
                            "3",
                        }
                    },
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(5, response.DocumentsCount);

            // Arrange
            request = new SearchRequest
            {
                Filter = new NotFilter
                {
                    ChildFilter = new RangeFilter
                    {
                        FieldName = "Size",
                        Values = new[]
                        {
                            new RangeFilterValue { Lower = "0", Upper = "4" },
                            new RangeFilterValue { Lower = "4", Upper = "20" },
                        }
                    },
                },
                Take = 10,
            };

            // Act
            response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(3, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task CanJoinFiltersWithAnd()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Filter = new AndFilter
                {
                    ChildFilters = new IFilter[]
                    {
                        new TermFilter
                        {
                            FieldName = "Color",
                            Values = new[]
                            {
                                "Red",
                                "Blue",
                                "Black",
                            }
                        },
                        new RangeFilter
                        {
                            FieldName = "Size",
                            Values = new[]
                            {
                                new RangeFilterValue { Lower = "0", Upper = "4" },
                                new RangeFilterValue { Lower = "4", Upper = "20", IncludeUpper = true },
                            }
                        },
                    }
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(4, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task CanJoinFiltersWithOr()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                // (Color = Red) OR (Size > 10)
                Filter = new OrFilter
                {
                    ChildFilters = new IFilter[]
                    {
                        new TermFilter
                        {
                            FieldName = "Color",
                            Values = new[]
                            {
                                "Red",
                            }
                        },
                        new RangeFilter
                        {
                            FieldName = "Size",
                            Values = new[]
                            {
                                new RangeFilterValue { Lower = "10" },
                            }
                        },
                    }
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(5, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task CanFilterByNestedFilters()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                // (Color = Red) OR (NOT(Color = Blue) AND (Size < 20))
                Filter = new OrFilter
                {
                    ChildFilters = new IFilter[]
                    {
                        new TermFilter
                        {
                            FieldName = "Color",
                            Values = new[]
                            {
                                "Red",
                            }
                        },
                        new AndFilter
                        {
                            ChildFilters = new IFilter[]
                            {
                                new NotFilter
                                {
                                    ChildFilter = new TermFilter
                                    {
                                        FieldName = "Color",
                                        Values = new[]
                                        {
                                            "Blue",
                                        }
                                    },
                                },
                                new RangeFilter
                                {
                                    FieldName = "Size",
                                    Values = new[]
                                    {
                                        new RangeFilterValue { Upper = "20" },
                                    }
                                },
                            },
                        },
                    }
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(4, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task SearchAsync_WildcardFilter_Works()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                // (Color = "*ed"))
                Filter = new WildCardTermFilter()
                {
                    FieldName = "Color",
                    Value = "*ed",
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(3, response.DocumentsCount);
        }

        [Fact]
        public virtual async Task CanLimitFacetSizeForStringField()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new TermAggregationRequest { FieldName = "Color", Size = 1 },
                },
                Take = 0,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(1, GetAggregationValuesCount(response, "Color"));
            Assert.Equal(3, GetAggregationValueCount(response, "Color", "Red"));
        }

        [Fact]
        public virtual async Task CanLimitFacetSizeForNumericField()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new TermAggregationRequest { FieldName = "Size", Size = 1 },
                },
                Take = 0,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(1, GetAggregationValuesCount(response, "Size"));
            Assert.Equal(2, GetAggregationValueCount(response, "Size", "10"));
        }

        [Fact]
        public virtual async Task CanGetAllFacetValuesForStringField()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    // Facets for non-existent fields should be ignored
                    new TermAggregationRequest { FieldName = "non-existent-field", Size = 0 },
                    new TermAggregationRequest { FieldName = "Color", Size = 0 },
                },
                Take = 0,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(5, GetAggregationValuesCount(response, "Color"));
            Assert.Equal(3, GetAggregationValueCount(response, "Color", "Red"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Black"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Blue"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Silver"));
        }

        [Fact]
        public virtual async Task CanGetAllFacetValuesForNumericField()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new TermAggregationRequest { FieldName = "Size", Size = 0 },
                },
                Take = 0,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(6, GetAggregationValuesCount(response, "Size"));
            Assert.Equal(1, GetAggregationValueCount(response, "Size", "2"));
            Assert.Equal(1, GetAggregationValueCount(response, "Size", "3"));
            Assert.Equal(1, GetAggregationValueCount(response, "Size", "4"));
            Assert.Equal(2, GetAggregationValueCount(response, "Size", "10"));
            Assert.Equal(1, GetAggregationValueCount(response, "Size", "20"));
        }

        [Fact]
        public virtual async Task CanGetSpecificFacetValuesForStringField()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new TermAggregationRequest
                    {
                        // Facets for non-existent fields should be ignored
                        FieldName = "non-existent-field",
                        Values = new[] { "Red" }
                    },
                    new TermAggregationRequest
                    {
                        FieldName = "Color",
                        Values = new[] { "Red", "Blue", "White" }
                    },
                },
                Take = 0,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(2, GetAggregationValuesCount(response, "Color"));
            Assert.Equal(3, GetAggregationValueCount(response, "Color", "Red"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Blue"));
        }

        [Fact]
        public virtual async Task CanGetSpecificFacetValuesForNumericField()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new TermAggregationRequest
                    {
                        FieldName = "Size",
                        Values = new[] { "3", "4", "5" }
                    },
                },
                Take = 0,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(2, GetAggregationValuesCount(response, "Size"));
            Assert.Equal(1, GetAggregationValueCount(response, "Size", "3"));
            Assert.Equal(1, GetAggregationValueCount(response, "Size", "4"));
        }

        [Fact]
        public virtual async Task CanGetRangeFacets()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new RangeAggregationRequest
                    {
                        // Facets for non-existent fields should be ignored
                        FieldName = "non-existent-field",
                        Values = new[] { new RangeAggregationRequestValue { Id = "5_to_20", Lower = "5", Upper = "20" } }
                    },
                    new RangeAggregationRequest
                    {
                        FieldName = "Size",
                        Values = new[]
                        {
                            new RangeAggregationRequestValue { Id = "5_to_20", Lower = "5", Upper = "20" },
                            new RangeAggregationRequestValue { Id = "0_to_5", Lower = "0", Upper = "5" },
                        }
                    },
                }
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            var size0To5Count = GetAggregationValueCount(response, "Size", "0_to_5");
            Assert.Equal(3, size0To5Count);

            var size5To10Count = GetAggregationValueCount(response, "Size", "5_to_20");
            Assert.Equal(2, size5To10Count);
        }

        [Fact]
        public virtual async Task CanGetAllFacetValuesWhenRequestFilterIsApplied()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new TermAggregationRequest
                    {
                        FieldName = "Color",
                        Values = new[] { "Red", "Blue", "Black", "Silver" }
                    },
                },
                Filter = new AndFilter
                {
                    ChildFilters = new IFilter[]
                    {
                        new TermFilter
                        {
                            FieldName = "Color",
                            Values = new[] { "Red", "Blue" }
                        },
                        new TermFilter
                        {
                            FieldName = "Size",
                            Values = new[] { "2", "4" }
                        },
                    }
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(4, GetAggregationValuesCount(response, "Color"));
            Assert.Equal(3, GetAggregationValueCount(response, "Color", "Red"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Black"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Blue"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Silver"));
        }

        [Fact]
        public async Task CanGetFacetWithFilterOnly()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new TermAggregationRequest
                    {
                        Id = "Filtered-Aggregation",
                        Filter = new TermFilter
                        {
                            FieldName = "Size",
                            Values = new[] { "10" }
                        },
                    },
                },
                Take = 0,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(0, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(1, GetAggregationValuesCount(response, "Filtered-Aggregation"));
            Assert.Equal(2, GetAggregationValueCount(response, "Filtered-Aggregation", "Filtered-Aggregation"));
        }

        [Fact]
        public virtual async Task CanApplyDifferentFiltersToFacetsAndRequest()
        {
            // Arrange
            var provider = GetSearchProvider();

            var request = new SearchRequest
            {
                Aggregations = new AggregationRequest[]
                {
                    new TermAggregationRequest
                    {
                        FieldName = "Color",
                        Values = new[] { "Red", "Blue", "Black", "Silver" },
                        Filter = new TermFilter
                        {
                            FieldName = "Size",
                            Values = new[] { "10" }
                        },
                    },
                },
                Filter = new AndFilter
                {
                    ChildFilters = new IFilter[]
                    {
                        new TermFilter
                        {
                            FieldName = "Color",
                            Values = new[] { "Red", "Blue" }
                        },
                        new TermFilter
                        {
                            FieldName = "Size",
                            Values = new[] { "2", "4" }
                        },
                    }
                },
                Take = 10,
            };

            // Act
            var response = await provider.SearchAsync(DocumentType, request);

            // Assert
            Assert.Equal(2, response.DocumentsCount);
            Assert.Equal(1, response.Aggregations?.Count);

            Assert.Equal(2, GetAggregationValuesCount(response, "Color"));
            Assert.Equal(0, GetAggregationValueCount(response, "Color", "Red"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Black"));
            Assert.Equal(1, GetAggregationValueCount(response, "Color", "Blue"));
            Assert.Equal(0, GetAggregationValueCount(response, "Color", "Silver"));
        }
    }
}
