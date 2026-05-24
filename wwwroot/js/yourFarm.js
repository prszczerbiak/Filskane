/**
 * ============================================================
 * 1. INIT: ATOMOWY START (Nuclear Option)
 * ============================================================
 */
(function initThemeAndAuth() {
    // 1. MOTYW
    const savedTheme = localStorage.getItem('theme');
    document.documentElement.classList.remove('dark-theme');
    if (document.body) document.body.classList.remove('dark-theme');

    if (savedTheme === 'dark') {
        document.documentElement.classList.add('dark-theme');
    }

    // 2. AUTH GUARD
    const token = localStorage.getItem("token");
    if (!token) {
        window.location.href = "login.html";
    }
})();

// ============================================================
// 2. GŁÓWNA LOGIKA STRONY
// ============================================================
document.addEventListener("DOMContentLoaded", () => {

    // --- KONFIGURACJA ---
    const token = localStorage.getItem("token");
    if (!token) {
        alert("Twoja sesja wygasła. Zaloguj się ponownie.");
        window.location.href = "index.html";
        return;
    }

    // Interceptor Fetch
    const originalFetch = window.fetch;
    window.fetch = async (...args) => {
        try {
            const response = await originalFetch(...args);
            if (response.status === 401) {
                alert("Twoja sesja wygasła. Zaloguj się ponownie.");
                localStorage.removeItem("token");
                window.location.href = "index.html";
                throw new Error("Unauthorized");
            }
            return response;
        } catch (error) {
            throw error;
        }
    };

    // --- ZMIENNE MAPY ---
    let map = L.map('map').setView([50.800667, 19.124278], 13);
    let selectMap, addFieldMap;
    let marker = null, tempMarker = null, addFieldMarker = null;
    let vehicleLayer = L.layerGroup().addTo(map);
    let vehicleMarkersById = new Map();
    let selectedVehicleId = null;
    let trackedVehicles = [];
    let vehicleTrackingInterval = null;
    let vehiclePositionRefreshInProgress = false;
    let selectedCoords = null;
    let drawnItems = null;
    let userFieldsLayer = L.layerGroup().addTo(map);
    let selectedFieldId = null, selectedLayer = null;

    // --- ATOMOWY SYNC MOTYWU (FUNKCJA POMOCNICZA) ---
    function syncThemeNuclear() {
        const savedTheme = localStorage.getItem('theme');
        document.documentElement.classList.remove('dark-theme');
        document.body.classList.remove('dark-theme');
        if (savedTheme === 'dark') {
            document.documentElement.classList.add('dark-theme');
        }
    }

    // --- IKONY ---
    const redIcon = L.icon({
        iconUrl: 'https://raw.githubusercontent.com/pointhi/leaflet-color-markers/master/img/marker-icon-red.png',
        shadowUrl: 'https://unpkg.com/leaflet@1.9.4/dist/images/marker-shadow.png',
        iconSize: [25, 41], iconAnchor: [12, 41], popupAnchor: [1, -34], shadowSize: [41, 41]
    });

    const vehicleIcon = L.divIcon({
        className: 'vehicle-map-icon',
        html: '<div style="width:14px;height:14px;border-radius:50%;background:#1976d2;border:2px solid #ffffff;box-shadow:0 0 4px rgba(0,0,0,0.35);"></div>',
        iconSize: [18, 18],
        iconAnchor: [9, 9]
    });

    // ============================================================
    // 3. INICJALIZACJA MAPY GŁÓWNEJ
    // ============================================================
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19, attribution: '&copy; OpenStreetMap'
    }).addTo(map);

    // Przycisk "Dodaj Pole"
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
    L.control.addField = function (opts) { return new L.Control.AddField(opts); }
    L.control.addField({ position: 'topright' }).addTo(map);

    L.Control.AddVehicle = L.Control.extend({
        onAdd: function () {
            const btn = L.DomUtil.create('button', 'add-vehicle-btn');
            btn.innerHTML = `<span class="plus-icon">+</span> Dodaj pojazd`;
            btn.style.cursor = 'pointer';
            L.DomEvent.disableClickPropagation(btn);
            L.DomEvent.disableScrollPropagation(btn);
            L.DomEvent.on(btn, 'click', function (e) {
                L.DomEvent.stopPropagation(e);
                document.getElementById('addVehicleModal').style.display = 'flex';
            });
            return btn;
        }
    });
    L.control.addVehicle = function (opts) { return new L.Control.AddVehicle(opts); }
    L.control.addVehicle({ position: 'topright' }).addTo(map);

    // ============================================================
    // 4. FUNKCJE POMOCNICZE MAPY
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

    function escapeHtml(value) {
        return String(value)
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#39;');
    }

    function normalizeVehicle(vehicle) {
        const id = Number(vehicle?.id);
        const lat = Number(vehicle?.lat);
        const lng = Number(vehicle?.lng);

        if (!Number.isFinite(id) || !Number.isFinite(lat) || !Number.isFinite(lng)) {
            return null;
        }

        return {
            id,
            name: String(vehicle?.name || 'Pojazd'),
            ipAdress: vehicle?.ipAdress || vehicle?.ipAddress || '',
            tcpPort: Number(vehicle?.tcpPort),
            lat,
            lng
        };
    }

    function renderVehicles(vehicles) {
        vehicleLayer.clearLayers();
        vehicleMarkersById.clear();

        const normalizedVehicles = Array.isArray(vehicles)
            ? vehicles.map(normalizeVehicle).filter(Boolean)
            : [];

        if (normalizedVehicles.length === 0) return;

        normalizedVehicles.forEach(vehicle => {

            const vehicleMarker = L.marker([vehicle.lat, vehicle.lng], { icon: vehicleIcon }).addTo(vehicleLayer);
            vehicleMarkersById.set(vehicle.id, vehicleMarker);
            vehicleMarker.bindPopup(`
                <div style="text-align:center;">
                    <b>${escapeHtml(vehicle.name || 'Pojazd')}</b>
                    <button class="btn-danger delete-vehicle-btn" data-id="${vehicle.id}" style="display:block;margin:8px auto 0 auto;">Usuń</button>
                </div>
            `);

            vehicleMarker.on('popupopen', (e) => {
                const popupEl = e.popup?.getElement();
                if (!popupEl) return;

                const deleteBtn = popupEl.querySelector('.delete-vehicle-btn');
                if (deleteBtn) {
                    deleteBtn.onclick = (ev) => {
                        ev.preventDefault();
                        selectedVehicleId = Number(deleteBtn.getAttribute('data-id'));
                        document.getElementById('deleteVehicleModal').style.display = 'flex';
                    };
                }
            });
        });
    }

    async function startVehicleTracking(vehicle) {
        if (!vehicle?.id || !vehicle?.ipAdress || !Number.isFinite(Number(vehicle?.tcpPort))) return;

        const res = await fetch(`/api/iot/vehicle/start/${vehicle.id}`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${token}`
            },
            body: JSON.stringify({
                host: vehicle.ipAdress,
                port: vehicle.tcpPort,
                initialLat: vehicle.lat,
                initialLng: vehicle.lng,
                type: vehicle.name
            })
        });

        if (!res.ok) {
            const data = await res.json().catch(() => ({}));
            throw new Error(data.message || `Nie udało się uruchomić śledzenia pojazdu ${vehicle.name}`);
        }
    }

    async function refreshVehiclePositions() {
        if (vehiclePositionRefreshInProgress || trackedVehicles.length === 0) return;

        vehiclePositionRefreshInProgress = true;
        try {
            const results = await Promise.allSettled(trackedVehicles.map(async vehicle => {
                const res = await fetch(`/api/iot/vehicle/latest/${vehicle.id}`, {
                    headers: { 'Authorization': `Bearer ${token}` }
                });

                if (res.status === 204 || !res.ok) return;

                const data = await res.json();
                const lat = Number(data?.lat);
                const lng = Number(data?.lng);
                const marker = vehicleMarkersById.get(vehicle.id);

                if (marker && Number.isFinite(lat) && Number.isFinite(lng)) {
                    marker.setLatLng([lat, lng]);
                }
            }));

            results.forEach(result => {
                if (result.status === 'rejected') {
                    console.warn('Błąd odczytu pozycji pojazdu:', result.reason);
                }
            });
        } finally {
            vehiclePositionRefreshInProgress = false;
        }
    }

    async function initializeVehicleTracking(vehicles) {
        await stopVehicleTracking();

        trackedVehicles = Array.isArray(vehicles)
            ? vehicles.map(normalizeVehicle).filter(v => v && v.ipAdress && Number.isFinite(v.tcpPort))
            : [];

        if (trackedVehicles.length === 0) return;

        await Promise.allSettled(trackedVehicles.map(startVehicleTracking));
        await refreshVehiclePositions();

        vehicleTrackingInterval = setInterval(refreshVehiclePositions, 1500);
    }

    async function stopVehicleTracking() {
        if (vehicleTrackingInterval) {
            clearInterval(vehicleTrackingInterval);
            vehicleTrackingInterval = null;
        }

        const vehiclesToStop = trackedVehicles;
        trackedVehicles = [];

        if (vehiclesToStop.length === 0) return;

        await Promise.allSettled(vehiclesToStop.map(vehicle => fetch(`/api/iot/vehicle/stop/${vehicle.id}`, {
            method: 'POST',
            headers: { 'Authorization': `Bearer ${token}` }
        })));
    }

    function createFieldLayer(layer, fieldName, fieldId = null) {
        userFieldsLayer.addLayer(layer);

        // Dodajemy formatowanie powierzchni (opcjonalnie, jeśli chcesz spójność)
        // const areaText = formatArea(turf.area(layer.toGeoJSON()));

        layer.bindPopup(`
            <div style="text-align:center;">
                <b>${fieldName}</b><br><br>
                <button id="infoFieldBtn" class="btn-primary" ${fieldId ? `data-id="${fieldId}"` : ''}>Informacje</button>
                <button id="deleteFieldBtn" class="btn-danger" ${fieldId ? `data-id="${fieldId}"` : ''}>Usuń</button>
            </div>
        `);

        layer.on("popupopen", () => {
            const popupEl = layer.getPopup().getElement();
            if (!popupEl) return;
            const infoBtn = popupEl.querySelector("#infoFieldBtn");
            const deleteBtn = popupEl.querySelector("#deleteFieldBtn");

            if (infoBtn) infoBtn.onclick = () => {
                if (!fieldId) return alert("Brak ID pola.");
                window.location.href = `/fieldDashboard.html?fieldId=${fieldId}`;
            };
            if (deleteBtn) deleteBtn.onclick = () => {
                selectedFieldId = fieldId;
                selectedLayer = layer;
                document.getElementById('deleteFieldModal').style.display = 'flex';
            };
        });
    }

    function getFeatureInfoUrl(map, layer, latlng) {
        const point = map.latLngToContainerPoint(latlng, map.getZoom());
        const size = map.getSize();
        const params = {
            request: 'GetFeatureInfo', service: 'WMS', srs: 'EPSG:4326', styles: '', transparent: true, version: '1.1.1', format: 'image/png',
            bbox: map.getBounds().toBBoxString(), height: size.y, width: size.x, layers: layer.wmsParams.layers, query_layers: layer.wmsParams.layers, info_format: 'text/html',
            x: Math.round(point.x), y: Math.round(point.y)
        };
        return layer._url + L.Util.getParamString(params, layer._url, true);
    }

    // ============================================================
    // 5. POBIERANIE DANYCH (API)
    // ============================================================
    // Farma
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

    fetch('/api/farm/getUserVehicles', { headers: { "Authorization": "Bearer " + token } })
        .then(async res => {
            const data = await res.json();
            if (!res.ok) throw new Error(data.error || "Błąd serwera");
            renderVehicles(Array.isArray(data) ? data : []);
            await initializeVehicleTracking(Array.isArray(data) ? data : []);
        })
        .catch(err => console.error("Błąd pojazdów:", err));

    // Pola
    fetch('/api/farm/getUserFields', { headers: { "Authorization": "Bearer " + token } })
        .then(async res => {
            const data = await res.json();
            if (!res.ok) throw new Error(data.error || "Błąd serwera");
            if (!Array.isArray(data)) return;
            userFieldsLayer.clearLayers();
            data.forEach(field => {
                if (field.geojson) {
                    try {
                        const geojsonObj = JSON.parse(field.geojson);
                        const layer = L.geoJSON(geojsonObj, { style: { color: '#008000', weight: 2, fillOpacity: 0.3 } }).addTo(map);
                        createFieldLayer(layer, field.name, field.id);
                    } catch (e) { console.error("Błąd GeoJSON:", e); }
                }
            });
        })
        .catch(err => console.error("Błąd pól:", err));

    // ============================================================
    // 6. INICJALIZACJA MAP POMOCNICZYCH
    // ============================================================
    function initAddFieldMap() {
        if (addFieldMap) { addFieldMap.off(); addFieldMap.remove(); addFieldMap = null; }
        addFieldMarker = null;
        addFieldMap = L.map('addFieldMap', { zoomControl: true, scrollWheelZoom: true, doubleClickZoom: true, dragging: true, minZoom: 16, maxZoom: 18 });

        if (marker) {
            addFieldMap.setView(marker.getLatLng(), 16);
            L.marker(marker.getLatLng(), { icon: redIcon }).bindPopup("<b>Moje gospodarstwo</b>").addTo(addFieldMap);
        } else {
            addFieldMap.setView([50.800667, 19.124278], 16);
        }

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { attribution: '&copy; OSM' }).addTo(addFieldMap);
        const dzialki = L.tileLayer.wms("https://integracja.gugik.gov.pl/cgi-bin/KrajowaIntegracjaEwidencjiGruntow", { layers: "dzialki", format: "image/png", transparent: true, attribution: "Geoportal KIEG" }).addTo(addFieldMap);

        dzialki.on('tileerror', () => console.error("Błąd WMS"));

        userFieldsLayer.eachLayer(layer => {
            L.geoJSON(layer.toGeoJSON(), { style: { color: '#008000', weight: 2, fillOpacity: 0.3 } }).addTo(addFieldMap);
        });

        L.Control.geocoder({ defaultMarkGeocode: false, placeholder: "Szukaj..." }).on('markgeocode', function (e) { addFieldMap.setView(e.geocode.center, 16); }).addTo(addFieldMap);

        drawnItems = new L.FeatureGroup();
        addFieldMap.addLayer(drawnItems);
        const drawControl = new L.Control.Draw({
            draw: { polygon: { allowIntersection: false, showArea: true, shapeOptions: { color: '#ff0000' } }, polyline: false, circle: false, rectangle: false, marker: false, circlemarker: false },
            edit: { featureGroup: drawnItems, remove: true }
        });
        addFieldMap.addControl(drawControl);

        const polygonButton = document.querySelector('.leaflet-draw-draw-polygon');
        addFieldMap.on(L.Draw.Event.CREATED, (e) => { drawnItems.addLayer(e.layer); if (polygonButton) polygonButton.classList.add('leaflet-disabled'); });
        addFieldMap.on(L.Draw.Event.DELETED, () => { if (polygonButton) polygonButton.classList.remove('leaflet-disabled'); });
        addFieldMap.on('click', (e) => { fetch(getFeatureInfoUrl(addFieldMap, dzialki, e.latlng)).then(res => res.text()); });
    }

    function initSelectFarmMap() {
        if (selectMap) { selectMap.off(); selectMap.remove(); selectMap = null; }
        tempMarker = null; selectedCoords = null;

        if (marker) {
            selectMap = L.map('selectMap').setView(marker.getLatLng(), 13);
            L.marker(marker.getLatLng(), { icon: redIcon }).bindPopup("<b>Moje gospodarstwo</b>").addTo(selectMap);
        } else {
            selectMap = L.map('selectMap').setView([50.800667, 19.124278], 13);
        }

        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '&copy; OSM' }).addTo(selectMap);
        L.Control.geocoder({ defaultMarkGeocode: false, placeholder: "Szukaj..." })
            .on('markgeocode', function (e) {
                selectMap.fitBounds(e.geocode.bbox);
                if (tempMarker) selectMap.removeLayer(tempMarker);
                tempMarker = L.marker(e.geocode.center).addTo(selectMap).bindPopup(e.geocode.name).openPopup();
                selectedCoords = e.geocode.center;
            }).addTo(selectMap);

        selectMap.on('click', function (e) {
            if (tempMarker) selectMap.removeLayer(tempMarker);
            tempMarker = L.marker(e.latlng).addTo(selectMap);
            selectedCoords = e.latlng;
        });
    }

    // ============================================================
    // 7. OBSŁUGA INTERFEJSU (UI & MODALE)
    // ============================================================

    // Dodawanie pola
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

        fetch('api/farm/saveField', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': "Bearer " + token },
            body: JSON.stringify({ name: fieldName, geojson: JSON.stringify(geojson), centerX: centerLatLng.lat, centerY: centerLatLng.lng, area: area })
        })
            .then(async res => {
                if (!res.ok) throw new Error("Błąd zapisu!");
                return res.json();
            })
            .then(data => {
                alert("Pole zapisane!");
                const newLayer = L.geoJSON(geojson, { style: { color: '#008000', weight: 2, fillOpacity: 0.3 } });
                createFieldLayer(newLayer, fieldName, data.fieldId);
                document.getElementById('nameFieldModal').style.display = 'none';
            })
            .catch(err => alert("Problem przy zapisie: " + err));
    });

    document.getElementById('addVehicleBtn').addEventListener('click', () => {
        const vehicleName = document.getElementById('vehicleNameInput').value.trim();
        const ipAdress = document.getElementById('vehicleIpInput').value.trim();
        const tcpPort = Number(document.getElementById('vehiclePortInput').value);

        if (!vehicleName) return alert("Podaj nazwę pojazdu!");
        if (!ipAdress) return alert("Podaj adres IP pojazdu!");
        if (!Number.isInteger(tcpPort) || tcpPort < 1 || tcpPort > 65535) {
            return alert("Podaj poprawny port TCP z zakresu 1-65535!");
        }

        fetch('api/farm/saveVehicle', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'Authorization': "Bearer " + token },
            body: JSON.stringify({ vehicleName, ipAdress, tcpPort })
        })
            .then(async res => {
                const data = await res.json().catch(() => ({}));
                if (!res.ok) throw new Error(data.error || "Błąd zapisu pojazdu!");
                alert("Pojazd zapisany!");
                window.location.reload();
            })
            .catch(err => alert("Problem przy zapisie: " + err.message));
    });

    // Obsługa farmy
    document.getElementById('createFarmBtn').addEventListener('click', () => {
        document.getElementById("noFarmModal").style.display = 'none';
        document.getElementById('setFarmModal').style.display = 'flex';
        initSelectFarmMap();
    });

    document.getElementById('confirmSetFarmBtn').addEventListener('click', () => {
        if (!selectedCoords) return alert("Wybierz miejsce!");
        fetch('/api/farm/setCoords', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', "Authorization": "Bearer " + token },
            body: JSON.stringify({ FarmX: selectedCoords.lng, FarmY: selectedCoords.lat })
        }).then(() => {
            if (marker) map.removeLayer(marker);
            document.getElementById("setFarmModal").style.display = 'none';
            map.flyTo([selectedCoords.lat, selectedCoords.lng], 16);
            drawFarmMarker(selectedCoords.lat, selectedCoords.lng);
        });
    });

    // Obsługa Popupów Mapy Głównej
    map.on("popupopen", function (e) {
        const popup = e.popup;
        if (!popup) return;
        const popupEl = popup.getElement();
        if (!popupEl) return;

        // Jeśli to farma
        if (popup.getContent().includes("Moje gospodarstwo")) {
            const moveBtn = popupEl.querySelector("#moveFarmBtn");
            const deleteBtn = popupEl.querySelector("#deleteFarmBtn");
            if (moveBtn) moveBtn.onclick = () => {
                document.getElementById("setFarmModal").style.display = 'flex';
                initSelectFarmMap();
                selectedCoords = null;
            };
            if (deleteBtn) deleteBtn.onclick = () => document.getElementById("deleteFarmModal").style.display = 'flex';
        }
    });

    // Usuwanie
    document.getElementById('confirmDeleteBtn').addEventListener("click", () => {
        document.getElementById('deleteFarmModal').style.display = "none";
        fetch('/api/farm/deleteFarm', { method: 'DELETE', headers: { "Authorization": "Bearer " + token } });
        window.location.href = '/dashboard.html';
    });

    document.getElementById('confirmDeleteVehicleBtn').addEventListener("click", (ev) => {
        ev.preventDefault();
        if (!selectedVehicleId) {
            alert("Najpierw wybierz pojazd do usunięcia.");
            return;
        }

        fetch(`/api/farm/deleteVehicle/${selectedVehicleId}`, {
            method: 'DELETE',
            headers: { "Authorization": "Bearer " + token }
        })
            .then(async res => {
                const data = await res.json().catch(() => ({}));
                if (!res.ok) throw new Error(data.error || "Błąd usuwania pojazdu");

                document.getElementById('deleteVehicleModal').style.display = "none";
                selectedVehicleId = null;
                window.location.reload();
            })
            .catch(err => alert(err.message));
    });

    document.getElementById('confirmDeleteFieldBtn').addEventListener("click", () => {
        if (!selectedFieldId) return;
        fetch(`/api/farm/deleteField/${selectedFieldId}`, { method: 'DELETE', headers: { "Authorization": "Bearer " + token } })
            .then(res => {
                if (!res.ok) throw new Error("Błąd usuwania");
                if (selectedLayer) map.removeLayer(selectedLayer);
                document.getElementById('deleteFieldModal').style.display = "none";
                selectedFieldId = null; selectedLayer = null;
                location.reload();
            })
            .catch(err => alert("Nie udało się usunąć pola."));
    });

    // Zamykanie modali i wyjście
    document.getElementById('cancelSetFarmBtn').addEventListener('click', () => document.getElementById('setFarmModal').style.display = 'none');
    document.getElementById('cancelAddFieldBtn').addEventListener('click', () => document.getElementById('drawFieldModal').style.display = 'none');
    document.getElementById('cancelAddVehicleBtn').addEventListener('click', () => document.getElementById('addVehicleModal').style.display = 'none');
    document.getElementById('cancelDeleteBtn').addEventListener("click", () => document.getElementById('deleteFarmModal').style.display = "none");
    document.getElementById('cancelDeleteFieldBtn').addEventListener("click", () => document.getElementById('deleteFieldModal').style.display = "none");
    document.getElementById('cancelDeleteVehicleBtn').addEventListener("click", () => {
        selectedVehicleId = null;
        document.getElementById('deleteVehicleModal').style.display = "none";
    });
    document.getElementById('cancelNamedFieldBtn').addEventListener("click", () => document.getElementById('nameFieldModal').style.display = "none");
    document.getElementById('exitBtn').addEventListener('click', () => window.location.href = '/dashboard.html');

    window.addEventListener('beforeunload', () => {
        void stopVehicleTracking();
    });

    // ============================================================
    // 8. OBSŁUGA POWROTU I ZMIANY MOTYWU (ATOMOWA)
    // ============================================================
    window.addEventListener('pageshow', () => {
        syncThemeNuclear(); // Upewnij się, że po cofnięciu motyw jest OK
    });

    window.addEventListener('storage', (e) => {
        if (e.key === 'theme') {
            syncThemeNuclear();
        }
    });

    // Start
    syncThemeNuclear();
});