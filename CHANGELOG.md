# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

### Added

## 3.0.0

* Remove setStatusCode and update comments (by @MaxDeg)

## 2.3.0

### Added

* Add `skipNullField` options to pass to `Encode.Auto`

## 2.2.0

### Fixed

* Fix encoding when passing array (see [#3](https://github.com/thoth-org/Thoth.Json.Giraffe/pull/3)) (by @johannesegger)

## 2.1.0

### Fixed

* Fix encoding when passing `None`

## 2.0.0

### Changed

* Release stable version

## 2.0.0-beta-001

### Added

* Add static helpers to deal with JSON directly for the request and response streams

### Changed

* Use Thoth.Json.Net 3.0.0-beta-001
* Some improvements made to write to streams when expected

## 1.1.0

### Changed

* Update for Giraffe 3.x

## 1.0.0

### Added

* Initial release
