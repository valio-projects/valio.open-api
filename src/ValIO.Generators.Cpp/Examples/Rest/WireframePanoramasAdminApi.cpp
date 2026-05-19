// Copyright Epic Games, Inc. All Rights Reserved.

#include "WireframePanoramasAdminApi.h"
#include "WireframePanoramasAdminPaths.h"
#include "Client/ValIOClient.h"
#include "Request/ValIORequestBuilder.h"
#include "Request/ValIOMultipartFormData.h"
#include "ValIOTypes.h"
#include "Response/ValIOResponse.h"
#include "Async/Async.h"

// ---------------------------------------------------------------------------
// Response parsing: 200 -> success DTO, 400/500 -> FRestApiResponse (Error or Exception)
// ---------------------------------------------------------------------------

namespace
{
	FPanoramaGetQueueTasksListResult ParseGetQueueTasksListResult(const FValIOResponse& R)
	{
		FPanoramaGetQueueTasksListResult Out;
		Out.StatusCode = R.StatusCode;
		if (R.IsSuccess())
		{
			const TOptional<FPanoramaQueueTasksListResponseDto> Dto = R.GetBodyAs<FPanoramaQueueTasksListResponseDto>();
			if (Dto.IsSet()) { Out.SuccessBody = Dto.GetValue(); }
		}
		else if (R.StatusCode >= 400 && R.StatusCode < 600)
		{
			const TOptional<FRestApiResponse> Err = R.GetBodyAs<FRestApiResponse>();
			if (Err.IsSet()) { Out.ErrorBody = Err.GetValue(); }
			else
			{
				// Fallback: put raw body into Exception.message
				Out.ErrorBody.Exception.message = R.GetBodyAsString();
			}
		}
		return Out;
	}

	FPanoramaStartQueueTaskResult ParseStartQueueTaskResult(const FValIOResponse& R)
	{
		FPanoramaStartQueueTaskResult Out;
		Out.StatusCode = R.StatusCode;
		if (R.IsSuccess())
		{
			const TOptional<FPanoramaTaskIdResponseDto> Dto = R.GetBodyAs<FPanoramaTaskIdResponseDto>();
			if (Dto.IsSet()) { Out.SuccessBody = Dto.GetValue(); }
		}
		else if (R.StatusCode >= 400 && R.StatusCode < 600)
		{
			const TOptional<FRestApiResponse> Err = R.GetBodyAs<FRestApiResponse>();
			if (Err.IsSet()) { Out.ErrorBody = Err.GetValue(); }
			else { Out.ErrorBody.Exception.message = R.GetBodyAsString(); }
		}
		return Out;
	}

	FPanoramaGetLayoutPanoramaPointsResult ParseGetLayoutPanoramaPointsResult(const FValIOResponse& R)
	{
		FPanoramaGetLayoutPanoramaPointsResult Out;
		Out.StatusCode = R.StatusCode;
		if (R.IsSuccess())
		{
			const TOptional<FPanoramaShootingPointsListResponseDto> Dto = R.GetBodyAs<FPanoramaShootingPointsListResponseDto>();
			if (Dto.IsSet()) { Out.SuccessBody = Dto.GetValue(); }
		}
		else if (R.StatusCode >= 400 && R.StatusCode < 600)
		{
			const TOptional<FRestApiResponse> Err = R.GetBodyAs<FRestApiResponse>();
			if (Err.IsSet()) { Out.ErrorBody = Err.GetValue(); }
			else { Out.ErrorBody.Exception.message = R.GetBodyAsString(); }
		}
		return Out;
	}

	FPanoramaUploadLayoutImageResult ParseUploadLayoutImageResult(const FValIOResponse& R)
	{
		FPanoramaUploadLayoutImageResult Out;
		Out.StatusCode = R.StatusCode;
		if (R.IsSuccess())
		{
			const TOptional<FPanoramaUploadLayoutImageResponseDto> Dto = R.GetBodyAs<FPanoramaUploadLayoutImageResponseDto>();
			if (Dto.IsSet()) { Out.SuccessBody = Dto.GetValue(); }
		}
		else if (R.StatusCode >= 400 && R.StatusCode < 600)
		{
			const TOptional<FRestApiResponse> Err = R.GetBodyAs<FRestApiResponse>();
			if (Err.IsSet()) { Out.ErrorBody = Err.GetValue(); }
			else { Out.ErrorBody.Exception.message = R.GetBodyAsString(); }
		}
		return Out;
	}

	FPanoramaRemoveShootingPointResult ParseRemoveShootingPointResult(const FValIOResponse& R)
	{
		FPanoramaRemoveShootingPointResult Out;
		Out.StatusCode = R.StatusCode;
		if (!R.IsSuccess() && R.StatusCode >= 400 && R.StatusCode < 600)
		{
			const TOptional<FRestApiResponse> Err = R.GetBodyAs<FRestApiResponse>();
			if (Err.IsSet()) { Out.ErrorBody = Err.GetValue(); }
			else { Out.ErrorBody.Exception.message = R.GetBodyAsString(); }
		}
		return Out;
	}
}

// ---------------------------------------------------------------------------
// API-методы (принимают Params)
// ---------------------------------------------------------------------------

TFuture<FPanoramaGetQueueTasksListResult> PanoramasAdmin_GetQueueTasksListByProjectId(
	FValIOClientRef Client,
	const FPanoramaGetQueueTasksListParams& Params)
{
	const FString Path = UWireframePanoramasAdminPathsLibrary::PathQueueTasksByProject(
		Params.TenantId, Params.ChessboardProjectId);
	FValIORequest Request = Client->NewRequest(EValIOHttpVerb::Get, Path)
		.AddQueryParam(TEXT("Paging.Offset"), FString::FromInt(Params.PagingOffset))
		.AddQueryParam(TEXT("Paging.Take"), FString::FromInt(Params.PagingTake))
		.SetRequestName(TEXT("GetPanoramasQueueTasksListByProjectId"))
		.Build();
	return Client->Send(MoveTemp(Request)).Next(ParseGetQueueTasksListResult);
}

TFuture<FPanoramaStartQueueTaskResult> PanoramasAdmin_StartQueueTask(
	FValIOClientRef Client,
	const FPanoramaStartQueueTaskParams& Params)
{
	const FString Path = UWireframePanoramasAdminPathsLibrary::PathStartQueueTask(Params.TenantId, Params.TaskId);
	FValIORequest Request = Client->NewRequest(EValIOHttpVerb::Post, Path)
		.SetRequestName(TEXT("StartQueueTask"))
		.Build();
	return Client->Post(Path, MoveTemp(Request)).Next(ParseStartQueueTaskResult);
}

TFuture<FPanoramaGetLayoutPanoramaPointsResult> PanoramasAdmin_GetObjectLayoutPanoramaPointsList(
	FValIOClientRef Client,
	const FPanoramaGetLayoutPanoramaPointsParams& Params)
{
	const FString Path = UWireframePanoramasAdminPathsLibrary::PathLayoutPanoramaPoints(
		Params.TenantId, Params.ChessboardPropertyLayoutId, Params.PresetId);
	return Client->Get(Path).Next(ParseGetLayoutPanoramaPointsResult);
}

TFuture<FPanoramaUploadLayoutImageResult> PanoramasAdmin_UploadLayoutPanoramaImage(
	FValIOClientRef Client,
	const FPanoramaUploadLayoutImageParams& Params)
{
	const FString Path = UWireframePanoramasAdminPathsLibrary::PathUploadLayoutPanoramaImage(
		Params.TenantId, Params.PointId, Params.PresetId);
	FValIOMultipartFormData FormData;
	TArray<uint8> BytesCopy = Params.CubemapImageBytes;
	FormData.AddFile(TEXT("CubemapImageFile"), Params.FileName, MoveTemp(BytesCopy), Params.MimeType);
	FValIORequest Request = Client->NewRequest(EValIOHttpVerb::Post, Path)
		.SetMultipartBody(FormData)
		.SetRequestName(TEXT("UploadLayoutPanoramaImage"))
		.Build();
	return Client->Post(Path, MoveTemp(Request)).Next(ParseUploadLayoutImageResult);
}

TFuture<FPanoramaRemoveShootingPointResult> PanoramasAdmin_RemovePanoramaShootingPoint(
	FValIOClientRef Client,
	const FPanoramaRemoveShootingPointParams& Params)
{
	const FString Path = UWireframePanoramasAdminPathsLibrary::PathRemovePanoramaShootingPoint(Params.TenantId, Params.PointId);
	return Client->Delete(Path).Next(ParseRemoveShootingPointResult);
}

// ---------------------------------------------------------------------------
// Blueprint async actions: NewAsyncObject + RunWithClientAndComplete in Activate
// ---------------------------------------------------------------------------

// ---------- GetQueueTasksList ----------

UPanoramasAdminGetQueueTasksListAsync* UPanoramasAdminGetQueueTasksListAsync::GetQueueTasksListByProjectId(
	UObject* WorldContextObject, const FString& ClientNameArg, const FPanoramaGetQueueTasksListParams& Params)
{
	return UValIOAsyncActionBase::NewAsyncObject<UPanoramasAdminGetQueueTasksListAsync>(WorldContextObject, ClientNameArg, Params);
}

void UPanoramasAdminGetQueueTasksListAsync::Complete(const FPanoramaGetQueueTasksListResult& Result)
{
	LastResult = Result;
	OnCompleted.Broadcast(Result);
	SetReadyToDestroy();
}

void UPanoramasAdminGetQueueTasksListAsync::Activate()
{
	RunWithClientAndComplete<UPanoramasAdminGetQueueTasksListAsync, FPanoramaGetQueueTasksListResult>(
		[this](FValIOClientRef Client) { return PanoramasAdmin_GetQueueTasksListByProjectId(Client, State.Params); });
}

// ---------- StartQueueTask ----------

UPanoramasAdminStartQueueTaskAsync* UPanoramasAdminStartQueueTaskAsync::StartQueueTask(
	UObject* WorldContextObject, const FString& ClientNameArg, const FPanoramaStartQueueTaskParams& Params)
{
	return UValIOAsyncActionBase::NewAsyncObject<UPanoramasAdminStartQueueTaskAsync>(WorldContextObject, ClientNameArg, Params);
}

void UPanoramasAdminStartQueueTaskAsync::Complete(const FPanoramaStartQueueTaskResult& Result)
{
	LastResult = Result;
	OnCompleted.Broadcast(Result);
	SetReadyToDestroy();
}

void UPanoramasAdminStartQueueTaskAsync::Activate()
{
	RunWithClientAndComplete<UPanoramasAdminStartQueueTaskAsync, FPanoramaStartQueueTaskResult>(
		[this](FValIOClientRef Client) { return PanoramasAdmin_StartQueueTask(Client, State.Params); });
}

// ---------- GetObjectLayoutPanoramaPointsList ----------

UPanoramasAdminGetLayoutPanoramaPointsAsync* UPanoramasAdminGetLayoutPanoramaPointsAsync::GetObjectLayoutPanoramaPointsList(
	UObject* WorldContextObject, const FString& ClientNameArg, const FPanoramaGetLayoutPanoramaPointsParams& Params)
{
	return UValIOAsyncActionBase::NewAsyncObject<UPanoramasAdminGetLayoutPanoramaPointsAsync>(WorldContextObject, ClientNameArg, Params);
}

void UPanoramasAdminGetLayoutPanoramaPointsAsync::Complete(const FPanoramaGetLayoutPanoramaPointsResult& Result)
{
	LastResult = Result;
	OnCompleted.Broadcast(Result);
	SetReadyToDestroy();
}

void UPanoramasAdminGetLayoutPanoramaPointsAsync::Activate()
{
	RunWithClientAndComplete<UPanoramasAdminGetLayoutPanoramaPointsAsync, FPanoramaGetLayoutPanoramaPointsResult>(
		[this](FValIOClientRef Client) { return PanoramasAdmin_GetObjectLayoutPanoramaPointsList(Client, State.Params); });
}

// ---------- UploadLayoutPanoramaImage ----------

UPanoramasAdminUploadLayoutImageAsync* UPanoramasAdminUploadLayoutImageAsync::UploadLayoutPanoramaImage(
	UObject* WorldContextObject, const FString& ClientNameArg, const FPanoramaUploadLayoutImageParams& Params)
{
	return UValIOAsyncActionBase::NewAsyncObject<UPanoramasAdminUploadLayoutImageAsync>(WorldContextObject, ClientNameArg, Params);
}

void UPanoramasAdminUploadLayoutImageAsync::Complete(const FPanoramaUploadLayoutImageResult& Result)
{
	LastResult = Result;
	OnCompleted.Broadcast(Result);
	SetReadyToDestroy();
}

void UPanoramasAdminUploadLayoutImageAsync::Activate()
{
	RunWithClientAndComplete<UPanoramasAdminUploadLayoutImageAsync, FPanoramaUploadLayoutImageResult>(
		[this](FValIOClientRef Client) { return PanoramasAdmin_UploadLayoutPanoramaImage(Client, State.Params); });
}

// ---------- RemovePanoramaShootingPoint ----------

UPanoramasAdminRemoveShootingPointAsync* UPanoramasAdminRemoveShootingPointAsync::RemovePanoramaShootingPoint(
	UObject* WorldContextObject, const FString& ClientNameArg, const FPanoramaRemoveShootingPointParams& Params)
{
	return UValIOAsyncActionBase::NewAsyncObject<UPanoramasAdminRemoveShootingPointAsync>(WorldContextObject, ClientNameArg, Params);
}

void UPanoramasAdminRemoveShootingPointAsync::Complete(const FPanoramaRemoveShootingPointResult& Result)
{
	LastResult = Result;
	OnCompleted.Broadcast(Result);
	SetReadyToDestroy();
}

void UPanoramasAdminRemoveShootingPointAsync::Activate()
{
	RunWithClientAndComplete<UPanoramasAdminRemoveShootingPointAsync, FPanoramaRemoveShootingPointResult>(
		[this](FValIOClientRef Client) { return PanoramasAdmin_RemovePanoramaShootingPoint(Client, State.Params); });
}
