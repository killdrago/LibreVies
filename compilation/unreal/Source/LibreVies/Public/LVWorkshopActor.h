#pragma once

#include "CoreMinimal.h"
#include "GameFramework/Actor.h"
#include "LVWorkshopActor.generated.h"

class UStaticMesh;
class UStaticMeshComponent;

/**
 * Petit atelier de generation reutilisable.
 *
 * The first pass creates clean modular prototypes. The same functions are
 * called by the character creator UI and can later be exposed as an editor
 * tool for author-only asset generation. Real authored meshes can replace a
 * generated part without changing the rest of the game.
 */
UCLASS(BlueprintType)
class LIBREVIES_API ALVWorkshopActor : public AActor
{
    GENERATED_BODY()

public:
    ALVWorkshopActor();

    UFUNCTION(BlueprintCallable, Category = "LibreVies Workshop")
    void GenerateTree();

    UFUNCTION(BlueprintCallable, Category = "LibreVies Workshop")
    void GenerateChair();

    UFUNCTION(BlueprintCallable, Category = "LibreVies Workshop")
    void GenerateBrick();

    UFUNCTION(BlueprintCallable, Category = "LibreVies Workshop")
    void GenerateWeapon();

    UFUNCTION(BlueprintCallable, Category = "LibreVies Workshop")
    void GenerateClothingPrototype();

    UFUNCTION(BlueprintCallable, Category = "LibreVies Workshop")
    void ClearGenerated();

private:
    UPROPERTY(VisibleAnywhere)
    TObjectPtr<USceneComponent> SceneRoot;

    UPROPERTY()
    TArray<TObjectPtr<UStaticMeshComponent>> GeneratedParts;

    UStaticMesh* LoadShape(const TCHAR* Name) const;
    UStaticMeshComponent* AddPart(const TCHAR* Shape, const FVector& Location, const FVector& Scale, const FRotator& Rotation = FRotator::ZeroRotator);
};
