namespace SephiriaExpanded
{
    internal static class ModText
    {
        internal const int Korean = 0;
        internal const int English = 1;

        internal static bool IsEnglish
        {
            get { return ModSettings.Language == English; }
        }

        internal static string T(string korean, string english)
        {
            return IsEnglish ? english : korean;
        }

        internal static string[] T(string[] korean, string[] english)
        {
            return IsEnglish ? english : korean;
        }
    }
}
