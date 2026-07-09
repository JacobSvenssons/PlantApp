// Laddar ner en fil i webbläsaren från base64-innehåll som skickats från .NET.
export function downloadFile(filnamn, base64Innehall, mimeType) {
    const byteChars = atob(base64Innehall);
    const byteNumbers = new Array(byteChars.length);
    for (let i = 0; i < byteChars.length; i++) {
        byteNumbers[i] = byteChars.charCodeAt(i);
    }
    const blob = new Blob([new Uint8Array(byteNumbers)], { type: mimeType });

    const url = URL.createObjectURL(blob);
    const lank = document.createElement("a");
    lank.href = url;
    lank.download = filnamn;
    document.body.appendChild(lank);
    lank.click();
    document.body.removeChild(lank);
    URL.revokeObjectURL(url);
}
