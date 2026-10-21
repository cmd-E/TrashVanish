using System;
using System.Collections.Generic;
using System.Windows.Forms;
using TrashVanish.Classes;

namespace TrashVanish
{
    internal class GridUpdater
    {
        private readonly DataGridView dataGrid;
        private readonly Func<List<RuleModel>> loadRules;
        private readonly Func<List<SetModel>> loadSets;

        public GridUpdater(DataGridView dataGrid) : this(dataGrid, DBConnection.LoadRules, DBConnection.LoadSets)
        {
        }

        internal GridUpdater(DataGridView dataGrid, Func<List<RuleModel>> loadRules, Func<List<SetModel>> loadSets)
        {
            this.dataGrid = dataGrid;
            this.loadRules = loadRules;
            this.loadSets = loadSets;
        }

        /// <summary>
        /// Reloads rules from database
        /// </summary>
        public void UpdateRules()
        {
            // Keep the displayed data if loading fails.
            List<RuleModel> rules = loadRules();
            dataGrid.Rows.Clear();
            foreach (RuleModel rule in rules)
            {
                dataGrid.Rows.Add(rule.ruleID, rule.ruleExtension, rule.ruleIncludes, rule.ruleIsCaseSensetive,
                                  rule.rulePath);
            }
        }

        /// <summary>
        /// Reloads extension sets from database
        /// </summary>
        public void UpdateExtensionsSets()
        {
            List<SetModel> sets = loadSets();
            dataGrid.Rows.Clear();
            foreach (SetModel set in sets)
            {
                dataGrid.Rows.Add(set.setID, set.setName, stringifyExtensions(set.extensions), set.isCaseSensetive,
                                  set.targetPath); // make string with all extensions
            }
        }

        private string stringifyExtensions(List<setExtensionModel> extensions)
        {
            string newstr = "";
            foreach (setExtensionModel ext in extensions)
            {
                newstr = newstr + ext.extension + "; ";
            }
            return newstr;
        }
    }
}
