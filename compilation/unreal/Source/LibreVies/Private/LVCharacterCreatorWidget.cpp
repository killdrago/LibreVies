#include "LVCharacterCreatorWidget.h"

#include "Components/Button.h"
#include "Components/HorizontalBox.h"
#include "Components/HorizontalBoxSlot.h"
#include "Components/ScrollBox.h"
#include "Components/Slider.h"
#include "Components/TextBlock.h"
#include "Components/VerticalBox.h"
#include "Components/VerticalBoxSlot.h"
#include "EngineUtils.h"
#include "LVCharacterGenerator.h"
#include "LVWorkshopActor.h"

void ULVCharacterCreatorWidget::NativeConstruct()
{
    Super::NativeConstruct();
    FindActors();

    UScrollBox* Scroll = WidgetTree->ConstructWidget<UScrollBox>(UScrollBox::StaticClass());
    UVerticalBox* Panel = WidgetTree->ConstructWidget<UVerticalBox>(UVerticalBox::StaticClass());
    WidgetTree->RootWidget = Scroll;
    Scroll->AddChild(Panel);

    auto AddText = [&](const FString& Text, int32 Size) -> UTextBlock*
    {
        UTextBlock* Label = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
        Label->SetText(FText::FromString(Text));
        Label->SetFont(FSlateFontInfo(nullptr, Size));
        Label->SetColorAndOpacity(FSlateColor(FLinearColor::White));
        UVerticalBoxSlot* Slot = Panel->AddChildToVerticalBox(Label);
        Slot->SetPadding(FMargin(12.0f, 6.0f));
        return Label;
    };

    AddText(TEXT("LIBREVIES - CREATEUR UNREAL"), 25);
    AddText(TEXT("Base MakeHuman CC0 : homme ou femme, sans accessoire.\n"
                 "Les changements sont reutilisables plus tard au debut du jeu."), 14);

    UHorizontalBox* SexRow = WidgetTree->ConstructWidget<UHorizontalBox>(UHorizontalBox::StaticClass());
    Panel->AddChildToVerticalBox(SexRow)->SetPadding(FMargin(12.0f, 5.0f));
    UButton* Male = WidgetTree->ConstructWidget<UButton>(UButton::StaticClass());
    UTextBlock* MaleText = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
    MaleText->SetText(FText::FromString(TEXT("HOMME")));
    Male->SetContent(MaleText);
    SexRow->AddChildToHorizontalBox(Male)->SetPadding(FMargin(4.0f));
    Male->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnMale);

    UButton* Female = WidgetTree->ConstructWidget<UButton>(UButton::StaticClass());
    UTextBlock* FemaleText = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
    FemaleText->SetText(FText::FromString(TEXT("FEMME")));
    Female->SetContent(FemaleText);
    SexRow->AddChildToHorizontalBox(Female)->SetPadding(FMargin(4.0f));
    Female->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnFemale);

    AddText(TEXT("MORPHOLOGIE"), 18);
    auto AddSlider = [&](const FString& Name, USlider*& OutSlider)
    {
        AddText(Name, 13);
        OutSlider = WidgetTree->ConstructWidget<USlider>(USlider::StaticClass());
        OutSlider->SetValue(0.5f);
        Panel->AddChildToVerticalBox(OutSlider)->SetPadding(FMargin(16.0f, 2.0f));
    };

    USlider* Slider = nullptr;
    AddSlider(TEXT("Ventre"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnBelly);
    AddSlider(TEXT("Bras - epaisseur"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnArmThickness);
    AddSlider(TEXT("Bras - longueur"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnArmLength);
    AddSlider(TEXT("Jambes - epaisseur"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnLegThickness);
    AddSlider(TEXT("Jambes - longueur"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnLegLength);
    AddSlider(TEXT("Pieds"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnFeet);

    AddText(TEXT("VISAGE"), 18);
    AddSlider(TEXT("Tete"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnHead);
    AddSlider(TEXT("Yeux"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnEyes);
    AddSlider(TEXT("Nez"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnNose);
    AddSlider(TEXT("Bouche"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnMouth);
    AddSlider(TEXT("Oreilles"), Slider); Slider->OnValueChanged.AddDynamic(this, &ULVCharacterCreatorWidget::OnEars);

    auto AddActionButton = [&](const FString& Label) -> UButton*
    {
        UButton* Button = WidgetTree->ConstructWidget<UButton>(UButton::StaticClass());
        UTextBlock* ButtonText = WidgetTree->ConstructWidget<UTextBlock>(UTextBlock::StaticClass());
        ButtonText->SetText(FText::FromString(Label));
        Button->SetContent(ButtonText);
        Panel->AddChildToVerticalBox(Button)->SetPadding(FMargin(12.0f, 4.0f));
        return Button;
    };

    UButton* Random = AddActionButton(TEXT("ALEATOIRE"));
    Random->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnRandom);
    UButton* Reset = AddActionButton(TEXT("REINITIALISER"));
    Reset->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnReset);
    UButton* Save = AddActionButton(TEXT("SAUVEGARDER LE PRESET"));
    Save->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnSave);

    AddText(TEXT("ATELIER OBJETS - PROTOTYPES PROCEDURAUX"), 18);
    AddText(TEXT("Ces fonctions seront reutilisees pour fabriquer les objets du jeu.\n"
                 "Les meshes artistiques pourront remplacer les prototypes sans changer l'interface."), 13);
    UButton* Tree = AddActionButton(TEXT("GENERER UN ARBRE")); Tree->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnTree);
    UButton* Chair = AddActionButton(TEXT("GENERER UNE CHAISE")); Chair->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnChair);
    UButton* Brick = AddActionButton(TEXT("GENERER UNE BRIQUE")); Brick->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnBrick);
    UButton* Weapon = AddActionButton(TEXT("GENERER UNE ARME")); Weapon->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnWeapon);
    UButton* Clothing = AddActionButton(TEXT("GENERER UN PROTOTYPE DE VETEMENT")); Clothing->OnClicked.AddDynamic(this, &ULVCharacterCreatorWidget::OnClothing);

    StatusText = AddText(TEXT("Pret."), 13);
}

void ULVCharacterCreatorWidget::FindActors()
{
    if (!GetWorld()) return;
    for (TActorIterator<ALVCharacterGenerator> It(GetWorld()); It; ++It)
    {
        Generator = *It;
        break;
    }
    for (TActorIterator<ALVWorkshopActor> It(GetWorld()); It; ++It)
    {
        Workshop = *It;
        break;
    }
}

void ULVCharacterCreatorWidget::SetStatus(const FString& Text)
{
    if (StatusText) StatusText->SetText(FText::FromString(Text));
}

void ULVCharacterCreatorWidget::OnMale()
{
    if (Generator.IsValid()) { Generator->SetSex(false); SetStatus(TEXT("Base homme generee.")); }
}

void ULVCharacterCreatorWidget::OnFemale()
{
    if (Generator.IsValid()) { Generator->SetSex(true); SetStatus(TEXT("Base femme generee.")); }
}

void ULVCharacterCreatorWidget::OnRandom()
{
    if (Generator.IsValid()) { Generator->RandomizeCharacter(); SetStatus(TEXT("Nouvelle morphologie aleatoire.")); }
}

void ULVCharacterCreatorWidget::OnReset()
{
    if (Generator.IsValid()) { Generator->ResetCharacter(); SetStatus(TEXT("Personnage reinitialise.")); }
}

void ULVCharacterCreatorWidget::OnSave()
{
    if (Generator.IsValid() && Generator->SavePreset()) SetStatus(TEXT("Preset sauvegarde dans Saved/CharacterCreator."));
    else SetStatus(TEXT("Impossible de sauvegarder le preset."));
}

void ULVCharacterCreatorWidget::OnTree()
{
    if (Workshop.IsValid()) { Workshop->GenerateTree(); SetStatus(TEXT("Prototype arbre genere.")); }
}

void ULVCharacterCreatorWidget::OnChair()
{
    if (Workshop.IsValid()) { Workshop->GenerateChair(); SetStatus(TEXT("Prototype chaise genere.")); }
}

void ULVCharacterCreatorWidget::OnBrick()
{
    if (Workshop.IsValid()) { Workshop->GenerateBrick(); SetStatus(TEXT("Prototype brique genere.")); }
}

void ULVCharacterCreatorWidget::OnWeapon()
{
    if (Workshop.IsValid()) { Workshop->GenerateWeapon(); SetStatus(TEXT("Prototype arme genere.")); }
}

void ULVCharacterCreatorWidget::OnClothing()
{
    if (Workshop.IsValid()) { Workshop->GenerateClothingPrototype(); SetStatus(TEXT("Prototype vetement genere.")); }
}

void ULVCharacterCreatorWidget::OnBelly(float Value)
{
    if (Generator.IsValid()) { Generator->Belly = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnArmThickness(float Value)
{
    if (Generator.IsValid()) { Generator->ArmThickness = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnArmLength(float Value)
{
    if (Generator.IsValid()) { Generator->ArmLength = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnLegThickness(float Value)
{
    if (Generator.IsValid()) { Generator->LegThickness = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnLegLength(float Value)
{
    if (Generator.IsValid()) { Generator->LegLength = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnFeet(float Value)
{
    if (Generator.IsValid()) { Generator->FeetSize = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnHead(float Value)
{
    if (Generator.IsValid()) { Generator->HeadShape = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnEyes(float Value)
{
    if (Generator.IsValid()) { Generator->EyesShape = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnNose(float Value)
{
    if (Generator.IsValid()) { Generator->NoseShape = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnMouth(float Value)
{
    if (Generator.IsValid()) { Generator->MouthShape = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}

void ULVCharacterCreatorWidget::OnEars(float Value)
{
    if (Generator.IsValid()) { Generator->EarsShape = Value * 2.0f - 1.0f; Generator->BuildCharacter(); }
}
