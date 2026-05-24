import numpy as np
from sklearn.cluster import DBSCAN


def detect_red_clusters_from_points(
    points: np.ndarray,
    ndvi_values: np.ndarray,
    eps: float = 5,
    min_samples: int = 3,
    ellipse_h: int = 3,
    ellipse_w: int = 4,
) -> tuple[list[int], dict[int, float]]:
    if len(points) == 0:
        return [], {}

    xs = points[:, 0].astype(int)
    ys = points[:, 1].astype(int)
    coords = np.column_stack([xs, ys])

    db = DBSCAN(eps=eps, min_samples=min_samples)
    labels = db.fit_predict(coords)

    cluster_means: dict[int, float] = {}
    unique_labels = np.unique(labels)
    valid_clusters = unique_labels[unique_labels >= 0]

    for lab in valid_clusters:
        cluster_points = ndvi_values[labels == lab]
        cluster_means[int(lab)] = float(np.mean(cluster_points))

    sorted_clusters = sorted(valid_clusters, key=lambda x: cluster_means[int(x)])

    new_label_map: dict[int, int] = {}
    for new_id, old_id in enumerate(sorted_clusters, start=1):
        new_label_map[int(old_id)] = -new_id

    new_labels: list[int] = []
    for old_label in labels:
        if old_label == -1:
            new_labels.append(1)
        else:
            new_labels.append(new_label_map[int(old_label)])

    new_means: dict[int, float] = {}
    for old_id, new_id in new_label_map.items():
        new_means[new_id] = cluster_means[old_id]

    noise_points = ndvi_values[labels == -1]
    if len(noise_points) > 0:
        new_means[1] = float(np.mean(noise_points))

    return new_labels, new_means
