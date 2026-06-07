const CACHE_VERSION = 'v4';
const STATIC_CACHE = `static-${CACHE_VERSION}`;
const FONTS_CACHE = `fonts-${CACHE_VERSION}`;
const PAGES_CACHE = `pages-${CACHE_VERSION}`;
const ALL_CACHES = [STATIC_CACHE, FONTS_CACHE, PAGES_CACHE];

const STATIC_EXTENSIONS = ['.css', '.js', '.png', '.jpg', '.jpeg', '.gif', '.ico', '.woff', '.woff2', '.svg'];

// Tracks the last time any mutation (POST/PUT/DELETE) was observed.
// staleWhileRevalidate checks this so pages cached before a mutation are
// re-fetched on the next navigation rather than served stale.
let lastMutationTime = 0;

function isStaticAsset(url) {
    return STATIC_EXTENSIONS.some(ext => url.pathname.endsWith(ext));
}

function isGoogleFont(url) {
    return url.hostname === 'fonts.googleapis.com' || url.hostname === 'fonts.gstatic.com';
}

self.addEventListener('install', event => {
    event.waitUntil(
        caches.open(STATIC_CACHE).then(cache => cache.add('/offline.html'))
    );
    self.skipWaiting();
});

self.addEventListener('activate', event => {
    event.waitUntil(
        caches.keys().then(keys =>
            Promise.all(keys.filter(k => !ALL_CACHES.includes(k)).map(k => caches.delete(k)))
        ).then(() => self.clients.claim())
    );
});

async function cacheFirst(request, cacheName) {
    const cached = await caches.match(request);
    if (cached) return cached;
    const response = await fetch(request);
    if (response.ok) {
        const cache = await caches.open(cacheName);
        cache.put(request, response.clone());
    }
    return response;
}

async function staleWhileRevalidate(request, cacheName) {
    const cache = await caches.open(cacheName);
    const cached = await cache.match(request);

    // If the cached response predates the last mutation, bypass it and fetch fresh.
    if (cached && lastMutationTime > 0) {
        const cachedDate = new Date(cached.headers.get('date') || 0).getTime();
        if (cachedDate < lastMutationTime) {
            const response = await fetch(request);
            if (response.ok) cache.put(request, response.clone());
            return response;
        }
    }

    const networkPromise = fetch(request).then(response => {
        if (response.ok) cache.put(request, response.clone());
        return response;
    });
    return cached || networkPromise;
}

async function networkFirst(request, cacheName) {
    const cache = await caches.open(cacheName);
    try {
        const response = await fetch(request);
        if (response.ok) cache.put(request, response.clone());
        return response;
    } catch {
        const cached = await cache.match(request);
        if (cached) return cached;
        return caches.match('/offline.html');
    }
}

self.addEventListener('fetch', event => {
    const { request } = event;

    // Record mutation time synchronously so the next navigate sees it.
    if (request.method !== 'GET') {
        lastMutationTime = Date.now();
        return;
    }

    const url = new URL(request.url);
    const isSameOrigin = url.origin === self.location.origin;

    if (isGoogleFont(url)) {
        event.respondWith(staleWhileRevalidate(request, FONTS_CACHE));
        return;
    }

    if (!isSameOrigin) return;

    if (isStaticAsset(url)) {
        event.respondWith(cacheFirst(request, STATIC_CACHE));
        return;
    }

    if (request.mode === 'navigate') {
        event.respondWith(staleWhileRevalidate(request, PAGES_CACHE));
        return;
    }
});
