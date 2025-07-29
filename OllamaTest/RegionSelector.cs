using System.Drawing;
using System.Windows.Forms;

namespace OllamaTest;

/// <summary>
/// Interactive region selection overlay with mouse support
/// Provides a user-friendly way to select screen regions with visual feedback
/// </summary>
public partial class RegionSelector : Form
{
    private bool _isSelecting = false;
    private Point _startPoint;
    private Point _currentPoint;
    private Rectangle _selectionRectangle;
    private Bitmap _backgroundImage;
    
    public Rectangle SelectedRegion { get; private set; }
    public bool RegionSelected { get; private set; }

    public RegionSelector()
    {
        InitializeComponent();
        SetupForm();
        CaptureBackground();
    }

    private void InitializeComponent()
    {
        this.SuspendLayout();
        
        // Form properties
        this.AutoScaleDimensions = new SizeF(7F, 15F);
        this.AutoScaleMode = AutoScaleMode.Font;
        this.ClientSize = new Size(800, 600);
        this.FormBorderStyle = FormBorderStyle.None;
        this.Name = "RegionSelector";
        this.Text = "Select Region";
        this.TopMost = true;
        this.WindowState = FormWindowState.Maximized;
        this.BackColor = Color.Black;
        this.Opacity = 0.3;
        this.Cursor = Cursors.Cross;
        
        // Event handlers
        this.MouseDown += RegionSelector_MouseDown;
        this.MouseMove += RegionSelector_MouseMove;
        this.MouseUp += RegionSelector_MouseUp;
        this.Paint += RegionSelector_Paint;
        this.KeyDown += RegionSelector_KeyDown;
        
        this.ResumeLayout(false);
    }

    private void SetupForm()
    {
        // Make form cover entire screen
        var screenBounds = Screen.PrimaryScreen.Bounds;
        this.Bounds = screenBounds;
        
        // Enable key events
        this.KeyPreview = true;
        this.Focus();
    }

    private void CaptureBackground()
    {
        try
        {
            // Capture screen before showing overlay
            var screenBounds = Screen.PrimaryScreen.Bounds;
            _backgroundImage = new Bitmap(screenBounds.Width, screenBounds.Height);
            
            using (var graphics = Graphics.FromImage(_backgroundImage))
            {
                graphics.CopyFromScreen(screenBounds.Location, Point.Empty, screenBounds.Size);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to capture background: {ex.Message}");
        }
    }

    private void RegionSelector_MouseDown(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
        {
            _isSelecting = true;
            _startPoint = e.Location;
            _currentPoint = e.Location;
            _selectionRectangle = new Rectangle(_startPoint, Size.Empty);
            
            // Make overlay more transparent during selection
            this.Opacity = 0.1;
            this.Invalidate();
        }
    }

    private void RegionSelector_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isSelecting)
        {
            _currentPoint = e.Location;
            
            // Calculate selection rectangle
            int x = Math.Min(_startPoint.X, _currentPoint.X);
            int y = Math.Min(_startPoint.Y, _currentPoint.Y);
            int width = Math.Abs(_currentPoint.X - _startPoint.X);
            int height = Math.Abs(_currentPoint.Y - _startPoint.Y);
            
            _selectionRectangle = new Rectangle(x, y, width, height);
            
            // Update title with current selection info
            this.Text = $"Select Region - {width}×{height} pixels at ({x}, {y})";
            
            this.Invalidate();
        }
        else
        {
            // Show current mouse position
            this.Text = $"Select Region - Click and drag to select area - Position: ({e.X}, {e.Y})";
        }
    }

    private void RegionSelector_MouseUp(object sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left && _isSelecting)
        {
            _isSelecting = false;
            
            // Ensure minimum size
            if (_selectionRectangle.Width > 10 && _selectionRectangle.Height > 10)
            {
                SelectedRegion = _selectionRectangle;
                RegionSelected = true;
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                // Selection too small, reset
                _selectionRectangle = Rectangle.Empty;
                this.Opacity = 0.3;
                this.Text = "Select Region - Selection too small (minimum 10×10 pixels)";
                this.Invalidate();
            }
        }
    }

    private void RegionSelector_Paint(object sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        
        // Draw background image if available
        if (_backgroundImage != null)
        {
            g.DrawImage(_backgroundImage, 0, 0);
        }
        
        // Draw selection rectangle
        if (_isSelecting && !_selectionRectangle.IsEmpty)
        {
            // Draw selection area with semi-transparent overlay
            using (var brush = new SolidBrush(Color.FromArgb(100, Color.Blue)))
            {
                g.FillRectangle(brush, _selectionRectangle);
            }
            
            // Draw selection border
            using (var pen = new Pen(Color.Red, 2))
            {
                g.DrawRectangle(pen, _selectionRectangle);
            }
            
            // Draw corner handles
            DrawCornerHandles(g, _selectionRectangle);
            
            // Draw dimensions text
            DrawDimensionsText(g, _selectionRectangle);
        }
        
        // Draw instructions
        DrawInstructions(g);
    }

    private void DrawCornerHandles(Graphics g, Rectangle rect)
    {
        const int handleSize = 8;
        using (var brush = new SolidBrush(Color.White))
        using (var pen = new Pen(Color.Black, 1))
        {
            // Top-left
            var handle = new Rectangle(rect.X - handleSize/2, rect.Y - handleSize/2, handleSize, handleSize);
            g.FillRectangle(brush, handle);
            g.DrawRectangle(pen, handle);
            
            // Top-right
            handle = new Rectangle(rect.Right - handleSize/2, rect.Y - handleSize/2, handleSize, handleSize);
            g.FillRectangle(brush, handle);
            g.DrawRectangle(pen, handle);
            
            // Bottom-left
            handle = new Rectangle(rect.X - handleSize/2, rect.Bottom - handleSize/2, handleSize, handleSize);
            g.FillRectangle(brush, handle);
            g.DrawRectangle(pen, handle);
            
            // Bottom-right
            handle = new Rectangle(rect.Right - handleSize/2, rect.Bottom - handleSize/2, handleSize, handleSize);
            g.FillRectangle(brush, handle);
            g.DrawRectangle(pen, handle);
        }
    }

    private void DrawDimensionsText(Graphics g, Rectangle rect)
    {
        if (rect.Width > 50 && rect.Height > 30) // Only show if rectangle is large enough
        {
            string text = $"{rect.Width} × {rect.Height}";
            using (var font = new Font("Arial", 12, FontStyle.Bold))
            using (var brush = new SolidBrush(Color.White))
            using (var shadowBrush = new SolidBrush(Color.Black))
            {
                var textSize = g.MeasureString(text, font);
                var textPos = new PointF(
                    rect.X + (rect.Width - textSize.Width) / 2,
                    rect.Y + (rect.Height - textSize.Height) / 2
                );
                
                // Draw shadow
                g.DrawString(text, font, shadowBrush, textPos.X + 1, textPos.Y + 1);
                // Draw text
                g.DrawString(text, font, brush, textPos);
            }
        }
    }

    private void DrawInstructions(Graphics g)
    {
        var instructions = new[]
        {
            "🖱️  Click and drag to select region",
            "📐 Minimum size: 10×10 pixels", 
            "⌨️  Press ESC to cancel",
            "✅ Release mouse to capture region"
        };
        
        using (var font = new Font("Arial", 10))
        using (var brush = new SolidBrush(Color.White))
        using (var shadowBrush = new SolidBrush(Color.Black))
        {
            int y = 20;
            foreach (var instruction in instructions)
            {
                // Draw shadow
                g.DrawString(instruction, font, shadowBrush, 21, y + 1);
                // Draw text
                g.DrawString(instruction, font, brush, 20, y);
                y += 25;
            }
        }
    }

    private void RegionSelector_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape)
        {
            RegionSelected = false;
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _backgroundImage?.Dispose();
        }
        base.Dispose(disposing);
    }
}