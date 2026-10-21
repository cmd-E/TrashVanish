using System.Globalization;
using System.Reflection;
using System.Resources;
namespace TrashVanish.Classes
{
    internal static class currentCulture
    {
        public static ResourceManager GlobalResourseManager { get; private set; }
        public static void setLanguageForCurrentCulture()
        {
            string language = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "ru" ? "ru" : "en";
            GlobalResourseManager =
                new ResourceManager("TrashVanish.lang_" + language, Assembly.GetExecutingAssembly());
        }
    }
}
