namespace EngineeringModelQA.Ifc.Tests;

/// <summary>
/// Paths to the sample files copied next to the test assembly, and GlobalIds of the generated fixtures.
/// The GlobalIds are copied from the fixture generator's listing, not from reader output.
/// </summary>
internal static class Fixtures
{
    public static string Root => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "samples");

    public static string Ifc(string name) => Path.Combine(Root, "fixtures", name);

    public static string Profile(string name) => Path.Combine(Root, "profiles", name);

    public static string Baseline => Ifc("baseline.ifc");

    public static string Revision => Ifc("revision.ifc");

    public static class Id
    {
        public const string B101 = "00ACm5_V$Bp$cpseOe8j$P";
        public const string B103 = "1mpdRzgi4ejcSNvPuYRwuU";
        public const string B104 = "1KY9SfOiz8T552BKlzwPLs";
        public const string B105 = "2b$T5S8WaeR7DT05Wd08Hf";
        public const string B106 = "3zYLSsKvaJKqL3CkouroVX";
        public const string B204 = "1QWL9zKabIm_Qgor2wmSG1";
        public const string B207 = "0$NvOprkEwAk8pg6K$fSnE";
        public const string B208 = "0975iUq9cU0JSGKtwcKltu";
        public const string C101 = "1SFhZhjzx3$fj3ViB8fash";
        public const string C104 = "3r5ruJCwKe2wYa04wC5YEG";
        public const string C203 = "2Zj$OmfonxII29l0HDi6zv";
        public const string C206 = "3uHTxLbo_Bsoa0Pda8A6LT";
        public const string S1 = "07SvmTeZ_DNUgrTRdNQWj$";
        public const string W2 = "0$5FMUApklpW81iSflNuhw";
        public const string W3 = "0rcR6yTZSW$KfBc$$FtcZg";
        public const string W4 = "352pBNeVRL8ZTQyMq84i1x";
    }
}
