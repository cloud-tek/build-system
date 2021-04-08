namespace CloudTek.BuildSystem
{
    public static class Constants
    {
        public static class TestCategories
        {
            public const string UnitTests = "UnitTests";
            public const string IntegrationTests = "IntegrationTests";
            public const string ModuleTests = "ModuleTests";
            public const string SystemTests = "SystemTests";
            public const string SmokeTests = "SmokeTests";

            public static string[] CodeCoverageCategories =
                new string[]
                {
                    Constants.TestCategories.UnitTests,
                    Constants.TestCategories.IntegrationTests
                };
        }
    }
}