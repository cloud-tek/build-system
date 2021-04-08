using CloudTek.BuildSystem;
using Nuke.Common.Execution;

[CheckBuildProjectConfigurations]
public class Build : SmartBuild
{
    public Build() : base(Modules, "1.0")
    {

    }

    public static int Main() => Execute<Build>(x => x.Compile);


    public static Module[] Modules = new[]
    {
        new Module()
        {
            Name = "build-system",
            Artifacts = new []
            {
                new Artifact() { Name = "build-system-pkg", Project = "CloudTek.BuildSystem", Type = ArtifactType.Package, Stability = Stability.Stable }
            }
        }
    };
}