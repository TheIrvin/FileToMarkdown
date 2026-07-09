const fileInput = document.getElementById('fileInput');
const btnConvert = document.getElementById('btnConvert');
const statusText = document.getElementById('statusText');
const markdownOutput = document.getElementById('markdownOutput');
const historyList = document.getElementById('historyList');
const searchHistory = document.getElementById('searchHistory');

btnConvert.addEventListener('click', async () => {
    const file = fileInput.files[0];
    if (!file) {
        statusText.textContent = 'Selecciona un archivo primero.';
        return;
    }

    const formData = new FormData();
    formData.append('file', file);

    statusText.textContent = 'Convirtiendo...';
    btnConvert.disabled = true;

    try {
        const response = await fetch('/Conversion/Convert', { method: 'POST', body: formData });
        const result = await response.json();

        if (result.success) {
            markdownOutput.textContent = result.markdown;
            statusText.textContent = `Completado en ${result.conversionTimeSeconds.toFixed(2)}s`;
            loadHistory();
        } else {
            statusText.textContent = result.errorMessage || 'Error al convertir.';
        }
    } catch (err) {
        statusText.textContent = 'Error de red.';
    } finally {
        btnConvert.disabled = false;
    }
});

document.getElementById('btnCopy').addEventListener('click', () => {
    navigator.clipboard.writeText(markdownOutput.textContent);
});

document.getElementById('btnDownload').addEventListener('click', () => {
    const blob = new Blob([markdownOutput.textContent], { type: 'text/markdown' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = 'resultado.md';
    a.click();
    URL.revokeObjectURL(url);
});

document.getElementById('btnClear').addEventListener('click', () => {
    markdownOutput.textContent = '';
    fileInput.value = '';
    statusText.textContent = '';
});

async function loadHistory(search = '') {
    const response = await fetch(`/Conversion/History?search=${encodeURIComponent(search)}`);
    const items = await response.json();

    historyList.innerHTML = '';
    items.forEach(item => {
        const li = document.createElement('li');
        li.className = 'list-group-item list-group-item-action';
        li.style.cursor = 'pointer';
        li.textContent = item.fileName;
        li.addEventListener('click', () => openHistoryItem(item.id));
        historyList.appendChild(li);
    });
}

async function openHistoryItem(id) {
    const response = await fetch(`/Conversion/Item?id=${id}`);
    const result = await response.json();
    markdownOutput.textContent = result.markdown;
}

searchHistory.addEventListener('input', (e) => loadHistory(e.target.value));

loadHistory();
