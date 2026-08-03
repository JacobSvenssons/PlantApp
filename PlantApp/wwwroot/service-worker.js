// service-worker.js
// Offlinestöd för PlantApp.
//
// VIKTIGT om arkitekturen:
// Appen är Blazor Server – de interaktiva sidorna kräver en levande SignalR-anslutning
// och kan INTE köras offline. Därför cachar vi INTE Blazor-kretsens navigering aggressivt.
// Istället:
//  - Statiska resurser (fältytan, Leaflet, ikoner, manifest) precachas för offline.
//  - GeoJSON och API-GET cachas "network-first" så senaste data används online, men
//    en cachad kopia visas offline.
//  - Navigering till /falt (den fristående offline-fältytan) fungerar offline via cache.
//  - Navigering till Blazor-sidor går alltid mot nätet; utan nät hänvisas till /falt.

const CACHE_VERSION = "plantapp-v2";
const APP_SHELL_CACHE = `${CACHE_VERSION}-shell`;
const RUNTIME_CACHE = `${CACHE_VERSION}-runtime`;

// Resurser som måste finnas offline för att fältytan ska fungera.
const APP_SHELL = [
    "/falt/",
    "/falt/index.html",
    "/falt/falt.js",
    "/js/offline-db.js",
    "/js/offline-sync.js",
    "/manifest.json",
    "/icon-192.png",
    "/icon-512.png",
    "https://unpkg.com/leaflet@1.9.4/dist/leaflet.css",
    "https://unpkg.com/leaflet@1.9.4/dist/leaflet.js"
];

self.addEventListener("install", (event) => {
    event.waitUntil(
        caches.open(APP_SHELL_CACHE)
            .then((cache) => cache.addAll(APP_SHELL.map((url) => new Request(url, { cache: "reload" }))))
            .then(() => self.skipWaiting())
            .catch((err) => console.warn("[sw] precache fel:", err))
    );
});

self.addEventListener("activate", (event) => {
    event.waitUntil(
        caches.keys()
            .then((keys) => Promise.all(
                keys
                    .filter((key) => !key.startsWith(CACHE_VERSION))
                    .map((key) => caches.delete(key))
            ))
            .then(() => self.clients.claim())
    );
});

function isGeoJsonOrApi(url) {
    return url.pathname.includes("/geojson")
        || url.pathname.startsWith("/api/trakt")
        || url.pathname.startsWith("/api/inventering");
}

// Network-first med cache-fallback (för API/GeoJSON: färsk data online, cachad offline).
async function networkFirst(request) {
    const cache = await caches.open(RUNTIME_CACHE);
    try {
        const response = await fetch(request);
        if (response && response.ok && request.method === "GET") {
            cache.put(request, response.clone());
        }
        return response;
    } catch (err) {
        const cached = await cache.match(request);
        if (cached) {
            return cached;
        }
        throw err;
    }
}

// Cache-first (för statiska app-shell-resurser).
async function cacheFirst(request) {
    const cached = await caches.match(request);
    if (cached) {
        return cached;
    }
    const response = await fetch(request);
    if (response && response.ok) {
        const cache = await caches.open(APP_SHELL_CACHE);
        cache.put(request, response.clone());
    }
    return response;
}

self.addEventListener("fetch", (event) => {
    const request = event.request;

    // Endast GET hanteras av cachen; POST/PUT (t.ex. synk) går alltid mot nätet.
    if (request.method !== "GET") {
        return;
    }

    const url = new URL(request.url);

    // Navigeringar (adressfält/länkar):
    if (request.mode === "navigate") {
        // Fältytan ska fungera offline.
        if (url.pathname.startsWith("/falt")) {
            event.respondWith(cacheFirst(request).catch(() => caches.match("/falt/index.html")));
            return;
        }
        // Blazor-sidor kräver nät; vid offline hänvisa till fältytan.
        event.respondWith(
            fetch(request).catch(() => caches.match("/falt/index.html"))
        );
        return;
    }

    // API/GeoJSON: network-first så offline-kopian finns tillgänglig.
    if (isGeoJsonOrApi(url)) {
        event.respondWith(networkFirst(request));
        return;
    }

    // Leaflet CDN + app-shell-resurser: cache-first.
    if (APP_SHELL.includes(url.href) || APP_SHELL.includes(url.pathname)) {
        event.respondWith(cacheFirst(request));
        return;
    }

    // Kartrutor (OpenStreetMap tiles): cacha opportunistiskt så tidigare besökta
    // områden syns offline. Begränsad livslängd via runtime-cachen.
    if (url.hostname.endsWith("tile.openstreetmap.org")) {
        event.respondWith(
            caches.open(RUNTIME_CACHE).then(async (cache) => {
                const cached = await cache.match(request);
                if (cached) {
                    return cached;
                }
                try {
                    const response = await fetch(request);
                    if (response && response.ok) {
                        cache.put(request, response.clone());
                    }
                    return response;
                } catch (err) {
                    return cached || Response.error();
                }
            })
        );
        return;
    }

    // Övriga statiska filer: försök nät, fall tillbaka till cache.
    event.respondWith(
        fetch(request).catch(() => caches.match(request))
    );
});

// Tillåt sidan att be service workern aktivera sig direkt efter uppdatering.
self.addEventListener("message", (event) => {
    if (event.data === "skipWaiting") {
        self.skipWaiting();
    }
});
