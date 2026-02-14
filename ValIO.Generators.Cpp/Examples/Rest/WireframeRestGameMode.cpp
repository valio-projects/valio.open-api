// Copyright Epic Games, Inc. All Rights Reserved.

#include "WireframeRestGameMode.h"
#include "WireframeRestHandler.h"
#include "WireframePanoramasAdminApi.h"
#include "SRequestsProcessor.h"
#include "SRequestsSubsystem.h"
#include "SRequest.h"
#include "SMultipartFormData.h"
#include "Blueprint/ValIOSubsystem.h"
#include "Client/ValIOClientBuilder.h"
#include "Kismet/KismetSystemLibrary.h"
#include "Misc/FileHelper.h"
#include "Engine/Engine.h"
#include "Engine/World.h"
#include "GameFramework/GameModeBase.h"

void AWireframeRestGameMode::BeginPlay()
{
	Super::BeginPlay();

	// Примеры ValIO: Talent Chessboard Panoramas Admin API
	RunPanoramasAdminApiExamples();

	// SREST: загрузка HDR/panorama (как раньше)
	const FString ApiEndpoint = TEXT("https://public-api.bytalent.ru/api/chessboard/panoramas/admin");
	const int32 FrameIndex = 0;
	const FString PointID = TEXT("019c4764-b0c9-7460-b3e6-377c9cd97a3f");
	const FString TenantID = TEXT("019bfe6c-6c09-73bb-a147-c41e8b5d945d");
	const FString PresetID = TEXT("019bfe6c-6c09-73bb-a147-c41e8b5d945d");
	SendHDRIForPoint(ApiEndpoint, FrameIndex, PointID, TenantID, PresetID);
}

void AWireframeRestGameMode::OnRestRequestError(const int32& InCode, const FString& InMessage)
{
	UE_LOG(LogActor, Error, TEXT("WireframeRestGameMode REST error: code=%d message=%s"), InCode, *InMessage);
}

bool AWireframeRestGameMode::SendHDRIForPoint(const FString& ApiEndpoint, int32 FrameIndex, const FString& PointID, const FString& TenantID, const FString& PresetID)
{
	USRequestsSubsystem* Subsystem = GEngine ? GEngine->GetEngineSubsystem<USRequestsSubsystem>() : nullptr;
	if (!Subsystem)
	{
		UE_LOG(LogActor, Error, TEXT("WireframeRestGameMode: USRequestsSubsystem not found."));
		return false;
	}

	UWireframeRestHandler* Handler = Subsystem->GetHandler<UWireframeRestHandler>(ApiEndpoint);
	USRequestsProcessor* Processor = Handler ? Handler->GetRequestsProcessor() : nullptr;
	if (!Processor)
	{
		UE_LOG(LogActor, Error, TEXT("WireframeRestGameMode: failed to get SREST processor."));
		return false;
	}

	TArray<uint8> Binary;
	FNumberFormattingOptions Config;
	Config.MinimumIntegralDigits = 4;
	Config.MaximumIntegralDigits = 4;
	Config.UseGrouping = false;

	FString FileName = FText::AsNumber(FrameIndex, &Config).ToString() + TEXT(".jpeg");
	FString Path = UKismetSystemLibrary::GetProjectSavedDirectory() + TEXT("MovieRenders/") + FileName;

	if (!FFileHelper::LoadFileToArray(Binary, *Path))
	{
		UE_LOG(LogActor, Error, TEXT("WireframeRestGameMode: can't find file %s"), *Path);
		return false;
	}

	// Multipart/form-data через SREST
	FMultipartFormData FormData;
	FormData.AddFile(TEXT("CubemapImageFile"), FileName, MoveTemp(Binary), TEXT("image/jpeg"));

	FString Method = FString::Printf(TEXT("/api/%s/chessboard/panoramas/panorama-points/points/%s/presets/%s/upload-layout-image"), *TenantID, *PointID, *PresetID);
	FSRequestRef Request = Processor->CreateRequest(this, Method, ESRequestType::VERB_POST, ESRequestContentType::Form);

	Request->BindErrorCallback().AddUObject(this, &AWireframeRestGameMode::OnRestRequestError);
	const bool bSent = Processor->SendRequest(Request, FormData);
	
	if (!bSent)
	{
		UE_LOG(LogActor, Error, TEXT("WireframeRestGameMode: failed to send request."));
		return false;
	}

	return true;
}

void AWireframeRestGameMode::RunPanoramasAdminApiExamples()
{
	UGameInstance* GameInstance = GetWorld() ? GetWorld()->GetGameInstance() : nullptr;
	UValIOSubsystem* ValIO = GameInstance ? GameInstance->GetSubsystem<UValIOSubsystem>() : nullptr;
	if (!ValIO)
	{
		UE_LOG(LogActor, Warning, TEXT("WireframeRestGameMode: UValIOSubsystem not found, skip Panoramas Admin examples."));
		return;
	}

	
	
	const FString BaseUrl = TEXT("https://public-api.bytalent.ru");
	ValIO->RegisterClient(TEXT("PanoramasAdmin"), [BaseUrl](FValIOClientBuilder& B)
	{
		B.SetName(TEXT("PanoramasAdmin"))
		 .SetBaseUrl(BaseUrl)
		 .EnableLogging();
	});

	FValIOClientRef Client = ValIO->GetClient(TEXT("PanoramasAdmin"));
	
	const FString TenantId = TEXT("019bfe6c-6c09-73bb-a147-c41e8b5d945d");
	const FString PresetId = TEXT("019bfe6c-6c09-73bb-a147-c41e8b5d945d");
	const FString PointId = TEXT("019c4764-b0c9-7460-b3e6-377c9cd97a3f");
	const int64 ProjectId = 1;
	const int64 LayoutId = 1;
	const FString TaskId = TEXT("00000000-0000-0000-0000-000000000001");

	// 1) GetPanoramasQueueTasksListByProjectId — typed result (200 SuccessBody, 400/500 ErrorBody)
	FPanoramaGetQueueTasksListParams GetListParams;
	GetListParams.TenantId = TenantId;
	GetListParams.ChessboardProjectId = ProjectId;
	GetListParams.PagingOffset = 0;
	GetListParams.PagingTake = 10;
	PanoramasAdmin_GetQueueTasksListByProjectId(Client, GetListParams)
		.Next([this](FPanoramaGetQueueTasksListResult Result)
		{
			if (Result.IsSuccess())
			{
				UE_LOG(LogActor, Log, TEXT("PanoramasAdmin GetQueueTasksListByProjectId: project=%s, tasks count=%d"),
					*Result.SuccessBody.project.name, Result.SuccessBody.tasks.items.Num());
			}
			else
			{
				const FString& Msg = (Result.StatusCode >= 500)
					? Result.ErrorBody.Exception.message
					: Result.ErrorBody.Error.message;
				UE_LOG(LogActor, Warning, TEXT("PanoramasAdmin GetQueueTasksListByProjectId: code=%d message=%s"),
					Result.StatusCode, *Msg);
			}
		});

	// 2) StartQueueTask
	FPanoramaStartQueueTaskParams StartTaskParams;
	StartTaskParams.TenantId = TenantId;
	StartTaskParams.TaskId = TaskId;
	PanoramasAdmin_StartQueueTask(Client, StartTaskParams)
		.Next([this](FPanoramaStartQueueTaskResult Result)
		{
			if (Result.IsSuccess())
			{
				UE_LOG(LogActor, Log, TEXT("PanoramasAdmin StartQueueTask: taskId=%s"), *Result.SuccessBody.taskId);
			}
			else
			{
				const FString& Msg = (Result.StatusCode >= 500) ? Result.ErrorBody.Exception.message : Result.ErrorBody.Error.message;
				UE_LOG(LogActor, Warning, TEXT("PanoramasAdmin StartQueueTask: code=%d message=%s"), Result.StatusCode, *Msg);
			}
		});

	// 3) GetObjectLayoutPanoramaPointsList
	FPanoramaGetLayoutPanoramaPointsParams GetPointsParams;
	GetPointsParams.TenantId = TenantId;
	GetPointsParams.ChessboardPropertyLayoutId = LayoutId;
	GetPointsParams.PresetId = PresetId;
	PanoramasAdmin_GetObjectLayoutPanoramaPointsList(Client, GetPointsParams)
		.Next([this](FPanoramaGetLayoutPanoramaPointsResult Result)
		{
			if (Result.IsSuccess())
			{
				UE_LOG(LogActor, Log, TEXT("PanoramasAdmin GetObjectLayoutPanoramaPointsList: points count=%d"), Result.SuccessBody.points.Num());
			}
			else
			{
				const FString& Msg = (Result.StatusCode >= 500) ? Result.ErrorBody.Exception.message : Result.ErrorBody.Error.message;
				UE_LOG(LogActor, Warning, TEXT("PanoramasAdmin GetObjectLayoutPanoramaPointsList: code=%d message=%s"), Result.StatusCode, *Msg);
			}
		});

	// 4) UploadLayoutPanoramaImage (пример: пустой массив байт — на сервере может вернуться ошибка валидации)
	FPanoramaUploadLayoutImageParams UploadParams;
	UploadParams.TenantId = TenantId;
	UploadParams.PointId = PointId;
	UploadParams.PresetId = PresetId;
	UploadParams.CubemapImageBytes = {};
	UploadParams.FileName = TEXT("example.jpeg");
	UploadParams.MimeType = TEXT("image/jpeg");
	PanoramasAdmin_UploadLayoutPanoramaImage(Client, UploadParams)
		.Next([this](FPanoramaUploadLayoutImageResult Result)
		{
			if (Result.IsSuccess())
			{
				UE_LOG(LogActor, Log, TEXT("PanoramasAdmin UploadLayoutPanoramaImage: pointId=%s layoutPanoramaPointFileId=%s"),
					*Result.SuccessBody.pointId, *Result.SuccessBody.layoutPanoramaPointFileId);
			}
			else
			{
				const FString& Msg = (Result.StatusCode >= 500) ? Result.ErrorBody.Exception.message : Result.ErrorBody.Error.message;
				UE_LOG(LogActor, Warning, TEXT("PanoramasAdmin UploadLayoutPanoramaImage: code=%d message=%s"), Result.StatusCode, *Msg);
			}
		});

	// 5) RemovePanoramaShootingPoint
	const FString ExamplePointIdToRemove = TEXT("00000000-0000-0000-0000-000000000002");
	FPanoramaRemoveShootingPointParams RemoveParams;
	RemoveParams.TenantId = TenantId;
	RemoveParams.PointId = ExamplePointIdToRemove;
	PanoramasAdmin_RemovePanoramaShootingPoint(Client, RemoveParams)
		.Next([this](FPanoramaRemoveShootingPointResult Result)
		{
			if (Result.IsSuccess())
			{
				UE_LOG(LogActor, Log, TEXT("PanoramasAdmin RemovePanoramaShootingPoint: 200 OK"));
			}
			else
			{
				const FString& Msg = (Result.StatusCode >= 500) ? Result.ErrorBody.Exception.message : Result.ErrorBody.Error.message;
				UE_LOG(LogActor, Warning, TEXT("PanoramasAdmin RemovePanoramaShootingPoint: code=%d message=%s"), Result.StatusCode, *Msg);
			}
		});
}
