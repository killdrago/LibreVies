#pragma once

#include "CoreMinimal.h"
#include "Blueprint/UserWidget.h"
#include "LVCharacterCreatorWidget.generated.h"

class ALVCharacterGenerator;
class ALVWorkshopActor;
class UTextBlock;

/** Native first version of the creator UI. It can later be moved to a styled UMG asset without changing the generator. */
UCLASS()
class LIBREVIES_API ULVCharacterCreatorWidget : public UUserWidget
{
    GENERATED_BODY()

public:
    virtual void NativeConstruct() override;

private:
    TWeakObjectPtr<ALVCharacterGenerator> Generator;
    TWeakObjectPtr<ALVWorkshopActor> Workshop;
    TObjectPtr<UTextBlock> StatusText;

    void FindActors();
    void SetStatus(const FString& Text);

    UFUNCTION()
    void OnMale();
    UFUNCTION()
    void OnFemale();
    UFUNCTION()
    void OnRandom();
    UFUNCTION()
    void OnReset();
    UFUNCTION()
    void OnSave();
    UFUNCTION()
    void OnTree();
    UFUNCTION()
    void OnChair();
    UFUNCTION()
    void OnBrick();
    UFUNCTION()
    void OnWeapon();
    UFUNCTION()
    void OnClothing();

    UFUNCTION()
    void OnBelly(float Value);
    UFUNCTION()
    void OnArmThickness(float Value);
    UFUNCTION()
    void OnArmLength(float Value);
    UFUNCTION()
    void OnLegThickness(float Value);
    UFUNCTION()
    void OnLegLength(float Value);
    UFUNCTION()
    void OnFeet(float Value);
    UFUNCTION()
    void OnHead(float Value);
    UFUNCTION()
    void OnEyes(float Value);
    UFUNCTION()
    void OnNose(float Value);
    UFUNCTION()
    void OnMouth(float Value);
    UFUNCTION()
    void OnEars(float Value);
};
