using System;
using System.Drawing;
using System.Resources;
using System.Threading;
using System.Windows.Forms;
using TrashVanish.Classes;
using TrashVanish.Forms;

namespace TrashVanish
{
    public delegate void UpdateLogger();
    public partial class MainWindow : Form
    {
        private readonly ResourceManager resourceManager;
        private readonly string cwd = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        private bool running;
        private CancellationTokenSource cancellation;
        public MainWindow()
        {
            InitializeComponent();
            Icon = Properties.Resources.appicon;
            resourceManager = currentCulture.GlobalResourseManager;
            FormClosing += (sender, args) =>
            {
                if (running)
                {
                    args.Cancel = true;
                    cancellation.Cancel();
                }
            };
        }
        private void MainWindow_Load(object sender, EventArgs e)
        {
            try
            {
                DBConnection.CheckDB();
                Log(resourceManager.GetString("AppIsLoaded"), Color.Cyan);
            }
            catch (Exception error)
            {
                mainTask.Enabled = false;
                MessageBox.Show(this, error.Message, resourceManager.GetString("dbError"), MessageBoxButtons.OK,
                                MessageBoxIcon.Error);
            }
        }
        private async void MainTask_Click(object sender, EventArgs e)
        {
            if (running)
                return;
            running = true;
            mainTask.Enabled = false;
            cancellation = new CancellationTokenSource();
            try
            {
                if (Properties.Settings.Default.clearLog)
                    logRichTextBox.Clear();
                var rules = DBConnection.LoadRules();
                var sets = DBConnection.LoadSets();
                var progress = new Progress<FileProgress>(
                    item =>
                    {
                        if (IsDisposed)
                            return;
                        string message =
                            resourceManager.GetString("operation" + item.Outcome) ?? item.Outcome.ToString();
                        Log(message + ": " + item.Source + (item.Destination == null ? "" : " -> " + item.Destination) +
                                (item.Error == null ? "" : " — " + item.Error),
                            item.Error == null ? Color.Lime : Color.OrangeRed);
                    });
                var result = await new Worker().RunVanisherAsync(
                    cwd, rules, sets, Properties.Settings.Default.deleteAfterCopy,
                    Properties.Settings.Default.overWriteFiles, progress, cancellation.Token);
                Log(string.Format(resourceManager.GetString("runSummary"), result.Copied, result.Moved, result.Failed,
                                  result.Skipped),
                    Color.Cyan);
            }
            catch (OperationCanceledException)
            {
                Log(resourceManager.GetString("runCancelled"), Color.Gold);
            }
            catch (Exception error)
            {
                Log(error.Message, Color.OrangeRed);
            }
            finally
            {
                running = false;
                cancellation.Dispose();
                cancellation = null;
                if (!IsDisposed)
                    mainTask.Enabled = true;
            }
        }
        private void Log(string message, Color color)
        {
            if (IsDisposed)
                return;
            logRichTextBox.SelectionStart = logRichTextBox.TextLength;
            logRichTextBox.SelectionColor = color;
            logRichTextBox.AppendText("[" + DateTime.Now + "] - " + message + Environment.NewLine);
            logRichTextBox.SelectionColor = logRichTextBox.ForeColor;
        }
        private void GlobalSettings_Click(object sender, EventArgs e)
        {
            var form = Application.OpenForms["globalSettingsForm"];
            if (form != null)
                form.Activate();
            else
                new globalSettingsForm().Show();
        }
        private void ClearLogItem_Click(object sender, EventArgs e)
        {
            logRichTextBox.Clear();
        }
    }
}
