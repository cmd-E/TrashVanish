using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Forms;
using TrashVanish.Classes;
using Xunit;

namespace TrashVanish.Tests
{
    [Trait("Category", "Integration")]
    public class GridUpdaterTests
    {
        private static void OnSta(Action action)
        {
            ExceptionDispatchInfo failure = null;
            var thread = new Thread(() =>
            {
                try { action(); }
                catch (Exception error) { failure = ExceptionDispatchInfo.Capture(error); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (failure != null) failure.Throw();
        }

        private static DataGridView Grid()
        {
            var grid = new DataGridView { AllowUserToAddRows = false };
            for (int i = 0; i < 5; i++) grid.Columns.Add("c" + i, "c" + i);
            grid.Rows.Add("old", "old", "old", "old", "old");
            return grid;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void FailedLoadPreservesDisplayedRows(bool rules)
        {
            OnSta(() =>
            {
                using (var grid = Grid())
                {
                    var updater = new GridUpdater(grid,
                        () => { throw new InvalidOperationException("database unavailable"); },
                        () => { throw new InvalidOperationException("database unavailable"); });

                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        if (rules) updater.UpdateRules(); else updater.UpdateExtensionsSets();
                    });

                    Assert.Equal("old", Assert.Single(grid.Rows.Cast<DataGridViewRow>()).Cells[0].Value);
                }
            });
        }

        [Fact]
        public void ReloadReplacesRulesWithoutDuplicatingRows()
        {
            OnSta(() =>
            {
                using (var grid = Grid())
                {
                    var updater = new GridUpdater(grid,
                        () => new List<RuleModel> { RulePlannerTests.Rule() }, () => new List<SetModel>());

                    updater.UpdateRules();
                    updater.UpdateRules();

                    var row = Assert.Single(grid.Rows.Cast<DataGridViewRow>());
                    Assert.Equal("1", row.Cells[0].Value);
                    Assert.Equal(".txt", row.Cells[1].Value);
                    Assert.Equal(@"C:\target", row.Cells[4].Value);
                }
            });
        }

        [Fact]
        public void ReloadDisplaysSetExtensionsAndClearsStaleRowsOnEmptyLoad()
        {
            OnSta(() =>
            {
                using (var grid = Grid())
                {
                    var sets = new List<SetModel> { new SetModel { setID = "2", setName = "Documents",
                        extensions = new List<setExtensionModel> {
                            new setExtensionModel { extension = ".txt" },
                            new setExtensionModel { extension = ".doc" } } } };
                    var updater = new GridUpdater(grid, () => new List<RuleModel>(), () => sets);

                    updater.UpdateExtensionsSets();

                    Assert.Equal(".txt; .doc; ", Assert.Single(grid.Rows.Cast<DataGridViewRow>()).Cells[2].Value);
                    sets.Clear();
                    updater.UpdateExtensionsSets();
                    Assert.Empty(grid.Rows);
                    updater.UpdateRules();
                    Assert.Empty(grid.Rows);
                }
            });
        }
    }
}
