using System;
using System.Globalization;
using System.Resources;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Data.SQLite;
using TrashVanish;
class DatabaseChecks
{
    static void Check(bool condition, string message)
    {
        if (!condition)
            throw new Exception(message);
    }
    static void Main()
    {
        var culture = typeof(DBConnection).Assembly.GetType("TrashVanish.Classes.currentCulture");
        foreach (string language in new[] { "de-DE", "ru-RU", "en-US" }) {
            System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(language);
            culture.GetMethod("setLanguageForCurrentCulture").Invoke(null, null);
            var resources = (ResourceManager)culture.GetProperty("GlobalResourseManager").GetValue(null, null);
            Check(!string.IsNullOrEmpty(resources.GetString("AppIsLoaded")), "language fallback failed: " + language);
        }
        // The runner places this executable and its configuration in a disposable directory.
        DBConnection.CheckDB();
        string root = AppDomain.CurrentDomain.BaseDirectory;
        DBConnection.AddRule(
            new RuleModel { ruleExtension = ".txt", ruleIncludes = "", ruleIsCaseSensetive = 0, rulePath = root });
        var rule = DBConnection.LoadRules().Single();
        Check(!DBConnection.IsRuleExist(".txt", "", 0, rule.ruleID), "current rule exclusion");
        rule.rulePath = Path.Combine(root, "destination");
        DBConnection.UpdateRule(rule.ruleID, rule);
        Check(DBConnection.LoadRules().Single().rulePath == rule.rulePath, "destination-only update");
        var extensions = new Dictionary<string, string> { { ".txt", "" } };
        DBConnection.AddSet("first", extensions, root, 0);
        DBConnection.AddSet("second", extensions, root, 0);
        var first = DBConnection.LoadSets().Single(s => s.setName == "first");
        bool failed = false;
        try
        {
            DBConnection.UpdateSet(first.setID, "second", new Dictionary<string, string> { { ".doc", "" } }, root, 1);
        }
        catch (SQLiteException)
        {
            failed = true;
        }
        Check(failed, "duplicate name accepted");
        var preserved = DBConnection.LoadSetByID(first.setID);
        Check(preserved.setName == "first" && preserved.extensions.Single().extension == ".txt",
              "failed update lost old set");
        DBConnection.UpdateSet(first.setID, "first", new Dictionary<string, string> { { ".doc", "" } }, root, 1);
        Check(DBConnection.LoadSetByID(first.setID).extensions.Single().extension == ".doc", "membership update");
        DBConnection.DeleteSet(first.setID);
        using (var connection = new SQLiteConnection("Data Source=" + Path.Combine(root, "trashVanish.db")))
        {
            connection.Open();
            using (var command =
                       new SQLiteCommand("SELECT COUNT(*) FROM extensionsForSetsTable WHERE setNameId=@id", connection))
            {
                command.Parameters.AddWithValue("@id", first.setID);
                Check(Convert.ToInt32(command.ExecuteScalar()) == 0, "orphan membership");
            }
        }
        Console.WriteLine("TrashVanish SQLite rule editing, transaction rollback and deletion checks passed");
    }
}
