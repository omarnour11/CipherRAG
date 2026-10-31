import { api } from '../api.js';
import {
  escapeHtml, icon, renderMarkdown, toast, formatDate, timeAgo, shortId,
  VERDICT, riskPercent, riskTone, POISON
} from '../utils.js';

const STORAGE_KEY = 'cipherrag.conversations.v1';
const MAX_CONVOS = 50;

const suggestions = [
  {
    title: 'Summarize a policy document',
    text: 'Give me a concise summary of the zero trust access policy and how ABAC rules are evaluated.',
    icon: 'file'
  },
  {
    title: 'Query the knowledge base',
    text: 'What are the guidelines for handling confidential patient records in the Emergency department?',
    icon: 'database'
  },
  {
    title: 'Explain security controls',
    text: 'Explain how PII masking and risk assessment protect a query before the LLM processes it.',
    icon: 'shield'
  },
  {
    title: 'Investigate an audit entry',
    text: 'Show me the recent blocked requests and why they were flagged as high risk.',
    icon: 'audit'
  }
];

let conversations = [];
let activeId = null;
let busy = false;

export function loadState() {
  try {
    conversations = JSON.parse(localStorage.getItem(STORAGE_KEY)) || [];
  } catch {
    conversations = [];
  }
  if (!conversations.length) createConversation();
}

function persist() {
  conversations = conversations.slice(-MAX_CONVOS);
  localStorage.setItem(STORAGE_KEY, JSON.stringify(conversations));
}

function createConversation() {
  const convo = { id: `c_${Date.now()}`, title: 'New conversation', createdAt: new Date().toISOString(), messages: [] };
  conversations.unshift(convo);
  activeId = convo.id;
  persist();
  return convo;
}

function getActive() {
  return conversations.find(c => c.id === activeId) || createConversation();
}

export function renderChatView(container, setTopbar) {
  setTopbar('Ask RAG');
  container.innerHTML = `
    <div class="chat-layout">
      <div class="chat-convos">
        <div class="chat-convos-header">
          <span style="font-weight:650;font-size:13px;display:flex;align-items:center;gap:8px">
            ${icon('chat', 15)} Conversations
          </span>
          <button class="icon-btn" id="newConvoBtn" title="New conversation">
            ${icon('plus', 16)}
          </button>
        </div>
        <div class="convo-list" id="convoList"></div>
      </div>
      <div class="chat-panel">
        <div class="chat-messages" id="chatMessages"></div>
        <div class="composer-area">
          <div class="composer">
            <textarea id="chatInput" placeholder="Ask anything against the knowledge base…  (Enter to send, Shift+Enter for newline)" rows="1"></textarea>
            <div class="composer-footer">
              <div class="composer-hint">
                ${icon('shield', 12)}
                Queries pass through masking, risk scoring & policy checks
              </div>
              <button class="send-btn" id="sendBtn" title="Send">
                ${icon('send', 18)}
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `;

  renderConvoList();

  const input = container.querySelector('#chatInput');
  const sendBtn = container.querySelector('#sendBtn');
  const convoList = container.querySelector('#convoList');
  const messagesEl = container.querySelector('#chatMessages');

  container.querySelector('#newConvoBtn').addEventListener('click', () => {
    createConversation();
    renderConvoList();
    renderMessages();
  });

  const send = () => {
    const text = input.value.trim();
    if (!text || busy) return;
    submitMessage(text);
    input.value = '';
    input.style.height = 'auto';
  };

  sendBtn.addEventListener('click', send);
  input.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      send();
    }
  });
  input.addEventListener('input', () => {
    input.style.height = 'auto';
    input.style.height = Math.min(input.scrollHeight, 220) + 'px';
  });

  convoList.addEventListener('click', (e) => {
    const item = e.target.closest('.convo-item');
    if (!item) return;
    if (e.target.closest('.c-del')) {
      const id = item.dataset.id;
      conversations = conversations.filter(c => c.id !== id);
      if (activeId === id) activeId = null;
      if (!conversations.length) createConversation();
      persist();
      renderConvoList();
      renderMessages();
      return;
    }
    activeId = item.dataset.id;
    renderConvoList();
    renderMessages();
  });

  renderMessages();
  input.focus();
}

function renderConvoList() {
  const list = document.querySelector('#convoList');
  if (!list) return;
  if (!conversations.length) {
    list.innerHTML = `<div class="empty-state" style="padding:30px 14px">
      ${icon('chat', 28)}<p>No conversations yet</p></div>`;
    return;
  }
  list.innerHTML = conversations.map(c => {
    const last = c.messages[c.messages.length - 1];
    const flagged = c.messages.some(m => m.blocked);
    const summary = last
      ? (last.role === 'user' ? last.text : (last.blocked ? 'Blocked by security' : last.text))
      : 'Empty conversation';
    return `<button class="convo-item ${c.id === activeId ? 'active' : ''}" data-id="${c.id}">
      <div class="c-info">
        <div class="c-title">${escapeHtml(c.title)}</div>
        <div class="c-meta">${timeAgo(c.createdAt)} · ${escapeHtml(truncateStr(summary, 46))}</div>
      </div>
      ${flagged ? `<span class="c-badge flagged">FLAG</span>` : ''}
      ${c.id === activeId ? `<span class="icon-btn c-del" style="width:22px;height:22px">${icon('x', 12)}</span>` : ''}
    </button>`;
  }).join('');
}

function truncateStr(s, n) {
  s = s.replace(/\s+/g, ' ').trim();
  return s.length > n ? s.slice(0, n) + '…' : s;
}

function renderMessages() {
  const el = document.querySelector('#chatMessages');
  if (!el) return;
  const convo = getActive();
  if (!convo.messages.length) {
    el.innerHTML = `
      <div class="chat-welcome">
        <div class="w-title">What would you like to know?</div>
        <div class="w-sub">Every query is evaluated through the security pipeline: policy checks, PII masking, and risk assessment before the model responds.</div>
        <div class="suggestion-grid">
          ${suggestions.map((s, i) => `
            <button class="suggestion" data-i="${i}">
              <span class="sg-icon">${icon(s.icon, 18)}</span>
              <span class="sg-title">${escapeHtml(s.title)}</span>
              <span class="sg-text">${escapeHtml(s.text)}</span>
            </button>`).join('')}
        </div>
      </div>`;
    el.querySelectorAll('.suggestion').forEach(btn => {
      btn.addEventListener('click', () => {
        const s = suggestions[Number(btn.dataset.i)];
        document.querySelector('#chatInput').value = s.text;
        document.querySelector('#chatInput').focus();
      });
    });
    return;
  }

  el.innerHTML = convo.messages.map((m, idx) => renderMessage(m, idx)).join('');
  el.querySelectorAll('.sources-header, .security-head').forEach(h => {
    h.addEventListener('click', () => h.closest('.sources, .security-panel').classList.toggle('open'));
  });
  el.scrollTop = el.scrollHeight;
}

function renderMessage(m, idx) {
  if (m.role === 'user') {
    return `<div class="msg user">
      <div class="msg-avatar">${icon('user', 15)}</div>
      <div class="msg-body">
        <div class="msg-head">
          <span class="msg-author">You</span>
          <span class="msg-time">${formatDate(m.ts)}</span>
        </div>
        <div class="msg-content">${escapeHtml(m.text)}</div>
      </div>
    </div>`;
  }

  const sec = m.security || {};
  const verdictInfo = VERDICT[sec.riskVerdict] || VERDICT[0];
  const scorePct = riskPercent(sec.compositeRiskScore);

  let content = '';
  if (m.blocked) {
    content = `<div class="alert-head">${icon('alert', 15)} SECURITY ALERT — Query Blocked</div>
      <div class="msg-content" style="padding:0">${renderMarkdown(m.text)}</div>`;
  } else {
    content = `<div class="msg-content">${renderMarkdown(m.text)}</div>`;
  }

  const sourcesHtml = m.sources && m.sources.length
    ? `<div class="sources">
        <div class="sources-header">
          <span>${icon('link', 13)} Sources · ${m.sources.length} retrieved</span>
          <span>${icon('chevron', 12)}</span>
        </div>
        <div class="sources-body" style="display:none">
          ${m.sources.map(s => {
            const poison = POISON[s.poisonStatus];
            return `<div class="source-item">
              <div class="s-icon">${icon('file', 14)}</div>
              <div class="s-body">
                <div class="s-title">
                  <span>${escapeHtml(s.documentTitle || 'Untitled')}</span>
                  <span class="badge badge-neutral">${escapeHtml(shortId(s.documentId))}</span>
                  ${poison && s.poisonStatus > 0 ? `<span class="badge badge-${poison.tone}">${escapeHtml(poison.label)}</span>` : ''}
                </div>
                <div class="s-text">${escapeHtml(s.content || '')}</div>
                <div class="s-score">
                  Relevance
                  <div class="score-bar"><div style="width:${Math.round((s.relevanceScore || 0) * 100)}%"></div></div>
                  ${Math.round((s.relevanceScore || 0) * 100)}%
                </div>
              </div>
            </div>`;
          }).join('')}
        </div>
      </div>` : '';

  const securityHtml = `<div class="security-panel ${verdictInfo.tone === 'danger' ? 'critical' : ''}">
    <div class="security-head">
      <span class="sec-title">${icon('shield', 13)} Security evaluation · ${escapeHtml(verdictInfo.label)}</span>
      <span style="display:flex;align-items:center;gap:8px">
        <span class="badge badge-${verdictInfo.tone}">${riskPercent(sec.compositeRiskScore)}% risk</span>
        <span>${icon('chevron', 12)}</span>
      </span>
    </div>
    <div class="security-body" style="display:none">
      <div class="risk-meter risk-meter-${sec.riskVerdict ?? 0}"><div style="width:${scorePct}%"></div></div>
      <div class="sec-grid">
        <div class="sec-cell"><div class="k">Verdict</div><div class="v ${verdictInfo.tone}">${escapeHtml(verdictInfo.label)}</div></div>
        <div class="sec-cell"><div class="k">Risk score</div><div class="v">${scorePct}%</div></div>
        <div class="sec-cell"><div class="k">Grounding</div><div class="v">${Math.round((sec.groundingScore || 0) * 100)}%</div></div>
        <div class="sec-cell"><div class="k">Injection</div><div class="v ${sec.injectionDetected ? 'danger' : 'success'}">${sec.injectionDetected ? 'Detected' : 'None'}</div></div>
        <div class="sec-cell"><div class="k">Poison</div><div class="v ${sec.poisonDetected ? 'warning' : 'success'}">${sec.poisonDetected ? 'Detected' : 'None'}</div></div>
        <div class="sec-cell"><div class="k">Masked tokens</div><div class="v">${sec.maskedTokenCount ?? 0}</div></div>
        <div class="sec-cell"><div class="k">Latency</div><div class="v">${m.latency ?? 0} ms</div></div>
        <div class="sec-cell"><div class="k">Session</div><div class="v mono">${escapeHtml(shortId(sec.sessionId))}</div></div>
      </div>
      ${m.maskedPrompt ? `
        <div class="sec-diff">
          <div class="diff-row"><span class="d-label">Sent to LLM</span><span class="d-val masked">${escapeHtml(truncateStr(m.maskedPrompt, 220))}</span></div>
        </div>` : ''}
    </div>
  </div>`;

  return `<div class="msg assistant ${m.blocked ? 'blocked' : ''}">
    <div class="msg-avatar">${icon(m.blocked ? 'alert' : 'spark', 15)}</div>
    <div class="msg-body">
      <div class="msg-head">
        <span class="msg-author">CipherRAG Assistant</span>
        <span class="msg-time">${formatDate(m.ts)}</span>
      </div>
      ${content}
      ${sourcesHtml}
      ${securityHtml}
    </div>
  </div>`;
}

async function submitMessage(text) {
  const convo = getActive();
  if (convo.title === 'New conversation') {
    convo.title = truncateStr(text, 42);
  }
  convo.messages.push({ role: 'user', text, ts: new Date().toISOString() });
  persist();
  renderConvoList();
  renderMessages();

  busy = true;
  document.querySelector('#sendBtn').disabled = true;

  const el = document.querySelector('#chatMessages');
  const typingEl = document.createElement('div');
  typingEl.className = 'msg assistant';
  typingEl.innerHTML = `
    <div class="msg-avatar">${icon('spark', 15)}</div>
    <div class="msg-body">
      <div class="msg-head"><span class="msg-author">CipherRAG Assistant</span></div>
      <div class="typing-indicator"><span></span><span></span><span></span></div>
    </div>`;
  el.appendChild(typingEl);
  el.scrollTop = el.scrollHeight;

  const start = performance.now();
  try {
    const data = await api.ask(text);
    const latency = Math.round(performance.now() - start);
    typingEl.remove();

    const sec = data.securityMetadata || {};
    const verdict = sec.riskVerdict ?? 0;
    const blocked = verdict >= 3 || String(data.finalAnswer || '').includes('[SECURITY ALERT]');

    convo.messages.push({
      role: 'assistant',
      text: data.finalAnswer || '',
      // rawPrompt intentionally NOT stored — server-side only (HIPAA §164.312)
      maskedPrompt: data.maskedPrompt || '',
      sources: data.retrievedChunks || [],
      security: sec,
      latency,
      blocked,
      ts: new Date().toISOString()
    });
    persist();
    renderConvoList();
    renderMessages();
  } catch (err) {
    typingEl.remove();
    toast(err.message || 'Request failed', 'error');
    convo.messages.push({
      role: 'assistant',
      text: `**Error:** ${err.message || 'The request could not be completed.'}\n\nThis may be due to an authorization policy, rate limit, or a temporarily unavailable security service.`,
      blocked: false,
      security: {},
      latency: Math.round(performance.now() - start),
      ts: new Date().toISOString()
    });
    persist();
    renderConvoList();
    renderMessages();
  } finally {
    busy = false;
    const btn = document.querySelector('#sendBtn');
    if (btn) btn.disabled = false;
  }
}
