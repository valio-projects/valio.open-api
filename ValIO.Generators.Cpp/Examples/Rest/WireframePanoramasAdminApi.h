// Copyright Epic Games, Inc. All Rights Reserved.

#pragma once

#include "CoreMinimal.h"
#include "Async/Future.h"
#include "Blueprint/ValIOTypedAsyncActionBase.h"
#include "WireframePanoramasAdminApi.generated.h"

// ---------------------------------------------------------------------------
// ValIO forward declarations
// ---------------------------------------------------------------------------

class FValIOClient;
using FValIOClientRef = TSharedRef<FValIOClient>;
struct FValIOResponse;

// ---------------------------------------------------------------------------
// DTOs по Swagger: Talent.Chessboard.Panoramas.Admin.Api
// https://public-api.bytalent.ru/api/chessboard/panoramas/admin/swagger/v1/swagger.json
// ---------------------------------------------------------------------------

/** Point { x, y } */
USTRUCT(BlueprintType)
struct FPanoramaPointDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	double x = 0.0;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	double y = 0.0;
};


USTRUCT(BlueprintType)
struct FRestApiErrorEntry
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString message;
};

USTRUCT(BlueprintType)
struct FRestApiExceptionEntry
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString message;
};


USTRUCT(BlueprintType)
struct FRestApiErrorResponse
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FRestApiErrorEntry Error;
};


USTRUCT(BlueprintType)
struct FRestApiExceptionResponse
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FRestApiExceptionEntry Exception;
};


USTRUCT(BlueprintType)
struct FRestApiResponse
{
	GENERATED_BODY()
	
	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FRestApiErrorEntry Error;
	
	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FRestApiExceptionEntry Exception;
};

/** LayoutModel */
USTRUCT(BlueprintType)
struct FPanoramaLayoutModelDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	int64 layoutId = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString name;
};

/** ProjectModel */
USTRUCT(BlueprintType)
struct FPanoramaProjectModelDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	int64 projectId = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString name;
};

/** PagingResultMeta */
USTRUCT(BlueprintType)
struct FPanoramaPagingMetaDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	int64 total = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	int32 take = 0;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	int32 offset = 0;
};

/** PointsModel (pointId, position, angle) */
USTRUCT(BlueprintType)
struct FPanoramaPointsModelDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString pointId;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FPanoramaPointDto position;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	double angle = 0.0;
};

/** GetPanoramasQueueTasksListByProjectIdQueryResponseEntry (элемент списка задач) */
USTRUCT(BlueprintType)
struct FPanoramaQueueTaskEntryDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString queueTaskId;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString createdAt;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString startedAt;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString completedAt;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString queueId;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString taskType;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString status;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	TArray<FString> presetIds;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	TArray<FPanoramaPointsModelDto> points;
};

/** GetPanoramasQueueTasksListByProjectIdQueryResponse.tasks (paging result) */
USTRUCT(BlueprintType)
struct FPanoramaQueueTasksPagingDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	TArray<FPanoramaQueueTaskEntryDto> items;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FPanoramaPagingMetaDto meta;
};

/** GetPanoramasQueueTasksListByProjectIdQueryResponse */
USTRUCT(BlueprintType)
struct FPanoramaQueueTasksListResponseDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FPanoramaProjectModelDto project;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FPanoramaQueueTasksPagingDto tasks;
};

/** StartQueueTaskCommandResponse / HeartbeatQueueTaskCommandResponse / CompleteQueueTaskCommandResponse */
USTRUCT(BlueprintType)
struct FPanoramaTaskIdResponseDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString taskId;
};

/** GetPanoramaShootingPointsListQueryResponseEntry */
USTRUCT(BlueprintType)
struct FPanoramaShootingPointEntryDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString pointId;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString name;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FPanoramaPointDto point;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	double angle = 0.0;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString pointType;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString panoramaPointFileId;
};

/** GetPanoramaShootingPointsListQueryResponse */
USTRUCT(BlueprintType)
struct FPanoramaShootingPointsListResponseDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	TArray<FPanoramaShootingPointEntryDto> points;
};

/** UploadLayoutPanoramaImageCommandResponse */
USTRUCT(BlueprintType)
struct FPanoramaUploadLayoutImageResponseDto
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString pointId;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FString layoutPanoramaPointFileId;

	UPROPERTY(BlueprintReadWrite, Category = "Panoramas")
	FPanoramaLayoutModelDto layout;
};

// ---------------------------------------------------------------------------
// Typed API results (200 = success DTO, 400/500 = FRestApiResponse; one of fields set)
// ---------------------------------------------------------------------------

/** Result for GetQueueTasksListByProjectId: 200 -> tasks list, 400/500 -> ErrorBody (Error or Exception set). */
USTRUCT(BlueprintType, meta = (ShortToolTip = "Result of Get Queue Tasks List By Project Id"))
struct FPanoramaGetQueueTasksListResult
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	int32 StatusCode = 0;

	/** Valid when StatusCode == 200. */
	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FPanoramaQueueTasksListResponseDto SuccessBody;

	/** Valid when StatusCode 400 or 500. For 400 use Error.message, for 500 use Exception.message. */
	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FRestApiResponse ErrorBody;

	bool IsSuccess() const { return StatusCode >= 200 && StatusCode < 300; }
};

/** Result for StartQueueTask. */
USTRUCT(BlueprintType)
struct FPanoramaStartQueueTaskResult
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	int32 StatusCode = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FPanoramaTaskIdResponseDto SuccessBody;

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FRestApiResponse ErrorBody;

	bool IsSuccess() const { return StatusCode >= 200 && StatusCode < 300; }
};

/** Result for GetObjectLayoutPanoramaPointsList. */
USTRUCT(BlueprintType)
struct FPanoramaGetLayoutPanoramaPointsResult
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	int32 StatusCode = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FPanoramaShootingPointsListResponseDto SuccessBody;

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FRestApiResponse ErrorBody;

	bool IsSuccess() const { return StatusCode >= 200 && StatusCode < 300; }
};

/** Result for UploadLayoutPanoramaImage. */
USTRUCT(BlueprintType)
struct FPanoramaUploadLayoutImageResult
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	int32 StatusCode = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FPanoramaUploadLayoutImageResponseDto SuccessBody;

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FRestApiResponse ErrorBody;

	bool IsSuccess() const { return StatusCode >= 200 && StatusCode < 300; }
};

/** Result for RemovePanoramaShootingPoint (204 no content or error). */
USTRUCT(BlueprintType)
struct FPanoramaRemoveShootingPointResult
{
	GENERATED_BODY()

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	int32 StatusCode = 0;

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FRestApiResponse ErrorBody;

	bool IsSuccess() const { return StatusCode >= 200 && StatusCode < 300; }
};

// ---------------------------------------------------------------------------
// Params structs (BlueprintType + UPROPERTY for Blueprint init)
// ---------------------------------------------------------------------------

USTRUCT(BlueprintType)
struct FPanoramaGetQueueTasksListParams
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString TenantId;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	int64 ChessboardProjectId = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	int32 PagingOffset = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	int32 PagingTake = 30;
};

USTRUCT(BlueprintType)
struct FPanoramaStartQueueTaskParams
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString TenantId;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString TaskId;
};

USTRUCT(BlueprintType)
struct FPanoramaGetLayoutPanoramaPointsParams
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString TenantId;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	int64 ChessboardPropertyLayoutId = 0;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString PresetId;
};

USTRUCT(BlueprintType)
struct FPanoramaUploadLayoutImageParams
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString TenantId;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString PointId;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString PresetId;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	TArray<uint8> CubemapImageBytes;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString FileName;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString MimeType = TEXT("image/jpeg");
};

USTRUCT(BlueprintType)
struct FPanoramaRemoveShootingPointParams
{
	GENERATED_BODY()

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString TenantId;

	UPROPERTY(EditAnywhere, BlueprintReadWrite, Category = "Panoramas")
	FString PointId;
};

// ---------------------------------------------------------------------------
// API-методы (клиент ValIO, base URL: https://public-api.bytalent.ru)
// Пути: /api/{tenantId}/chessboard/panoramas/...
// ---------------------------------------------------------------------------

/**
 * Получить список задач в очереди по ИД проекта.
 * GET /api/{tenantId}/chessboard/panoramas/queue-tasks/projects/{chessboardProjectId}
 * Returns: 200 -> FPanoramaQueueTasksListResponseDto, 400/500 -> FRestApiResponse (Error or Exception).
 */
TFuture<FPanoramaGetQueueTasksListResult> PanoramasAdmin_GetQueueTasksListByProjectId(
	FValIOClientRef Client,
	const FPanoramaGetQueueTasksListParams& Params);

/**
 * Подтвердить взятие задачи в работу.
 * POST /api/{tenantId}/chessboard/panoramas/queue-tasks/{taskId}/start
 */
TFuture<FPanoramaStartQueueTaskResult> PanoramasAdmin_StartQueueTask(
	FValIOClientRef Client,
	const FPanoramaStartQueueTaskParams& Params);

/**
 * Получить список точек съёмки по ИД планировки (с файлами панорам по пресету).
 */
TFuture<FPanoramaGetLayoutPanoramaPointsResult> PanoramasAdmin_GetObjectLayoutPanoramaPointsList(
	FValIOClientRef Client,
	const FPanoramaGetLayoutPanoramaPointsParams& Params);

/**
 * Загрузить изображение панорамы (кубическая карта) для точки в планировке.
 * Body: multipart/form-data, поле CubemapImageFile
 */
TFuture<FPanoramaUploadLayoutImageResult> PanoramasAdmin_UploadLayoutPanoramaImage(
	FValIOClientRef Client,
	const FPanoramaUploadLayoutImageParams& Params);

/**
 * Удалить точку съёмки.
 * DELETE /api/{tenantId}/chessboard/panoramas/panorama-points/{pointId}
 */
TFuture<FPanoramaRemoveShootingPointResult> PanoramasAdmin_RemovePanoramaShootingPoint(
	FValIOClientRef Client,
	const FPanoramaRemoveShootingPointParams& Params);

// ---------------------------------------------------------------------------
// Blueprint / UFunction entry points
// ---------------------------------------------------------------------------

DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnPanoramaGetQueueTasksListCompleted, FPanoramaGetQueueTasksListResult, Result);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnPanoramaStartQueueTaskCompleted, FPanoramaStartQueueTaskResult, Result);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnPanoramaGetLayoutPanoramaPointsCompleted, FPanoramaGetLayoutPanoramaPointsResult, Result);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnPanoramaUploadLayoutImageCompleted, FPanoramaUploadLayoutImageResult, Result);
DECLARE_DYNAMIC_MULTICAST_DELEGATE_OneParam(FOnPanoramaRemoveShootingPointCompleted, FPanoramaRemoveShootingPointResult, Result);

/** Async action for GetQueueTasksListByProjectId. */
UCLASS(meta = (ExposedAsyncProxy = "AsyncTask"))
class WIREFRAME_API UPanoramasAdminGetQueueTasksListAsync : public UValIOAsyncActionBase
{
	GENERATED_BODY()
public:
	UPROPERTY(BlueprintAssignable, meta = (DisplayName = "On Completed"))
	FOnPanoramaGetQueueTasksListCompleted OnCompleted;

	UPROPERTY(BlueprintReadOnly, Category = "Panoramas")
	FPanoramaGetQueueTasksListResult LastResult;

	UFUNCTION(BlueprintCallable, Category = "Wireframe|Panoramas Admin",
		meta = (BlueprintInternalUseOnly = "true", WorldContext = "WorldContextObject",
			DisplayName = "Get Queue Tasks List By Project Id"))
	static UPanoramasAdminGetQueueTasksListAsync* GetQueueTasksListByProjectId(
		UObject* WorldContextObject,
		const FString& ClientName,
		const FPanoramaGetQueueTasksListParams& Params);

	virtual void Activate() override;

	void Complete(const FPanoramaGetQueueTasksListResult& Result);

protected:
	virtual UObject* GetWorldContext() const override { return State.WorldContext.Get(); }
	virtual FString GetClientName() const override { return State.ClientName; }

public:
	/** Used by UValIOAsyncActionBase::NewAsyncObject. */
	FValIOAsyncState<FPanoramaGetQueueTasksListParams> State;
};

UCLASS(meta = (ExposedAsyncProxy = "AsyncTask"))
class WIREFRAME_API UPanoramasAdminStartQueueTaskAsync : public UValIOAsyncActionBase
{
	GENERATED_BODY()
public:
	UPROPERTY(BlueprintAssignable)
	FOnPanoramaStartQueueTaskCompleted OnCompleted;
	UPROPERTY(BlueprintReadOnly)
	FPanoramaStartQueueTaskResult LastResult;

	UFUNCTION(BlueprintCallable, Category = "Wireframe|Panoramas Admin",
		meta = (BlueprintInternalUseOnly = "true", WorldContext = "WorldContextObject"))
	static UPanoramasAdminStartQueueTaskAsync* StartQueueTask(
		UObject* WorldContextObject, const FString& ClientName, const FPanoramaStartQueueTaskParams& Params);

	virtual void Activate() override;

	void Complete(const FPanoramaStartQueueTaskResult& Result);

protected:
	virtual UObject* GetWorldContext() const override { return State.WorldContext.Get(); }
	virtual FString GetClientName() const override { return State.ClientName; }

public:
	FValIOAsyncState<FPanoramaStartQueueTaskParams> State;
};

UCLASS(meta = (ExposedAsyncProxy = "AsyncTask"))
class WIREFRAME_API UPanoramasAdminGetLayoutPanoramaPointsAsync : public UValIOAsyncActionBase
{
	GENERATED_BODY()
public:
	UPROPERTY(BlueprintAssignable)
	FOnPanoramaGetLayoutPanoramaPointsCompleted OnCompleted;
	UPROPERTY(BlueprintReadOnly)
	FPanoramaGetLayoutPanoramaPointsResult LastResult;

	UFUNCTION(BlueprintCallable, Category = "Wireframe|Panoramas Admin",
		meta = (BlueprintInternalUseOnly = "true", WorldContext = "WorldContextObject"))
	static UPanoramasAdminGetLayoutPanoramaPointsAsync* GetObjectLayoutPanoramaPointsList(
		UObject* WorldContextObject, const FString& ClientName, const FPanoramaGetLayoutPanoramaPointsParams& Params);

	virtual void Activate() override;

	void Complete(const FPanoramaGetLayoutPanoramaPointsResult& Result);

protected:
	virtual UObject* GetWorldContext() const override { return State.WorldContext.Get(); }
	virtual FString GetClientName() const override { return State.ClientName; }

public:
	FValIOAsyncState<FPanoramaGetLayoutPanoramaPointsParams> State;
};

UCLASS(meta = (ExposedAsyncProxy = "AsyncTask"))
class WIREFRAME_API UPanoramasAdminUploadLayoutImageAsync : public UValIOAsyncActionBase
{
	GENERATED_BODY()
public:
	UPROPERTY(BlueprintAssignable)
	FOnPanoramaUploadLayoutImageCompleted OnCompleted;
	UPROPERTY(BlueprintReadOnly)
	FPanoramaUploadLayoutImageResult LastResult;

	UFUNCTION(BlueprintCallable, Category = "Wireframe|Panoramas Admin",
		meta = (BlueprintInternalUseOnly = "true", WorldContext = "WorldContextObject"))
	static UPanoramasAdminUploadLayoutImageAsync* UploadLayoutPanoramaImage(
		UObject* WorldContextObject, const FString& ClientName, const FPanoramaUploadLayoutImageParams& Params);

	virtual void Activate() override;

	void Complete(const FPanoramaUploadLayoutImageResult& Result);

protected:
	virtual UObject* GetWorldContext() const override { return State.WorldContext.Get(); }
	virtual FString GetClientName() const override { return State.ClientName; }

public:
	FValIOAsyncState<FPanoramaUploadLayoutImageParams> State;
};

UCLASS(meta = (ExposedAsyncProxy = "AsyncTask"))
class WIREFRAME_API UPanoramasAdminRemoveShootingPointAsync : public UValIOAsyncActionBase
{
	GENERATED_BODY()
public:
	UPROPERTY(BlueprintAssignable)
	FOnPanoramaRemoveShootingPointCompleted OnCompleted;
	UPROPERTY(BlueprintReadOnly)
	FPanoramaRemoveShootingPointResult LastResult;

	UFUNCTION(BlueprintCallable, Category = "Wireframe|Panoramas Admin",
		meta = (BlueprintInternalUseOnly = "true", WorldContext = "WorldContextObject"))
	static UPanoramasAdminRemoveShootingPointAsync* RemovePanoramaShootingPoint(
		UObject* WorldContextObject, const FString& ClientName, const FPanoramaRemoveShootingPointParams& Params);

	virtual void Activate() override;

	void Complete(const FPanoramaRemoveShootingPointResult& Result);

protected:
	virtual UObject* GetWorldContext() const override { return State.WorldContext.Get(); }
	virtual FString GetClientName() const override { return State.ClientName; }

public:
	FValIOAsyncState<FPanoramaRemoveShootingPointParams> State;
};
