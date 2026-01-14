# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.7.2] - 2026-01-14

**Changed**:
- Updated dependency `com.gamelovers.dataextensions` and `com.gamelovers.configsprovider` to `com.gamelovers.gamedata`
- Updated assembly definitions to reference `GameLovers.GameData`

## [0.7.1] - 2023-08-03

**New**:
- Added new dependency on `com.gamelovers.configsprovider` package (version 0.1.0)
- Added author and license information to package.json

**Changed**:
- Updated `com.gamelovers.dataextensions` dependency to version 0.4.0
- Moved `ConfigsProvider`, `IConfigsProvider`, and `IConfigsAdder` to the new `com.gamelovers.configsprovider` package
- Moved `IConfigsContainer`, `ISingleConfigContainer`, `IPairConfigsContainer`, and `IConfig` interfaces to the `com.gamelovers.configsprovider` package
- Updated minimum Unity version to 2021.3

## [0.7.0] - 2023-07-28

**New**:
- Added `GoogleSheetScriptableObjectImportContainer` base class for improved code reuse in importers
- Added `IScriptableObjectImporter` and `IGoogleSheetConfigsImporter` interfaces
- Added `GoogleSheetSingleConfigSubListImporter` to support importing lists within single configs
- Added `OnImportComplete` virtual method for post-import processing in all importers
- Added support for custom deserializers in `CsvParser` methods
- Added `IGNORE_COLUMN_CHAR` ("$"), `SUB_LIST_SUFFIX` constant ("[]") and `IGNORE_FIELD_CHAR` ("#") constants for CSV parsing control
- Added support for `UnitySerializedDictionary` type deserialization
- Added OpenAI code reviewer GitHub Action for automated PR reviews
- Refactored `CsvParser` with improved array/dictionary parsing and custom deserializer support
- Improved CSV parsing to handle missing columns gracefully instead of throwing exceptions

**Changed**:
- Renamed `IConfigsProvider.GetSingleConfig<T>()` to `GetConfig<T>()` with improved error handling
- Removed `IGoogleSheetImporter` interface (replaced with `IGoogleSheetConfigsImporter`)

## [0.6.2] - 2020-09-24

**New**:
- Added Importers Example

## [0.6.1] - 2020-08-18

**New**:
- Added *IConfigsAdder* interface to separate the contract behaviour when an object wants to add configs without needing to rely on the *ConfigsProvider*

## [0.6.0] - 2020-07-28

**New**:
- Added *ConfigsProvider* to provide the loaded google sheet configs
- Added *IConfigsContainer* to provide the interface to maintain the loaded google sheet configs

**Changed**:
- Removed the dependency to the *com.gamelovers.configscontainer* & *com.gamelovers.asyncawait* package

## [0.5.3] - 2020-04-25

**Changed**:
- Updated the package *com.gamelovers.configscontainer* to version 0.7.0
- Updated *GoogleSheetImporter* documentation

## [0.5.2] - 2020-04-06

**Changed**:
- Updated to use the package *com.gamelovers.asyncawait* to version 0.2.0
- Removed UnityWebRequestAwaiter out of the package

## [0.5.1] - 2020-03-07

**New**:
- Added package dependency to *com.gamelovers.configscontainer* version 1.1.2

**Changed**:
- Updated the package *com.gamelovers.configscontainer* to version 0.5.0

## [0.5.0] - 2020-02-26

**New**:
- Added the *GoogleSheetSingleConfigImporter* to import single unique configs
- Improved the parsing to allow to parse by giving the type as a parameter

**Changed**:
- Updated the package *com.gamelovers.configscontainer* to version 0.4.0

**Fixed**:
- Fixed bug where KeyValuePair types values were not string trimmed

## [0.4.0] - 2020-02-25

**New**:
- Added the possibility to parse CsV pairs (ex: 1:2,2<3,3>4) to dictionaries and value pair types
- Improved the parsing performance

**Changed**:
- Now generic types have their values properly parsed to their value instead of paring always to string. This will allow to avoid unnecessary later conversion on the importers.
- Updated the package *com.gamelovers.configscontainer* to version 0.3.0

## [0.3.0] - 2020-01-20

**New**:
- Improved the *CsvParser* to include special characters like money symbols and dot
- Added easy selection of the *GoogleSheetImporter.asset* file. Just go to *Tools > Select GoogleSheetImporter.asset*. If the *GoogleSheetImporter.asset* does not exist, it will create a new one in the Assets folder

**Changed**:
- Updated the package *com.gamelovers.configscontainer* to version 0.2.0

## [0.2.1] - 2020-01-15

**Changed**:
- Removed Debug.Log lines

## [0.2.0] - 2020-01-15

**New**:
- Added *ParseIgnoreAttribute* to allow fields to be ignored during deserialization
- Improved tests with [ParseIgnore] attribute test

**Changed**:
- Moved CsvParser to the runtime assembly to be able to use during a project runtime

## [0.1.2] - 2020-01-08

**New**:
- Added missing meta files

## [0.1.1] - 2020-01-08

**New**:
- Added *UnityWebRequestAwaiter* to remove the dependency of the AsyncAwait Package
- Added *GameIdsImporter* example

## [0.1.0] - 2020-01-06

- Initial submission for package distribution
