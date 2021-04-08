using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.GitVersion;
using Nuke.Common.Utilities.Collections;
using Nuke.Common.Git;
using System.Linq;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

namespace CloudTek.BuildSystem
{
    public abstract class SmartBuild : NukeBuild
    {
        readonly Module[] Modules;
        readonly string Version;
        readonly Artifact finalArtifactWithTests;
        protected SmartBuild(Module[] modules, string version)
        {
            // Debugger.Launch(); // Uncomment to debug a build
            Version = version;
            Modules = modules;

            var modulesWithExistingTests = Modules
              .Where(m => m.Artifacts.Any(a => FileExists(a.GetTestProjectPath(m, RootDirectory))))
              .OrderBy(x => x.Name);


            var finalModuleWithTests = modulesWithExistingTests.LastOrDefault();

            finalArtifactWithTests =
              finalModuleWithTests?.Artifacts.Last(a => FileExists(a.GetTestProjectPath(finalModuleWithTests, RootDirectory)));
        }

        [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
        public Configuration Configuration { get; set; } = IsLocalBuild ? Configuration.Debug : Configuration.Release;

        private string buildNumber;
        [Parameter("BuildNumber")]
        public string BuildNumber
        {
            get => buildNumber;
            set => buildNumber = value.Replace(".", string.Empty);
        }

        //[Solution] readonly Solution Solution;
        //[GitRepository] readonly GitRepository GitRepository;
        [GitVersion] public GitVersion GitVersion { get; set; }

        protected AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";
        protected AbsolutePath TestResultsDirectory => RootDirectory / "tests/results";
        protected AbsolutePath TestCoverageDirectory => RootDirectory / "tests/coverage";

        protected Target Clean => _ => _
            .Before(Restore)
            .Executes(() =>
            {
                EnsureCleanDirectory(ArtifactsDirectory);
                EnsureCleanDirectory(TestResultsDirectory);
                EnsureCleanDirectory(TestCoverageDirectory);
            });

        protected Target Restore => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.ForEach(artifact =>
                    {
                        Logger.Trace($"Restoring {artifact.Name}");
                        DotNetRestore(s => s
                          .SetProjectFile((RootDirectory /
                                           $"{m.Name}/{artifact.Name}/src/{artifact.Project}/{artifact.Project}.csproj")));
                    });

                });

            });

        protected Target Compile => _ => _
            .DependsOn(Restore)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.ForEach(artifact =>
                    {
                        var path = (RootDirectory /
                                 $"{m.Name}/{artifact.Name}/src/{artifact.Project}/{artifact.Project}.csproj");

                        DotNetBuild(s => s
                          .SetProjectFile(path)
                          .SetConfiguration(Configuration)
                          .SetVersion(GitVersion.NuGetVersionV2)
                          .SetFileVersion(GitVersion.AssemblySemFileVer)
                          .SetAssemblyVersion(GitVersion.AssemblySemVer)
                          .EnableNoRestore());
                    });
                });
            });

        protected Target Pack => _ => _
            .DependsOn(Compile)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.Where(a => a.Type == ArtifactType.Package).ForEach(artifact =>
                    {
                        var path = (RootDirectory /
                                 $"{m.Name}/{artifact.Name}/src/{artifact.Project}/{artifact.Project}.csproj");

                        DotNetPack(s => s
                          .SetProject(path)
                          .SetConfiguration(Configuration)
                          .SetVersion(GitVersion.NuGetVersionV2)
                          .SetFileVersion(GitVersion.AssemblySemFileVer)
                          .SetAssemblyVersion(GitVersion.AssemblySemVer)
                          .SetOutputDirectory(ArtifactsDirectory / artifact.Name)
                          .EnableNoBuild());
                    });
                });
            });


        protected Target Publish => _ => _
            .DependsOn(Compile)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.Where(a => a.Type == ArtifactType.Package).ForEach(artifact =>
                    {
                        var path = (RootDirectory /
                                 $"{m.Name}/{artifact.Name}/src/{artifact.Project}/{artifact.Project}.csproj");

                        DotNetPublish(s => s
                          .SetProject(path)
                          .SetConfiguration(Configuration)
                          .SetVersion(GitVersion.NuGetVersionV2)
                          .SetFileVersion(GitVersion.AssemblySemFileVer)
                          .SetAssemblyVersion(GitVersion.AssemblySemVer)
                          .SetOutput(ArtifactsDirectory / artifact.Name)
                          .EnableNoBuild());
                    });
                });
            });


        private DotNetTestSettings ConfigureTestSettings(DotNetTestSettings settings, Module module, Artifact artifact, string category, bool isFinal = false)
        {
            return settings
              .SetProjectFile(artifact.GetTestProjectPath(module, RootDirectory))
              .SetFilter($"Category={category}")
              .SetLogger($"trx;LogFileName={artifact.Project}.{category}.trx")
              .SetConfiguration(Configuration)
              .SetResultsDirectory(TestResultsDirectory / artifact.Name)
              .When(Constants.TestCategories.CodeCoverageCategories.Contains(category), x =>
                x.SetProcessArgumentConfigurator(args =>
                  args
                    .Add("/p:CollectCoverage=true")
                    .Add("/maxcpucount:1")
                    .Add($"/p:MergeWith={TestCoverageDirectory}/coverage.temp.json")
                    .Add($"/p:CoverletOutput={TestCoverageDirectory}/coverage.temp.json", !isFinal)
                    .Add($"/p:CoverletOutput={TestCoverageDirectory}/coverage.xml", isFinal)
                    .Add("/p:CoverletOutputFormat=cobertura", isFinal)));
        }

        protected Target UnitTests => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.ForEach(artifact =>
                    {
                        if (FileExists(artifact.GetTestProjectPath(m, RootDirectory)))
                        {
                            DotNetTest(s => ConfigureTestSettings(s, m, artifact, Constants.TestCategories.UnitTests,
                         m.Equals(Modules.Last()) && artifact.Equals(finalArtifactWithTests)));
                        }
                    });
                });
            });

        protected Target IntegrationTests => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.ForEach(artifact =>
                    {
                        if (FileExists(artifact.GetTestProjectPath(m, RootDirectory)))
                        {
                            DotNetTest(s => ConfigureTestSettings(s, m, artifact, Constants.TestCategories.IntegrationTests,
                          m.Equals(Modules.Last()) && artifact.Equals(finalArtifactWithTests)));
                        }
                    });
                });
            });

        protected Target ModuleTests => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.ForEach(artifact =>
                    {
                        if (FileExists(artifact.GetTestProjectPath(m, RootDirectory)))
                        {
                            DotNetTest(s => ConfigureTestSettings(s, m, artifact, Constants.TestCategories.ModuleTests));
                        }
                    });
                });
            });

        protected Target SystemTests => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.ForEach(artifact =>
                    {
                        if (FileExists(artifact.GetTestProjectPath(m, RootDirectory)))
                        {
                            DotNetTest(s => ConfigureTestSettings(s, m, artifact, Constants.TestCategories.SystemTests));
                        }
                    });
                });
            });

        protected Target SmokeTests => _ => _
            .DependsOn(Clean)
            .Executes(() =>
            {
                Modules.ForEach(m =>
                {
                    m.Artifacts.ForEach(artifact =>
                    {
                        if (FileExists(artifact.GetTestProjectPath(m, RootDirectory)))
                        {
                            DotNetTest(s => ConfigureTestSettings(s, m, artifact, Constants.TestCategories.SmokeTests));
                        }
                    });
                });
            });
    }
}
