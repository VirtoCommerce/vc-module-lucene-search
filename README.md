# VirtoCommerce.LuceneSearch

[![CI status](https://github.com/VirtoCommerce/vc-module-lucene-search/workflows/Module%20CI/badge.svg?branch=dev)](https://github.com/VirtoCommerce/vc-module-lucene-search/actions?query=workflow%3A"Module+CI") [![Quality gate](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-lucene-search&metric=alert_status&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-lucene-search) [![Reliability rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-lucene-search&metric=reliability_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-lucene-search) [![Security rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-lucene-search&metric=security_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-lucene-search) [![Sqale rating](https://sonarcloud.io/api/project_badges/measure?project=VirtoCommerce_vc-module-lucene-search&metric=sqale_rating&branch=dev)](https://sonarcloud.io/dashboard?id=VirtoCommerce_vc-module-lucene-search)

VirtoCommerce.LuceneSearch module implements ISearchProvider defined in the VirtoCommerce.Core module and uses Lucene search engine which stores indexed documents in a local file system.

# Installation
Installing the module:
* Automatically: in VC Manager go to **Modules > Available**, select the **Lucene Search module** and click **Install**.
* Manually: download module ZIP package from https://github.com/VirtoCommerce/vc-module-lucene-search/releases. In VC Manager go to **Modules > Advanced**, upload module package and click **Install**.

# Configuration
## VirtoCommerce.Search.SearchConnectionString
The search configuration string is a text string consisting of name/value pairs seaprated by semicolon (;). Name and value are separated by equal sign (=).
For Lucene provider the configuration string must have three parameters:
```
provider=Lucene;server=~/App_Data/Lucene;scope=default
```
* **provider** should be **Lucene**
* **DataDirectoryPath** (or **server** for compatibility with VirtoCommerce.Search module which is now obsolete) is a virtual or physical path to the root directory where indexed documents are stored.
* **scope** is a common name for each index. In fact, this is the prefix for the name of a subdirectory inside the root directory which will contain indexed documents. So the directory structure will be like this: 
```
DataDirectoryPath
    scope-DocumentType1
    scope-DocumentType2
```

You can configure the search configuration string either in the VC Manager UI or in VC Manager web.config. Web.config has higher priority.
* VC Manager: **Settings > Search > General > Search configuration string**
* web.config: **connectionStrings > SearchConnectionString**:
```
<connectionStrings>
    <add name="SearchConnectionString" connectionString="provider=Lucene;server=~/App_Data/Lucene;scope=default" />
</connectionStrings>
```

# Known issues
* Sorting by geo distance (`GeoDistanceSortingField`) is not supported

# License
Copyright (c) Virtosoftware Ltd. All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
