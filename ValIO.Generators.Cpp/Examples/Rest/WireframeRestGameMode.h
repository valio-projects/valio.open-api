// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "GameFramework/GameModeBase.h"
#include "WireframeRestGameMode.generated.h"

/**
 * Game Mode с примерами REST: SREST (загрузка HDR/panorama) и ValIO (Talent Chessboard Panoramas Admin API).
 * В BeginPlay регистрируется ValIO-клиент и вызываются примеры: GetPanoramasQueueTasksListByProjectId,
 * StartQueueTask, GetObjectLayoutPanoramaPointsList, UploadLayoutPanoramaImage, RemovePanoramaShootingPoint.
 */
UCLASS()
class WIREFRAME_API AWireframeRestGameMode : public AGameModeBase
{
	GENERATED_BODY()

protected:

	virtual void BeginPlay() override;

	/** Обработчик ошибки REST-запроса (логирование). Сигнатура должна совпадать с делегатом SREST. */
	UFUNCTION()
	void OnRestRequestError(const int32& InCode, const FString& InMessage);

private:

	/** Отправка изображения на сервер через SREST. Возвращает true, если запрос отправлен. */
	bool SendHDRIForPoint(const FString& ApiEndpoint, int32 FrameIndex, const FString& PointID, const FString& TenantID, const FString& PresetID);

	/** Примеры вызовов Panoramas Admin API через ValIO (регистрация клиента и 5 методов). */
	void RunPanoramasAdminApiExamples();
};
