#include "LVGameMode.h"

#include "Camera/CameraActor.h"
#include "Camera/CameraComponent.h"
#include "Engine/StaticMesh.h"
#include "Engine/World.h"
#include "GameFramework/PlayerController.h"
#include "Components/StaticMeshComponent.h"
#include "LVCharacterCreatorWidget.h"
#include "LVCharacterGenerator.h"
#include "LVWorkshopActor.h"

void ALVGameMode::BeginPlay()
{
    Super::BeginPlay();

    UWorld* World = GetWorld();
    if (!World) return;

    ALVCharacterGenerator* Generator = World->SpawnActor<ALVCharacterGenerator>(FVector::ZeroVector, FRotator::ZeroRotator);
    ALVWorkshopActor* Workshop = World->SpawnActor<ALVWorkshopActor>(FVector(360.0f, 60.0f, 0.0f), FRotator::ZeroRotator);
    if (Workshop) Workshop->GenerateTree();

    // A simple presentation floor keeps the migration usable before the first
    // authored Unreal level is created. It is replaced by the village level.
    UStaticMesh* Cube = LoadObject<UStaticMesh>(nullptr, TEXT("/Engine/BasicShapes/Cube.Cube"));
    if (Cube)
    {
        UStaticMeshComponent* Floor = NewObject<UStaticMeshComponent>(this, TEXT("PresentationFloor"));
        Floor->SetStaticMesh(Cube);
        Floor->SetWorldLocation(FVector(150.0f, 80.0f, -12.0f));
        Floor->SetWorldScale3D(FVector(8.0f, 6.0f, 0.1f));
        Floor->RegisterComponent();
    }

    APlayerController* Controller = World->GetFirstPlayerController();
    if (!Controller) return;

    FActorSpawnParameters CameraParams;
    ACameraActor* Camera = World->SpawnActor<ACameraActor>(FVector(50.0f, -850.0f, 320.0f), FRotator::ZeroRotator, CameraParams);
    if (Camera)
    {
        const FVector Target(160.0f, 40.0f, 105.0f);
        Camera->SetActorRotation((Target - Camera->GetActorLocation()).Rotation());
        Camera->GetCameraComponent()->FieldOfView = 48.0f;
        Controller->SetViewTarget(Camera);
    }

    Controller->bShowMouseCursor = true;
    Controller->bEnableClickEvents = true;
    Controller->bEnableMouseOverEvents = true;
    FInputModeGameAndUI InputMode;
    InputMode.SetHideCursorDuringCapture(false);
    InputMode.SetLockMouseToViewportBehavior(EMouseLockMode::DoNotLock);
    Controller->SetInputMode(InputMode);

    ULVCharacterCreatorWidget* Widget = CreateWidget<ULVCharacterCreatorWidget>(Controller, ULVCharacterCreatorWidget::StaticClass());
    if (Widget)
    {
        Widget->AddToViewport(10);
    }

    UE_LOG(LogTemp, Log, TEXT("LibreVies Unreal: createur MakeHuman et atelier prets (%s)"),
        Generator && Generator->IsSourceLoaded() ? TEXT("base CC0 chargee") : TEXT("base CC0 introuvable"));
}
