import { api } from '../api.js';
import {
  escapeHtml, icon, toast, formatDate, truncate, shortId
} from '../utils.js';

let page = 1;
let pageSize = 15;
let totalCount = 0;
let totalPages = 0;
let logs = [];
let filter = 'all'; // all | allowed | blocked | flagged

export function renderAuditView(container, setTopbar) {
  setTopbar('Audit Trail');
  container.innerHTML = `
    <div class="view-pad" style="padding-top:20px">
      <div class="stats-grid" id="auditStats"></div>

      <div class="toolbar" style="padding:0 0 14px">
        <div class="pill-group" id="auditFilters">
          <button class="pill active" data-f="all">All</button>
          <button class="pill" data-f="allowed">Allowed</button>
          <button class="pill danger" data-f="blocked">Blocked</button>
          <button class="pill danger" data-f="flagged">Flagged</button>
        </div>
        <div class="toolbar-right">
          <button class="btn btn-secondary btn-sm" id="verifyChainBtn">${icon('shield', 13)} Verify Chain Integrity</button>
          <button class="btn btn-ghost btn-sm" id="auditRefreshBtn">${icon('refresh', 13)} Refresh</button>
        </div>
      </div>

      <div class="card">
        <div class="card-header">
          <div class="card-title">${icon('audit', 15)} Immutable request log</div>
          <div class="badge badge-neutral" id="auditCountBadge">0 records</div>
        </div>
        <div id="auditTableContainer"></div>
        <div id="auditPagination"></div>
      </div>
    </div>
  `;

  container.querySelector('#auditRefreshBtn').addEventListener('click', () => {
    loadAudit();
  });

  container.querySelector('#verifyChainBtn').addEventListener('click', async () => {
    const btn = container.querySelector('#verifyChainBtn');
    btn.disabled = true;
    btn.innerHTML = `${icon('refresh', 13)} Verifying...`;
    try {
      const res = await api.verifyAuditChain();
      openVerifyChainModal(res);
    } catch (err) {
      toast(err.message || 'Chain verification failed', 'error');
    } finally {
      btn.disabled = false;
      btn.innerHTML = `${icon('shield', 13)} Verify Chain Integrity`;
    }
  });

  container.querySelectorAll('#auditFilters .pill').forEach(p => {
    p.addEventListener('click', () => {
      container.querySelectorAll('#auditFilters .pill').forEach(x => x.classList.remove('active'));
      p.classList.add('active');
      filter = p.dataset.f;
      page = 1;
      loadAudit();
    });
  });

  loadAudit();
}

async function loadAudit() {
  const container = document.getElementById('auditTableContainer');
  if (!container) return;
  container.innerHTML = `<div class="skeleton-row">
    <div class="skeleton"></div><div class="skeleton" style="width:80%"></div><div class="skeleton" style="width:50%"></div>
  </div>`;

  try {
    if (filter === 'flagged') {
      logs = await api.getFlaggedAuditLogs();
      totalCount = logs.length;
      totalPages = 1;
      page = 1;
      document.getElementById('auditPagination').innerHTML = '';
    } else {
      const res = await api.getPagedAuditLogs(page, pageSize);
      logs = [...res.items];
      totalCount = res.totalCount;
      totalPages = res.totalPages || 1;
      renderPagination();
    }

    let list = logs;
    if (filter === 'allowed') list = logs.filter(l => l.actionStatus !== 'Blocked' && l.actionStatus !== 1);
    if (filter === 'blocked') list = logs.filter(l => l.actionStatus === 'Blocked' || l.actionStatus === 1);

    document.getElementById('auditCountBadge').textContent = `${totalCount} records`;
    renderStats(list);
    renderAuditTable(container, list);
  } catch (err) {
    container.innerHTML = `<div class="empty-state">
      ${icon('alert', 30)}
      <h3>Could not load audit logs</h3>
      <p>${escapeHtml(err.message || 'An error occurred.')}</p>
      <span class="badge badge-danger">${icon('shield', 11)} Requires Admin / Auditor role</span>
    </div>`;
  }
}

function renderStats(list) {
  const host = document.getElementById('auditStats');
  if (!host) return;
  const total = totalCount;
  const blocked = list.filter(l => l.actionStatus === 'Blocked' || l.actionStatus === 1).length;
  const injection = list.filter(l => l.injectionDetected).length;
  const poison = list.filter(l => l.poisonDetected).length;
  const flagged = list.filter(l => l.injectionDetected || l.poisonDetected || l.actionStatus === 'Blocked' || l.actionStatus === 1).length;

  host.innerHTML = `
    ${statCard('accent', 'activity', total, 'Total requests')}
    ${statCard('danger', 'alert', blocked, 'Blocked by security')}
    ${statCard('warning', 'eye', injection, 'Injection detected')}
    ${statCard('danger', 'shield', poison, 'Poisoned context')}
    ${statCard('info', 'flag', flagged, 'Flagged for review')}
  `;
}

function statCard(tone, ic, value, label) {
  return `<div class="stat-card">
    <div class="st-icon ${tone}">${icon(ic, 19)}</div>
    <div>
      <div class="st-value">${value}</div>
      <div class="st-label">${label}</div>
    </div>
  </div>`;
}

function renderAuditTable(container, list) {
  if (!list.length) {
    container.innerHTML = `<div class="empty-state">
      ${icon('audit', 30)}
      <h3>No audit records</h3>
      <p>Queries that pass through the RAG gateway are recorded here with their security verdicts.</p>
    </div>`;
    return;
  }

  const rows = list.map(l => {
    const isBlocked = l.actionStatus === 'Blocked' || l.actionStatus === 1;
    const flagged = l.injectionDetected || l.poisonDetected;
    return `<tr>
      <td><span class="mono" style="font-weight:600">#${l.logId}</span></td>
      <td style="white-space:nowrap">${formatDate(l.tsUtc)}</td>
      <td>
        <div class="primary" style="max-width:300px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">${escapeHtml(truncate(l.rawPrompt || l.maskedPrompt || '', 64))}</div>
      </td>
      <td>
        <div style="display:flex;gap:6px;flex-wrap:wrap">
          <span class="badge badge-${isBlocked ? 'danger' : 'success'}">${isBlocked ? 'Blocked' : 'Allowed'}</span>
          ${l.injectionDetected ? `<span class="badge badge-warning">Injection</span>` : ''}
          ${l.poisonDetected ? `<span class="badge badge-danger">Poison</span>` : ''}
        </div>
      </td>
      <td>
        <div style="display:flex;align-items:center;gap:6px">
          <div class="risk-meter risk-meter-${groundingTone(l.groundingScore)}" style="width:52px"><div style="width:${Math.round((l.groundingScore || 0) * 100)}%"></div></div>
          <span class="mono" style="font-size:11px;color:var(--text-3)">${Math.round((l.groundingScore || 0) * 100)}%</span>
        </div>
      </td>
      <td><span class="mono" style="font-size:11px" title="${escapeHtml(l.rowHash || '')}">${escapeHtml(truncate(l.rowHash || '—', 16))}</span></td>
      <td>
        <div class="row-actions">
          <button class="icon-btn" data-log="${l.logId}">${icon('eye', 14)}</button>
        </div>
      </td>
    </tr>`;
  }).join('');

  container.innerHTML = `<div class="data-table-wrap">
    <table class="data-table">
      <thead><tr>
        <th>#</th><th>Timestamp (UTC)</th><th>Prompt</th><th>Verdict</th><th>Grounding</th><th>Row hash</th><th style="text-align:right">Detail</th>
      </tr></thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;

  container.querySelectorAll('[data-log]').forEach(btn => {
    btn.addEventListener('click', () => openLogDetail(Number(btn.dataset.log), list.find(l => l.logId === Number(btn.dataset.log))));
  });
}

function groundingTone(score) {
  const s = Number(score) || 0;
  if (s < 0.4) return '3';
  if (s < 0.7) return '1';
  return '0';
}

function renderPagination() {
  const host = document.getElementById('auditPagination');
  if (!host) return;
  if (totalPages <= 1) { host.innerHTML = ''; return; }
  let controls = '';
  for (let p = 1; p <= totalPages; p++) {
    if (totalPages > 9 && p > 2 && p < totalPages - 1 && Math.abs(p - page) > 2) {
      if (controls.endsWith('…')) continue;
      controls += `<span style="color:var(--text-3);font-size:12px;padding:0 4px">…</span>`;
      continue;
    }
    controls += `<button class="page-btn ${p === page ? 'active' : ''}" data-page="${p}">${p}</button>`;
  }
  host.innerHTML = `<div class="pagination">
    <div class="pagination-info">Page ${page} of ${totalPages} · ${totalCount} records</div>
    <div class="pagination-controls">
      <button class="page-btn" data-page="${page - 1}" ${page <= 1 ? 'disabled' : ''}>${icon('chevron', 12)}</button>
      ${controls}
      <button class="page-btn" data-page="${page + 1}" ${page >= totalPages ? 'disabled' : ''}>${icon('chevron', 12)}</button>
    </div>
  </div>`;
  host.querySelectorAll('.page-btn[data-page]').forEach(btn => {
    btn.addEventListener('click', () => {
      const p = Number(btn.dataset.page);
      if (p >= 1 && p <= totalPages) { page = p; loadAudit(); }
    });
  });
}

function openLogDetail(id, log) {
  const host = document.getElementById('drawerHost');
  host.innerHTML = `
    <div class="drawer-backdrop open" id="drawerBackdrop"></div>
    <div class="drawer open" id="logDrawer">
      <div class="drawer-header">
        <div class="card-title">${icon('audit', 15)} Audit record #${id}</div>
        <button class="icon-btn" id="drawerCloseBtn">${icon('x', 15)}</button>
      </div>
      <div class="drawer-body">
        ${log ? renderLogDetailBody(log) : '<div class="skeleton-row"><div class="skeleton"></div></div>'}
      </div>
    </div>`;
  const close = () => { host.innerHTML = ''; };
  host.querySelector('#drawerCloseBtn').addEventListener('click', close);
  host.querySelector('#drawerBackdrop').addEventListener('click', close);

  if (!log) {
    api.getAuditLog(id).then(l => {
      host.querySelector('.drawer-body').innerHTML = renderLogDetailBody(l);
    }).catch(() => {
      host.querySelector('.drawer-body').innerHTML = `<div class="empty-state">${icon('alert', 24)}<p>Could not load record.</p></div>`;
    });
  }
}

function renderLogDetailBody(l) {
  const isBlocked = l.actionStatus === 'Blocked' || l.actionStatus === 1;

  return `
    <div>
      <div class="mono" style="font-size:11px;color:var(--text-3)">Record #${l.logId}</div>
      <div class="primary" style="font-size:16px;font-weight:700;margin-top:2px">${formatDate(l.tsUtc)}</div>
      <div style="margin-top:8px">
        <span class="badge badge-${isBlocked ? 'danger' : 'success'}">${isBlocked ? 'Blocked' : 'Allowed'}</span>
        ${l.injectionDetected ? `<span class="badge badge-warning">Injection</span>` : ''}
        ${l.poisonDetected ? `<span class="badge badge-danger">Poison</span>` : ''}
      </div>
    </div>

    <div class="sec-grid">
      <div class="sec-cell"><div class="k">User ID</div><div class="v mono" style="font-size:11px">${escapeHtml(shortId(l.userId))}</div></div>
      <div class="sec-cell"><div class="k">Grounding</div><div class="v">${Math.round((l.groundingScore || 0) * 100)}%</div></div>
      <div class="sec-cell"><div class="k">Injection</div><div class="v ${l.injectionDetected ? 'danger' : 'success'}">${l.injectionDetected ? 'Yes' : 'No'}</div></div>
      <div class="sec-cell"><div class="k">Poison</div><div class="v ${l.poisonDetected ? 'warning' : 'success'}">${l.poisonDetected ? 'Yes' : 'No'}</div></div>
    </div>

    <div class="form-group">
      <label style="display:flex;align-items:center;gap:6px">
        ${icon('shield', 12)}
        Prompt <span class="badge badge-neutral" style="font-size:10px;margin-left:4px">User Query</span>
      </label>
      <div style="background:var(--bg-0);border:1px solid var(--border);border-radius:8px;padding:12px 14px;font-size:13px;line-height:1.8;word-break:break-word">
        ${escapeHtml(l.rawPrompt || l.maskedPrompt || '—')}
      </div>
    </div>

    ${l.maskedPrompt && l.maskedPrompt.includes('[MASKED_') ? `
    <div class="form-group">
      <label style="display:flex;align-items:center;gap:6px">
        ${icon('shield', 12)}
        Masked Prompt (Sanitized for LLM) <span class="badge badge-accent" style="font-size:10px;margin-left:4px">PII Redacted</span>
      </label>
      <div style="background:var(--bg-0);border:1px solid var(--border);border-radius:8px;padding:12px 14px;font-size:13px;line-height:1.8;word-break:break-word">
        ${escapeHtml(l.maskedPrompt).replace(/\[MASKED_([A-Z_]+)_(\d+)\]/g,
            (_, type, num) =>
              `<span style="background:rgba(99,102,241,0.15);color:#a5b4fc;border:1px solid rgba(99,102,241,0.3);border-radius:4px;padding:2px 6px;font-size:11px;font-family:monospace;white-space:nowrap">[MASKED_${type}_${num}]</span>`
          )}
      </div>
      <div class="form-hint">${icon('shield', 11)} Sensitive PII tokens replaced with cryptographic tokens before RAG vector search & LLM inference.</div>
    </div>
    ` : ''}

    <div class="form-group">
      <label style="display:flex;align-items:center;gap:6px">
        ${icon('shield', 12)}
        Final answer <span class="badge badge-accent" style="font-size:10px;margin-left:4px">HIPAA Redacted</span>
      </label>
      <div class="md" style="background:var(--bg-0);border:1px solid var(--border);border-radius:8px;padding:12px 14px;font-size:13px;white-space:pre-wrap;line-height:1.7">
        ${(l.maskedFinalAnswer || l.finalAnswer)
          ? escapeHtml(l.maskedFinalAnswer || l.finalAnswer).replace(/\[MASKED_([A-Z_]+)_(\d+)\]/g,
              (_, type, num) =>
                `<span style="background:rgba(99,102,241,0.15);color:#a5b4fc;border:1px solid rgba(99,102,241,0.3);border-radius:4px;padding:2px 6px;font-size:11px;font-family:monospace;white-space:nowrap">[MASKED_${type}_${num}]</span>`
            )
          : '—'}
      </div>
      <div class="form-hint">${icon('shield', 11)} HIPAA §164.514 & §164.312(b): PHI/PII in final answers is automatically de-identified before storage in the immutable audit trail.</div>
    </div>

    <div class="form-group">
      <label>Tamper-evident chain</label>
      <div class="chain-row" style="flex-wrap:wrap;background:var(--bg-0);border:1px solid var(--border);border-radius:8px;padding:12px 14px">
        <span class="badge badge-accent">Row hash</span>
        <code class="mono" style="font-size:11px;word-break:break-all">${escapeHtml(l.rowHash || '—')}</code>
      </div>
      <div class="form-hint">${icon('shield', 11)} Each record is linked to the previous hash, forming an immutable chain. Any alteration invalidates the chain.</div>
    </div>
  `;
}

function openVerifyChainModal(res) {
  const host = document.getElementById('modalHost');
  const isIntact = res.isChainIntact;
  host.innerHTML = `
    <div class="modal-backdrop open" id="verifyModalBackdrop">
      <div class="modal" style="max-width:540px">
        <div class="modal-header">
          <div class="modal-title">${icon('shield', 16)} Cryptographic Chain Verification</div>
          <button class="icon-btn" id="closeVerifyModalBtn">${icon('x', 15)}</button>
        </div>
        <div class="modal-body">
          <div style="display:flex;align-items:center;gap:12px;margin-bottom:16px">
            <div style="font-size:32px">${isIntact ? '🛡️' : '⚠️'}</div>
            <div>
              <div style="font-size:16px;font-weight:600;color:${isIntact ? 'var(--accent)' : 'var(--danger)'}">
                ${isIntact ? 'Audit Ledger Intact & Verified' : 'Audit Chain Discrepancy Detected'}
              </div>
              <div style="font-size:12px;color:var(--text-muted)">HIPAA §164.312(b) Cryptographic Verification</div>
            </div>
          </div>
          <div class="sec-grid" style="grid-template-columns:repeat(2,1fr);margin-bottom:14px">
            <div class="sec-cell"><div class="k">Total Records Verified</div><div class="v">${res.totalLogsAudited}</div></div>
            <div class="sec-cell"><div class="k">Tampered Records</div><div class="v ${res.tamperedRecords > 0 ? 'danger' : 'success'}">${res.tamperedRecords}</div></div>
          </div>
          <div class="form-group">
            <label>Genesis Block Hash</label>
            <div class="chain-row" style="background:var(--bg-0);padding:8px 10px;border-radius:6px;font-size:11px;word-break:break-all">
              <code>${escapeHtml(res.genesisHash || '—')}</code>
            </div>
          </div>
          <div class="form-group">
            <label>Latest Block Hash</label>
            <div class="chain-row" style="background:var(--bg-0);padding:8px 10px;border-radius:6px;font-size:11px;word-break:break-all">
              <code>${escapeHtml(res.latestHash || '—')}</code>
            </div>
          </div>
          <div class="form-hint" style="margin-top:12px">
            ${escapeHtml(res.message || '')}
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-primary" id="dismissVerifyModalBtn">Done</button>
        </div>
      </div>
    </div>
  `;
  const close = () => { host.innerHTML = ''; };
  host.querySelector('#closeVerifyModalBtn').addEventListener('click', close);
  host.querySelector('#dismissVerifyModalBtn').addEventListener('click', close);
}
