/* Map picker for the admin site editor: click to set the geofence centre,
   drag nothing, and watch the radius preview follow the radius box. */
(function () {
    'use strict';

    var ids = window.RP_SITE || {};
    var latBox = document.getElementById(ids.latId);
    var lngBox = document.getElementById(ids.lngId);
    var radiusBox = document.getElementById(ids.radiusId);
    if (!latBox || !lngBox || !window.L) return;

    var DEFAULT = [28.6328, 77.2197];   // New Delhi, used until a centre is set

    var map = L.map('map').setView(readCentre() || DEFAULT, readCentre() ? 16 : 11);
    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
        maxZoom: 19,
        attribution: '&copy; OpenStreetMap contributors'
    }).addTo(map);

    var marker = null;
    var circle = null;

    function readCentre() {
        var lat = parseFloat(latBox.value);
        var lng = parseFloat(lngBox.value);
        if (isNaN(lat) || isNaN(lng)) return null;
        if (lat < -90 || lat > 90 || lng < -180 || lng > 180) return null;
        return [lat, lng];
    }

    function readRadius() {
        var radius = parseInt(radiusBox ? radiusBox.value : '150', 10);
        if (isNaN(radius) || radius < 25) radius = 150;
        return Math.min(radius, 100000);
    }

    function place(latlng, recentre) {
        latBox.value = latlng[0].toFixed(6);
        lngBox.value = latlng[1].toFixed(6);

        if (!marker) {
            marker = L.marker(latlng, { draggable: true }).addTo(map);
            marker.on('dragend', function () {
                var position = marker.getLatLng();
                place([position.lat, position.lng], false);
            });
            circle = L.circle(latlng, {
                radius: readRadius(),
                color: '#1e6fd9', weight: 1, fillColor: '#1e6fd9', fillOpacity: .15
            }).addTo(map);
        } else {
            marker.setLatLng(latlng);
            circle.setLatLng(latlng).setRadius(readRadius());
        }
        if (recentre) map.setView(latlng, Math.max(map.getZoom(), 16));
    }

    var initial = readCentre();
    if (initial) place(initial, true);

    map.on('click', function (e) { place([e.latlng.lat, e.latlng.lng], false); });

    if (radiusBox) {
        radiusBox.addEventListener('input', function () {
            if (circle) circle.setRadius(readRadius());
        });
    }

    var button = document.getElementById('btnUseMyLocation');
    if (button && navigator.geolocation) {
        button.addEventListener('click', function () {
            button.disabled = true;
            button.textContent = 'Locating…';
            navigator.geolocation.getCurrentPosition(function (position) {
                place([position.coords.latitude, position.coords.longitude], true);
                button.disabled = false;
                button.textContent = 'Use my current location';
            }, function () {
                button.disabled = false;
                button.textContent = 'Location unavailable';
            }, { enableHighAccuracy: true, timeout: 20000, maximumAge: 0 });
        });
    }
})();
