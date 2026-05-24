# Filskane

**System wspomagania decyzji i analizy danych przestrzennych dla rolnictwa precyzyjnego.**

Filskane to aplikacja webowa służąca do cyfrowego odwzorowania gospodarstwa rolnego oraz zaawansowanej analizy danych zbieranych z pól. System integruje wydajność platformy .NET 8 z zaawansowaną analityką języka Python.

## 🚀 Główne funkcjonalności

* **Wirtualne Gospodarstwo:** Cyfrowe odwzorowanie granic pól i struktury upraw z wykorzystaniem danych geoprzestrzennych.
* **Analiza Danych (AI):** Filtracja błędów pomiarowych i wykrywanie anomalii przy użyciu algorytmu klasteryzacji **DBSCAN**.
* **Zaawansowane Mapowanie:** Przetwarzanie danych rastrowych i wektorowych (GDAL)..
* **Wizualizacja Danych:** Generowanie heatmap NDVI (ScottPlot).
* **Bezpieczeństwo:** Autoryzacja i uwierzytelnianie użytkowników oparte o standard **JWT (JSON Web Tokens)**.

## 🛠️ Technologie

Projekt wykorzystuje nowoczesny, hybrydowy stos technologiczny:

### Backend (.NET 8.0)
Rdzeń systemu oparty na **ASP.NET Core Web API**.
* **MaxRev.Gdal.Core** – obsługa zaawansowanych formatów danych geoprzestrzennych (GIS).
* **SixLabors.ImageSharp** – dynamiczne generowanie i przetwarzanie grafik rastrowych.
* **Oracle.ManagedDataAccess.Core** – komunikacja z relacyjną bazą danych **Oracle**.
* **HTTP Client** – komunikacja z osobnym mikroserwisem analitycznym Python/FastAPI.
* **ScottPlot** – biblioteka do generowania wykresów naukowych i statystycznych.
* **JWT Bearer** – zabezpieczenie API tokenami dostępu.
* **Swagger** – dokumentacja i testowanie API.

### Mikroserwis Analityczny (Python 3.11 + FastAPI)
Odpowiada za obliczenia numeryczne i algorytmy uczenia maszynowego.
* **NumPy** – wydajne obliczenia macierzowe.
* **Scikit-learn** – implementacja algorytmów ML (m.in. DBSCAN).
* **FastAPI** – REST API dla funkcji analitycznych wywoływanych z backendu .NET.

### Frontend
Warstwa prezentacji danych mapowych w przeglądarce.
* **HTML5 / CSS3 / JavaScript**
* **Leaflet** – interaktywne mapy.
* **OpenStreetMap** – podkład mapowy.
* **Turf.js** – zaawansowana analiza geoprzestrzenna po stronie klienta (GeoJSON).
* **Geoportal API (GUGiK)** – integracja z usługami WMS/WMTS (działki, dane glebowe).

## ⚙️ Wymagania i Instalacja

### Wymagania wstępne
* **.NET SDK 8.0**
* **Docker** (do uruchomienia mikroserwisu Python)
* **Baza danych Oracle** (lub dostęp do instancji zdalnej)

### Uruchomienie lokalne

1.  **Klonowanie repozytorium:**
    ```bash
    git clone [https://github.com/twoj-nick/filskane.git](https://github.com/twoj-nick/filskane.git)
    ```

2.  **Uruchomienie mikroserwisu Python:**
    ```bash
    docker compose up --build python-service
    ```
    - Symulator maszyny (TCP):
      ```bash
      docker compose up --build machine-simulator
      ```
      Symulator nasłuchuje na porcie `8001` i wysyła kolejne pozycje jako linie JSON przez TCP.

3.  **Konfiguracja Backend:**
    W pliku `appsettings.json` uzupełnij `ConnectionString` do bazy Oracle.
    Następnie przywróć pakiety i uruchom aplikację:
    ```bash
    dotnet restore
    dotnet run
    ```

4.  **Dostęp:**
    Dokumentacja API (Swagger) dostępna pod adresem: `https://localhost:7197/swagger`