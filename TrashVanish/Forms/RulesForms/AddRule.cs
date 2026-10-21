using System;
using System.Drawing;
using System.IO;
using System.Resources;
using System.Windows.Forms;
using TrashVanish.Classes;

namespace TrashVanish
{
    public partial class AddRule : Form
    {
        private DataGridView rulesGrid;
        private ResourceManager resourceManager;

        public AddRule(DataGridView dataGridView)
        {
            InitializeComponent();
            this.Icon = Properties.Resources.appicon;
            rulesGrid = dataGridView;
            resourceManager = currentCulture.GlobalResourseManager;
        }

        private string path, extension, includes;
        private int isCaseSensetive;

        private void browseFilesButton_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                extension = Path.GetExtension(openFileDialog.FileName);
                ExtensionTextBox.Text = extension;
            }
        }

        private void AddRuleButton_Click(object sender, EventArgs e)
        {
            try
            {

                includes = includesTextBox.Text.Trim();
                path = pathTextBox.Text.Trim();
                extension = ExtensionTextBox.Text.Trim();
                isCaseSensetive = isCaseSensetiveCheckBox.Checked
                                      ? 1
                                      : 0; // Since SQLite doesn't have native boolean, I will use digits instead
                if (extension == "" || path == "")
                {
                    messageLabelFunc(resourceManager.GetString("ReqFieldsAreEmpty"), Color.DarkOrange);
                    return;
                }
                if (extension[0] != '.')
                {
                    extension = '.' + extension;
                }
                if (!extValidate(extension) || !pathValidate(path))
                {
                    return;
                }

                if (DBConnection.IsRuleExist(extension, includes, isCaseSensetive))
                {
                    messageLabelFunc(string.Format(resourceManager.GetString("ruleForExtAlreadyExist"), extension),
                                     Color.DarkOrange);
                    return;
                }
                RuleModel rule = new RuleModel { ruleExtension = extension, ruleIncludes = includes,
                                                 ruleIsCaseSensetive = isCaseSensetive, rulePath = path };
                DBConnection.AddRule(rule);
                messageLabelFunc(string.Format(resourceManager.GetString("ruleCreatedSuccessfuly")), Color.Lime);
                ExtensionTextBox.Text = "";
                includesTextBox.Text = "";
                pathTextBox.Text = "";
                GridUpdater gu = new GridUpdater(rulesGrid);
                gu.UpdateRules();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void messageLabelFunc(string message, Color color)
        {
            messageLabel.Visible = true;
            messageLabel.ForeColor = color;
            messageLabel.Text = message;
            var timer = new Timer();
            timer.Interval = 5000;
            timer.Tick += (s, e) =>
            {
                messageLabel.Hide();
                timer.Stop();
                timer.Dispose();
            };
            timer.Start();
        }

        private void browseFolders_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                path = folderBrowserDialog.SelectedPath;
                pathTextBox.Text = path;
            }
        }

        private void isCaseSensetive_CheckedChanged(object sender, EventArgs e)
        {
        }

        private bool extValidate(string extension)
        {
            if (extension.Substring(1).Length == 0)
            {
                messageLabelFunc(resourceManager.GetString("extIsInvalid"), Color.DarkOrange);
                return false;
            }
            foreach (char l in extension.Substring(1))
            {
                if (!(l >= 65 && l <= 90) && !(l >= 97 && l <= 122))
                {
                    messageLabelFunc(resourceManager.GetString("extIsInvalid"), Color.DarkOrange);
                    return false;
                }
            }
            return true;
        }

        private bool pathValidate(string path)
        {
            try
            {
                Path.GetFullPath(path);
            }
            catch
            {
                messageLabelFunc(resourceManager.GetString("pathIsInvalid"), Color.DarkOrange);
                return false;
            }
            if (!Path.IsPathRooted(path))
            {
                messageLabelFunc(resourceManager.GetString("relPathesAreForbidden"), Color.DarkOrange);
                return false;
            }
            return true;
        }
    }
}
