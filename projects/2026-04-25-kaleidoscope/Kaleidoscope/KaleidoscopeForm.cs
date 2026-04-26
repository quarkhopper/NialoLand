// KaleidoscopeForm.cs — the animation window.
//
// This form fills (most of) the screen with the live kaleidoscope animation.
// It owns two timers:
//   - animTimer (33ms): drives the rotation, calls Invalidate() to request a repaint
//   - imageTimer (5000ms): cycles to the next photo in the folder
//
// The overlay panel at the bottom holds three buttons:
//   Pause/Resume — freeze/unfreeze the rotation
//   Screenshot   — prompt for a save path and write a PNG
//   Back         — stop everything and return to the control window

using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;

namespace Kaleidoscope;

public class KaleidoscopeForm : Form
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly MainForm _owner;        // reference so we can show it again on Back
    private readonly string[] _imagePaths;   // all image files in the selected folder
    private readonly int _segments;          // mirror slice count (from MainForm settings)
    private readonly double _speedStep;      // radians added per animation frame

    private int _currentIndex = 0;          // which image we're currently displaying
    private Bitmap? _currentBitmap;         // the loaded source image (disposed on cycle)
    private double _rotationAngle = 0.0;    // current rotation in radians; grows each frame
    private bool _isPaused = false;         // whether animation is frozen

    // ── Timers ────────────────────────────────────────────────────────────────

    // animTimer fires ~30 times per second to advance the rotation angle.
    private readonly System.Windows.Forms.Timer _animTimer;

    // imageTimer fires every 5 seconds to swap to the next photo.
    private readonly System.Windows.Forms.Timer _imageTimer;

    // ── UI Controls ───────────────────────────────────────────────────────────

    private Panel _canvas = null!;       // fills the form; OnPaint draws the kaleidoscope here
    private Button _pauseButton = null!;
    private Button _screenshotButton = null!;
    private Button _backButton = null!;

    // ── Constructor ───────────────────────────────────────────────────────────

    public KaleidoscopeForm(MainForm owner, string[] imagePaths, int segments, int speed)
    {
        _owner = owner;
        _imagePaths = imagePaths;
        _segments = segments;

        // Map the speed slider value (1–10) to radians per frame.
        // At speed 1: 0.005 rad/frame ≈ very slow drift
        // At speed 10: 0.05 rad/frame ≈ fast spin
        _speedStep = speed * 0.005;

        // Form setup — dark background, no chrome, start maximised.
        Text = "Kaleidoscope — Animation";
        BackColor = Color.Black;
        WindowState = FormWindowState.Maximized;
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;

        // DoubleBuffered reduces flicker by compositing off-screen before showing.
        DoubleBuffered = true;

        BuildUI();
        LoadImage(_currentIndex);

        // animTimer: advances the rotation angle and requests a redraw.
        _animTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _animTimer.Tick += OnAnimTick;
        _animTimer.Start();

        // imageTimer: swaps to the next image every 5 seconds.
        _imageTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _imageTimer.Tick += OnImageTick;
        _imageTimer.Start();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas panel — fills the whole form, custom painted each frame.
        _canvas = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black
        };
        // Setting DoubleBuffered on a Panel requires this sub-class trick via reflection
        // (WinForms doesn't expose it directly on Panel). We use the form's buffering instead.
        _canvas.Paint += OnCanvasPaint;

        // Overlay panel — sits at the bottom, semi-transparent buttons.
        var overlay = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 56,
            BackColor = Color.FromArgb(180, 15, 15, 25) // semi-transparent dark strip
        };

        _pauseButton = MakeOverlayButton("⏸  Pause", Color.FromArgb(80, 60, 130));
        _pauseButton.Click += OnPauseClicked;

        _screenshotButton = MakeOverlayButton("📷  Screenshot", Color.FromArgb(40, 100, 80));
        _screenshotButton.Click += OnScreenshotClicked;

        _backButton = MakeOverlayButton("← Back", Color.FromArgb(100, 40, 40));
        _backButton.Click += OnBackClicked;

        // Layout the three buttons centered in the overlay panel.
        // We use a TableLayoutPanel so they stay evenly spaced when the window resizes.
        var buttonRow = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            Padding = new Padding(8)
        };
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.3f));
        buttonRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.4f));
        buttonRow.Controls.Add(_pauseButton, 0, 0);
        buttonRow.Controls.Add(_screenshotButton, 1, 0);
        buttonRow.Controls.Add(_backButton, 2, 0);

        overlay.Controls.Add(buttonRow);

        Controls.Add(_canvas);
        Controls.Add(overlay);
    }

    /// <summary>Creates a styled button for the overlay bar.</summary>
    private static Button MakeOverlayButton(string text, Color bg)
    {
        var btn = new Button
        {
            Text = text,
            Dock = DockStyle.Fill,
            Margin = new Padding(6, 4, 6, 4),
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            BackColor = bg,
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    // ── Timer Handlers ────────────────────────────────────────────────────────

    private void OnAnimTick(object? sender, EventArgs e)
    {
        if (_isPaused) return;

        // Advance the rotation. Wrapping at 2π keeps the number small,
        // though it's not strictly necessary — Math.Atan2 handles big angles fine.
        _rotationAngle = (_rotationAngle + _speedStep) % (2.0 * Math.PI);

        // Invalidate() tells WinForms "this control needs repainting" — it will
        // call OnCanvasPaint on the next UI cycle. More efficient than drawing directly.
        _canvas.Invalidate();
    }

    private void OnImageTick(object? sender, EventArgs e)
    {
        // Advance to the next image, wrapping around at the end of the list.
        _currentIndex = (_currentIndex + 1) % _imagePaths.Length;
        LoadImage(_currentIndex);
        _canvas.Invalidate();
    }

    // ── Canvas Paint ─────────────────────────────────────────────────────────

    private void OnCanvasPaint(object? sender, PaintEventArgs e)
    {
        if (_currentBitmap == null) return;

        // Render one frame. This allocates a new Bitmap — we dispose it after
        // drawing so memory doesn't pile up (30 frames/sec × un-disposed bitmaps
        // would eat RAM very quickly).
        using var frame = KaleidoscopeRenderer.RenderFrame(
            _currentBitmap, _segments, _rotationAngle, _canvas.Size);

        e.Graphics.DrawImage(frame, 0, 0);
    }

    // ── Button Handlers ───────────────────────────────────────────────────────

    private void OnPauseClicked(object? sender, EventArgs e)
    {
        _isPaused = !_isPaused;
        _pauseButton.Text = _isPaused ? "▶  Resume" : "⏸  Pause";
        _pauseButton.BackColor = _isPaused
            ? Color.FromArgb(60, 110, 60)   // green tint when paused (ready to resume)
            : Color.FromArgb(80, 60, 130);  // purple when playing
    }

    private void OnScreenshotClicked(object? sender, EventArgs e)
    {
        if (_currentBitmap == null) return;

        // Pause animation briefly while the dialog is open so the image doesn't
        // change out from under us. We'll restart after the dialog closes.
        bool wasPaused = _isPaused;
        _isPaused = true;

        using var dialog = new SaveFileDialog
        {
            Title = "Save screenshot",
            Filter = "PNG image|*.png",
            // Suggest a filename with a timestamp so saves don't overwrite each other.
            FileName = $"kaleidoscope_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png",
            DefaultExt = "png"
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            // Render the current frame at full canvas size and save to disk.
            using var frame = KaleidoscopeRenderer.RenderFrame(
                _currentBitmap, _segments, _rotationAngle, _canvas.Size);
            frame.Save(dialog.FileName, ImageFormat.Png);
        }

        _isPaused = wasPaused;
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        // Clean up timers and bitmaps before closing.
        _animTimer.Stop();
        _imageTimer.Stop();
        _currentBitmap?.Dispose();
        _currentBitmap = null;

        // Re-show the control window and close this one.
        _owner.Show();
        Close();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Loads the image at the given index, disposing the previous one.</summary>
    private void LoadImage(int index)
    {
        var prev = _currentBitmap;

        try
        {
            // Load the image from disk. We wrap in a 'new Bitmap()' call so the
            // file handle is released immediately (Bitmap.FromFile keeps the file locked).
            using var img = Image.FromFile(_imagePaths[index]);
            _currentBitmap = new Bitmap(img);
        }
        catch
        {
            // If a file fails to load (corrupted, unsupported sub-format, etc.),
            // skip it and try the next one.
            _currentIndex = (_currentIndex + 1) % _imagePaths.Length;
            if (_currentIndex != index) // guard against infinite loop if all files fail
                LoadImage(_currentIndex);
        }

        prev?.Dispose();
    }

    // ── Cleanup ───────────────────────────────────────────────────────────────

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _animTimer.Dispose();
            _imageTimer.Dispose();
            _currentBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }
}
