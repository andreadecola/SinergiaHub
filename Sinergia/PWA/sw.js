// sw.js - Service Worker per Sinergia (senza notifiche push)

// Nome della cache che useremo
const CACHE_NAME = 'sinergia-cache-v1';

// URL da usare come fallback in caso di assenza di rete
const OFFLINE_URL = '/PWA/offline.html';

// Elenco delle risorse da salvare in cache durante l'installazione
const ASSETS_TO_CACHE = [
    '/',                             // homepage
    '/Content/css/Site.css',         // esempio: CSS dell'app (modifica con quello reale)
    //'/Scripts/site.js',              // esempio: JS dell'app (modifica con quello reale)
    OFFLINE_URL                      // pagina offline personalizzata
];

// 🔧 1. Evento install: viene eseguito alla prima registrazione o aggiornamento del SW
self.addEventListener('install', event => {
    event.waitUntil(
        // Apre (o crea) la cache e inserisce le risorse da cacheare
        caches.open(CACHE_NAME).then(cache => cache.addAll(ASSETS_TO_CACHE))
    );
});

// 🧹 2. Evento activate: pulisce vecchie versioni della cache non più usate
self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(
                keys
                    .filter(k => k !== CACHE_NAME) // filtra tutte le cache che NON sono quella attuale
                    .map(k => caches.delete(k))    // elimina le cache obsolete
            )
        )
    );
});

// 🌐 3. Evento fetch: intercetta tutte le richieste del browser
self.addEventListener('fetch', event => {
    // ignora richieste non GET (es. POST, PUT ecc.)
    if (event.request.method !== 'GET') return;
    const url = new URL(event.request.url);

    if (url.origin !== self.location.origin) {
        return fetch(event.request);
    }

    event.respondWith(
        // Tenta di scaricare la risorsa dalla rete
        fetch(event.request)
            .catch(() => caches.match(event.request))  // se la rete fallisce, prova la cache
            .then(response => response || caches.match(OFFLINE_URL)) // se non c'è nemmeno in cache, mostra offline.html
    );
});
