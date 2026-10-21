using System;
using System.Collections.Generic;
using System.Linq;
using TrashVanish.Classes;
using Xunit;

namespace TrashVanish.Tests
{
    public class RulePlannerTests
    {
        internal static RuleModel Rule(string id = "1", string destination = @"C:\target",
                                       string includes = "", int sensitive = 0, string extension = ".txt")
        {
            return new RuleModel { ruleID = id, rulePath = destination, ruleIncludes = includes,
                ruleIsCaseSensetive = sensitive, ruleExtension = extension };
        }

        internal static IList<PlannedRule> Plan(params RuleModel[] rules)
        {
            return RulePlanner.Build(rules, new SetModel[0]);
        }

        [Fact]
        public void EmptyConfigurationProducesEmptyPlan()
        {
            Assert.Empty(Plan());
        }

        [Theory]
        [InlineData("report.txt", "", 0, true)]
        [InlineData("report.TXT", "", 0, true)]
        [InlineData("report.txtx", "", 0, false)]
        [InlineData("reporttxt", "", 0, false)]
        [InlineData("report.txt.bak", "", 0, false)]
        [InlineData("REPORT.txt", "report", 0, true)]
        [InlineData("REPORT.txt", "report", 1, false)]
        [InlineData("report.txt", "report", 1, true)]
        [InlineData(@"C:\report\other.txt", "report", 0, false)]
        public void MatchingUsesExactExtensionAndOnlyTheFileName(string file, string includes,
                                                                 int sensitive, bool expected)
        {
            var rule = Assert.Single(Plan(Rule(includes: includes, sensitive: sensitive)));

            Assert.Equal(expected, rule.Matches(file));
        }

        [Fact]
        public void PrioritizesSpecificRulesBeforeGeneralRulesAndSets()
        {
            var rules = new[] { Rule("10"), Rule("2"), Rule("4", includes: "a"),
                Rule("3", includes: "a", sensitive: 1) };
            var sets = new[] { new SetModel { setID = "1", targetPath = @"C:\sets", isCaseSensetive = 1,
                extensions = new List<setExtensionModel> {
                    new setExtensionModel { extensionID = "20", extension = ".txt", includes = "a" },
                    new setExtensionModel { extensionID = "11", extension = ".txt", includes = "" } } } };

            var plan = RulePlanner.Build(rules, sets);

            Assert.Equal(new long[] { 3, 4, 2, 10, 20, 11 }, plan.Select(r => r.ID));
        }

        [Fact]
        public void PlanIsReadOnlyAndIndependentOfLaterModelEdits()
        {
            var input = Rule(includes: null);
            var plan = Plan(input);
            input.ruleExtension = ".pdf";

            Assert.Equal(".txt", plan[0].Extension);
            Assert.Equal("", plan[0].Includes);
            Assert.Throws<NotSupportedException>(() => plan.Add(plan[0]));
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("txt")]
        [InlineData(".t/xt")]
        [InlineData(".*")]
        public void RejectsInvalidPersistedExtensions(string extension)
        {
            Assert.Throws<ArgumentException>(() => Plan(Rule(extension: extension)));
        }

        [Theory]
        [InlineData("relative")]
        [InlineData("")]
        public void RejectsRelativeDestinations(string destination)
        {
            Assert.Throws<ArgumentException>(() => Plan(Rule(destination: destination)));
        }

        [Theory]
        [InlineData("abc")]
        [InlineData("")]
        [InlineData("9223372036854775808")]
        public void RejectsInvalidPersistedIds(string id)
        {
            Assert.Throws<FormatException>(() => Plan(Rule(id)));
        }

        [Fact]
        public void NormalizesDestinationPaths()
        {
            var plan = Plan(Rule(destination: @"C:\target\child\.."));

            Assert.Equal(@"C:\target", plan[0].Destination.TrimEnd('\\'));
        }
    }
}
