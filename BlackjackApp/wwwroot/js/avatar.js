// Helpers for the player icon. The icon is shrunk in the browser so it stays tiny
// (a few KB). Blazor Server and SignalR reject messages over ~32 KB by default,
// so a full-size image data URL passed through them kills the connection.
window.blackjackAvatar = (function () {
    const KEY = "blackjack.playerAvatar";
    const MAX_CHARS = 24000; // keep well under the 32 KB Blazor/SignalR message limit
    const SIZE = 128;

    // Returns the saved icon, or null if none / too large to send safely.
    function get() {
        try {
            const value = localStorage.getItem(KEY);
            if (!value) return null;
            if (value.length > MAX_CHARS) {
                // Left over from before icons were resized; unusable, so clear it.
                localStorage.removeItem(KEY);
                return null;
            }
            return value;
        } catch (e) {
            return null;
        }
    }

    function loadImage(url) {
        return new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = () => reject(new Error("Could not read image"));
            img.src = url;
        });
    }

    // Reads the file chosen in <input id=inputId>, center-crops to a square and
    // returns a small data URL (or null if it could not be made small enough).
    async function fromInput(inputId) {
        const input = document.getElementById(inputId);
        const file = input && input.files && input.files[0];
        if (!file) return null;

        const url = URL.createObjectURL(file);
        try {
            const img = await loadImage(url);
            const side = Math.min(img.naturalWidth, img.naturalHeight);
            if (!side) return null;
            const sx = (img.naturalWidth - side) / 2;
            const sy = (img.naturalHeight - side) / 2;

            let size = SIZE;
            let quality = 0.85;
            for (let attempt = 0; attempt < 4; attempt++) {
                const canvas = document.createElement("canvas");
                canvas.width = size;
                canvas.height = size;
                const ctx = canvas.getContext("2d");
                ctx.drawImage(img, sx, sy, side, side, 0, 0, size, size);

                let data = canvas.toDataURL("image/webp", quality);
                if (!data.startsWith("data:image/webp")) {
                    // Browser can't encode WebP (e.g. Safari): use JPEG on white.
                    ctx.globalCompositeOperation = "destination-over";
                    ctx.fillStyle = "#ffffff";
                    ctx.fillRect(0, 0, size, size);
                    data = canvas.toDataURL("image/jpeg", quality);
                }
                if (data.length <= MAX_CHARS) return data;

                size = Math.round(size * 0.75);
                quality = Math.max(0.5, quality - 0.15);
            }
            return null;
        } finally {
            URL.revokeObjectURL(url);
        }
    }

    return { get, fromInput };
})();
