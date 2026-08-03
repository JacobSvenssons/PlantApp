// offline-sync.js
// Delad offline-modul som används av BÅDE Blazor-knappar (via IJSRuntime) och den
// fristående fältytan (/falt/). Exponeras som window.PlantAppOffline.
//
// Ansvar:
//  - forberedAllt(): ladda ner allt som behövs för fältarbete offline (alla trakter för
//    plantering + alla inventeringar), medan täckning finns.
//  - laddaNerTrakt(id) / laddaNerInventering(id): ladda ner en enskild enhet.
//  - synka(): PUT:a alla köade ändringar (generisk kö) mot servern.
//
// Kräver att offline-db.js har laddats först (window.OfflineDb).

(function () {
    "use strict";

    function omDb() {
        if (!window.OfflineDb) {
            throw new Error("OfflineDb saknas – ladda /js/offline-db.js först.");
        }
        return window.OfflineDb;
    }

    async function fetchJson(url) {
        const resp = await fetch(url, { credentials: "same-origin" });
        if (!resp.ok) {
            throw new Error(`${url} svarade ${resp.status}`);
        }
        return resp.json();
    }

    async function fetchText(url) {
        const resp = await fetch(url, { credentials: "same-origin" });
        if (!resp.ok) {
            throw new Error(`${url} svarade ${resp.status}`);
        }
        return resp.text();
    }

    function normProvytor(provytor) {
        return (provytor || []).map((p) => ({
            antalPlantor: p.antalPlantor ?? p.antal ?? 0,
            latitud: p.latitud ?? null,
            longitud: p.longitud ?? null
        }));
    }

    const PlantAppOffline = {

        // ---- Nedladdning: inventering ----

        /** Laddar ner en inventering (detalj + trakt-GeoJSON) för offline-bruk. */
        async laddaNerInventering(inventeringId) {
            const detail = await fetchJson(`/api/inventering/${inventeringId}`);
            let traktGeoJson = null;
            try {
                traktGeoJson = await fetchText(`/api/trakt/${detail.traktId}/geojson`);
            } catch { /* karta valfri */ }

            const provytor = (detail.provytor || []).map((p) => ({
                delOmradeId: p.delOmradeId ?? null,
                antalPlantor: p.antalPlantor ?? 0,
                tradsort: p.tradsort ?? null,
                kommentar: p.kommentar ?? null,
                latitud: p.latitud ?? null,
                longitud: p.longitud ?? null
            }));

            await omDb().sparaPaket({
                typ: "inventering",
                id: Number(inventeringId),
                namn: detail.traktNamn || `Trakt ${detail.traktId}`,
                traktId: detail.traktId,
                traktGeoJson,
                data: { provytor }
            });
            return true;
        },

        // ---- Nedladdning: trakt (plantering) ----

        /**
         * Laddar ner en trakt för plantering: GeoJSON för delområdena + varje delområdes
         * detalj (status, antal, trädsort, kommentar, provytor).
         */
        async laddaNerTrakt(traktId) {
            const traktGeoJson = await fetchText(`/api/trakt/${traktId}/geojson`);
            let namn = `Trakt ${traktId}`;

            // Plocka delområdes-id från GeoJSON-egenskaperna.
            let geo = null;
            const delOmradeIds = [];
            try {
                geo = JSON.parse(traktGeoJson);
                (geo.features || []).forEach((f) => {
                    const id = f.properties && f.properties.id;
                    if (id != null) delOmradeIds.push(Number(id));
                });
            } catch { /* ignore */ }

            const delomraden = {};
            for (const id of delOmradeIds) {
                try {
                    const detail = await fetchJson(`/api/trakt/delomrade/${id}`);
                    delomraden[id] = {
                        id: detail.id,
                        status: detail.status,
                        areal: detail.areal,
                        tradsort: detail.tradsort ?? null,
                        traktStandardTradsort: detail.traktStandardTradsort ?? null,
                        malPlantorPerHektar: detail.malPlantorPerHektar ?? null,
                        traktMalPlantorPerHektar: detail.traktMalPlantorPerHektar ?? null,
                        antalPlantor: detail.antalPlantor ?? null,
                        kommentar: detail.kommentar ?? null,
                        tilldeladAnvandare: detail.tilldeladAnvandare ?? null,
                        provytor: normProvytor(detail.provytor)
                    };
                } catch (err) {
                    console.warn(`Kunde inte ladda delområde ${id}:`, err);
                }
            }

            // Traktens namn försöker vi hämta från trakt-listan (valfritt).
            try {
                const trakter = await fetchJson(`/api/trakt`);
                const t = (trakter || []).find((x) => Number(x.id) === Number(traktId));
                if (t && t.namn) namn = t.namn;
            } catch { /* ignore */ }

            await omDb().sparaPaket({
                typ: "trakt",
                id: Number(traktId),
                namn,
                traktId: Number(traktId),
                traktGeoJson,
                data: { delomraden }
            });
            return true;
        },

        // ---- Förbered allt ----

        /**
         * Laddar ner allt som behövs för fältarbete offline. Anropar valfri
         * framstegs-callback med (klart, totalt, text).
         * @returns {object} { trakter, inventeringar, fel }
         */
        async forberedAllt(progress) {
            const rapport = (klart, totalt, text) => {
                if (typeof progress === "function") progress(klart, totalt, text);
            };

            let trakter = [];
            let inventeringar = [];
            try { trakter = await fetchJson(`/api/trakt`); } catch (e) { console.warn(e); }
            try { inventeringar = await fetchJson(`/api/inventering`); } catch (e) { console.warn(e); }

            const totalt = trakter.length + inventeringar.length;
            let klart = 0;
            let fel = 0;

            for (const t of trakter) {
                try {
                    await this.laddaNerTrakt(t.id);
                } catch (err) {
                    fel++;
                    console.warn(`Trakt ${t.id} kunde inte laddas ner:`, err);
                }
                klart++;
                rapport(klart, totalt, `Trakt ${t.namn || t.id}`);
            }

            for (const inv of inventeringar) {
                try {
                    await this.laddaNerInventering(inv.id);
                } catch (err) {
                    fel++;
                    console.warn(`Inventering ${inv.id} kunde inte laddas ner:`, err);
                }
                klart++;
                rapport(klart, totalt, `Inventering ${inv.id}`);
            }

            return { trakter: trakter.length, inventeringar: inventeringar.length, fel };
        },

        // ---- Synk ----

        /**
         * Synkar hela den utgående kön mot servern. Returnerar antal lyckade poster.
         * Körs bara online.
         */
        async synka() {
            if (!navigator.onLine) return 0;
            const db = omDb();
            const poster = await db.listaKo();
            let lyckade = 0;
            for (const post of poster) {
                try {
                    const resp = await fetch(post.endpoint, {
                        method: "PUT",
                        headers: { "Content-Type": "application/json" },
                        credentials: "same-origin",
                        body: JSON.stringify(post.body)
                    });
                    if (resp.ok) {
                        await db.taBortKoPost(post.typ, post.id);
                        lyckade++;
                    } else {
                        console.warn(`Synk misslyckades för ${post.key}: ${resp.status}`);
                    }
                } catch (err) {
                    console.warn(`Synk avbröts för ${post.key}:`, err);
                }
            }
            return lyckade;
        },

        async antalKoade() {
            return omDb().antalKoade();
        }
    };

    window.PlantAppOffline = PlantAppOffline;
})();
