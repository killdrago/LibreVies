using UnrealBuildTool;

public class LibreVies : ModuleRules
{
    public LibreVies(ReadOnlyTargetRules Target) : base(Target)
    {
        PCHUsage = PCHUsageMode.UseExplicitOrSharedPCHs;
        PublicDependencyModuleNames.AddRange(new string[]
        {
            "Core",
            "CoreUObject",
            "Engine",
            "InputCore",
            "EnhancedInput",
            "UMG",
            "Slate",
            "SlateCore",
            "ProceduralMeshComponent",
            "Json",
            "JsonUtilities"
        });
    }
}
