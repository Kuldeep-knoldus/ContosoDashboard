<!--
Sync Impact Report
- Version change: unversioned scaffold -> 1.0.0
- Modified principles: all five scaffold placeholders replaced with project-specific
  principles for offline-first architecture, security, separation of concerns,
  testable SDD, and maintainability.
- Added sections: Project Constraints; Development Workflow and Quality Gates.
- Removed sections: none.
- Follow-up TODOs: RATIFICATION_DATE remains TODO because the original adoption date
  is not recorded in the repository.
-->

# ContosoDashboard Constitution

## Core Principles

### I. Offline-First, Abstraction-Ready Architecture
All features MUST run locally without cloud services or network-only dependencies.
Infrastructure integrations MUST be accessed through interfaces and dependency
injection so local implementations can be replaced without changing business logic.
Rationale: the repository is a training application that must remain available
offline while teaching a credible migration path to managed services.

### II. Defense-in-Depth Authorization
Every protected page MUST enforce authentication and authorization at the
presentation boundary, and every service that returns or mutates user-scoped data
MUST enforce authorization independently. Implementations MUST prevent IDOR by
validating ownership or permission for every supplied identifier. New security
behavior MUST include an explicit note that mock authentication is training-only.
Rationale: layered checks make security behavior visible and prevent a UI omission
from exposing data.

### III. Separation of Concerns and Explicit Contracts
UI pages, business services, data access, and domain models MUST remain in their
respective layers. Cross-layer behavior MUST use explicit models, interfaces, or
service contracts rather than reaching through another layer's implementation.
Features MUST reuse existing abstractions before introducing parallel mechanisms.
Rationale: clear boundaries are a primary learning objective and preserve the
offline-to-cloud migration path.

### IV. Specification- and Test-Driven Change
Every non-trivial change MUST begin with a written specification that states user
value, acceptance criteria, and authorization impact. Automated tests MUST cover
new or changed business rules, authorization boundaries, and regression-prone
contracts; integration tests MUST cover changed persistence or cross-layer
behavior. A change is not complete until its acceptance criteria and relevant
tests pass.
Rationale: the repository teaches Spec-Driven Development and requires behavior
to be demonstrable rather than inferred from implementation details.

### V. Simplicity, Accessibility, and Maintainability
Implementations MUST choose the smallest design that satisfies the specification,
avoid speculative framework or cloud dependencies, and document deliberate
training simplifications. User-facing pages MUST preserve keyboard-accessible
interaction, meaningful labels, and responsive Bootstrap-based presentation.
Breaking changes MUST include migration notes in the specification or release
documentation.
Rationale: constrained, accessible examples are easier for learners to understand,
review, and safely extend.

## Project Constraints

ContosoDashboard targets ASP.NET Core 8.0 with Blazor Server, Entity Framework
Core, SQL Server LocalDB, Bootstrap 5.3, and Bootstrap Icons unless a specification
explicitly justifies a change. The mock cookie authentication and seeded users
MUST NOT be represented as production-ready identity. Production-oriented guidance
MUST call out the need for a real identity provider, password protection, MFA,
secure transport, audit logging, and applicable compliance controls.

The project is for training only. Changes MUST avoid introducing external service
requirements that prevent offline setup. Secrets, real personal data, and
production credentials MUST NOT be committed.

## Development Workflow and Quality Gates

Each feature MUST have a Spec Kit specification before implementation and MUST
record acceptance criteria, affected layers, data changes, and security
considerations. Plans and tasks MUST trace back to those criteria.

Before review, contributors MUST run the applicable build, unit tests, integration
tests, and analyzers. Reviewers MUST verify layer boundaries, authorization at
both page and service levels, IDOR protection, accessibility of changed UI, and
that training-only limitations remain documented. A failed required quality gate
blocks merge unless the exception and follow-up task are documented.

## Governance

This constitution supersedes conflicting project practices. Amendments MUST be
made through the constitution workflow, include a Sync Impact Report, explain
their compatibility impact, and update the version and amendment date. Changes
to principles or mandatory constraints require maintainer approval; clarifications
may be approved through normal review.

Versioning follows semantic versioning: MAJOR for incompatible principle or
governance changes, MINOR for new or materially expanded principles or sections,
and PATCH for clarifications, wording, or typo fixes. Every pull request that
changes behavior MUST include a compliance review against the applicable
principles. Reviewers MUST reject undocumented exceptions and MUST verify that
new specifications, plans, tasks, and tests remain consistent with this document.

**Version**: 1.0.0 | **Ratified**: TODO(RATIFICATION_DATE): original adoption date is not recorded | **Last Amended**: 2026-09-10
