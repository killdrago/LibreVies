#include "LVWorkshopActor.h"

#include "Components/StaticMeshComponent.h"
#include "Engine/StaticMesh.h"
#include "UObject/ConstructorHelpers.h"

ALVWorkshopActor::ALVWorkshopActor()
{
    PrimaryActorTick.bCanEverTick = false;
    SceneRoot = CreateDefaultSubobject<USceneComponent>(TEXT("WorkshopRoot"));
    RootComponent = SceneRoot;
}

UStaticMesh* ALVWorkshopActor::LoadShape(const TCHAR* Name) const
{
    return LoadObject<UStaticMesh>(nullptr, *FString::Printf(TEXT("/Engine/BasicShapes/%s.%s"), Name, Name));
}

UStaticMeshComponent* ALVWorkshopActor::AddPart(const TCHAR* Shape, const FVector& Location, const FVector& Scale, const FRotator& Rotation)
{
    UStaticMesh* Mesh = LoadShape(Shape);
    if (!Mesh) return nullptr;

    UStaticMeshComponent* Part = NewObject<UStaticMeshComponent>(this);
    Part->SetStaticMesh(Mesh);
    Part->SetRelativeLocation(Location);
    Part->SetRelativeRotation(Rotation);
    Part->SetRelativeScale3D(Scale);
    Part->SetCollisionProfileName(TEXT("BlockAllDynamic"));
    Part->AttachToComponent(SceneRoot, FAttachmentTransformRules::KeepRelativeTransform);
    Part->RegisterComponent();
    GeneratedParts.Add(Part);
    return Part;
}

void ALVWorkshopActor::ClearGenerated()
{
    for (UStaticMeshComponent* Part : GeneratedParts)
    {
        if (IsValid(Part)) Part->DestroyComponent();
    }
    GeneratedParts.Reset();
}

void ALVWorkshopActor::GenerateTree()
{
    ClearGenerated();
    AddPart(TEXT("Cylinder"), FVector(0, 0, 150), FVector(0.45f, 0.45f, 1.5f));
    AddPart(TEXT("Cone"), FVector(0, 0, 360), FVector(2.4f, 2.4f, 2.2f));
    AddPart(TEXT("Cone"), FVector(0, 0, 520), FVector(1.75f, 1.75f, 1.8f));
    AddPart(TEXT("Cone"), FVector(0, 0, 650), FVector(1.1f, 1.1f, 1.45f));
}

void ALVWorkshopActor::GenerateChair()
{
    ClearGenerated();
    AddPart(TEXT("Cube"), FVector(0, 0, 45), FVector(70, 70, 8));
    AddPart(TEXT("Cube"), FVector(0, 0, 125), FVector(70, 8, 90));
    AddPart(TEXT("Cube"), FVector(-52, -52, 0), FVector(8, 8, 45));
    AddPart(TEXT("Cube"), FVector(52, -52, 0), FVector(8, 8, 45));
    AddPart(TEXT("Cube"), FVector(-52, 52, 0), FVector(8, 8, 45));
    AddPart(TEXT("Cube"), FVector(52, 52, 0), FVector(8, 8, 45));
}

void ALVWorkshopActor::GenerateBrick()
{
    ClearGenerated();
    AddPart(TEXT("Cube"), FVector(0, 0, 20), FVector(100, 48, 20));
}

void ALVWorkshopActor::GenerateWeapon()
{
    ClearGenerated();
    AddPart(TEXT("Cylinder"), FVector(0, 0, 75), FVector(8, 8, 75), FRotator(0, 90, 0));
    AddPart(TEXT("Cube"), FVector(0, 0, 175), FVector(42, 8, 15));
    AddPart(TEXT("Cone"), FVector(50, 0, 175), FVector(12, 12, 20), FRotator(0, 90, 0));
}

void ALVWorkshopActor::GenerateClothingPrototype()
{
    ClearGenerated();
    // A simple garment block is deliberately kept separate from the human
    // mesh. The next pass will replace it with a skeletal garment and morph
    // targets that follow the MakeHuman body.
    AddPart(TEXT("Cube"), FVector(0, 0, 95), FVector(45, 28, 65));
    AddPart(TEXT("Cylinder"), FVector(-55, 0, 100), FVector(12, 12, 50), FRotator(0, 90, 0));
    AddPart(TEXT("Cylinder"), FVector(55, 0, 100), FVector(12, 12, 50), FRotator(0, 90, 0));
}
