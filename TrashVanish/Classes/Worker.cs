﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrashVanish.Classes;

namespace TrashVanish
{
    internal class Worker
    {
        private readonly RichTextBox box;
        private ResourceManager resourceManager;

        public Worker(RichTextBox richTextBox)
        {
            box = richTextBox;
            resourceManager = new ResourceManager("TrashVanish.lang_" + System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, Assembly.GetExecutingAssembly());
        }

        private readonly List<string> affectedFiles = new List<string>();

        /// <summary>
        /// Запускает потоки для каждой задачи
        /// </summary>
        /// <param name="cwd">Текущая рабочая директория (рабочий стол)</param>
        /// <param name="rules">Список правил</param>
        /// <param name="sets">Список наборов</param>
        /// <param name="deleteFile">Флаг удаления файлов после копирования</param>
        /// <param name="owFiles">Флаг перезаписи файлов если уже есть в конечной директории</param>
        async public void RunVanisher(string cwd, List<RuleModel> rules, List<SetModel> sets, bool deleteFile, bool owFiles)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            Logger(resourceManager.GetString("start"), Color.Lime);
            List<RuleModel> complexRules = new List<RuleModel>();
            List<RuleModel> simpleRules = new List<RuleModel>();
            List<Task> tasks = new List<Task>();
            foreach (RuleModel rule in rules)
            {
                if (rule.ruleIncludes != "")
                {
                    complexRules.Add(rule);
                }
                else
                {
                    simpleRules.Add(rule);
                }
            }
            List<RuleModel> caseSensitiveRules = new List<RuleModel>();
            List<RuleModel> caseInsensitiveRules = new List<RuleModel>();
            foreach (RuleModel rule in complexRules)
            {
                if (rule.ruleIsCaseSensetive == 1)
                {
                    caseSensitiveRules.Add(rule);
                    //complexRules.Remove(rule);// В комплексных правилах не останется правил с учетом регистра UPD нельзя удалять элементы кллекции по которой ходишь циклом
                }
                else
                {
                    caseInsensitiveRules.Add(rule);
                }
            }
            foreach (RuleModel rule in caseSensitiveRules)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    Work(cwd, rule.ruleExtension, rule.ruleIncludes, rule.rulePath, deleteFile, owFiles, rule.ruleIsCaseSensetive);
                }));
            }
            await Task.WhenAll(tasks.ToArray());
            Logger(resourceManager.GetString("caseSensetiveTasksWithCmplxRulesAreDone"), Color.MediumSpringGreen);
            tasks.Clear();
            foreach (RuleModel rule in caseInsensitiveRules)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    Work(cwd, rule.ruleExtension, rule.ruleIncludes, rule.rulePath, deleteFile, owFiles, rule.ruleIsCaseSensetive);
                }));
            }
            await Task.WhenAll(tasks.ToArray());
            Logger(resourceManager.GetString("complexRulesAreDone"), Color.MediumSpringGreen);
            tasks.Clear();
            foreach (RuleModel rule in simpleRules)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    Work(cwd, rule.ruleExtension, rule.ruleIncludes, rule.rulePath, deleteFile, owFiles, rule.ruleIsCaseSensetive);
                }));
            }
            await Task.WhenAll(tasks.ToArray());
            complexRules.Clear();
            simpleRules.Clear();
            Logger(resourceManager.GetString("rulesAreDone"), Color.MediumSpringGreen);
            tasks.Clear();
            foreach (SetModel set in sets)
            {
                foreach (setExtensionModel ext in set.extensions)
                {
                    if (ext.includes != "")
                    {
                        complexRules.Add(new RuleModel { ruleExtension = ext.extension, ruleIncludes = ext.includes, ruleIsCaseSensetive = set.isCaseSensetive, rulePath = set.targetPath });
                    }
                    else
                    {
                        simpleRules.Add(new RuleModel { ruleExtension = ext.extension, ruleIncludes = ext.includes, ruleIsCaseSensetive = set.isCaseSensetive, rulePath = set.targetPath });
                    }
                }
            }
            caseSensitiveRules.Clear();
            caseInsensitiveRules.Clear();

            foreach (RuleModel rule in complexRules)
            {
                if (rule.ruleIsCaseSensetive == 1)
                {
                    caseSensitiveRules.Add(rule);
                }
                else
                {
                    caseInsensitiveRules.Add(rule);
                }
            }

            foreach (RuleModel rule in caseSensitiveRules)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    Work(cwd, rule.ruleExtension, rule.ruleIncludes, rule.rulePath, deleteFile, owFiles, rule.ruleIsCaseSensetive);
                }));
            }
            await Task.WhenAll(tasks.ToArray());
            Logger(resourceManager.GetString("caseSensetiveSetsWithCmplxRulesAreDone"), Color.MediumSpringGreen);
            tasks.Clear();
            foreach (RuleModel rule in caseInsensitiveRules)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    Work(cwd, rule.ruleExtension, rule.ruleIncludes, rule.rulePath, deleteFile, owFiles, rule.ruleIsCaseSensetive);
                }));
            }
            await Task.WhenAll(tasks.ToArray());
            Logger(resourceManager.GetString("complexSetsAreDone"), Color.MediumSpringGreen);
            tasks.Clear();
            foreach (RuleModel rule in simpleRules)
            {
                tasks.Add(Task.Factory.StartNew(() =>
                {
                    Work(cwd, rule.ruleExtension, rule.ruleIncludes, rule.rulePath, deleteFile, owFiles, rule.ruleIsCaseSensetive);
                }));
            }
            await Task.WhenAll(tasks.ToArray());
            Logger(resourceManager.GetString("setsAreDone"), Color.MediumSpringGreen);
            await Task.WhenAll(tasks.ToArray());
            watch.Stop();
            Logger(string.Format(resourceManager.GetString("tasksAreDoneForElapsedTime"), watch.ElapsedMilliseconds), Color.Lime);
        }

        private void Work(string cwd, string extension, string includes, string targetpath, bool deleteFile, bool owFiles, int isCaseSensetive)
        {
            int filesCopied = 0;
            int errors = 0;
            string[] files = Directory.GetFiles(cwd, "*" + includes + "*" + extension);
            if (files.Length < 1)
            {
                if (includes != "")
                {
                    Logger(string.Format(resourceManager.GetString("noFilesForExtAndInclude"), extension, includes), Color.DarkOrange);
                }
                else
                {
                    Logger(string.Format(resourceManager.GetString("noFilesForExt"), extension), Color.DarkOrange);
                }

                return;
            }
            if (!Directory.Exists(targetpath))
            {
                Directory.CreateDirectory(targetpath);
            }
            foreach (string file in files)
            {
                bool isContains = false;
                bool doNotDelete = false;
                string filename = Path.GetFileName(file);
                string destination = Path.Combine(targetpath, filename);

                if (isCaseSensetive == 1)
                {
                    if (filename.Contains(includes))
                    {
                        isContains = true;
                    }
                }
                else
                {
                    if (filename.ToLower().Contains(includes.ToLower()))
                    {
                        isContains = true;
                    }
                }
                if (isContains)
                {
                    if (File.Exists(destination) & !owFiles)
                    {
                        Logger(string.Format(resourceManager.GetString("fileAlreadyExist"), filename, targetpath), Color.Gold);
                        continue;
                    }
                    if (NotAffected(file))
                    {
                        try
                        {
                            File.Copy(file, destination, owFiles);
                        }
                        catch (UnauthorizedAccessException uae)
                        {
                            //Logger(uae.Message + ". Чтобы скопировать файл в \"" + targetpath + "\" запустите программу от имени администратора", Color.Maroon);
                            Logger(string.Format(resourceManager.GetString("unauthorizedAccessExceptionMessage"), uae.Message, targetpath), Color.Maroon);
                            errors++;
                            doNotDelete = true;
                        }
                        catch (Exception e)
                        {
                            Logger(e.Message, Color.Maroon);
                            errors++;
                            doNotDelete = true;
                        }
                        affectedFiles.Add(file);
                    }
                    else
                    {
                        continue;
                    }
                    filesCopied++;
                    if (deleteFile)
                    {
                        if (!doNotDelete) // Если произошла ошибка при копировании - оставить оригинал
                        {
                            try
                            {
                                File.Delete(file);
                            }
                            catch (Exception e)
                            {
                                Logger(e.Message, Color.Maroon);
                                errors++;
                            }
                        }
                    }
                }
            }
            if (errors != 0)
            {
                Logger(string.Format(resourceManager.GetString("notAllFilesWithExtWereMoved"), extension), Color.OrangeRed);
            }
            else
            {
                if (includes != "")
                {
                    Logger(string.Format(resourceManager.GetString("taskCompletedWithIncludes"), extension, includes, filesCopied), Color.Lime);
                }
                else
                {
                    Logger(string.Format(resourceManager.GetString("taskCompleted"), extension, filesCopied), Color.Lime);
                }
            }
        }

        private bool NotAffected(string file)
        {
            foreach (string affectedFile in affectedFiles)
            {
                if (file == affectedFile)
                {
                    return false;
                }
            }
            return true;
        }

        private void Logger(string message, Color color)
        {
            Action action = () =>
            {
                box.SelectionStart = box.TextLength;
                box.SelectionLength = 0;

                box.SelectionColor = color;
                box.AppendText("[" + DateTime.Now + "]" + " - " + message + "\r\n");
                box.SelectionColor = box.ForeColor;
            };
            if (box.InvokeRequired)
            {
                box.Invoke(action);
            }
            else
            {
                action();
            }
        }
    }
}