using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrashVanish.Classes;

namespace TrashVanish.Forms.SetsForms
{
    public partial class AddExtensionSet : Form
    {
        private ResourceManager resourceManager;
        private DataGridView gridView;

        public AddExtensionSet(DataGridView dataGridView)
        {
            InitializeComponent();
            this.Icon = Properties.Resources.appicon;
            gridView = dataGridView;
            resourceManager = currentCulture.GlobalResourseManager;
        }

        private void addExtensionToSetButton_Click(object sender, EventArgs e)
        {
            addExtToList();
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

        private void deleteExtensionFromListButton_Click(object sender, EventArgs e)
        {
            if (extensionsList.SelectedRows.Count > 0)
                extensionsList.Rows.RemoveAt(extensionsList.SelectedRows[0].Index);
        }

        private void addSetButton_Click(object sender, EventArgs e)
        {
            try
            {

                if (setNameTextBox.Text.Trim() == "" || extensionsList.Rows.Count == 0 ||
                    targetPathTextBox.Text.Trim() == "")
                {
                    messageLabelFunc(resourceManager.GetString("ReqFieldsAreEmpty"), Color.DarkOrange);
                    return;
                }
                if (!pathValidate(targetPathTextBox.Text))
                {
                    return;
                }
                string setName = setNameTextBox.Text.Trim();
                int isCaseSensetive = isCaseSensetiveCheckBox.Checked ? 1 : 0;
                Dictionary<string, string> setExtensionsAndIncludes =
                    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (DataGridViewRow listItem in extensionsList.Rows)
                {
                    if (listItem.IsNewRow)
                        continue;
                    string ext = Convert.ToString(listItem.Cells[0].Value).Trim();
                    if (ext.Length == 0 || setExtensionsAndIncludes.ContainsKey(ext))
                    {
                        messageLabelFunc(resourceManager.GetString("duplicateExtension"), Color.DarkOrange);
                        return;
                    }
                    setExtensionsAndIncludes.Add(ext, Convert.ToString(listItem.Cells[1].Value).Trim());
                }
                string targetPath = targetPathTextBox.Text;
                DBConnection.AddSet(setName, setExtensionsAndIncludes, targetPath, isCaseSensetive);
                setNameTextBox.Text = "";
                extensionTextBox.Text = "";
                includesTextBox.Text = "";
                extensionsList.Rows.Clear();
                GridUpdater gridUpdater = new GridUpdater(gridView);
                gridUpdater.UpdateExtensionsSets();
                messageLabelFunc(resourceManager.GetString("setAddedSuccessfully"), Color.Lime);
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void extensionTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar == (char)Keys.Enter)
            {
                addExtToList();
            }
        }

        private void addExtToList()
        {
            string extension = extensionTextBox.Text.Trim();
            string includes = includesTextBox.Text.Trim();
            if (extension.Length == 0)
            {
                messageLabelFunc(resourceManager.GetString("extIsInvalid"), Color.DarkOrange);
                return;
            }
            if (extension[0] != '.')
            {
                extension = '.' + extension;
            }
            if (!extValidate(extension))
            {
                return;
            }
            foreach (DataGridViewRow row in extensionsList.Rows)
            {
                if (!row.IsNewRow &&
                    string.Equals(Convert.ToString(row.Cells[0].Value), extension, StringComparison.OrdinalIgnoreCase))
                {
                    messageLabelFunc(resourceManager.GetString("duplicateExtension"), Color.DarkOrange);
                    return;
                }
            }
            extensionsList.Rows.Add(extension, includes);
            includesTextBox.Text = "";
            extensionTextBox.Text = "";
        }

        private void selectFolderButton_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                targetPathTextBox.Text = folderBrowserDialog.SelectedPath;
            }
        }
    }
}
