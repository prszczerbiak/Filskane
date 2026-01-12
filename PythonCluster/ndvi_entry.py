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
        # 4. Deserializacja danych wejściowych
        data = json.loads(input_json)
        points = np.array(data["points"])
        ndvi_values = np.array(data["ndvi_values"])
        
        # Pobranie parametrów algorytmu (lub wartości domyślnych)
        eps = data.get("eps", 5)
        min_samples = data.get("min_samples", 3)
        ellipse_h = data.get("ellipse_h", 3)
        ellipse_w = data.get("ellipse_w", 4)

        print(f"Clustering {len(points)} points with DBSCAN(eps={eps}, min_samples={min_samples})...")
        
        # 5. Uruchomienie analizy (detekcja i obliczanie średnich)
        cluster_ids, ndvi_means = detect_red_clusters_from_points(
            points,
            ndvi_values,
            eps=eps,
            min_samples=min_samples,
            ellipse_h=ellipse_h,
            ellipse_w=ellipse_w
        )

        print(f"Clustering completed. Cluster IDs: {set(cluster_ids)}")
        print(f"NDVI means: {ndvi_means}")
        
        # 6. Przygotowanie odpowiedzi JSON
        # Klucz 'ndvi_means' zawiera mapę: ID klastra -> Średnie NDVI
        response = {
            "cluster_ids": cluster_ids,
            "ndvi_means": ndvi_means 
        }
        
        return json.dumps(response)
        
    except Exception as e:
        # 7. Globalna obsługa błędów (zwracamy pusty wynik zamiast wyjątku)
        print(f"Error in ndvi_cluster: {e}")
        return json.dumps({
            "cluster_ids": [],
            "ndvi_means": {}
        })