// JS interop-modul för TraktKarta.razor.
// Ansvar: initiera Leaflet-karta, rita GeoJSON-lager, färglägga efter status,
// skicka klick till Blazor och färglägga om en ruta efter sparning.

// Håller reda på state per kart-container (id -> { map, geoJsonLayer, layersById }).
const instances = new Map();

// Statusfärger enligt planteringsflödet.
// 0 = Ej påbörjad, 1 = Påbörjad, 2 = Klar.
const STATUS_STYLES = {
    0: { color: "#333333", weight: 2, fillColor: "#000000", fillOpacity: 0 },
    1: { color: "#8a6d1f", weight: 2, fillColor: "#E2B13C", fillOpacity: 0.55 },
    2: { color: "#166016", weight: 2, fillColor: "#228B22", fillOpacity: 0.55 }
};

// Översätter status (string-namn eller numeriskt värde) till 0/1/2.
function normalizeStatus(status) {
    if (typeof status === "number") {
        return status;
    }

    switch (String(status ?? "").toLowerCase()) {
        case "paborjad":
        case "1":
            return 1;
        case "klar":
        case "2":
            return 2;
        default:
            return 0; // EjPaborjad
    }
}

function styleForStatus(status) {
    return STATUS_STYLES[normalizeStatus(status)] ?? STATUS_STYLES[0];
}

export function initMap(containerId, geoJson, dotNetRef) {
    // Städa ev. tidigare instans (t.ex. vid re-render).
    dispose(containerId);

    const map = L.map(containerId, { zoomControl: true });

    // Öppet satellitlager (Esri World Imagery).
    L.tileLayer(
        "https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}",
        {
            maxZoom: 19,
            attribution:
                "Tiles © Esri — Source: Esri, Maxar, Earthstar Geographics, and the GIS User Community"
        }
    ).addTo(map);

    const layersById = new Map();

    const data = typeof geoJson === "string" ? JSON.parse(geoJson) : geoJson;

    const geoJsonLayer = L.geoJSON(data, {
        style: (feature) => styleForStatus(feature.properties?.status),
        onEachFeature: (feature, layer) => {
            const props = feature.properties ?? {};
            // GeoJsonWriter lyfter "id" till feature-nivå (feature.id), inte in i properties.
            // Läs därför feature.id i första hand och properties.id som fallback.
            const featureId = feature.id ?? props.id;
            if (featureId != null) {
                layersById.set(Number(featureId), layer);
            }

            layer.on("click", async () => {
                try {
                    await dotNetRef.invokeMethodAsync("OnFeatureClicked", {
                        id: featureId,
                        traktId: props.traktId,
                        status: props.status,
                        areal: props.areal,
                        tradsort: props.tradsort,
                        antalPlantor: props.antalPlantor,
                        antalPlantorProvyta: props.antalPlantorProvyta
                    });
                } catch (err) {
                    console.error("Kunde inte skicka klick till Blazor:", err);
                }
            });
        }
    }).addTo(map);

    // Centrera kartan på trakten.
    const bounds = geoJsonLayer.getBounds();
    if (bounds.isValid()) {
        map.fitBounds(bounds, { padding: [20, 20] });
    } else {
        // Fallback: centrera över Sverige om inga geometrier finns.
        map.setView([62.0, 15.0], 5);
    }

    instances.set(containerId, { map, geoJsonLayer, layersById });
}

// Färglägger om en ruta direkt efter att status sparats i backend.
export function updateFeatureStatus(featureId, statusValue) {
    for (const instance of instances.values()) {
        const layer = instance.layersById.get(Number(featureId));
        if (layer) {
            layer.setStyle(styleForStatus(statusValue));
            if (layer.feature?.properties) {
                layer.feature.properties.status = statusValue;
            }
        }
    }
}

export function dispose(containerId) {
    const instance = instances.get(containerId);
    if (instance) {
        instance.map.remove();
        instances.delete(containerId);
    }
}
