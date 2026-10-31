import { api } from '../api.js';
import {
  escapeHtml, icon, toast, formatDate, SENSITIVITY, APPROVAL,
  truncate, setLoading, debounce, formatBytes
} from '../utils.js';

let allDocs = [];
let page = 1;
let pageSize = 12;
let totalCount = 0;
let totalPages = 0;
let filterSensitivity = -1;
let filterApproval = -1;
let searchTerm = '';

function getSensNum(val) {
  if (val === 0 || val === '0' || val === 'Public') return 0;
  if (val === 1 || val === '1' || val === 'Internal') return 1;
  if (val === 2 || val === '2' || val === 'Confidential') return 2;
  if (val === 3 || val === '3' || val === 'Restricted') return 3;
  return Number(val);
}

function getApprNum(val) {
  if (val === 0 || val === '0' || val === 'Pending') return 0;
  if (val === 1 || val === '1' || val === 'Approved') return 1;
  if (val === 2 || val === '2' || val === 'Rejected') return 2;
  return Number(val);
}

export function renderKnowledgeView(container, setTopbar) {
  setTopbar('Knowledge Base');
  container.innerHTML = `
    <div class="toolbar">
      <div class="toolbar-left">
        <div class="search-box">
          ${icon('search', 14)}
          <input type="text" id="docSearch" placeholder="Search documents…">
        </div>
        <select class="select" id="docSensFilter" style="width:auto">
          <option value="-1">All sensitivity</option>
          <option value="0">Public</option>
          <option value="1">Internal</option>
          <option value="2">Confidential</option>
          <option value="3">Restricted</option>
        </select>
        <select class="select" id="docApprovalFilter" style="width:auto">
          <option value="-1">All status</option>
          <option value="1">Approved</option>
          <option value="0">Pending</option>
          <option value="2">Rejected</option>
        </select>
        <button class="btn btn-ghost btn-sm" id="docRefreshBtn">
          ${icon('refresh', 13)} Refresh
        </button>
      </div>
      <div class="toolbar-right">
        <button class="btn btn-secondary" id="docUploadBtn">${icon('upload', 14)} Upload file</button>
        <button class="btn btn-primary" id="docAddBtn">${icon('plus', 14)} Add document</button>
      </div>
    </div>

    <div class="view-pad">
      <div class="card">
        <div class="card-header">
          <div class="card-title">${icon('library', 15)} Documents & chunks</div>
          <div class="badge badge-neutral" id="docCountBadge">0 total</div>
        </div>
        <div id="docListContainer"></div>
        <div id="docPagination"></div>
      </div>
    </div>
  `;

  const searchInput = container.querySelector('#docSearch');
  searchInput.addEventListener('input', debounce(() => {
    searchTerm = searchInput.value.trim().toLowerCase();
    page = 1;
    const c = document.getElementById('docListContainer');
    if (c) renderDocTable(c);
  }, 250));

  container.querySelector('#docSensFilter').addEventListener('change', (e) => {
    filterSensitivity = Number(e.target.value);
    page = 1;
    const c = document.getElementById('docListContainer');
    if (c) renderDocTable(c);
  });
  container.querySelector('#docApprovalFilter').addEventListener('change', (e) => {
    filterApproval = Number(e.target.value);
    page = 1;
    const c = document.getElementById('docListContainer');
    if (c) renderDocTable(c);
  });
  container.querySelector('#docRefreshBtn').addEventListener('click', () => loadDocs());
  container.querySelector('#docUploadBtn').addEventListener('click', () => openUploadModal());
  container.querySelector('#docAddBtn').addEventListener('click', () => openAddModal());

  loadDocs();
}

async function loadDocs() {
  const container = document.getElementById('docListContainer');
  if (!container) return;
  container.innerHTML = `<div class="skeleton-row">
    <div class="skeleton"></div><div class="skeleton" style="width:70%"></div><div class="skeleton" style="width:40%"></div>
  </div>`;

  try {
    const res = await api.getPagedDocuments(1, 200);
    allDocs = Array.isArray(res?.items) ? [...res.items] : [];
    renderDocTable(container);
  } catch (err) {
    container.innerHTML = `<div class="empty-state">
      ${icon('alert', 30)}
      <h3>Could not load documents</h3>
      <p>${escapeHtml(err.message || 'An error occurred.')}</p>
    </div>`;
  }
}

function renderDocTable(container) {
  let filtered = allDocs;

  if (searchTerm) {
    filtered = filtered.filter(d =>
      (d.title || '').toLowerCase().includes(searchTerm) ||
      (d.department || '').toLowerCase().includes(searchTerm));
  }

  if (filterSensitivity !== -1) {
    filtered = filtered.filter(d => getSensNum(d.sensitivityLevel) === filterSensitivity);
  }

  if (filterApproval !== -1) {
    filtered = filtered.filter(d => getApprNum(d.approvalStatus) === filterApproval);
  }

  totalCount = filtered.length;
  totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  if (page > totalPages) page = totalPages;

  const badge = document.getElementById('docCountBadge');
  if (badge) badge.textContent = `${totalCount} total`;

  renderPagination();

  if (!filtered.length) {
    container.innerHTML = `<div class="empty-state">
      ${icon('library', 30)}
      <h3>No documents found</h3>
      <p>No documents match the current search or filter criteria.</p>
      <button class="btn btn-primary" onclick="document.getElementById('docUploadBtn')?.click()">${icon('upload', 14)} Upload file</button>
    </div>`;
    return;
  }

  const start = (page - 1) * pageSize;
  const pagedItems = filtered.slice(start, start + pageSize);

  const rows = pagedItems.map(d => {
    const sens = SENSITIVITY[getSensNum(d.sensitivityLevel)] || SENSITIVITY[1];
    const appr = APPROVAL[getApprNum(d.approvalStatus)] || APPROVAL[0];
    return `<tr>
      <td>
        <div style="display:flex;align-items:center;gap:10px">
          <div style="width:34px;height:34px;border-radius:9px;background:var(--bg-3);display:flex;align-items:center;justify-content:center;color:var(--accent);flex-shrink:0">
            ${icon('file', 15)}
          </div>
          <div style="min-width:0">
            <div class="primary" style="max-width:320px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(d.title)}</div>
            <div class="mono" style="color:var(--text-3);font-size:11px">${escapeHtml(truncate(d.documentID, 20))}</div>
          </div>
        </div>
      </td>
      <td><span class="badge badge-neutral">${escapeHtml(d.department || '—')}</span></td>
      <td><span class="badge badge-${sens.tone}">${escapeHtml(sens.label)}</span></td>
      <td><span class="badge badge-${appr.tone}">${escapeHtml(appr.label)}</span></td>
      <td><span class="mono">${d.chunkCount ?? 0} chunks</span></td>
      <td>
        <div class="row-actions">
          <button class="icon-btn" data-view-doc="${d.documentID}" title="View details">${icon('eye', 14)}</button>
          <button class="icon-btn danger" data-del-doc="${d.documentID}" title="Delete">${icon('trash', 14)}</button>
        </div>
      </td>
    </tr>`;
  }).join('');

  container.innerHTML = `<div class="data-table-wrap">
    <table class="data-table">
      <thead><tr>
        <th>Document</th><th>Department</th><th>Sensitivity</th><th>Approval</th><th>Indexed</th><th style="text-align:right">Actions</th>
      </tr></thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;

  container.querySelectorAll('[data-view-doc]').forEach(btn => {
    btn.addEventListener('click', () => viewDocument(btn.dataset.viewDoc));
  });
  container.querySelectorAll('[data-del-doc]').forEach(btn => {
    btn.addEventListener('click', () => confirmDelete(btn.dataset.delDoc));
  });
}

function renderPagination() {
  const host = document.getElementById('docPagination');
  if (!host) return;
  if (totalPages <= 1) {
    host.innerHTML = '';
    return;
  }
  let controls = '';
  for (let p = 1; p <= totalPages; p++) {
    if (totalPages > 7 && p > 2 && p < totalPages - 1 && Math.abs(p - page) > 1) {
      if (controls.endsWith('…')) continue;
      controls += `<span style="color:var(--text-3);font-size:12px;padding:0 4px">…</span>`;
      continue;
    }
    controls += `<button class="page-btn ${p === page ? 'active' : ''}" data-page="${p}">${p}</button>`;
  }
  host.innerHTML = `<div class="pagination">
    <div class="pagination-info">Page ${page} of ${totalPages} · ${totalCount} documents</div>
    <div class="pagination-controls">
      <button class="page-btn" data-page="${page - 1}" ${page <= 1 ? 'disabled' : ''}>${icon('chevron', 12)}</button>
      ${controls}
      <button class="page-btn" data-page="${page + 1}" ${page >= totalPages ? 'disabled' : ''}>${icon('chevron', 12)}</button>
    </div>
  </div>`;
  host.querySelectorAll('.page-btn[data-page]').forEach(btn => {
    btn.addEventListener('click', () => {
      const p = Number(btn.dataset.page);
      if (p >= 1 && p <= totalPages) {
        page = p;
        const c = document.getElementById('docListContainer');
        if (c) renderDocTable(c);
      }
    });
  });
}

function openModal(html) {
  const host = document.getElementById('modalHost');
  host.innerHTML = `<div class="modal-backdrop open" id="modalBackdrop">
    <div class="modal">${html}</div>
  </div>`;
  const backdrop = host.querySelector('#modalBackdrop');
  backdrop.addEventListener('mousedown', (e) => {
    if (e.target === backdrop) closeModal();
  });
  return backdrop;
}

function closeModal() {
  const host = document.getElementById('modalHost');
  host.innerHTML = '';
}

function openUploadModal() {
  const backdrop = openModal(`
    <div class="modal-header">
      <div class="modal-title">${icon('upload', 15)} Upload & ingest document</div>
      <button class="icon-btn" onclick="document.getElementById('modalHost').innerHTML=''">${icon('x', 15)}</button>
    </div>
    <div class="modal-body">
      <div class="dropzone" id="docDropzone">
        <span class="dz-icon">${icon('upload', 26)}</span>
        <strong style="font-size:13.5px">Drag & drop a file here</strong>
        <span style="font-size:12px">or click to browse — PDF, DOCX, TXT supported</span>
        <input type="file" id="docFileInput" accept=".pdf,.docx,.doc,.txt" style="display:none">
      </div>
      <div id="fileMeta" style="display:none;padding:10px 14px;background:var(--bg-2);border:1px solid var(--border);border-radius:8px;font-size:12.5px">
        <strong id="fileName"></strong> · <span id="fileSize" class="mono"></span>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Title</label>
          <input class="input" id="upTitle" placeholder="Leave blank to use filename">
        </div>
        <div class="form-group">
          <label>Department</label>
          <input class="input" id="upDept" placeholder="e.g. Cardiology">
        </div>
      </div>
      <div class="form-group">
        <label>Sensitivity level (Clearance Gating)</label>
        <select class="select" id="upSensitivity">
          <option value="0">Public (Clearance Level 1 - All Staff)</option>
          <option value="1" selected>Internal (Clearance Level 2 - Nurses & Above)</option>
          <option value="2">Confidential (Clearance Level 3 - Specialists & Leads)</option>
          <option value="3">Restricted (Clearance Level 4 - Senior Physicians & Admins)</option>
        </select>
      </div>
    </div>
    <div class="modal-footer">
      <button class="btn btn-ghost" id="upCancelBtn">Cancel</button>
      <button class="btn btn-primary" id="upSubmitBtn">${icon('upload', 14)} Upload & index</button>
    </div>
  `);

  const dz = backdrop.querySelector('#docDropzone');
  const fileInput = backdrop.querySelector('#docFileInput');
  let selectedFile = null;

  dz.addEventListener('click', () => fileInput.click());
  dz.addEventListener('dragover', (e) => { e.preventDefault(); dz.classList.add('drag'); });
  dz.addEventListener('dragleave', () => dz.classList.remove('drag'));
  dz.addEventListener('drop', (e) => {
    e.preventDefault();
    dz.classList.remove('drag');
    if (e.dataTransfer.files.length) selectFile(e.dataTransfer.files[0]);
  });
  fileInput.addEventListener('change', () => {
    if (fileInput.files.length) selectFile(fileInput.files[0]);
  });

  function selectFile(f) {
    selectedFile = f;
    document.getElementById('fileName').textContent = f.name;
    document.getElementById('fileSize').textContent = formatBytes(f.size);
    document.getElementById('fileMeta').style.display = 'block';
  }

  backdrop.querySelector('#upCancelBtn').addEventListener('click', closeModal);
  backdrop.querySelector('#upSubmitBtn').addEventListener('click', async () => {
    if (!selectedFile) return toast('Please select a file first.', 'warning');
    const fd = new FormData();
    fd.append('File', selectedFile);
    fd.append('Title', document.getElementById('upTitle').value.trim());
    fd.append('Department', document.getElementById('upDept').value.trim() || 'General');
    fd.append('SensitivityLevel', document.getElementById('upSensitivity').value);

    const btn = backdrop.querySelector('#upSubmitBtn');
    setLoading(btn, true, 'Uploading…');
    try {
      const res = await api.uploadDocument(fd);
      toast(`Ingested "${res.title}" — ${res.totalChunksCreated} chunks created (trust ${Math.round((res.trustScore || 0) * 100)}%).`, 'success');
      closeModal();
      loadDocs();
    } catch (err) {
      toast(err.message || 'Upload failed.', 'error');
    } finally {
      setLoading(btn, false);
    }
  });
}

function openAddModal() {
  const backdrop = openModal(`
    <div class="modal-header">
      <div class="modal-title">${icon('plus', 15)} Register document</div>
      <button class="icon-btn" onclick="document.getElementById('modalHost').innerHTML=''">${icon('x', 15)}</button>
    </div>
    <div class="modal-body">
      <div class="form-group">
        <label>Title</label>
        <input class="input" id="addTitle" placeholder="e.g. Cardiology Protocol 2026">
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Department</label>
          <input class="input" id="addDept" placeholder="e.g. Cardiology">
        </div>
        <div class="form-group">
          <label>Sensitivity (Clearance Gating)</label>
          <select class="select" id="addSensitivity">
            <option value="0">Public (Clearance Level 1 - All Staff)</option>
            <option value="1" selected>Internal (Clearance Level 2 - Nurses & Above)</option>
            <option value="2">Confidential (Clearance Level 3 - Specialists & Leads)</option>
            <option value="3">Restricted (Clearance Level 4 - Senior Physicians & Admins)</option>
          </select>
        </div>
      </div>
      <div class="form-group">
        <label>Raw text (optional) <span class="form-hint">— indexes content into chunks immediately</span></label>
        <textarea class="textarea" id="addRawText" placeholder="Paste document text here to ingest…" style="min-height:120px"></textarea>
      </div>
    </div>
    <div class="modal-footer">
      <button class="btn btn-ghost" id="addCancelBtn">Cancel</button>
      <button class="btn btn-primary" id="addSubmitBtn">${icon('check', 14)} Create</button>
    </div>
  `);

  backdrop.querySelector('#addCancelBtn').addEventListener('click', closeModal);
  backdrop.querySelector('#addSubmitBtn').addEventListener('click', async () => {
    const title = document.getElementById('addTitle').value.trim();
    if (!title) return toast('A title is required.', 'warning');
    const rawText = document.getElementById('addRawText').value.trim();

    const btn = backdrop.querySelector('#addSubmitBtn');
    setLoading(btn, true, 'Creating…');
    try {
      const doc = await api.createDocument({
        userId: '00000000-0000-0000-0000-000000000000',
        title,
        department: document.getElementById('addDept').value.trim() || 'General',
        sensitivityLevel: Number(document.getElementById('addSensitivity').value),
        digitalSignature: '',
        approvalStatus: 1
      });
      if (rawText) {
        const ing = await api.ingestDocument(doc.documentID, rawText);
        toast(`Document created & ingested — ${ing.totalChunksCreated} chunks indexed.`, 'success');
      } else {
        toast('Document registered.', 'success');
      }
      closeModal();
      loadDocs();
    } catch (err) {
      toast(err.message || 'Create failed.', 'error');
    } finally {
      setLoading(btn, false);
    }
  });
}

async function viewDocument(id) {
  const host = document.getElementById('drawerHost');
  host.innerHTML = `
    <div class="drawer-backdrop open" id="drawerBackdrop"></div>
    <div class="drawer open" id="docDrawer">
      <div class="drawer-header">
        <div class="card-title">${icon('file', 15)} Document details</div>
        <button class="icon-btn" id="drawerCloseBtn">${icon('x', 15)}</button>
      </div>
      <div class="drawer-body">
        <div class="skeleton-row"><div class="skeleton"></div><div class="skeleton" style="width:60%"></div></div>
      </div>
    </div>`;

  const close = () => { host.innerHTML = ''; };
  host.querySelector('#drawerCloseBtn').addEventListener('click', close);
  host.querySelector('#drawerBackdrop').addEventListener('click', close);

  try {
    const doc = await api.getDocument(id);
    const sens = SENSITIVITY[getSensNum(doc.sensitivityLevel)] || SENSITIVITY[1];
    const appr = APPROVAL[getApprNum(doc.approvalStatus)] || APPROVAL[0];
    const body = host.querySelector('.drawer-body');
    body.innerHTML = `
      <div>
        <h3 style="font-size:16px;font-weight:700;margin-bottom:4px;word-break:break-word">${escapeHtml(doc.title)}</h3>
        <div class="mono" style="font-size:11px;color:var(--text-3)">${escapeHtml(doc.documentID)}</div>
      </div>
      <div class="sec-grid">
        <div class="sec-cell"><div class="k">Department</div><div class="v">${escapeHtml(doc.department)}</div></div>
        <div class="sec-cell"><div class="k">Sensitivity</div><div class="v" style="color:var(--${sens.tone})">${escapeHtml(sens.label)}</div></div>
        <div class="sec-cell"><div class="k">Approval</div><div class="v" style="color:var(--${appr.tone})">${escapeHtml(appr.label)}</div></div>
        <div class="sec-cell"><div class="k">Chunks</div><div class="v">${doc.chunkCount ?? 0}</div></div>
      </div>
      <div class="form-group">
        <label>Digital signature</label>
        <div class="mono" style="background:var(--bg-0);border:1px solid var(--border);border-radius:6px;padding:8px 12px;font-size:11.5px;word-break:break-all">${escapeHtml(doc.digitalSignature || '—')}</div>
      </div>
      <div class="form-group">
        <label>Re-index from raw text</label>
        <textarea class="textarea" id="drawerRawText" placeholder="Paste updated content and click index to rebuild chunks…"></textarea>
        <button class="btn btn-secondary btn-sm" id="drawerIngestBtn" style="align-self:flex-start">${icon('refresh', 12)} Re-ingest</button>
      </div>`;

    body.querySelector('#drawerIngestBtn').addEventListener('click', async () => {
      const raw = body.querySelector('#drawerRawText').value.trim();
      if (!raw) return toast('Paste some raw text first.', 'warning');
      const btn = body.querySelector('#drawerIngestBtn');
      setLoading(btn, true, 'Indexing…');
      try {
        const res = await api.ingestDocument(id, raw);
        toast(`Re-indexed — ${res.totalChunksCreated} chunks.`, 'success');
      } catch (err) {
        toast(err.message || 'Ingest failed.', 'error');
      } finally {
        setLoading(btn, false);
      }
    });
  } catch (err) {
    host.querySelector('.drawer-body').innerHTML = `<div class="empty-state">${icon('alert', 26)}<p>${escapeHtml(err.message || 'Could not load document.')}</p></div>`;
  }
}

async function confirmDelete(id) {
  const backdrop = openModal(`
    <div class="modal-header">
      <div class="modal-title">${icon('trash', 15)} Delete document</div>
      <button class="icon-btn" onclick="document.getElementById('modalHost').innerHTML=''">${icon('x', 15)}</button>
    </div>
    <div class="modal-body">
      <p style="font-size:13.5px;color:var(--text-2)">This permanently deletes the document and its indexed chunks. This action requires administrator privileges and cannot be undone.</p>
    </div>
    <div class="modal-footer">
      <button class="btn btn-ghost" id="delCancelBtn">Cancel</button>
      <button class="btn btn-danger" id="delConfirmBtn">${icon('trash', 14)} Delete permanently</button>
    </div>
  `);
  backdrop.querySelector('#delCancelBtn').addEventListener('click', closeModal);
  backdrop.querySelector('#delConfirmBtn').addEventListener('click', async () => {
    const btn = backdrop.querySelector('#delConfirmBtn');
    setLoading(btn, true, 'Deleting…');
    try {
      await api.deleteDocument(id);
      toast('Document deleted.', 'success');
      closeModal();
      loadDocs();
    } catch (err) {
      toast(err.message || 'Delete failed.', 'error');
      setLoading(btn, false);
    }
  });
}
