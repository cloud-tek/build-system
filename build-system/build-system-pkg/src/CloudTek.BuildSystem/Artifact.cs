using Nuke.Common.IO;

namespace CloudTek.BuildSystem
{
    public enum ArtifactType
    {
        Package,
        Container,
        Lib
    }

    public enum Stability
    {
        Stable,
        PreRelease
    }
    public class Artifact
    {
        public string Name { get; set; }

        public string Project { get; set; }
        public ArtifactType Type { get; set; }
        public Stability Stability { get; set; }
    }


    public static class ArtifactExtensions
    {
        public static AbsolutePath GetTestProjectPath(this Artifact artifact, Module module, AbsolutePath rootDirectory)
        {
            return rootDirectory / $"{module.Name}/{artifact.Name}/tests/{artifact.Project}.Tests/{artifact.Project}.Tests.csproj";
        }
    }
}