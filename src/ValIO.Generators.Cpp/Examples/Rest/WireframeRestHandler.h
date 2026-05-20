// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "SBaseRequestsHandler.h"
#include "WireframeRestHandler.generated.h"

class USRequestsProcessor;

/**
 * Минимальный handler для доступа к USRequestsProcessor через USRequestsSubsystem.
 * Используется в WireframeRestGameMode для загрузки HDR/panorama через SREST.
 * Публичный геттер процессора, т.к. GetProcessor() в базовом классе protected.
 */
UCLASS(NotBlueprintable)
class WIREFRAME_API UWireframeRestHandler : public USBaseRequestsHandler
{
	GENERATED_BODY()

public:

	/** Возвращает процессор запросов (для кода вне handler). */
	UFUNCTION(BlueprintPure, Category = "REST")
	USRequestsProcessor* GetRequestsProcessor() const { return GetProcessor(); }
};
