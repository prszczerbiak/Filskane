// ============================================================
// ZMIENNE GLOBALNE
// ============================================================
const token = localStorage.getItem("token");
if (!token) {
    handleUnauthorized();
    throw new Error("Brak tokena – zatrzymano dalsze wykonywanie skryptu");
}

const savedTheme = localStorage.getItem('theme') || 'light';
if (savedTheme === 'dark') {
    document.documentElement.classList.add('dark-theme');
}

function handleUnauthorized() {
    alert("Twoja sesja wygasła. Zaloguj się ponownie.");
    localStorage.removeItem("token"); // usuń stary token
    window.location.href = "index.html"; // przekierowanie do strony logowania
}

// Interceptor dla fetch, żeby złapać 401
const originalFetch = window.fetch;
window.fetch = async (...args) => {
    const response = await originalFetch(...args);
    if (response.status === 401) {
        handleUnauthorized(); // pokaz alert i wróć do logowania
        throw new Error("Unauthorized");
    }
    return response;
};

// Sprawdzenie od razu przy ładowaniu strony


let map = L.map('map').setView([50.800667, 19.124278], 13);
let selectMap, addFieldMap;
let marker = null, tempMarker = null, addFieldMarker = null;
let selectedCoords = null;
let drawnItems = null;
let userFieldsLayer = L.layerGroup().addTo(map);
let selectedFieldId = null, selectedLayer = null;

// ============================================================
// IKONY
// ============================================================
const redIcon = L.icon({
    iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-red.png',
    shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
    iconSize: [25, 41],
    iconAnchor: [12, 41],
    popupAnchor: [1, -34],
    shadowSize: [41, 41]
});

// ============================================================
// INICJALIZACJA MAPY GŁÓWNEJ
// ============================================================
L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19,
    attribution: '&copy; OpenStreetMap'
}).addTo(map);

// ============================================================
// PRZYCISK DODAWANIA POLA
// ============================================================
L.Control.AddField = L.Control.extend({
    onAdd: function () {
        const btn = L.DomUtil.create('button', 'add-field-btn');
        btn.innerHTML = `<span class="plus-icon">+</span> Dodaj Pole`;
        btn.style.cursor = 'pointer';

        L.DomEvent.disableClickPropagation(btn);
        L.DomEvent.disableScrollPropagation(btn);

        L.DomEvent.on(btn, 'click', function (e) {
            L.DomEvent.stopPropagation(e);
            document.getElementById('drawFieldModal').style.display = 'flex';
            initAddFieldMap();
        });

        return btn;
    }
});

L.control.addField = function (opts) {
    return new L.Control.AddField(opts);
}

L.control.addField({ position: 'topright' }).addTo(map);

// ============================================================
// FUNKCJA RYSOWANIA MARKERA FARMY
// ============================================================
function drawFarmMarker(lat, lng) {
    if (marker) map.removeLayer(marker);

    marker = L.marker([lat, lng], { icon: redIcon }).addTo(map)
        .bindPopup(`
                    <div style="text-align: center;">
                        <b>Moje gospodarstwo</b><br><br>
                        <button id="moveFarmBtn" class="btn-primary">Modyfikuj pozycję</button>
                        <button id="deleteFarmBtn" class="btn-danger" style="display: block; margin: 5px auto;">Usuń</button>
                    </div>
                `);
}

// ============================================================
// POBRANIE DANYCH FARMY
// ============================================================
fetch('/api/farm/getFarm', { headers: { "Authorization": "Bearer " + token } })
    .then(res => res.json())
    .then(data => {
        if (data.farmX == null || data.farmY == null) {
            document.getElementById("noFarmModal").style.display = 'flex';
        } else {
            drawFarmMarker(data.farmY, data.farmX);
            map.flyTo([data.farmY, data.farmX], 16);
        }
    });

// ============================================================
// POBRANIE POL UŻYTKOWNIKA
// ============================================================
fetch('/api/farm/getUserFields', { headers: { "Authorization": "Bearer " + token } })
    .then(async res => {
        const data = await res.json();
        if (!res.ok) throw new Error(data.error || "Nieznany błąd serwera");
        if (!Array.isArray(data)) return;

        userFieldsLayer.clearLayers();

        data.forEach(field => {
            if (field.geoJSON) {
                try {
                    const geojsonObj = JSON.parse(field.geoJSON);
                    const layer = L.geoJSON(geojsonObj, {
                        style: { color: '#008000', weight: 2, fillOpacity: 0.3 }
                    }).addTo(map);

                    createFieldLayer(layer, field.name, field.fieldId);
                } catch (e) {
                    console.error("Błąd parsowania GeoJSON pola:", e);
                }
            }
        });
    })
    .catch(err => console.error("Błąd pobierania pól:", err));

// ============================================================
// MAPA DODAWANIA POLA
// ============================================================
function getFeatureInfoUrl(map, layer, latlng) {
    const point = map.latLngToContainerPoint(latlng, map.getZoom());
    const size = map.getSize();

    const params = {
        request: 'GetFeatureInfo',
        service: 'WMS',
        srs: 'EPSG:4326',
        styles: '',
        transparent: true,
        version: '1.1.1',
        format: 'image/png',
        bbox: map.getBounds().toBBoxString(),
        height: size.y,
        width: size.x,
        layers: layer.wmsParams.layers,
        query_layers: layer.wmsParams.layers,
        info_format: 'text/html',
        x: Math.round(point.x),
        y: Math.round(point.y)
    };

    return layer._url + L.Util.getParamString(params, layer._url, true);
}

function initAddFieldMap() {
    if (addFieldMap) {
        addFieldMap.off();
        addFieldMap.remove();
        addFieldMap = null;
    }

    addFieldMarker = null;

    addFieldMap = L.map('addFieldMap', {
        zoomControl: true,       // włączamy kontrolki zoom
        scrollWheelZoom: true,
        doubleClickZoom: true,
        dragging: true,
        minZoom: 16,
        maxZoom: 18
    });

    if (marker) {
        addFieldMap.setView(marker.getLatLng(), 16);
        L.marker(marker.getLatLng(), { icon: redIcon })
            .bindPopup("<b>Moje gospodarstwo</b>")
            .addTo(addFieldMap);
    } else {
        addFieldMap.setView([50.800667, 19.124278], 16);
    }

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
 //       zoomControl: true,       // włączamy kontrolki zoom
        attribution: '&copy; OpenStreetMap'
    }).addTo(addFieldMap);

    const dzialki = L.tileLayer.wms(
        "https://integracja.gugik.gov.pl/cgi-bin/KrajowaIntegracjaEwidencjiGruntow",
        { layers: "dzialki", format: "image/png", transparent: true, attribution: "Geoportal KIEG" }
    ).addTo(addFieldMap);

    dzialki.on('tileerror', function (errorEvent) {
        console.error("❌ Błąd wczytywania kafelka WMS:", errorEvent);
        alert("Nie udało się pobrać warstwy WMS (działki). Sprawdź połączenie lub adres serwera.");
    });

    userFieldsLayer.eachLayer(layer => {
        L.geoJSON(layer.toGeoJSON(), {
            style: { color: '#008000', weight: 2, fillOpacity: 0.3 }
        }).addTo(addFieldMap);
    });

    L.Control.geocoder({ defaultMarkGeocode: false, placeholder: "Szukaj adresu..." })
        .on('markgeocode', function (e) {
            addFieldMap.setView(e.geocode.center, 16);
        })
        .addTo(addFieldMap);

    drawnItems = new L.FeatureGroup();
    addFieldMap.addLayer(drawnItems);

    const drawControl = new L.Control.Draw({
        draw: {
            polygon: {
                allowIntersection: false,
                showArea: true,
                shapeOptions: { color: '#ff0000' }
            },
            polyline: false, circle: false, rectangle: false, marker: false, circlemarker: false
        },
        edit: { featureGroup: drawnItems, remove: true }
    });
    addFieldMap.addControl(drawControl);

    const polygonButton = document.querySelector('.leaflet-draw-draw-polygon');

    addFieldMap.on(L.Draw.Event.CREATED, function (e) {
        const layer = e.layer;
        drawnItems.addLayer(layer);
        if (polygonButton) polygonButton.classList.add('leaflet-disabled');
    });

    addFieldMap.on(L.Draw.Event.DELETED, function () {
        if (polygonButton) polygonButton.classList.remove('leaflet-disabled');
    });

    addFieldMap.on('click', function (e) {
        const url = getFeatureInfoUrl(addFieldMap, dzialki, e.latlng);
        fetch(url).then(res => res.text());
    });
}

// ============================================================
// OBSŁUGA MODALI, DODAWANIA I USUWANIA
// ============================================================
document.getElementById('addFieldBtn').addEventListener('click', () => {
    if (drawnItems.getLayers().length === 0) return alert("Najpierw narysuj poligon!");

    document.getElementById('drawFieldModal').style.display = 'none';
    document.getElementById('nameFieldModal').style.display = 'flex';
});

document.getElementById('namedFieldBtn').addEventListener('click', () => {
    const fieldName = document.getElementById('fieldNameInput').value.trim();
    if (!fieldName) return alert("Podaj nazwę pola!");

    const layer = drawnItems.getLayers()[0];
    const geojson = layer.toGeoJSON();
    const area = turf.area(geojson);
    const centerLatLng = layer.getBounds().getCenter();

    const polygonData = {
        name: fieldName,
        geojson: JSON.stringify(geojson),
        centerX: centerLatLng.lat,
        centerY: centerLatLng.lng,
        area: area
    };

    fetch('api/farm/saveField', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', 'Authorization': "Bearer " + token },
        body: JSON.stringify(polygonData)
    })
        .then(async res => {
            if (!res.ok) {
                let errorMessage;
                try {
                    const errBody = await res.json();
                    errorMessage = errBody.error || JSON.stringify(errBody);
                } catch {
                    errorMessage = await res.text();
                }
                throw new Error(`Błąd zapisu! (${res.status}): ${errorMessage}`);
            }
            return res.json();
        })
        .then(data => {
            alert("Pole zapisane poprawnie!");
            const geojsonObj = JSON.parse(JSON.stringify(geojson));
            const newLayer = L.geoJSON(geojsonObj, { style: { color: '#008000', weight: 2, fillOpacity: 0.3 } });
            createFieldLayer(newLayer, fieldName, data.fieldId);
            document.getElementById('nameFieldModal').style.display = 'none';
        })
        .catch(err => {
            console.error(err);
            alert("Wystąpił problem przy zapisie pola.");
        });

    document.getElementById('nameFieldModal').style.display = 'none';
});

// ============================================================
// MAPA WYBORU LOKALIZACJI FARMY
// ============================================================
function initSelectFarmMap() {
    if (selectMap) {
        selectMap.off();
        selectMap.remove();
        selectMap = null;
    }

    tempMarker = null;
    selectedCoords = null;

    if (marker) {
        selectMap = L.map('selectMap').setView(marker.getLatLng(), 13);
        L.marker(marker.getLatLng(), { icon: redIcon })
            .bindPopup("<b>Moje gospodarstwo</b>")
            .addTo(selectMap);
    } else {
        selectMap = L.map('selectMap').setView([50.800667, 19.124278], 13);
    }

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap'
    }).addTo(selectMap);

    L.Control.geocoder({ defaultMarkGeocode: false, placeholder: "Szukaj adresu..." })
        .on('markgeocode', function (e) {
            selectMap.fitBounds(e.geocode.bbox);
            if (tempMarker) selectMap.removeLayer(tempMarker);
            tempMarker = L.marker(e.geocode.center).addTo(selectMap).bindPopup(e.geocode.name).openPopup();
            selectedCoords = e.geocode.center;
        })
        .addTo(selectMap);

    selectMap.on('click', function (e) {
        if (tempMarker) selectMap.removeLayer(tempMarker);
        tempMarker = L.marker(e.latlng).addTo(selectMap);
        selectedCoords = e.latlng;
    });
}

// ============================================================
// OBSŁUGA PRZYCISKÓW MODALI
// ============================================================
document.getElementById('createFarmBtn').addEventListener('click', () => {
    document.getElementById("noFarmModal").style.display = 'none';
    document.getElementById('setFarmModal').style.display = 'flex';
    initSelectFarmMap();
});

document.getElementById('confirmSetFarmBtn').addEventListener('click', () => {
    if (!selectedCoords) return alert("Najpierw wybierz miejsce na mapie!");

    fetch('/api/farm/setCoords', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', "Authorization": "Bearer " + token },
        body: JSON.stringify({ FarmX: selectedCoords.lng, FarmY: selectedCoords.lat })
    })
        .then(() => {
            if (marker) map.removeLayer(marker);
            document.getElementById("setFarmModal").style.display = 'none';
            map.flyTo([selectedCoords.lat, selectedCoords.lng], 16);
            drawFarmMarker(selectedCoords.lat, selectedCoords.lng);
        });
});

document.getElementById('cancelSetFarmBtn').addEventListener('click', () => {
    document.getElementById('setFarmModal').style.display = 'none';
});

document.getElementById('cancelAddFieldBtn').addEventListener('click', () => {
    document.getElementById('drawFieldModal').style.display = 'none';
});

document.getElementById('cancelDeleteBtn').addEventListener("click", () => {
    document.getElementById('deleteFarmModal').style.display = "none";
});

document.getElementById('cancelDeleteFieldBtn').addEventListener("click", () => {
    document.getElementById('deleteFieldModal').style.display = "none";
});

document.getElementById('cancelNamedFieldBtn').addEventListener("click", () => {
    document.getElementById('nameFieldModal').style.display = "none";
});

document.getElementById('confirmDeleteBtn').addEventListener("click", () => {
    document.getElementById('deleteFarmModal').style.display = "none";
    fetch('/api/farm/deleteFarm', { method: 'DELETE', headers: { "Authorization": "Bearer " + token } });
    window.location.href = '/dashboard.html';
});

document.getElementById('confirmDeleteFieldBtn').addEventListener("click", () => {
    if (!selectedFieldId) return;

    fetch(`/api/farm/deleteField/${selectedFieldId}`, {
        method: 'DELETE',
        headers: { "Authorization": "Bearer " + token }
    })
        .then(res => {
            if (!res.ok) throw new Error("Błąd usuwania pola");
            if (selectedLayer) map.removeLayer(selectedLayer);
            document.getElementById('deleteFieldModal').style.display = "none";
            selectedFieldId = null;
            selectedLayer = null;
            location.reload();
        })
        .catch(err => {
            console.error(err);
            alert("Nie udało się usunąć pola.");
        });
});

// ============================================================
// OBSŁUGA POPUPÓW
// ============================================================
map.on("popupopen", function (e) {
    const popup = e.popup;
    if (!popup || !popup.getContent().includes("Moje gospodarstwo")) return;

    const popupEl = popup.getElement();
    if (!popupEl) return;

    const moveBtn = popupEl.querySelector("#moveFarmBtn");
    const deleteBtn = popupEl.querySelector("#deleteFarmBtn");

    const deleteModal = document.getElementById("deleteFarmModal");
    const setFarmModal = document.getElementById("setFarmModal");

    if (moveBtn) {
        moveBtn.onclick = () => {
            setFarmModal.style.display = 'flex';
            initSelectFarmMap();
            if (tempMarker) {
                selectMap.removeLayer(tempMarker);
                tempMarker = null;
            }
            selectedCoords = null;
        };
    }

    if (deleteBtn) {
        deleteBtn.onclick = () => {
            deleteModal.style.display = 'flex';
        };
    }
});

// ============================================================
// FUNKCJA TWORZENIA WARSTWY POLA Z POPUPAMI
// ============================================================
function createFieldLayer(layer, fieldName, fieldId = null) {
    // Dodaj do głównej warstwy pól
    userFieldsLayer.addLayer(layer);

    // Bind popup
    layer.bindPopup(`
        <div style="text-align:center;">
            <b>${fieldName}</b><br><br>
            <button id="infoFieldBtn" class="btn-primary" ${fieldId ? `data-id="${fieldId}"` : ''}>Informacje</button>
            <button id="deleteFieldBtn" class="btn-danger" ${fieldId ? `data-id="${fieldId}"` : ''}>Usuń</button>
        </div>
    `);

    // Obsługa popupu
    layer.on("popupopen", () => {
        const popupEl = layer.getPopup().getElement();
        if (!popupEl) return;

        const infoBtn = popupEl.querySelector("#infoFieldBtn");
        const deleteBtn = popupEl.querySelector("#deleteFieldBtn");

        if (infoBtn) infoBtn.onclick = () => {
            if (!fieldId) {
                alert("Brak ID pola – nie można przejść do szczegółów.");
                return;
            }
            window.location.href = `/fieldDashboard.html?fieldId=${fieldId}`;
        };

        if (deleteBtn) deleteBtn.onclick = () => {
            selectedFieldId = fieldId;
            selectedLayer = layer;
            document.getElementById('deleteFieldModal').style.display = 'flex';
        };
    });
}


// ============================================================
// PRZYCISK WYJŚCIA
// ============================================================
document.getElementById('exitBtn').addEventListener('click', () =>
    window.location.href = '/dashboard.html'
);