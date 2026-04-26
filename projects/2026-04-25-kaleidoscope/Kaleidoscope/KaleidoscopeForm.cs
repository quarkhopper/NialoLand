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
using OpenTK.GLControl;

namespace Kaleidoscope;

public class KaleidoscopeForm : Form
{
    // ── State ─────────────────────────────────────────────────────────────────

    private readonly MainForm _owner;        // reference so we can show it again on Back
    private readonly string[] _imagePaths;   // all image files in the selected folder
    private readonly int _segments;          // mirror slice count (from MainForm settings)
    private readonly double _speedStep;      // radians added per animation frame

    private int _currentIndex = 0;          // which image we're currently displaying
    private readonly Random _rng = new();    // used to pick the next image at random
    private Bitmap? _currentBitmap;         // the loaded source image (disposed on cycle)
    private double _rotationAngle = 0.0;    // current rotation in radians; grows each frame
    private bool _isPaused = false;         // whether animation is frozen

    // ── Timers ────────────────────────────────────────────────────────────────

    // animTimer fires ~30 times per second to advance the rotation angle.
    private readonly System.Windows.Forms.Timer _animTimer;

    // imageTimer fires every 5 seconds to swap to the next photo.
    private readonly System.Windows.Forms.Timer _imageTimer;

    // ── UI Controls ───────────────────────────────────────────────────────────

    private GLControl? _glControl;
    private BufferedCanvasPanel _cpuCanvas = null!;
    private Panel _overlayPanel = null!;
    private GpuKaleidoscopeRenderer? _gpuRenderer;
    private bool _gpuActive;
    private string? _gpuDisabledReason;

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

        // DoubleBuffered still helps UI overlays while the render surface is updating.
        DoubleBuffered = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        UpdateStyles();

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
        _cpuCanvas = new BufferedCanvasPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black
        };
        _cpuCanvas.Paint += OnCanvasPaint;

        try
        {
            _glControl = new GLControl
            {
                Dock = DockStyle.Fill,
                BackColor = Color.Black
            };

            _glControl.Load += OnGlLoad;
            _glControl.Paint += OnGlPaint;
            _glControl.Resize += OnGlResize;
            _glControl.Visible = true;

            _gpuActive = true;
        }
        catch (Exception ex)
        {
            _gpuActive = false;
            _gpuDisabledReason = ex.Message;
        }

        _cpuCanvas.Visible = !_gpuActive;

        // Overlay panel — sits at the bottom, semi-transparent buttons.
        _overlayPanel = new Panel
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

        _overlayPanel.Controls.Add(buttonRow);

        if (_glControl is not null)
            Controls.Add(_glControl);

        Controls.Add(_cpuCanvas);
        Controls.Add(_overlayPanel);
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
        if (_gpuActive && _glControl is not null)
            _glControl.Invalidate();
        else
            _cpuCanvas.Invalidate();
    }

    private void OnImageTick(object? sender, EventArgs e)
    {
        if (_isPaused) return;

        // Pick a random image, avoiding the one we're already showing.
        // If there's only one image, this is a no-op.
        if (_imagePaths.Length > 1)
        {
            int next;
            do { next = _rng.Next(_imagePaths.Length); } while (next == _currentIndex);
            _currentIndex = next;
        }
        LoadImage(_currentIndex);

        if (_gpuActive && _glControl is not null)
            _glControl.Invalidate();
        else
            _cpuCanvas.Invalidate();
    }

    private void OnGlLoad(object? sender, EventArgs e)
    {
        if (_glControl is null)
            return;

        try
        {
            _glControl.MakeCurrent();

            _gpuRenderer = new GpuKaleidoscopeRenderer();
            _gpuRenderer.Initialize();

            if (_currentBitmap is not null)
                _gpuRenderer.SetSourceImage(_currentBitmap);

            _gpuActive = true;
            _glControl.Visible = true;
            _cpuCanvas.Visible = false;
        }
        catch (Exception ex)
        {
            _gpuDisabledReason = ex.Message;
            SwitchToCpuFallback();
        }
    }

    private void OnGlResize(object? sender, EventArgs e)
    {
        if (_gpuActive && _glControl is not null)
            _glControl.Invalidate();
    }

    private void OnGlPaint(object? sender, PaintEventArgs e)
    {
        if (!_gpuActive || _glControl is null || _gpuRenderer is null)
            return;

        try
        {
            _glControl.MakeCurrent();
            _gpuRenderer.Render(_glControl.ClientSize, _segments, (float)_rotationAngle);
            _glControl.SwapBuffers();
        }
        catch (Exception ex)
        {
            _gpuDisabledReason = ex.Message;
            SwitchToCpuFallback();
        }
    }

    private void SwitchToCpuFallback()
    {
        _gpuActive = false;

        if (_glControl is not null)
            _glControl.Visible = false;

        _cpuCanvas.Visible = true;
        _cpuCanvas.BringToFront();
        _overlayPanel.BringToFront();
        _cpuCanvas.Invalidate();

        if (!string.IsNullOrWhiteSpace(_gpuDisabledReason))
        {
            Text = "Kaleidoscope — Animation (CPU fallback)";
        }
    }

    // ── Canvas Paint ─────────────────────────────────────────────────────────

    private void OnCanvasPaint(object? sender, PaintEventArgs e)
    {
        if (_currentBitmap == null) return;

        // Render one frame. This allocates a new Bitmap — we dispose it after
        // drawing so memory doesn't pile up (30 frames/sec × un-disposed bitmaps
        // would eat RAM very quickly).
        using var frame = KaleidoscopeRenderer.RenderFrame(
            _currentBitmap, _segments, _rotationAngle, _cpuCanvas.ClientSize);

        e.Graphics.DrawImage(frame, 0, 0);
    }

    private Size CpuCanvasSize => _cpuCanvas.ClientSize;

    private Size RenderSurfaceSize =>
        _gpuActive && _glControl is not null ? _glControl.ClientSize : CpuCanvasSize;

    // ── Button Handlers ───────────────────────────────────────────────────────

    private void OnPauseClicked(object? sender, EventArgs e)
    {
        SetPaused(!_isPaused, updateButtonState: true);
    }

    private void SetPaused(bool paused, bool updateButtonState)
    {
        _isPaused = paused;

        if (_isPaused)
        {
            _animTimer.Stop();
            _imageTimer.Stop();
        }
        else
        {
            _animTimer.Start();
            _imageTimer.Start();

            if (_gpuActive && _glControl is not null)
                _glControl.Invalidate();
            else
                _cpuCanvas.Invalidate();
        }

        if (!updateButtonState)
            return;

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
        SetPaused(true, updateButtonState: false);

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
            if (_gpuActive && _glControl is not null && _gpuRenderer is not null)
            {
                _glControl.MakeCurrent();
                using var frame = _gpuRenderer.CaptureCurrentFrame(
                    _glControl.ClientSize, _segments, (float)_rotationAngle);
                frame.Save(dialog.FileName, ImageFormat.Png);
            }
            else
            {
                // Render the current frame at full canvas size and save to disk.
                using var frame = KaleidoscopeRenderer.RenderFrame(
                    _currentBitmap, _segments, _rotationAngle, RenderSurfaceSize);
                frame.Save(dialog.FileName, ImageFormat.Png);
            }
        }

        SetPaused(wasPaused, updateButtonState: false);
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        // Clean up timers and bitmaps before closing.
        _animTimer.Stop();
        _imageTimer.Stop();
        _gpuRenderer?.Dispose();
        _gpuRenderer = null;
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

            if (_gpuActive && _gpuRenderer is not null && _glControl is not null)
            {
                _glControl.MakeCurrent();
                _gpuRenderer.SetSourceImage(_currentBitmap);
            }
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
            _gpuRenderer?.Dispose();
            _glControl?.Dispose();
            _currentBitmap?.Dispose();
        }
        base.Dispose(disposing);
    }

    // A custom panel that uses full buffering and skips background clears.
    // This avoids the black flash/flicker when resizing large animation windows.
    private sealed class BufferedCanvasPanel : Panel
    {
        public BufferedCanvasPanel()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            // Intentionally blank to prevent a separate background erase pass.
            // The frame renderer paints every pixel anyway.
        }
    }
}
