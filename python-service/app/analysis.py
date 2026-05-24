import numpy as np

from app.cluster import detect_red_clusters_from_points
from app.schemas import (
    DbscanRequest,
    DbscanResponse,
    MultiIndexGroupingRequest,
    MultiIndexGroupingResponse,
)


def run_dbscan(request: DbscanRequest) -> DbscanResponse:
    points = np.array(request.points)
    ndvi_values = np.array(request.ndvi_values)

    cluster_ids, ndvi_means = detect_red_clusters_from_points(
        points,
        ndvi_values,
        eps=request.eps,
        min_samples=request.min_samples,
        ellipse_h=request.ellipse_h,
        ellipse_w=request.ellipse_w,
    )

    return DbscanResponse(
        cluster_ids=cluster_ids,
        ndvi_means={str(key): value for key, value in ndvi_means.items()},
    )


def _classify_matrix(matrix: np.ndarray, min_t: float, max_t: float) -> np.ndarray:
    result = np.ones(matrix.shape, dtype=np.int32)
    result[matrix >= max_t] = 0
    result[matrix < min_t] = 2
    return result


def _confirmed_bad_points_by_dbscan(
    field_points: np.ndarray,
    index_values: np.ndarray,
    index_cls: np.ndarray,
    eps: float,
    min_samples: int,
    ellipse_h: int,
    ellipse_w: int,
) -> set[tuple[int, int]]:
    bad_points = []
    bad_values = []

    h, w = index_values.shape
    for point in field_points:
        x = int(point[0])
        y = int(point[1])

        if x < 0 or y < 0 or x >= w or y >= h:
            continue

        if index_cls[y, x] == 2:
            bad_points.append([x, y])
            bad_values.append(float(index_values[y, x]))

    if not bad_points:
        return set()

    cluster_ids, _ = detect_red_clusters_from_points(
        np.array(bad_points, dtype=np.int32),
        np.array(bad_values, dtype=np.float64),
        eps=eps,
        min_samples=min_samples,
        ellipse_h=ellipse_h,
        ellipse_w=ellipse_w,
    )

    confirmed = set()
    for i, cid in enumerate(cluster_ids):
        if int(cid) < 0:
            confirmed.add((int(bad_points[i][0]), int(bad_points[i][1])))

    return confirmed


def run_multi_index_grouping(request: MultiIndexGroupingRequest) -> MultiIndexGroupingResponse:
    ndvi = np.array(request.ndvi, dtype=np.float64)
    gndvi = np.array(request.gndvi, dtype=np.float64)
    ndwi = np.array(request.ndwi, dtype=np.float64)
    field_points = np.array(request.field_points, dtype=np.int32)

    if ndvi.shape != gndvi.shape or ndvi.shape != ndwi.shape:
        raise ValueError("Macierze NDVI/GNDVI/NDWI musza miec te same wymiary")

    ndvi_cls = _classify_matrix(ndvi, request.thresholds.ndvi.min, request.thresholds.ndvi.max)
    gndvi_cls = _classify_matrix(gndvi, request.thresholds.gndvi.min, request.thresholds.gndvi.max)
    ndwi_cls = _classify_matrix(ndwi, request.thresholds.ndwi.min, request.thresholds.ndwi.max)

    combined = np.full(ndvi.shape, -1, dtype=np.int32)

    ndvi_bad_points = _confirmed_bad_points_by_dbscan(
        field_points,
        ndvi,
        ndvi_cls,
        request.eps,
        request.min_samples,
        request.ellipse_h,
        request.ellipse_w,
    )
    gndvi_bad_points = _confirmed_bad_points_by_dbscan(
        field_points,
        gndvi,
        gndvi_cls,
        request.eps,
        request.min_samples,
        request.ellipse_h,
        request.ellipse_w,
    )
    ndwi_bad_points = _confirmed_bad_points_by_dbscan(
        field_points,
        ndwi,
        ndwi_cls,
        request.eps,
        request.min_samples,
        request.ellipse_h,
        request.ellipse_w,
    )

    if field_points.size > 0:
        h, w = ndvi.shape
        for point in field_points:
            x = int(point[0])
            y = int(point[1])

            if x < 0 or y < 0 or x >= w or y >= h:
                continue

            ndvi_c = ndvi_cls[y, x]
            gndvi_c = gndvi_cls[y, x]
            ndwi_c = ndwi_cls[y, x]

            ndvi_bad = (x, y) in ndvi_bad_points
            gndvi_bad = (x, y) in gndvi_bad_points
            ndwi_bad = (x, y) in ndwi_bad_points

            if ndvi_bad and gndvi_bad and ndwi_bad:
                combined[y, x] = 5
            elif gndvi_bad and ndwi_bad:
                combined[y, x] = 5
            elif gndvi_bad:
                combined[y, x] = 3
            elif ndwi_bad:
                combined[y, x] = 4
            elif ndvi_bad:
                combined[y, x] = 2
            else:
                good_count = int(ndvi_c == 0) + int(gndvi_c == 0) + int(ndwi_c == 0)
                combined[y, x] = 0 if good_count >= 2 else 1

    return MultiIndexGroupingResponse(
        combined_classes=combined.tolist(),
        cluster_ids=[],
        cluster_means={},
        cluster_points=[],
    )
