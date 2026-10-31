export function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

export function debounce(fn, ms) {
  let t;
  return function (...args) {
    clearTimeout(t);
    t = setTimeout(() => fn.apply(this, args), ms);
  };
}

export function formatDate(iso) {
  if (!iso) return '—';
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return '—';
  return d.toLocaleString(undefined, {
    year: 'numeric', month: 'short', day: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit'
  });
}

export function timeAgo(iso) {
  if (!iso) return '';
  const d = new Date(iso);
  const diff = Date.now() - d.getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'just now';
  if (mins < 60) return `${mins}m ago`;
  const hrs = Math.floor(mins / 60);
  if (hrs < 24) return `${hrs}h ago`;
  const days = Math.floor(hrs / 24);
  return `${days}d ago`;
}

export function shortId(guid) {
  if (!guid) return '—';
  return String(guid).slice(0, 8) + '…';
}

export function copyText(text) {
  if (navigator.clipboard && navigator.clipboard.writeText) {
    navigator.clipboard.writeText(text).catch(() => {});
  }
}

export const VERDICT = {
  0: { label: 'Safe', tone: 'success' },
  1: { label: 'Low Risk', tone: 'info' },
  2: { label: 'Medium Risk', tone: 'warning' },
  3: { label: 'High Risk', tone: 'danger' },
  4: { label: 'Critical', tone: 'danger' }
};

export const SENSITIVITY = {
  0: { label: 'Public', tone: 'info' },
  1: { label: 'Internal', tone: 'neutral' },
  2: { label: 'Confidential', tone: 'warning' },
  3: { label: 'Restricted', tone: 'danger' }
};

export const APPROVAL = {
  0: { label: 'Pending', tone: 'warning' },
  1: { label: 'Approved', tone: 'success' },
  2: { label: 'Rejected', tone: 'danger' }
};

export const POISON = {
  0: { label: 'Clean', tone: 'success' },
  1: { label: 'Suspicious', tone: 'warning' },
  2: { label: 'Rejected', tone: 'danger' }
};

export function badge(tone, text) {
  return `<span class="badge badge-${tone}">${escapeHtml(text)}</span>`;
}

export function riskPercent(score) {
  const p = Math.max(0, Math.min(1, Number(score) || 0)) * 100;
  return Math.round(p);
}

export function riskTone(score) {
  const p = Number(score) || 0;
  if (p >= 0.75) return 'danger';
  if (p >= 0.5) return 'warning';
  if (p >= 0.3) return 'info';
  return 'success';
}

export function formatBytes(bytes) {
  if (!bytes && bytes !== 0) return '—';
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 ** 2) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 ** 3) return `${(bytes / 1024 ** 2).toFixed(1)} MB`;
  return `${(bytes / 1024 ** 3).toFixed(2)} GB`;
}

export function truncate(str, n) {
  if (!str) return '';
  return str.length > n ? str.slice(0, n) + '…' : str;
}

export function maskEmail(email) {
  if (!email) return '';
  const [user, domain] = email.split('@');
  if (!domain) return email;
  const first = user.slice(0, 1);
  return `${first}•••@${domain}`;
}

const SVG_NS = 'http://www.w3.org/2000/svg';

export function icon(name, size = 16) {
  const paths = {
    chat: '<path d="M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"/>',
    library: '<path d="M4 19.5A2.5 2.5 0 0 1 6.5 17H20"/><path d="M6.5 2H20v20H6.5A2.5 2.5 0 0 1 4 19.5v-15A2.5 2.5 0 0 1 6.5 2z"/>',
    audit: '<path d="M9 11l3 3L22 4"/><path d="M21 12v7a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11"/>',
    users: '<path d="M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M23 21v-2a4 4 0 0 0-3-3.87"/><path d="M16 3.13a4 4 0 0 1 0 7.75"/>',
    shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>',
    send: '<path d="M22 2L11 13"/><path d="M22 2l-7 20-4-9-9-4z"/>',
    plus: '<path d="M12 5v14"/><path d="M5 12h14"/>',
    search: '<circle cx="11" cy="11" r="8"/><path d="M21 21l-4.35-4.35"/>',
    x: '<path d="M18 6L6 18"/><path d="M6 6l12 12"/>',
    check: '<path d="M20 6L9 17l-5-5"/>',
    alert: '<path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"/><line x1="12" y1="9" x2="12" y2="13"/><line x1="12" y1="17" x2="12.01" y2="17"/>',
    info: '<circle cx="12" cy="12" r="10"/><path d="M12 16v-4"/><path d="M12 8h.01"/>',
    copy: '<rect x="9" y="9" width="13" height="13" rx="2" ry="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>',
    trash: '<path d="M3 6h18"/><path d="M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2"/>',
    edit: '<path d="M11 4H4a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2v-7"/><path d="M18.5 2.5a2.121 2.121 0 0 1 3 3L12 15l-4 1 1-4z"/>',
    upload: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="M17 8l-5-5-5 5"/><path d="M12 3v12"/>',
    logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="M16 17l5-5-5-5"/><path d="M21 12H9"/>',
    refresh: '<path d="M23 4v6h-6"/><path d="M1 20v-6h6"/><path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"/>',
    chevron: '<path d="M6 9l6 6 6-6"/>',
    file: '<path d="M13 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V9z"/><path d="M13 2v7h7"/>',
    key: '<path d="M21 2l-2 2m-7.61 7.61a5.5 5.5 0 1 1-7.778 7.778 5.5 5.5 0 0 1 7.777-7.777zm0 0L15.5 7.5m0 0l3 3L22 7l-3-3m-3.5 3.5L19 4"/>',
    database: '<ellipse cx="12" cy="5" rx="9" ry="3"/><path d="M21 12c0 1.66-4 3-9 3s-9-1.34-9-3"/><path d="M3 5v14c0 1.66 4 3 9 3s9-1.34 9-3V5"/>',
    clock: '<circle cx="12" cy="12" r="10"/><path d="M12 6v6l4 2"/>',
    link: '<path d="M10 13a5 5 0 0 0 7.54.54l3-3a5 5 0 0 0-7.07-7.07l-1.72 1.71"/><path d="M14 11a5 5 0 0 0-7.54-.54l-3 3a5 5 0 0 0 7.07 7.07l1.71-1.71"/>',
    spark: '<path d="M12 3l1.9 5.8a2 2 0 0 0 1.3 1.3L21 12l-5.8 1.9a2 2 0 0 0-1.3 1.3L12 21l-1.9-5.8a2 2 0 0 0-1.3-1.3L3 12l5.8-1.9a2 2 0 0 0 1.3-1.3z"/>',
    eye: '<path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/>',
    eyeOff: '<path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><path d="M1 1l22 22"/>',
    download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="M7 10l5 5 5-5"/><path d="M12 15V3"/>',
    server: '<rect x="2" y="2" width="20" height="8" rx="2"/><rect x="2" y="14" width="20" height="8" rx="2"/><path d="M6 6h.01M6 18h.01"/>',
    activity: '<path d="M22 12h-4l-3 9L9 3l-3 9H2"/>',
    filter: '<path d="M22 3H2l8 9.46V19l4 2v-8.54z"/>',
    more: '<circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/><circle cx="5" cy="12" r="1"/>',
    user: '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
    flag: '<path d="M4 15s1-1 4-1 5 2 8 2 4-1 4-1V3s-1 1-4 1-5-2-8-2-4 1-4 1z"/><line x1="4" y1="22" x2="4" y2="15"/>'
  };
  const p = paths[name] || paths.info;
  return `<svg class="icon" width="${size}" height="${size}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round">${p}</svg>`;
}

const MD_ESCAPE_RE = /[&<>"']/g;
function mdEscape(s) {
  return s.replace(MD_ESCAPE_RE, (ch) => ({
    '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;'
  })[ch]);
}

function inlineMd(s) {
  let out = mdEscape(s);
  out = out.replace(/`([^`]+)`/g, (_, c) => `<code>${c}</code>`);
  out = out.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>');
  out = out.replace(/\*([^*]+)\*/g, '<em>$1</em>');
  out = out.replace(/~~([^~]+)~~/g, '<s>$1</s>');
  out = out.replace(/\[([^\]]+)\]\(([^)]+)\)/g, (_, t, u) => {
    const safe = /^https?:\/\//.test(u) ? u : '#';
    return `<a href="${mdEscape(safe)}" target="_blank" rel="noopener noreferrer">${t}</a>`;
  });
  return out;
}

export function renderMarkdown(md) {
  if (!md) return '';
  const lines = String(md).replace(/\r\n/g, '\n').split('\n');
  const html = [];
  let listType = null;
  let inCode = false;
  let codeBuf = [];
  let codeLang = '';
  let inTable = false;
  let tableRows = [];
  let para = [];

  const flushPara = () => {
    if (para.length) {
      html.push(`<p>${inlineMd(para.join(' '))}</p>`);
      para = [];
    }
  };
  const flushList = () => {
    if (listType) {
      html.push(`</${listType}>`);
      listType = null;
    }
  };
  const flushTable = () => {
    if (inTable && tableRows.length) {
      const head = tableRows[0];
      const body = tableRows.slice(1);
      html.push('<div class="md-table-wrap"><table><thead><tr>' +
        head.map(c => `<th>${inlineMd(c)}</th>`).join('') + '</tr></thead><tbody>' +
        body.map(r => `<tr>${r.map(c => `<td>${inlineMd(c)}</td>`).join('')}</tr>`).join('') +
        '</tbody></table></div>');
      tableRows = [];
      inTable = false;
    }
  };
  const flushCode = () => {
    if (inCode) {
      const lang = codeLang.trim() ? `<span class="md-code-lang">${mdEscape(codeLang.trim())}</span>` : '';
      html.push(`<div class="md-code">${lang}<pre><code>${codeBuf.join('\n')}</code></pre></div>`);
      codeBuf = [];
      codeLang = '';
      inCode = false;
    }
  };

  for (const raw of lines) {
    if (inCode) {
      if (raw.trim().startsWith('```')) {
        flushCode();
      } else {
        codeBuf.push(mdEscape(raw));
      }
      continue;
    }
    if (raw.trim().startsWith('```')) {
      flushPara(); flushList(); flushTable();
      inCode = true;
      codeLang = raw.trim().slice(3);
      continue;
    }
    const line = raw.trim();
    if (!line) {
      flushPara(); flushList(); flushTable();
      continue;
    }
    if (/^\|.*\|$/.test(line)) {
      flushPara(); flushList();
      if (!inTable) {
        inTable = true;
        tableRows = [];
      }
      if (/^[\s:|-]+$/.test(line.replace(/\|/g, ''))) continue;
      tableRows.push(line.split('|').slice(1, -1).map(c => c.trim()));
      continue;
    }
    if (inTable) flushTable();
    if (/^#{1,6}\s/.test(line)) {
      flushPara(); flushList();
      const level = line.match(/^(#{1,6})\s/)[1].length;
      html.push(`<h${level}>${inlineMd(line.replace(/^#{1,6}\s/, ''))}</h${level}>`);
      continue;
    }
    if (/^>\s?/.test(line)) {
      flushPara(); flushList();
      html.push(`<blockquote>${inlineMd(line.replace(/^>\s?/, ''))}</blockquote>`);
      continue;
    }
    if (/^[-*+]\s/.test(line)) {
      flushPara();
      if (listType !== 'ul') { flushList(); listType = 'ul'; html.push('<ul>'); }
      html.push(`<li>${inlineMd(line.replace(/^[-*+]\s/, ''))}</li>`);
      continue;
    }
    if (/^\d+\.\s/.test(line)) {
      flushPara();
      if (listType !== 'ol') { flushList(); listType = 'ol'; html.push('<ol>'); }
      html.push(`<li>${inlineMd(line.replace(/^\d+\.\s/, ''))}</li>`);
      continue;
    }
    if (/^(-{3,}|\*{3,})$/.test(line)) {
      flushPara(); flushList(); flushTable();
      html.push('<hr/>');
      continue;
    }
    para.push(line);
  }
  flushPara(); flushList(); flushTable(); flushCode();
  return html.join('\n');
}

export function toast(message, type = 'success', timeout = 4000) {
  const container = document.getElementById('toast-container');
  if (!container) return;
  const el = document.createElement('div');
  el.className = `toast toast-${type}`;
  const icons = { success: 'check', error: 'alert', warning: 'alert', info: 'info' };
  el.innerHTML = `${icon(icons[type] || 'info', 16)}<span>${escapeHtml(message)}</span>`;
  container.appendChild(el);
  requestAnimationFrame(() => el.classList.add('show'));
  setTimeout(() => {
    el.classList.remove('show');
    setTimeout(() => el.remove(), 300);
  }, timeout);
}

export function setLoading(btn, loading, loadingText = 'Loading…') {
  if (!btn) return;
  if (loading) {
    btn.dataset.original = btn.innerHTML;
    btn.disabled = true;
    btn.classList.add('is-loading');
    btn.innerHTML = `<span class="spinner"></span> ${escapeHtml(loadingText)}`;
  } else {
    btn.disabled = false;
    btn.classList.remove('is-loading');
    if (btn.dataset.original) btn.innerHTML = btn.dataset.original;
  }
}
