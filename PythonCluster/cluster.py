import numpy as np
from sklearn.cluster import DBSCAN

def detect_red_clusters_from_points(points, ndvi_values, eps=5, min_samples=3, ellipse_h=3, ellipse_w=4):
    if len(points) == 0:
        return [], {}

    xs = points[:, 0].astype(int)
    ys = points[:, 1].astype(int)
    coords = np.column_stack([xs, ys])

    db = DBSCAN(eps=eps, min_samples=min_samples)
    labels = db.fit_predict(coords)

    # Oblicz mediany NDVI dla klastrów (bez szumu)
    cluster_medians = {}
    unique_labels = np.unique(labels)
    valid_clusters = unique_labels[unique_labels >= 0]
    
    for lab in valid_clusters:
        cluster_points = ndvi_values[labels == lab]
        cluster_medians[lab] = float(np.median(cluster_points))

    # 🔹 SORTUJ KLASTRY WG MEDIANY NDVI (rosnąco - niższa mediana = większe zagrożenie)
    sorted_clusters = sorted(valid_clusters, key=lambda x: cluster_medians[x])
    
    # 🔹 NOWE ID: szum = 1, klastry = -1, -2, -3...
    new_label_map = {}
    for new_id, old_id in enumerate(sorted_clusters, start=1):
        new_label_map[old_id] = -new_id  # -1, -2, -3...

    # 🔹 PRZYPISZ NOWE ID (szum zostaje 1)
    new_labels = []
    for old_label in labels:
        if old_label == -1:
            new_labels.append(1)  # 🔹 SZUM = 1
        else:
            new_labels.append(new_label_map[old_label])

    # 🔹 ZAPISZ MEDIANY Z NOWYMI ID
    new_medians = {}
    for old_id, new_id in new_label_map.items():
        new_medians[new_id] = cluster_medians[old_id]
    
    # Dodaj medianę dla szumu
    noise_points = ndvi_values[labels == -1]
    if len(noise_points) > 0:
        new_medians[1] = float(np.median(noise_points))  # 🔹 SZUM = 1

    return new_labels, new_medians