# Dual Face Recognition Engines Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Keep a compatible YuNet + SFace default while adding a separately configured and licensed user-supplied ArcFace R100 ONNX engine.

**Architecture:** Introduce an engine-neutral contract with immutable model identity, runtime profile, availability, batching, and cancellation semantics. Keep each engine's embeddings in a versioned storage namespace so legacy YuNet/SFace data can migrate in place while ArcFace never reads it.

**Tech Stack:** .NET 8, C# 12, WPF, Microsoft.ML.OnnxRuntime, OpenCvSharp, xUnit, FluentAssertions.

## Global Constraints

- Do not download or bundle ArcFace or InsightFace weights.
- ArcFace requires user-supplied detector and recognizer paths plus an explicit license declaration.
- Use reusable ONNX Runtime sessions, bounded concurrency, bounded batches, cancellation, five-point alignment, L2 normalization, and cosine similarity.
- Do not commit or push.

---

### Task 1: Engine policy and isolated persistence

**Files:**
- Create: `src/HanabePhotoManager.App/Services/FaceRecognitionModels.cs`
- Modify: `src/HanabePhotoManager.App/Services/PeopleAlbumService.cs`
- Test: `tests/HanabePhotoManager.App.Tests/FaceRecognitionPolicyTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/PeopleAlbumServiceTests.cs`

- [ ] Write failing tests for ArcFace license/model availability, distinct model identities, legacy YuNet migration, and cross-engine rejection.
- [ ] Run focused tests and confirm failures describe missing types/behavior.
- [ ] Implement engine descriptors and versioned snapshot metadata.
- [ ] Run focused tests and confirm pass.

### Task 2: Reusable ONNX engines and batching

**Files:**
- Create: `src/HanabePhotoManager.App/Services/OnnxFaceRecognitionEngine.cs`
- Create: `src/HanabePhotoManager.App/Services/FaceImageProcessor.cs`
- Modify: `src/HanabePhotoManager.App/Services/LocalFaceEmbeddingService.cs`
- Modify: `src/HanabePhotoManager.App/HanabePhotoManager.App.csproj`
- Test: `tests/HanabePhotoManager.App.Tests/FaceRecognitionMathTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/FaceRecognitionPerformanceTests.cs`

- [ ] Write failing tests for five-point transforms, L2 normalization, cosine matching, bounded parallelism, batching, session reuse, and cancellation.
- [ ] Run focused tests and confirm expected failures.
- [ ] Implement YuNet/SFace and ArcFace R100 pipelines with shared session lifecycle.
- [ ] Run focused tests and confirm pass.

### Task 3: Settings, composition, and migration behavior

**Files:**
- Modify: `src/HanabePhotoManager.App/Services/AppSettingsStore.cs`
- Create: `src/HanabePhotoManager.App/Services/FaceRecognitionEngineFactory.cs`
- Modify: `src/HanabePhotoManager.App/ViewModels/MainWindowViewModel.cs`
- Modify: `src/HanabePhotoManager.App/SettingsCenterPage.xaml`
- Test: `tests/HanabePhotoManager.App.Tests/AppSettingsStoreTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/FaceRecognitionEngineFactoryTests.cs`
- Test: `tests/HanabePhotoManager.App.Tests/ControlThemeTests.cs`

- [ ] Write failing tests for defaults, settings persistence, ArcFace disabled reasons, profiles, and factory selection.
- [ ] Run focused tests and confirm expected failures.
- [ ] Add settings bindings and compose the selected engine without changing existing YuNet defaults.
- [ ] Run focused tests and confirm pass.

### Task 4: Documentation and full verification

**Files:**
- Modify: `docs/architecture.md`
- Create: `src/HanabePhotoManager.App/Models/Face/ARCFACE-MODEL-NOTICE.md`

- [ ] Document engine isolation, supported model ownership, and model placement.
- [ ] Run Release restore, build, full tests, and focused performance tests.
- [ ] Inspect `git diff` and `git status` to confirm no unrelated files changed and no commit/push occurred.
