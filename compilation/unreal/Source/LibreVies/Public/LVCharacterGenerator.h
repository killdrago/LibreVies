#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LVCharacterGenerator.generated.h"

class UProceduralMeshComponent;

/**
 * Runtime MakeHuman CC0 character generator.
 *
 * The source mesh and the CC0 morph targets are kept as plain data in
 * Content/Characters/MakeHuman. This makes the same generator usable in the
 * standalone creator and later in the New Game screen.
 */
UCLASS(BlueprintType)
class LIBREVIES_API ALVCharacterGenerator : public AActor
{
    GENERATED_BODY()

public:
    ALVCharacterGenerator();

    UPROPERTY(VisibleAnywhere, BlueprintReadOnly, Category = "Character")
    TObjectPtr<UProceduralMeshComponent> BodyMesh;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Character")
    bool bFemale = true;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Morphology", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float Belly = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Morphology", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float ArmThickness = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Morphology", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float ArmLength = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Morphology", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float LegThickness = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Morphology", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float LegLength = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Morphology", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float FeetSize = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Face", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float HeadShape = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Face", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float EyesShape = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Face", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float NoseShape = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Face", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float MouthShape = 0.0f;

    UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Face", meta = (ClampMin = "-1.0", ClampMax = "1.0"))
    float EarsShape = 0.0f;

    UFUNCTION(BlueprintCallable, Category = "Character")
    void BuildCharacter();

    UFUNCTION(BlueprintCallable, Category = "Character")
    void SetSex(bool bUseFemale);

    UFUNCTION(BlueprintCallable, Category = "Character")
    void RandomizeCharacter();

    UFUNCTION(BlueprintCallable, Category = "Character")
    void ResetCharacter();

    UFUNCTION(BlueprintCallable, Category = "Character")
    bool SavePreset();

    UFUNCTION(BlueprintCallable, Category = "Character")
    bool IsSourceLoaded() const { return bSourceLoaded; }

protected:
    virtual void BeginPlay() override;

private:
    struct FTriangle
    {
        int32 A = 0;
        int32 B = 0;
        int32 C = 0;
    };

    bool bSourceLoaded = false;
    TArray<FVector> SourceVertices;
    TArray<FTriangle> BodyTriangles;
    TMap<FString, TMap<int32, FVector>> Targets;
    TObjectPtr<UMaterialInterface> SkinMaterial;

    FString DataPath(const FString& RelativeName) const;
    bool LoadSourceMesh();
    void LoadTarget(const FString& Name);
    void ApplyTarget(TArray<FVector>& Vertices, const FString& Name, float Amount, float Strength) const;
    void ApplySignedTarget(TArray<FVector>& Vertices, float Amount, const FString& Positive, const FString& Negative, float Strength) const;
    void BuildMeshFromCurrentValues();
    void AddTriangleFromFace(const TArray<FString>& FaceTokens, bool bIncludeGroup);
};
