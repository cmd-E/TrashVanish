﻿using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace TrashVanish
{
    public partial class AddRule : Form
    {
        public AddRule()
        {
            InitializeComponent();
        }

        private string path, extension, includes;

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
            includes = includesTextBox.Text;
            path = pathTextBox.Text;
            extension = ExtensionTextBox.Text;
            path = path.Trim();
            extension = extension.Trim();
            if (extension == "" || path == "")
            {
                messageLabelFunc("Обязательные поля не заполнены", Color.DarkOrange);
                return;
            }
            if (!extValidate(extension) || !pathValidate(path))
            {
                return;
            }

            if (DBConnection.isRuleExist(extension, includes))
            {
                messageLabelFunc("Правило для \"" + extension + "\" уже существует", Color.DarkOrange);
                return;
            }
            RuleModel rule = new RuleModel { ruleExtension = extension, ruleIncludes = includes, rulePath = path };
            DBConnection.AddRule(rule);
            messageLabelFunc("Правило для \"" + extension + "\" создано", Color.Lime);
            ExtensionTextBox.Text = "";
            includesTextBox.Text = "";
            pathTextBox.Text = "";
            GridUpdater gu = new GridUpdater();
            gu.UpdateRules();
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

        private bool extValidate(string extension)
        {
            if (extension[0] == '.')
            {
                extension = sliceDot(extension);
            }
            else
            {
                messageLabelFunc("Расширение не корректно", Color.DarkOrange);
                return false;
            }
            if ((char)extension.Length == 0)
            {
                messageLabelFunc("Расширение не корректно", Color.DarkOrange);
                return false;
            }
            foreach (char l in extension)
            {
                if (!(l >= 65) && !(l <= 90) || !(l >= 97) && !(l <= 122))
                {
                    messageLabelFunc("Расширение не корректно", Color.DarkOrange);
                    return false;
                }
            }
            return true;
        }

        private void ExtensionTextBox_DropDownClosed(object sender, EventArgs e)
        {
            if (ExtensionTextBox.SelectedItem.ToString() == "Добавить набор")
            {
                MessageBox.Show("Добавить набор");
                // Открыть окно "добавить набор"
            }
            else if (ExtensionTextBox.SelectedItem.ToString() == "Изменить наборы")
            {
                MessageBox.Show("Изменить наборы");
                // Открыть окно "изменить наборы"
            }
        }

        private bool pathValidate(string path)
        {
            try
            {
                Path.GetFullPath(path);
            }
            catch
            {
                messageLabelFunc("Путь не корректен", Color.DarkOrange);
                return false;
            }
            if (!Path.IsPathRooted(path))
            {
                messageLabelFunc("Относительные пути запрещены", Color.DarkOrange);
                return false;
            }
            return true;
        }

        private string sliceDot(string str)
        {
            string newstr = "";
            for (int i = 1; i < str.Length; i++)
            {
                newstr = newstr + str[i];
            }
            return newstr;
        }
    }
}