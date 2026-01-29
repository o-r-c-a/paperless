const apiBase = '/api'; // nginx will proxy this to the REST container

// --- State ---
// editingId: null (upload mode) or Guid (edit mode)
const state = { tags: new Set(), editingId: null };

function el(tag, className, text) {
    const n = document.createElement(tag);
    if (className) n.className = className;
    if (text !== undefined) n.textContent = text;
    return n;
}

// --- Tag Management ---

function renderChips() {
    const box = document.getElementById('tagChips');
    if (!box) return;
    box.innerHTML = '';
    for (const t of state.tags) {
        const chip = el('span', 'chip');
        chip.appendChild(document.createTextNode(t));

        const btn = el('button', null, '×');
        btn.setAttribute('aria-label', 'remove');
        btn.title = 'remove';
        btn.addEventListener('click', () => {
            state.tags.delete(t);
            renderChips();
        });

        chip.appendChild(btn);
        box.appendChild(chip);
    }
}

function addTagFromInput() {
    const input = document.getElementById('tagInput');
    if (!input) return;
    const raw = input.value.trim().toLowerCase();
    if (!raw) return;

    if (!state.tags.has(raw)) {
        state.tags.add(raw);
        renderChips();
    }
    input.value = '';
}

function wireTagInput() {
    const input = document.getElementById('tagInput');
    if (!input) return;

    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            addTagFromInput();
        }
    });

    input.addEventListener('change', addTagFromInput);
}

// --- UI Helpers ---

function setStatus(message, kind = 'muted') {
    const box = document.getElementById('createResult');
    if (!box) return;
    box.className = `status-box ${kind}`;
    box.textContent = message || '';
}

function setSearchHint(message) {
    const out = document.getElementById('searchResult');
    if (!out) return;
    out.className = 'search-results muted';
    out.textContent = message;
}

function formatDateTime(isoString) {
    if (!isoString) return '—';
    const date = new Date(isoString);
    // Returns format like "Oct 24, 2023, 10:45 AM"
    return date.toLocaleString(undefined, {
        year: 'numeric',
        month: 'short',
        day: 'numeric',
        hour: 'numeric',
        minute: '2-digit'
    });
}

// --- Edit Mode Logic ---

function enterEditMode(doc) {
    state.editingId = doc.id;

    // Update Header
    const header = document.getElementById('formHeader');
    if (header) header.textContent = 'Edit document';

    // Populate Name
    const nameInput = document.getElementById('name');
    if (nameInput) nameInput.value = doc.name || '';

    // Hide File Input
    const fileGroup = document.getElementById('fileGroup');
    const fileInput = document.getElementById('file');
    if (fileGroup) fileGroup.style.display = 'none';
    if (fileInput) fileInput.required = false; // Not required in edit mode

    // Change Submit Button
    const submitBtn = document.getElementById('submitBtn');
    if (submitBtn) submitBtn.textContent = 'Save';

    // Show Cancel Button
    const cancelBtn = document.getElementById('cancelEditBtn');
    if (cancelBtn) cancelBtn.style.display = 'inline-block';

    // Populate Tags
    state.tags.clear();
    if (Array.isArray(doc.tags)) {
        doc.tags.forEach(t => state.tags.add(t));
    }
    renderChips();

    // Scroll to form
    document.getElementById('createForm')?.scrollIntoView({ behavior: 'smooth' });
    setStatus(`Editing "${doc.name}"...`, 'muted');
}

function exitEditMode() {
    state.editingId = null;

    // Reset Header
    const header = document.getElementById('formHeader');
    if (header) header.textContent = 'Upload document';

    // Show File Input
    const fileGroup = document.getElementById('fileGroup');
    const fileInput = document.getElementById('file');
    if (fileGroup) fileGroup.style.display = 'block';
    if (fileInput) fileInput.required = true;

    // Reset Form Fields
    document.getElementById('createForm')?.reset();

    // Change Submit Button
    const submitBtn = document.getElementById('submitBtn');
    if (submitBtn) submitBtn.textContent = 'Upload';

    // Hide Cancel Button
    const cancelBtn = document.getElementById('cancelEditBtn');
    if (cancelBtn) cancelBtn.style.display = 'none';

    // Clear Tags
    state.tags.clear();
    renderChips();

    setStatus('', 'muted');
}

// --- Main Form Handler (Create or Update) ---

async function handleFormSubmit(e) {
    e.preventDefault();

    if (state.editingId) {
        await updateDocument(state.editingId);
    } else {
        await uploadDocument();
    }
}

async function uploadDocument() {
    const name = document.getElementById('name')?.value?.trim();
    const fileInput = document.getElementById('file');
    const file = fileInput?.files?.[0];

    if (!name || !file) {
        setStatus('Please provide a name and select a file.', 'muted');
        return;
    }

    setStatus('Uploading…', 'muted');

    const formData = new FormData();
    formData.append('name', name);
    formData.append('file', file);

    if (state?.tags?.size > 0) {
        for (const t of state.tags) {
            formData.append('tags', t);
        }
    }

    const res = await fetch(`${apiBase}/documents`, {
        method: 'POST',
        body: formData
    });

    if (!res.ok) {
        setStatus(`Upload failed (${res.status}). ${await res.text()}`, 'muted');
        return;
    }

    const created = await res.json();
    setStatus('Uploaded successfully.', 'muted');

    exitEditMode(); // Resets form
    await loadDocuments();
    if (created?.id) await viewDocumentById(created.id);
}

async function updateDocument(id) {
    const name = document.getElementById('name')?.value?.trim();

    if (!name) {
        setStatus('Name is required.', 'muted');
        return;
    }

    setStatus('Saving changes…', 'muted');

    // Create JSON payload for UpdateDocumentRequest
    const payload = {
        name: name,
        tags: Array.from(state.tags)
    };

    const res = await fetch(`${apiBase}/documents/${id}`, {
        method: 'PUT',
        headers: {
            'Content-Type': 'application/json'
        },
        body: JSON.stringify(payload)
    });

    if (!res.ok) {
        setStatus(`Update failed (${res.status}). ${await res.text()}`, 'muted');
        return;
    }

    setStatus('Saved successfully.', 'muted');
    exitEditMode();
    await loadDocuments();
    await viewDocumentById(id);
}

// --- List & Search ---

async function loadDocuments() {
    const res = await fetch(`${apiBase}/documents`);
    const data = res.ok ? await res.json() : [];
    const tbody = document.querySelector('#docsTable tbody');
    if (!tbody) return;

    tbody.innerHTML = '';
    const known = new Set();

    for (const d of data) {
        if (Array.isArray(d.tags)) {
            for (const t of d.tags) known.add(String(t).toLowerCase());
        }

        const tagsCell = Array.isArray(d.tags) && d.tags.length ? d.tags.join(', ') : '—';
        const tr = document.createElement('tr');

        const tdName = document.createElement('td');
        tdName.textContent = d.name ?? '';
        const tdType = document.createElement('td');
        tdType.textContent = d.contentType ?? '';
        const tdUploaded = document.createElement('td');
        tdUploaded.textContent = formatDateTime(d.uploadedAt);
        const tdTags = document.createElement('td');
        tdTags.textContent = tagsCell;

        const tdActions = document.createElement('td');
        const actions = el('div', 'table-actions');

        // View Button
        const viewBtn = el('button', 'secondary', 'View');
        viewBtn.classList.add('viewBtn');
        viewBtn.setAttribute('data-id', d.id);

        // Edit Button (New)
        const editBtn = el('button', 'secondary', 'Edit');
        editBtn.classList.add('editBtn');
        // Store the full object so we don't need to re-fetch on click
        editBtn.onclick = () => enterEditMode(d);

        // Delete Button
        const delBtn = el('button', 'secondary', 'Delete');
        delBtn.classList.add('deleteBtn');
        delBtn.setAttribute('data-id', d.id);

        actions.appendChild(viewBtn);
        actions.appendChild(editBtn); // Add to DOM
        actions.appendChild(delBtn);
        tdActions.appendChild(actions);

        tr.appendChild(tdName);
        tr.appendChild(tdType);
        tr.appendChild(tdUploaded);
        tr.appendChild(tdTags);
        tr.appendChild(tdActions);

        tbody.appendChild(tr);
    }

    const dl = document.getElementById('tagSuggestions');
    if (dl) {
        dl.innerHTML = '';
        [...known].sort().forEach(t => {
            const opt = document.createElement('option');
            opt.value = t;
            dl.appendChild(opt);
        });
    }

    // Attach listeners for View/Delete
    document.querySelectorAll('.deleteBtn').forEach(b => b.addEventListener('click', onDelete));
    document.querySelectorAll('.viewBtn').forEach(b => b.addEventListener('click', onView));
}

async function onDelete(e) {
    const id = e.target.getAttribute('data-id');
    if (!id) return;
    if (!confirm('Delete this document?')) return;

    const res = await fetch(`${apiBase}/documents/${id}`, { method: 'DELETE' });
    if (res.ok) {
        if (state.editingId === id) exitEditMode(); // Exit edit mode if deleting active doc
        await loadDocuments();
        const box = document.getElementById('detailsBox');
        if (box) {
            box.className = 'details-placeholder';
            box.textContent = 'Deleted.';
        }
    } else {
        alert(`Delete failed: ${res.status}`);
    }
}

async function onView(e) {
    const id = e.target.getAttribute('data-id');
    if (!id) return;
    await viewDocumentById(id);
}

async function viewDocumentById(id) {
    const box = document.getElementById('detailsBox');
    if (box) {
        box.className = 'details-placeholder';
        box.textContent = 'Loading…';
    }

    const res = await fetch(`${apiBase}/documents/${id}`);
    if (!res.ok) {
        if (box) box.textContent = `Error ${res.status}: ${await res.text()}`;
        return;
    }

    const doc = await res.json();
    renderDetails(doc);
}

function renderDetails(doc) {
    const box = document.getElementById('detailsBox');
    if (!box) return;
    box.innerHTML = '';
    box.className = '';

    if (!doc) {
        box.className = 'details-placeholder';
        box.textContent = 'No details available.';
        return;
    }

    const wrap = el('div', 'details-grid');
    const kvs = el('div', null);

    const addKV = (k, v) => {
        const row = el('div', 'kv');
        row.appendChild(el('div', 'k', k));
        row.appendChild(el('div', 'v', v ?? ''));
        kvs.appendChild(row);
    };

    addKV('Name', doc.name ?? '');
    addKV('Type', doc.contentType ?? '');
    addKV('Uploaded', formatDateTime(doc.uploadedAt));
    addKV('Tags', Array.isArray(doc.tags) && doc.tags.length ? doc.tags.join(', ') : '—');
    addKV('Document ID', doc.id ?? '');

    wrap.appendChild(kvs);

    const summary = doc.summary ?? doc.aiSummary ?? doc.generatedSummary;
    if (summary && String(summary).trim().length) {
        const s = el('div', 'details-summary');
        s.appendChild(el('div', 'k', 'Summary'));
        s.appendChild(el('div', 'summary-text', String(summary)));
        wrap.appendChild(s);
    }

    box.appendChild(wrap);
}

// --- Search Logic ---

function renderSearchResults(items) {
    const out = document.getElementById('searchResult');
    if (!out) return;

    out.innerHTML = '';
    out.className = 'search-results';

    if (!items || items.length === 0) {
        out.appendChild(el('div', 'muted', 'No matches found.'));
        return;
    }

    const grid = el('div', 'result-grid');

    for (const r of items) {
        const card = el('div', 'result-card');
        const top = el('div', 'result-top');
        const name = el('div', 'result-name', r.name ?? '(untitled)');
        const score = el('div', 'badge', `Score: ${typeof r.score === 'number' ? r.score.toFixed(3) : r.score ?? 'n/a'}`);

        top.appendChild(name);
        top.appendChild(score);

        const meta = el('div', 'result-meta');
        meta.appendChild(el('span', null, `ID: ${r.id}`));
        meta.appendChild(el('span', null, `Click to open details`));

        card.appendChild(top);
        card.appendChild(meta);
        card.addEventListener('click', async () => await viewDocumentById(r.id));
        grid.appendChild(card);
    }
    out.appendChild(grid);
}

async function searchDocuments(e) {
    e.preventDefault();
    const q = document.getElementById('searchQuery')?.value?.trim();
    if (!q) {
        setSearchHint('Enter a search term.');
        return;
    }
    setSearchHint('Searching…');
    const res = await fetch(`${apiBase}/search?query=${encodeURIComponent(q)}`);
    if (!res.ok) {
        setSearchHint(`Error ${res.status}: ${await res.text()}`);
        return;
    }
    const data = await res.json();
    renderSearchResults(data);
}

function clearSearch() {
    const q = document.getElementById('searchQuery');
    if (q) q.value = '';
    setSearchHint('Start typing to search your documents.');
}

// --- Initialization ---

document.getElementById('createForm')?.addEventListener('submit', handleFormSubmit);
document.getElementById('refreshBtn')?.addEventListener('click', loadDocuments);
document.getElementById('searchForm')?.addEventListener('submit', searchDocuments);
document.getElementById('searchClearBtn')?.addEventListener('click', clearSearch);
document.getElementById('cancelEditBtn')?.addEventListener('click', exitEditMode);

wireTagInput();
setStatus('', 'muted');
setSearchHint('Start typing to search your documents.');
loadDocuments().catch(console.error);