// offline-db.js
// IndexedDB-hjälpare för offlinestöd i fält (inventeringar OCH planteringar/delområden).
//
// Två syften:
//  1) Cacha nedladdade "paket" – en inventering (med trakt-GeoJSON + provytor) eller
//     en trakt för plantering (med GeoJSON + alla delområdens detaljer). Nyckel: "typ:id".
//  2) Hålla en GENERISK utgående kö av ändringar. Varje köpost innehåller den färdiga
//     endpointen och body:n som ska PUT:as, så synk blir identisk oavsett om det gäller
//     en inventering (PUT /api/inventering/{id}) eller ett delområde (PUT /api/trakt/delomrade/{id}).
//
// Exponeras som window.OfflineDb.

(function () {
    const DB_NAME = "plantapp-offline";
    const DB_VERSION = 2;
    const STORE_PAKET = "paket"; // key: "typ:id"  (typ = "inventering" | "trakt")
    const STORE_KO = "utko";     // key: "typ:id"  (senaste tillstånd per redigerbar enhet)

    let _dbPromise = null;

    function openDb() {
        if (_dbPromise) {
            return _dbPromise;
        }

        _dbPromise = new Promise((resolve, reject) => {
            const req = indexedDB.open(DB_NAME, DB_VERSION);

            req.onupgradeneeded = (event) => {
                const db = event.target.result;
                // Rensa ev. gamla v1-stores och skapa de generiska.
                for (const name of Array.from(db.objectStoreNames)) {
                    db.deleteObjectStore(name);
                }
                db.createObjectStore(STORE_PAKET, { keyPath: "key" });
                db.createObjectStore(STORE_KO, { keyPath: "key" });
            };

            req.onsuccess = () => resolve(req.result);
            req.onerror = () => reject(req.error);
        });

        return _dbPromise;
    }

    function store(storeName, mode) {
        return openDb().then((db) => db.transaction(storeName, mode).objectStore(storeName));
    }

    function toPromise(request) {
        return new Promise((resolve, reject) => {
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });
    }

    function paketNyckel(typ, id) {
        return `${typ}:${Number(id)}`;
    }

    const OfflineDb = {
        paketNyckel,

        // ---- Paket (nedladdad data) ----

        /**
         * Sparar ett nedladdat paket för offline-bruk.
         * @param {object} paket { typ, id, namn, traktGeoJson, data, nedladdad }
         */
        async sparaPaket(paket) {
            if (!paket || paket.typ == null || paket.id == null) {
                throw new Error("typ och id krävs för att cacha paket.");
            }
            paket.key = paketNyckel(paket.typ, paket.id);
            paket.nedladdad = paket.nedladdad || new Date().toISOString();
            const s = await store(STORE_PAKET, "readwrite");
            await toPromise(s.put(paket));
            return true;
        },

        async hamtaPaket(typ, id) {
            const s = await store(STORE_PAKET, "readonly");
            return (await toPromise(s.get(paketNyckel(typ, id)))) || null;
        },

        async listaPaket() {
            const s = await store(STORE_PAKET, "readonly");
            return (await toPromise(s.getAll())) || [];
        },

        async taBortPaket(typ, id) {
            const s = await store(STORE_PAKET, "readwrite");
            await toPromise(s.delete(paketNyckel(typ, id)));
            return true;
        },

        // ---- Generisk utgående kö ----

        /**
         * Köar en ändring för synk. En post per redigerbar enhet (nyckel typ:id):
         * senaste tillståndet ersätter tidigare, vilket matchar "ersätt allt"-endpoints.
         * @param {string} typ   "inventering" | "trakt"
         * @param {number} id     inventeringId eller delOmradeId
         * @param {string} endpoint  t.ex. "/api/inventering/5" eller "/api/trakt/delomrade/12"
         * @param {object} body   objektet som ska PUT:as
         */
        async koaAndring(typ, id, endpoint, body) {
            const s = await store(STORE_KO, "readwrite");
            await toPromise(s.put({
                key: paketNyckel(typ, id),
                typ,
                id: Number(id),
                endpoint,
                body,
                andrad: new Date().toISOString()
            }));
            return true;
        },

        async hamtaKoPost(typ, id) {
            const s = await store(STORE_KO, "readonly");
            return (await toPromise(s.get(paketNyckel(typ, id)))) || null;
        },

        async listaKo() {
            const s = await store(STORE_KO, "readonly");
            return (await toPromise(s.getAll())) || [];
        },

        async taBortKoPost(typ, id) {
            const s = await store(STORE_KO, "readwrite");
            await toPromise(s.delete(paketNyckel(typ, id)));
            return true;
        },

        async antalKoade() {
            return (await this.listaKo()).length;
        }
    };

    window.OfflineDb = OfflineDb;
})();
