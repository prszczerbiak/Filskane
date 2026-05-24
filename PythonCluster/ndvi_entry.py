import numpy as np
import json
import sys
import os

# 1. Konfiguracja ścieżek (umożliwia importy lokalne przy wywołaniu z C#)
current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

print(f"=== ndvi_entry.py loaded ===")

# 2. Bezpieczny import logiki klastrowania
try:
    from cluster import detect_red_clusters_from_points
    CLUSTER_AVAILABLE = True
    print("✓ cluster module imported successfully")
except ImportError as e:
    print(f"✗ Error importing cluster module: {e}")
    CLUSTER_AVAILABLE = False

def ndvi_cluster(input_json: str) -> str:
    """
    Interfejs dla C#: Odbiera JSON, wykonuje DBSCAN, zwraca klastry i statystyki.
    """
    # 3. Zabezpieczenie przed brakiem modułu
    if not CLUSTER_AVAILABLE:
        return json.dumps({
            "cluster_ids": [],
            "ndvi_means": {} 
        })

    try:
        data = json.loads(input_json)
        points = np.array(data["points"])
        ndvi_values = np.array(data["ndvi_values"])

        eps = data.get("eps", 5)
        min_samples = data.get("min_samples", 3)
        ellipse_h = data.get("ellipse_h", 3)
        ellipse_w = data.get("ellipse_w", 4)

        cluster_ids, ndvi_means = detect_red_clusters_from_points(
            points,
            ndvi_values,
            eps=eps,
            min_samples=min_samples,
            ellipse_h=ellipse_h,
            ellipse_w=ellipse_w
        )

        return json.dumps({
            "cluster_ids": cluster_ids,
            "ndvi_means": ndvi_means
        })
    except Exception as e:
        print(f"Error in ndvi_cluster: {e}")
        return json.dumps({
            "cluster_ids": [],
            "ndvi_means": {}
        })


def _classify_matrix(matrix: np.ndarray, min_t: float, max_t: float) -> np.ndarray:
    # 0 - dobry, 1 - zadowalający, 2 - zagrożenie
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
    ellipse_w: int
) -> set[tuple[int, int]]:
    bad_points = []
    bad_values = []

    h, w = index_values.shape
    for p in field_points:
        x = int(p[0])
        y = int(p[1])

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
        ellipse_w=ellipse_w
    )

    confirmed = set()
    for i, cid in enumerate(cluster_ids):
        if int(cid) < 0:
            confirmed.add((int(bad_points[i][0]), int(bad_points[i][1])))

    return confirmed


def multi_index_grouping(input_json: str) -> str:
    """
    Grupowanie wielowskaźnikowe:
    - klasyfikuje NDVI/GNDVI/NDWI osobno (0 dobry, 1 zadowalający, 2 zagrożenie)
    - splata trzy macierze w jedną końcową klasę piksela.

    Kody klas końcowych:
    0 - dobry (co najmniej dwa wskaźniki dobre, brak zagrożenia)
    1 - zadowalający
    2 - zagrożenie NDVI
    3 - zagrożenie GNDVI
    4 - zagrożenie NDWI
    5 - zagrożenie NDWI + GNDVI (+ ewentualnie NDVI)
    """
    if not CLUSTER_AVAILABLE:
        return json.dumps({
            "combined_classes": [],
            "cluster_ids": [],
            "cluster_means": {},
            "cluster_points": []
        })

    try:
        data = json.loads(input_json)

        matrix_width = int(data.get("matrix_width", 0))
        matrix_height = int(data.get("matrix_height", 0))

        ndvi = np.array(data["ndvi"], dtype=np.float64)
        gndvi = np.array(data["gndvi"], dtype=np.float64)
        ndwi = np.array(data["ndwi"], dtype=np.float64)
        field_points = np.array(data.get("field_points", []), dtype=np.int32)

        if matrix_width > 0 and matrix_height > 0:
            expected_size = matrix_width * matrix_height
            if ndvi.size != expected_size or gndvi.size != expected_size or ndwi.size != expected_size:
                raise ValueError("Macierze NDVI/GNDVI/NDWI muszą mieć zgodny rozmiar z matrix_width i matrix_height")

            ndvi = ndvi.reshape((matrix_height, matrix_width))
            gndvi = gndvi.reshape((matrix_height, matrix_width))
            ndwi = ndwi.reshape((matrix_height, matrix_width))

        if ndvi.shape != gndvi.shape or ndvi.shape != ndwi.shape:
            raise ValueError("Macierze NDVI/GNDVI/NDWI muszą mieć te same wymiary")

        t = data.get("thresholds", {})
        t_ndvi = t.get("ndvi", {})
        t_gndvi = t.get("gndvi", {})
        t_ndwi = t.get("ndwi", {})

        eps = data.get("eps", 5)
        min_samples = data.get("min_samples", 3)
        ellipse_h = data.get("ellipse_h", 3)
        ellipse_w = data.get("ellipse_w", 4)

        ndvi_cls = _classify_matrix(ndvi, float(t_ndvi.get("min", 0.2)), float(t_ndvi.get("max", 0.6)))
        gndvi_cls = _classify_matrix(gndvi, float(t_gndvi.get("min", 0.2)), float(t_gndvi.get("max", 0.6)))
        ndwi_cls = _classify_matrix(ndwi, float(t_ndwi.get("min", 0.2)), float(t_ndwi.get("max", 0.6)))

        combined = np.full(ndvi.shape, -1, dtype=np.int32)

        # DBSCAN działa przed syntezą klas i osobno dla każdego indeksu.
        ndvi_bad_points = _confirmed_bad_points_by_dbscan(
            field_points,
            ndvi,
            ndvi_cls,
            eps,
            min_samples,
            ellipse_h,
            ellipse_w
        )
        gndvi_bad_points = _confirmed_bad_points_by_dbscan(
            field_points,
            gndvi,
            gndvi_cls,
            eps,
            min_samples,
            ellipse_h,
            ellipse_w
        )
        ndwi_bad_points = _confirmed_bad_points_by_dbscan(
            field_points,
            ndwi,
            ndwi_cls,
            eps,
            min_samples,
            ellipse_h,
            ellipse_w
        )

        if field_points.size > 0:
            h, w = ndvi.shape
            for p in field_points:
                x = int(p[0])
                y = int(p[1])

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

        return json.dumps({
            "combined_classes": combined.tolist(),
            "cluster_ids": [],
            "cluster_means": {},
            "cluster_points": []
        })

    except Exception as e:
        print(f"Error in multi_index_grouping: {e}")
        return json.dumps({
            "combined_classes": [],
            "cluster_ids": [],
            "cluster_means": {},
            "cluster_points": []
        })