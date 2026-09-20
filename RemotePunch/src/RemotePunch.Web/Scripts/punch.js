/* RemotePunch - client-side geo sensing.
 *
 * What happens here is only ever a preview and an aid: the server recomputes
 * the distance, the geofence verdict and the risk score from the raw
 * coordinates this file posts. Nothing below is a security control.
 */
(function () {
    'use strict';

    var cfg = window.RP_CONFIG || {};
    var QUEUE_KEY = 'rp.queue.v1';

    var state = {
        best: null,          // the best fix seen this attempt
        latest: null,        // the most recent fix, good or bad
        samples: 0,
        watchId: null,
        settleTimer: null,
        settled: false,
        busy: false,
        selfie: null,        // base64 JPEG
        stream: null,
        warnings: [],
        nextAction: cfg.nextAction || 'IN',
        map: null,
        youMarker: null,
        accuracyCircle: null
    };

    // ---------------------------------------------------------------- utils

    function $(id) { return document.getElementById(id); }

    function text(id, value) {
        var el = $(id);
        if (el) el.textContent = value;
    }

    function alertBox(kind, message, bullets) {
        var box = $('rpAlert');
        if (!box) return;
        box.className = 'alert alert-' + kind;
        var html = escapeHtml(message);
        if (bullets && bullets.length) {
            html += '<ul>';
            for (var i = 0; i < bullets.length; i++) html += '<li>' + escapeHtml(bullets[i]) + '</li>';
            html += '</ul>';
        }
        box.innerHTML = html;
    }

    function escapeHtml(value) {
        return String(value === null || value === undefined ? '' : value)
            .replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;').replace(/'/g, '&#39;');
    }

    function metres(value) {
        if (value === null || value === undefined) return '—';
        return value < 1000 ? Math.round(value) + ' m' : (value / 1000).toFixed(2) + ' km';
    }

    function uuid() {
        if (window.crypto && typeof window.crypto.randomUUID === 'function') return window.crypto.randomUUID();
        if (window.crypto && window.crypto.getRandomValues) {
            var bytes = new Uint8Array(16);
            window.crypto.getRandomValues(bytes);
            bytes[6] = (bytes[6] & 0x0f) | 0x40;
            bytes[8] = (bytes[8] & 0x3f) | 0x80;
            var hex = '';
            for (var i = 0; i < bytes.length; i++) hex += ('0' + bytes[i].toString(16)).slice(-2);
            return hex.slice(0, 8) + '-' + hex.slice(8, 12) + '-' + hex.slice(12, 16) + '-' +
                   hex.slice(16, 20) + '-' + hex.slice(20);
        }
        return 'rp-' + Date.now() + '-' + Math.random().toString(16).slice(2, 10);
    }

    /* Haversine, same formula as GeoMath.cs on the server. */
    function distanceMeters(lat1, lon1, lat2, lon2) {
        var toRad = Math.PI / 180;
        var dLat = (lat2 - lat1) * toRad;
        var dLon = (lon2 - lon1) * toRad;
        var a = Math.sin(dLat / 2) * Math.sin(dLat / 2) +
                Math.cos(lat1 * toRad) * Math.cos(lat2 * toRad) * Math.sin(dLon / 2) * Math.sin(dLon / 2);
        return 6371008.8 * 2 * Math.atan2(Math.sqrt(a), Math.sqrt(Math.max(0, 1 - a)));
    }

    function nearestSite(fix) {
        var sites = cfg.sites || [];
        var best = null;
        for (var i = 0; i < sites.length; i++) {
            var d = distanceMeters(fix.latitude, fix.longitude, sites[i].lat, sites[i].lng);
            if (!best || d < best.distance) best = { site: sites[i], distance: d };
        }
        if (best) best.inside = best.distance <= best.site.radius;
        return best;
    }

    // ------------------------------------------------- environment checks

    function runIntegrityChecks() {
        var warnings = [];

        if (!window.isSecureContext && location.hostname !== 'localhost') {
            warnings.push('INSECURE_CONTEXT');
        }

        // A mock-location extension or injected script replaces the native
        // function; the native one always stringifies with [native code].
        try {
            var source = Function.prototype.toString.call(navigator.geolocation.getCurrentPosition);
            if (source.indexOf('[native code]') < 0) warnings.push('GEOLOCATION_API_PATCHED');
        } catch (e) {
            warnings.push('GEOLOCATION_API_PATCHED');
        }

        if (navigator.webdriver) warnings.push('AUTOMATED_BROWSER');

        state.warnings = warnings;
        return warnings;
    }

    /* Stable-ish signals hashed into a device fingerprint on the server. */
    function deviceSignals() {
        var screenSize = '';
        try { screenSize = screen.width + 'x' + screen.height + 'x' + (screen.colorDepth || 0); } catch (e) { }

        var timeZone = '';
        try { timeZone = Intl.DateTimeFormat().resolvedOptions().timeZone || ''; } catch (e) { }

        return [
            navigator.platform || '',
            navigator.language || '',
            (navigator.languages || []).join('-'),
            screenSize,
            timeZone,
            String(new Date().getTimezoneOffset()),
            String(navigator.hardwareConcurrency || ''),
            String(navigator.deviceMemory || ''),
            String(navigator.maxTouchPoints || 0)
        ].join('|');
    }

    function deviceLabel() {
        var ua = navigator.userAgent || '';
        var os = 'Unknown OS';
        if (/Android/i.test(ua)) os = 'Android';
        else if (/iPhone|iPad|iPod/i.test(ua)) os = 'iOS';
        else if (/Windows/i.test(ua)) os = 'Windows';
        else if (/Mac OS X/i.test(ua)) os = 'macOS';
        else if (/Linux/i.test(ua)) os = 'Linux';

        var browser = 'Browser';
        if (/Edg\//i.test(ua)) browser = 'Edge';
        else if (/Chrome\//i.test(ua) && !/Edg\//i.test(ua)) browser = 'Chrome';
        else if (/Firefox\//i.test(ua)) browser = 'Firefox';
        else if (/Safari\//i.test(ua) && !/Chrome\//i.test(ua)) browser = 'Safari';

        return os + ' / ' + browser;
    }

    // ------------------------------------------------------------ geo loop

    function startWatching(manual) {
        if (!navigator.geolocation) {
            alertBox('bad', 'This browser cannot report a location, so punching is not possible here.');
            return;
        }

        stopWatching();
        state.best = null;
        state.latest = null;
        state.samples = 0;
        state.settled = false;
        text('roSamples', '0');
        setButton(false, 'Getting your location…');
        if (manual) alertBox('info', 'Refreshing your location…');

        state.watchId = navigator.geolocation.watchPosition(onPosition, onPositionError, {
            enableHighAccuracy: true,
            maximumAge: 0,
            timeout: 20000
        });

        state.settleTimer = window.setTimeout(settle, cfg.settleMs || 12000);
    }

    function stopWatching() {
        if (state.watchId !== null && navigator.geolocation) {
            navigator.geolocation.clearWatch(state.watchId);
            state.watchId = null;
        }
        if (state.settleTimer) {
            window.clearTimeout(state.settleTimer);
            state.settleTimer = null;
        }
    }

    function onPosition(position) {
        var c = position.coords;
        var fix = {
            latitude: c.latitude,
            longitude: c.longitude,
            accuracy: (typeof c.accuracy === 'number' && isFinite(c.accuracy)) ? c.accuracy : null,
            altitude: (typeof c.altitude === 'number' && isFinite(c.altitude)) ? c.altitude : null,
            altitudeAccuracy: (typeof c.altitudeAccuracy === 'number' && isFinite(c.altitudeAccuracy))
                ? c.altitudeAccuracy : null,
            speed: (typeof c.speed === 'number' && isFinite(c.speed)) ? c.speed : null,
            heading: (typeof c.heading === 'number' && isFinite(c.heading)) ? c.heading : null,
            timestamp: position.timestamp,
            receivedAt: Date.now()
        };

        state.samples++;
        state.latest = fix;
        text('roSamples', String(state.samples));

        // Keep the most accurate reading of this attempt, not merely the last.
        if (!state.best || (fix.accuracy !== null &&
            (state.best.accuracy === null || fix.accuracy < state.best.accuracy))) {
            state.best = fix;
        }

        render(state.best);

        var goodEnough = cfg.goodEnoughMeters || 25;
        if (state.best.accuracy !== null && state.best.accuracy <= goodEnough) settle();
    }

    function onPositionError(error) {
        stopWatching();
        setButton(false, 'Location unavailable');

        var message;
        switch (error.code) {
            case 1:
                message = 'Location permission is blocked. Allow location for this site in your browser ' +
                          'settings, then reload the page.';
                break;
            case 2:
                message = 'Your device could not determine a position. Move into the open, check that GPS ' +
                          'is on, and try again.';
                break;
            case 3:
                message = 'Getting a location took too long. Try again from a spot with a clearer view of the sky.';
                break;
            default:
                message = 'Location failed: ' + (error.message || 'unknown error');
        }
        alertBox('bad', message);
    }

    function settle() {
        if (state.settled) return;
        state.settled = true;
        stopWatching();

        if (!state.best) {
            setButton(false, 'No location yet');
            alertBox('warn', 'No usable location arrived. Press "Refresh location" to try again.');
            return;
        }
        render(state.best);
        evaluateReadiness(state.best);
    }

    function evaluateReadiness(fix) {
        var maxAccuracy = cfg.maxAccuracyMeters || 150;
        if (fix.accuracy !== null && fix.accuracy > maxAccuracy) {
            setButton(false, 'Accuracy too low to punch');
            alertBox('warn', 'Your location is only accurate to ' + metres(fix.accuracy) + '. ' +
                'Punching needs ' + metres(maxAccuracy) + ' or better - move outdoors and refresh.');
            return;
        }

        var near = nearestSite(fix);
        setButton(true, (state.nextAction === 'IN' ? 'Punch IN' : 'Punch OUT'));

        if (!near) {
            alertBox('warn', 'No site is assigned to you yet, so this punch will be sent for review.');
        } else if (near.inside) {
            alertBox('ok', 'You are inside ' + near.site.name + ' (' + metres(near.distance) + ' from its centre).');
        } else if (cfg.allowRemotePunch) {
            alertBox('warn', 'You are ' + metres(near.distance) + ' from ' + near.site.name +
                '. Remote punching is allowed on your account, but add a note - this punch goes for review.');
        } else {
            alertBox('bad', 'You are ' + metres(near.distance) + ' from ' + near.site.name +
                '. Punching is only allowed at your assigned site.');
        }
    }

    function setButton(enabled, label) {
        var button = $('btnPunch');
        if (!button) return;
        button.disabled = !enabled || state.busy;
        button.textContent = label;
    }

    // -------------------------------------------------------------- render

    function render(fix) {
        if (!fix) return;

        text('roCoords', fix.latitude.toFixed(6) + ', ' + fix.longitude.toFixed(6));
        text('roAccuracy', fix.accuracy === null ? 'not reported' : '±' + metres(fix.accuracy));
        text('roAltitude', fix.altitude === null ? 'not reported' :
            Math.round(fix.altitude) + ' m' + (fix.altitudeAccuracy !== null ?
                ' (±' + Math.round(fix.altitudeAccuracy) + ' m)' : ''));

        var motion = [];
        motion.push(fix.speed === null ? 'speed n/a' : (fix.speed * 3.6).toFixed(1) + ' km/h');
        motion.push(fix.heading === null ? 'heading n/a' : Math.round(fix.heading) + '°');
        text('roMotion', motion.join(' · '));

        var ageSeconds = Math.max(0, Math.round((Date.now() - fix.receivedAt) / 1000));
        text('roAge', ageSeconds + ' s');

        var near = nearestSite(fix);
        text('roSite', near ? near.site.name : 'none assigned');
        text('roDistance', near ? metres(near.distance) + (near.inside ? ' (inside)' : ' (outside)') : '—');

        renderAccuracyBar(fix.accuracy);
        renderMap(fix, near);
    }

    function renderAccuracyBar(accuracy) {
        var bar = $('accuracyBar');
        if (!bar) return;

        var max = cfg.maxAccuracyMeters || 150;
        var warn = cfg.warnAccuracyMeters || 60;
        var fill = bar.firstElementChild;

        if (accuracy === null) {
            bar.className = 'accuracy-bar bad';
            if (fill) fill.style.width = '100%';
            return;
        }
        // Full bar = a perfect fix, empty = at or beyond the rejection limit.
        var ratio = Math.max(0, Math.min(1, 1 - (accuracy / max)));
        bar.className = 'accuracy-bar' + (accuracy > max ? ' bad' : (accuracy > warn ? ' warn' : ''));
        if (fill) fill.style.width = (ratio * 100).toFixed(0) + '%';
    }

    function renderMap(fix, near) {
        if (!window.L) {
            var holder = $('map');
            if (holder) holder.style.display = 'none';
            return;
        }

        if (!state.map) {
            state.map = L.map('map', { zoomControl: true, attributionControl: true })
                         .setView([fix.latitude, fix.longitude], 16);
            L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
                maxZoom: 19,
                attribution: '&copy; OpenStreetMap contributors'
            }).addTo(state.map);

            var sites = cfg.sites || [];
            for (var i = 0; i < sites.length; i++) {
                L.circle([sites[i].lat, sites[i].lng], {
                    radius: sites[i].radius,
                    color: '#1e6fd9', weight: 1, fillColor: '#1e6fd9', fillOpacity: .12
                }).addTo(state.map).bindPopup(escapeHtml(sites[i].name) + '<br>radius ' + sites[i].radius + ' m');
            }
        }

        var position = [fix.latitude, fix.longitude];
        if (!state.youMarker) {
            state.youMarker = L.marker(position).addTo(state.map).bindPopup('You are here');
            state.accuracyCircle = L.circle(position, {
                radius: fix.accuracy || 20,
                color: '#12855a', weight: 1, fillColor: '#12855a', fillOpacity: .12
            }).addTo(state.map);
        } else {
            state.youMarker.setLatLng(position);
            state.accuracyCircle.setLatLng(position).setRadius(fix.accuracy || 20);
        }

        if (near && !near.inside) {
            state.map.fitBounds(L.latLngBounds([position, [near.site.lat, near.site.lng]]).pad(0.25));
        } else {
            state.map.setView(position, Math.max(state.map.getZoom(), 16));
        }
    }

    // -------------------------------------------------------------- selfie

    function startCamera() {
        if (!navigator.mediaDevices || !navigator.mediaDevices.getUserMedia) {
            alertBox('bad', 'This browser cannot open the camera.');
            return;
        }
        navigator.mediaDevices.getUserMedia({ video: { facingMode: 'user', width: 640 }, audio: false })
            .then(function (stream) {
                state.stream = stream;
                var video = $('selfieVideo');
                video.srcObject = stream;
                video.style.display = '';
                $('selfiePreview').style.display = 'none';
                $('btnCapture').disabled = false;
                $('btnCamera').disabled = true;
            })
            .catch(function () {
                alertBox('bad', 'Camera permission was refused, and this account requires a photo to punch.');
            });
    }

    function capturePhoto() {
        var video = $('selfieVideo');
        if (!video || !video.videoWidth) return;

        var width = Math.min(640, video.videoWidth);
        var height = Math.round(video.videoHeight * (width / video.videoWidth));

        var canvas = document.createElement('canvas');
        canvas.width = width;
        canvas.height = height;
        canvas.getContext('2d').drawImage(video, 0, 0, width, height);

        state.selfie = canvas.toDataURL('image/jpeg', 0.78);

        var preview = $('selfiePreview');
        preview.src = state.selfie;
        preview.style.display = '';
        video.style.display = 'none';
        $('btnCapture').disabled = true;
        $('btnRetake').style.display = '';
        stopCamera();
    }

    function stopCamera() {
        if (!state.stream) return;
        var tracks = state.stream.getTracks();
        for (var i = 0; i < tracks.length; i++) tracks[i].stop();
        state.stream = null;
    }

    function retakePhoto() {
        state.selfie = null;
        $('selfiePreview').style.display = 'none';
        $('btnRetake').style.display = 'none';
        $('btnCamera').disabled = false;
        $('selfieVideo').style.display = '';
        startCamera();
    }

    // --------------------------------------------------------------- punch

    function buildPayload() {
        var fix = state.best;
        return {
            requestedType: state.nextAction,
            clientRequestId: uuid(),
            note: ($('noteBox') ? $('noteBox').value : '').substring(0, 500),
            selfieBase64: state.selfie,
            deviceSignals: deviceSignals(),
            deviceLabel: deviceLabel(),
            clientWarnings: state.warnings,
            fix: {
                latitude: fix.latitude,
                longitude: fix.longitude,
                accuracy: fix.accuracy,
                altitude: fix.altitude,
                altitudeAccuracy: fix.altitudeAccuracy,
                speed: fix.speed,
                heading: fix.heading,
                timestamp: fix.timestamp,
                ageSeconds: Math.max(0, (Date.now() - fix.receivedAt) / 1000),
                sampleCount: state.samples
            }
        };
    }

    function punch() {
        if (state.busy) return;
        if (!state.best) {
            alertBox('warn', 'No location yet - press "Refresh location".');
            return;
        }

        // A fix that has aged past the server's limit would be rejected as
        // cached, so take a fresh one instead of posting a doomed punch.
        var ageSeconds = (Date.now() - state.best.receivedAt) / 1000;
        var maxAge = cfg.maxPositionAgeSeconds || 120;
        if (ageSeconds > maxAge * 0.75) {
            alertBox('info', 'Your location reading has gone stale. Getting a fresh one - try again in a moment.');
            startWatching(true);
            return;
        }

        var near = nearestSite(state.best);
        var note = $('noteBox') ? $('noteBox').value.trim() : '';
        if (near && !near.inside && cfg.allowRemotePunch && note.length < 5) {
            alertBox('warn', 'You are outside your site, so please add a short note explaining where you are.');
            $('noteBox').focus();
            return;
        }
        if (cfg.selfieRequired && !state.selfie) {
            alertBox('warn', 'Capture a photo before punching - it is required on this account.');
            return;
        }

        state.busy = true;
        setButton(false, 'Sending…');
        send(buildPayload(), false);
    }

    function send(payload, fromQueue) {
        var request = new XMLHttpRequest();
        request.open('POST', cfg.punchUrl, true);
        request.setRequestHeader('Content-Type', 'application/json');
        // Same-origin marker: a cross-site form post cannot set this header.
        request.setRequestHeader('X-RemotePunch', '1');
        request.timeout = 30000;

        request.onload = function () {
            state.busy = false;
            var result = null;
            try { result = JSON.parse(request.responseText); } catch (e) { }

            if (!result) {
                handleTransportFailure(payload, fromQueue, 'The server sent an unexpected response.');
                return;
            }
            removeFromQueue(payload.clientRequestId);
            applyResult(result);
        };

        request.onerror = function () {
            state.busy = false;
            handleTransportFailure(payload, fromQueue, 'Network error.');
        };
        request.ontimeout = function () {
            state.busy = false;
            handleTransportFailure(payload, fromQueue, 'The request timed out.');
        };

        request.send(JSON.stringify(payload));
    }

    function applyResult(result) {
        var bullets = result.flags || [];

        if (result.status === 'Accepted') {
            alertBox('ok', result.message, bullets);
        } else if (result.status === 'Flagged') {
            alertBox('warn', result.message, bullets);
        } else {
            alertBox('bad', result.message, bullets);
        }

        if (result.nextAction) state.nextAction = result.nextAction;
        state.selfie = null;
        if ($('selfiePreview')) $('selfiePreview').style.display = 'none';
        if ($('btnRetake')) $('btnRetake').style.display = 'none';
        if ($('btnCamera')) $('btnCamera').disabled = false;
        if ($('noteBox') && result.ok) $('noteBox').value = '';

        setButton(true, state.nextAction === 'IN' ? 'Punch IN' : 'Punch OUT');

        // The page shows today's punches server-side, so reload once the
        // record exists - but only after the reader has seen the message.
        if (result.ok && !result.duplicate) {
            window.setTimeout(function () { window.location.reload(); }, 2500);
        }
    }

    // ------------------------------------------------------- offline queue

    function handleTransportFailure(payload, fromQueue, reason) {
        if (!fromQueue) addToQueue(payload);
        setButton(true, state.nextAction === 'IN' ? 'Punch IN' : 'Punch OUT');
        alertBox('warn', reason + ' Your punch has been saved on this device and will be sent ' +
            'automatically when the connection returns. Keep this page open.');
        renderQueueNotice();
    }

    function readQueue() {
        try { return JSON.parse(window.localStorage.getItem(QUEUE_KEY) || '[]'); } catch (e) { return []; }
    }

    function writeQueue(items) {
        try { window.localStorage.setItem(QUEUE_KEY, JSON.stringify(items)); } catch (e) { /* private mode */ }
    }

    function addToQueue(payload) {
        var queue = readQueue();
        queue.push({ payload: payload, queuedAt: Date.now() });
        // The idempotency key travels with the payload, so a replay that did in
        // fact reach the server returns the original punch instead of a copy.
        writeQueue(queue.slice(-20));
    }

    function removeFromQueue(requestId) {
        var queue = readQueue();
        var kept = [];
        for (var i = 0; i < queue.length; i++) {
            if (queue[i].payload && queue[i].payload.clientRequestId !== requestId) kept.push(queue[i]);
        }
        writeQueue(kept);
        renderQueueNotice();
    }

    function flushQueue() {
        var queue = readQueue();
        if (!queue.length) return;
        // One at a time: each send removes its own entry on success.
        send(queue[0].payload, true);
    }

    function renderQueueNotice() {
        var box = $('queueNotice');
        if (!box) return;
        var queue = readQueue();
        if (!queue.length) {
            box.style.display = 'none';
            return;
        }
        box.style.display = '';
        box.textContent = queue.length + ' punch(es) waiting to be sent from this device.';
    }

    // ----------------------------------------------------------------- init

    function init() {
        var warnings = runIntegrityChecks();
        if (warnings.indexOf('INSECURE_CONTEXT') >= 0) {
            alertBox('bad', 'This page is not on a secure (https) connection, so your browser will not ' +
                'share a location. Open the https address of this site.');
            return;
        }

        $('btnPunch').addEventListener('click', punch);
        $('btnRefreshFix').addEventListener('click', function () { startWatching(true); });

        if ($('btnCamera')) $('btnCamera').addEventListener('click', startCamera);
        if ($('btnCapture')) $('btnCapture').addEventListener('click', capturePhoto);
        if ($('btnRetake')) $('btnRetake').addEventListener('click', retakePhoto);

        window.addEventListener('online', flushQueue);
        window.addEventListener('beforeunload', stopCamera);

        renderQueueNotice();
        startWatching(false);
        if (navigator.onLine) window.setTimeout(flushQueue, 1500);

        // Keep the fix-age readout honest while the page sits open.
        window.setInterval(function () {
            if (state.best) {
                text('roAge', Math.max(0, Math.round((Date.now() - state.best.receivedAt) / 1000)) + ' s');
            }
        }, 1000);
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();
