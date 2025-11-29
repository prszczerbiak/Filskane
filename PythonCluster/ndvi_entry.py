import numpy as np
import json
import sys
import os

current_dir = os.path.dirname(os.path.abspath(__file__))
if current_dir not in sys.path:
    sys.path.insert(0, current_dir)

print(f"=== ndvi_entry.py loaded ===")

try:
    from cluster import detect_red_clusters_from_points
    CLUSTER_AVAILABLE = True
    print("✓ cluster module imported successfully")
except ImportError as e:
    print(f"✗ Error importing cluster module: {e}")
    CLUSTER_AVAILABLE = False

def ndvi_cluster(input_json: str) -> str:
    """
    Odbiera JSON z C#, wykonuje DBSCAN, zwraca listę klastrów i mediany NDVI.
    """
    if not CLUSTER_AVAILABLE:
        return json.dumps({
            "cluster_ids": [],
            "ndvi_medians": {}
        })

    try:
        data = json.loads(input_json)
        points = np.array(data["points"])  # Nx2
        ndvi_values = np.array(data["ndvi_values"])  # 🔹 NOWE: wartości NDVI
        eps = data.get("eps", 5)
        min_samples = data.get("min_samples", 3)
        ellipse_h = data.get("ellipse_h", 3)
        ellipse_w = data.get("ellipse_w", 4)

        print(f"Clustering {len(points)} points with DBSCAN(eps={eps}, min_samples={min_samples})...")
        print(f"NDVI values range: {ndvi_values.min():.3f} to {ndvi_values.max():.3f}")
        
        # 🔹 PRZEKAZUJEMY WARTOŚCI NDVI DO KLUSTERYZACJI
        cluster_ids, ndvi_medians = detect_red_clusters_from_points(
            points,
            ndvi_values,  # 🔹 NOWY ARGUMENT
            eps=eps,
            min_samples=min_samples,
            ellipse_h=ellipse_h,
            ellipse_w=ellipse_w
        )

        print(f"Clustering completed. Cluster IDs: {set(cluster_ids)}")
        print(f"NDVI medians: {ndvi_medians}")
        
        response = {
            "cluster_ids": cluster_ids,
            "ndvi_medians": ndvi_medians  # 🔹 NOWE: mediany NDVI
        }
        
        return json.dumps(response)
        
    except Exception as e:
        print(f"Error in ndvi_cluster: {e}")
        return json.dumps({
            "cluster_ids": [],
            "ndvi_medians": {}
        })