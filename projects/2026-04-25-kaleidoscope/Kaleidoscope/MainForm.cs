// MainForm.cs — the control screen shown when the app starts.
//
// This is a standard Windows Forms (WinForms) form — a window with buttons,
// labels, and controls laid out visually. We build it entirely in code here
// (no Visual Studio drag-and-drop designer) so everything is transparent and
// easy to follow on stream.
//
// Controls on this form:
//   - A folder picker so you can point the app at your photos
//   - A "segments" spinner: how many mirror slices the kaleidoscope uses
//   - A "speed" slider: how fast it rotates
//   - A "Go!" button to launch the animation

using System.Drawing;
using System.Windows.Forms;

namespace Kaleidoscope;

public class MainForm : Form
{
    // ── UI Controls ───────────────────────────────────────────────────────────

    private TextBox _folderBox = null!;
    private Button _browseButton = null!;
    private NumericUpDown _segmentsPicker = null!;
    private TrackBar _speedSlider = null!;
    private Label _speedLabel = null!;
    private Button _goButton = null!;

    // ── Constructor ───────────────────────────────────────────────────────────

    public MainForm()
    {
        // Basic form setup — title, size, background.
        Text = "Kaleidoscope";
        Size = new Size(480, 320);
        MinimumSize = new Size(480, 320);
        MaximumSize = new Size(480, 320); // fixed size for the control screen
        BackColor = Color.FromArgb(20, 20, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 10f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;

        BuildUI();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        // ── Title label ──────────────────────────────────────────────────────
        var title = new Label
        {
            Text = "✦ Kaleidoscope",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 130, 255),
            AutoSize = true,
            Location = new Point(20, 20)
        };

        // ── Folder row ───────────────────────────────────────────────────────
        var folderLabel = new Label
        {
            Text = "Photo folder:",
            AutoSize = true,
            Location = new Point(20, 80),
            ForeColor = Color.LightGray
        };

        // Read-only text box that shows the selected folder path.
        _folderBox = new TextBox
        {
            Location = new Point(20, 100),
            Size = new Size(330, 26),
            ReadOnly = true,
            BackColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        _browseButton = new Button
        {
            Text = "Browse…",
            Location = new Point(360, 99),
            Size = new Size(90, 28),
            BackColor = Color.FromArgb(80, 60, 120),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _browseButton.FlatAppearance.BorderSize = 0;
        _browseButton.Click += OnBrowseClicked;

        // ── Segments row ─────────────────────────────────────────────────────
        var segmentsLabel = new Label
        {
            Text = "Mirror segments:",
            AutoSize = true,
            Location = new Point(20, 148),
            ForeColor = Color.LightGray
        };

        // NumericUpDown is the spinner control (▲▼ arrows next to a number).
        // Segments must be even for the pattern to tile cleanly.
        _segmentsPicker = new NumericUpDown
        {
            Location = new Point(160, 145),
            Size = new Size(70, 26),
            Minimum = 4,
            Maximum = 16,
            Increment = 2,
            Value = 8,
            BackColor = Color.FromArgb(40, 40, 55),
            ForeColor = Color.White,
            BorderStyle = BorderStyle.FixedSingle
        };

        // ── Speed row ────────────────────────────────────────────────────────
        var speedRowLabel = new Label
        {
            Text = "Rotation speed:",
            AutoSize = true,
            Location = new Point(20, 196),
            ForeColor = Color.LightGray
        };

        // TrackBar is the slider control. Value maps to radians-per-frame later.
        _speedSlider = new TrackBar
        {
            Location = new Point(160, 188),
            Size = new Size(200, 40),
            Minimum = 1,
            Maximum = 10,
            Value = 5,
            TickFrequency = 1,
            BackColor = Color.FromArgb(20, 20, 30)
        };
        _speedSlider.ValueChanged += (_, _) => UpdateSpeedLabel();

        _speedLabel = new Label
        {
            Text = "5",
            AutoSize = true,
            Location = new Point(368, 196),
            ForeColor = Color.White
        };

        // ── Go button ────────────────────────────────────────────────────────
        _goButton = new Button
        {
            Text = "Go!",
            Location = new Point(180, 240),
            Size = new Size(120, 36),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            BackColor = Color.FromArgb(100, 60, 180),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false,         // disabled until a folder is selected
            Cursor = Cursors.Hand
        };
        _goButton.FlatAppearance.BorderSize = 0;
        _goButton.Click += OnGoClicked;

        // ── Add everything to the form ───────────────────────────────────────
        Controls.AddRange(new Control[]
        {
            title, folderLabel, _folderBox, _browseButton,
            segmentsLabel, _segmentsPicker,
            speedRowLabel, _speedSlider, _speedLabel,
            _goButton
        });
    }

    // ── Event Handlers ────────────────────────────────────────────────────────

    private void OnBrowseClicked(object? sender, EventArgs e)
    {
        // FolderBrowserDialog opens the standard Windows folder picker.
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a folder of photos for the kaleidoscope",
            UseDescriptionForTitle = true
        };

        if (dialog.ShowDialog() == DialogResult.OK)
        {
            _folderBox.Text = dialog.SelectedPath;

            // Only enable Go! if the folder actually contains image files.
            var images = GetImagePaths(dialog.SelectedPath);
            _goButton.Enabled = images.Length > 0;

            if (images.Length == 0)
            {
                MessageBox.Show(
                    "No images found in that folder.\nSupported formats: JPG, JPEG, PNG, BMP, GIF",
                    "No images",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
            }
        }
    }

    private void OnGoClicked(object? sender, EventArgs e)
    {
        var images = GetImagePaths(_folderBox.Text);
        if (images.Length == 0) return;

        int segments = (int)_segmentsPicker.Value;
        int speed = _speedSlider.Value;

        // Hide this control window and open the animation window.
        // We pass 'this' (the MainForm) so the animation window can show it again
        // when the user clicks Back.
        Hide();
        var animWindow = new KaleidoscopeForm(this, images, segments, speed);
        animWindow.Show();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void UpdateSpeedLabel()
    {
        _speedLabel.Text = _speedSlider.Value.ToString();
    }

    /// <summary>
    /// Returns all supported image file paths in the given folder.
    /// </summary>
    private static string[] GetImagePaths(string folder)
    {
        if (!Directory.Exists(folder)) return [];

        // Collect jpg, jpeg, png, bmp, gif — the formats Bitmap can load natively.
        var extensions = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" };
        return extensions
            .SelectMany(ext => Directory.GetFiles(folder, ext, SearchOption.TopDirectoryOnly))
            .OrderBy(f => f)
            .ToArray();
    }
}
