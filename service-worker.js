/* Manifest version: +n+5EgiB */
// Offline support: on install all files of the app are stored in the browser cache, afterwards the
// game starts from the cache – also without internet. A new version is fetched in the background and
// used from the next start on (see https://aka.ms/blazor-offline-considerations).

self.importScripts('./service-worker-assets.js');
self.addEventListener('install', event => event.waitUntil(onInstall(event)));
self.addEventListener('activate', event => event.waitUntil(onActivate(event)));
self.addEventListener('fetch', event => event.respondWith(onFetch(event)));

const cacheNamePrefix = 'chip-royale-';
const cacheName = `${cacheNamePrefix}${self.assetsManifest.version}`;
const offlineAssetsInclude = [/\.dll$/, /\.pdb$/, /\.wasm/, /\.html/, /\.js$/, /\.json$/, /\.css$/, /\.woff2?$/, /\.png$/, /\.jpe?g$/, /\.gif$/, /\.ico$/, /\.blat$/, /\.dat$/, /\.webmanifest$/];
const offlineAssetsExclude = [/^service-worker\.js$/];

// Works in any folder (e.g. https://user.github.io/chip-royale/).
const baseUrl = new URL(self.registration.scope);
const manifestUrlList = self.assetsManifest.assets.map(asset => new URL(asset.url, baseUrl).href);

// index.html and appsettings.json are adjusted after the build (base href), so their hash cannot be checked.
const unhashed = /(^|\/)(index\.html|appsettings\.json)$/;

async function onInstall() {
    self.skipWaiting();
    const assetsRequests = self.assetsManifest.assets
        .filter(asset => offlineAssetsInclude.some(pattern => pattern.test(asset.url)))
        .filter(asset => !offlineAssetsExclude.some(pattern => pattern.test(asset.url)))
        .map(asset => new Request(new URL(asset.url, baseUrl), unhashed.test(asset.url)
            ? { cache: 'no-cache' }
            : { integrity: asset.hash, cache: 'no-cache' }));
    const cache = await caches.open(cacheName);
    await cache.addAll(assetsRequests);
}

async function onActivate() {
    const cacheKeys = await caches.keys();
    await Promise.all(cacheKeys
        .filter(key => key.startsWith(cacheNamePrefix) && key !== cacheName)
        .map(key => caches.delete(key)));
    await self.clients.claim();
}

async function onFetch(event) {
    let cachedResponse = null;
    if (event.request.method === 'GET') {
        // Page navigations (e.g. /chip-royale/shop) get index.html; the Blazor router does the rest.
        const shouldServeIndexHtml = event.request.mode === 'navigate'
            && !manifestUrlList.some(url => url === event.request.url);
        const request = shouldServeIndexHtml ? new URL('index.html', baseUrl).href : event.request;
        const cache = await caches.open(cacheName);
        cachedResponse = await cache.match(request, { ignoreSearch: shouldServeIndexHtml });
    }
    return cachedResponse || fetch(event.request);
}
