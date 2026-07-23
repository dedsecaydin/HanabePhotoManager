(() => {
  const map = L.map('map', {
    center: [35, 105], zoom: 4, minZoom: 2, maxZoom: 19,
    zoomControl: true, scrollWheelZoom: true, wheelDebounceTime: 20,
    wheelPxPerZoomLevel: 90, zoomAnimation: true, fadeAnimation: true,
    markerZoomAnimation: true, inertia: true, inertiaDeceleration: 2600
  });
  L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
    maxZoom: 19, updateWhenZooming: false, keepBuffer: 4,
    attribution: '© OpenStreetMap contributors'
  }).addTo(map);

  const markerLayer = L.layerGroup().addTo(map);
  let selectionPin = null;
  let didFit = false;
  const markerById = new Map();

  map.on('click', e => {
    if (selectionPin) selectionPin.setLatLng(e.latlng);
    else selectionPin = L.circleMarker(e.latlng, {
      radius: 8, color: '#fff', weight: 3, fillColor: '#f43f5e', fillOpacity: 1
    }).addTo(map);
    window.chrome.webview.postMessage({ type: 'mapClick', latitude: e.latlng.lat, longitude: e.latlng.lng });
  });

  function setMarkers(markers) {
    markerLayer.clearLayers();
    markerById.clear();
    const bounds = [];
    for (const marker of markers || []) {
      const previews = Array.isArray(marker.PreviewUrls) ? marker.PreviewUrls : [];
      const cards = previews.slice(0, 3).map((url, index) =>
        `<span class="photo-card card-${index}" style="background-image:url('${url}')"></span>`).join('');
      const icon = L.divIcon({
        className: 'hanabe-marker-wrap',
        html: `<span class="photo-stack">${cards || '<span class="photo-fallback">照片</span>'}<b>${marker.Count}</b></span>`,
        iconSize: [92, 72], iconAnchor: [46, 36]
      });
      const leafletMarker = L.marker([marker.Latitude, marker.Longitude], { icon, keyboard: true })
        .on('click', event => {
          L.DomEvent.stopPropagation(event);
          window.chrome.webview.postMessage({ type: 'markerClick', markerId: marker.Id });
        })
        .addTo(markerLayer);
      markerById.set(marker.Id, leafletMarker);
      bounds.push([marker.Latitude, marker.Longitude]);
    }
    if (!didFit && bounds.length) {
      didFit = true;
      map.fitBounds(bounds, { padding: [54, 54], maxZoom: 12, animate: true });
    }
  }

  function showCluster(message) {
    const marker = markerById.get(message.markerId);
    if (!marker) return;
    const photos = (message.urls || []).map((url, index) =>
      `<button class="popup-photo" data-index="${index}"><img src="${url}" alt="地点照片 ${index + 1}"></button>`).join('');
    const more = message.total > message.urls.length ? `<p>显示前 ${message.urls.length} 张，共 ${message.total} 张</p>` : '';
    marker.bindPopup(`<section class="photo-popover"><header>这个地点的照片 <b>${message.total}</b></header><div class="photo-grid">${photos}</div>${more}</section>`, {
      className: 'hanabe-photo-popup', maxWidth: 560, minWidth: 320, maxHeight: 420, autoPanPadding: [36, 36]
    }).openPopup();
    requestAnimationFrame(() => document.querySelectorAll('.popup-photo').forEach(button => button.addEventListener('click', event => {
      event.preventDefault(); event.stopPropagation();
      window.chrome.webview.postMessage({ type: 'photoClick', markerId: message.markerId, index: Number(button.dataset.index) });
    })));
  }

  window.chrome.webview.addEventListener('message', e => {
    if (e.data?.type === 'setMarkers') setMarkers(e.data.markers);
    else if (e.data?.type === 'showCluster') showCluster(e.data);
  });
  new ResizeObserver(() => map.invalidateSize({ pan: false })).observe(document.querySelector('#map'));
})();
