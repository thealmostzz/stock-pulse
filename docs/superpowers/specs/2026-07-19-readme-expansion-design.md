# README Expansion Design

**Date:** 2026-07-19  
**Scope:** Expand the repository root README into bilingual onboarding and product documentation.

## Goal

Make the repository understandable and runnable for both prospective users/investors and developers. The README must distinguish implemented functionality from the product roadmap and must not claim that unimplemented news-provider ingestion or dashboard functionality exists.

## Audience

- **Users and investors:** understand StockPulse's purpose, intended real-time news workflow, current MVP status, and the investment disclaimer.
- **Developers:** install prerequisites, start each local service, locate the implemented API and realtime endpoints, run tests, and navigate the repository.

## Content Design

Every primary section uses an English heading followed by its Thai translation in the same heading. Within each section, English copy appears first and the equivalent Thai copy immediately follows it.

The README will include:

1. Product summary and a concise MVP-status callout.
2. Current capabilities and a separate roadmap, based only on verified repository state.
3. Technology stack and a high-level local architecture diagram.
4. Prerequisites and local-start commands for PostgreSQL, API, and Angular frontend.
5. The verified REST endpoints, the SignalR hub route, and short request examples.
6. Test commands and a top-level project-structure guide.
7. Development notes covering local-only credentials, CORS origin, and the absence of production configuration.
8. A non-investment-advice disclaimer and contribution guidance.

## Accuracy Rules

- Document .NET 10, Angular 18, PostgreSQL 16, Docker Compose, EF Core, and SignalR only where supported by project configuration.
- Describe the frontend as an Angular scaffold until the dashboard is implemented.
- Describe provider ingestion, alerting, and production deployment as planned work, not available features.
- Never include local passwords, API keys, or secrets beyond the explicitly committed local Docker development configuration.

## Verification

After writing the README, validate that all referenced files and commands exist, inspect the Markdown diff, and run a lightweight Markdown consistency check. No runtime code changes or service startup are required because the scope is documentation only.
