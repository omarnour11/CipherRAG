import { api } from '../api.js';
import {
  escapeHtml, icon, toast, formatDate, truncate, setLoading
} from '../utils.js';

let users = [];
let roles = [];
let currentTab = 'users'; // 'users' | 'roles'
let userPage = 1;
let userPageSize = 15;
let userTotalCount = 0;
let userTotalPages = 0;
let userSearchTerm = '';
let roleSearchTerm = '';

export function renderUsersView(container, setTopbar) {
  setTopbar('Users & Roles');
  container.innerHTML = `
    <div class="view-pad" style="padding-top:16px">
      <div class="toolbar" style="padding:0 0 14px">
        <div class="pill-group" id="usersTabGroup">
          <button class="pill active" data-tab="users">${icon('users', 14)} Users</button>
          <button class="pill" data-tab="roles">${icon('shield', 14)} Roles & Clearances</button>
        </div>
        <div class="toolbar-right">
          <div class="search-box" style="width:240px">
            ${icon('search', 14)}
            <input type="text" id="viewSearch" placeholder="Search users…">
          </div>
          <button class="btn btn-ghost btn-sm" id="viewRefreshBtn">${icon('refresh', 13)} Refresh</button>
          <button class="btn btn-primary btn-sm" id="viewAddBtn">${icon('plus', 14)} Add user</button>
        </div>
      </div>

      <div class="card">
        <div class="card-header">
          <div class="card-title" id="cardTitle">${icon('users', 15)} Access & Identity</div>
          <div class="badge badge-neutral" id="viewCountBadge">0 items</div>
        </div>
        <div id="contentTableContainer"></div>
        <div id="contentPagination"></div>
      </div>
    </div>
  `;

  // Tab switching
  container.querySelectorAll('#usersTabGroup .pill').forEach(btn => {
    btn.addEventListener('click', () => {
      container.querySelectorAll('#usersTabGroup .pill').forEach(b => b.classList.remove('active'));
      btn.classList.add('active');
      currentTab = btn.dataset.tab;
      updateToolbarForTab(container);
      if (currentTab === 'users') loadUsers();
      else loadRoles();
    });
  });

  const searchInput = container.querySelector('#viewSearch');
  searchInput.addEventListener('input', debounce(() => {
    if (currentTab === 'users') {
      userSearchTerm = searchInput.value.trim().toLowerCase();
      userPage = 1;
      const c = document.getElementById('contentTableContainer');
      if (c) renderUserTable(c);
    } else {
      roleSearchTerm = searchInput.value.trim().toLowerCase();
      const c = document.getElementById('contentTableContainer');
      if (c) renderRoleTable(c);
    }
  }, 250));

  container.querySelector('#viewRefreshBtn').addEventListener('click', () => {
    if (currentTab === 'users') loadUsers();
    else loadRoles();
  });

  container.querySelector('#viewAddBtn').addEventListener('click', () => {
    if (currentTab === 'users') openAddUserModal();
    else openRoleModal();
  });

  loadUsers();
}

function updateToolbarForTab(container) {
  const searchInput = container.querySelector('#viewSearch');
  const addBtn = container.querySelector('#viewAddBtn');
  const cardTitle = container.querySelector('#cardTitle');

  if (currentTab === 'users') {
    searchInput.placeholder = 'Search users by name, email, department…';
    searchInput.value = userSearchTerm;
    addBtn.innerHTML = `${icon('plus', 14)} Add user`;
    cardTitle.innerHTML = `${icon('users', 15)} Access & Identity`;
  } else {
    searchInput.placeholder = 'Search roles by name or clearance…';
    searchInput.value = roleSearchTerm;
    addBtn.innerHTML = `${icon('plus', 14)} Add role`;
    cardTitle.innerHTML = `${icon('shield', 15)} System Roles & Clearance Governance`;
  }
}

function debounce(fn, ms) {
  let timer;
  return (...args) => {
    clearTimeout(timer);
    timer = setTimeout(() => fn(...args), ms);
  };
}

// ================= USER MANAGEMENT =================

async function loadUsers() {
  const container = document.getElementById('contentTableContainer');
  if (!container) return;
  container.innerHTML = `<div class="skeleton-row">
    <div class="skeleton"></div><div class="skeleton" style="width:70%"></div><div class="skeleton" style="width:45%"></div>
  </div>`;

  try {
    const res = await api.getPagedUsers(userPage, userPageSize);
    users = Array.isArray(res?.items) ? [...res.items] : [];
    userTotalCount = res?.totalCount ?? users.length;
    userTotalPages = res?.totalPages || 1;
    renderUserTable(container);
    renderUserPagination();
    const badge = document.getElementById('viewCountBadge');
    if (badge) badge.textContent = `${userTotalCount} users`;
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
  if (userSearchTerm) {
    list = list.filter(u =>
      (u.fullName || '').toLowerCase().includes(userSearchTerm) ||
      (u.email || '').toLowerCase().includes(userSearchTerm) ||
      (u.department || '').toLowerCase().includes(userSearchTerm) ||
      (u.roleName || '').toLowerCase().includes(userSearchTerm));
  }

  if (!list.length) {
    container.innerHTML = `<div class="empty-state">
      ${icon('users', 30)}
      <h3>No users found</h3>
      <p>No user accounts match the search criteria.</p>
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
          <button class="icon-btn" data-edit-user="${u.userId}" title="Edit User">${icon('edit', 14)}</button>
          <button class="icon-btn danger" data-delete-user="${u.userId}" title="Delete User">${icon('x', 14)}</button>
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

  container.querySelectorAll('[data-delete-user]').forEach(btn => {
    btn.addEventListener('click', async () => {
      const user = users.find(u => u.userId === btn.dataset.deleteUser);
      if (!user) return;
      if (!confirm(`Are you sure you want to delete user '${user.fullName}' (${user.email})?`)) return;
      try {
        const res = await api.deleteUser(user.userId);
        if (res && res.deactivated) {
          toast(res.message || 'User deactivated due to compliance logs.', 'warning');
        } else {
          toast(res?.message || 'User deleted successfully.', 'success');
        }
        loadUsers();
      } catch (err) {
        toast(err.message || 'Delete failed.', 'error');
      }
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

function renderUserPagination() {
  const host = document.getElementById('contentPagination');
  if (!host) return;
  if (userTotalPages <= 1) { host.innerHTML = ''; return; }
  let controls = '';
  for (let p = 1; p <= userTotalPages; p++) {
    if (userTotalPages > 9 && p > 2 && p < userTotalPages - 1 && Math.abs(p - userPage) > 2) {
      if (controls.endsWith('…')) continue;
      controls += `<span style="color:var(--text-3);font-size:12px;padding:0 4px">…</span>`;
      continue;
    }
    controls += `<button class="page-btn ${p === userPage ? 'active' : ''}" data-page="${p}">${p}</button>`;
  }
  host.innerHTML = `<div class="pagination">
    <div class="pagination-info">Page ${userPage} of ${userTotalPages} · ${userTotalCount} users</div>
    <div class="pagination-controls">
      <button class="page-btn" data-page="${userPage - 1}" ${userPage <= 1 ? 'disabled' : ''}>${icon('chevron', 12)}</button>
      ${controls}
      <button class="page-btn" data-page="${userPage + 1}" ${userPage >= userTotalPages ? 'disabled' : ''}>${icon('chevron', 12)}</button>
    </div>
  </div>`;
  host.querySelectorAll('.page-btn[data-page]').forEach(btn => {
    btn.addEventListener('click', () => {
      const p = Number(btn.dataset.page);
      if (p >= 1 && p <= userTotalPages) { userPage = p; loadUsers(); }
    });
  });
}

// ================= ROLE MANAGEMENT =================

async function loadRoles() {
  const container = document.getElementById('contentTableContainer');
  const pagHost = document.getElementById('contentPagination');
  if (pagHost) pagHost.innerHTML = '';
  if (!container) return;
  container.innerHTML = `<div class="skeleton-row">
    <div class="skeleton"></div><div class="skeleton" style="width:70%"></div><div class="skeleton" style="width:45%"></div>
  </div>`;

  try {
    roles = await api.getRoles();
    if (!Array.isArray(roles)) roles = [];
    renderRoleTable(container);
    const badge = document.getElementById('viewCountBadge');
    if (badge) badge.textContent = `${roles.length} roles`;
  } catch (err) {
    container.innerHTML = `<div class="empty-state">
      ${icon('alert', 30)}
      <h3>Could not load roles</h3>
      <p>${escapeHtml(err.message || 'An error occurred.')}</p>
    </div>`;
  }
}

function renderRoleTable(container) {
  let list = roles;
  if (roleSearchTerm) {
    list = list.filter(r =>
      (r.roleName || '').toLowerCase().includes(roleSearchTerm) ||
      String(r.clinicalClearance ?? '').includes(roleSearchTerm));
  }

  if (!list.length) {
    container.innerHTML = `<div class="empty-state">
      ${icon('shield', 30)}
      <h3>No roles found</h3>
      <p>Create roles to structure clinical clearance and authorization tiers.</p>
    </div>`;
    return;
  }

  const rows = list.map(r => {
    const cat = (r.roleCategory === 0 || r.roleCategory === 'Clinical') ? 'Clinical' : 'Operational';
    const catBadge = cat === 'Clinical' ? 'badge-primary' : 'badge-neutral';
    const polBadge = r.canManagePolicies ? `<span class="badge badge-success">Policies</span>` : `<span class="badge badge-neutral">No</span>`;
    const auditBadge = r.canViewAuditDashboard ? `<span class="badge badge-warning">Audit Trail</span>` : `<span class="badge badge-neutral">No</span>`;

    return `<tr>
      <td style="font-weight:600">
        <div style="display:flex;align-items:center;gap:8px">
          ${icon('shield', 14)}
          <span>${escapeHtml(r.roleName)}</span>
        </div>
      </td>
      <td><span class="badge ${catBadge}">${cat}</span></td>
      <td><span class="badge badge-accent">Clearance Level ${r.clinicalClearance ?? '0'}</span></td>
      <td>${polBadge}</td>
      <td>${auditBadge}</td>
      <td>
        <div class="row-actions">
          <button class="icon-btn" data-edit-role="${r.roleId}" title="Edit Role">${icon('edit', 14)}</button>
          <button class="icon-btn danger" data-delete-role="${r.roleId}" title="Delete Role">${icon('x', 14)}</button>
        </div>
      </td>
    </tr>`;
  }).join('');

  container.innerHTML = `<div class="data-table-wrap">
    <table class="data-table">
      <thead><tr>
        <th>Role Name</th><th>Category</th><th>Clearance</th><th>Policy Admin</th><th>Audit Access</th><th style="text-align:right">Actions</th>
      </tr></thead>
      <tbody>${rows}</tbody>
    </table>
  </div>`;

  container.querySelectorAll('[data-edit-role]').forEach(btn => {
    btn.addEventListener('click', () => {
      const role = roles.find(r => String(r.roleId).toLowerCase() === String(btn.dataset.editRole).toLowerCase());
      if (role) openRoleModal(role);
    });
  });

  container.querySelectorAll('[data-delete-role]').forEach(btn => {
    btn.addEventListener('click', async () => {
      const role = roles.find(r => String(r.roleId).toLowerCase() === String(btn.dataset.deleteRole).toLowerCase());
      if (!role) return;
      if (!confirm(`Are you sure you want to delete role '${role.roleName}'?`)) return;
      try {
        await api.deleteRole(role.roleId);
        toast(`Role '${role.roleName}' deleted successfully.`, 'success');
        loadRoles();
      } catch (err) {
        toast(err.message || 'Cannot delete role.', 'error');
      }
    });
  });
}

// ================= MODALS =================

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
    const rList = await api.getRoles();
    if (!Array.isArray(rList) || !rList.length) {
      return '<option value="" disabled selected>No roles available</option>';
    }
    return rList.map(role => {
      const isSel = selectedId && String(role.roleId).toLowerCase() === String(selectedId).toLowerCase();
      return `<option value="${role.roleId}" ${isSel ? 'selected' : ''}>${escapeHtml(role.roleName)} (L${role.clinicalClearance ?? '0'})</option>`;
    }).join('');
  } catch {
    return '<option value="" disabled selected>Could not load roles</option>';
  }
}

async function openAddUserModal() {
  const roleOptions = await getRoleOptions();
  const backdrop = openModal(`
    <div class="modal-header">
      <div class="modal-title">${icon('plus', 15)} Add User</div>
      <button class="icon-btn" onclick="document.getElementById('modalHost').innerHTML=''">${icon('x', 15)}</button>
    </div>
    <div class="modal-body">
      <div class="form-row">
        <div class="form-group">
          <label>Full name <span style="color:#ef4444">*</span></label>
          <input class="input" id="nuName" placeholder="e.g. Dr. Mona Hassan">
        </div>
        <div class="form-group">
          <label>Department</label>
          <input class="input" id="nuDept" placeholder="e.g. Cardiology">
        </div>
      </div>
      <div class="form-group">
        <label>Email <span style="color:#ef4444">*</span></label>
        <input class="input" type="email" id="nuEmail" placeholder="user@cipherrag.local">
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Password <span style="color:#ef4444">*</span></label>
          <input class="input" type="password" id="nuPassword" placeholder="Temporary password">
        </div>
        <div class="form-group">
          <label>Role <span style="color:#ef4444">*</span></label>
          <select class="select" id="nuRole">${roleOptions}</select>
        </div>
      </div>
    </div>
    <div class="modal-footer">
      <button class="btn btn-ghost" id="nuCancelBtn">Cancel</button>
      <button class="btn btn-primary" id="nuSubmitBtn">${icon('check', 14)} Create User</button>
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
      toast(`User ${email} created successfully.`, 'success');
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
      <div class="modal-title">${icon('edit', 15)} Edit User</div>
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
      toast('User updated successfully.', 'success');
      closeModal();
      loadUsers();
    } catch (err) {
      toast(err.message || 'Update failed.', 'error');
    } finally {
      setLoading(btn, false);
    }
  });
}

function openRoleModal(role = null) {
  const isEdit = !!role;
  const host = document.getElementById('modalHost');
  
  host.innerHTML = `
    <div class="modal-backdrop open" id="roleModalBackdrop">
      <div class="modal" style="max-width:480px">
        <div class="modal-header">
          <div class="modal-title">${icon('shield', 15)} ${isEdit ? 'Edit Role' : 'Create Role'}</div>
          <button class="icon-btn" id="closeRoleModalBtn">${icon('x', 15)}</button>
        </div>
        <div class="modal-body">
          <div class="form-group">
            <label>Role Name <span style="color:#ef4444">*</span></label>
            <input class="input" id="roName" value="${escapeHtml(role?.roleName || '')}" placeholder="e.g. ChiefSurgeon, ComplianceAuditor">
          </div>
          <div class="form-row">
            <div class="form-group">
              <label>Role Category</label>
              <select class="select" id="roCategory">
                <option value="0" ${(!role || role.roleCategory === 0 || role.roleCategory === 'Clinical') ? 'selected' : ''}>Clinical</option>
                <option value="1" ${(role && (role.roleCategory === 1 || role.roleCategory === 'Operational')) ? 'selected' : ''}>Operational</option>
              </select>
            </div>
            <div class="form-group">
              <label>Clearance Level (0 to 5)</label>
              <input class="input" type="number" min="0" max="5" id="roClearance" value="${role?.clinicalClearance ?? 1}">
            </div>
          </div>
          <div class="form-group" style="display:flex;flex-direction:column;gap:8px;margin-top:10px">
            <label style="display:flex;align-items:center;gap:8px;cursor:pointer">
              <input type="checkbox" id="roManagePolicies" ${role?.canManagePolicies ? 'checked' : ''} style="width:16px;height:16px">
              Can manage security policies (ABAC rules)
            </label>
            <label style="display:flex;align-items:center;gap:8px;cursor:pointer">
              <input type="checkbox" id="roViewAudit" ${role?.canViewAuditDashboard ? 'checked' : ''} style="width:16px;height:16px">
              Can view audit trail & logs
            </label>
          </div>
        </div>
        <div class="modal-footer">
          <button class="btn btn-ghost" id="cancelRoleBtn">Cancel</button>
          <button class="btn btn-primary" id="saveRoleBtn">${isEdit ? 'Save Changes' : 'Create Role'}</button>
        </div>
      </div>
    </div>
  `;

  const close = () => { host.innerHTML = ''; };
  host.querySelector('#closeRoleModalBtn').addEventListener('click', close);
  host.querySelector('#cancelRoleBtn').addEventListener('click', close);

  const backdrop = host.querySelector('#roleModalBackdrop');
  backdrop.addEventListener('mousedown', (e) => {
    if (e.target === backdrop) close();
  });

  host.querySelector('#saveRoleBtn').addEventListener('click', async () => {
    const roleName = document.getElementById('roName').value.trim();
    if (!roleName) {
      toast('Role name is required.', 'warning');
      return;
    }
    const roleCategory = Number(document.getElementById('roCategory').value);
    const clinicalClearance = Math.max(0, Math.min(5, Number(document.getElementById('roClearance').value) || 0));
    const canManagePolicies = document.getElementById('roManagePolicies').checked;
    const canViewAuditDashboard = document.getElementById('roViewAudit').checked;

    const btn = host.querySelector('#saveRoleBtn');
    setLoading(btn, true, isEdit ? 'Saving…' : 'Creating…');

    try {
      if (isEdit) {
        await api.updateRole(role.roleId, {
          roleName,
          roleCategory,
          clinicalClearance,
          canManagePolicies,
          canViewAuditDashboard
        });
        toast(`Role '${roleName}' updated successfully.`, 'success');
      } else {
        await api.createRole({
          roleName,
          roleCategory,
          clinicalClearance,
          canManagePolicies,
          canViewAuditDashboard
        });
        toast(`Role '${roleName}' created successfully.`, 'success');
      }
      close();
      loadRoles();
    } catch (err) {
      toast(err.message || 'Operation failed.', 'error');
    } finally {
      setLoading(btn, false);
    }
  });
}
