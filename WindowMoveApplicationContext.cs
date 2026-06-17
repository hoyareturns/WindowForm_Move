namespace WindowForm_Move;

public sealed class WindowMoveApplicationContext : ApplicationContext
{
    private readonly OverlayForm _overlay = new();
    private readonly NotifyIcon _notifyIcon;

    public WindowMoveApplicationContext()
    {
        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "Window Move",
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        _notifyIcon.DoubleClick += (_, _) => _overlay.ToggleVisible();
        _overlay.ExitRequested += (_, _) => ExitThread();
        _overlay.Show();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _overlay.Dispose();
        }

        base.Dispose(disposing);
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();

        var showItem = new ToolStripMenuItem("Show / Hide buttons");
        showItem.Click += (_, _) => _overlay.ToggleVisible();
        menu.Items.Add(showItem);

        var allItem = new ToolStripMenuItem("Move all windows");
        allItem.CheckOnClick = true;
        allItem.CheckedChanged += (_, _) => _overlay.MoveAllWindows = allItem.Checked;
        menu.Items.Add(allItem);

        menu.Items.Add(new ToolStripSeparator());

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitThread();
        menu.Items.Add(exitItem);

        return menu;
    }
}
