// falt.js
// Logik för den fristående offline-fältytan (/falt).
// Hanterar BÅDE inventeringar och planteringar (delområden) helt offline och synkar
// via den delade modulen window.PlantAppOffline (offline-sync.js).
//
// Kartval:
//  - Inventering: klick i kartan sätter provytans plats.
//  - Plantering: klick på ett delområdes polygon väljer delområdet för rapportering.

(function () {
    "use strict";

    const $ = (id) => document.getElementById(id);
    const Db = () => window.OfflineDb;
    const Sync = () => window.PlantAppOffline;

    // --- DOM ---
    const onlineChip = $("online-chip");
    const synkChip = $("synk-chip");
    const offlineBanner = $("offline-banner");
    const valVy = $("val-vy");
    const invVy = $("inventering-vy");
    const plantVy = $("plant-vy");
    const offlineLista = $("offline-lista");
    const toastEl = $("toast");

    // --- Tillstånd ---
    let karta = null;
    let geoLager = null;
    let provytaMarkorer = null;
    let platsMarkor = null;
    let valdPlats = null;

    let aktivTyp = null;      // "inventering" | "trakt"
    let aktivtPaket = null;   // cachat paket som redigeras
    let aktivInvProvytor = [];   // inventering: arbetslista
    let valtDelOmradeId = null;  // plantering: valt delområde
    let toastTimer = null;

    // ---------- Hjälpare ----------
    function toast(text, ms = 2800) {
        toastEl.textContent = text;
        toastEl.classList.add("toast--visas");
        clearTimeout(toastTimer);
        toastTimer = setTimeout(() => toastEl.classList.remove("toast--visas"), ms);
    }

    function online() { return navigator.onLine; }

    function uppdateraOnlineChip() {
        if (online()) {
            onlineChip.textContent = "Online";
            onlineChip.className = "status-chip status-chip--online";
            offlineBanner.classList.add("dold");
        } else {
            onlineChip.textContent = "Offline";
            onlineChip.className = "status-chip status-chip--offline";
            offlineBanner.classList.remove("dold");
        }
    }

    async function uppdateraSynkChip() {
        const antal = await Db().antalKoade();
        if (antal > 0) {
            synkChip.textContent = `${antal} väntar på synk`;
            synkChip.classList.remove("dold");
        } else {
            synkChip.classList.add("dold");
        }
    }

    function visaVy(vy) {
        valVy.classList.toggle("dold", vy !== "start");
        invVy.classList.toggle("dold", vy !== "inventering");
        plantVy.classList.toggle("dold", vy !== "trakt");
        setTimeout(() => karta && karta.invalidateSize(), 150);
    }

    // ---------- Karta ----------
    function initKarta() {
        if (karta) return;
        karta = L.map("karta", { zoomControl: true }).setView([62.4, 14.0], 6);
        // Flytta +/- till höger så den inte skymmer panelen/listorna till vänster.
        karta.zoomControl.setPosition("topright");
        L.tileLayer(
            maxZoom: 19,
            attribution: "© OpenStreetMap"
        }).addTo(karta);

        karta.on("click", (e) => {
            // Endast i inventeringsläge (eller planteringsläge med valt delområde) sätter
            // kartklick en provyta-plats. Delområdesval sker via polygon-klick (nedan).
            if (aktivTyp === "inventering" && !invVy.classList.contains("dold")) {
                sättPlats(e.latlng, "inv-plats-hjälp");
            } else if (aktivTyp === "trakt" && valtDelOmradeId != null) {
                sättPlats(e.latlng, "do-plats-hjälp");
            }
        });
    }

    function sättPlats(latlng, hjälpId) {
        valdPlats = { lat: latlng.lat, lng: latlng.lng };
        if (platsMarkor) {
            platsMarkor.setLatLng(latlng);
        } else {
            platsMarkor = L.marker(latlng, { draggable: true }).addTo(karta);
            platsMarkor.on("dragend", () => {
                const p = platsMarkor.getLatLng();
                valdPlats = { lat: p.lat, lng: p.lng };
            });
        }
        $(hjälpId).textContent = `Plats vald: ${latlng.lat.toFixed(5)}, ${latlng.lng.toFixed(5)}`;
    }

    function rensaPlats(hjälpId) {
        valdPlats = null;
        if (platsMarkor) { karta.removeLayer(platsMarkor); platsMarkor = null; }
        if (hjälpId) $(hjälpId).textContent = "Tryck på kartan för att sätta provytans plats (valfritt).";
    }

    function rensaKartlager() {
        if (geoLager) { karta.removeLayer(geoLager); geoLager = null; }
        if (provytaMarkorer) { karta.removeLayer(provytaMarkorer); provytaMarkorer = null; }
        rensaPlats(null);
    }

    function parseGeo(paket) {
        if (!paket.traktGeoJson) return null;
        try {
            return typeof paket.traktGeoJson === "string" ? JSON.parse(paket.traktGeoJson) : paket.traktGeoJson;
        } catch { return null; }
    }

    // ---------- Startvy: offline-lista ----------
    async function renderaOfflineLista() {
        const paket = await Db().listaPaket();
        offlineLista.innerHTML = "";
        if (paket.length === 0) {
            offlineLista.innerHTML = '<li style="color:#6b7a70;">Inget nedladdat ännu. Tryck "Förbered offline".</li>';
            return;
        }
        paket
            .sort((a, b) => (a.typ + a.id).localeCompare(b.typ + b.id))
            .forEach((p) => {
                const li = document.createElement("li");

                const vänster = document.createElement("span");
                vänster.style.display = "flex";
                vänster.style.alignItems = "center";
                vänster.style.gap = "8px";
                const tagg = document.createElement("span");
                tagg.className = p.typ === "inventering" ? "tagg tagg--inv" : "tagg";
                tagg.textContent = p.typ === "inventering" ? "Inventering" : "Plantering";
                const namn = document.createElement("span");
                const antalUnderenheter = p.typ === "inventering"
                    ? `${(p.data?.provytor || []).length} provytor`
                    : `${Object.keys(p.data?.delomraden || {}).length} delområden`;
                namn.textContent = `#${p.id} · ${p.namn} (${antalUnderenheter})`;
                vänster.append(tagg, namn);

                const oppna = document.createElement("button");
                oppna.className = "btn-sekundär";
                oppna.style.minWidth = "auto";
                oppna.style.padding = "6px 12px";
                oppna.textContent = "Öppna";
                oppna.onclick = () => oppnaPaket(p.typ, p.id);

                li.append(vänster, oppna);
                offlineLista.appendChild(li);
            });
    }

    // ---------- Öppna paket ----------
    async function oppnaPaket(typ, id) {
        const paket = await Db().hamtaPaket(typ, id);
        if (!paket) { toast("Enheten är inte nedladdad."); return; }
        aktivTyp = typ;
        aktivtPaket = paket;
        initKarta();
        rensaKartlager();

        if (typ === "inventering") {
            await oppnaInventering(paket);
        } else {
            await oppnaTrakt(paket);
        }
    }

    // ---------- Inventering ----------
    async function oppnaInventering(paket) {
        // Använd köad version om den finns.
        const ko = await Db().hamtaKoPost("inventering", paket.id);
        aktivInvProvytor = ko ? (ko.body.provytor || []) : (paket.data?.provytor || []).slice();

        $("inv-titel").textContent = `#${paket.id} · ${paket.namn}`;
        ritaGeo(paket);
        ritaInvMarkorer();
        renderaInvProvytaLista();
        rensaPlats("inv-plats-hjälp");
        visaVy("inventering");
    }

    function ritaGeo(paket) {
        const geo = parseGeo(paket);
        if (geo) {
            geoLager = L.geoJSON(geo, { style: { color: "#1f4d2e", weight: 2, fillOpacity: 0.08 } }).addTo(karta);
            const b = geoLager.getBounds();
            if (b.isValid()) karta.fitBounds(b, { padding: [24, 24] });
        }
    }

    function ritaInvMarkorer() {
        if (provytaMarkorer) karta.removeLayer(provytaMarkorer);
        provytaMarkorer = L.layerGroup();
        aktivInvProvytor.forEach((p, i) => {
            if (p.latitud != null && p.longitud != null) {
                L.marker([p.latitud, p.longitud])
                    .bindPopup(`#${i + 1} · ${p.antalPlantor || 0} plantor` + (p.tradsort ? ` · ${p.tradsort}` : ""))
                    .addTo(provytaMarkorer);
            }
        });
        provytaMarkorer.addTo(karta);
    }

    function renderaInvProvytaLista() {
        const ul = $("inv-provyta-lista");
        ul.innerHTML = "";
        if (aktivInvProvytor.length === 0) {
            ul.innerHTML = '<li style="color:#6b7a70;">Inga provytor registrerade ännu.</li>';
            return;
        }
        aktivInvProvytor.forEach((p, i) => {
            const li = document.createElement("li");
            const info = document.createElement("span");
            const plats = (p.latitud != null && p.longitud != null) ? " 📍" : "";
            info.textContent = `#${i + 1} · ${p.antalPlantor || 0} plantor` + (p.tradsort ? ` · ${p.tradsort}` : "") + plats;
            const taBort = document.createElement("button");
            taBort.className = "btn-sekundär";
            taBort.style.minWidth = "auto";
            taBort.style.padding = "6px 10px";
            taBort.textContent = "✕";
            taBort.onclick = () => taBortInvProvyta(i);
            li.append(info, taBort);
            ul.appendChild(li);
        });
    }

    async function sparaInvProvyta() {
        const antal = parseInt($("inv-antal").value, 10);
        aktivInvProvytor.push({
            delOmradeId: null,
            antalPlantor: Number.isFinite(antal) ? antal : 0,
            tradsort: $("inv-tradsort").value || null,
            kommentar: $("inv-kommentar").value.trim() || null,
            latitud: valdPlats ? valdPlats.lat : null,
            longitud: valdPlats ? valdPlats.lng : null
        });
        await koaInventering();
        $("inv-antal").value = "0";
        $("inv-tradsort").value = "";
        $("inv-kommentar").value = "";
        rensaPlats("inv-plats-hjälp");
        renderaInvProvytaLista();
        ritaInvMarkorer();
        toast(online() ? "Registrerad – synkar…" : "Registrerad offline.");
        if (online()) körSynk();
    }

    async function taBortInvProvyta(index) {
        aktivInvProvytor.splice(index, 1);
        await koaInventering();
        renderaInvProvytaLista();
        ritaInvMarkorer();
        if (online()) körSynk();
    }

    async function koaInventering() {
        const body = {
            provytor: aktivInvProvytor.map((p) => ({
                delOmradeId: p.delOmradeId ?? null,
                antalPlantor: p.antalPlantor ?? 0,
                tradsort: p.tradsort ?? null,
                kommentar: p.kommentar ?? null,
                latitud: p.latitud ?? null,
                longitud: p.longitud ?? null
            }))
        };
        await Db().koaAndring("inventering", aktivtPaket.id, `/api/inventering/${aktivtPaket.id}`, body);
        await uppdateraSynkChip();
    }

    // ---------- Plantering (delområde) ----------
    async function oppnaTrakt(paket) {
        $("plant-titel").textContent = `#${paket.id} · ${paket.namn}`;
        valtDelOmradeId = null;
        $("delomrade-form").classList.add("dold");
        $("delomrade-val").classList.remove("dold");

        // Rita delområden och gör dem klickbara.
        const geo = parseGeo(paket);
        if (geo) {
            geoLager = L.geoJSON(geo, {
                style: (f) => stilFörStatus(delomradeStatus(paket, f.properties && f.properties.id)),
                onEachFeature: (f, layer) => {
                    const id = f.properties && f.properties.id;
                    if (id != null) {
                        layer.on("click", () => valjDelOmrade(Number(id)));
                    }
                }
            }).addTo(karta);
            const b = geoLager.getBounds();
            if (b.isValid()) karta.fitBounds(b, { padding: [24, 24] });
        }

        renderaDelOmradeLista();
        visaVy("trakt");
    }

    // Hämtar (ev. köad) status för ett delområde.
    function delomradeStatus(paket, id) {
        if (id == null) return 0;
        const d = (paket.data?.delomraden || {})[id];
        return d ? Number(d.status) : 0;
    }

    function stilFörStatus(status) {
        const färg = status === 2 ? "#2e6b41" : status === 1 ? "#b8860b" : "#8a94a6";
        return { color: färg, weight: 2, fillColor: färg, fillOpacity: 0.18 };
    }

    function delomradenSorterade() {
        const map = aktivtPaket.data?.delomraden || {};
        return Object.values(map).sort((a, b) => a.id - b.id);
    }

    function renderaDelOmradeLista() {
        const ul = $("delomrade-lista");
        ul.innerHTML = "";
        const delar = delomradenSorterade();
        if (delar.length === 0) {
            ul.innerHTML = '<li style="color:#6b7a70;">Inga delområden i denna trakt.</li>';
            return;
        }
        const statusText = ["Ej påbörjad", "Pågående", "Klar"];
        delar.forEach((d) => {
            const li = document.createElement("li");
            const info = document.createElement("span");
            info.textContent = `#${d.id} · ${statusText[Number(d.status)] || "?"} · ${d.antalPlantor ?? 0} plantor`
                + (d.kommentar ? " ⚠️" : "");
            const oppna = document.createElement("button");
            oppna.className = "btn-sekundär";
            oppna.style.minWidth = "auto";
            oppna.style.padding = "6px 12px";
            oppna.textContent = "Öppna";
            oppna.onclick = () => valjDelOmrade(d.id);
            li.append(info, oppna);
            ul.appendChild(li);
        });
    }

    function valjDelOmrade(id) {
        const d = (aktivtPaket.data?.delomraden || {})[id];
        if (!d) { toast("Delområdet är inte nedladdat."); return; }
        valtDelOmradeId = id;

        $("do-titel").textContent = `#${id}`;
        $("do-status").value = String(Number(d.status) || 0);
        $("do-antal").value = d.antalPlantor ?? 0;
        $("do-tradsort").value = d.tradsort || "";
        $("do-kommentar").value = d.kommentar || "";
        $("do-provyta-antal").value = "0";
        rensaPlats("do-plats-hjälp");
        renderaDoProvytaLista();

        $("delomrade-val").classList.add("dold");
        $("delomrade-form").classList.remove("dold");

        // Zooma till delområdet.
        if (geoLager) {
            geoLager.eachLayer((layer) => {
                const fid = layer.feature && layer.feature.properties && layer.feature.properties.id;
                if (Number(fid) === Number(id) && layer.getBounds) {
                    karta.fitBounds(layer.getBounds(), { padding: [32, 32] });
                }
            });
        }
    }

    function aktivtDelOmrade() {
        return (aktivtPaket.data?.delomraden || {})[valtDelOmradeId];
    }

    function renderaDoProvytaLista() {
        const ul = $("do-provyta-lista");
        ul.innerHTML = "";
        const d = aktivtDelOmrade();
        const provytor = (d && d.provytor) || [];
        if (provytor.length === 0) {
            ul.innerHTML = '<li style="color:#6b7a70;">Inga provytor ännu.</li>';
            return;
        }
        provytor.forEach((p, i) => {
            const li = document.createElement("li");
            const info = document.createElement("span");
            const plats = (p.latitud != null && p.longitud != null) ? " 📍" : "";
            info.textContent = `#${i + 1} · ${p.antalPlantor || 0} plantor` + plats;
            const taBort = document.createElement("button");
            taBort.className = "btn-sekundär";
            taBort.style.minWidth = "auto";
            taBort.style.padding = "6px 10px";
            taBort.textContent = "✕";
            taBort.onclick = () => { d.provytor.splice(i, 1); renderaDoProvytaLista(); };
            li.append(info, taBort);
            ul.appendChild(li);
        });
    }

    function laggTillDoProvyta() {
        const d = aktivtDelOmrade();
        if (!d) return;
        d.provytor = d.provytor || [];
        const antal = parseInt($("do-provyta-antal").value, 10);
        d.provytor.push({
            antalPlantor: Number.isFinite(antal) ? antal : 0,
            latitud: valdPlats ? valdPlats.lat : null,
            longitud: valdPlats ? valdPlats.lng : null
        });
        $("do-provyta-antal").value = "0";
        rensaPlats("do-plats-hjälp");
        renderaDoProvytaLista();
    }

    async function sparaDelOmrade() {
        const d = aktivtDelOmrade();
        if (!d) return;

        d.status = parseInt($("do-status").value, 10) || 0;
        const antal = parseInt($("do-antal").value, 10);
        d.antalPlantor = Number.isFinite(antal) ? antal : null;
        d.tradsort = $("do-tradsort").value || null;
        d.kommentar = $("do-kommentar").value.trim() || null;

        // Uppdatera cachat paket så listan/kartan speglar ändringen.
        await Db().sparaPaket(aktivtPaket);

        // Köa PUT mot delområdes-endpointen (DelOmradeUpdateDto).
        const body = {
            status: d.status,
            antalPlantor: d.antalPlantor,
            tradsort: d.tradsort,
            malPlantorPerHektar: d.malPlantorPerHektar ?? null,
            kommentar: d.kommentar,
            tilldeladAnvandare: d.tilldeladAnvandare ?? null,
            provytor: (d.provytor || []).map((p) => ({
                antalPlantor: p.antalPlantor ?? 0,
                latitud: p.latitud ?? null,
                longitud: p.longitud ?? null
            }))
        };
        await Db().koaAndring("trakt", d.id, `/api/trakt/delomrade/${d.id}`, body);
        await uppdateraSynkChip();

        // Uppdatera polygon-färg och lista.
        if (geoLager) {
            geoLager.eachLayer((layer) => {
                const fid = layer.feature && layer.feature.properties && layer.feature.properties.id;
                if (Number(fid) === Number(d.id) && layer.setStyle) {
                    layer.setStyle(stilFörStatus(Number(d.status)));
                }
            });
        }
        renderaDelOmradeLista();
        toast(online() ? "Sparat – synkar…" : "Sparat offline.");
        if (online()) körSynk();
    }

    // ---------- Synk ----------
    let synkarNu = false;
    async function körSynk(visaToast) {
        if (synkarNu) return;
        if (!online()) { if (visaToast) toast("Ingen täckning – synkar när nätet är tillbaka."); return; }
        synkarNu = true;
        try {
            const lyckade = await Sync().synka();
            await uppdateraSynkChip();
            if (lyckade > 0) {
                toast(`Synkade ${lyckade} ändring(ar).`);
                await renderaOfflineLista();
            } else if (visaToast) {
                toast("Inget att synka.");
            }
        } finally {
            synkarNu = false;
        }
    }

    // ---------- Förbered offline ----------
    async function förbered() {
        if (!online()) { toast("Ingen täckning – kan inte ladda ner nu."); return; }
        const knapp = $("forbered");
        knapp.disabled = true;
        const status = $("forbered-status");
        try {
            const res = await Sync().forberedAllt((klart, totalt, text) => {
                status.textContent = `Laddar ner ${klart}/${totalt}… ${text}`;
            });
            status.textContent = `Klart: ${res.trakter} trakter, ${res.inventeringar} inventeringar nedladdade`
                + (res.fel ? ` (${res.fel} fel).` : ".");
            await renderaOfflineLista();
            toast("Förberedd för offline.");
        } catch (err) {
            console.error(err);
            status.textContent = "Nedladdning misslyckades: " + (err.message || err);
        } finally {
            knapp.disabled = false;
        }
    }

    // ---------- Service worker ----------
    function registreraServiceWorker() {
        if (!("serviceWorker" in navigator)) return;
        navigator.serviceWorker.register("/service-worker.js").catch((err) => {
            console.warn("Service worker kunde inte registreras:", err);
        });
    }

    // ---------- Djuplänk (?inv= / ?trakt=) ----------
    async function hanteraQuery() {
        const params = new URLSearchParams(location.search);
        const inv = params.get("inv");
        const trakt = params.get("trakt");
        if (inv) {
            if (online() && !(await Db().hamtaPaket("inventering", inv))) {
                try { await Sync().laddaNerInventering(inv); await renderaOfflineLista(); } catch { /* */ }
            }
            if (await Db().hamtaPaket("inventering", inv)) oppnaPaket("inventering", Number(inv));
            else toast("Inventeringen är inte nedladdad – anslut och tryck Förbered offline.");
        } else if (trakt) {
            if (online() && !(await Db().hamtaPaket("trakt", trakt))) {
                try { await Sync().laddaNerTrakt(trakt); await renderaOfflineLista(); } catch { /* */ }
            }
            if (await Db().hamtaPaket("trakt", trakt)) oppnaPaket("trakt", Number(trakt));
            else toast("Trakten är inte nedladdad – anslut och tryck Förbered offline.");
        }
    }

    // ---------- Init ----------
    async function init() {
        registreraServiceWorker();
        initKarta();
        uppdateraOnlineChip();
        await renderaOfflineLista();
        await uppdateraSynkChip();

        $("forbered").addEventListener("click", förbered);
        $("synka").addEventListener("click", () => körSynk(true));
        $("inv-spara").addEventListener("click", sparaInvProvyta);
        $("do-spara").addEventListener("click", sparaDelOmrade);
        $("do-provyta-lagg").addEventListener("click", laggTillDoProvyta);
        $("do-stang").addEventListener("click", () => {
            valtDelOmradeId = null;
            $("delomrade-form").classList.add("dold");
            $("delomrade-val").classList.remove("dold");
            rensaPlats("do-plats-hjälp");
        });
        document.querySelectorAll(".tillbaka").forEach((b) => b.addEventListener("click", () => {
            aktivTyp = null; aktivtPaket = null; valtDelOmradeId = null;
            rensaKartlager();
            visaVy("start");
        }));

        window.addEventListener("online", () => { uppdateraOnlineChip(); körSynk(); });
        window.addEventListener("offline", uppdateraOnlineChip);

        await hanteraQuery();
    }

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", init);
    } else {
        init();
    }
})();
