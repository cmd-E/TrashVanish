using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using Xunit;

namespace TrashVanish.Tests
{
    [Trait("Category", "Integration")]
    public class DatabaseStoreTests : IDisposable
    {
        private readonly TemporaryDirectory fixture = new TemporaryDirectory();
        private readonly DatabaseStore store;
        private readonly string connectionString;

        public DatabaseStoreTests()
        {
            connectionString = new SQLiteConnectionStringBuilder {
                DataSource = Path.Combine(fixture.Root, "test.db"), Pooling = false }.ConnectionString;
            store = new DatabaseStore(connectionString);
            store.CheckDB();
        }

        private object Sql(string sql)
        {
            using (var connection = new SQLiteConnection(connectionString))
            {
                connection.Open();
                using (var command = new SQLiteCommand(sql, connection)) return command.ExecuteScalar();
            }
        }

        private string AddRule(string includes = "")
        {
            store.AddRule(RulePlannerTests.Rule(destination: fixture.Root, includes: includes));
            return store.LoadRules().Single().ruleID;
        }

        private string AddSet(string name = "Documents")
        {
            store.AddSet(name, new Dictionary<string, string> { { ".txt", "report" } }, fixture.Root, 0);
            return store.LoadSets().Single(s => s.setName == name).setID;
        }

        [Fact]
        public void SchemaInitializationIsIdempotent()
        {
            store.CheckDB();

            Assert.Empty(store.LoadRules());
            Assert.Empty(store.LoadSets());
        }

        [Fact]
        public void RuleRoundTripSupportsDestinationOnlyUpdateAndDelete()
        {
            string id = AddRule();
            var rule = Assert.Single(store.LoadRules());
            rule.rulePath = Path.Combine(fixture.Root, "new destination");

            store.UpdateRule(id, rule);

            Assert.Equal(rule.rulePath, Assert.Single(store.LoadRules()).rulePath);
            Assert.False(store.IsRuleExist(".txt", "", 0, id));
            Assert.True(store.IsRuleExist(".TXT", "", 0));
            store.DeleteRule(id);
            Assert.Empty(store.LoadRules());
        }

        [Fact]
        public void DuplicateRuleIsRejectedWithoutChangingStoredRule()
        {
            AddRule();
            var duplicate = RulePlannerTests.Rule(destination: fixture.Root, extension: ".TXT");

            Assert.Throws<InvalidOperationException>(() => store.AddRule(duplicate));

            Assert.Equal(".txt", Assert.Single(store.LoadRules()).ruleExtension);
        }

        [Fact]
        public void UpdatingRuleToDuplicateRollsBack()
        {
            string first = AddRule("first");
            store.AddRule(RulePlannerTests.Rule(destination: fixture.Root, includes: "second"));
            var changed = RulePlannerTests.Rule(destination: fixture.Root, includes: "second");

            Assert.Throws<InvalidOperationException>(() => store.UpdateRule(first, changed));

            Assert.Equal("first", store.LoadRules().Single(r => r.ruleID == first).ruleIncludes);
            Assert.Equal(2, store.LoadRules().Count);
        }

        [Fact]
        public void MissingRuleUpdateFailsWithoutInserting()
        {
            Assert.Throws<InvalidOperationException>(() => store.UpdateRule("999", RulePlannerTests.Rule()));

            Assert.Empty(store.LoadRules());
        }

        [Fact]
        public void RuleIncludesAreParametersRatherThanSql()
        {
            const string includes = "x'); DROP TABLE rulesTable; --";
            AddRule(includes);

            Assert.Equal(includes, Assert.Single(store.LoadRules()).ruleIncludes);
            Assert.True(store.IsRuleExist(".txt", includes, 0));
        }

        [Fact]
        public void SetsRoundTripAndMembershipCanBeReplacedAtomically()
        {
            string id = AddSet();

            store.UpdateSet(id, "Renamed", new Dictionary<string, string> { { ".doc", "" }, { ".pdf", "invoice" } },
                fixture.Root, 1);

            var set = store.LoadSetByID(id);
            Assert.Equal("Renamed", set.setName);
            Assert.Equal(1, set.isCaseSensetive);
            Assert.Equal(new[] { ".doc", ".pdf" }, set.extensions.Select(e => e.extension));
            Assert.All(set.extensions, e => Assert.Equal(id, e.setNameID));
        }

        [Fact]
        public void DuplicateNameUpdatePreservesOriginalSetAndMembership()
        {
            string id = AddSet("first");
            AddSet("second");

            Assert.Throws<SQLiteException>(() => store.UpdateSet(id, "second",
                new Dictionary<string, string> { { ".pdf", "" } }, fixture.Root, 1));

            var set = store.LoadSetByID(id);
            Assert.Equal("first", set.setName);
            Assert.Equal(".txt", Assert.Single(set.extensions).extension);
            Assert.Equal(0, set.isCaseSensetive);
        }

        [Fact]
        public void MembershipInsertFailureRollsBackBothMetadataAndDeletedMembership()
        {
            string id = AddSet();
            Sql("CREATE TRIGGER reject_extension BEFORE INSERT ON extensionsForSetsTable " +
                "WHEN NEW.extension='.fail' BEGIN SELECT RAISE(ABORT,'fixture rejection'); END");

            Assert.Throws<SQLiteException>(() => store.UpdateSet(id, "Changed",
                new Dictionary<string, string> { { ".fail", "" } }, fixture.Root, 1));

            var set = store.LoadSetByID(id);
            Assert.Equal("Documents", set.setName);
            Assert.Equal(".txt", Assert.Single(set.extensions).extension);
        }

        [Fact]
        public void FailedSetCreationDoesNotLeavePartialMetadata()
        {
            Sql("CREATE TRIGGER reject_extension BEFORE INSERT ON extensionsForSetsTable " +
                "BEGIN SELECT RAISE(ABORT,'fixture rejection'); END");

            Assert.Throws<SQLiteException>(() => AddSet());

            Assert.Empty(store.LoadSets());
        }

        [Fact]
        public void DuplicateExtensionsIgnoreCaseAndSurroundingWhitespace()
        {
            var entries = new Dictionary<string, string> { { ".txt", "" }, { " .TXT ", "" } };

            Assert.Throws<ArgumentException>(() => store.AddSet("duplicate", entries, fixture.Root, 0));

            Assert.Empty(store.LoadSets());
        }

        [Fact]
        public void SetDeletionRemovesMembershipAndPreservesOtherSets()
        {
            string id = AddSet("first");
            string second = AddSet("second");

            store.DeleteSet(id);

            Assert.Equal(second, Assert.Single(store.LoadSets()).setID);
            Assert.Equal(1L, Convert.ToInt64(Sql("SELECT COUNT(*) FROM extensionsForSetsTable")));
        }

        [Fact]
        public void MissingSetOperationsFailWithoutCreatingData()
        {
            Assert.Throws<InvalidOperationException>(() => store.LoadSetByID("999"));
            Assert.Throws<InvalidOperationException>(() => store.UpdateSet("999", "missing",
                new Dictionary<string, string>(), fixture.Root, 0));

            Assert.Empty(store.LoadSets());
        }

        [Fact]
        public void OrphanMembershipIsReportedInsteadOfSilentlyHidden()
        {
            Sql("INSERT INTO extensionsForSetsTable(setNameId,extension,includes) VALUES(999,'.txt','')");

            Assert.Throws<InvalidDataException>(() => store.LoadSets());
        }

        [Fact]
        public void IndependentStoresCannotSeeEachOthersData()
        {
            AddRule();
            var other = new DatabaseStore(new SQLiteConnectionStringBuilder {
                DataSource = Path.Combine(fixture.Root, "other.db"), Pooling = false }.ConnectionString);
            other.CheckDB();

            Assert.Empty(other.LoadRules());
            Assert.Single(store.LoadRules());
        }

        public void Dispose() { fixture.Dispose(); }
    }
}
