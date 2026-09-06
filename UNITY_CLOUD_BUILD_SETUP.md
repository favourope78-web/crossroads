# CROSSROADS — Unity Cloud (Build Automation) setup

Everything you need to build the Android APK on Unity Cloud, field by field. Values marked
**COPY** are exact values from this repository; values marked **YOU** are the only things you
must decide or create yourself.

Repository: `https://github.com/favourope78-web/crossroads` (public, branch `main`)
Unity version in the project: **6000.0.23f1** (`ProjectSettings/ProjectVersion.txt`)

---

## 0. Before you start — the 4 things only you can provide

| # | What | Why | Where you get it |
|---|------|-----|------------------|
| 1 | **Unity ID** (free) with a **Unity Cloud organization + project** | Build Automation runs inside a Unity Cloud project | https://cloud.unity.com → sign in → *Projects* → **Create project** (name: `CROSSROADS`) |
| 2 | **GitHub Personal Access Token (classic)**, scope **`public_repo`** (repo is public; use `repo` only if you make it private) | Build Automation authenticates to GitHub with a PAT | GitHub → *Settings → Developer settings → Personal access tokens → Tokens (classic) → Generate new token*. **Do not reuse the token you gave me** — create a new one just for Unity Cloud; you can revoke it independently. |
| 3 | **Android keystore** (`.keystore`/`.jks`) + keystore password + key alias + key password | A release APK must be signed with *your* key. For a first test build you can skip this and pick *Auto-generated debug keystore* | See §5 — one command, 2 minutes |
| 4 | **Build minutes** | Free tier includes Windows build minutes (currently 200 Windows "Micro" minutes on the free plan; check https://unity.com/products → DevOps). An Android IL2CPP build of this project will take roughly 15–30 minutes on a Micro Windows builder (estimate — the first cloud build tells you the real number) | Nothing to do unless you exceed the free minutes; then it is pay-as-you-go per minute |

---

## 1. Connect the repository (Source control)

Unity Cloud → your project → **DevOps → Build Automation → Configurations → Get started**
(or the **Settings → Source control** tab if you already see the Build Automation page)

| Field | Value |
|---|---|
| Source control provider | **GitHub** |
| Authentication | **Personal Access Token** → paste **YOU #2**, click *Authorize* |
| Repository | **`favourope78-web/crossroads`** **COPY** |
| Branch (default) | **`main`** **COPY** |
| Git LFS | leave **off** — the repo contains no LFS objects (the old LFS rules were removed in the release pass) |
| Save | ✔ |

If the repository does not appear in the dropdown, the token lacks `public_repo`.

---

## 2. Create the build target (Configurations → **Add new**)

Choose **Target setup** (not *Quick target setup* — you need Advanced settings).

### Basic Info

| Field | Value |
|---|---|
| Target name | **`Android-Release`** **COPY** (name is yours, this is what the docs below assume) |
| Platform | **Android** |
| Branch | **`main`** |
| Project subfolder path | **leave empty** — `Assets/` and `ProjectSettings/` are at the repository root |
| Unity version | tick **Auto detect Unity version** (reads `ProjectSettings/ProjectVersion.txt` → 6000.0.23f1). If auto-detect is not offered, pick **6000.0.23f1** manually — do **not** pick a different minor; the URP 17.0.3 / Input System 1.11.2 / Cinemachine 3.1.2 package set in `Packages/manifest.json` is pinned to 6000.0 |
| Builder Operating System and Version | **Windows 11 24H2** (the free minutes are Windows minutes; Android builds are supported on Windows) |
| Android SDK version | **SDK 35** (for 6000.0.18–6000.0.37 Unity Cloud offers 34/35/36; the project's `targetSdkVersion` is *highest installed*, and the docs require selecting the maximum available). SDK 34 is also fine; 36 works too |

### Builder Configuration

| Field | Value |
|---|---|
| Machine type | **Micro** (8 vCPU / 16 GB) — this project is 70 MB with one scene; Micro is the cheapest and the free minutes are Micro minutes. Use **Standard** only if the Micro build times out |

### Credentials

| Field | Value |
|---|---|
| Bundle ID | **`com.favourope78.crossroads`** **COPY** (must match `ProjectSettings.asset` → `applicationIdentifier.Android`) |
| Credentials set | First build: **Auto-generated debug keystore (for development only)**. Release: **Add new provisioning credentials (for release)** → fill from §5: Name `crossroads-release`, Keystore file (upload), Keystore password, Key alias, Key password |

### Scheduling

| Field | Value |
|---|---|
| Auto-build | **On** if you want a build on every push to `main` (costs minutes per push). **Off** to build manually from the dashboard (recommended while iterating) |
| Auto-cancel | On |
| Build schedule | none |

→ **Save** (or **Next / Advanced settings**, which you do need — see §3).

---

## 3. Advanced settings (same target → **Advanced settings**)

These are the fields that make the cloud build identical to the repo's own release configuration.

| Section | Field | Value |
|---|---|---|
| Build output | **Development build** | **Off** for `Android-Release`. (On + `PreExportDev` below for a profiler build — see §6) |
| Build output | Compression | default |
| Caching | Library caching | **On** (second build much faster) |
| Platform specific settings (Android) | Build app bundles (.aab) instead of an APK | **Off** for sideloading/testing (you get `Crossroads.apk`). **On** only when uploading to Google Play |
| Platform specific settings (Android) | Build asset packs | Off |
| Platform specific settings (Android) | Make split binary application builds | Off |
| Script hooks | **Pre-Export Method** | **`Crossroads.EditorTools.CloudBuildHooks.PreExportRelease`** **COPY** |
| Script hooks | **Post-Export Method** | **`Crossroads.EditorTools.CloudBuildHooks.PostExport`** **COPY** (optional; writes `crossroads-build-info.txt` next to the artifact) |
| Script hooks | Pre-Build Script / Post-Build Script | leave empty |
| Scripting Define Symbols | | leave empty. The code compiles under both `ENABLE_INPUT_SYSTEM` and `ENABLE_LEGACY_INPUT_MANAGER` (Player setting *Active Input Handling = Both*, set by the pre-export method) |
| Environment variables | *(optional)* `CROSSROADS_ARCH` = `arm64` | Drops the ARMv7 slice → roughly halves IL2CPP time and build minutes. Leave unset to ship ARM64 + ARMv7 as configured in the project |
| Tests | | Off (the project's test suite is the mono/`mcs` suite in `scripts/run_tests.sh`, not Unity Test Framework) |
| Scenes | | leave empty — the pre-export method pins **`Assets/Scenes/Prototype/FirstLocation.unity`** **COPY** (the only scene; also in `EditorBuildSettings.asset`). If you prefer to set it in the UI, use that exact path |
| Addressables / Asset bundles | | Off (not used) |

→ **Save**.

### What `PreExportRelease` does on the builder (so you don't have to)

`Assets/Editor/CloudBuildHooks.cs` → `AndroidDevBuild.Configure()`:

| Player setting | Value applied |
|---|---|
| Company / product | `favourope78-web` / `CROSSROADS` |
| Application identifier | `com.favourope78.crossroads` |
| Min SDK / Target SDK | API 24 / highest installed |
| Scripting backend / C++ config | IL2CPP / **Master**; managed stripping Medium; engine code stripping on |
| Architectures | ARM64 + ARMv7 (ARM64 only with `CROSSROADS_ARCH=arm64`) |
| Graphics APIs | Vulkan, then OpenGL ES 3 |
| Texture compression | ASTC |
| Orientation | landscape (left/right auto-rotate) |
| Input | Active Input Handling = Both |
| Quality default | Balanced tier (`ProjectSettings/QualitySettings.asset`) |
| Version | `versionCode` = Unity Cloud build number (`UCB_BUILD_NUMBER`) so every build installs over the previous one; `bundleVersion` = `0.1.<build>` |
| Scenes | `Assets/Scenes/Prototype/FirstLocation.unity` only |
| Development / profiler | off (`PreExportDev` turns both on) |

---

## 4. Run the first build

1. **Configurations → Android-Release → Build** (or *Build now*).
2. Expect ~15–30 min on Micro (first build compiles URP shaders + IL2CPP for two ABIs; later builds reuse the cached `Library`).
3. When it finishes: **Download** → `Crossroads.apk` (or `.aab`). The build page also lists `crossroads-build-info.txt` if the post-export hook ran.
4. Install: `adb install -r Crossroads.apk`, or copy the APK to the phone and open it (enable *Install unknown apps* for your file manager).

### Reading a failed build log (search the log for these, in this order)

| Log text | Meaning / fix |
|---|---|
| `Repository not found` / auth error at checkout | PAT scope (`public_repo`) or the token expired |
| `Unity version ... is not available` | Auto-detect picked 6000.0.23f1 and the builder pool lacks it — pick the nearest **6000.0.x** offered (e.g. 6000.0.2xf1–6000.0.5xf1). Then also **commit the re-serialised `ProjectSettings/ProjectVersion.txt`** Unity writes, or keep auto-detect off and select that version in the target |
| `error CS` (compiler error) | Copy the file:line into an issue — the runtime code compiles under both input configs here, but the real editor sees packages the stub doesn't (report it; don't change settings). *Build #1 hit exactly this*: `MapHUD.cs(41/131) 'Button' does not contain a definition for 'rectTransform'` — fixed in `f4f0294` (stub tightened so it reproduces locally) |
| `Unexpected scalar when reading mapping` / `Unexpected node type` while importing `ProjectSettings/*.asset` | A hand-written settings file used a scalar where Unity 6 serialises a per-platform dictionary. Not fatal, but it discards the file and forces a full Library rebuild every build. `ProjectSettings.asset` was regenerated from the real 6000.0.23f1 schema in `6cad2b3`; if it reappears, diff the file against a freshly saved one from the editor and copy key spellings from there |
| `Assembly ... has no scripts` / `empty assembly definition` warning | An `.asmdef` folder with no `.cs` files. `Crossroads.Input.asmdef` (input code lives in `Crossroads.Gameplay`) was removed in `6cad2b3` — warning only |
| `Asset ... has no meta file, generating one` / `.meta file ... does not exist` | Unity auto-creates metas with random GUIDs → churn between builds and, for scripts, broken scene references. All 21 missing metas were committed in `6cad2b3`; keep new assets' `.meta` files in git |
| `TimeoutOverflowWarning` (Package Manager) | Node/npm warning from Unity's package resolver on the builder — harmless, safe to ignore |
| `Failed to resolve packages` / `com.unity.cinemachine` fetch error | Package resolution needs registry access from the builder; retry. Cinemachine is listed in `Packages/manifest.json` but not used by any script — remove the line if it ever blocks resolution |
| `Pre-export method ... not found` | Field must be exactly `Crossroads.EditorTools.CloudBuildHooks.PreExportRelease` (namespace + class + method, no parentheses) |
| `Shader error in 'Universal Render Pipeline/Lit'` | Transient on first import; retry the build once |
| `IL2CPP error` / `clang++ ... exited with code 1` | Almost always the machine ran out of memory/time on Micro → switch Builder Configuration to **Standard** |
| `Failed to sign` / `keystore was tampered with` | Wrong keystore or key password (case-sensitive) — re-enter the credentials set (§5) |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` on the phone | A previous build signed with a *different* keystore (debug vs release) is installed — uninstall it first |

---

## 5. Create the release keystore (YOU #3) — once, keep it forever

You need a JDK (`keytool`). Unity Hub installs one with the Android module; otherwise any Temurin JDK 17.

```bash
keytool -genkeypair -v \
  -keystore crossroads-release.keystore \
  -alias crossroads \
  -keyalg RSA -keysize 2048 -validity 10000 \
  -dname "CN=favourope78-web, O=CROSSROADS, C=NG"
```

It asks for a **keystore password** and a **key password** (you may use the same). Record:

| Unity Cloud credential field | Your value |
|---|---|
| Keystore file | `crossroads-release.keystore` (upload) |
| Keystore password | *(what you typed)* |
| Key alias | `crossroads` |
| Key password | *(what you typed)* |

**Never commit the keystore** (`.gitignore` ignores `*.keystore` / `*.jks`) and back it up — Google
Play ties your app to this key. Alternatively create it in the editor: *Project Settings → Player →
Android → Publishing Settings → Keystore Manager*.

---

## 6. Optional second target: `Android-Dev` (profiler build for FINAL_RELEASE_REPORT §7)

Clone `Android-Release` → rename **`Android-Dev`** and change only:

| Field | Value |
|---|---|
| Build output → Development build | **On** |
| Pre-Export Method | **`Crossroads.EditorTools.CloudBuildHooks.PreExportDev`** |
| Credentials set | Auto-generated debug keystore |
| Environment variables | `CROSSROADS_ARCH` = `arm64` (faster; fine for profiling) |

Install it, open the Unity Editor's **Profiler** (*Window → Analysis → Profiler*), target the phone
over USB/Wi-Fi, and capture the numbers listed in `FINAL_RELEASE_REPORT.md` §7 (FPS per quality
tier, GPU/CPU frame time, PSS memory, Batches/SetPass from the Frame Debugger, cold start).

---

## 7. Checklist (copy into the dashboard as you go)

- [ ] Unity Cloud project created (org + project) — **YOU**
- [ ] Source control: GitHub, PAT with `public_repo`, repo `favourope78-web/crossroads`, branch `main` — **YOU** (token)
- [ ] Target `Android-Release`: Android · `main` · Unity 6000.0.23f1 (auto-detect) · Windows 11 24H2 · SDK 35 · Micro
- [ ] Bundle ID `com.favourope78.crossroads`
- [ ] Credentials: debug keystore (first build) / release keystore from §5 (release) — **YOU**
- [ ] Advanced: Development build **off** · Pre-Export `Crossroads.EditorTools.CloudBuildHooks.PreExportRelease` · Post-Export `Crossroads.EditorTools.CloudBuildHooks.PostExport` · Library caching on · `.aab` off (APK)
- [ ] Build → download `Crossroads.apk` → `adb install -r`
- [ ] (Optional) `Android-Dev` target with `PreExportDev` for the on-device profile

---

### Reference — where each value lives in the repo

| Value | File |
|---|---|
| Unity version `6000.0.23f1` | `ProjectSettings/ProjectVersion.txt` |
| Bundle ID, company/product, min SDK 24, IL2CPP, architectures, landscape, input = Both | `ProjectSettings/ProjectSettings.asset` and (authoritatively, applied at build time) `Assets/Editor/AndroidDevBuild.cs → Configure()` |
| Cloud hooks `PreExportRelease` / `PreExportDev` / `PostExport` | `Assets/Editor/CloudBuildHooks.cs` |
| Scene list | `ProjectSettings/EditorBuildSettings.asset` (+ `AndroidDevBuild.ApplySceneList()`) |
| Quality tiers Low/Balanced/High, default Balanced | `ProjectSettings/QualitySettings.asset`, `Assets/Settings/URP_*.asset` |
| Packages (URP 17.0.3, Input System 1.11.2, Cinemachine 3.1.2, uGUI 2.0.0) | `Packages/manifest.json` |
| Local/CLI equivalents | `scripts/build_android_apk.sh [Unity] [dev\|release]`, GitHub workflow `.github/workflows/android-apk.yml` (manual, needs `UNITY_LICENSE`) |
