import { api } from '../api.js';
import {
  escapeHtml, icon, toast, formatDate, truncate, setLoading
} from '../utils.js';

let users = [];
let page = 1;
let pageSize = 15;
let totalCount = 0;
let totalPages = 0;
let searchTerm = '';

export function renderUsersView(container, setTopbar) {
  setTopbar('Users & Roles');
  container.innerHTML = `
    <div class="toolbar">
      <div class="toolbar-left">
        <div class="search-box">
          ${icon('search', 14)}
          <input type="text" id="userSearch" placeholder="Search users…">
        </div>
        <button class="btn btn-ghost btn-sm" id="userRefreshBtn">${icon('refresh', 13)} Refresh</button>
      </div>
      <div class="toolbar-right">
        <button class="btn btn-primary" id="userAddBtn">${icon('plus', 14)} Add user</button>
      </div>
    </div>

    <div class="view-pad">
      <div class="card">
        <div class="card-header">
          <div class="card-title">${icon('users', 15)} Access & identity</div>
          <div class="badge badge-neutral" id="userCountBadge">0 users</div>
        </div>
        <div id="userTableContainer"></div>
        <div id="userPagination"></div>
      </div>
    </div>
  `;

  container.querySelector('#userSearch').addEventListener('input', debounceSearch());
  container.querySelector('#userRefreshBtn').addEventListener('click', loadUsers);
  container.querySelector('#userAddBtn').addEventListener('click', openAddUserModal);

  loadUsers();
}

function debounceSearch() {
  let t;
  return () => {
    clearTimeout(t);
    t = setTimeout(() => {
      searchTerm = document.getElementById('userSearch').value.trim().toLowerCase();
      page = 1;
      loadUsers();
    }, 300);
  };
}

async function loadUsers() {
  const container = document.getElementById('userTableContainer');
  if (!container) return;
  container.innerHTML = `<div class="skeleton-row">
    <div class="skeleton"></div><div class="skeleton" style="width:70%"></div><div class="skeleton" style="width:45%"></div>
  </div>`;

  try {
    const res = await api.getPagedUsers(page, pageSize);
    users = [...res.items];
    totalCount = res.totalCount;
    totalPages = res.totalPages || 1;
    renderUserTable(container);
    renderPagination();
    document.getElementById('userCountBadge').textContent = `${totalCount} users`;
  } catch (err) {
    container.innerHTML = `<div class="empty-state">
      ${icon('alert', 30)}
      <h3>Could not load users</h3>
      <p>${escapeHtml(err.message || 'An error occurred.')}</p>
      <span class="badge badge-danger">${icon('shield', 11)} Requires Admin role</span>
    </div>`;
  }
}

function renderUserTable(container) {
  let list = users;
  if (searchTerm) {
    list = list.filter(u =>
      u.fullName.toLowerCase().includes(searchTerm) ||
      u.email.toLowerCase().includes(searchTerm) ||
      u.department.toLowerCase().includes(searchTerm));
  }

  if (!list.length) {
    container.innerHTML = `<div class="empty-state">
      ${icon('users', 30)}
      <h3>No users found</h3>
      <p>Provision accounts and assign roles here.</p>
    </div>`;
    return;
  }

  const rows = list.map(u => {
    const roleTone = getRoleTone(u.roleName);
    return `<tr>
      <td>
        <div style="display:flex;align-items:center;gap:10px">
          <div class="avatar" style="width:32px;height:32px;font-size:12px">${escapeHtml(initial(u.fullName))}</div>
          <div style="min-width:0">
            <div class="primary">${escapeHtml(u.fullName)}</div>
            <div style="font-size:11.5px;color:var(--text-3)">${escapeHtml(u.email)}</div>
          </div>
        </div>
      </td>
      <td><span class="badge badge-${roleTone}">${escapeHtml(u.roleName || 'User')}</span></td>
      <td><span class="badge badge-neutral">${escapeHtml(u.department || '—')}</span></td>
      <td>
        <label class="switch">
          <input type="checkbox" data-active-toggle="${u.userId}" ${u.isActive ? 'checked' : ''}>
          <span class="track"></span>
        </label>
      </td>
      <td>
        <div class="row-actions">
          <button class="icon-btn" data-edit-user="${u.userId}" title="Edit">${icon('edit', 14)}</button>
        </div>
      </td>
    </tr>`;
  }).join('');

  container.innerHTML = `<div class="data-table-wrap">
    <table class="data-table">
      <thead><tr>
        <th>User</th><th>Role</th><th>Department</th><th>Active</th><th style="text-align:right">Actions</th>
      </tr></thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;

  container.querySelectorAll('[data-active-toggle]').forEach(toggle => {
    toggle.addEventListener('change', async () => {
      const id = toggle.dataset.activeToggle;
      const user = users.find(u => u.userId === id);
      if (!user) return;
      try {
        await api.updateUser(id, {
          roleId: user.roleId,
          fullName: user.fullName,
          email: user.email,
          department: user.department,
          isActive: toggle.checked
        });
        user.isActive = toggle.checked;
        toast(`User ${toggle.checked ? 'activated' : 'deactivated'}.`, 'success');
      } catch (err) {
        toggle.checked = !toggle.checked;
        toast(err.message || 'Update failed.', 'error');
      }
    });
  });

  container.querySelectorAll('[data-edit-user]').forEach(btn => {
    btn.addEventListener('click', () => {
      const user = users.find(u => u.userId === btn.dataset.editUser);
      if (user) openEditUserModal(user);
    });
  });
}

function initial(name) {
  if (!name) return '?';
  return name.trim().charAt(0).toUpperCase();
}

function getRoleTone(role) {
  const r = (role || '').toLowerCase();
  if (r.includes('admin')) return 'danger';
  if (r.includes('auditor') || r.includes('compliance')) return 'warning';
  if (r.includes('doctor') || r.includes('clinical') || r.includes('physician') || r.includes('lead')) return 'accent';
  if (r.includes('nurse') || r.includes('reception')) return 'info';
  return 'neutral';
}

function renderPagination() {
  const host = document.getElementById('userPagination');
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
    <div class="pagination-info">Page ${page} of ${totalPages} · ${totalCount} users</div>
    <div class="pagination-controls">
      <button class="page-btn" data-page="${page - 1}" ${page <= 1 ? 'disabled' : ''}>${icon('chevron', 12)}</button>
      ${controls}
      <button class="page-btn" data-page="${page + 1}" ${page >= totalPages ? 'disabled' : ''}>${icon('chevron', 12)}</button>
    </div>
  </div>`;
  host.querySelectorAll('.page-btn[data-page]').forEach(btn => {
    btn.addEventListener('click', () => {
      const p = Number(btn.dataset.page);
      if (p >= 1 && p <= totalPages) { page = p; loadUsers(); }
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
  document.getElementById('modalHost').innerHTML = '';
}

async function getRoleOptions(selectedId) {
  try {
    const roles = await api.getRoles();
    if (!Array.isArray(roles) || !roles.length) {
      return '<option value="" disabled selected>No roles available</option>';
    }
    return roles.map(role =>
      `<option value="${role.roleId}" ${role.roleId === selectedId ? 'selected' : ''}>${escapeHtml(role.roleName)}</option>`).join('');
  } catch {
    return '<option value="" disabled selected>Could not load roles</option>';
  }
}

async function openAddUserModal() {
  const roleOptions = await getRoleOptions();
  const backdrop = openModal(`
    <div class="modal-header">
      <div class="modal-title">${icon('plus', 15)} Add user</div>
      <button class="icon-btn" onclick="document.getElementById('modalHost').innerHTML=''">${icon('x', 15)}</button>
    </div>
    <div class="modal-body">
      <div class="form-row">
        <div class="form-group">
          <label>Full name</label>
          <input class="input" id="nuName" placeholder="e.g. Mona Hassan">
        </div>
        <div class="form-group">
          <label>Department</label>
          <input class="input" id="nuDept" placeholder="e.g. Cardiology">
        </div>
      </div>
      <div class="form-group">
        <label>Email</label>
        <input class="input" type="email" id="nuEmail" placeholder="user@cipherrag.local">
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Password</label>
          <input class="input" type="password" id="nuPassword" placeholder="Temporary password">
        </div>
        <div class="form-group">
          <label>Role</label>
          <select class="select" id="nuRole">${roleOptions}</select>
        </div>
      </div>
    </div>
    <div class="modal-footer">
      <button class="btn btn-ghost" id="nuCancelBtn">Cancel</button>
      <button class="btn btn-primary" id="nuSubmitBtn">${icon('check', 14)} Create user</button>
    </div>
  `);

  backdrop.querySelector('#nuCancelBtn').addEventListener('click', closeModal);
  backdrop.querySelector('#nuSubmitBtn').addEventListener('click', async () => {
    const fullName = document.getElementById('nuName').value.trim();
    const email = document.getElementById('nuEmail').value.trim();
    const password = document.getElementById('nuPassword').value;
    const roleId = document.getElementById('nuRole').value;
    if (!fullName || !email || !password || !roleId) return toast('Name, email, password and role are required.', 'warning');

    const btn = backdrop.querySelector('#nuSubmitBtn');
    setLoading(btn, true, 'Creating…');
    try {
      await api.createUser({
        roleId,
        fullName,
        email,
        password,
        department: document.getElementById('nuDept').value.trim() || 'General'
      });
      toast(`User ${email} created.`, 'success');
      closeModal();
      loadUsers();
    } catch (err) {
      toast(err.message || 'Create failed.', 'error');
    } finally {
      setLoading(btn, false);
    }
  });
}

async function openEditUserModal(user) {
  const roleOptions = await getRoleOptions(user.roleId);
  const backdrop = openModal(`
    <div class="modal-header">
      <div class="modal-title">${icon('edit', 15)} Edit user</div>
      <button class="icon-btn" onclick="document.getElementById('modalHost').innerHTML=''">${icon('x', 15)}</button>
    </div>
    <div class="modal-body">
      <div class="form-row">
        <div class="form-group">
          <label>Full name</label>
          <input class="input" id="euName" value="${escapeHtml(user.fullName)}">
        </div>
        <div class="form-group">
          <label>Department</label>
          <input class="input" id="euDept" value="${escapeHtml(user.department || '')}">
        </div>
      </div>
      <div class="form-group">
        <label>Email</label>
        <input class="input" type="email" id="euEmail" value="${escapeHtml(user.email)}">
      </div>
      <div class="form-group">
        <label>Role</label>
        <select class="select" id="euRole">${roleOptions}</select>
      </div>
      <div class="form-group">
        <label style="display:flex;align-items:center;gap:8px;cursor:pointer">
          <input type="checkbox" id="euActive" ${user.isActive ? 'checked' : ''} style="width:16px;height:16px">
          Account active
        </label>
      </div>
    </div>
    <div class="modal-footer">
      <button class="btn btn-ghost" id="euCancelBtn">Cancel</button>
      <button class="btn btn-primary" id="euSubmitBtn">${icon('check', 14)} Save changes</button>
    </div>
  `);

  backdrop.querySelector('#euCancelBtn').addEventListener('click', closeModal);
  backdrop.querySelector('#euSubmitBtn').addEventListener('click', async () => {
    const btn = backdrop.querySelector('#euSubmitBtn');
    setLoading(btn, true, 'Saving…');
    try {
      await api.updateUser(user.userId, {
        roleId: document.getElementById('euRole').value,
        fullName: document.getElementById('euName').value.trim(),
        email: document.getElementById('euEmail').value.trim(),
        department: document.getElementById('euDept').value.trim() || 'General',
        isActive: document.getElementById('euActive').checked
      });
      toast('User updated.', 'success');
      closeModal();
      loadUsers();
    } catch (err) {
      toast(err.message || 'Update failed.', 'error');
    } finally {
      setLoading(btn, false);
    }
  });
}
