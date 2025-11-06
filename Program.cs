using System;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Xml.Serialization;

public class LanguageSwitcher : Form
{
    private readonly List<InputLanguage> languages;
    private Size buttonSize = new Size(180, 40);
    private int numCols = 4;
    private int numRows = 4;
    private FlowLayoutPanel panel;
    public Dictionary<Keys, InputLanguage> keyBindings = new Dictionary<Keys, InputLanguage>();
    private readonly string configPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LanguageSwitcher", "LanguageSwitcherSettings.xml");

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr SetActiveWindow(IntPtr hWnd);
    
    [DllImport("user32")]
    private static extern bool SetWindowPos(IntPtr hwnd, IntPtr hwnd2, int x, int y, int cx, int cy, int flags);
    // https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowpos
    //instead of calling SetForegroundWindow
    //SWP_NOSIZE = 1, SWP_NOMOVE = 2  -> keep the current pos and size (ignore x,y,cx,cy).
    //the second param = -1   -> set window as Topmost.


    public LanguageSwitcher()
    {

        // Get languages installed
        languages = InputLanguage.InstalledInputLanguages.Cast<InputLanguage>().ToList();

        // Compute display
        numRows = (int)Math.Ceiling(languages.Count / (double)numCols);

        // Initialize form
        this.FormBorderStyle = FormBorderStyle.None;
        this.TopMost = true;
        this.ShowInTaskbar = false;
        this.StartPosition = FormStartPosition.Manual;
        this.BackColor = Color.FromArgb(30, 30, 30);
        this.Size = new Size(buttonSize.Width*numCols+1, buttonSize.Height*numRows+1);
        this.WindowState = FormWindowState.Normal;
        this.MinimizeBox = false;
        this.MaximizeBox = false;

        // Create panel
        panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = true,
            AutoScroll = true
        };
        panel.Paint += PaintSeparator;


        // Load bindings        
        LoadKeyBindings();
        BuildLanguageButtons();

        this.Controls.Add(panel);

        // Handle form events
        this.Deactivate += (s, e) => this.Hide();
        this.KeyPreview = true;
        this.KeyDown += (s, e) => {
            if (e.KeyCode == Keys.Escape) this.Hide();
        };

        // Center on screen initially
        CenterOnScreen();
        SetActiveWindow(this.Handle);
        SetWindowPos(this.Handle, new IntPtr(-1), 0, 0, 0, 0, 0x1 | 0x2); // required to prevent app from closing automaticlaly when started if not focused and topmost.
    }

    private void LoadKeyBindings()
    {
        keyBindings = new Dictionary<Keys, InputLanguage>();

        if (!File.Exists(configPath))
        {
            CreateDefaultBindings();
            SaveKeyBindings();
            return;
        }

        try
        {
            var serializer = new XmlSerializer(typeof(List<KeyBinding>));
            using var reader = new StreamReader(configPath);
            var bindings = (List<KeyBinding>?)serializer.Deserialize(reader);

            if (bindings != null)
            foreach (var binding in bindings)
            {
                var lang = languages.FirstOrDefault(l =>
                    l.Culture.Name == binding.LanguageId);
                if (lang != null)
                    keyBindings[binding.Key] = lang;
            }
        }
        catch
        {
            // If config is invalid, use default bindings
            CreateDefaultBindings();
        }
    }

    private void SaveKeyBindings()
    {
        var dir = Path.GetDirectoryName(configPath) ?? Path.GetTempPath();
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        var bindings = keyBindings.Select(kvp => new KeyBinding
        {
            Key = kvp.Key,
            LanguageId = kvp.Value.Culture.Name
        }).ToList();

        var serializer = new XmlSerializer(typeof(List<KeyBinding>));
        using var writer = new StreamWriter(configPath);
        serializer.Serialize(writer, bindings);
    }

    public void CreateDefaultBindings()
    {
        keyBindings.Clear();
        for (int i = 0; i < Math.Min(languages.Count, 35); i++)
        {
            Keys key;
            if (i < 9) key = (Keys)(Keys.D1 + i); // D1 to D9
            else if (i == 9) key = Keys.D0;       // D0 for 10th item
            else key = (Keys)(Keys.A + i - 10);   // A to Z

            keyBindings[key] = languages[i];
        }
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        this.Hide(); // Hide initially        
    }

    private void CenterOnScreen()
    {
        var screen = Screen.FromPoint(Cursor.Position);
        var workingArea = screen.WorkingArea;
        this.Location = new Point(
            workingArea.Left + (workingArea.Width - this.Width) / 2,
            workingArea.Top + (workingArea.Height - this.Height) / 2
        );
    }

    public void ShowSwitcher()
    {
        CenterOnScreen();
        this.Show();
        this.BringToFront();
        this.Focus();
        FocusFirstButton();
    }

    
    private void FocusFirstButton()
    {
        if (panel.Controls.Count > 0)
        {
            ((Button)panel.Controls[0]).Focus();
        }
    }

    private void BuildLanguageButtons()
    {
        panel.Controls.Clear();

        foreach (var lang in languages)
        {
            if (lang == null) continue;
            Random r = new Random();
            var btn = new Button
            {
                Text = $"{GetKeyDisplay(lang)}: {lang.Culture.NativeName}",
                Width = buttonSize.Width - 1 ,
                Height = buttonSize.Height - 1,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(50, 50, 50),
                ForeColor = Color.White,
                UseVisualStyleBackColor = false,
                Tag = lang,
                Padding = new Padding(0,0,0,0),
                Margin = new Padding(1,1,0,0),

            };
            btn.FlatAppearance.BorderSize = 0;

            btn.Click += (s, e) => { if ( s != null) SwitchLanguage((InputLanguage?)((Button)s).Tag); this.Hide(); };
            //btn.Click += (s, e) => { var k = new Keys(); k = Keys.Control | Keys.X;   ButtonKeyDown(s, new KeyEventArgs(k)); };
            
            btn.KeyDown += ButtonKeyDown;
            btn.MouseEnter += (s, e) => { if (s == null) return; ((Button)s).BackColor = Color.FromArgb(70, 70, 70); ((Button)s).Focus(); };
            btn.MouseLeave += (s, e) => { if (s == null) return; ((Button)s).BackColor = Color.FromArgb(50, 50, 50); };
            panel.Controls.Add(btn);
        }
     
    }

    private string GetKeyDisplay(InputLanguage lang)
    {
        var binding = keyBindings.FirstOrDefault(kvp => kvp.Value.Equals(lang));
        if (binding.Key != default(Keys))
        {
            var key = binding.Key;
            if (key >= Keys.D1 && key <= Keys.D9) return (key - Keys.D1 + 1).ToString();
            if (key == Keys.D0) return "0";
            if (key >= Keys.A && key <= Keys.Z) return ((char)key).ToString();
        }
        return "?";
    }

    private void ButtonKeyDown(object? sender, KeyEventArgs e)
    {       
        // Exit program on esc
        if (e.KeyCode == Keys.Escape)
        {
            this.Hide();
            this.Close();
            return;
        }

        var btn = (Button?)sender;
        if (btn == null || (InputLanguage?)btn.Tag == null) return;
        var lang = (InputLanguage)btn.Tag;        
        var idx = panel.Controls.IndexOf(btn);

        

        // Quick assignment: Ctrl+Key assigns current highlighted language to that key
        if (e.Control && e.KeyCode != Keys.ControlKey && e.KeyCode != Keys.LControlKey && e.KeyCode != Keys.RControlKey)
        {
            if (IsAssignableKey(e.KeyCode))
            {
                // Remove value for assinging new keys to avoid duplicates
                foreach (var k in keyBindings) if (k.Value == lang) keyBindings.Remove(k.Key);
                 //&& !keyBindings.ContainsKey(key);
                keyBindings[e.KeyCode] = lang;
                SaveKeyBindings();
                BuildLanguageButtons(); // Refresh display                
            }

            return;
        }


        switch (e.KeyCode)
        {
            case Keys.Enter:
                SwitchLanguage(lang);
                this.Hide();
                break;
            case Keys.Tab:
                var next = (idx + 1) % panel.Controls.Count;
                ((Button)panel.Controls[next]).Focus();
                break;
            case Keys.Up:
                if (idx > 0) ((Button)panel.Controls[idx - 1]).Focus();
                break;
            case Keys.Down:
                if (idx < panel.Controls.Count - 1) ((Button)panel.Controls[idx + 1]).Focus();
                break;
        }

        // Handle key bindings
        if (keyBindings.ContainsKey(e.KeyCode))
        {
            SwitchLanguage(keyBindings[e.KeyCode]);
            this.Hide();
        }
    }

    private bool IsAssignableKey(Keys key)
    {
        return ((key >= Keys.D1 && key <= Keys.D9) || key == Keys.D0 || (key >= Keys.A && key <= Keys.Z));
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        
        // Handle global key presses
        if (keyBindings.ContainsKey(keyData))
        {
            SwitchLanguage(keyBindings[keyData]);            
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void SwitchLanguage(InputLanguage? lang)
    {
        InputLanguage.CurrentInputLanguage = lang;
        this.Close();
    }

    private void PaintSeparator(object? sender, PaintEventArgs e)
    {
        using (var pen = new Pen(Color.FromArgb(60, 60, 60), 1))
        {
            for (int i = 0; i < panel.Controls.Count - 1; i++)
            {
                var btn = panel.Controls[i];
                var rect = btn.Bounds;
                e.Graphics.DrawLine(pen, rect.Right, 0, rect.Right, this.Height);
            }
        }
    }

    private const int WM_ACTIVATEAPP = 0x1C;
    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WM_ACTIVATEAPP)
        {
            if (m.WParam == IntPtr.Zero)                
                this.Close();
        }
        base.WndProc(ref m);
    }


    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
        }
        base.Dispose(disposing);
    }

    [STAThread]
    public static void Main()
    {
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        var form = new LanguageSwitcher();
        Application.Run(form);
    }
}

// Serializable class for key bindings
[Serializable]
public class KeyBinding
{
    public Keys Key { get; set; }
    required public string LanguageId { get; set; }
}