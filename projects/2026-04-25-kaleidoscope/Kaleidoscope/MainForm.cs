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
        Size = new Size(620, 460);
        MinimumSize = new Size(620, 460);
        MaximumSize = new Size(620, 460); // fixed size for the control screen
        BackColor = Color.FromArgb(20, 20, 30);
        ForeColor = Color.White;
        Font = new Font("Segoe UI", 11f);
        FormBorderStyle = FormBorderStyle.FixedSingle;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildUI();
    }

    // ── UI Construction ───────────────────────────────────────────────────────

    private void BuildUI()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = BackColor,
            Padding = new Padding(24, 20, 24, 20),
            ColumnCount = 1,
            RowCount = 7
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 14f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var title = new Label
        {
            Text = "✦ Kaleidoscope",
            Font = new Font("Segoe UI", 24f, FontStyle.Bold),
            ForeColor = Color.FromArgb(180, 130, 255),
            AutoSize = true,
            Margin = new Padding(0)
        };

        var folderLabel = new Label
        {
            Text = "Photo folder",
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Margin = new Padding(0, 0, 0, 8)
        };

        var folderRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18)
        };
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        folderRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150f));

        _folderBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Height = 36,
            ReadOnly = true,
            BackColor = Color.FromArgb(55, 55, 75),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 12, 0)
        };

        _browseButton = new Button
        {
            Text = "Browse...",
            Dock = DockStyle.Fill,
            Height = 40,
            BackColor = Color.FromArgb(100, 75, 160),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand
        };
        _browseButton.FlatAppearance.BorderSize = 0;
        _browseButton.Click += OnBrowseClicked;
        folderRow.Controls.Add(_folderBox, 0, 0);
        folderRow.Controls.Add(_browseButton, 1, 0);

        var segmentsRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 2,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 18)
        };
        segmentsRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
        segmentsRow.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        var segmentsLabel = new Label
        {
            Text = "Mirror segments",
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 4, 0, 0)
        };

        _segmentsPicker = new NumericUpDown
        {
            Width = 96,
            Height = 36,
            Minimum = 4,
            Maximum = 16,
            Increment = 2,
            Value = 8,
            BackColor = Color.FromArgb(60, 60, 85),
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0)
        };

        segmentsRow.Controls.Add(segmentsLabel, 0, 0);
        segmentsRow.Controls.Add(_segmentsPicker, 1, 0);

        var speedRow = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            ColumnCount = 3,
            AutoSize = true,
            Margin = new Padding(0)
        };
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 180f));
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        speedRow.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 48f));

        var speedRowLabel = new Label
        {
            Text = "Rotation speed",
            AutoSize = true,
            ForeColor = Color.FromArgb(220, 220, 220),
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Anchor = AnchorStyles.Left,
            Margin = new Padding(0, 10, 0, 0)
        };

        _speedSlider = new TrackBar
        {
            Dock = DockStyle.Fill,
            Height = 48,
            Minimum = 1,
            Maximum = 10,
            Value = 5,
            TickFrequency = 1,
            BackColor = Color.FromArgb(20, 20, 30),
            Margin = new Padding(0)
        };
        _speedSlider.ValueChanged += (_, _) => UpdateSpeedLabel();

        _speedLabel = new Label
        {
            Text = "5",
            Dock = DockStyle.Fill,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(0)
        };

        speedRow.Controls.Add(speedRowLabel, 0, 0);
        speedRow.Controls.Add(_speedSlider, 1, 0);
        speedRow.Controls.Add(_speedLabel, 2, 0);

        _goButton = new Button
        {
            Text = "Launch Kaleidoscope",
            Size = new Size(260, 56),
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            BackColor = Color.FromArgb(110, 65, 195),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Enabled = false,
            Cursor = Cursors.Hand,
            Anchor = AnchorStyles.None,
            Margin = new Padding(0)
        };
        _goButton.FlatAppearance.BorderSize = 0;
        _goButton.Click += OnGoClicked;

        var goHost = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            AutoSize = true,
            Margin = new Padding(0, 0, 0, 0)
        };
        goHost.Controls.Add(_goButton);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(new Panel { Height = 2, Width = 1, Margin = new Padding(0) }, 0, 1);
        root.Controls.Add(folderLabel, 0, 2);
        root.Controls.Add(folderRow, 0, 3);
        root.Controls.Add(segmentsRow, 0, 4);
        root.Controls.Add(speedRow, 0, 5);
        root.Controls.Add(goHost, 0, 6);

        Controls.Add(root);
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
