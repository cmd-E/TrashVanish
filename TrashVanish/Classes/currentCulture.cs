using System.Collections.Generic;
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
            var assembly = Assembly.GetExecutingAssembly();
            GlobalResourseManager = new ResourceManager(
                ResolveResourceName(CultureInfo.CurrentUICulture, assembly.GetManifestResourceNames()), assembly);
        }

        internal static string ResolveResourceName(CultureInfo culture, IEnumerable<string> resourceNames)
        {
            var available = new HashSet<string>(resourceNames, System.StringComparer.Ordinal);
            // Check regional and parent cultures before using the English fallback.
            // Adding an embedded lang_<culture>.resx requires no selector changes.
            for (var candidate = culture; candidate != CultureInfo.InvariantCulture; candidate = candidate.Parent)
            {
                string name = "TrashVanish.lang_" + candidate.Name;
                if (available.Contains(name + ".resources"))
                    return name;
            }
            return "TrashVanish.lang_en";
        }
    }
}
