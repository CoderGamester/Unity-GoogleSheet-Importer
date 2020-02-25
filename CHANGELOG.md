# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.4.0] - 2020-01-20

- Added the possibility to parse CsV pairs (ex: 1:2,2<3,3>4) to dictionaries and value pair types
- Improved the parsing performance

**Changed**:

- Now generic types have their values properly parsed to their value instead of paring always to string. This will allow to avoid unnecessary later conversion on the importers.

## [0.3.0] - 2020-01-20

- Updated the package *com.gamelovers.configscontainer* to version 0.2.0
- Improved the *CsvParser* to include special characters like money symbols and dot
- Added easy selection of the *GoogleSheetImporter.asset* file. Just go to *Tools > Select GoogleSheetImporter.asset*. If the *GoogleSheetImporter.asset* does not exist, it will create a new one in the Assets folder

## [0.2.1] - 2020-01-15

- Removed Debug.Log lines

## [0.2.0] - 2020-01-15

- Added *ParseIgnoreAttribute* to allow fields to be ignored during deserialization
- Improved tests with [ParseIgnore] attribute test

**Changed**:
- Moved CsvParser to the runtime assembly to be able to use during a project runtime

## [0.1.2] - 2020-01-08

- Added missing meta files

## [0.1.1] - 2020-01-08

- Added *UnityWebRequestAwaiter* to remove the dependency of the AsyncAwait Package
- Added *GameIdsImporter* example

## [0.1.0] - 2020-01-06

- Initial submission for package distribution
