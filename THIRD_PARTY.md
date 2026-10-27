# THIRD_PARTY.md - external code, data and assets

Everything else in this repository was written during the hackathon (Oct 27 - Nov 4, 2026).
Runtime dependencies are added only as listed below, at the time they are introduced.

| Package / Component | Version | License | Used for | Added |
|---|---|---|---|---|
| .NET 9 SDK | 9.0.304 | MIT | platform | Oct 27 (scaffold) |
| xunit + Microsoft.NET.Test.Sdk | (template) | Apache-2.0 / MIT | tests | Oct 27 (scaffold) |
| OpenCvSharp4 | 4.13.0.20260627 | Apache-2.0 | webcam capture, resize, grayscale | Oct 27 (senses) |
| OpenCvSharp4.runtime.win | 4.13.0.20260627 | Apache-2.0 | native OpenCV binaries for Windows | Oct 27 (senses) |
| NAudio | 3.3.0 | MIT | microphone capture (16 kHz mono PCM) | Oct 27 (senses) |
| System.Speech | 10.0.14 | MIT | local text-to-speech (Windows SAPI) | Oct 27 (host) |

Transitive dependencies (System.Memory, System.Numerics.Tensors, NAudio sub-packages) come with
the above and carry the same licenses. Planned later: OpenAI .NET SDK (MIT) for the Meta model
clients, System.Speech (MIT) for local text-to-speech.

External services (configuration, not code): Meta Model API (dev.meta.ai), Baseten, Ollama
(serves locally hosted open-weight models; MIT).
