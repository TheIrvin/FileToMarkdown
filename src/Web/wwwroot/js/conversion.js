const fileInput = document.getElementById('fileInput');
const btnConvert = document.getElementById('btnConvert');
const btnCopy = document.getElementById('btnCopy');
const btnDownload = document.getElementById('btnDownload');
const btnClear = document.getElementById('btnClear');
const statusText = document.getElementById('statusText');
const markdownOutput = document.getElementById('markdownOutput');
const resultHeading = document.getElementById('resultHeading');
const historyList = document.getElementById('historyList');
const historyEmpty = document.getElementById('historyEmpty');
const searchHistory = document.getElementById('searchHistory');

let currentFileName = '';

function setMarkdown(markdown, fileName = '') {
    markdownOutput.textContent = markdown || '';
    currentFileName = fileName;
    resultHeading.textContent = fileName ? `${fileName} → Markdown` : 'Markdown generado';
    const hasMarkdown = markdownOutput.textContent.length > 0;
    btnCopy.disabled = !hasMarkdown;
    btnDownload.disabled = !hasMarkdown;
}

async function readResponse(response) {
    const body = await response.json().catch(() => ({}));
    if (!response.ok) {
        throw new Error(body.error || 'No se pudo completar la solicitud.');
    }
    return body;
}

btnConvert.addEventListener('click', async () => {
    const file = fileInput.files[0];
    if (!file) {
        statusText.textContent = 'Selecciona un archivo primero.';
        fileInput.focus();
        return;
    }

    const formData = new FormData();
    formData.append('file', file);
    statusText.textContent = 'Convirtiendo...';
    btnConvert.disabled = true;

    try {
        const response = await fetch('/Conversion/Convert', { method: 'POST', body: formData });
        const result = await readResponse(response);
        if (!result.success) {
            throw new Error(result.errorMessage || 'No se pudo convertir el archivo.');
        }

        setMarkdown(result.markdown, result.fileName || file.name);
        statusText.textContent = `Listo en ${Number(result.conversionTimeSeconds).toFixed(2)} s.`;
        await loadHistory(searchHistory.value);
    } catch (error) {
        statusText.textContent = error.message || 'Error de conexión. Comprueba que la aplicación siga abierta.';
    } finally {
        btnConvert.disabled = false;
    }
});

btnCopy.addEventListener('click', async () => {
    try {
        await navigator.clipboard.writeText(markdownOutput.textContent);
        statusText.textContent = 'Markdown copiado al portapapeles.';
    } catch {
        statusText.textContent = 'No se pudo acceder al portapapeles en este navegador.';
    }
});

btnDownload.addEventListener('click', () => {
    const blob = new Blob([markdownOutput.textContent], { type: 'text/markdown;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    const baseName = (currentFileName || 'documento').replace(/\.[^.]+$/, '');
    const safeName = baseName.replace(/[\\/:*?"<>|\u0000-\u001f]/g, '_').trim() || 'documento';
    link.href = url;
    link.download = `${safeName}.md`;
    document.body.appendChild(link);
    link.click();
    link.remove();
    setTimeout(() => URL.revokeObjectURL(url), 1000);
    statusText.textContent = `Descarga iniciada: ${safeName}.md`;
});

btnClear.addEventListener('click', () => {
    setMarkdown('');
    fileInput.value = '';
    statusText.textContent = '';
    fileInput.focus();
});

async function loadHistory(search = '') {
    try {
        const response = await fetch(`/Conversion/History?search=${encodeURIComponent(search)}`);
        const items = await readResponse(response);
        historyList.replaceChildren();

        items.forEach(item => {
            const entry = document.createElement('li');
            entry.className = 'list-group-item px-0';

            const button = document.createElement('button');
            button.type = 'button';
            button.className = 'history-entry';
            button.disabled = item.status !== 'Completed';
            button.setAttribute('aria-label', `Abrir ${item.fileName}`);

            const name = document.createElement('span');
            name.className = 'history-file-name';
            name.textContent = item.fileName;

            const detail = document.createElement('span');
            detail.className = 'history-date';
            detail.textContent = item.status === 'Completed'
                ? new Date(item.createdAt).toLocaleString()
                : 'No se pudo convertir';

            button.append(name, detail);
            button.addEventListener('click', () => openHistoryItem(item.id));
            entry.appendChild(button);
            historyList.appendChild(entry);
        });

        historyEmpty.hidden = items.length > 0;
        historyEmpty.textContent = search ? 'No hay resultados para esa búsqueda.' : 'Aún no hay conversiones guardadas.';
    } catch {
        historyEmpty.hidden = false;
        historyEmpty.textContent = 'No se pudo cargar el historial.';
    }
}

async function openHistoryItem(id) {
    statusText.textContent = 'Abriendo conversión...';
    try {
        const response = await fetch(`/Conversion/Item?id=${encodeURIComponent(id)}`);
        const result = await readResponse(response);
        setMarkdown(result.markdown, result.fileName);
        statusText.textContent = result.markdown ? 'Conversión cargada desde el historial.' : 'Esta conversión no tiene Markdown disponible.';
    } catch (error) {
        statusText.textContent = error.message;
    }
}

let historySearchTimer;
searchHistory.addEventListener('input', event => {
    clearTimeout(historySearchTimer);
    historySearchTimer = setTimeout(() => loadHistory(event.target.value), 200);
});

fileInput.addEventListener('change', () => {
    if (fileInput.files[0]) {
        statusText.textContent = `${fileInput.files[0].name} seleccionado.`;
    }
});

loadHistory();
