using CloudTek.BuildSystem;
using Nuke.Common.Execution;
using Nuke.Common.Tools.GitVersion;
using System.ComponentModel.DataAnnotations;

[CheckBuildProjectConfigurations]
public class Build : SmartBuild
{
    public Build() : base(Modules)
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

[GitVersion(Framework = "net5.0", NoFetch = true)]
[Required] public override GitVersion GitVersion { get; set; }
}