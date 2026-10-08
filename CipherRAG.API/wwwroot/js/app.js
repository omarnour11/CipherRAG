import { api, ApiError } from './api.js?v=20260906_4';
import { icon, toast } from './utils.js?v=20260906_4';
import { renderChatView, loadState as loadChatState } from './views/chat.js?v=20260906_4';
import { renderKnowledgeView } from './views/knowledge.js?v=20260906_4';
import { renderAuditView } from './views/audit.js?v=20260906_4';
import { renderUsersView } from './views/users.js?v=20260906_4';
import { renderPoliciesView } from './views/policies.js?v=20260906_4';

let currentUser = null;
let viewContainer = null;
let bound = false;
let healthTimer = null;

const demoAccounts = [
  { email: 'omar.nour@cipherrag.local', label: 'System Admin', role: 'SystemAdmin', icon: 'shield' },
  { email: 'sarah.kassab@cipherrag.local', label: 'Security Analyst', role: 'SecurityAnalyst', icon: 'eye' },
  { email: 'ahmed.mansour@cipherrag.local', label: 'Senior Doctor', role: 'SeniorPhysician', icon: 'activity' },
  { email: 'fatima.soliman@cipherrag.local', label: 'Nurse', role: 'JuniorNurse', icon: 'users' }
];

const VIEW_TITLES = {
  chat: 'Ask RAG',
  knowledge: 'Knowledge Base',
  audit: 'Audit Trail',
  users: 'Users & Roles',
  policies: 'Security Policies (ABAC)'
};

function setTopbar(title) {
  const el = document.getElementById('topbarTitle');
  if (el) el.textContent = title;
}

async function boot() {
  renderDemoAccounts();
  wireLoginForm();
  wireStaticUI();

  try {
    const me = await api.me();
    currentUser = me;
    enterApp(me);
  } catch (err) {
    if (err instanceof ApiError && err.status === 401) {
      showLogin();
    } else {
      showLogin();
      toast('Could not verify session. Make sure the API is running.', 'error');
    }
  }

  window.addEventListener('app:unauthorized', () => {
    currentUser = null;
    showLogin();
    toast('Your session has expired. Please sign in again.', 'warning');
  });
}

function renderDemoAccounts() {
  const host = document.getElementById('demoAccounts');
  host.innerHTML = demoAccounts.map(a => `
    <button class="demo-account" data-email="${a.email}">
      <span style="display:flex;align-items:center;gap:8px">
        ${icon(a.icon, 14)}
        <span>${a.email}</span>
      </span>
      <span class="role-tag">${a.role}</span>
    </button>`).join('');

  host.querySelectorAll('.demo-account').forEach(btn => {
    btn.addEventListener('click', () => {
      document.getElementById('loginEmail').value = btn.dataset.email;
      document.getElementById('loginPassword').value = '562005M';
      document.getElementById('loginForm').requestSubmit();
    });
  });
}

function wireLoginForm() {
  const form = document.getElementById('loginForm');
  form.addEventListener('submit', async (e) => {
    e.preventDefault();
    const email = document.getElementById('loginEmail').value.trim();
    const password = document.getElementById('loginPassword').value;
    hideLoginError();

    const btn = document.getElementById('loginBtn');
    btn.disabled = true;
    btn.innerHTML = `<span class="spinner"></span> Signing in…`;

    try {
      const loginResult = await api.login(email, password);
      if (!loginResult?.accessToken) {
        throw new Error('Sign-in succeeded but no access token was returned.');
      }
      api.setAccessToken(loginResult.accessToken);
      const me = await api.me();
      currentUser = me;
      enterApp(me);
    } catch (err) {
      let msg = err.message || 'Sign-in failed.';
      if (err instanceof ApiError && err.status === 403) msg = 'This account has been deactivated.';
      showLoginError(msg);
    } finally {
      btn.disabled = false;
      btn.innerHTML = `Sign in securely`;
    }
  });
}

// Wire things that never change between login/logout cycles.
function wireStaticUI() {
  if (bound) return;
  bound = true;

  document.getElementById('logoutBtn').addEventListener('click', async () => {
    try { await api.logout(); } catch { /* ignore */ }
    api.clearAccessToken();
    currentUser = null;
    showLogin();
  });

  document.getElementById('nav').addEventListener('click', (e) => {
    const item = e.target.closest('.nav-item');
    if (!item) return;
    switchView(item.dataset.view);
  });

  // Keep health checks running once; only poll while authenticated.
  healthTimer = setInterval(checkHealth, 30000);
}

function showLoginError(msg) {
  document.getElementById('loginErrorText').textContent = msg;
  document.getElementById('loginError').classList.add('show');
}
function hideLoginError() {
  document.getElementById('loginError').classList.remove('show');
}

function showLogin() {
  document.body.classList.remove('authenticated');
}

function enterApp(me) {
  document.body.classList.add('authenticated');
  updateUserCard(me);
  loadChatState();
  switchView('chat');
  checkHealth();
}

function updateUserCard(me) {
  document.getElementById('userName').textContent = me.fullName || me.email;
  document.getElementById('userRole').textContent = `${me.roleName || 'User'} · ${me.department || '—'}`;
  const avatar = document.getElementById('userAvatar');
  avatar.textContent = (me.fullName || me.email || 'U').trim().charAt(0).toUpperCase();
}

function switchView(view) {
  document.querySelectorAll('.nav-item').forEach(n => n.classList.remove('active'));
  const navItem = document.querySelector(`.nav-item[data-view="${view}"]`);
  if (navItem) navItem.classList.add('active');

  viewContainer = document.getElementById('viewContainer');
  setTopbar(VIEW_TITLES[view] || '');

  switch (view) {
    case 'chat':
      renderChatView(viewContainer, setTopbar);
      break;
    case 'knowledge':
      renderKnowledgeView(viewContainer, setTopbar);
      loadDocCount();
      break;
    case 'audit':
      renderAuditView(viewContainer, setTopbar);
      break;
    case 'users':
      renderUsersView(viewContainer, setTopbar);
      break;
    case 'policies':
      renderPoliciesView(viewContainer, setTopbar);
      break;
  }
}

async function loadDocCount() {
  try {
    const res = await api.getDocuments();
    const badge = document.getElementById('navDocCount');
    if (badge && res) badge.textContent = res.length;
  } catch { /* ignore */ }
}

async function checkHealth() {
  if (!document.body.classList.contains('authenticated')) return;
  const status = document.getElementById('connStatus');
  const statusTop = document.getElementById('connStatusTop');
  const label = document.getElementById('connLabel');
  const labelTop = document.getElementById('connLabelTop');

  try {
    await api.health();
    status.classList.add('online');
    status.classList.remove('offline');
    statusTop.classList.add('online');
    statusTop.classList.remove('offline');
    label.textContent = 'System online';
    labelTop.textContent = 'Connected';
  } catch {
    status.classList.remove('online');
    status.classList.add('offline');
    statusTop.classList.remove('online');
    statusTop.classList.add('offline');
    label.textContent = 'Offline';
    labelTop.textContent = 'API unreachable';
  }
}

boot();
