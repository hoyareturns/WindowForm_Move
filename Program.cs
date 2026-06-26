namespace WindowForm_Move;

static class Program
{
    /// <summary>
    ///  The main entry point for the application.
    /// </summary>
    [STAThread]
    static void Main()
    {
        SingleInstanceCoordinator.StopExistingInstances();
        ApplicationConfiguration.Initialize();
        Application.Run(new WindowMoveApplicationContext());
    }    
}
