# Changelog
All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## Unreleased

## 6.0.0-beta-001 - 2021-11-11

* Upgrade to Giraffe v6
* Change the runtime to `net6.0`

## 5.0.0 - 2021-05-28

### Added

* Release stable version

## 5.0.0-beta-001 - 2021-05-27

### Changed

* Upgrade to Thoth.Json.Net v5
* Upgrade to Giraffe v5
* Change the runtime to `net5.0`

## 4.3.0 - 2021-01-13

### Changed

* PR #17, Issue #16: Catch exception thrown by JsonTextReader when the request body is empty. To match with Thoth.Json.Net behaviour (by @BennieCopeland)

## 4.2.0 - 2020-06-09

* Fix Sequence serialization not writing a close `]` (by @ImaginaryDevelopment)

## 4.1.0 - 2020-05-16

### Fixed

* Avoiding aspnet core "Synchronous operations are disallowed" exception (by @MaxDeg)

## 4.0.0 - 2020-03-04

### Changed

* Update to Thoth.Json.Net v4

## 3.2.0

### Fixed

* Avoid "Synchronous operations are disallowed. Call WriteAsync or set AllowSynchronousIO to true." (by @MaxDeg)

## 3.1.0

### Changed

* Fix #7: Don't dispose the streams we didn't create ourself (by @MaxDeg)

## 3.0.0

### Changed

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
