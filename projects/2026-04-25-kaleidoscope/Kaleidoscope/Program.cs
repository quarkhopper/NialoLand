// Program.cs — entry point for the Kaleidoscope app.
//
// [STAThread] is required for all WinForms apps. It sets the COM threading model
// to "Single-Threaded Apartment", which Windows UI components depend on.
// Without it, clipboard access and dialogs like FolderBrowserDialog won't work.

namespace Kaleidoscope;

static class Program
{
    [STAThread]
    static void Main()
    {
        // ApplicationConfiguration.Initialize() applies the app.manifest settings:
        // high-DPI awareness, visual styles, and compatible text rendering.
        ApplicationConfiguration.Initialize();

        // Start on the control screen. The MainForm launches KaleidoscopeForm
        // itself when the user clicks Go!, and shows itself again on Back.
        Application.Run(new MainForm());
    }
}