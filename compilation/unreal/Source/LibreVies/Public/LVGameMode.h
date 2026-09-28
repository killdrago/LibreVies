#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "LVGameMode.generated.h"

UCLASS()
class LIBREVIES_API ALVGameMode : public AGameModeBase
{
    GENERATED_BODY()

public:
    virtual void BeginPlay() override;
};
