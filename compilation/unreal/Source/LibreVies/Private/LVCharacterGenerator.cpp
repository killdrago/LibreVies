#include "LVCharacterGenerator.h"

#include "KismetProceduralMeshLibrary.h"
#include "HAL/FileManager.h"
#include "Materials/Material.h"
#include "Misc/FileHelper.h"
#include "Misc/Paths.h"
#include "ProceduralMeshComponent.h"
#include "Serialization/JsonSerializer.h"
#include "Serialization/JsonWriter.h"

namespace
{
    static FString MakeHumanRoot()
    {
        return FPaths::Combine(FPaths::ProjectContentDir(), TEXT("Characters/MakeHuman"));
    }

    static bool ParseObjIndex(const FString& Token, int32& OutIndex, int32 VertexCount)
    {
        TArray<FString> Parts;
        Token.ParseIntoArray(Parts, TEXT("/"), false);
        if (Parts.Num() == 0) return false;
        const int32 ObjIndex = FCString::Atoi(*Parts[0]);
        if (ObjIndex == 0) return false;
        OutIndex = ObjIndex < 0 ? VertexCount + ObjIndex : ObjIndex - 1;
        return OutIndex >= 0 && OutIndex < VertexCount;
    }
}

ALVCharacterGenerator::ALVCharacterGenerator()
{
    PrimaryActorTick.bCanEverTick = false;
    BodyMesh = CreateDefaultSubobject<UProceduralMeshComponent>(TEXT("MakeHumanBody"));
    RootComponent = BodyMesh;
    BodyMesh->bUseAsyncCooking = true;
    BodyMesh->SetCollisionProfileName(TEXT("BlockAllDynamic"));
    SkinMaterial = UMaterial::GetDefaultMaterial(MD_Surface);
}

void ALVCharacterGenerator::BeginPlay()
{
    Super::BeginPlay();
    if (!LoadSourceMesh())
    {
        UE_LOG(LogTemp, Error, TEXT("LibreVies: impossible de charger la base MakeHuman CC0 depuis %s"), *MakeHumanRoot());
        return;
    }
    ResetCharacter();
}

FString ALVCharacterGenerator::DataPath(const FString& RelativeName) const
{
    return FPaths::Combine(MakeHumanRoot(), RelativeName);
}

bool ALVCharacterGenerator::LoadSourceMesh()
{
    FString Text;
    if (!FFileHelper::LoadFileToString(Text, *DataPath(TEXT("MakeHumanBaseData.txt"))))
    {
        return false;
    }

    SourceVertices.Reset();
    BodyTriangles.Reset();
    FString CurrentGroup = TEXT("body");
    TArray<FString> Lines;
    Text.ParseIntoArrayLines(Lines);

    for (FString Line : Lines)
    {
        Line.TrimStartAndEndInline();
        if (Line.StartsWith(TEXT("v ")))
        {
            TArray<FString> Parts;
            Line.ParseIntoArrayWS(Parts);
            if (Parts.Num() >= 4)
            {
                SourceVertices.Add(FVector(
                    FCString::Atof(*Parts[1]),
                    FCString::Atof(*Parts[2]),
                    FCString::Atof(*Parts[3])));
            }
        }
        else if (Line.StartsWith(TEXT("g ")))
        {
            CurrentGroup = Line.Mid(2).TrimStartAndEnd();
        }
        else if (Line.StartsWith(TEXT("f ")))
        {
            TArray<FString> FaceTokens;
            Line.Mid(2).ParseIntoArrayWS(FaceTokens);
            const bool bHelperMesh = CurrentGroup.StartsWith(TEXT("helper-"), ESearchCase::IgnoreCase)
                || CurrentGroup.StartsWith(TEXT("joint-"), ESearchCase::IgnoreCase);
            AddTriangleFromFace(FaceTokens, !bHelperMesh);
        }
    }

    if (SourceVertices.Num() == 0 || BodyTriangles.Num() == 0)
    {
        return false;
    }

    const TCHAR* TargetNames[] = {
        TEXT("universal-male-young-averagemuscle-averageweight"),
        TEXT("universal-female-young-averagemuscle-averageweight"),
        TEXT("stomach-pregnant-incr"), TEXT("stomach-pregnant-decr"),
        TEXT("torso-scale-horiz-incr"), TEXT("torso-scale-horiz-decr"),
        TEXT("l-upperarm-scale-horiz-incr"), TEXT("l-upperarm-scale-horiz-decr"),
        TEXT("r-upperarm-scale-horiz-incr"), TEXT("r-upperarm-scale-horiz-decr"),
        TEXT("l-upperarm-scale-vert-incr"), TEXT("l-upperarm-scale-vert-decr"),
        TEXT("r-upperarm-scale-vert-incr"), TEXT("r-upperarm-scale-vert-decr"),
        TEXT("l-lowerarm-scale-horiz-incr"), TEXT("l-lowerarm-scale-horiz-decr"),
        TEXT("r-lowerarm-scale-horiz-incr"), TEXT("r-lowerarm-scale-horiz-decr"),
        TEXT("r-lowerarm-scale-vert-incr"), TEXT("r-lowerarm-scale-vert-decr"),
        TEXT("l-lowerarm-scale-vert-incr"), TEXT("l-lowerarm-scale-vert-decr"),
        TEXT("upperlegs-height-incr"), TEXT("upperlegs-height-decr"),
        TEXT("l-upperleg-scale-horiz-incr"), TEXT("l-upperleg-scale-horiz-decr"),
        TEXT("r-upperleg-scale-horiz-incr"), TEXT("r-upperleg-scale-horiz-decr"),
        TEXT("l-lowerleg-scale-horiz-incr"), TEXT("l-lowerleg-scale-horiz-decr"),
        TEXT("r-lowerleg-scale-horiz-incr"), TEXT("r-lowerleg-scale-horiz-decr"),
        TEXT("l-foot-scale-incr"), TEXT("l-foot-scale-decr"),
        TEXT("r-foot-scale-incr"), TEXT("r-foot-scale-decr"),
        TEXT("head-round"), TEXT("head-oval"),
        TEXT("l-eye-scale-incr"), TEXT("l-eye-scale-decr"),
        TEXT("r-eye-scale-incr"), TEXT("r-eye-scale-decr"),
        TEXT("nose-volume-incr"), TEXT("nose-volume-decr"),
        TEXT("mouth-lowerlip-volume-incr"), TEXT("mouth-lowerlip-volume-decr"),
        TEXT("l-ear-scale-incr"), TEXT("l-ear-scale-decr"),
        TEXT("r-ear-scale-incr"), TEXT("r-ear-scale-decr")
    };
    for (const TCHAR* Name : TargetNames)
    {
        LoadTarget(Name);
    }

    bSourceLoaded = true;
    return true;
}

void ALVCharacterGenerator::AddTriangleFromFace(const TArray<FString>& FaceTokens, bool bIncludeGroup)
{
    if (!bIncludeGroup || FaceTokens.Num() < 3) return;

    int32 First = INDEX_NONE;
    int32 Previous = INDEX_NONE;
    if (!ParseObjIndex(FaceTokens[0], First, SourceVertices.Num())
        || !ParseObjIndex(FaceTokens[1], Previous, SourceVertices.Num()))
    {
        return;
    }

    for (int32 Index = 2; Index < FaceTokens.Num(); ++Index)
    {
        int32 Current = INDEX_NONE;
        if (ParseObjIndex(FaceTokens[Index], Current, SourceVertices.Num()))
        {
            FTriangle Triangle;
            Triangle.A = First;
            Triangle.B = Previous;
            Triangle.C = Current;
            BodyTriangles.Add(Triangle);
        }
        Previous = Current;
    }
}

void ALVCharacterGenerator::LoadTarget(const FString& Name)
{
    FString Text;
    if (!FFileHelper::LoadFileToString(Text, *DataPath(Name + TEXT(".txt"))))
    {
        return;
    }

    TMap<int32, FVector> Target;
    TArray<FString> Lines;
    Text.ParseIntoArrayLines(Lines);
    for (FString Line : Lines)
    {
        Line.TrimStartAndEndInline();
        TArray<FString> Parts;
        Line.ParseIntoArrayWS(Parts);
        if (Parts.Num() < 4) continue;
        const int32 Vertex = FCString::Atoi(*Parts[0]);
        Target.Add(Vertex, FVector(
            FCString::Atod(*Parts[1]),
            FCString::Atod(*Parts[2]),
            FCString::Atod(*Parts[3])));
    }
    Targets.Add(Name, MoveTemp(Target));
}

void ALVCharacterGenerator::ApplyTarget(TArray<FVector>& Vertices, const FString& Name, float Amount, float Strength) const
{
    const TMap<int32, FVector>* Target = Targets.Find(Name);
    if (!Target || FMath::IsNearlyZero(Amount)) return;
    for (const TPair<int32, FVector>& Pair : *Target)
    {
        if (Vertices.IsValidIndex(Pair.Key))
        {
            Vertices[Pair.Key] += Pair.Value * Amount * Strength;
        }
    }
}

void ALVCharacterGenerator::ApplySignedTarget(TArray<FVector>& Vertices, float Amount, const FString& Positive, const FString& Negative, float Strength) const
{
    ApplyTarget(Vertices, Amount >= 0.0f ? Positive : Negative, FMath::Abs(Amount), Strength);
}

void ALVCharacterGenerator::BuildCharacter()
{
    if (!bSourceLoaded) return;
    BuildMeshFromCurrentValues();
}

void ALVCharacterGenerator::BuildMeshFromCurrentValues()
{
    TArray<FVector> Deformed = SourceVertices;
    ApplyTarget(Deformed, bFemale ? TEXT("universal-female-young-averagemuscle-averageweight") : TEXT("universal-male-young-averagemuscle-averageweight"), 1.0f, 1.0f);
    ApplySignedTarget(Deformed, Belly, TEXT("stomach-pregnant-incr"), TEXT("stomach-pregnant-decr"), 0.55f);
    ApplySignedTarget(Deformed, Belly, TEXT("torso-scale-horiz-incr"), TEXT("torso-scale-horiz-decr"), 0.28f);
    ApplySignedTarget(Deformed, ArmThickness, TEXT("l-upperarm-scale-horiz-incr"), TEXT("l-upperarm-scale-horiz-decr"), 0.5f);
    ApplySignedTarget(Deformed, ArmThickness, TEXT("r-upperarm-scale-horiz-incr"), TEXT("r-upperarm-scale-horiz-decr"), 0.5f);
    ApplySignedTarget(Deformed, ArmThickness, TEXT("l-lowerarm-scale-horiz-incr"), TEXT("l-lowerarm-scale-horiz-decr"), 0.5f);
    ApplySignedTarget(Deformed, ArmThickness, TEXT("r-lowerarm-scale-horiz-incr"), TEXT("r-lowerarm-scale-horiz-decr"), 0.5f);
    ApplySignedTarget(Deformed, ArmLength, TEXT("l-upperarm-scale-vert-incr"), TEXT("l-upperarm-scale-vert-decr"), 0.55f);
    ApplySignedTarget(Deformed, ArmLength, TEXT("r-upperarm-scale-vert-incr"), TEXT("r-upperarm-scale-vert-decr"), 0.55f);
    ApplySignedTarget(Deformed, ArmLength, TEXT("l-lowerarm-scale-vert-incr"), TEXT("l-lowerarm-scale-vert-decr"), 0.55f);
    ApplySignedTarget(Deformed, ArmLength, TEXT("r-lowerarm-scale-vert-incr"), TEXT("r-lowerarm-scale-vert-decr"), 0.55f);
    ApplySignedTarget(Deformed, LegThickness, TEXT("l-upperleg-scale-horiz-incr"), TEXT("l-upperleg-scale-horiz-decr"), 0.52f);
    ApplySignedTarget(Deformed, LegThickness, TEXT("r-upperleg-scale-horiz-incr"), TEXT("r-upperleg-scale-horiz-decr"), 0.52f);
    ApplySignedTarget(Deformed, LegThickness, TEXT("l-lowerleg-scale-horiz-incr"), TEXT("l-lowerleg-scale-horiz-decr"), 0.52f);
    ApplySignedTarget(Deformed, LegThickness, TEXT("r-lowerleg-scale-horiz-incr"), TEXT("r-lowerleg-scale-horiz-decr"), 0.52f);
    ApplySignedTarget(Deformed, LegLength, TEXT("upperlegs-height-incr"), TEXT("upperlegs-height-decr"), 0.55f);
    ApplySignedTarget(Deformed, FeetSize, TEXT("l-foot-scale-incr"), TEXT("l-foot-scale-decr"), 0.58f);
    ApplySignedTarget(Deformed, FeetSize, TEXT("r-foot-scale-incr"), TEXT("r-foot-scale-decr"), 0.58f);
    ApplySignedTarget(Deformed, HeadShape, TEXT("head-round"), TEXT("head-oval"), 0.58f);
    ApplySignedTarget(Deformed, EyesShape, TEXT("l-eye-scale-incr"), TEXT("l-eye-scale-decr"), 0.62f);
    ApplySignedTarget(Deformed, EyesShape, TEXT("r-eye-scale-incr"), TEXT("r-eye-scale-decr"), 0.62f);
    ApplySignedTarget(Deformed, NoseShape, TEXT("nose-volume-incr"), TEXT("nose-volume-decr"), 0.65f);
    ApplySignedTarget(Deformed, MouthShape, TEXT("mouth-lowerlip-volume-incr"), TEXT("mouth-lowerlip-volume-decr"), 0.7f);
    ApplySignedTarget(Deformed, EarsShape, TEXT("l-ear-scale-incr"), TEXT("l-ear-scale-decr"), 0.62f);
    ApplySignedTarget(Deformed, EarsShape, TEXT("r-ear-scale-incr"), TEXT("r-ear-scale-decr"), 0.62f);

    float MinY = TNumericLimits<float>::Max();
    for (const FVector& Vertex : Deformed) MinY = FMath::Min(MinY, Vertex.Y);

    TArray<FVector> Vertices;
    TArray<FVector2D> UVs;
    Vertices.Reserve(Deformed.Num());
    UVs.Reserve(Deformed.Num());
    for (const FVector& Vertex : Deformed)
    {
        // MakeHuman uses Y as height. Unreal uses Z as height.
        Vertices.Add(FVector(Vertex.X * 10.0f, Vertex.Z * 10.0f, (Vertex.Y - MinY) * 10.0f));
        UVs.Add(FVector2D(Vertex.X * 0.1f, Vertex.Z * 0.1f));
    }

    TArray<int32> Indices;
    Indices.Reserve(BodyTriangles.Num() * 3);
    for (const FTriangle& Triangle : BodyTriangles)
    {
        Indices.Add(Triangle.A);
        Indices.Add(Triangle.C);
        Indices.Add(Triangle.B);
    }

    TArray<FVector> Normals;
    TArray<FProcMeshTangent> Tangents;
    UKismetProceduralMeshLibrary::CalculateTangentsForMesh(Vertices, Indices, UVs, Normals, Tangents);
    TArray<FLinearColor> Colors;
    Colors.Init(FLinearColor(0.72f, 0.42f, 0.31f, 1.0f), Vertices.Num());

    BodyMesh->ClearAllMeshSections();
    BodyMesh->CreateMeshSection_LinearColor(0, Vertices, Indices, Normals, UVs, Colors, Tangents, true);
    BodyMesh->SetMaterial(0, SkinMaterial);
}

void ALVCharacterGenerator::SetSex(bool bUseFemale)
{
    bFemale = bUseFemale;
    BuildCharacter();
}

void ALVCharacterGenerator::ResetCharacter()
{
    bFemale = true;
    Belly = ArmThickness = ArmLength = LegThickness = LegLength = FeetSize = 0.0f;
    HeadShape = EyesShape = NoseShape = MouthShape = EarsShape = 0.0f;
    BuildCharacter();
}

void ALVCharacterGenerator::RandomizeCharacter()
{
    bFemale = FMath::RandBool();
    Belly = FMath::FRandRange(-0.65f, 0.75f);
    ArmThickness = FMath::FRandRange(-0.7f, 0.75f);
    ArmLength = FMath::FRandRange(-0.65f, 0.7f);
    LegThickness = FMath::FRandRange(-0.65f, 0.7f);
    LegLength = FMath::FRandRange(-0.65f, 0.7f);
    FeetSize = FMath::FRandRange(-0.65f, 0.7f);
    HeadShape = FMath::FRandRange(-0.75f, 0.75f);
    EyesShape = FMath::FRandRange(-0.65f, 0.75f);
    NoseShape = FMath::FRandRange(-0.7f, 0.75f);
    MouthShape = FMath::FRandRange(-0.7f, 0.75f);
    EarsShape = FMath::FRandRange(-0.65f, 0.7f);
    BuildCharacter();
}

bool ALVCharacterGenerator::SavePreset()
{
    TSharedPtr<FJsonObject> Json = MakeShared<FJsonObject>();
    Json->SetBoolField(TEXT("female"), bFemale);
    Json->SetNumberField(TEXT("belly"), Belly);
    Json->SetNumberField(TEXT("armThickness"), ArmThickness);
    Json->SetNumberField(TEXT("armLength"), ArmLength);
    Json->SetNumberField(TEXT("legThickness"), LegThickness);
    Json->SetNumberField(TEXT("legLength"), LegLength);
    Json->SetNumberField(TEXT("feetSize"), FeetSize);
    Json->SetNumberField(TEXT("headShape"), HeadShape);
    Json->SetNumberField(TEXT("eyesShape"), EyesShape);
    Json->SetNumberField(TEXT("noseShape"), NoseShape);
    Json->SetNumberField(TEXT("mouthShape"), MouthShape);
    Json->SetNumberField(TEXT("earsShape"), EarsShape);

    FString Output;
    TSharedRef<TJsonWriter<>> Writer = TJsonWriterFactory<>::Create(&Output);
    if (!FJsonSerializer::Serialize(Json.ToSharedRef(), Writer)) return false;
    const FString Directory = FPaths::Combine(FPaths::ProjectSavedDir(), TEXT("CharacterCreator"));
    IFileManager::Get().MakeDirectory(*Directory, true);
    return FFileHelper::SaveStringToFile(Output, *FPaths::Combine(Directory, TEXT("personnage_preset.json")));
}
