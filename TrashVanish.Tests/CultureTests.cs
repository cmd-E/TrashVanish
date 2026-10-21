using System.Globalization;
using System.Linq;
using TrashVanish.Classes;
using Xunit;

namespace TrashVanish.Tests
{
    public class CultureTests
    {
        [Theory]
        [InlineData("ru-RU", "ru")]
        [InlineData("en-US", "en")]
        [InlineData("fr-CA", "fr")]
        [InlineData("pt-BR", "pt-BR")]
        [InlineData("pt-PT", "pt")]
        [InlineData("zh-CN", "zh-Hans")]
        [InlineData("de-DE", "en")]
        [InlineData("", "en")]
        public void ResolvesRegionalParentAndFallbackResources(string culture, string expected)
        {
            var resources = new[] { "en", "ru", "fr", "pt", "pt-BR", "zh-Hans" }
                .Select(c => "TrashVanish.lang_" + c + ".resources");

            var result = currentCulture.ResolveResourceName(new CultureInfo(culture), resources);

            Assert.Equal("TrashVanish.lang_" + expected, result);
        }

        [Fact]
        public void UnrelatedResourceNamesDoNotPretendToBeTranslations()
        {
            var result = currentCulture.ResolveResourceName(new CultureInfo("fr-FR"),
                new[] { "Other.lang_fr.resources", "TrashVanish.lang_fr_CA.resources" });

            Assert.Equal("TrashVanish.lang_en", result);
        }

        [Theory]
        [InlineData("ru-RU", "TrashVanish.lang_ru")]
        [InlineData("en-US", "TrashVanish.lang_en")]
        [InlineData("fr-FR", "TrashVanish.lang_en")]
        public void ApplicationInitializerUsesTheCurrentCulture(string culture, string expected)
        {
            var previous = CultureInfo.CurrentUICulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = new CultureInfo(culture);

                currentCulture.setLanguageForCurrentCulture();

                Assert.Equal(expected, currentCulture.GlobalResourseManager.BaseName);
                Assert.False(string.IsNullOrWhiteSpace(currentCulture.GlobalResourseManager.GetString("AppIsLoaded")));
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentUICulture = previous;
                currentCulture.setLanguageForCurrentCulture();
            }
        }

        [Theory]
        [InlineData("ru-RU", "TrashVanish.lang_ru")]
        [InlineData("en-US", "TrashVanish.lang_en")]
        [InlineData("de-DE", "TrashVanish.lang_en")]
        public void BundledResourcesContainAllOperationMessages(string culture, string expected)
        {
            var assembly = typeof(Worker).Assembly;
            var resourceName = currentCulture.ResolveResourceName(new CultureInfo(culture),
                assembly.GetManifestResourceNames());
            var manager = new System.Resources.ResourceManager(resourceName, assembly);

            Assert.Equal(expected, resourceName);
            foreach (FileOutcome outcome in System.Enum.GetValues(typeof(FileOutcome)))
                Assert.False(string.IsNullOrWhiteSpace(manager.GetString("operation" + outcome)));
            Assert.False(string.IsNullOrWhiteSpace(manager.GetString("AppIsLoaded")));
            manager.ReleaseAllResources();
        }
    }
}
