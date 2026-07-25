# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/).
This project is deployed continuously (no version tags), so entries are grouped
by date rather than by release version.

## [Unreleased]

## 2026-07-25

### Added

- Initial `CHANGELOG.md` to track notable changes going forward.

### Baseline

Feature set at the time this changelog was introduced (not individually
dated — see git history for prior detail):

- Recipe management (create, edit, organize with ingredient sections and instructions)
- Recipe import from URL via Spoonacular, with AI-assisted ingredient parsing
- Weekly meal planning with ingredient aggregation and unit conversion
- Kroger integration: product search, UPC linking, cart add via OAuth
- Shopping list with aisle sorting, price lookup, and Kroger store detection
- PWA support with service worker caching
- OpenTelemetry traces/metrics/logs exported to Grafana Cloud
