# Contributing to BuildGuard

This is the onboarding note for someone opening the repo on their second day. Read `README.md` for why the project exists and how CI is wired. This file is the map of the code and the rules for changing it.

## First hour

1. Install Unity **6000.3.29f1** plus **Linux Build Support (IL2CPP)**. The version is in `ProjectSettings/ProjectVersion.txt`. Do not let Hub open the project in a different patch. A different editor rewrites project settings and makes the CI image pin a lie.
2. Open the project. If Unity upgrades settings or writes `Packages/packages-lock.json`, commit that in its own commit so reviewers can see it.
3. Play `Assets/BuildGuard/Scenes/TrainingScenario.unity`. You should get a welfare-check prompt and four-or-fewer choices. The cube and the floor are Unity primitives. There is no art pipeline on purpose.
4. Run EditMode, then PlayMode, from **Window → General → Test Runner**. Both should pass before you change rules.
5. **BuildGuard → Validate Project**. The console should report zero errors. Warnings are allowed; this project has none.

You do not need the GitHub secrets to work locally. You need them before a push to a GitHub remote will go green. The secret setup is in the README under "GitHub secrets".

## How the code is allowed to depend on things

```text
BuildGuard.Runtime  ->  UnityEngine, UnityEngine.UI
BuildGuard.Editor   ->  UnityEditor, BuildGuard.Editor.Validation (same assembly)
EditMode tests      ->  Runtime, Editor, Unity Test Framework
PlayMode tests      ->  Runtime, Unity Test Framework
```

`BuildGuard.Editor` does not reference `BuildGuard.Runtime`. The validator does not know the scenario exists. That is deliberate: a content rule and a build rule change for different reasons.

Inside `Runtime`, only `TrainingScenarioView` is a `MonoBehaviour`. `ScenarioEngine` and the types it uses have no `using UnityEngine`. If you need a new rule, put it on the engine or the definition and test it in EditMode. Put a new button or label in the view.

Inside `Editor`, only `BuildValidationRunner`, `BuildValidatorWindow`, and `BuildScript` call UnityEditor APIs. `Evaluate` on a check takes a `ValidationContext` and returns issues. If you query `AssetDatabase` from a check, the EditMode test cannot construct the failure case without breaking a real asset.

## File by file

### Runtime

| File | What it is |
| --- | --- |
| `Runtime/BuildGuard.Runtime.asmdef` | Assembly boundary. References `UnityEngine.UI` because the view uses uGUI `Text`. |
| `Runtime/Scenario/ScenarioModel.cs` | `ScenarioChoice`, `ScenarioStep`, `DecisionRecord`, `ScenarioResult`, and the two enums. Choices with a null `nextStepId` end the scenario. An empty string is rejected so "end" cannot be a typo. |
| `Runtime/Scenario/ScenarioDefinition.cs` | Validates the graph once, at creation. Duplicate ids, a missing start step, and a dangling next step throw. The engine trusts the graph after that. |
| `Runtime/Scenario/ScenarioEngine.cs` | `IScenarioEngine`, the immutable `ScenarioSession`, and `AdvanceResult`. A rejected choice returns the same session instance. A null session throws, because that is a programmer error. An unknown choice id does not. |
| `Runtime/Training/CommunicationScenario.cs` | The welfare-check content: four steps, score deltas, and the thresholds (exemplary 12, satisfactory 6). The engine tests that lock the rules use small fixtures, not this text. One test walks the sample so the scene content cannot silently stop being exemplary. |
| `Runtime/Training/TrainingScenarioView.cs` | Builds a screen-space canvas, a prompt, and one button per choice. `SelectChoice` is public so PlayMode tests do not click generated buttons. |

### Editor

| File | What it is |
| --- | --- |
| `Editor/BuildGuard.Editor.asmdef` | Editor-only assembly. This is what makes `BuildScript` legal for GameCI's `buildMethod` input. |
| `Editor/Validation/ValidationModel.cs` | Issues, settings, and the snapshot types (`ScriptScan`, `TextureScan`, `ValidationContext`). |
| `Editor/Validation/BuildChecks.cs` | `IBuildCheck` plus the three built-in checks. |
| `Editor/Validation/BuildValidator.cs` | Runs the list, counts severities, writes the JSON report, and holds `BuildCheckCatalog`. |
| `Editor/BuildValidationRunner.cs` | The Unity adapter. Loads settings, scans prefabs and enabled scenes, writes `BuildReports/build-validation.json` under the project root (`Application.dataPath`, not the process working directory). Also the `BuildValidationSettingsAsset` ScriptableObject. |
| `Editor/BuildValidatorWindow.cs` | IMGUI window and the **BuildGuard** menu. IMGUI avoids a UI Toolkit dependency for a tool this small. |
| `Editor/BuildScript.cs` | `BuildLinux64`. Validator first. Non-zero exit in batchmode on failure. Reads `-customBuildPath`, then the `BUILD_PATH` environment variable GameCI sets, then `build/StandaloneLinux64/BuildGuard`. |

### Tests

| File | What it is |
| --- | --- |
| `Tests/EditMode/BuildGuard.Tests.EditMode.asmdef` | Editor platform, references Runtime and Editor, NUnit from the Test Framework. `UNITY_INCLUDE_TESTS` keeps it out of the player. |
| `Tests/EditMode/ScenarioDefinitionTests.cs` | Broken graphs are rejected at creation. |
| `Tests/EditMode/ScenarioEngineTests.cs` | Score bands, rejected input, loops, negative scores, immutability, and the sample scenario. `ScenarioFixtures` at the bottom is the tiny graph the rule tests share. |
| `Tests/EditMode/BuildValidatorTests.cs` | Each check against a hand-built context, including the JSON shape. |
| `Tests/EditMode/ProjectValidationTests.cs` | Runs the real runner on this project and expects zero errors. This is the one EditMode test that needs the editor. |
| `Tests/PlayMode/BuildGuard.Tests.PlayMode.asmdef` | References Runtime only. It must not reference the Editor assembly, or the player test build fails. |
| `Tests/PlayMode/TrainingScenarioPlayModeTests.cs` | Loads `TrainingScenario`. The second test plays Announce → Acknowledge → Scope → Summarize and expects exemplary, score 16. |

### CI and dashboard

| File | What it is |
| --- | --- |
| `.github/workflows/ci.yml` | Test job, build job, metrics job. Secrets, image pin, Library cache, artifacts, Pages. |
| `ci/collect_metrics.py` | Stdlib only. Sums `test-run` XML, reads the validator and the player report, appends one object. Missing files stay null. |
| `ci/test_collect_metrics.py` | Three tests: counts add up, a skipped build is not "zero warnings", a re-run with the same run id does not duplicate. |
| `docs/dashboard/index.html` | The page. |
| `docs/dashboard/dashboard.css` | Layout. One column on a narrow screen. |
| `docs/dashboard/dashboard.js` | Renders `history.json`. The preview button fabricates a chart in memory and never writes the file. |
| `docs/dashboard/history.json` | `{ "schemaVersion": 1, "runs": [] }` until CI appends. The collector keeps the newest 100 runs. |

### Project plumbing

| File | What it is |
| --- | --- |
| `Packages/manifest.json` | uGUI, Test Framework 1.4.6, Code Coverage 1.3.0, and the engine modules the scene actually imports. |
| `ProjectSettings/ProjectVersion.txt` | `6000.3.29f1 (96d2fe17529c)`. |
| `ProjectSettings/ProjectSettings.asset` | Product name, the Linux bundle id, and IL2CPP for Standalone. |
| `ProjectSettings/EditorBuildSettings.asset` | `TrainingScenario` enabled. The guid matches the scene `.meta`. |
| `ProjectSettings/*.asset` | The rest of the manager files Unity expects so a batch import does not have to invent them. |
| `.gitignore` | `Library/`, `BuildReports/`, `artifacts/`, `CodeCoverage/`, generated IDE files. `.meta` files are not ignored. |
| `LICENSE` | MIT. |

Every asset under `Assets/` has a `.meta` file. Unity matches scripts to the scene by the guid in that meta. If you regenerate a meta, the scene's `m_Script` guid has to be updated to the same value or the view shows as a missing script, and the missing-script check will then fail the build. That is the check doing its job.

## Changing the scenario

Edit `CommunicationScenario`. Keep choice ids stable: the PlayMode test uses `CommunicationScenario.Choices`. If you change a score, update the sample tests in `ScenarioEngineTests` in the same commit and say why the threshold moved. Do not teach the engine about welfare checks. New content is a new `ScenarioDefinition`, not a new branch in `ScenarioEngine`.

A session is a snapshot. Do not add a setter for the score. If a caller needs a different score, it is a new session returned by `Choose`.

## Changing a build check

Follow the four steps in the README section "Add a validator check". Add the test first if you already know the case that should fail. A check that only passes on this repo, with no snapshot test for the failure, will not catch the next project that copies it.

Severity is a product decision. Write it down in the test name. `Texture_WiderThanBudget_IsAWarning` is the spec.

## Tests you should run before a pull request

- EditMode in the Test Runner.
- PlayMode in the Test Runner.
- `python3 -m unittest test_collect_metrics.py` from `ci/` if you touched `collect_metrics.py` or the dashboard's assumptions about the JSON.
- **BuildGuard → Validate Project** if you touched scenes, prefabs, or the runner.

You cannot run the GameCI workflow locally without the secrets and a Linux editor image. The pull request is where that runs. Read the failing job using the README section "How to investigate a failing run" before you re-run the whole pipeline. A re-run costs the cold image pull when the cache misses.

## Pull request shape

- One change of behavior per pull request. A new check does not need to ride along with a scenario copy edit.
- Do not commit `Library/`, `BuildReports/`, `UserSettings/`, or coverage output.
- Do not commit a `history.json` you edited by hand to make the chart look populated. CI owns that file. The empty `runs` array is the honest starting point.
- If you change `ProjectVersion.txt`, change `unityVersion` in `ci.yml` to the same string, and confirm Docker Hub has `ubuntu-<version>-linux-il2cpp-4` and `ubuntu-<version>-base-4` before you push. The actions default to image generation 3, which this editor version does not have.

## CI permissions

The metrics job pushes a commit as `github-actions[bot]`. That needs Actions workflow permissions set to read and write, and it needs `main` to accept a push from the GitHub Actions token. If branch protection requires a pull request for every commit, the metrics push is rejected and the chart stops. For this repo, allow the Actions token to update `docs/dashboard/history.json`, or turn that protection off until you have a bot exemption. The workflow ignores that path so the push does not loop.
