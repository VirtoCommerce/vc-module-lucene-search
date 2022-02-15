# Virto Commerce Lucene Search Module

[![CI status](https://github.com/VirtoCommerce/vc-module-lucene-search/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-lucene-search/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-lucene-search&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-lucene-search) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-lucene-search&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-lucene-search) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-lucene-search&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-lucene-search) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-lucene-search&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-lucene-search)

VirtoCommerce.LuceneSearch module implements ISearchProvider defined in the VirtoCommerce Search module and uses Lucene search engine which stores indexed documents in a local file system.

## Configuration
Azure Search provider are configurable by these configuration keys:

* **Search.Provider** is the name of the search provider and must be **Lucene**
* **Search.Lucene.Path** is a virtual or physical path to the root directory where indexed documents are stored.
* **Search.Scope** is a common name (prefix) of all indexes. Each document type is stored in a separate index. Full index name is `scope-{documenttype}`. One search service can serve multiple indexes.

[Read more about configuration here](https://virtocommerce.com/docs/user-guide/configuration-settings/)

## Documentation

* [Search Fundamentals](https://virtocommerce.com/docs/fundamentals/search/)

## References

* Deploy: https://virtocommerce.com/docs/latest/developer-guide/deploy-module-from-source-code/
* Installation: https://www.virtocommerce.com/docs/latest/user-guide/modules/
* Home: https://virtocommerce.com
* Community: https://www.virtocommerce.org
* [Download Latest Release](https://github.com/VirtoCommerce/vc-module-catalog/releases/latest)

## License

Copyright (c) Virto Solutions LTD.  All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
