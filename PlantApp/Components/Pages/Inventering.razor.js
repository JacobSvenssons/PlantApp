// JS interop-modul för Inventering.razor.
// Ansvar: initiera Leaflet-karta, rita traktens delområden som bakgrund, låta
// användaren markera provytors platser genom att klicka i kartan, visa användarens
// GPS-position, samt rita ut både temporära (osparade) och sparade provyta-markörer.

const instances = new Map();

const DELOMRADE_STYLE = { color: "#333333", weight: 2, fillColor: "#000000", fillOpacity: 0 };
const DELOMRADE_CLASS = "inventering-delomrade";

let pointerStyleInjected = false;
function ensureDelomradePointerStyle() {
    if (pointerStyleInjected || typeof document === "undefined") {
        return;
    }
    const style = document.createElement("style");
    style.textContent = `.${DELOMRADE_CLASS}{pointer-events:all !important;}`;
    document.head.appendChild(style);
    pointerStyleInjected = true;
}

export function initMap(containerId, geoJson, dotNetRef) {
    dispose(containerId);
    ensureDelomradePointerStyle();

    const map = L.map(containerId, { zoomControl: true });

    L.tileLayer(
        "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
        {
            maxZoom: 19,
            attribution:
                "Tiles © Esri — Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community"
        }
    ).addTo(map);

    const instance = {
        map,
        dotNetRef,
        provytaLayer: null,   // sparade provytor (från servern)
        tempMarkers: [],      // temporära markörer för osparade provytor
        activeMarker: null,   // markör för provytan som redigeras just nu
        positionMarker: null, // användarens GPS-position
        positionCircle: null, // osäkerhetscirkel för positionen
        picking: false        // sant medan användaren markerar en provytas plats
    };

    async function handlePick(latlng) {
        instance.picking = false;
        if (instance.map?.getContainer) {
            instance.map.getContainer().style.cursor = "";
        }
        try {
            await dotNetRef.invokeMethodAsync("OnMapPointPicked", latlng.lat, latlng.lng);
        } catch (err) {
            console.error("Kunde inte skicka provyta-markering till Blazor:", err);
        }
    }

    const data = typeof geoJson === "string" ? JSON.parse(geoJson) : geoJson;

    const geoJsonLayer = L.geoJSON(data, {
        style: () => ({ ...DELOMRADE_STYLE, className: DELOMRADE_CLASS }),
        onEachFeature: (feature, layer) => {
            layer.on("click", async (e) => {
                if (instance.picking) {
                    if (e?.originalEvent) {
                        L.DomEvent.stopPropagation(e);
                    }
                    await handlePick(e.latlng);
                }
            });
        }
    }).addTo(map);

    const bounds = geoJsonLayer.getBounds();
    if (bounds.isValid()) {
        map.fitBounds(bounds, { padding: [20, 20] });
    } else {
        map.setView([62.0, 15.0], 5);
    }

    map.on("click", async (e) => {
        if (!instance.picking) {
            return;
        }
        await handlePick(e.latlng);
    });

    instances.set(containerId, instance);
}

export function startPointPicking() {
    for (const instance of instances.values()) {
        instance.picking = true;
        if (instance.map?.getContainer) {
            instance.map.getContainer().style.cursor = "crosshair";
        }
    }
}

export function cancelPointPicking() {
    for (const instance of instances.values()) {
        instance.picking = false;
        if (instance.map?.getContainer) {
            instance.map.getContainer().style.cursor = "";
        }
    }
}

// Ritar de temporära markörerna för osparade provytor.
// punkter: [{ index, lat, lng }]
export function setTempProvytaMarkorer(punkter) {
    for (const instance of instances.values()) {
        for (const marker of instance.tempMarkers) {
            instance.map.removeLayer(marker);
        }
        instance.tempMarkers = [];

        for (const p of punkter ?? []) {
            const marker = L.marker([p.lat, p.lng], {
                title: `Provyta #${(p.index ?? 0) + 1}`
            }).addTo(instance.map);
            marker.bindTooltip(`#${(p.index ?? 0) + 1}`, { permanent: false });
            instance.tempMarkers.push(marker);
        }
    }
}

// Markerar (och panorerar till) den provyta som redigeras just nu.
export function setActivePin(lat, lng) {
    for (const instance of instances.values()) {
        if (instance.activeMarker) {
            instance.map.removeLayer(instance.activeMarker);
            instance.activeMarker = null;
        }

        if (lat == null || lng == null) {
            continue;
        }

        instance.activeMarker = L.circleMarker([lat, lng], {
            radius: 11,
            color: "#f59e0b",
            weight: 3,
            fillColor: "#fbbf24",
            fillOpacity: 0.35
        }).addTo(instance.map);

        if (instance.map.getBounds && !instance.map.getBounds().contains([lat, lng])) {
            instance.map.panTo([lat, lng]);
        }
    }
}

// Panorerar/zoomar till en given punkt (t.ex. när en provyta öppnas i listan).
export function focusPoint(lat, lng) {
    if (lat == null || lng == null) {
        return;
    }
    for (const instance of instances.values()) {
        instance.map.setView([lat, lng], Math.max(instance.map.getZoom(), 17));
    }
}

// Hämtar användarens GPS-position, ritar ut den och returnerar { lat, lng }.
export function getCurrentPosition() {
    return new Promise((resolve, reject) => {
        if (!navigator.geolocation) {
            reject("Positionering stöds inte i den här webbläsaren.");
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (pos) => {
                const lat = pos.coords.latitude;
                const lng = pos.coords.longitude;
                const accuracy = pos.coords.accuracy;

                for (const instance of instances.values()) {
                    if (instance.positionMarker) {
                        instance.map.removeLayer(instance.positionMarker);
                    }
                    if (instance.positionCircle) {
                        instance.map.removeLayer(instance.positionCircle);
                    }

                    instance.positionCircle = L.circle([lat, lng], {
                        radius: accuracy,
                        color: "#2563eb",
                        weight: 1,
                        fillColor: "#3b82f6",
                        fillOpacity: 0.12
                    }).addTo(instance.map);

                    instance.positionMarker = L.circleMarker([lat, lng], {
                        radius: 7,
                        color: "#1d4ed8",
                        weight: 3,
                        fillColor: "#60a5fa",
                        fillOpacity: 0.95
                    }).addTo(instance.map);

                    instance.map.setView([lat, lng], Math.max(instance.map.getZoom(), 17));
                }

                resolve({ lat, lng });
            },
            (err) => reject(err?.message ?? "Kunde inte hämta position."),
            { enableHighAccuracy: true, timeout: 10000, maximumAge: 5000 }
        );
    });
}

// Visar bara användarens position utan att registrera en provyta.
export async function showCurrentPosition() {
    await getCurrentPosition();
}

// Ritar det sparade provyta-lagret (GeoJSON-punkter) på kartan.
export function setProvytaLager(geoJson) {
    const data = typeof geoJson === "string" ? JSON.parse(geoJson) : geoJson;
    for (const instance of instances.values()) {
        if (instance.provytaLayer) {
            instance.map.removeLayer(instance.provytaLayer);
            instance.provytaLayer = null;
        }

        instance.provytaLayer = L.geoJSON(data, {
            pointToLayer: (feature, latlng) =>
                L.circleMarker(latlng, {
                    radius: 6,
                    color: "#166534",
                    weight: 2,
                    fillColor: "#22c55e",
                    fillOpacity: 0.85
                }),
            onEachFeature: (feature, layer) => {
                const props = feature.properties ?? {};
                const antal = props.antalPlantor ?? 0;
                const sort = props.tradsort ? ` – ${props.tradsort}` : "";
                layer.bindTooltip(`Provyta: ${antal} plantor${sort}`, { permanent: false });
            }
        }).addTo(instance.map);
    }
}

export function dispose(containerId) {
    const instance = instances.get(containerId);
    if (instance) {
        instance.map.remove();
        instances.delete(containerId);
    }
}
