import numpy as np
from sklearn.cluster import DBSCAN

def detect_red_clusters_from_points(points, ndvi_values, eps=5, min_samples=3, ellipse_h=3, ellipse_w=4):
    # 1. Walidacja danych wejściowych
    if len(points) == 0:
        return [], {}

    # 2. Przygotowanie współrzędnych (rzutowanie na int dla pikseli)
    xs = points[:, 0].astype(int)
    ys = points[:, 1].astype(int)
    coords = np.column_stack([xs, ys])

    # 3. Klastrowanie DBSCAN
    db = DBSCAN(eps=eps, min_samples=min_samples)
    labels = db.fit_predict(coords)

    # 4. Obliczenie średniego NDVI dla każdego klastra (bez szumu)
    cluster_means = {}
    unique_labels = np.unique(labels)
    valid_clusters = unique_labels[unique_labels >= 0]
    
    for lab in valid_clusters:
        cluster_points = ndvi_values[labels == lab]
        cluster_means[lab] = float(np.mean(cluster_points))

    # 5. Sortowanie klastrów wg średniej (najniższe/najgorsze NDVI pierwsze)
    sorted_clusters = sorted(valid_clusters, key=lambda x: cluster_means[x])
    
    # 6. Mapowanie ID:
    #    - Klastry otrzymują ujemne ID (-1, -2, -3...) wg ważności
    #    - Szum otrzymuje ID = 1
    new_label_map = {}
    for new_id, old_id in enumerate(sorted_clusters, start=1):
        new_label_map[old_id] = -new_id 

    # 7. Przypisanie nowych etykiet do punktów
    new_labels = []
    for old_label in labels:
        if old_label == -1:
            new_labels.append(1)  # Szum
        else:
            new_labels.append(new_label_map[old_label])

    # 8. Utworzenie słownika średnich dla nowych ID
    new_means = {}
    for old_id, new_id in new_label_map.items():
        new_means[new_id] = cluster_means[old_id]
    
    # Obliczenie i dodanie średniej dla szumu (ID=1)
    noise_points = ndvi_values[labels == -1]
    if len(noise_points) > 0:
        new_means[1] = float(np.mean(noise_points))

    return new_labels, new_means