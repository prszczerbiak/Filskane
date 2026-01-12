/**
 * ============================================================
 * 1. INIT: ATOMOWY START (Nuclear Option)
 * ============================================================
 */
(function initThemeAndAuth() {
    const savedTheme = localStorage.getItem('theme');

    document.documentElement.classList.remove('dark-theme');
    if (document.body) document.body.classList.remove('dark-theme');

    if (savedTheme === 'dark') {
        document.documentElement.classList.add('dark-theme');
    }

    const token = localStorage.getItem("token");
    if (!token) {
        window.location.href = "login.html";
    }
})();

document.addEventListener("DOMContentLoaded", () => {
    // ============================================================
    // 2. KONFIGURACJA I STAN
    // ============================================================
    const CONFIG = {
        API_BASE: '/api/field',
        FIELD_ID: new URLSearchParams(window.location.search).get("fieldId")
    };

    if (!CONFIG.FIELD_ID) {
        alert("Nieprawidłowy adres. Powrót do listy pól.");
        window.location.href = "yourFields.html";
        return;
    }

    const STATE = {
        fieldData: null,
        ndviCache: null,
        selectedZip: null,
        scanBboxCache: null
    };

    const UI = {
        title: document.getElementById('pageTitle'),
        scanImage: document.getElementById('scanImage'),
        scanDate: document.getElementById('scanDate'),
        noScanMsg: document.getElementById('noScanMsg'),
        scanLoader: document.getElementById('scanLoader'),
        fieldName: document.getElementById('fieldName'),
        area: document.getElementById('area'),
        crop: document.getElementById('crop'),
        state: document.getElementById('state'),
        date: document.getElementById('date'),
        soilComplex: document.getElementById('complex'),
        soilType: document.getElementById('type'),
        soilSubstrate: document.getElementById('substrate'),
        minBbox: document.getElementById('bbox'),
        modals: document.querySelectorAll('.modal'),

        // Elementy NDVI
        modalNdvi: document.getElementById('ndviModal'),
        imgNdvi: document.getElementById('ndviImage'),
        imgColorbar: document.getElementById('colorbar'), // <--- TU JEST NASZ OBRAZEK
        imgLegend: document.getElementById('legendImage'),

        // Pozostałe modale
        modalCrop: document.getElementById('cropModal'),
        inputCrop: document.getElementById('cropInput'),
        formCrop: document.getElementById('cropForm'),
        modalDate: document.getElementById('dateModal'),
        inputDate: document.getElementById('dateInput'),
        formDate: document.getElementById('dateForm'),
        modalImportDate: document.getElementById('scanDateModal'),
        inputImportDate: document.getElementById('importDateInput'),
        formImportDate: document.getElementById('importDateForm'),
        modalHistory: document.getElementById('historyModal'),
        historyTbody: document.querySelector('#scanHistoryTable tbody'),
        modalRgb: document.getElementById('rgbModal'),
        imgRgb: document.getElementById('rgbImage'),

        // Przyciski
        btnImport: document.getElementById('importBtn'),
        inputTiff: document.getElementById('tiffInput'),
        btnNdvi: document.getElementById('ndviBtn'),
        btnHistory: document.getElementById('historyBtn'),
        btnClustering: document.getElementById('clusteringBtn'),
        btnEditCrop: document.querySelector('.edit-btn[data-target="cropModal"]'),
        btnEditDate: document.querySelector('.edit-btn[data-target="dateModal"]')
    };

    // ============================================================
    // 3. SYNCHRONIZACJA WIZUALNA (MOTYW + JEDNOSTKI + OBRAZKI)
    // ============================================================

    function syncThemeNuclear() {
        const savedTheme = localStorage.getItem('theme');

        // 1. Klasy CSS (Clean start)
        document.documentElement.classList.remove('dark-theme');
        document.body.classList.remove('dark-theme');

        if (savedTheme === 'dark') {
            document.documentElement.classList.add('dark-theme');
        }

        // 2. Podmiana obrazka Colorbar
        if (UI.imgColorbar) {
            if (savedTheme === 'dark') {
                UI.imgColorbar.src = 'css/colorbarDark.png';
            } else {
                UI.imgColorbar.src = 'css/colorbar.png';
            }
        }

        // 3. Odświeżenie jednostek
        if (STATE.fieldData && STATE.fieldData.area) {
            UI.area.textContent = formatArea(STATE.fieldData.area);
        }
    }

    function formatArea(areaM2) {
        if (!areaM2) return "0.0000 ha";
        const unit = localStorage.getItem('unitType') || 'ha';

        switch (unit) {
            case 'a': return (areaM2 / 100).toFixed(2) + " a";
            case 'ac': return (areaM2 / 4046.86).toFixed(3) + " ac";
            default: return (areaM2 / 10000).toFixed(4) + " ha";
        }
    }

    // ============================================================
    // 4. API HELPER
    // ============================================================
    async function apiCall(endpoint, method = 'GET', body = null, isFormData = false) {
        const token = localStorage.getItem("token");
        if (!token) {
            window.location.href = "login.html";
            return;
        }

        const headers = { "Authorization": `Bearer ${token}` };
        if (!isFormData) headers["Content-Type"] = "application/json";

        const options = { method, headers };
        if (body) options.body = isFormData ? body : JSON.stringify(body);

        try {
            const res = await fetch(`${CONFIG.API_BASE}${endpoint}`, options);
            if (res.status === 401) {
                alert("Sesja wygasła.");
                localStorage.removeItem("token");
                window.location.href = "login.html";
                return null;
            }
            return res;
        } catch (err) {
            console.error(`API Error (${endpoint}):`, err);
            throw err;
        }
    }

    // ============================================================
    // 5. LOGIKA DANYCH
    // ============================================================
    async function initDashboard() {
        syncThemeNuclear(); // Uruchom na starcie, żeby ustawić obrazek

        try {
            const res = await apiCall(`/getData/${CONFIG.FIELD_ID}`);
            if (!res.ok) throw new Error("Błąd pobierania danych pola");

            STATE.fieldData = await res.json();
            console.log("DANE Z BACKENDU:", STATE.fieldData);
            renderFieldInfo();
            loadLatestScan();

        } catch (err) {
            console.error(err);
            UI.title.textContent = "Błąd: Nie znaleziono pola";
        }
    }

    function renderFieldInfo() {
        const d = STATE.fieldData;
        UI.title.textContent = `Pole: ${d.name}`;
        UI.fieldName.textContent = d.name;
        UI.area.textContent = formatArea(d.area);
        UI.crop.textContent = d.plantName || "Nie wybrano";
        UI.state.textContent = d.cycleName || "Nieznana";
        UI.soilComplex.textContent = d.soilComplex || "Brak danych";
        UI.soilType.textContent = d.soilType || "Brak danych";
        UI.soilSubstrate.textContent = d.soilSubstrate || "Brak danych";
        if (d.minBbox) {
            try {
                const rawBbox = `${d.minBbox.minX.toFixed(6)},${d.minBbox.minY.toFixed(6)},${d.minBbox.maxX.toFixed(6)},${d.minBbox.maxY.toFixed(6)}`;
                UI.minBbox.textContent = rawBbox;
            } catch (e) {
                UI.minBbox.textContent = d.minBbox;
            }
        } else {
            UI.minBbox.textContent = "Brak danych";
        }

        if (d.sowingDate) {
            UI.date.textContent = new Date(d.sowingDate).toLocaleDateString('pl-PL');
            UI.inputDate.value = d.sowingDate.split('T')[0];
        } else {
            UI.date.textContent = "Brak daty";
        }
    }

    async function loadLatestScan() {
        // 1. Reset widoku na start
        UI.scanLoader.style.display = "block";
        UI.scanImage.style.display = "none";
        if (UI.noScanMsg) UI.noScanMsg.style.display = "none";

        try {
            const res = await apiCall(`/latestScan/${CONFIG.FIELD_ID}`, 'POST', {
                geojson: STATE.fieldData.geojson
            });

            // SCENARIUSZ 1: Brak skanów (Kod 204)
            if (res.status === 204) {
                UI.scanDate.textContent = "Brak danych";

                UI.scanLoader.style.display = "none"; // Tu ukrywałeś OK
                if (UI.noScanMsg) UI.noScanMsg.style.display = "block";
                return;
            }

            if (!res.ok) throw new Error("Błąd pobierania obrazu");

            // SCENARIUSZ 2: Sukces
            const blob = await res.blob();
            UI.scanImage.src = URL.createObjectURL(blob);

            // ==========================================
            // <--- POPRAWKA: Ukrywamy loader!
            // ==========================================
            UI.scanLoader.style.display = "none";

            UI.scanImage.style.display = "block";
            if (UI.noScanMsg) UI.noScanMsg.style.display = "none";

            const dateHeader = res.headers.get("X-Scan-Date");
            UI.scanDate.textContent = dateHeader ? new Date(dateHeader).toLocaleDateString('pl-PL') : "Nieznana";

        } catch (err) {
            console.error(err);
            UI.scanLoader.style.display = "none"; // W błędzie też ukrywamy
            if (UI.noScanMsg) {
                UI.noScanMsg.innerHTML = `<p style='color:red'>Błąd pobierania danych.</p>`;
                UI.noScanMsg.style.display = "block";
            }
        }
    }

    // ============================================================
    // 6. NDVI & CLUSTERING
    // ============================================================
    async function showNdvi(scanId = null) {
        openModal(UI.modalNdvi);
        UI.imgNdvi.src = "";
        UI.imgNdvi.alt = "Generowanie mapy NDVI...";
        UI.imgColorbar.style.display = 'block';
        UI.imgLegend.style.display = 'none';

        // Upewnij się, że colorbar ma dobry motyw po otwarciu modala
        syncThemeNuclear();

        try {
            const endpoint = scanId ? `/NDVIDataById/${scanId}` : `/latestNDVIData/${CONFIG.FIELD_ID}`;
            const resData = await apiCall(endpoint);

            if (resData.status === 204) {
                UI.imgNdvi.alt = "Brak danych NDVI.";
                return;
            }
            if (!resData.ok) throw new Error("Błąd pobierania danych numerycznych");

            const json = await resData.json();
            const ndviData = json.ndvi || json;
            const currentScanBbox = json.fieldBbox || STATE.fieldData.geojson;
            STATE.ndviCache = ndviData;

            if (json.fieldBbox) {
                STATE.scanBboxCache = json.fieldBbox;
            } else {
                // Fallback (mało bezpieczny, ale lepszy niż nic)
                console.warn("Brak bboxa skanu, używam bboxa pola (może powodować rozciąganie!)");
                STATE.scanBboxCache = STATE.fieldData.minBbox;
            }

            const resViz = await apiCall('/visualize', 'POST', {
                ndviMatrix: ndviData,
                fieldBbox: STATE.fieldData.geojson,
                bbox: currentScanBbox
            });

            if (!resViz.ok) throw new Error("Błąd renderowania");

            const blob = await resViz.blob();
            UI.imgNdvi.src = URL.createObjectURL(blob);

        } catch (err) {
            UI.imgNdvi.alt = "Błąd: " + err.message;
        }
    }

    async function runClustering() {
        if (!STATE.ndviCache) return alert("Najpierw załaduj mapę NDVI.");
        if (!STATE.fieldData.plantStateId) return alert("Brak fazy rozwoju.");

        UI.imgNdvi.style.opacity = "0.5";

        try {
            const isDarkMode = localStorage.getItem('theme') === 'dark';

            const res = await apiCall(`/group/${CONFIG.FIELD_ID}`, 'POST', {
                cycleId: STATE.fieldData.plantStateId,
                ndvi: STATE.ndviCache,
                fieldGeojson: STATE.fieldData.geojson,
                imageBbox: STATE.scanBboxCache,
                darkMode: isDarkMode
            });

            if (!res.ok) throw new Error(await res.text());

            const result = await res.json();
            UI.imgNdvi.src = `data:image/png;base64,${result.mainImageBase64 || result.mainImage}`;
            UI.imgLegend.src = `data:image/png;base64,${result.legendBase64 || result.legend}`;

            UI.imgColorbar.style.display = 'none';
            UI.imgLegend.style.display = 'block';

        } catch (err) {
            alert("Błąd analizy AI: " + err.message);
        } finally {
            UI.imgNdvi.style.opacity = "1";
        }
    }

    // ============================================================
    // 7. OBSŁUGA IMPORTU / EDYCJI / HISTORII
    // ============================================================
    UI.btnImport.addEventListener('click', () => UI.inputTiff.click());
    UI.inputTiff.addEventListener('change', (e) => {
        if (e.target.files.length > 0) {
            STATE.selectedZip = e.target.files[0];
            openModal(UI.modalImportDate);
        }
    });
    UI.formImportDate.addEventListener('submit', async (e) => {
        e.preventDefault(); closeModal();
        const formData = new FormData();
        formData.append("zip", STATE.selectedZip);
        formData.append("date", UI.inputImportDate.value);
        formData.append("geojson", STATE.fieldData.geojson);

        try {
            const res = await apiCall(`/uploadScan/${CONFIG.FIELD_ID}`, 'POST', formData, true);
            if (!res.ok) throw new Error(await res.text());
            alert("Zaimportowano!"); loadLatestScan();
        } catch (err) { alert("Błąd: " + err.message); }
        finally { UI.inputTiff.value = ""; }
    });

    UI.btnEditCrop.addEventListener('click', async (e) => {
        e.preventDefault();
        const res = await apiCall('/getPlantsList');
        const plants = await res.json();
        UI.inputCrop.innerHTML = plants.map(p => `<option value="${p.id}">${p.name}</option>`).join('');
        if (STATE.fieldData.cropId) UI.inputCrop.value = STATE.fieldData.cropId;
        openModal(UI.modalCrop);
    });
    UI.btnEditDate.addEventListener('click', (e) => { e.preventDefault(); openModal(UI.modalDate); });

    UI.formCrop.addEventListener('submit', async (e) => {
        e.preventDefault();
        await updateField({ cropId: parseInt(UI.inputCrop.value), sowingDate: STATE.fieldData.sowingDate });
    });
    UI.formDate.addEventListener('submit', async (e) => {
        e.preventDefault();
        await updateField({ cropId: STATE.fieldData.cropId, sowingDate: UI.inputDate.value });
    });

    async function updateField(payload) {
        const res = await apiCall(`/update/${CONFIG.FIELD_ID}`, 'PUT', payload);
        if (res.ok) { closeModal(); initDashboard(); }
        else alert("Błąd zapisu.");
    }

    UI.btnHistory.addEventListener('click', async () => {
        openModal(UI.modalHistory);
        UI.historyTbody.innerHTML = "<tr><td>Ładowanie...</td></tr>";
        const res = await apiCall(`/getScansHistory/${CONFIG.FIELD_ID}`);
        const scans = await res.json();
        UI.historyTbody.innerHTML = "";
        if (!scans.length) { UI.historyTbody.innerHTML = "<tr><td>Brak historii</td></tr>"; return; }

        scans.forEach(s => {
            const tr = document.createElement('tr');
            tr.innerHTML = `
                <td>${new Date(s.date).toLocaleDateString()}</td>
                <td><button class="btn-sm btn-info view-rgb" data-id="${s.id}">RGB</button>
                    <button class="btn-sm btn-success view-ndvi" data-id="${s.id}">NDVI</button></td>
                <td><i class="fa-solid fa-trash text-danger delete-scan" style="cursor:pointer" data-id="${s.id}"></i></td>`;
            UI.historyTbody.appendChild(tr);
        });

        document.querySelectorAll('.view-rgb').forEach(btn => btn.addEventListener('click', async () => {
            const id = btn.dataset.id; openModal(UI.modalRgb);
            UI.imgRgb.src = "";
            const r = await apiCall(`/imageById/${id}`, 'POST', { geojson: STATE.fieldData.geojson });
            if (r.ok) UI.imgRgb.src = URL.createObjectURL(await r.blob());
        }));
        document.querySelectorAll('.view-ndvi').forEach(btn => btn.addEventListener('click', () => showNdvi(btn.dataset.id)));
        document.querySelectorAll('.delete-scan').forEach(btn => btn.addEventListener('click', async () => {
            if (confirm("Usunąć?")) { await apiCall(`/deleteScan/${btn.dataset.id}`, 'DELETE'); UI.btnHistory.click(); loadLatestScan(); }
        }));
    });

    function openModal(m) { m.style.display = 'block'; }
    function closeModal() { UI.modals.forEach(m => m.style.display = 'none'); }
    document.querySelectorAll('.btn-close, .btn-cancel').forEach(b => b.addEventListener('click', closeModal));
    window.onclick = (e) => { if (e.target.classList.contains('modal')) closeModal(); };

    UI.btnNdvi.addEventListener('click', () => showNdvi(null));
    UI.btnClustering.addEventListener('click', runClustering);

    // ============================================================
    // 8. EVENT LISTENER (NAPRAWA POWROTU I ZMIANY KARTY)
    // ============================================================
    window.addEventListener('pageshow', (event) => {
        syncThemeNuclear();
        if (event.persisted && STATE.fieldData) {
            renderFieldInfo();
        }
    });

    window.addEventListener('storage', (e) => {
        if (e.key === 'theme' || e.key === 'unitType') {
            syncThemeNuclear();
        }
    });

    // Start
    initDashboard();
});