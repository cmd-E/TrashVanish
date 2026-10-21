using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using TrashVanish.Classes;

namespace TrashVanish
{
    internal class GridUpdater
    {
        private DataGridView dataGrid;

        public GridUpdater(DataGridView dataGrid)
        {
            this.dataGrid = dataGrid;
        }

        /// <summary>
        /// Reloads rules from database
        /// </summary>
        public void UpdateRules()
        {
            List<RuleModel> rules = DBConnection.LoadRules();
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
            List<SetModel> sets = DBConnection.LoadSets();
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
