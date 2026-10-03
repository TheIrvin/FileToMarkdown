const fileInput = document.getElementById('fileInput');
const btnImport = document.getElementById('btnImport');
const btnFirstRunImport = document.getElementById('btnFirstRunImport');
const firstRunPanel = document.getElementById('firstRunPanel');
const firstRunStatus = document.getElementById('firstRunStatus');
const workspaceLayout = document.getElementById('workspaceLayout');
const libraryToolbar = document.getElementById('libraryToolbar');
const supportedFormats = document.getElementById('supportedFormats');
const pageTitle = document.getElementById('pageTitle');
const pageDescription = document.getElementById('pageDescription');
const statusText = document.getElementById('statusText');
const importProgress = document.getElementById('importProgress');
const importResults = document.getElementById('importResults');
const markdownOutput = document.getElementById('markdownOutput');
const resultHeading = document.getElementById('resultHeading');
const detailEmpty = document.getElementById('detailEmpty');
const historyList = document.getElementById('historyList');
const historyEmpty = document.getElementById('historyEmpty');
const historyEmptyTitle = document.getElementById('historyEmptyTitle');
const historyEmptyText = document.getElementById('historyEmptyText');
const documentCount = document.getElementById('documentCount');
const documentTableHead = document.getElementById('documentTableHead');
const librarySearch = document.getElementById('librarySearch');
const libraryResultsPanel = document.getElementById('libraryResultsPanel');
const libraryResults = document.getElementById('libraryResults');
const librarySearchEmpty = document.getElementById('librarySearchEmpty');
const btnCopy = document.getElementById('btnCopy');
const btnDownload = document.getElementById('btnDownload');
const btnDeleteCurrent = document.getElementById('btnDeleteCurrent');
const askForm = document.getElementById('askForm');
const questionInput = document.getElementById('questionInput');
const btnAsk = document.getElementById('btnAsk');
const answerPanel = document.getElementById('answerPanel');

let currentFileName = '';
let currentConversionId = null;
let historyRequestId = 0;
let librarySearchRequestId = 0;
let detailRequestId = 0;
let isImporting = false;

function updateLibraryState(itemCount, search = '') {
    const isFirstRun = itemCount === 0 && !search.trim();
    firstRunPanel.hidden = !isFirstRun;
    workspaceLayout.hidden = isFirstRun;
    libraryToolbar.hidden = isFirstRun;
    supportedFormats.hidden = isFirstRun;
    const [titleLead, titleAccent] = isFirstRun
        ? ['Convierte y consulta', 'sin salir de tu equipo.']
        : ['Encuentra en tus archivos', 'lo que buscas.'];
    const titleBreak = document.createElement('br');
    const accent = document.createElement('span');
    accent.textContent = titleAccent;
    pageTitle.replaceChildren(titleLead, titleBreak, accent);
    pageDescription.textContent = isFirstRun
        ? 'Convierte tus archivos y guarda los resultados localmente en este equipo.'
        : 'Busca contenido y vuelve a tus archivos desde un espacio privado en este equipo.';
}

function setMarkdown(markdown, fileName = '', conversionId = null) {
    detailRequestId++;
    currentFileName = fileName;
    currentConversionId = conversionId;
    markdownOutput.textContent = markdown || '';
    const hasMarkdown = markdownOutput.textContent.length > 0;
    resultHeading.textContent = fileName || 'Selecciona un documento';
    markdownOutput.hidden = !hasMarkdown;
    detailEmpty.hidden = hasMarkdown;
    detailEmpty.textContent = fileName && !hasMarkdown
        ? 'Esta conversión no tiene Markdown disponible.'
        : 'El Markdown del documento seleccionado aparecerá aquí.';
    btnCopy.disabled = !hasMarkdown;
    btnDownload.disabled = !hasMarkdown;
    btnDeleteCurrent.disabled = !conversionId;
}

async function readResponse(response) {
    const body = await response.json().catch(() => ({}));
    if (!response.ok) throw new Error(body.error || 'No se pudo completar la solicitud.');
    return body;
}

function extensionOf(fileName) {
    return (fileName.split('.').pop() || '').toUpperCase();
}

function formatDate(value) {
    return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' });
}

function createHistoryRow(item) {
    const row = document.createElement('tr');
    const fileCell = document.createElement('td');
    const open = document.createElement('button');
    open.type = 'button';
    open.className = 'document-name-button';
    open.textContent = item.fileName;
    open.disabled = item.status !== 'Completed';
    open.setAttribute('aria-label', `Abrir vista previa de ${item.fileName}`);
    open.addEventListener('click', () => openHistoryItem(item.id));
    fileCell.appendChild(open);

    const typeCell = document.createElement('td');
    const typeBadge = document.createElement('span');
    typeBadge.className = 'file-type';
    typeBadge.textContent = extensionOf(item.fileName) || 'FILE';
    typeCell.appendChild(typeBadge);

    const dateCell = document.createElement('td');
    dateCell.className = 'document-date';
    dateCell.textContent = formatDate(item.createdAt);

    const stateCell = document.createElement('td');
    const state = document.createElement('span');
    state.className = `document-state ${item.status === 'Completed' ? 'is-complete' : 'is-failed'}`;
    state.textContent = item.status === 'Completed' ? 'Listo' : 'Error';
    stateCell.appendChild(state);

    const actionCell = document.createElement('td');
    actionCell.className = 'document-actions';
    const remove = document.createElement('button');
    remove.type = 'button';
    remove.className = 'icon-action';
    remove.textContent = 'Borrar';
    remove.setAttribute('aria-label', `Borrar ${item.fileName}`);
    remove.addEventListener('click', () => deleteHistoryItem(item.id, item.fileName));
    actionCell.appendChild(remove);
    row.append(fileCell, typeCell, dateCell, stateCell, actionCell);
    return row;
}

async function loadHistory(search = '') {
    const requestId = ++historyRequestId;
    try {
        const response = await fetch(`/Conversion/History?search=${encodeURIComponent(search)}`);
        const items = await readResponse(response);
        if (requestId !== historyRequestId) return;
        historyList.replaceChildren(...items.map(createHistoryRow));
        documentCount.textContent = items.length;
        documentTableHead.hidden = items.length === 0;
        historyEmpty.hidden = items.length > 0;
        historyEmptyTitle.textContent = search ? 'No hay coincidencias' : 'Tu biblioteca está vacía';
        historyEmptyText.textContent = search
            ? 'No hay archivos con ese nombre. Prueba otra palabra o borra la búsqueda; las coincidencias de contenido aparecen abajo.'
            : 'Importa un documento para convertirlo, buscar su contenido y consultarlo aquí.';
        updateLibraryState(items.length, search);
    } catch {
        if (requestId !== historyRequestId) return;
        updateLibraryState(1, search);
        historyEmpty.hidden = false;
        historyEmptyTitle.textContent = 'No se pudo cargar la biblioteca';
        historyEmptyText.textContent = 'Comprueba que la aplicación siga abierta e inténtalo de nuevo.';
    }
}

async function openHistoryItem(id) {
    const requestId = ++detailRequestId;
    statusText.textContent = 'Abriendo documento...';
    try {
        const response = await fetch(`/Conversion/Item?id=${encodeURIComponent(id)}`);
        const result = await readResponse(response);
        if (requestId !== detailRequestId) return;
        setMarkdown(result.markdown, result.fileName, result.conversionId);
        statusText.textContent = 'Vista previa actualizada.';
    } catch (error) {
        if (requestId !== detailRequestId) return;
        statusText.textContent = error.message;
    }
}

async function deleteHistoryItem(id, fileName) {
    if (!window.confirm(`¿Borrar ${fileName} y su Markdown de este equipo?`)) return;
    try {
        const response = await fetch(`/Conversion/Delete?id=${encodeURIComponent(id)}`, { method: 'POST' });
        await readResponse(response);
        if (currentConversionId === id) setMarkdown('');
        statusText.textContent = `${fileName} y su índice se borraron del equipo.`;
        await Promise.all([loadHistory(librarySearch.value), loadLibraryResults(librarySearch.value)]);
    } catch (error) {
        statusText.textContent = error.message || `No se pudo borrar ${fileName}.`;
    }
}

function renderEvidence(target, hit) {
    const item = document.createElement('li');
    item.className = 'list-group-item evidence-item';
    const open = document.createElement('button');
    open.type = 'button';
    open.className = 'evidence-open';
    open.textContent = `${hit.fileName} · ${hit.section}`;
    open.addEventListener('click', () => openHistoryItem(hit.conversionId));
    const excerpt = document.createElement('p');
    excerpt.className = 'evidence-excerpt mb-0 mt-1';
    excerpt.textContent = hit.content;
    item.append(open, excerpt);
    target.appendChild(item);
}

async function loadLibraryResults(query = '') {
    const requestId = ++librarySearchRequestId;
    if (!query.trim()) {
        libraryResultsPanel.hidden = true;
        libraryResults.replaceChildren();
        return;
    }
    libraryResultsPanel.hidden = false;
    try {
        const response = await fetch(`/Conversion/Search?query=${encodeURIComponent(query)}`);
        const items = await readResponse(response);
        if (requestId !== librarySearchRequestId) return;
        libraryResults.replaceChildren();
        items.forEach(hit => renderEvidence(libraryResults, hit));
        librarySearchEmpty.hidden = items.length > 0;
        librarySearchEmpty.textContent = 'No encontré fragmentos para esta búsqueda.';
    } catch {
        if (requestId !== librarySearchRequestId) return;
        librarySearchEmpty.hidden = false;
        librarySearchEmpty.textContent = 'No se pudo buscar en la biblioteca.';
    }
}

async function importFiles(files) {
    if (files.length === 0) return;
    if (isImporting) {
        statusText.textContent = 'Espera a que termine el lote actual antes de importar más archivos.';
        firstRunStatus.textContent = statusText.textContent;
        firstRunStatus.hidden = false;
        return;
    }
    if (files.length > 10) {
        statusText.textContent = 'Selecciona hasta 10 archivos por lote.';
        firstRunStatus.textContent = statusText.textContent;
        firstRunStatus.hidden = false;
        return;
    }

    isImporting = true;
    importProgress.hidden = false;
    importResults.replaceChildren();
    btnImport.disabled = true;
    btnFirstRunImport.disabled = true;
    let lastResult = null;
    firstRunStatus.textContent = `Importando ${files.length} ${files.length === 1 ? 'archivo' : 'archivos'}...`;
    firstRunStatus.hidden = false;

    for (const file of files) {
        const entry = document.createElement('li');
        entry.className = 'import-result';
        entry.textContent = `${file.name} · En espera`;
        importResults.appendChild(entry);
        entry.textContent = `${file.name} · Convirtiendo...`;
        try {
            const formData = new FormData();
            formData.append('file', file);
            const response = await fetch('/Conversion/Convert', { method: 'POST', body: formData });
            const result = await readResponse(response);
            if (!result.success) throw new Error(result.errorMessage || 'No se pudo convertir.');
            lastResult = result;
            entry.classList.add('is-success');
            entry.textContent = `${file.name} · Importado`;
        } catch (error) {
            entry.classList.add('is-error');
            entry.textContent = `${file.name} · ${error.message || 'No se pudo convertir.'}`;
        }
    }

    if (lastResult) {
        setMarkdown(lastResult.markdown, lastResult.fileName, lastResult.conversionId);
        statusText.textContent = 'Importación completada.';
    } else {
        statusText.textContent = 'No se pudo importar ningún archivo.';
    }
    await Promise.all([loadHistory(librarySearch.value), loadLibraryResults(librarySearch.value)]);
    firstRunStatus.textContent = statusText.textContent;
    firstRunStatus.hidden = !firstRunStatus.textContent;
    fileInput.value = '';
    isImporting = false;
    btnImport.disabled = false;
    btnFirstRunImport.disabled = false;
    if (lastResult)
        resultHeading.focus({ preventScroll: true });
    else if (!firstRunPanel.hidden)
        firstRunStatus.focus({ preventScroll: true });
    else
        statusText.focus({ preventScroll: true });
}

btnImport.addEventListener('click', () => fileInput.click());
btnFirstRunImport.addEventListener('click', () => fileInput.click());
fileInput.addEventListener('change', () => importFiles([...fileInput.files]));

firstRunPanel.addEventListener('dragover', event => {
    event.preventDefault();
    firstRunPanel.classList.add('is-dragging');
});
firstRunPanel.addEventListener('dragleave', event => {
    if (!firstRunPanel.contains(event.relatedTarget))
        firstRunPanel.classList.remove('is-dragging');
});
firstRunPanel.addEventListener('drop', event => {
    event.preventDefault();
    firstRunPanel.classList.remove('is-dragging');
    if (isImporting) {
        statusText.textContent = 'Espera a que termine el lote actual antes de importar más archivos.';
        firstRunStatus.textContent = statusText.textContent;
        firstRunStatus.hidden = false;
        return;
    }
    importFiles([...event.dataTransfer.files]);
});

btnCopy.addEventListener('click', async () => {
    try {
        await navigator.clipboard.writeText(markdownOutput.textContent);
        statusText.textContent = 'Markdown copiado.';
    } catch {
        statusText.textContent = 'El navegador no permitió copiar al portapapeles.';
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

btnDeleteCurrent.addEventListener('click', () => {
    if (currentConversionId) deleteHistoryItem(currentConversionId, currentFileName);
});

askForm.addEventListener('submit', async event => {
    event.preventDefault();
    btnAsk.disabled = true;
    btnAsk.textContent = 'Buscando...';
    answerPanel.hidden = false;
    answerPanel.replaceChildren();
    const loading = document.createElement('p');
    loading.className = 'text-muted small mb-0';
    loading.textContent = 'Buscando evidencia local...';
    answerPanel.appendChild(loading);
    try {
        const response = await fetch('/Conversion/Ask', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ question: questionInput.value })
        });
        const result = await readResponse(response);
        answerPanel.replaceChildren();
        const answer = document.createElement('p');
        answer.className = result.hasEvidence ? 'answer-text' : 'answer-text text-muted';
        answer.textContent = result.answer;
        answerPanel.appendChild(answer);
        if (result.citations?.length) {
            const citations = document.createElement('ul');
            citations.className = 'list-group list-group-flush';
            result.citations.forEach(hit => renderEvidence(citations, hit));
            answerPanel.appendChild(citations);
        }
    } catch {
        answerPanel.textContent = 'No se pudo consultar la biblioteca local.';
    } finally {
        btnAsk.disabled = false;
        btnAsk.textContent = 'Consultar';
    }
});

let searchTimer;
librarySearch.addEventListener('input', event => {
    historyRequestId++;
    librarySearchRequestId++;
    const query = event.target.value;
    historyList.replaceChildren();
    documentTableHead.hidden = true;
    historyEmpty.hidden = false;
    historyEmptyTitle.textContent = query.trim() ? 'Buscando...' : 'Cargando biblioteca...';
    historyEmptyText.textContent = query.trim() ? 'Actualizando coincidencias.' : 'Actualizando documentos guardados.';
    libraryResultsPanel.hidden = !query.trim();
    libraryResults.replaceChildren();
    librarySearchEmpty.hidden = !query.trim();
    librarySearchEmpty.textContent = 'Buscando fragmentos...';
    clearTimeout(searchTimer);
    searchTimer = setTimeout(() => {
        loadHistory(query);
        loadLibraryResults(query);
    }, 220);
});

loadHistory();
