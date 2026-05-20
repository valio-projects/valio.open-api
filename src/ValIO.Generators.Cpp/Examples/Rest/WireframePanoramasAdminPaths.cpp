// Copyright Epic Games, Inc. All Rights Reserved.

#include "WireframePanoramasAdminPaths.h"

// ---------------------------------------------------------------------------
// Path builders (Swagger: Talent.Chessboard.Panoramas.Admin.Api)
// ---------------------------------------------------------------------------

FString UWireframePanoramasAdminPathsLibrary::PathQueueTasksByProject(const FString& TenantId, int64 ChessboardProjectId)
{
	return FString::Printf(TEXT("/api/%s/chessboard/panoramas/queue-tasks/projects/%lld"),
		*TenantId, ChessboardProjectId);
}

FString UWireframePanoramasAdminPathsLibrary::PathStartQueueTask(const FString& TenantId, const FString& TaskId)
{
	return FString::Printf(TEXT("/api/%s/chessboard/panoramas/queue-tasks/%s/start"),
		*TenantId, *TaskId);
}

FString UWireframePanoramasAdminPathsLibrary::PathLayoutPanoramaPoints(const FString& TenantId, int64 ChessboardPropertyLayoutId, const FString& PresetId)
{
	return FString::Printf(TEXT("/api/%s/chessboard/panoramas/panorama-points/layouts/%lld/presets/%s/points"),
		*TenantId, ChessboardPropertyLayoutId, *PresetId);
}

FString UWireframePanoramasAdminPathsLibrary::PathUploadLayoutPanoramaImage(const FString& TenantId, const FString& PointId, const FString& PresetId)
{
	return FString::Printf(TEXT("/api/%s/chessboard/panoramas/panorama-points/points/%s/presets/%s/upload-layout-image"),
		*TenantId, *PointId, *PresetId);
}

FString UWireframePanoramasAdminPathsLibrary::PathRemovePanoramaShootingPoint(const FString& TenantId, const FString& PointId)
{
	return FString::Printf(TEXT("/api/%s/chessboard/panoramas/panorama-points/%s"),
		*TenantId, *PointId);
}
