// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Kismet/BlueprintFunctionLibrary.h"
#include "WireframePanoramasAdminPaths.generated.h"

/**
 * Blueprint Function Library для построения путей API Talent Chessboard Panoramas Admin.
 * Соответствует путям из Swagger: https://public-api.bytalent.ru/api/chessboard/panoramas/admin/swagger/v1/swagger.json
 *
 * Используется из C++ (PanoramasAdmin_* функции) и из Blueprint при ручной сборке запросов.
 */
UCLASS(meta = (ShortToolTip = "Paths for Panoramas Admin API"))
class WIREFRAME_API UWireframePanoramasAdminPathsLibrary : public UBlueprintFunctionLibrary
{
	GENERATED_BODY()

public:

	/**
	 * GET /api/{tenantId}/chessboard/panoramas/queue-tasks/projects/{chessboardProjectId}
	 * Список задач в очереди по ИД проекта (query: Paging.Offset, Paging.Take).
	 */
	UFUNCTION(BlueprintPure, Category = "Wireframe|Panoramas Admin|Paths",
		meta = (DisplayName = "Path Queue Tasks By Project"))
	static FString PathQueueTasksByProject(const FString& TenantId, int64 ChessboardProjectId);

	/**
	 * POST /api/{tenantId}/chessboard/panoramas/queue-tasks/{taskId}/start
	 * Подтвердить взятие задачи в работу.
	 */
	UFUNCTION(BlueprintPure, Category = "Wireframe|Panoramas Admin|Paths",
		meta = (DisplayName = "Path Start Queue Task"))
	static FString PathStartQueueTask(const FString& TenantId, const FString& TaskId);

	/**
	 * GET /api/{tenantId}/chessboard/panoramas/panorama-points/layouts/{chessboardPropertyLayoutId}/presets/{presetId}/points
	 * Список точек съёмки по ИД планировки (с файлами панорам по пресету).
	 */
	UFUNCTION(BlueprintPure, Category = "Wireframe|Panoramas Admin|Paths",
		meta = (DisplayName = "Path Layout Panorama Points"))
	static FString PathLayoutPanoramaPoints(const FString& TenantId, int64 ChessboardPropertyLayoutId, const FString& PresetId);

	/**
	 * POST /api/{tenantId}/chessboard/panoramas/panorama-points/points/{pointId}/presets/{presetId}/upload-layout-image
	 * Загрузить изображение панорамы (кубическая карта) для точки в планировке (multipart CubemapImageFile).
	 */
	UFUNCTION(BlueprintPure, Category = "Wireframe|Panoramas Admin|Paths",
		meta = (DisplayName = "Path Upload Layout Panorama Image"))
	static FString PathUploadLayoutPanoramaImage(const FString& TenantId, const FString& PointId, const FString& PresetId);

	/**
	 * DELETE /api/{tenantId}/chessboard/panoramas/panorama-points/{pointId}
	 * Удалить точку съёмки.
	 */
	UFUNCTION(BlueprintPure, Category = "Wireframe|Panoramas Admin|Paths",
		meta = (DisplayName = "Path Remove Panorama Shooting Point"))
	static FString PathRemovePanoramaShootingPoint(const FString& TenantId, const FString& PointId);
};
