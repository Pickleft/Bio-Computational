# THIRD_PARTY.md — external code, data and assets

Everything else in this repository was written during the hackathon (Oct 27 – Nov 4, 2026).
Runtime dependencies are added only as listed below, at the time they are introduced.

| Package / Component | Version | License | Used for | Added |
|---|---|---|---|---|
| .NET 9 SDK | 9.0.304 | MIT | platform | D1 (scaffold) |
| xunit + Microsoft.NET.Test.Sdk | (template) | Apache-2.0 / MIT | tests | D1 (scaffold) |

Planned (added here when installed — see docs/D0/RUNBOOK.md): OpenAI .NET SDK (MIT),
OpenCvSharp4 (Apache-2.0), NAudio (MIT), System.Speech (MIT). Optional: BrainFlow (verify license
at add time).

External services (configuration, not code): Meta Model API (dev.meta.ai), Baseten, Ollama
(serves locally hosted open-weight models; MIT).
