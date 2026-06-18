# AGENTS.md

## Scope and current AI rules
- No existing repo-level AI rule files were found besides `README.md` and vendor docs in `Assets/Plugins/Dreamteck/Utilities/README.md`.
- Treat `Assets/Plugins/**`, `Assets/Ciconia Studio/**`, and most of `Assets/MathMesh/**` as third-party or shared framework code; prefer changes in `Assets/Scripts/**` unless integration fixes require otherwise.

## Project big picture (Unity 6)
- Unity version is `6000.0.24f1` (`ProjectSettings/ProjectVersion.txt`); only enabled build scene is `Assets/Scenes/SampleScene.unity` (`ProjectSettings/EditorBuildSettings.asset`).
- Core domain is the Bestvina-Handel workflow on fibred graphs/surfaces:
  - `Assets/Scripts/FibredSurfaces/*`: graph + algorithm state (`FibredSurface` partial class, in-place mutations).
  - `Assets/Scripts/GeometricObjects_Abstract/*`: geometric/domain primitives (`Curve`, `Point`, `GeodesicSurface`, `Homeomorphism`, etc.).
  - `Assets/Scripts/Surfaces_Explicit/SurfaceGenerator.cs`: constructs `AbstractSurface` + optional `FibredSurface` from user parameters.
- UI orchestration path:
  - `Assets/Scripts/UIElements/StartButton.cs` -> `MainMenu.Initialize*()`.
  - `MainMenu` creates surfaces (`SurfaceGenerator.CreateSurface`) and initializes `SurfaceMenu` windows/cameras.
  - `FibredSurfaceMenu` runs/branches algorithm suggestions and updates graph maps.

## Data flow and service boundaries
- `SurfaceGenerator.CreateSurface(...)` returns `(AbstractSurface, FibredSurface)`; `AbstractSurface` is the visualization/mapping container, `FibredSurface` is algorithm state.
- `SurfaceMenu.Display(...)` is the central propagation point: it draws one object and maps it across drawing surfaces/menus via `Homeomorphism`.
- `FibredSurfaceMenu` stores history as a directed graph (`AdjacencyGraph<MenuVertex, MenuEdge>`), not a linear undo stack.
- Algorithm execution is suggestion-driven:
  - `FibredSurface.NextSuggestion()` computes next UI action.
  - `FibredSurface.ApplySuggestion(...)` mutates graph/map and runs integrity checks.

## Critical implementation patterns
- `FibredSurface` is split across many `partial` files; check neighboring partials before editing behavior.
- Most algorithm methods are explicitly **in-place** (`FibredSurface.cs` summary comment). Use `FibredSurface.Copy()` before branching or speculative transforms.
- Graph map text parsing is permissive (`FibredSurfaceChangeMap.cs`): supports `g(a)=...`, `a -> ...`, `a ↦ ...`, `a := ...`; supports conjugation (`^`, `°`) and named edge-path definitions.
- Edge orientation is semantic: reversed strips are represented by `ReverseStrip`, and names/inverses are case/suffix sensitive in parsers (`EdgePath.FromString`).
- Error handling often uses `OnError`/`HandleInconsistentBehavior` + UI messages (not always exceptions). Preserve this flow when adding validations.

## Dependencies and integration points
- Unity packages: `com.unity.splines`, `com.unity.ugui`, input system support (`Packages/manifest.json`, project defines include both new and legacy input).
- NuGet-in-Assets dependencies: `QuikGraph 2.5.0`, `MathNet.Numerics 5.0.0` (`Assets/packages.config`, `Assets/Packages/*`).
- Dreamteck and MathMesh are integrated via project references and runtime scripts; avoid refactors there without regression checks.

## Developer workflows (repo-specific)
- Open project in Unity and start from `SampleScene`; startup UX is driven by `StartButton`/`MainMenu` example presets.
- No automated tests are present (`**/*Test*.cs` returned none). Validate changes via Play Mode flows:
  - initialize example,
  - inspect `FibredSurfaceMenu` suggestions,
  - run stepwise/auto algorithm and verify graph-map text updates.
- `Assembly-CSharp.csproj` is Unity-generated; do not hand-edit project references/compile includes there.

## Safe change strategy for agents
- Prefer minimal, localized edits in `Assets/Scripts/UIElements/*` for interaction issues and `Assets/Scripts/FibredSurfaces/*` for algorithm logic.
- When changing algorithm steps, verify both:
  - `FibredSurface.GraphString()`/map output in UI,
  - no new `OnError` messages during `StartAlgorithm()` or manual stepping.
- For map-related features, add/adjust parsing through `FibredSurfaceChangeMap.cs` and canonical path behavior in `EdgePath.cs` together, not in only one place.

