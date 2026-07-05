# Android spike — Option A (NativeAOT owns the entry point)

> **Status: EXPERIMENTAL / UNTESTED.** This directory is a *spike*, not a working
> port. Its only goal is to answer one question: **can a .NET (NativeAOT) build
> drive Raylib on a physical arm64 Android device long enough to clear the screen
> to a solid colour?** If that works, the full port (touch input, save-path fix,
> lifecycle, the actual game) becomes ordinary work. If it fights us, we should
> drop Raylib on mobile and port the render layer instead (the engine's
> simulation is already cleanly separated from rendering).
>
> Nobody has published a managed-.NET Raylib-on-Android bridge (see the research
> in the PR/branch history). So the pieces below that touch NativeAOT + Android +
> Raylib are **unverified** and will almost certainly need iteration on a machine
> with the Android NDK and the .NET Android workload. This container has neither,
> so none of this has been compiled.

## Why Option A

Raylib's Android backend (`rcore_android.c`) is designed to **call a `main()`
that the app provides** — it expects to be linked *into* the app's native
library, not to exist as a standalone `libraylib.so`. That's the root cause of
[raysan5/raylib#4484](https://github.com/raysan5/raylib/issues/4484)
(`undefined symbol: main referenced by rcore_android.c:279` when building a
shared lib).

Option A leans into that instead of fighting it:

1. Build Raylib for Android as a **static** archive (`libraylib.a`) — the
   supported, reliable path.
2. Compile the C# spike with **NativeAOT** to a native `.so`, **statically
   linking `libraylib.a` into it**. NativeAOT's generated module supplies the
   entry symbol Raylib is missing.
3. A thin `NativeActivity` in the manifest loads that `.so`.

One project, no separate C shim to maintain — that's why this is the leaner of
the two candidate architectures.

## Prerequisites (on a real dev machine, not this container)

- .NET 9 SDK + Android workload: `dotnet workload install android`
- Android NDK (r26+) and SDK, `ANDROID_NDK_HOME` exported
- A physical **arm64-v8a** device with USB debugging (emulator is `x86_64` — build
  that ABI too if you want the emulator)

## Steps

```sh
# 1. Build the native static lib for arm64 (well-trodden path — see raylib wiki)
./build-raylib-android.sh arm64-v8a

# 2. Build/deploy the spike (EXPERIMENTAL — expect to iterate here)
dotnet publish Aetheria.Android.Spike.csproj -f net9.0-android -c Release
#   then deploy the produced .apk to the device
```

Success = the device shows a solid dark-teal screen (the `ClearBackground`
colour in `AndroidSpike.cs`) and doesn't crash for a few seconds.

## The unknowns to expect (this is the actual research question)

- **Entry-point wiring.** Does `NativeActivity` + `native_app_glue` find the entry
  that NativeAOT exports, and does Raylib's `rcore_android` platform-init run? This
  is the make-or-break unknown. May require a few lines of glue and/or feeding
  Raylib the `ANativeActivity` / `AAssetManager`.
- **Static-link symbols.** NativeAOT static-linking of Raylib had unresolved-symbol
  friction even on desktop ([raylib-cs#258](https://github.com/chrisdill/raylib-cs/issues/258));
  Android will surface its own (log, EGL, GLESv2, OpenSLES/AAudio, `android`,
  `native_app_glue`). Add them to `NativeLibrary` / linker flags as they appear.
- **P/Invoke name.** Every binding is `[DllImport("raylib")]`; with static linking
  the symbols must resolve in-module. `DirectPInvoke` + `NativeLibrary` handle this,
  but the exact incantation is what the spike is for.

Everything here is a starting point to iterate from, not a guarantee.
