using System.Runtime.InteropServices;
using Raylib_cs;

// Minimal Raylib-on-Android smoke: open a window, clear to dark teal, run a few
// hundred frames, close. If this shows a solid colour on-device without crashing,
// Option A is viable. See README.md — EXPERIMENTAL, never compiled in this repo.
namespace Aetheria.Android.Spike;

public static class Entry
{
    // NativeAOT exports this as a C-callable symbol. How the NativeActivity /
    // native_app_glue reaches it is the open question the spike exists to answer;
    // wire the manifest's android.app.lib_name / entry to call this.
    [UnmanagedCallersOnly(EntryPoint = "aetheria_android_main")]
    public static void AndroidMain()
    {
        try
        {
            Run();
        }
        catch
        {
            // On Android, an unhandled exception here is invisible — a real port
            // would route this to android log (__android_log_write via P/Invoke).
        }
    }

    private static void Run()
    {
        // On Android, size comes from the surface; 0,0 lets Raylib use the display.
        Raylib.InitWindow(0, 0, "Aetheria Android spike");
        Raylib.SetTargetFPS(60);

        var teal = new Color(18, 40, 44, 255);
        int frames = 0;
        while (!Raylib.WindowShouldClose() && frames++ < 600)
        {
            Raylib.BeginDrawing();
            Raylib.ClearBackground(teal);
            Raylib.DrawText("Aetheria: Raylib on Android", 40, 40, 20, Color.RayWhite);
            Raylib.EndDrawing();
        }

        Raylib.CloseWindow();
    }
}
