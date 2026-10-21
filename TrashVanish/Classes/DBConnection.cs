using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using TrashVanish.Classes;

namespace TrashVanish
{
    // Persistence never displays dialogs. UI operation boundaries own presentation.
    public static class DBConnection
    {
        private static SQLiteConnection Open()
        {
            var builder =
                new SQLiteConnectionStringBuilder(ConfigurationManager.ConnectionStrings["Default"].ConnectionString);
            if (!Path.IsPathRooted(builder.DataSource))
                builder.DataSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, builder.DataSource);
            var connection = new SQLiteConnection(builder.ConnectionString);
            connection.Open();
            return connection;
        }
        public static void CheckDB()
        {
            using (var connection = Open()) using (var transaction = connection.BeginTransaction())
            {
                Execute(
                    connection, transaction,
                    "CREATE TABLE IF NOT EXISTS rulesTable(id INTEGER PRIMARY KEY AUTOINCREMENT,extension TEXT NOT " +
                        "NULL,includes TEXT,isCaseSensetive INTEGER NOT NULL,path TEXT NOT NULL)");
                Execute(
                    connection, transaction,
                    "CREATE TABLE IF NOT EXISTS extensionSetsTable(id INTEGER PRIMARY KEY AUTOINCREMENT,name TEXT " +
                        "NOT NULL UNIQUE,isCaseSensetive INTEGER NOT NULL,targetPath TEXT NOT NULL)");
                Execute(connection, transaction,
                        "CREATE TABLE IF NOT EXISTS extensionsForSetsTable(id INTEGER PRIMARY KEY " +
                            "AUTOINCREMENT,setNameId INTEGER NOT NULL,extension TEXT NOT NULL,includes TEXT NOT NULL)");
                transaction.Commit();
            }
        }
        private static int Execute(SQLiteConnection connection, SQLiteTransaction transaction, string sql,
                                   params object[] values)
        {
            using (var command = new SQLiteCommand(sql, connection, transaction))
            {
                for (int i = 0; i < values.Length; i++)
                    command.Parameters.AddWithValue("@p" + i, values[i] ?? DBNull.Value);
                return command.ExecuteNonQuery();
            }
        }
        public static List<RuleModel> LoadRules()
        {
            using (var connection = Open()) using (
                var command =
                    new SQLiteCommand("SELECT id,extension,includes,isCaseSensetive,path FROM rulesTable ORDER BY id",
                                      connection)) using (var reader = command.ExecuteReader())
            {
                var rules = new List<RuleModel>();
                while (reader.Read())
                    rules.Add(new RuleModel { ruleID = Convert.ToString(reader["id"]),
                                              ruleExtension = Convert.ToString(reader["extension"]),
                                              ruleIncludes = Convert.ToString(reader["includes"]),
                                              ruleIsCaseSensetive = Convert.ToInt32(reader["isCaseSensetive"]),
                                              rulePath = Convert.ToString(reader["path"]) });
                return rules;
            }
        }
        public static bool IsRuleExist(string extension, string includes, int sensitive, string excludedID = null)
        {
            using (var connection = Open()) using (
                var command =
                    new SQLiteCommand("SELECT COUNT(*) FROM rulesTable WHERE extension=@extension COLLATE " +
                                          "NOCASE AND COALESCE(includes,'')=@includes AND " +
                                          "isCaseSensetive=@sensitive AND (@excluded IS NULL OR id<>@excluded)",
                                      connection))
            {
                command.Parameters.AddWithValue("@extension", extension);
                command.Parameters.AddWithValue("@includes", includes);
                command.Parameters.AddWithValue("@sensitive", sensitive);
                command.Parameters.AddWithValue("@excluded", (object)excludedID ?? DBNull.Value);
                return Convert.ToInt32(command.ExecuteScalar()) > 0;
            }
        }
        private static void SaveRule(string id, RuleModel rule)
        {
            using (var connection = Open()) using (var transaction = connection.BeginTransaction())
            {
                using (var duplicate = new SQLiteCommand(
                           "SELECT COUNT(*) FROM rulesTable WHERE extension=@e COLLATE NOCASE AND " +
                               "COALESCE(includes,'')=@i AND isCaseSensetive=@s AND (@id IS NULL OR id<>@id)",
                           connection, transaction))
                {
                    duplicate.Parameters.AddWithValue("@e", rule.ruleExtension);
                    duplicate.Parameters.AddWithValue("@i", rule.ruleIncludes ?? "");
                    duplicate.Parameters.AddWithValue("@s", rule.ruleIsCaseSensetive);
                    duplicate.Parameters.AddWithValue("@id", (object)id ?? DBNull.Value);
                    if (Convert.ToInt32(duplicate.ExecuteScalar()) > 0)
                        throw new InvalidOperationException("A matching rule already exists");
                }
                int changed;
                if (id == null)
                    changed = Execute(
                        connection, transaction,
                        "INSERT INTO rulesTable(extension,includes,isCaseSensetive,path) VALUES(@p0,@p1,@p2,@p3)",
                        rule.ruleExtension, rule.ruleIncludes, rule.ruleIsCaseSensetive, rule.rulePath);
                else
                    changed = Execute(
                        connection, transaction,
                        "UPDATE rulesTable SET extension=@p0,includes=@p1,isCaseSensetive=@p2,path=@p3 WHERE id=@p4",
                        rule.ruleExtension, rule.ruleIncludes, rule.ruleIsCaseSensetive, rule.rulePath, id);
                if (changed != 1)
                    throw new InvalidOperationException("Rule not found");
                transaction.Commit();
            }
        }
        public static void AddRule(RuleModel rule)
        {
            SaveRule(null, rule);
        }
        public static void UpdateRule(string id, RuleModel rule)
        {
            SaveRule(id, rule);
        }
        public static void DeleteRule(string id)
        {
            using (var connection = Open()) Execute(connection, null, "DELETE FROM rulesTable WHERE id=@p0", id);
        }
        public static List<SetModel> LoadSets()
        {
            var sets = new List<SetModel>();
            var lookup = new Dictionary<string, SetModel>();
            using (var connection = Open())
            {
                using (var command = new SQLiteCommand(
                           "SELECT id,name,isCaseSensetive,targetPath FROM extensionSetsTable ORDER BY id",
                           connection)) using (var reader = command.ExecuteReader()) while (reader.Read())
                {
                    var set = new SetModel { setID = Convert.ToString(reader["id"]),
                                             setName = Convert.ToString(reader["name"]),
                                             isCaseSensetive = Convert.ToInt32(reader["isCaseSensetive"]),
                                             targetPath = Convert.ToString(reader["targetPath"]),
                                             extensions = new List<setExtensionModel>() };
                    sets.Add(set);
                    lookup.Add(set.setID, set);
                }
                using (var command = new SQLiteCommand(
                           "SELECT id,setNameId,extension,includes FROM extensionsForSetsTable ORDER BY id",
                           connection)) using (var reader = command.ExecuteReader()) while (reader.Read())
                {
                    var extension = new setExtensionModel { extensionID = Convert.ToString(reader["id"]),
                                                            setNameID = Convert.ToString(reader["setNameId"]),
                                                            extension = Convert.ToString(reader["extension"]),
                                                            includes = Convert.ToString(reader["includes"]) };
                    SetModel set;
                    if (!lookup.TryGetValue(extension.setNameID, out set))
                        throw new InvalidDataException("Orphan extension entry in database");
                    set.extensions.Add(extension);
                }
            }
            return sets;
        }
        public static SetModel LoadSetByID(string id)
        {
            var set = LoadSets().SingleOrDefault(s => s.setID == id);
            if (set == null)
                throw new InvalidOperationException("Set not found");
            return set;
        }
        public static void AddSet(string name, Dictionary<string, string> extensions, string path, int sensitive)
        {
            SaveSet(null, name, extensions, path, sensitive);
        }
        public static void UpdateSet(string id, string name, Dictionary<string, string> extensions, string path,
                                     int sensitive)
        {
            SaveSet(id, name, extensions, path, sensitive);
        }
        private static void SaveSet(string id, string name, Dictionary<string, string> extensions, string path,
                                    int sensitive)
        {
            var unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var extension in extensions.Keys)
                if (!unique.Add(extension.Trim()))
                    throw new ArgumentException("Duplicate extension");
            using (var connection = Open()) using (var transaction = connection.BeginTransaction())
            {
                if (id == null)
                {
                    Execute(connection, transaction,
                            "INSERT INTO extensionSetsTable(name,isCaseSensetive,targetPath) VALUES(@p0,@p1,@p2)", name,
                            sensitive, path);
                    id = connection.LastInsertRowId.ToString();
                }
                else
                {
                    if (Execute(
                            connection, transaction,
                            "UPDATE extensionSetsTable SET name=@p0,isCaseSensetive=@p1,targetPath=@p2 WHERE id=@p3",
                            name, sensitive, path, id) != 1)
                        throw new InvalidOperationException("Set not found");
                    Execute(connection, transaction, "DELETE FROM extensionsForSetsTable WHERE setNameId=@p0", id);
                }
                foreach (var entry in extensions)
                    Execute(connection, transaction,
                            "INSERT INTO extensionsForSetsTable(setNameId,extension,includes) VALUES(@p0,@p1,@p2)", id,
                            entry.Key.Trim(), entry.Value);
                transaction.Commit();
            }
        }
        public static void DeleteSet(string id)
        {
            using (var connection = Open()) using (var transaction = connection.BeginTransaction())
            {
                Execute(connection, transaction, "DELETE FROM extensionsForSetsTable WHERE setNameId=@p0", id);
                Execute(connection, transaction, "DELETE FROM extensionSetsTable WHERE id=@p0", id);
                transaction.Commit();
            }
        }
    }
}
