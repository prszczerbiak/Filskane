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
        indexWidth: 0,
        indexHeight: 0,
        selectedZip: null,
        scanBboxCache: null,
        currentAnalysis: 'NDVI'
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

        // Elementy Analizy
        modalNdvi: document.getElementById('ndviModal'),
        imgNdvi: document.getElementById('ndviImage'),
        imgColorbar: document.getElementById('colorbar'),
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
        modalOverallAnalysis: document.getElementById('overallAnalysisModal'),
        overallAnalysisLoader: document.getElementById('overallAnalysisLoader'),
        overallAnalysisImage: document.getElementById('overallAnalysisImage'),
        overallAnalysisNoData: document.getElementById('overallAnalysisNoData'),
        overallAnalysisLegend: document.getElementById('overallAnalysisLegend'),
        btnGenerateOverallAnalysisReport: document.getElementById('generateOverallAnalysisReportBtn'),

        // Przyciski i Kontrolki
        btnImport: document.getElementById('importBtn'),
        inputTiff: document.getElementById('tiffInput'),
        btnAnalysis: document.getElementById('analysisBtn'),
        analysisDropdown: document.getElementById('analysisDropdown'),
        btnOverallAnalysis: document.querySelector('.btn-overall-analysis'),
        btnHistory: document.getElementById('historyBtn'),
        btnClustering: document.getElementById('clusteringBtn'),
        btnEditCrop: document.querySelector('.edit-btn[data-target="cropModal"]'),
        btnEditDate: document.querySelector('.edit-btn[data-target="dateModal"]')
    };

    // ============================================================
    // 3. SYNCHRONIZACJA WIZUALNA
    // ============================================================
    function syncThemeNuclear() {
        const savedTheme = localStorage.getItem('theme');
        document.documentElement.classList.remove('dark-theme');
        document.body.classList.remove('dark-theme');

        if (savedTheme === 'dark') {
            document.documentElement.classList.add('dark-theme');
        }
        if (UI.imgColorbar) {
            UI.imgColorbar.src = (savedTheme === 'dark') ? 'css/colorbarDark.png' : 'css/colorbar.png';
        }

        if (STATE.fieldData && STATE.fieldData.area) {
            UI.area.textContent = formatArea(STATE.fieldData.area);
        }

    }

    async function showOverallAnalysis() {
        openModal(UI.modalOverallAnalysis);

        if (UI.overallAnalysisLoader) UI.overallAnalysisLoader.style.display = 'block';
        if (UI.overallAnalysisImage) {
            UI.overallAnalysisImage.style.display = 'none';
            UI.overallAnalysisImage.src = '';
        }
        if (UI.overallAnalysisNoData) UI.overallAnalysisNoData.style.display = 'none';

        try {
            const res = await apiCall(`/overallAnalysis/${CONFIG.FIELD_ID}`, 'POST', {
                geojson: STATE.fieldData.geojson
            });

            if (res.status === 204) {
                if (UI.overallAnalysisNoData) UI.overallAnalysisNoData.style.display = 'block';
                return;
            }

            if (!res || !res.ok) throw new Error(await res.text());

            const blob = await res.blob();
            if (UI.overallAnalysisImage) {
                UI.overallAnalysisImage.src = URL.createObjectURL(blob);
                UI.overallAnalysisImage.style.display = 'block';
            }
        } catch (err) {
            if (UI.overallAnalysisNoData) {
                UI.overallAnalysisNoData.innerHTML = `<p>${err.message}</p>`;
                UI.overallAnalysisNoData.style.display = 'block';
            }
        } finally {
            if (UI.overallAnalysisLoader) UI.overallAnalysisLoader.style.display = 'none';
        }
    }

    async function sendOverallAnalysisReport() {
        if (UI.btnGenerateOverallAnalysisReport) {
            UI.btnGenerateOverallAnalysisReport.disabled = true;
        }

        try {
            const res = await apiCall(`/overallAnalysisReport/send/${CONFIG.FIELD_ID}`, 'POST', {
                geojson: STATE.fieldData.geojson
            });

            if (!res || !res.ok) throw new Error(await res.text());

            const result = await res.json();
            alert('Raport został wysłany.');
        } catch (err) {
            alert('Błąd wysyłania raportu: ' + err.message);
        } finally {
            if (UI.btnGenerateOverallAnalysisReport) {
                UI.btnGenerateOverallAnalysisReport.disabled = false;
            }
        }
    }

    async function downloadOverallAnalysisReport() {
        if (UI.btnGenerateOverallAnalysisReport) {
            UI.btnGenerateOverallAnalysisReport.disabled = true;
        }

        try {
            const res = await apiCall(`/overallAnalysisReport/${CONFIG.FIELD_ID}`, 'POST', {
                geojson: STATE.fieldData.geojson
            });

            if (res && res.status === 204) {
                alert('Brak danych do wygenerowania raportu.');
                return;
            }

            if (!res || !res.ok) throw new Error(await res.text());

            const blob = await res.blob();
            const url = URL.createObjectURL(blob);
            const link = document.createElement('a');
            link.href = url;
            link.download = `raport-analizy-pola-${CONFIG.FIELD_ID}.pdf`;
            document.body.appendChild(link);
            link.click();
            link.remove();
            URL.revokeObjectURL(url);
        } catch (err) {
            alert('Błąd generowania raportu: ' + err.message);
        } finally {
            if (UI.btnGenerateOverallAnalysisReport) {
                UI.btnGenerateOverallAnalysisReport.disabled = false;
            }
        }
    }

        

    async function updateField(payload) {
        try {
            const res = await apiCall(`/update/${CONFIG.FIELD_ID}`, 'PUT', payload);
            if (!res || !res.ok) throw new Error(await res.text());

            closeModal();
            await initDashboard();
        } catch (err) {
            alert("Błąd zapisu: " + err.message);
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

    function setIndexData(json) {
        const indexValues = json?.ndvi ?? json?.indexMatrix ?? json;
        STATE.ndviCache = Array.isArray(indexValues) ? indexValues : [];
        STATE.indexWidth = Number(json?.matrixWidth ?? json?.MatrixWidth ?? 0);
        STATE.indexHeight = Number(json?.matrixHeight ?? json?.MatrixHeight ?? 0);
        STATE.scanBboxCache = json?.fieldBbox || STATE.fieldData.minBbox;
        return STATE.ndviCache;
    }

    // ============================================================
    // 4. API HELPER
    // ============================================================
    async function apiCall(endpoint, method = 'GET', body = null, isFormData = false) {
        const token = localStorage.getItem("token");
        if (!token) {
            window.location.href = "login.html";
            return null;
        }

        const headers = { "Authorization": `Bearer ${token}` };
        if (!isFormData) headers["Content-Type"] = "application/json";

        const options = { method, headers };
        if (body) options.body = isFormData ? body : JSON.stringify(body);

        try {
            const res = await fetch(`${CONFIG.API_BASE}${endpoint}`, options);
            if (res.status === 401) {
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
        syncThemeNuclear();
        try {
            const res = await apiCall(`/getData/${CONFIG.FIELD_ID}`);
            if (!res.ok) throw new Error("Błąd pobierania danych pola");

            STATE.fieldData = await res.json();
            renderFieldInfo();
            loadLatestScan();
        } catch (err) {
            console.error(err);
            if (UI.title) UI.title.textContent = "Błąd: Nie znaleziono pola";
        }
    }

    function renderFieldInfo() {
        const d = STATE.fieldData;
        if (!d) return;
        if (UI.title) UI.title.textContent = `Pole: ${d.name}`;
        if (UI.fieldName) UI.fieldName.textContent = d.name;
        if (UI.area) UI.area.textContent = formatArea(d.area);
        if (UI.crop) UI.crop.textContent = d.plantName || "Nie wybrano";
        if (UI.state) UI.state.textContent = d.cycleName || "Nieznana";
        if (UI.soilComplex) UI.soilComplex.textContent = d.soilComplex || "Brak danych";
        if (UI.soilType) UI.soilType.textContent = d.soilType || "Brak danych";
        if (UI.soilSubstrate) UI.soilSubstrate.textContent = d.soilSubstrate || "Brak danych";

        if (UI.minBbox && d.minBbox) {
            UI.minBbox.textContent = `${d.minBbox.minX.toFixed(6)}, ${d.minBbox.minY.toFixed(6)}, ${d.minBbox.maxX.toFixed(6)}, ${d.minBbox.maxY.toFixed(6)}`;
        }

        if (UI.date && d.sowingDate) {
            UI.date.textContent = new Date(d.sowingDate).toLocaleDateString('pl-PL');
            if (UI.inputDate) UI.inputDate.value = d.sowingDate.split('T')[0];
        }
    }

    async function loadLatestScan() {
        if (!UI.scanLoader || !UI.scanImage) return;
        UI.scanLoader.style.display = "block";
        UI.scanImage.style.display = "none";
        if (UI.noScanMsg) UI.noScanMsg.style.display = "none";

        try {
            const res = await apiCall(`/latestScan/${CONFIG.FIELD_ID}`, 'POST', {
                geojson: STATE.fieldData.geojson
            });

            if (res.status === 204) {
                UI.scanDate.textContent = "Brak danych";
                UI.scanLoader.style.display = "none";
                if (UI.noScanMsg) UI.noScanMsg.style.display = "block";
                return;
            }

            if (!res.ok) throw new Error("Błąd pobierania obrazu");

            const blob = await res.blob();
            UI.scanImage.src = URL.createObjectURL(blob);
            UI.scanLoader.style.display = "none";
            UI.scanImage.style.display = "block";

            const dateHeader = res.headers.get("X-Scan-Date");
            UI.scanDate.textContent = dateHeader ? new Date(dateHeader).toLocaleDateString('pl-PL') : "Nieznana";

        } catch (err) {
            console.error(err);
            UI.scanLoader.style.display = "none";
            if (UI.noScanMsg) UI.noScanMsg.style.display = "block";
        }
    }

    // ============================================================
    // 6. ANALIZY (NDVI, GNDVI, ETC.)
    // ============================================================
    async function runAnalysis(type, scanId = null) {
        STATE.currentAnalysis = type;
        openModal(UI.modalNdvi);

        const modalTitle = UI.modalNdvi.querySelector('h3');
        if (modalTitle) modalTitle.textContent = `Analiza ${type}`;

        UI.imgNdvi.src = "";
        UI.imgNdvi.alt = `Generowanie mapy ${type}...`;
        UI.imgColorbar.style.display = 'block';
        UI.imgLegend.style.display = 'none';

        switch (type) {
            case 'NDVI':
                await showNdvi(scanId);
                break;
            case 'GNDVI':
                await showGndvi(scanId);
                break;
            case 'SAVI':
                await showSavi(scanId);
                break;
            case 'NDWI':
                await showNdwi(scanId);
                break;
            case 'EVI':
                await showEvi(scanId);
                break;
            default:
                UI.imgNdvi.alt = `Nieobsługiwany typ analizy: ${type}`;
                break;
        }
    }

    async function showNdvi(scanId = null) {
        try {
            const endpoint = scanId ? `/NDVIDataById/${scanId}` : `/latestNDVIData/${CONFIG.FIELD_ID}`;
            const resData = await apiCall(endpoint);

            if (resData.status === 204) {
                UI.imgNdvi.alt = "Brak danych skanu.";
                return;
            }

            const json = await resData.json();
            const ndviData = setIndexData(json);

            const resViz = await apiCall('/visualize', 'POST', {
                indexMatrix: ndviData,
                matrixWidth: STATE.indexWidth,
                matrixHeight: STATE.indexHeight,
                fieldGeoJson: STATE.fieldData.geojson,
                bbox: STATE.scanBboxCache,
                analysisType: 'NDVI'
            });

            if (!resViz.ok) throw new Error("Błąd renderowania");

            const blob = await resViz.blob();
            UI.imgNdvi.src = URL.createObjectURL(blob);

        } catch (err) {
            UI.imgNdvi.alt = "Błąd: " + err.message;
        }
    }

    async function showGndvi(scanId = null) {
        try {
            const endpoint = scanId ? `/GNDVIDataById/${scanId}` : `/latestGNDVIData/${CONFIG.FIELD_ID}`;
            const resData = await apiCall(endpoint);

            if (resData.status === 204) {
                UI.imgNdvi.alt = "Brak danych skanu.";
                return;
            }

            const json = await resData.json();
            const ndviData = setIndexData(json);

            const resViz = await apiCall('/visualize', 'POST', {
                indexMatrix: ndviData,
                matrixWidth: STATE.indexWidth,
                matrixHeight: STATE.indexHeight,
                fieldGeoJson: STATE.fieldData.geojson,
                bbox: STATE.scanBboxCache,
                analysisType: 'GNDVI'
            });

            if (!resViz.ok) throw new Error("Błąd renderowania");

            const blob = await resViz.blob();
            UI.imgNdvi.src = URL.createObjectURL(blob);

        } catch (err) {
            UI.imgNdvi.alt = "Błąd: " + err.message;
        }
    }

    async function showSavi(scanId = null) {
        try {
            const endpoint = scanId ? `/SAVIDataById/${scanId}` : `/latestSAVIData/${CONFIG.FIELD_ID}`;
            const resData = await apiCall(endpoint);

            if (resData.status === 204) {
                UI.imgNdvi.alt = "Brak danych skanu.";
                return;
            }

            const json = await resData.json();
            const ndviData = setIndexData(json);

            const resViz = await apiCall('/visualize', 'POST', {
                indexMatrix: ndviData,
                matrixWidth: STATE.indexWidth,
                matrixHeight: STATE.indexHeight,
                fieldGeoJson: STATE.fieldData.geojson,
                bbox: STATE.scanBboxCache,
                analysisType: 'SAVI'
            });

            if (!resViz.ok) throw new Error("Błąd renderowania");

            const blob = await resViz.blob();
            UI.imgNdvi.src = URL.createObjectURL(blob);

        } catch (err) {
            UI.imgNdvi.alt = "Błąd: " + err.message;
        }
    }

    async function showNdwi(scanId = null) {
        try {
            const endpoint = scanId ? `/NDWIDataById/${scanId}` : `/latestNDWIData/${CONFIG.FIELD_ID}`;
            const resData = await apiCall(endpoint);

            if (resData.status === 204) {
                UI.imgNdvi.alt = "Brak danych skanu.";
                return;
            }

            const json = await resData.json();
            const ndviData = setIndexData(json);

            const resViz = await apiCall('/visualize', 'POST', {
                indexMatrix: ndviData,
                matrixWidth: STATE.indexWidth,
                matrixHeight: STATE.indexHeight,
                fieldGeoJson: STATE.fieldData.geojson,
                bbox: STATE.scanBboxCache,
                analysisType: 'NDWI'
            });

            if (!resViz.ok) throw new Error("Błąd renderowania");

            const blob = await resViz.blob();
            UI.imgNdvi.src = URL.createObjectURL(blob);

        } catch (err) {
            UI.imgNdvi.alt = "Błąd: " + err.message;
        }
    }

    async function showEvi(scanId = null) {
        try {
            const endpoint = scanId ? `/EVIDataById/${scanId}` : `/latestEVIData/${CONFIG.FIELD_ID}`;
            const resData = await apiCall(endpoint);

            if (resData.status === 204) {
                UI.imgNdvi.alt = "Brak danych skanu.";
                return;
            }

            const json = await resData.json();
            const ndviData = setIndexData(json);

            const resViz = await apiCall('/visualize', 'POST', {
                indexMatrix: ndviData,
                matrixWidth: STATE.indexWidth,
                matrixHeight: STATE.indexHeight,
                fieldGeoJson: STATE.fieldData.geojson,
                bbox: STATE.scanBboxCache,
                analysisType: 'EVI'
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
        if (!STATE.fieldData?.cropId || !STATE.fieldData?.plantStateId) {
            return alert("Brak danych uprawy lub cyklu dla tego pola.");
        }
        UI.imgNdvi.style.opacity = "0.5";

        try {
            const res = await apiCall(`/group/${CONFIG.FIELD_ID}`, 'POST', {
                plantId: STATE.fieldData.cropId,
                cycleId: STATE.fieldData.plantStateId,
                vegetationIndex: STATE.ndviCache,
                matrixWidth: STATE.indexWidth,
                matrixHeight: STATE.indexHeight,
                analysisType: STATE.currentAnalysis || 'NDVI',
                fieldGeojson: STATE.fieldData.geojson,
                imageBbox: STATE.scanBboxCache,
                darkMode: localStorage.getItem('theme') === 'dark'
            });

            if (!res.ok) throw new Error(await res.text());

            const result = await res.json();
            UI.imgNdvi.src = `data:image/png;base64,${result.mainImageBase64 || result.mainImage}`;
            UI.imgLegend.src = `data:image/png;base64,${result.legendBase64 || result.legend}`;

            UI.imgColorbar.style.display = 'none';
            UI.imgLegend.style.display = 'block';
        } catch (err) {
            alert("Błąd AI: " + err.message);
        } finally {
            UI.imgNdvi.style.opacity = "1";
        }
    }

    async function showOverallAnalysis() {
        openModal(UI.modalOverallAnalysis);

        if (UI.overallAnalysisLoader) UI.overallAnalysisLoader.style.display = 'block';
        if (UI.overallAnalysisNoData) UI.overallAnalysisNoData.style.display = 'none';
        if (UI.overallAnalysisLegend) UI.overallAnalysisLegend.style.display = 'none';
        if (UI.overallAnalysisImage) {
            UI.overallAnalysisImage.style.display = 'none';
            UI.overallAnalysisImage.src = '';
        }

        try {
            const res = await apiCall(`/overallAnalysis/${CONFIG.FIELD_ID}`, 'POST', {
                geojson: STATE.fieldData.geojson
            });

            if (res && res.status === 204) {
                if (UI.overallAnalysisNoData) UI.overallAnalysisNoData.style.display = 'block';
                return;
            }

            if (!res || !res.ok) throw new Error(await res.text());

            const blob = await res.blob();
            if (UI.overallAnalysisImage) {
                UI.overallAnalysisImage.src = URL.createObjectURL(blob);
                UI.overallAnalysisImage.style.display = 'block';
            }
            if (UI.overallAnalysisLegend) {
                UI.overallAnalysisLegend.style.display = 'block';
            }
        } catch (err) {
            if (UI.overallAnalysisNoData) {
                UI.overallAnalysisNoData.innerHTML = `<p>${err.message}</p>`;
                UI.overallAnalysisNoData.style.display = 'block';
            }
        } finally {
            if (UI.overallAnalysisLoader) UI.overallAnalysisLoader.style.display = 'none';
        }
    }

    // ============================================================
    // 7. OBSŁUGA ZDARZEŃ I MODALI (BEZPIECZNA)
    // ============================================================
    function openModal(m) { if (m) m.style.display = 'block'; }
    function closeModal() { UI.modals.forEach(m => m.style.display = 'none'); }

    document.querySelectorAll('.btn-close, .btn-cancel').forEach(b => b.addEventListener('click', closeModal));

    window.onclick = (e) => {
        if (e.target.classList.contains('modal')) closeModal();
        if (UI.analysisDropdown && UI.btnAnalysis && !UI.btnAnalysis.contains(e.target) && !UI.analysisDropdown.contains(e.target)) {
            UI.analysisDropdown.style.display = 'none';
        }
    };

    // Import
    if (UI.btnImport) UI.btnImport.addEventListener('click', () => UI.inputTiff.click());
    if (UI.inputTiff) {
        UI.inputTiff.addEventListener('change', (e) => {
            if (e.target.files.length > 0) {
                STATE.selectedZip = e.target.files[0];
                openModal(UI.modalImportDate);
            }
        });
    }

    // Edycja uprawy i daty siewu
    if (UI.btnEditCrop) {
        UI.btnEditCrop.addEventListener('click', async (e) => {
            e.preventDefault();

            try {
                const res = await apiCall('/getPlantsList');
                if (!res || !res.ok) throw new Error('Nie udało się pobrać listy upraw');

                const plants = await res.json();
                UI.inputCrop.innerHTML = plants.map(p => `<option value="${p.id}">${p.name}</option>`).join('');

                if (STATE.fieldData?.cropId) {
                    UI.inputCrop.value = STATE.fieldData.cropId;
                }

                openModal(UI.modalCrop);
            } catch (err) {
                alert(err.message);
            }
        });
    }

    if (UI.btnEditDate) {
        UI.btnEditDate.addEventListener('click', (e) => {
            e.preventDefault();
            openModal(UI.modalDate);
        });
    }

    if (UI.formCrop) {
        UI.formCrop.addEventListener('submit', async (e) => {
            e.preventDefault();

            const cropId = Number(UI.inputCrop.value);
            const sowingDate = STATE.fieldData?.sowingDate ? STATE.fieldData.sowingDate : null;

            await updateField({
                cropId: Number.isNaN(cropId) ? null : cropId,
                sowingDate
            });
        });
    }

    if (UI.formDate) {
        UI.formDate.addEventListener('submit', async (e) => {
            e.preventDefault();

            await updateField({
                cropId: STATE.fieldData?.cropId ?? null,
                sowingDate: UI.inputDate.value || null
            });
        });
    }

    if (UI.formImportDate) {
        UI.formImportDate.addEventListener('submit', async (e) => {
            e.preventDefault();
            closeModal();
            const formData = new FormData();
            formData.append("zip", STATE.selectedZip);
            formData.append("date", UI.inputImportDate.value);
            formData.append("geojson", STATE.fieldData.geojson);
            try {
                const res = await apiCall(`/uploadScan/${CONFIG.FIELD_ID}`, 'POST', formData, true);
                if (!res.ok) throw new Error(await res.text());
                alert("Sukces!"); loadLatestScan();
            } catch (err) { alert(err.message); }
        });
    }

    // Dropdown Analizy
    if (UI.btnAnalysis) {
        UI.btnAnalysis.addEventListener('click', (e) => {
            e.stopPropagation();
            if (UI.analysisDropdown) {
                const isHidden = UI.analysisDropdown.style.display === 'none' || UI.analysisDropdown.style.display === '';
                UI.analysisDropdown.style.display = isHidden ? 'flex' : 'none';
            }
        });
    }

    if (UI.analysisDropdown) {
        UI.analysisDropdown.querySelectorAll('.btn-action').forEach(btn => {
            btn.addEventListener('click', () => {
                const type = btn.getAttribute('data-analysis');
                runAnalysis(type);
                UI.analysisDropdown.style.display = 'none';
            });
        });
    }

    if (UI.btnOverallAnalysis) {
        UI.btnOverallAnalysis.addEventListener('click', () => {
            showOverallAnalysis();
            if (UI.analysisDropdown) UI.analysisDropdown.style.display = 'none';
        });
    }

    if (UI.btnGenerateOverallAnalysisReport) {
        UI.btnGenerateOverallAnalysisReport.addEventListener('click', async () => {
            await sendOverallAnalysisReport();
        });
    }

    if (UI.btnGenerateOverallAnalysisReport) {
        UI.btnGenerateOverallAnalysisReport.addEventListener('click', downloadOverallAnalysisReport);
    }

    // Przyciski Akcji
    if (UI.btnClustering) UI.btnClustering.addEventListener('click', runClustering);
    if (UI.btnHistory) {
        UI.btnHistory.addEventListener('click', async () => {
            openModal(UI.modalHistory);
            UI.historyTbody.innerHTML = '<tr><td colspan="3">Ładowanie...</td></tr>';

            try {
                const res = await apiCall(`/getScansHistory/${CONFIG.FIELD_ID}`);
                if (!res || !res.ok) throw new Error('Błąd pobierania historii');

                const scans = await res.json();
                UI.historyTbody.innerHTML = '';

                if (!scans.length) {
                    UI.historyTbody.innerHTML = '<tr><td colspan="3">Brak historii</td></tr>';
                    return;
                }

                scans.forEach(s => {
                    const tr = document.createElement('tr');
                    tr.innerHTML = `
                        <td>${new Date(s.date).toLocaleDateString('pl-PL')}</td>
                        <td>
                            <button class="btn-sm btn-rgb-gradient view-rgb" data-id="${s.id}">Obraz</button>
                            <button class="btn-sm btn-success view-analysis" data-analysis="NDVI" data-id="${s.id}">NDVI</button>
                            <button class="btn-sm btn-success view-analysis" data-analysis="GNDVI" data-id="${s.id}">GNDVI</button>
                            <button class="btn-sm btn-success view-analysis" data-analysis="SAVI" data-id="${s.id}">SAVI</button>
                            <button class="btn-sm btn-ndwi view-analysis" data-analysis="NDWI" data-id="${s.id}">NDWI</button>
                            <button class="btn-sm btn-success view-analysis" data-analysis="EVI" data-id="${s.id}">EVI</button>
                        </td>
                        <td><i class="fa-solid fa-trash text-danger delete-scan" style="cursor:pointer" data-id="${s.id}"></i></td>`;
                    UI.historyTbody.appendChild(tr);
                });

                UI.historyTbody.querySelectorAll('.view-rgb').forEach(btn => btn.addEventListener('click', async () => {
                    const id = btn.dataset.id;
                    openModal(UI.modalRgb);
                    UI.imgRgb.src = '';
                    const r = await apiCall(`/imageById/${id}`, 'POST', { geojson: STATE.fieldData.geojson });
                    if (r && r.ok) UI.imgRgb.src = URL.createObjectURL(await r.blob());
                }));

                UI.historyTbody.querySelectorAll('.view-analysis').forEach(btn => btn.addEventListener('click', () => {
                    const scanId = Number(btn.dataset.id);
                    const analysisType = btn.dataset.analysis;
                    closeModal();
                    runAnalysis(analysisType, scanId);
                }));

                UI.historyTbody.querySelectorAll('.delete-scan').forEach(btn => btn.addEventListener('click', async () => {
                    if (!confirm('Usunąć?')) return;

                    const delRes = await apiCall(`/deleteScan/${btn.dataset.id}`, 'DELETE');
                    if (delRes && delRes.ok) {
                        UI.btnHistory.click();
                        loadLatestScan();
                    }
                }));
            } catch (err) {
                UI.historyTbody.innerHTML = `<tr><td colspan="3">${err.message}</td></tr>`;
            }
        });
    }

    // Start
    initDashboard();
});