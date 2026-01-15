# AI Coding Guidelines for SVS-SardineHead

## Project Overview
Unity mod for Japanese adult games (Aicomi, SamabakeScramble, DigitalCraft) providing runtime texture/material modification. Structure:
- Shared core: `SardineHead.cs` (MaterialWrapper), `Modifications.cs` (data models), `UserInterface.cs` (UI), `Custom.cs` (integration)
- Game-specific plugins: `AC/`, `DC/`, `SVS/` subdirs
- MSBuild build system with auto-deploy via `Tasks.xml`

## Architecture
- **MaterialWrapper**: Wraps Unity materials for type-safe shader property access (int/float/range/color/vector/texture)
- **Data Flow**: `CharaMods` → `CoordMods` → `Mods` (per-part) → `Modifications` (material changes)
- **UI**: Reactive edit windows in character creation, templates for property types
- **Textures**: SHA256-hashed, ZIP-persisted in `UserData/plugins/SardineHead/`

## Key Patterns
- **Functional**: Custom `F` class for composition (e.g., `F.Apply(func, arg) + action`)
- **Reactive**: `System.Reactive` for UI/lifecycle events
- **Conditional**: `#if Aicomi` for game-specific code
- **Extensions**: `item.Wrap()` for human parts (face/body/hair/clothes/accessories)
- **Harmony**: `[HarmonyPostfix, HarmonyWrapSafe]` for game hooks

## Build & Workflow
- **Build**: `dotnet build --configuration Debug/Release` (auto-clones deps, deploys to game)
- **Deps**: `Tasks.xml` git-clones `SVS-Fishbone`, `SVS-VarietyOfScales`
- **NuGet**: Custom sources `BepInExLibs`, `IllusionMods`
- **Test**: Run game → character creation → Ctrl+S (configurable) to show UI

## Code Conventions
- **Naming**: PascalCase classes/methods, camelCase locals (e.g., `MaterialWrapper`)
- **Error Handling**: `.Try()` for safe ops, log via `Plugin.Instance.Log.LogError`
- **Collections**: `Dictionary<string, T>` for properties (keys = shader names)
- **Async**: `UniTask` for Unity async
- **UI**: Chain `UGUI` calls (e.g., `UGUI.LayoutV() + UGUI.Text("Name")`)
- **Config**: `Json<T>.Load()` for files like `shaders.json`
- **Types**: `Float4` for colors/vectors, `Il2CppSystem.Threading` for threading

## Common Tasks
- **New Property**: Subclass `CommonEdit` in `UserInterface.cs`, add template, implement `Store()`/`Apply()`/`UpdateGet()`/`UpdateSet()`
- **Hook**: Add `[HarmonyPostfix]` in game-specific `.cs`, call via `Hooks.Initialize()`
- **Modify Material**: `wrapper.SetFloat(name, value); wrapper.Apply(mods)`
- **Texture**: `Textures.FromFile(path)` or `FromHash(hash)`, store in `Modifications.TextureHashes`

## Integration Points
- **CoastalSmell/Fishbone**: Character extensions, persistence
- **BepInEx**: IL2CPP plugin framework
- **IllusionLibs**: Game assemblies (`Character`, `CharacterCreation`)
- **VarietyOfScales**: Soft dep for scaling

## Debugging Tips
- **UI**: Check `Edit.isOn` - mods apply only when toggled
- **Materials**: `wrapper.GetShader()`, inspect `Properties` dict
- **Build**: Ensure deps cloned at `../../`
- **Runtime**: Log errors, check null `Renderer`/`Material`</content>
<parameter name="filePath">f:\Repositories\SVS-SardineHead\.github\copilot-instructions.md