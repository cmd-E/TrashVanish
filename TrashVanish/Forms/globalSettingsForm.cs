using System;
using System.Windows.Forms;
using TrashVanish.Classes;
using TrashVanish.Forms.RulesForms;
using TrashVanish.Forms.SetsForms;

namespace TrashVanish.Forms
{
    public partial class globalSettingsForm : Form
    {
        public globalSettingsForm()
        {
            InitializeComponent();
            this.Icon = Properties.Resources.appicon;
        }

        private GridUpdater rulesUpdater;
        private GridUpdater setsUpdater;

        private void ClearLogCheckbox_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.clearLog = clearLogCheckbox.Checked;
            Properties.Settings.Default.Save();
        }

        private void OverwriteFiles_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.overWriteFiles = overwriteFiles.Checked;
            Properties.Settings.Default.Save();
        }

        private void DeleteFlag_CheckedChanged(object sender, EventArgs e)
        {
            Properties.Settings.Default.deleteAfterCopy = deleteFlag.Checked;
            Properties.Settings.Default.Save();
        }

        private void GlobalSettingsForm_Load(object sender, EventArgs e)
        {
            try
            {

                LoadCheckboxSettings();
                rulesUpdater = new GridUpdater(rulesGrid);
                setsUpdater = new GridUpdater(extensionsSetGrid);

                rulesUpdater.UpdateRules();
                setsUpdater.UpdateExtensionsSets();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void LoadCheckboxSettings()
        {
            clearLogCheckbox.Checked = Properties.Settings.Default.clearLog;
            overwriteFiles.Checked = Properties.Settings.Default.overWriteFiles;
            deleteFlag.Checked = Properties.Settings.Default.deleteAfterCopy;
        }

        private void RulesAndSetsTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {

                if (rulesAndSetsTabControl.SelectedTab == rulesAndSetsTabControl.TabPages["RulesSettingsTab"])
                {
                    rulesUpdater.UpdateRules();
                }
                else
                {
                    setsUpdater.UpdateExtensionsSets();
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private bool RulesAreExist()
        {
            int selectedrow;
            try
            {
                selectedrow = rulesGrid.CurrentCell.RowIndex;
            }
            catch (NullReferenceException)
            {
                // No rules in grid yet
                return false;
            }
            return true;
        }

        // RULES CONTEXT MENU
        private void UpdateGridItem_Click(object sender, EventArgs e)
        {
            try
            {

                rulesUpdater.UpdateRules();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddRuleItem_Click(object sender, EventArgs e)
        {
            try
            {

                Form ar = Application.OpenForms["AddRule"];
                if (ar != null)
                {
                    ar.Activate();
                    return;
                }
                else
                {
                    AddRule addRuleForm = new AddRule(rulesGrid);
                    addRuleForm.ShowDialog();
                }
                rulesUpdater.UpdateRules();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EditRuleItem_Click(object sender, EventArgs e)
        {
            try
            {

                if (!RulesAreExist())
                {
                    return;
                }
                Form er = Application.OpenForms["editRule"];
                if (er != null)
                {
                    er.Activate();
                    return;
                }
                else
                {
                    // editRuleForm editRuleForm = new editRuleForm(rulesGrid);
                    editRuleForm erf = new editRuleForm(rulesGrid);
                    erf.ShowDialog();
                }
                rulesUpdater.UpdateRules();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteRuleItem_Click(object sender, EventArgs e)
        {
            try
            {

                if (!RulesAreExist())
                {
                    return;
                }
                int selectedrow = rulesGrid.CurrentCell.RowIndex;
                ;
                int columnbydefault = 0; // column with id (invisible)
                DBConnection.DeleteRule(rulesGrid.Rows[selectedrow].Cells[columnbydefault].Value.ToString());
                rulesUpdater.UpdateRules();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // SETS CONTEXT MENU

        private void UpdateSetGridItem_Click(object sender, EventArgs e)
        {
            try
            {

                setsUpdater.UpdateExtensionsSets();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void AddSetItem_Click(object sender, EventArgs e)
        {
            Form AES = Application.OpenForms["AddExtensionSet"];
            if (AES != null)
            {
                AES.Activate();
                return;
            }
            else
            {
                AddExtensionSet addExtensionSet = new AddExtensionSet(extensionsSetGrid);
                addExtensionSet.ShowDialog();
            }
        }

        private void EditSetItem_Click(object sender, EventArgs e)
        {
            try
            {

                Form esf = Application.OpenForms["editSetForm"];
                if (esf != null)
                {
                    esf.Activate();
                    return;
                }
                else
                {
                    int selectedrow;
                    try
                    {
                        selectedrow = extensionsSetGrid.CurrentCell.RowIndex;
                    }
                    catch (NullReferenceException)
                    {
                        // Mo rules in grid yet
                        return;
                    }
                    int columnbydefault = 0; // column with id (invisible)
                    SetModel setToEdit = DBConnection.LoadSetByID(
                        extensionsSetGrid.Rows[selectedrow].Cells[columnbydefault].Value.ToString());
                    editSetForm editSetForm = new editSetForm(setToEdit, extensionsSetGrid);
                    editSetForm.Show();
                }
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void DeleteSetItem_Click(object sender, EventArgs e)
        {
            try
            {

                // Удалить набор
                int selectedrow;
                try
                {
                    selectedrow = extensionsSetGrid.CurrentCell.RowIndex;
                }
                catch (NullReferenceException)
                {
                    // Mo rules in grid yet
                    return;
                }
                int columnbydefault = 0; // column with id (invisible)
                DBConnection.DeleteSet(extensionsSetGrid.Rows[selectedrow].Cells[columnbydefault].Value.ToString());
                setsUpdater.UpdateExtensionsSets();
            }
            catch (Exception error)
            {
                MessageBox.Show(error.Message, currentCulture.GlobalResourseManager.GetString("dbInteractionError"),
                                MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
